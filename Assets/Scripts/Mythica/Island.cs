using System.Collections.Generic;
using UnityEngine;

namespace Mythica
{
    // The sanctuary island: ground, water, scenery, the three buildings, and the grid of spots
    // that creatures and decorations live on.
    public class Island : MonoBehaviour
    {
        public const float Radius = 9.5f;
        const float SlotSpacing = 2.3f;

        public readonly List<Vector3> Slots = new List<Vector3>();
        readonly List<GameObject> markers = new List<GameObject>();
        public Transform CreatureRoot;
        public Transform DecorRoot;
        public Structure Grove, Nursery, Bakery;
        Transform sea;

        public static Island Build()
        {
            var island = new GameObject("Island").AddComponent<Island>();
            var t = island.transform;

            island.sea = CreatureBuilder.Prim(PrimitiveType.Plane, t, new Vector3(0, -0.45f, 0), new Vector3(30, 1, 30), new Color(0.22f, 0.5f, 0.8f));
            island.sea.name = "Sea";
            CreatureBuilder.Prim(PrimitiveType.Cylinder, t, new Vector3(0, -0.62f, 0), new Vector3(Radius * 2.25f, 0.4f, Radius * 2.25f), new Color(0.95f, 0.87f, 0.62f));
            CreatureBuilder.Prim(PrimitiveType.Cylinder, t, new Vector3(0, -0.5f, 0), new Vector3(Radius * 2f, 0.5f, Radius * 2f), new Color(0.42f, 0.72f, 0.32f));
            // A lighter meadow where creatures stand.
            CreatureBuilder.Prim(PrimitiveType.Cylinder, t, new Vector3(0, -0.47f, 0.2f), new Vector3(14.5f, 0.5f, 14.5f), new Color(0.5f, 0.8f, 0.36f));

            // The Six Powers: a crystal for each magic around the front shore, with trees between.
            for (int i = 0; i < 6; i++)
            {
                float a = Mathf.Lerp(200f, 340f, i / 5f) * Mathf.Deg2Rad;
                var p = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * (Radius - 0.7f);
                CreatureBuilder.Prim(PrimitiveType.Cube, t, p + Vector3.up * 0.6f, new Vector3(0.45f, 1.3f, 0.45f),
                    MythicaDB.MagicColor(MythicaDB.AllMagic[i]), new Vector3(0, a * Mathf.Rad2Deg, 12));
            }
            foreach (float deg in new[] { 165f, 185f, 355f, 15f })
            {
                float a = deg * Mathf.Deg2Rad;
                var tp = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * (Radius - 0.6f);
                Scenery.Tree(t, tp, 1.1f);
            }

            for (int x = -2; x <= 2; x++)
                for (int z = -2; z <= 2; z++)
                {
                    var p = new Vector3(x * SlotSpacing, 0, z * SlotSpacing - 0.6f);
                    if (new Vector3(p.x, 0, z * SlotSpacing).magnitude > 6.4f) continue;
                    island.Slots.Add(p);
                    var m = GameObject.CreatePrimitive(PrimitiveType.Cylinder);   // keeps its collider for taps
                    m.name = "Slot " + (island.Slots.Count - 1);
                    m.transform.SetParent(t, false);
                    m.transform.localPosition = p + Vector3.up * 0.03f;
                    m.transform.localScale = new Vector3(1.7f, 0.03f, 1.7f);
                    m.GetComponent<Renderer>().sharedMaterial = CreatureBuilder.Mat(new Color(1f, 0.95f, 0.55f));
                    m.SetActive(false);
                    island.markers.Add(m);
                }

            // The buildings sit along the back of the island, like the castle and bakery in a monster sanctuary.
            island.Nursery = Structure.Build(StructureKind.Nursery, t, PolarBack(135f));
            island.Grove = Structure.Build(StructureKind.Grove, t, PolarBack(90f));
            island.Bakery = Structure.Build(StructureKind.Bakery, t, PolarBack(45f));

            island.CreatureRoot = new GameObject("Creatures").transform;
            island.CreatureRoot.SetParent(t, false);
            island.DecorRoot = new GameObject("Decorations").transform;
            island.DecorRoot.SetParent(t, false);
            return island;
        }

        static Vector3 PolarBack(float deg)
        {
            float a = deg * Mathf.Deg2Rad;
            return new Vector3(Mathf.Cos(a) * 7.4f, 0, Mathf.Sin(a) * 7.4f);
        }

        void Update()
        {
            // A gentle swell on the sea.
            sea.localPosition = new Vector3(0, -0.45f + Mathf.Sin(Time.time * 0.8f) * 0.05f, 0);
            float pulse = 0.85f + 0.15f * Mathf.Sin(Time.time * 4f);
            foreach (var m in markers)
                if (m.activeSelf) m.transform.localScale = new Vector3(1.7f * pulse, 0.03f, 1.7f * pulse);
        }

