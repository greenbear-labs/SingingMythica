using System.Collections.Generic;
using UnityEngine;

namespace Mythica
{
    // First-pass UI drawn with IMGUI: no prefabs, canvases or input packages needed.
    // World clicks are also read here, so the game works with either Unity input system.
    public class GameUI : MonoBehaviour
    {
        enum Panel { None, Shop, Nest, Breed, Bestiary }

        GameManager gm;
        Panel panel;
        Vector2 scroll;
        Creature breedA, breedB;
        bool confirmReset;
        float scale = 1f;
        float W, H;
        readonly List<Rect> blockers = new List<Rect>();

        GUIStyle title, label, small, coins, centered, cell;
        bool stylesReady;

        void Awake() { gm = GetComponent<GameManager>(); }

        void InitStyles()
        {
            if (stylesReady) return;
            stylesReady = true;
            GUI.skin.button.fontSize = 16;
            GUI.skin.box.fontSize = 16;
            title = new GUIStyle(GUI.skin.label) { fontSize = 22, fontStyle = FontStyle.Bold, wordWrap = true };
            label = new GUIStyle(GUI.skin.label) { fontSize = 16, wordWrap = true };
            small = new GUIStyle(GUI.skin.label) { fontSize = 13, wordWrap = true };
            coins = new GUIStyle(GUI.skin.label) { fontSize = 22, fontStyle = FontStyle.Bold };
            coins.normal.textColor = new Color(1f, 0.85f, 0.3f);
            centered = new GUIStyle(label) { alignment = TextAnchor.MiddleCenter };
            cell = new GUIStyle(GUI.skin.label) { fontSize = 14, alignment = TextAnchor.MiddleCenter };
        }

        void OnGUI()
        {
            InitStyles();
            scale = Mathf.Min(Screen.height / 720f, Screen.width / 960f);
            W = Screen.width / scale;
            H = Screen.height / scale;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            blockers.Clear();

            if (!gm.Started) { DrawStartScreen(); return; }

            DrawWorldLabels();
            DrawTopBar();
            DrawBottomBar();
            DrawModeBanner();
            if (gm.Selected != null && gm.CurrentMode == GameManager.Mode.Normal) DrawSelected(gm.Selected);
            switch (panel)
            {
                case Panel.Shop: DrawShop(); break;
                case Panel.Nest: DrawNest(); break;
                case Panel.Breed: DrawBreed(); break;
                case Panel.Bestiary: DrawBestiary(); break;
            }
            DrawToast();
            HandleWorldInput();

            // State changes from buttons run after the whole pass, so IMGUI's layout stays consistent.
            if (deferred != null)
            {
                var run = deferred;
                deferred = null;
                run();
            }
        }

        System.Action deferred;
        void Later(System.Action a) { deferred += a; }

        Rect Block(Rect r) { blockers.Add(r); return r; }

        bool OverUI(Vector2 p)
        {
            foreach (var r in blockers) if (r.Contains(p)) return true;
            return false;
        }

        void HandleWorldInput()
        {
            var e = Event.current;
            if (e.type == EventType.MouseDown && e.button == 0 && !OverUI(e.mousePosition))
            {
                gm.OnWorldClick(new Vector2(e.mousePosition.x * scale, Screen.height - e.mousePosition.y * scale));
                e.Use();
            }
            else if (e.type == EventType.ScrollWheel && !OverUI(e.mousePosition))
            {
                gm.Zoom(e.delta.y);
                e.Use();
            }
            else if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape)
            {
                if (gm.CurrentMode != GameManager.Mode.Normal) gm.CancelMode();
                else panel = Panel.None;
                e.Use();
            }
        }

        // ------------------------------------------------------------ screens

