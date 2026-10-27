using System.Collections.Generic;
using UnityEngine;

namespace Mythica
{
    // Owns the game state and rules. Boots itself in any scene, so the project needs no scene setup.
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        public const int NestCapacity = 3;
        public const int BreedSecondsPerTier = 20;
        const long StartingCoins = 250;

        public enum Mode { Normal, Placing, Moving }

        public SaveData Save { get; private set; }
        public readonly List<Creature> Creatures = new List<Creature>();
        public Island Island { get; private set; }
        public Mode CurrentMode { get; private set; }
        public Creature Selected { get; private set; }
        public bool Started { get; private set; }
        public int PlacingEgg { get; private set; }
        public Creature MovingCreature { get; private set; }

        public string Toast { get; private set; }
        public float ToastUntil { get; private set; }

        Camera cam;
        float camDistance = 19f;
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
            SetupCameraAndLight();
            Island = Island.Build();

            Save = SaveSystem.Load() ?? NewGame();
            foreach (var data in Save.creatures)
                if (MythicaDB.Get(data.species) != null) SpawnCreature(data);
            AudioListener.volume = Save.soundOn ? 1f : 0f;

            gameObject.AddComponent<GameUI>();
        }

        SaveData NewGame()
        {
            var data = new SaveData { coins = StartingCoins };
            long now = SaveSystem.Now;
            // Start with a beat and a melody so the island sings from the first second.
            AddStarter(data, "golem", new Vector3(0, 0, 0), now);
            AddStarter(data, "jackalope", new Vector3(2.3f, 0, 0), now);
            return data;
        }

        void AddStarter(SaveData data, string id, Vector3 near, long now)
        {
            var s = MythicaDB.Get(id);
            data.creatures.Add(new OwnedCreature { species = id, magic = s.Native, slot = Island.NearestSlot(near), lastCollect = now });
            data.discovered.Add(MythicaDB.VariantKey(id, s.Native));
        }

        void SetupCameraAndLight()
        {
            cam = Camera.main;
            if (cam == null)
            {
                var go = new GameObject("Main Camera");
                go.tag = "MainCamera";
                cam = go.AddComponent<Camera>();
                go.AddComponent<AudioListener>();
            }
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.55f, 0.75f, 0.95f);
            cam.fieldOfView = 45f;
            PlaceCamera();

            if (RenderSettings.sun == null && Light.GetLights(LightType.Directional, 0).Length == 0)
            {
                var light = new GameObject("Sun").AddComponent<Light>();
                light.type = LightType.Directional;
                light.intensity = 1.1f;
                light.shadows = LightShadows.Soft;
                light.transform.rotation = Quaternion.Euler(50, -30, 0);
                RenderSettings.sun = light;
            }
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.55f, 0.58f, 0.65f);
        }

        void PlaceCamera()
        {
            var target = new Vector3(0, 0, 1f);
            var dir = Quaternion.Euler(48, 0, 0) * Vector3.back;
            cam.transform.position = target + dir * camDistance;
            cam.transform.LookAt(target);
        }

        public void Zoom(float delta)
        {
            camDistance = Mathf.Clamp(camDistance + delta * 0.6f, 10f, 26f);
            PlaceCamera();
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
            if (job.active && now >= job.doneAt && Save.eggs.Count < NestCapacity)
            {
                var s = MythicaDB.Get(job.species);
                Save.eggs.Add(new Egg { species = job.species, magic = job.magic, hatchAt = now + s.HatchSeconds });
                job.active = false;
                ShowToast("Breeding done! A " + MythicaDB.VariantName(s, job.magic) + " egg is in the nest.");
                SaveNow();
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

        public void ShowToast(string text)
        {
            Toast = text;
            ToastUntil = Time.unscaledTime + 3.5f;
        }

        // ------------------------------------------------------------ creatures

        Creature SpawnCreature(OwnedCreature data)
        {
            var c = Creature.Spawn(data, Island.CreatureRoot);
            PositionCreature(c);
            Creatures.Add(c);
            if (Started) c.StartSinging();
            return c;
        }

        void PositionCreature(Creature c)
        {
            c.transform.localPosition = Island.Slots[c.Data.slot];
            // Face the camera, with a little variety.
            c.transform.localRotation = Quaternion.Euler(0, 180f + (c.Data.slot * 37 % 30) - 15f, 0);
        }

        public HashSet<int> OccupiedSlots()
        {
            var set = new HashSet<int>();
            foreach (var c in Creatures) set.Add(c.Data.slot);
            return set;
        }

        public bool IslandFull { get { return Creatures.Count >= Island.Slots.Count; } }

        public void Select(Creature c)
        {
            if (Selected != null) Selected.SetSelected(false);
            Selected = c;
            if (c != null) c.SetSelected(true);
        }

        public int Collect(Creature c)
        {
            long now = SaveSystem.Now;
            int coins = c.StoredCoins(now);
            if (coins <= 0) return 0;
            Save.coins += coins;
            // Advance the timer by whole coins only, so partial progress isn't lost. At the cap, restart.
            long elapsed = now - c.Data.lastCollect;
            if (elapsed >= (long)(MythicaDB.CoinCapMinutes * 60f)) c.Data.lastCollect = now;
            else c.Data.lastCollect += (long)Mathf.Ceil(coins / c.CoinsPerMinute * 60f);
            return coins;
        }

        public int CollectAll()
        {
            int total = 0;
            foreach (var c in Creatures) total += Collect(c);
            if (total > 0) ShowToast("Collected " + total + " coins");
            return total;
        }

        public int PendingCoins()
        {
            long now = SaveSystem.Now;
            int total = 0;
            foreach (var c in Creatures) total += c.StoredCoins(now);
            return total;
        }

        public int SellPrice(Creature c)
        {
            int price = c.Species.Cost / 4;
            return c.Magic == c.Species.Native ? price : price * 2;
        }

        public void Sell(Creature c)
        {
            if (Creatures.Count <= 1) { ShowToast("Keep at least one creature on the island."); return; }
            Collect(c);
            Save.coins += SellPrice(c);
            Creatures.Remove(c);
            Save.creatures.Remove(c.Data);
            if (Selected == c) Selected = null;
            ShowToast(MythicaDB.VariantName(c.Species, c.Magic) + " returned to the wild. +" + SellPrice(c) + " coins");
            Destroy(c.gameObject);
            SaveNow();
        }

        public void ToggleMute(Creature c) { c.SetMuted(!c.Data.muted); }

        public void ToggleSound()
        {
            Save.soundOn = !Save.soundOn;
            AudioListener.volume = Save.soundOn ? 1f : 0f;
        }

        // ------------------------------------------------------------ shop and nest

        public bool TierUnlocked(int tier) { return Creatures.Count >= MythicaDB.TierUnlockCount[tier - 1]; }

        public bool NestFull { get { return Save.eggs.Count >= NestCapacity; } }

        public void Buy(Species s)
        {
            if (!TierUnlocked(s.Tier)) { ShowToast("Unlocks when your island has " + MythicaDB.TierUnlockCount[s.Tier - 1] + " creatures."); return; }
            if (NestFull) { ShowToast("The nest is full. Place an egg first."); return; }
            if (Save.coins < s.Cost) { ShowToast("Not enough coins."); return; }
            Save.coins -= s.Cost;
            Save.eggs.Add(new Egg { species = s.Id, magic = s.Native, hatchAt = SaveSystem.Now + s.HatchSeconds });
            ShowToast(s.Name + " egg added to the nest.");
            SaveNow();
        }

        public bool EggReady(Egg e) { return SaveSystem.Now >= e.hatchAt; }

        public void BeginPlacing(int eggIndex)
        {
            if (IslandFull) { ShowToast("The island is full. Return a creature to the wild to make room."); return; }
            Select(null);
            CurrentMode = Mode.Placing;
            PlacingEgg = eggIndex;
            Island.ShowFreeSlots(OccupiedSlots());
        }

        public void BeginMoving(Creature c)
        {
            CurrentMode = Mode.Moving;
            MovingCreature = c;
            Island.ShowFreeSlots(OccupiedSlots());
        }

        public void CancelMode()
        {
            CurrentMode = Mode.Normal;
            MovingCreature = null;
            Island.ShowFreeSlots(null);
        }

        void PlaceEgg(int slot)
        {
            var egg = Save.eggs[PlacingEgg];
            Save.eggs.RemoveAt(PlacingEgg);
            var data = new OwnedCreature { species = egg.species, magic = egg.magic, slot = slot, lastCollect = SaveSystem.Now };
            Save.creatures.Add(data);
            var c = SpawnCreature(data);
            string key = MythicaDB.VariantKey(egg.species, egg.magic);
            if (!Save.discovered.Contains(key))
            {
                Save.discovered.Add(key);
                ShowToast("New discovery: " + MythicaDB.VariantName(c.Species, c.Magic) + "!");
            }
            else ShowToast(MythicaDB.VariantName(c.Species, c.Magic) + " joined the chorus.");
            CancelMode();
            Select(c);
            SaveNow();
        }

        // ------------------------------------------------------------ breeding

        public int BreedSeconds(Creature a, Creature b)
        {
            return BreedSecondsPerTier * Mathf.Max(a.Species.Tier, b.Species.Tier);
        }

        public void StartBreeding(Creature a, Creature b)
        {
            if (Save.breeding.active) { ShowToast("The breeding grove is busy."); return; }
            if (a == null || b == null || a == b) { ShowToast("Pick two different creatures."); return; }
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
                parents = MythicaDB.VariantName(a.Species, a.Magic) + " + " + MythicaDB.VariantName(b.Species, b.Magic),
            };
            ShowToast("Breeding started: " + Save.breeding.parents);
            SaveNow();
        }

        // ------------------------------------------------------------ input from the UI layer

        public void OnWorldClick(Vector2 screenPos)
        {
            if (!Started) return;
            var ray = cam.ScreenPointToRay(screenPos);

            if (CurrentMode != Mode.Normal)
            {
                // Look through creatures standing in front of the spot that was tapped.
                int slot = -1;
                foreach (var h in Physics.RaycastAll(ray, 200f))
                {
                    slot = Island.SlotFromCollider(h.collider);
                    if (slot >= 0) break;
                }
                if (slot < 0) return;
                if (CurrentMode == Mode.Placing) PlaceEgg(slot);
                else
                {
                    MovingCreature.Data.slot = slot;
                    PositionCreature(MovingCreature);
                    CancelMode();
                    SaveNow();
                }
                return;
            }

            RaycastHit hit;
            var creature = Physics.Raycast(ray, out hit, 200f) ? hit.collider.GetComponentInParent<Creature>() : null;
            if (creature != null)
            {
                int coins = Collect(creature);
                Select(creature);
                if (coins > 0) ShowToast("+" + coins + " coins from " + MythicaDB.VariantName(creature.Species, creature.Magic));
            }
            else Select(null);
        }

        public Vector3 WorldToScreen(Vector3 p) { return cam.WorldToScreenPoint(p); }

        public void ResetGame()
        {
            SaveSystem.Delete();
            foreach (var c in Creatures) Destroy(c.gameObject);
            Creatures.Clear();
            Selected = null;
            CancelMode();
            Save = NewGame();
            foreach (var data in Save.creatures) SpawnCreature(data);
            SaveNow();
        }
    }
}