        public void ShowFreeSlots(HashSet<int> occupied)
        {
            for (int i = 0; i < markers.Count; i++) markers[i].SetActive(occupied != null && !occupied.Contains(i));
        }

        public int SlotFromCollider(Collider c)
        {
            return c == null ? -1 : markers.IndexOf(c.gameObject);
        }

        public int NearestSlot(Vector3 p)
        {
            int best = 0;
            for (int i = 1; i < Slots.Count; i++)
                if ((Slots[i] - p).sqrMagnitude < (Slots[best] - p).sqrMagnitude) best = i;
            return best;
        }
    }

    public enum StructureKind { Grove, Nursery, Bakery }

    // A tappable building on the island. Its look reacts to what it's doing (breeding, eggs, baking).
    public class Structure : MonoBehaviour
    {
        public StructureKind Kind;
        public float Height = 2.4f;
        Transform orb;
        Transform smokeRoot;
        readonly List<Transform> smoke = new List<Transform>();
        readonly List<Transform> eggs = new List<Transform>();
        Transform eggRoot;
        string eggSignature = "";

        public static Structure Build(StructureKind kind, Transform parent, Vector3 pos)
        {
            var root = new GameObject(kind.ToString());
            root.transform.SetParent(parent, false);
            root.transform.localPosition = pos;
            root.transform.localRotation = Quaternion.Euler(0, 180f - Mathf.Atan2(pos.x, pos.z) * Mathf.Rad2Deg * 0.4f, 0);
            var s = root.AddComponent<Structure>();
            s.Kind = kind;
            var t = root.transform;
            Color stone = new Color(0.72f, 0.7f, 0.66f), wood = new Color(0.5f, 0.32f, 0.18f);

            switch (kind)
            {
                case StructureKind.Grove:
                    // A ring of six standing stones, one per magic, around a mossy altar.
                    CreatureBuilder.Prim(PrimitiveType.Cylinder, t, new Vector3(0, 0.05f, 0), new Vector3(2.6f, 0.05f, 2.6f), new Color(0.35f, 0.6f, 0.3f));
                    for (int i = 0; i < 6; i++)
                    {
                        float a = i * Mathf.PI * 2f / 6f;
                        var p = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * 1.15f;
                        CreatureBuilder.Prim(PrimitiveType.Cube, t, p + Vector3.up * 0.55f, new Vector3(0.3f, 1.1f, 0.3f), stone, new Vector3(0, -a * Mathf.Rad2Deg, 0));
                        CreatureBuilder.Prim(PrimitiveType.Sphere, t, p + Vector3.up * 1.2f, Vector3.one * 0.22f, MythicaDB.MagicColor(MythicaDB.AllMagic[i]));
                    }
                    CreatureBuilder.Prim(PrimitiveType.Cylinder, t, new Vector3(0, 0.25f, 0), new Vector3(0.9f, 0.25f, 0.9f), stone);
                    s.orb = CreatureBuilder.Prim(PrimitiveType.Sphere, t, new Vector3(0, 1.0f, 0), Vector3.one * 0.5f, new Color(1f, 0.55f, 0.75f));
                    s.Height = 2.0f;
                    break;

                case StructureKind.Nursery:
                    // A round hut with a thatched cone roof and a nest in front.
                    CreatureBuilder.Prim(PrimitiveType.Cylinder, t, new Vector3(0, 0.7f, -0.2f), new Vector3(1.8f, 0.7f, 1.8f), new Color(0.95f, 0.88f, 0.7f));
                    for (int i = 0; i < 5; i++)
                        CreatureBuilder.Prim(PrimitiveType.Cylinder, t, new Vector3(0, 1.5f + i * 0.22f, -0.2f),
                            new Vector3(2.2f - i * 0.45f, 0.12f, 2.2f - i * 0.45f), new Color(0.85f, 0.6f, 0.25f));
                    CreatureBuilder.Prim(PrimitiveType.Cube, t, new Vector3(0, 0.45f, 0.62f), new Vector3(0.6f, 0.9f, 0.1f), wood);
                    CreatureBuilder.Prim(PrimitiveType.Cylinder, t, new Vector3(0, 0.1f, 1.35f), new Vector3(1.5f, 0.1f, 1.0f), new Color(0.6f, 0.42f, 0.22f));
                    s.eggRoot = new GameObject("Eggs").transform;
                    s.eggRoot.SetParent(t, false);
                    s.eggRoot.localPosition = new Vector3(0, 0.2f, 1.35f);
                    s.Height = 2.6f;
                    break;

                case StructureKind.Bakery:
                    // A clay oven with a chimney that puffs while baking.
                    CreatureBuilder.Prim(PrimitiveType.Cube, t, new Vector3(0, 0.35f, 0), new Vector3(2.0f, 0.7f, 1.6f), new Color(0.8f, 0.45f, 0.3f));
                    CreatureBuilder.Prim(PrimitiveType.Sphere, t, new Vector3(0, 0.75f, 0), new Vector3(1.8f, 1.4f, 1.5f), new Color(0.88f, 0.55f, 0.35f));
                    CreatureBuilder.Prim(PrimitiveType.Sphere, t, new Vector3(0, 0.75f, 0.68f), new Vector3(0.7f, 0.6f, 0.2f), new Color(0.2f, 0.08f, 0.05f));
                    CreatureBuilder.Prim(PrimitiveType.Sphere, t, new Vector3(0, 0.62f, 0.72f), new Vector3(0.5f, 0.2f, 0.12f), new Color(1f, 0.6f, 0.15f));
                    CreatureBuilder.Prim(PrimitiveType.Cylinder, t, new Vector3(0.5f, 1.6f, -0.2f), new Vector3(0.35f, 0.5f, 0.35f), new Color(0.6f, 0.35f, 0.25f));
                    s.smokeRoot = new GameObject("Smoke").transform;
                    s.smokeRoot.SetParent(t, false);
                    s.smokeRoot.localPosition = new Vector3(0.5f, 2.1f, -0.2f);
                    for (int i = 0; i < 4; i++)
                        s.smoke.Add(CreatureBuilder.Prim(PrimitiveType.Sphere, s.smokeRoot, Vector3.zero, Vector3.one * 0.3f, new Color(0.92f, 0.92f, 0.95f)));
                    s.Height = 2.4f;
                    break;
            }

            var col = root.AddComponent<BoxCollider>();
            col.center = new Vector3(0, 0.9f, 0.2f);
            col.size = new Vector3(2.6f, 1.8f, 2.6f);
            return s;
        }