        void DrawStartScreen()
        {
            GUI.color = new Color(0, 0, 0, 0.55f);
            GUI.DrawTexture(new Rect(0, 0, W, H), Texture2D.whiteTexture);
            GUI.color = Color.white;
            var box = new Rect(W / 2 - 260, H / 2 - 150, 520, 300);
            GUI.Box(box, "");
            GUILayout.BeginArea(new Rect(box.x + 24, box.y + 20, box.width - 48, box.height - 40));
            GUILayout.Label("Mythica Sanctuary", new GUIStyle(title) { fontSize = 32, alignment = TextAnchor.MiddleCenter });
            GUILayout.Space(8);
            GUILayout.Label("Creatures here aren't captured. They choose to stand beside you, and each one sings its own part.", centered);
            GUILayout.Space(6);
            GUILayout.Label("Buy eggs, hatch them, place them on the island, and breed new magic types to grow the song.", new GUIStyle(small) { alignment = TextAnchor.MiddleCenter });
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Enter the Sanctuary", GUILayout.Height(52))) gm.BeginSession();
            GUILayout.EndArea();
        }

        void DrawTopBar()
        {
            var r = Block(new Rect(0, 0, W, 52));
            GUI.Box(r, "");
            GUI.Label(new Rect(16, 12, 240, 30), gm.Save.coins.ToString("N0") + " coins", coins);
            GUI.Label(new Rect(260, 15, 200, 30), "Island " + gm.Creatures.Count + "/" + gm.Island.Slots.Count, label);
            GUI.Label(new Rect(400, 15, 220, 30), "Discovered " + gm.Save.discovered.Count + "/" + MythicaDB.VariantCount, label);
            int pending = gm.PendingCoins();
            GUI.enabled = pending > 0;
            if (GUI.Button(new Rect(W - 220, 8, 204, 36), "Collect all (+" + pending + ")")) Later(() => gm.CollectAll());
            GUI.enabled = true;
        }

        void DrawBottomBar()
        {
            var r = Block(new Rect(0, H - 64, W, 64));
            GUI.Box(r, "");
            float bw = Mathf.Min(150, (W - 40) / 6f);
            float x = 10;
            Tab(ref x, bw, "Shop", Panel.Shop);
            int ready = 0;
            foreach (var e in gm.Save.eggs) if (gm.EggReady(e)) ready++;
            Tab(ref x, bw, "Nest " + gm.Save.eggs.Count + "/" + GameManager.NestCapacity + (ready > 0 ? " !" : ""), Panel.Nest);
            Tab(ref x, bw, gm.Save.breeding.active ? "Breed ..." : "Breed", Panel.Breed);
            Tab(ref x, bw, "Bestiary", Panel.Bestiary);
            if (GUI.Button(new Rect(x, H - 54, bw, 44), gm.Save.soundOn ? "Sound: on" : "Sound: off")) Later(gm.ToggleSound);
            x += bw + 6;
            if (GUI.Button(new Rect(x, H - 54, bw, 44), confirmReset ? "Really reset?" : "New game"))
            {
                if (confirmReset) Later(() => { gm.ResetGame(); confirmReset = false; panel = Panel.None; });
                else confirmReset = true;
            }
        }

        void Tab(ref float x, float w, string text, Panel p)
        {
            bool on = panel == p;
            GUI.backgroundColor = on ? new Color(1f, 0.85f, 0.4f) : Color.white;
            if (GUI.Button(new Rect(x, H - 54, w, 44), text))
            {
                Later(() => { panel = on ? Panel.None : p; scroll = Vector2.zero; confirmReset = false; });
            }
            GUI.backgroundColor = Color.white;
            x += w + 6;
        }

        void DrawModeBanner()
        {
            if (gm.CurrentMode == GameManager.Mode.Normal) return;
            string what;
            if (gm.CurrentMode == GameManager.Mode.Placing)
            {
                var egg = gm.Save.eggs[gm.PlacingEgg];
                what = MythicaDB.VariantName(MythicaDB.Get(egg.species), egg.magic);
            }
            else what = MythicaDB.VariantName(gm.MovingCreature.Species, gm.MovingCreature.Magic);
            var r = Block(new Rect(W / 2 - 230, 62, 460, 50));
            GUI.Box(r, "");
            GUI.Label(new Rect(r.x + 12, r.y + 13, 330, 26), "Tap a glowing spot for " + what, label);
            if (GUI.Button(new Rect(r.xMax - 100, r.y + 8, 90, 34), "Cancel")) Later(gm.CancelMode);
        }

