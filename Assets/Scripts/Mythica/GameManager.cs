using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Mythica
{
    public enum Currency { Coins, Food, Crystals, Xp }

    // Owns the game state and rules. Boots itself in any scene, so the project needs no scene setup.
    // The HUD listens to the events below and calls the public methods; nothing here draws UI.
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        public enum Mode { Normal, PlaceBought, PlaceEgg, PlaceDecoration, MoveCreature, MoveDecoration }

        public SaveData Save { get; private set; }
        public readonly List<Creature> Creatures = new List<Creature>();
        public readonly List<Decor> Decorations = new List<Decor>();
        public Island Island { get; private set; }
        public CameraRig Rig { get; private set; }
        public Mode CurrentMode { get; private set; }
        public Creature Selected { get; private set; }
        public Decor SelectedDecor { get; private set; }
        public Creature Soloist { get; private set; }
        public bool Started { get; private set; }

        // What is being placed or moved in the current mode.
        public Species PlacingSpecies { get; private set; }
        public int PlacingEgg { get; private set; }
        public DecorationDef PlacingDecoration { get; private set; }
        public Creature MovingCreature { get; private set; }
        public Decor MovingDecor { get; private set; }

        public event Action<string> Toasted;
        public event Action<Vector3, Currency, int> Gained;   // world position, what, how much
        public event Action<int> LeveledUp;
        public event Action<StructureKind> StructureTapped;
        public event Action SelectionChanged;
        public event Action StateChanged;                      // anything a menu might show changed

        float saveAt;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (Instance == null) new GameObject("Singing Mythica").AddComponent<GameManager>();
        }

        void Awake()
        {
            Instance = this;
            Application.targetFrameRate = 60;
            SetupLight();
            Rig = CameraRig.Create();
            Island = Island.Build();

            Save = SaveSystem.Load() ?? NewGame();
            foreach (var data in Save.creatures)
                if (MythicaDB.Get(data.species) != null) SpawnCreature(data);
            foreach (var d in Save.decorations)
                if (MythicaDB.GetDecoration(d.id) != null) SpawnDecoration(d);
            AudioListener.volume = Save.soundOn ? 1f : 0f;

            gameObject.AddComponent<GameHUD>();
        }

        SaveData NewGame()
        {
            var data = new SaveData
            {
                coins = Progression.StartCoins,
                food = Progression.StartFood,
                crystals = Progression.StartCrystals,
            };
            long now = SaveSystem.Now;
            // Start with a beat and a melody so the island sings from the first second.
            AddStarter(data, "golem", new Vector3(-1.2f, 0, 0), now);
            AddStarter(data, "jackalope", new Vector3(1.2f, 0, 0), now);
            data.decorations.Add(new PlacedDecoration { id = "flowers", slot = Island.NearestSlot(new Vector3(-4.6f, 0, 2.3f)) });
            return data;
        }

        void AddStarter(SaveData data, string id, Vector3 near, long now)
        {
            var s = MythicaDB.Get(id);
            data.creatures.Add(new OwnedCreature { species = id, magic = s.Native, slot = Island.NearestSlot(near), lastCollect = now });
            data.discovered.Add(MythicaDB.VariantKey(id, s.Native));
        }

        void SetupLight()
        {
            if (RenderSettings.sun == null && FindLight() == null)
            {
                var light = new GameObject("Sun").AddComponent<Light>();
                light.type = LightType.Directional;
                light.intensity = 1.1f;
                light.shadows = LightShadows.Soft;
                light.transform.rotation = Quaternion.Euler(50, -30, 0);
                RenderSettings.sun = light;
            }
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.58f, 0.6f, 0.68f);
        }

        static Light FindLight()
        {
#if UNITY_2023_1_OR_NEWER
            return FindAnyObjectByType<Light>();
#else
            return FindObjectOfType<Light>();
#endif
        }

        // Called from the start screen's button: browsers only allow audio after a click.
        public void BeginSession()
        {
            if (Started) return;
            Started = true;
            foreach (var c in Creatures) c.StartSinging();
        }

        void Update()
        {
            long now = SaveSystem.Now;
            var job = Save.breeding;
            if (job.active && now >= job.doneAt && !NestFull)
            {
                var s = MythicaDB.Get(job.species);
                Save.eggs.Add(new Egg { species = job.species, magic = job.magic, hatchAt = now + s.HatchSeconds });
                job.active = false;
                Toast("Breeding done! A " + MythicaDB.VariantName(s, job.magic) + " egg is in the Nursery.");
                Changed();
            }

            if (Time.unscaledTime > saveAt)
            {
                saveAt = Time.unscaledTime + 15f;
                SaveNow();
            }
        }

        void OnApplicationPause(bool paused) { if (paused) SaveNow(); }
        void OnApplicationQuit() { SaveNow(); }

        public void SaveNow() { SaveSystem.Save(Save); }

        void Changed()
        {
            SaveNow();
            if (StateChanged != null) StateChanged();
        }

        public void Toast(string text) { if (Toasted != null) Toasted(text); }

        void Gain(Vector3 world, Currency what, int amount)
        {
            if (amount <= 0) return;
            switch (what)
            {
                case Currency.Coins: Save.coins += amount; break;
                case Currency.Food: Save.food += amount; break;
                case Currency.Crystals: Save.crystals += amount; break;
                case Currency.Xp: AddXp(amount); return;
            }
            if (Gained != null) Gained(world, what, amount);
        }

        bool Spend(Currency what, long amount)
        {
            long have = what == Currency.Coins ? Save.coins : what == Currency.Food ? Save.food : Save.crystals;
            if (have < amount)
            {
                Toast(what == Currency.Coins ? "Not enough coins." : what == Currency.Food ? "Not enough food. Bake some at the Bakery!" : "Not enough crystals.");
                return false;
            }
            if (what == Currency.Coins) Save.coins -= amount;
            else if (what == Currency.Food) Save.food -= amount;
            else Save.crystals -= amount;
            return true;
        }

        // ------------------------------------------------------------ player level

        public int XpToNext { get { return Progression.XpToNext(Save.level); } }

        void AddXp(int amount)
        {
            if (Save.level >= Progression.MaxPlayerLevel) return;
            Save.xp += amount;
            if (Gained != null) Gained(Vector3.zero, Currency.Xp, amount);
            while (Save.level < Progression.MaxPlayerLevel && Save.xp >= XpToNext)
            {
                Save.xp -= XpToNext;
                Save.level++;
                Save.crystals += LevelCrystals(Save.level);
                Save.coins += LevelCoins(Save.level);
                if (LeveledUp != null) LeveledUp(Save.level);
            }
        }

        public static int LevelCrystals(int level) { return level % 5 == 0 ? 3 : 1; }
        public static int LevelCoins(int level) { return 50 * level; }

        // ------------------------------------------------------------ creatures

        Creature SpawnCreature(OwnedCreature data)
        {
            var c = Creature.Spawn(data, Island.CreatureRoot);
            PositionAt(c.transform, data.slot);
            Creatures.Add(c);
            if (Started) c.StartSinging();
            if (Soloist != null) c.SetSoloMuted(true);
            return c;
        }

        void PositionAt(Transform t, int slot)
        {
            t.localPosition = Island.Slots[slot];
            // Face the camera, with a little variety.
            t.localRotation = Quaternion.Euler(0, 180f + (slot * 37 % 30) - 15f, 0);
        }

        // Spots taken by creatures and decorations, leaving out whatever is being moved.
        public HashSet<int> OccupiedSlots()
        {
            var set = new HashSet<int>();
            foreach (var c in Creatures) if (c != MovingCreature) set.Add(c.Data.slot);
            foreach (var d in Decorations) if (d != MovingDecor) set.Add(d.Data.slot);
            return set;
        }

        public bool IslandFull { get { return OccupiedSlots().Count >= Island.Slots.Count; } }

        public void Select(Creature c)
        {
            if (Selected != null) Selected.SetSelected(false);
            SelectedDecor = null;
            Selected = c;
            if (c != null) c.SetSelected(true);
            if (SelectionChanged != null) SelectionChanged();
        }

        void SelectDecor(Decor d)
        {
            if (Selected != null) Selected.SetSelected(false);
            Selected = null;
            SelectedDecor = d;
            if (SelectionChanged != null) SelectionChanged();
        }

        public void ClearSelection() { if (Selected != null || SelectedDecor != null) Select(null); }

        public int Collect(Creature c)
        {
            long now = SaveSystem.Now;
            int coins = c.StoredCoins(now);
            if (coins <= 0) return 0;
            // Advance the timer by whole coins only, so partial progress isn't lost. At the cap, restart.
            long elapsed = now - c.Data.lastCollect;
            if (elapsed >= (long)(MythicaDB.CoinCapMinutes * 60f)) c.Data.lastCollect = now;
            else c.Data.lastCollect += (long)Mathf.Ceil(coins / c.CoinsPerMinute * 60f);
            Gain(c.transform.position + Vector3.up * c.Height, Currency.Coins, coins);
            return coins;
        }

        public int CollectAll()
        {
            int total = 0;
            foreach (var c in Creatures) total += Collect(c);
            if (total > 0) Changed();
            else Toast("Nothing to collect yet. Your creatures are still singing for coins.");
            return total;
        }

        public int PendingCoins()
        {
            long now = SaveSystem.Now;
            int total = 0;
            foreach (var c in Creatures) total += c.StoredCoins(now);
            return total;
        }

        public bool CanFeed(Creature c) { return c.Level < Progression.MaxCreatureLevel; }

        public int FeedCost(Creature c) { return Progression.FeedCost(c.Level); }

        // Feeding fills a creature's level bar. A full bar levels it up: it grows and earns more.
        public void Feed(Creature c)
        {
            if (!CanFeed(c)) { Toast(Name(c) + " is fully grown!"); return; }
            if (!Spend(Currency.Food, FeedCost(c))) return;
            c.Data.feeds++;
            c.Hop();
            Gain(Vector3.zero, Currency.Xp, 2 + c.Level);
            if (c.Data.feeds >= Progression.FeedsPerLevel)
            {
                c.Data.feeds = 0;
                c.Data.level++;
                c.ApplyLevelScale();
                Toast(Name(c) + " grew to level " + c.Level + "!" + (c.Level == Progression.BreedMinLevel ? " It can breed now." : ""));
            }
            Changed();
        }

        public int SellPrice(Creature c)
        {
            int price = c.Species.Cost / 4 + (c.Level - 1) * 10;
            return c.Magic == c.Species.Native ? price : price * 2;
        }

        public void Sell(Creature c)
        {
            if (Creatures.Count <= 1) { Toast("Keep at least one creature on the island."); return; }
            Collect(c);
            int price = SellPrice(c);
            Gain(c.transform.position + Vector3.up * c.Height, Currency.Coins, price);
            if (Soloist == c) SetSolo(null);
            Creatures.Remove(c);
            Save.creatures.Remove(c.Data);
            if (Selected == c) Select(null);
            Toast(Name(c) + " returned to the wild.");
            Destroy(c.gameObject);
            Changed();
        }

        public void ToggleMute(Creature c) { c.SetMuted(!c.Data.muted); Changed(); }

        // Solo: hear one creature alone. Tapping solo again brings the whole chorus back.
        public void ToggleSolo(Creature c) { SetSolo(Soloist == c ? null : c); }

        void SetSolo(Creature c)
        {
            Soloist = c;
            foreach (var other in Creatures) other.SetSoloMuted(c != null && other != c);
            if (StateChanged != null) StateChanged();
        }

        public void ToggleSound()
        {
            Save.soundOn = !Save.soundOn;
            AudioListener.volume = Save.soundOn ? 1f : 0f;
            Changed();
        }

        public static string Name(Creature c) { return MythicaDB.VariantName(c.Species, c.Magic); }

        // ------------------------------------------------------------ market

        public bool TierUnlocked(int tier) { return Save.level >= MythicaDB.TierUnlockLevel[tier - 1]; }

        public void Buy(Species s)
        {
            if (!TierUnlocked(s.Tier)) { Toast("Reach level " + MythicaDB.TierUnlockLevel[s.Tier - 1] + " to unlock " + s.Name + "."); return; }
            if (Save.coins < s.Cost) { Toast("Not enough coins."); return; }
            if (!BeginMode(Mode.PlaceBought)) return;
            PlacingSpecies = s;
        }

        public void BuyDecoration(DecorationDef d)
        {
            if (Save.level < d.Level) { Toast("Reach level " + d.Level + " to unlock " + d.Name + "."); return; }
            if (Save.coins < d.Cost) { Toast("Not enough coins."); return; }
            if (!BeginMode(Mode.PlaceDecoration)) return;
            PlacingDecoration = d;
        }

        // ------------------------------------------------------------ nursery

        public int NestCapacity { get { return Progression.NestCapacity + Save.nurseryLevel - 1; } }
        public bool NestFull { get { return Save.eggs.Count >= NestCapacity; } }
        public bool EggReady(Egg e) { return SaveSystem.Now >= e.hatchAt; }

        public void PlaceEgg(int eggIndex)
        {
            if (eggIndex < 0 || eggIndex >= Save.eggs.Count) return;
            if (!EggReady(Save.eggs[eggIndex])) { Toast("That egg isn't ready to hatch yet."); return; }
            if (!BeginMode(Mode.PlaceEgg)) return;
            PlacingEgg = eggIndex;
        }

        public void SpeedUpEgg(int eggIndex)
        {
            var e = Save.eggs[eggIndex];
            if (!Spend(Currency.Crystals, Progression.SpeedUpCost(e.hatchAt - SaveSystem.Now))) return;
            e.hatchAt = SaveSystem.Now;
            Changed();
        }

        public void UpgradeNursery()
        {
            int lvl = Save.nurseryLevel;
            if (lvl >= Progression.MaxNurseryLevel) return;
            if (Save.level < Progression.UpgradeLevelNeeded(lvl)) { Toast("Reach level " + Progression.UpgradeLevelNeeded(lvl) + " to upgrade the Nursery."); return; }
            if (!Spend(Currency.Coins, Progression.NurseryUpgradeCost(lvl))) return;
            Save.nurseryLevel++;
            Gain(Vector3.zero, Currency.Xp, 20 * lvl);
            Toast("The Nursery now holds " + NestCapacity + " eggs.");
            Changed();
        }

        // ------------------------------------------------------------ breeding grove

        public bool CanBreed(Creature c) { return c.Level >= Progression.BreedMinLevel; }

        public int BreedSeconds(Creature a, Creature b)
        {
            float secs = Progression.BreedSecondsPerTier * Mathf.Max(a.Species.Tier, b.Species.Tier);
            return Mathf.CeilToInt(secs * Progression.GroveSpeed(Save.groveLevel));
        }

        public void StartBreeding(Creature a, Creature b)
        {
            if (Save.breeding.active) { Toast("The Breeding Grove is busy."); return; }
            if (a == null || b == null || a == b) { Toast("Pick two different creatures."); return; }
            if (!CanBreed(a) || !CanBreed(b)) { Toast("Both parents need to be level " + Progression.BreedMinLevel + ". Feed them!"); return; }
            var outcomes = MythicaDB.BreedOutcomes(a.Species, a.Magic, b.Species, b.Magic);
            float roll = Random.value;
            var pick = outcomes[outcomes.Count - 1];
            foreach (var o in outcomes)
            {
                if (roll < o.Chance) { pick = o; break; }
                roll -= o.Chance;
            }
            Save.breeding = new BreedJob
            {
                active = true,
                species = pick.Species.Id,
                magic = pick.Magic,
                doneAt = SaveSystem.Now + BreedSeconds(a, b),
                parents = Name(a) + " + " + Name(b),
            };
            a.Hop();
            b.Hop();
            Gain(Vector3.zero, Currency.Xp, 10);
            Toast("Breeding started: " + Save.breeding.parents);
            Changed();
        }

        public void SpeedUpBreeding()
        {
            var job = Save.breeding;
            if (!job.active) return;
            if (!Spend(Currency.Crystals, Progression.SpeedUpCost(job.doneAt - SaveSystem.Now))) return;
            job.doneAt = SaveSystem.Now;
            if (NestFull) Toast("Make room in the Nursery to receive the egg.");
            Changed();
        }

        public void UpgradeGrove()
        {
            int lvl = Save.groveLevel;
            if (lvl >= Progression.MaxGroveLevel) return;
            if (Save.level < Progression.UpgradeLevelNeeded(lvl)) { Toast("Reach level " + Progression.UpgradeLevelNeeded(lvl) + " to upgrade the Grove."); return; }
            if (!Spend(Currency.Coins, Progression.GroveUpgradeCost(lvl))) return;
            Save.groveLevel++;
            Gain(Vector3.zero, Currency.Xp, 25 * lvl);
            Toast("The Breeding Grove is faster now.");
            Changed();
        }

        // ------------------------------------------------------------ bakery

        public bool BakeReady { get { return Save.bake.active && SaveSystem.Now >= Save.bake.doneAt; } }

        public void StartBake(int recipe)
        {
            if (Save.bake.active) { Toast("The Bakery is busy."); return; }
            var r = MythicaDB.Recipes[recipe];
            if (!Spend(Currency.Coins, r.Cost)) return;
            Save.bake = new BakeJob { active = true, recipe = recipe, doneAt = SaveSystem.Now + r.Seconds };
            Toast("Baking " + r.Name + "...");
            Changed();
        }

        public void CollectBake()
        {
            if (!BakeReady) return;
            var r = MythicaDB.Recipes[Save.bake.recipe];
            Save.bake.active = false;
            Gain(Island.Bakery.transform.position + Vector3.up * 2.4f, Currency.Food, r.Food);
            Gain(Vector3.zero, Currency.Xp, Mathf.Max(2, r.Food / 20));
            Changed();
        }

        public void SpeedUpBake()
        {
            if (!Save.bake.active || BakeReady) return;
            if (!Spend(Currency.Crystals, Progression.SpeedUpCost(Save.bake.doneAt - SaveSystem.Now))) return;
            Save.bake.doneAt = SaveSystem.Now;
            Changed();
        }

        // ------------------------------------------------------------ decorations

        Decor SpawnDecoration(PlacedDecoration data)
        {
            var def = MythicaDB.GetDecoration(data.id);
            var go = Scenery.Decoration(def, Island.DecorRoot);
            var d = go.AddComponent<Decor>();
            d.Data = data;
            d.Def = def;
            PositionAt(go.transform, data.slot);
            Decorations.Add(d);
            return d;
        }

        public int DecorSellPrice(Decor d) { return d.Def.Cost / 2; }

        public void SellDecoration(Decor d)
        {
            Gain(d.transform.position + Vector3.up, Currency.Coins, DecorSellPrice(d));
            Decorations.Remove(d);
            Save.decorations.Remove(d.Data);
            if (SelectedDecor == d) Select(null);
            Destroy(d.gameObject);
            Changed();
        }

        // ------------------------------------------------------------ placing and moving

        bool BeginMode(Mode mode)
        {
            bool moving = mode == Mode.MoveCreature || mode == Mode.MoveDecoration;
            if (!moving && IslandFull) { Toast("The island is full. Sell something to make room."); return false; }
            CancelMode();
            Select(null);
            CurrentMode = mode;
            Island.ShowFreeSlots(OccupiedSlots());
            if (StateChanged != null) StateChanged();
            return true;
        }

        public void BeginMoving(Creature c)
        {
            if (!BeginMode(Mode.MoveCreature)) return;
            MovingCreature = c;
            Island.ShowFreeSlots(OccupiedSlots());
        }

        public void BeginMovingDecor(Decor d)
        {
            if (!BeginMode(Mode.MoveDecoration)) return;
            MovingDecor = d;
            Island.ShowFreeSlots(OccupiedSlots());
        }

        public void CancelMode()
        {
            CurrentMode = Mode.Normal;
            MovingCreature = null;
            MovingDecor = null;
            PlacingSpecies = null;
            PlacingDecoration = null;
            Island.ShowFreeSlots(null);
            if (StateChanged != null) StateChanged();
        }

        // What to show on the banner while placing or moving.
        public string ModeLabel
        {
            get
            {
                switch (CurrentMode)
                {
                    case Mode.PlaceBought: return "Place your " + PlacingSpecies.Name;
                    case Mode.PlaceEgg:
                        var e = Save.eggs[PlacingEgg];
                        return "Hatch your " + MythicaDB.VariantName(MythicaDB.Get(e.species), e.magic);
                    case Mode.PlaceDecoration: return "Place your " + PlacingDecoration.Name;
                    case Mode.MoveCreature: return "Move " + Name(MovingCreature);
                    case Mode.MoveDecoration: return "Move " + MovingDecor.Def.Name;
                    default: return "";
                }
            }
        }

        void PlaceAt(int slot)
        {
            if (OccupiedSlots().Contains(slot)) return;
            switch (CurrentMode)
            {
                case Mode.PlaceBought:
                {
                    var s = PlacingSpecies;
                    if (!Spend(Currency.Coins, s.Cost)) { CancelMode(); return; }
                    var c = AddCreature(s.Id, s.Native, slot);
                    CancelMode();
                    Gain(Vector3.zero, Currency.Xp, s.Cost / 10);
                    Select(c);
                    break;
                }
                case Mode.PlaceEgg:
                {
                    var egg = Save.eggs[PlacingEgg];
                    Save.eggs.RemoveAt(PlacingEgg);
                    var c = AddCreature(egg.species, egg.magic, slot);
                    CancelMode();
                    Gain(Vector3.zero, Currency.Xp, 15 * c.Species.Tier);
                    Select(c);
                    break;
                }
                case Mode.PlaceDecoration:
                {
                    var def = PlacingDecoration;
                    if (!Spend(Currency.Coins, def.Cost)) { CancelMode(); return; }
                    var data = new PlacedDecoration { id = def.Id, slot = slot };
                    Save.decorations.Add(data);
                    SpawnDecoration(data);
                    CancelMode();
                    Gain(Vector3.zero, Currency.Xp, Mathf.Max(3, def.Cost / 10));
                    Toast(def.Name + " placed.");
                    break;
                }
                case Mode.MoveCreature:
                {
                    var c = MovingCreature;
                    c.Data.slot = slot;
                    PositionAt(c.transform, slot);
                    c.Hop();
                    CancelMode();
                    Select(c);
                    break;
                }
                case Mode.MoveDecoration:
                {
                    var d = MovingDecor;
                    d.Data.slot = slot;
                    PositionAt(d.transform, slot);
                    CancelMode();
                    break;
                }
            }
            Changed();
        }

        Creature AddCreature(string species, Magic magic, int slot)
        {
            var data = new OwnedCreature { species = species, magic = magic, slot = slot, lastCollect = SaveSystem.Now };
            Save.creatures.Add(data);
            var c = SpawnCreature(data);
            c.Hop();
            string key = MythicaDB.VariantKey(species, magic);
            if (!Save.discovered.Contains(key))
            {
                Save.discovered.Add(key);
                Gain(c.transform.position + Vector3.up * c.Height, Currency.Crystals, 1);
                Toast("New discovery: " + Name(c) + "!");
            }
            else Toast(Name(c) + " joined the chorus!");
            return c;
        }

        // ------------------------------------------------------------ taps on the world (from CameraRig)

        public void Tap(Ray ray)
        {
            if (!Started) return;

            if (CurrentMode != Mode.Normal)
            {
                // Look through creatures standing in front of the spot that was tapped.
                foreach (var h in Physics.RaycastAll(ray, 200f))
                {
                    int slot = Island.SlotFromCollider(h.collider);
                    if (slot >= 0) { PlaceAt(slot); return; }
                }
                return;
            }

            RaycastHit hit;
            if (!Physics.Raycast(ray, out hit, 200f)) { ClearSelection(); return; }

            var creature = hit.collider.GetComponentInParent<Creature>();
            if (creature != null)
            {
                int coins = Collect(creature);
                creature.Hop();
                Select(creature);
                if (coins > 0) SaveNow();
                return;
            }
            var structure = hit.collider.GetComponentInParent<Structure>();
            if (structure != null)
            {
                ClearSelection();
                if (structure.Kind == StructureKind.Bakery && BakeReady) { CollectBake(); return; }
                if (StructureTapped != null) StructureTapped(structure.Kind);
                return;
            }
            var decor = hit.collider.GetComponentInParent<Decor>();
            if (decor != null) { SelectDecor(decor); return; }
            ClearSelection();
        }

        public void ResetGame()
        {
            SaveSystem.Delete();
            CancelMode();
            SetSolo(null);
            foreach (var c in Creatures) Destroy(c.gameObject);
            foreach (var d in Decorations) Destroy(d.gameObject);
            Creatures.Clear();
            Decorations.Clear();
            Select(null);
            Save = NewGame();
            foreach (var data in Save.creatures) SpawnCreature(data);
            foreach (var d in Save.decorations) SpawnDecoration(d);
            AudioListener.volume = 1f;
            Rig.ResetView();
            Changed();
        }
    }

    // A placed decoration on the island.
    public class Decor : MonoBehaviour
    {
        public PlacedDecoration Data;
        public DecorationDef Def;
    }
}