        void Update()
        {
            var gm = GameManager.Instance;
            if (gm == null || gm.Save == null) return;
            long now = SaveSystem.Now;
            switch (Kind)
            {
                case StructureKind.Grove:
                {
                    bool busy = gm.Save.breeding.active;
                    orb.gameObject.SetActive(busy);
                    if (busy)
                    {
                        float k = 0.5f + 0.1f * Mathf.Sin(Time.time * 5f);
                        orb.localScale = Vector3.one * k;
                        orb.localPosition = new Vector3(0, 1.0f + 0.1f * Mathf.Sin(Time.time * 2f), 0);
                        orb.Rotate(0, 90f * Time.deltaTime, 0);
                    }
                    break;
                }
                case StructureKind.Bakery:
                {
                    bool baking = gm.Save.bake.active && now < gm.Save.bake.doneAt;
                    smokeRoot.gameObject.SetActive(baking);
                    for (int i = 0; i < smoke.Count; i++)
                    {
                        float k = (Time.time * 0.5f + i / (float)smoke.Count) % 1f;
                        smoke[i].localPosition = new Vector3(Mathf.Sin(k * 6f + i) * 0.15f, k * 1.4f, 0);
                        smoke[i].localScale = Vector3.one * (0.2f + k * 0.5f) * (1f - k * 0.6f);
                    }
                    break;
                }
                case StructureKind.Nursery:
                    SyncEggs(gm, now);
                    break;
            }
        }

        // One little egg in the nest for each egg in the nursery; ready ones wobble.
        void SyncEggs(GameManager gm, long now)
        {
            string sig = "";
            foreach (var e in gm.Save.eggs) sig += e.species + e.magic + ";";
            if (sig != eggSignature)
            {
                eggSignature = sig;
                foreach (var e in eggs) Destroy(e.gameObject);
                eggs.Clear();
                for (int i = 0; i < gm.Save.eggs.Count; i++)
                {
                    var egg = gm.Save.eggs[i];
                    var x = (i - (gm.Save.eggs.Count - 1) * 0.5f) * 0.45f;
                    var eggT = CreatureBuilder.Prim(PrimitiveType.Sphere, eggRoot, new Vector3(x, 0.22f, 0), new Vector3(0.32f, 0.42f, 0.32f),
                        Color.Lerp(Color.white, MythicaDB.MagicColor(egg.magic), 0.6f));
                    CreatureBuilder.Prim(PrimitiveType.Sphere, eggT, new Vector3(0.25f, 0.1f, 0.4f), Vector3.one * 0.3f, MythicaDB.Get(egg.species).Accent);
                    eggs.Add(eggT);
                }
            }
            for (int i = 0; i < eggs.Count && i < gm.Save.eggs.Count; i++)
            {
                bool ready = now >= gm.Save.eggs[i].hatchAt;
                eggs[i].localRotation = Quaternion.Euler(0, 0, ready ? Mathf.Sin(Time.time * 12f + i) * 12f : 0f);
            }
        }
    }