        void DrawSelected(Creature c)
        {
            var r = Block(new Rect(10, H - 64 - 230, 340, 222));
            GUI.Box(r, "");
            GUILayout.BeginArea(new Rect(r.x + 12, r.y + 8, r.width - 24, r.height - 16));
            GUILayout.Label(MythicaDB.VariantName(c.Species, c.Magic), title);
            MagicTag(c.Magic, c.Magic != c.Species.Native);
            GUILayout.Label("Sings: " + PartName(c.Species.Part) + "   Earns " + c.CoinsPerMinute.ToString("0") + "/min", small);
            GUILayout.Label(c.Species.Blurb, small);
            GUILayout.FlexibleSpace();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(c.Data.muted ? "Unmute" : "Mute", GUILayout.Height(36))) Later(() => gm.ToggleMute(c));
            if (GUILayout.Button("Move", GUILayout.Height(36))) Later(() => gm.BeginMoving(c));
            if (GUILayout.Button("Release +" + gm.SellPrice(c), GUILayout.Height(36))) Later(() => gm.Sell(c));
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        Rect PanelRect()
        {
            float w = Mathf.Min(420, W - 20);
            return Block(new Rect(W - w - 10, 62, w, H - 62 - 74));
        }

        bool PanelHeader(Rect r, string text)
        {
            GUI.Box(r, "");
            GUI.Label(new Rect(r.x + 14, r.y + 8, r.width - 70, 32), text, title);
            if (GUI.Button(new Rect(r.xMax - 46, r.y + 8, 36, 32), "X")) Later(() => panel = Panel.None);
            return true;
        }