    // Scenery and decorations built from primitives.
    public static class Scenery
    {
        public static void Tree(Transform parent, Vector3 p, float size)
        {
            CreatureBuilder.Limb(parent, p, p + Vector3.up * 0.9f * size, 0.28f * size, new Color(0.45f, 0.3f, 0.15f));
            CreatureBuilder.Prim(PrimitiveType.Sphere, parent, p + Vector3.up * 1.35f * size, Vector3.one * 1.1f * size, new Color(0.22f, 0.55f, 0.27f));
            CreatureBuilder.Prim(PrimitiveType.Sphere, parent, p + new Vector3(0.25f, 1.6f, 0.1f) * size, Vector3.one * 0.7f * size, new Color(0.3f, 0.65f, 0.3f));
        }

        public static GameObject Decoration(DecorationDef d, Transform parent)
        {
            var root = new GameObject(d.Name);
            root.transform.SetParent(parent, false);
            var t = root.transform;
            switch (d.Id)
            {
                case "tree":
                    Tree(t, Vector3.zero, 1.2f);
                    break;
                case "rock":
                    CreatureBuilder.Prim(PrimitiveType.Sphere, t, new Vector3(-0.3f, 0.25f, 0), new Vector3(0.9f, 0.6f, 0.8f), d.Color);
                    CreatureBuilder.Prim(PrimitiveType.Sphere, t, new Vector3(0.35f, 0.2f, 0.2f), new Vector3(0.6f, 0.45f, 0.6f), d.Color * 0.9f);
                    CreatureBuilder.Prim(PrimitiveType.Sphere, t, new Vector3(-0.3f, 0.5f, 0), new Vector3(0.7f, 0.15f, 0.6f), new Color(0.35f, 0.65f, 0.3f));
                    break;
                case "flowers":
                    for (int i = 0; i < 7; i++)
                    {
                        float a = i * 2.4f;
                        var p = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * (0.15f + i * 0.08f);
                        CreatureBuilder.Limb(t, p, p + Vector3.up * 0.45f, 0.05f, new Color(0.3f, 0.6f, 0.25f));
                        Color petal = Color.HSVToRGB((0.9f + i * 0.07f) % 1f, 0.5f, 1f);
                        CreatureBuilder.Prim(PrimitiveType.Sphere, t, p + Vector3.up * 0.5f, new Vector3(0.28f, 0.12f, 0.28f), petal);
                        CreatureBuilder.Prim(PrimitiveType.Sphere, t, p + Vector3.up * 0.55f, Vector3.one * 0.1f, new Color(1f, 0.9f, 0.3f));
                    }
                    break;
                case "lantern":
                    CreatureBuilder.Limb(t, Vector3.zero, Vector3.up * 1.6f, 0.1f, new Color(0.35f, 0.25f, 0.15f));
                    CreatureBuilder.Limb(t, Vector3.up * 1.6f, new Vector3(0.45f, 1.6f, 0), 0.07f, new Color(0.35f, 0.25f, 0.15f));
                    CreatureBuilder.Prim(PrimitiveType.Sphere, t, new Vector3(0.45f, 1.3f, 0), new Vector3(0.35f, 0.45f, 0.35f), d.Color);
                    break;
                case "mushroom":
                    for (int i = 0; i < 3; i++)
                    {
                        var p = new Vector3((i - 1) * 0.4f, 0, (i % 2) * 0.3f);
                        float h = 0.4f + i * 0.15f;
                        CreatureBuilder.Limb(t, p, p + Vector3.up * h, 0.14f, new Color(0.95f, 0.92f, 0.85f));
                        CreatureBuilder.Prim(PrimitiveType.Sphere, t, p + Vector3.up * h, new Vector3(0.5f, 0.25f, 0.5f), d.Color);
                    }
                    break;
                default:
                    CreatureBuilder.Prim(PrimitiveType.Cube, t, new Vector3(0, 0.8f, 0), new Vector3(0.6f, 1.6f, 0.6f), d.Color, new Vector3(0, 45, 8));
                    CreatureBuilder.Prim(PrimitiveType.Cube, t, new Vector3(0.45f, 0.4f, 0.2f), new Vector3(0.35f, 0.8f, 0.35f), d.Color * 0.85f, new Vector3(0, 20, -15));
                    CreatureBuilder.Prim(PrimitiveType.Cylinder, t, new Vector3(0, 0.05f, 0), new Vector3(1.3f, 0.05f, 1.3f), new Color(0.6f, 0.6f, 0.62f));
                    break;
            }
            var col = root.AddComponent<BoxCollider>();
            col.center = new Vector3(0, 0.7f, 0);
            col.size = new Vector3(1.4f, 1.4f, 1.4f);
            return root;
        }
    }
}