        void DrawShop()
        {
            var r = PanelRect();
            if (!PanelHeader(r, "Shop")) return;
            GUILayout.BeginArea(new Rect(r.x + 10, r.y + 48, r.width - 20, r.height - 56));
            scroll = GUILayout.BeginScrollView(scroll);
            for (int tier = 1; tier <= 4; tier++)
            {
                bool unlocked = gm.TierUnlocked(tier);
                GUILayout.Label("Tier " + tier + (unlocked ? "" : "  (unlocks at " + MythicaDB.TierUnlockCount[tier - 1] + " creatures)"), label);
                foreach (var s in MythicaDB.All)
                {
                    if (s.Tier != tier) continue;
                    GUILayout.BeginHorizontal(GUI.skin.box);
                    Swatch(MythicaDB.MagicColor(s.Native));
                    GUILayout.BeginVertical();
                    GUILayout.Label(s.Name + "  -  " + s.Native, label);
                    GUILayout.Label(PartName(s.Part) + ", " + MythicaDB.CoinsPerMinute(s, s.Native).ToString("0") + " coins/min, hatches in " + Duration(s.HatchSeconds), small);
                    GUILayout.EndVertical();
                    GUI.enabled = unlocked && gm.Save.coins >= s.Cost && !gm.NestFull;
                    if (GUILayout.Button(s.Cost.ToString("N0"), GUILayout.Width(80), GUILayout.Height(40))) Later(() => gm.Buy(s));
                    GUI.enabled = true;
                    GUILayout.EndHorizontal();
                }
            }
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        void DrawNest()
        {
            var r = PanelRect();
            if (!PanelHeader(r, "Nest")) return;
            GUILayout.BeginArea(new Rect(r.x + 10, r.y + 48, r.width - 20, r.height - 56));
            GUILayout.Label("Eggs hatch over time, even while you're away. Up to " + GameManager.NestCapacity + " at once.", small);
            if (gm.Save.eggs.Count == 0) GUILayout.Label("The nest is empty. Buy an egg in the Shop or breed two creatures.", label);
            for (int i = 0; i < gm.Save.eggs.Count; i++)
            {
                var egg = gm.Save.eggs[i];
                var s = MythicaDB.Get(egg.species);
                GUILayout.BeginHorizontal(GUI.skin.box);
                Swatch(MythicaDB.MagicColor(egg.magic));
                GUILayout.Label(MythicaDB.VariantName(s, egg.magic) + " egg", label);
                if (gm.EggReady(egg))
                {
                    if (GUILayout.Button("Place", GUILayout.Width(90), GUILayout.Height(36)))
                    {
                        int index = i;
                        Later(() => { gm.BeginPlacing(index); panel = Panel.None; });
                    }
                }
                else GUILayout.Label(Duration((int)(egg.hatchAt - SaveSystem.Now)), label, GUILayout.Width(90));
                GUILayout.EndHorizontal();
            }
            var job = gm.Save.breeding;
            if (job.active)
            {
                GUILayout.Space(12);
                long left = job.doneAt - SaveSystem.Now;
                GUILayout.Label(left > 0 ? "Breeding " + job.parents + ": " + Duration((int)left)
                                         : "Breeding finished. The egg arrives when the nest has room.", small);
            }
            GUILayout.EndArea();
        }

        void DrawBreed()
        {
            var r = PanelRect();
            if (!PanelHeader(r, "Breeding grove")) return;
            GUILayout.BeginArea(new Rect(r.x + 10, r.y + 48, r.width - 20, r.height - 56));
            var job = gm.Save.breeding;
            if (job.active)
            {
                long left = job.doneAt - SaveSystem.Now;
                GUILayout.Label(job.parents, label);
                GUILayout.Label(left > 0 ? "An egg is on the way: " + Duration((int)left) : "Done! Make room in the nest for the egg.", label);
                GUILayout.EndArea();
                return;
            }
            if (!gm.Creatures.Contains(breedA)) breedA = null;
            if (!gm.Creatures.Contains(breedB)) breedB = null;

            GUILayout.Label("Pick two creatures. With different magic, there's a 50/50 chance of either creature taking the other's magic.", small);
            GUILayout.Label("A: " + Name(breedA) + "      B: " + Name(breedB), label);
            if (breedA != null && breedB != null)
            {
                foreach (var o in MythicaDB.BreedOutcomes(breedA.Species, breedA.Magic, breedB.Species, breedB.Magic))
                    GUILayout.Label(Mathf.RoundToInt(o.Chance * 100) + "%  " + MythicaDB.VariantName(o.Species, o.Magic)
                        + (gm.Save.discovered.Contains(MythicaDB.VariantKey(o.Species.Id, o.Magic)) ? "" : "  (new!)"), small);
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Breed (" + Duration(gm.BreedSeconds(breedA, breedB)) + ")", GUILayout.Height(40)))
                {
                    var a = breedA;
                    var b = breedB;
                    Later(() => { gm.StartBreeding(a, b); breedA = breedB = null; });
                }
                if (GUILayout.Button("Clear", GUILayout.Width(80), GUILayout.Height(40))) Later(() => breedA = breedB = null);
                GUILayout.EndHorizontal();
            }
            scroll = GUILayout.BeginScrollView(scroll);
            foreach (var c in gm.Creatures)
            {
                bool picked = c == breedA || c == breedB;
                GUI.backgroundColor = picked ? new Color(1f, 0.85f, 0.4f) : Color.white;
                GUILayout.BeginHorizontal(GUI.skin.box);
                Swatch(MythicaDB.MagicColor(c.Magic));
                if (GUILayout.Button(MythicaDB.VariantName(c.Species, c.Magic), GUILayout.Height(32)))
                {
                    var picked2 = c;
                    Later(() =>
                    {
                        if (picked2 == breedA) breedA = null;
                        else if (picked2 == breedB) breedB = null;
                        else if (breedA == null) breedA = picked2;
                        else breedB = picked2;
                    });
                }
                GUILayout.EndHorizontal();
                GUI.backgroundColor = Color.white;
            }
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        void DrawBestiary()
        {
            var r = PanelRect();
            if (!PanelHeader(r, "Bestiary")) return;
            GUILayout.BeginArea(new Rect(r.x + 10, r.y + 48, r.width - 20, r.height - 56));
            GUILayout.Label("Every creature can carry each of the six powers. Breed to find them all.", small);
            GUILayout.BeginHorizontal();
            GUILayout.Label("", GUILayout.Width(130));
            foreach (var m in MythicaDB.AllMagic)
            {
                GUI.contentColor = MythicaDB.MagicColor(m);
                GUILayout.Label(m.ToString().Substring(0, 2), cell, GUILayout.Width(36));
            }
            GUI.contentColor = Color.white;
            GUILayout.EndHorizontal();
            scroll = GUILayout.BeginScrollView(scroll);
            foreach (var s in MythicaDB.All)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label(s.Name, small, GUILayout.Width(130));
                foreach (var m in MythicaDB.AllMagic)
                {
                    bool found = gm.Save.discovered.Contains(MythicaDB.VariantKey(s.Id, m));
                    GUI.contentColor = found ? MythicaDB.MagicColor(m) : new Color(1, 1, 1, 0.3f);
                    GUILayout.Label(found ? "●" : (m == s.Native ? "○" : "·"), cell, GUILayout.Width(36));
                }
                GUI.contentColor = Color.white;
                GUILayout.EndHorizontal();
            }
            GUILayout.EndScrollView();
            GUILayout.Label("● found   ○ native magic, buy in the Shop   · breed to find", small);
            GUILayout.EndArea();
        }

        void DrawWorldLabels()
        {
            long now = SaveSystem.Now;
            foreach (var c in gm.Creatures)
            {
                int stored = c.StoredCoins(now);
                if (stored <= 0) continue;
                Vector3 sp = gm.WorldToScreen(c.transform.position + Vector3.up * (c.Height + 0.3f));
                if (sp.z < 0) continue;
                var p = new Vector2(sp.x / scale, (Screen.height - sp.y) / scale);
                var rect = new Rect(p.x - 32, p.y - 14, 64, 26);
                GUI.Box(rect, "+" + stored);
            }
        }

        void DrawToast()
        {
            if (gm.Toast == null || Time.unscaledTime > gm.ToastUntil) return;
            var r = new Rect(W / 2 - 260, gm.CurrentMode == GameManager.Mode.Normal ? 62 : 120, 520, 40);
            GUI.Box(r, "");
            GUI.Label(r, gm.Toast, centered);
        }

        // ------------------------------------------------------------ helpers

        void Swatch(Color c)
        {
            var r = GUILayoutUtility.GetRect(22, 22, GUILayout.Width(22), GUILayout.Height(22));
            r.y += 6;
            GUI.color = c;
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = Color.white;
        }

        void MagicTag(Magic m, bool bred)
        {
            GUI.contentColor = MythicaDB.MagicColor(m);
            GUILayout.Label(m + " magic" + (bred ? " (bred, earns x" + MythicaDB.BredIncomeBonus + ")" : ""), label);
            GUI.contentColor = Color.white;
        }

        static string Name(Creature c) { return c == null ? "-" : MythicaDB.VariantName(c.Species, c.Magic); }

        static string Duration(int seconds)
        {
            if (seconds < 0) seconds = 0;
            return seconds >= 60 ? (seconds / 60) + "m " + (seconds % 60).ToString("00") + "s" : seconds + "s";
        }

        static string PartName(Part p)
        {
            switch (p)
            {
                case Part.Kick: return "Drum";
                case Part.Snare: return "Snare";
                case Part.Hat: return "Shaker";
                case Part.Tom: return "Toms";
                case Part.Click: return "Clicks";
                case Part.Bass: return "Bass";
                case Part.SubBass: return "Deep bass";
                case Part.WalkBass: return "Walking bass";
                case Part.Riff: return "Riff";
                case Part.Pad: return "Choir";
                case Part.LowPad: return "Howl";
                case Part.Arp: return "Arpeggio";
                case Part.HighArp: return "Bubbles";
                case Part.Harp: return "Harp";
                case Part.Stabs: return "Chord stabs";
                case Part.Skank: return "Offbeat chords";
                case Part.Bells: return "Bells";
                case Part.Counter: return "Counter-melody";
                case Part.Lead: return "Melody";
                default: return "High melody";
            }
        }
    }
}
