using System.Collections.Generic;
using UnityEngine;

namespace Mythica
{
    // The sanctuary island: ground, water, decorations, and the grid of spots creatures can live on.
    public class Island : MonoBehaviour
    {
        public const float Radius = 8f;
        const float SlotSpacing = 2.3f;

        public readonly List<Vector3> Slots = new List<Vector3>();
        readonly List<GameObject> markers = new List<GameObject>();
        public Transform CreatureRoot;

        public static Island Build()
        {
            var island = new GameObject("Island").AddComponent<Island>();
            var t = island.transform;

            var water = CreatureBuilder.Prim(PrimitiveType.Plane, t, new Vector3(0, -0.45f, 0), new Vector3(20, 1, 20), new Color(0.2f, 0.45f, 0.75f));
            water.name = "Sea";
            CreatureBuilder.Prim(PrimitiveType.Cylinder, t, new Vector3(0, -0.6f, 0), new Vector3(Radius * 2.2f, 0.4f, Radius * 2.2f), new Color(0.93f, 0.85f, 0.6f));
            CreatureBuilder.Prim(PrimitiveType.Cylinder, t, new Vector3(0, -0.5f, 0), new Vector3(Radius * 2f, 0.5f, Radius * 2f), new Color(0.4f, 0.68f, 0.32f));

            // The Six Powers: a crystal for each magic around the shore.
            for (int i = 0; i < 6; i++)
            {
                float a = i * Mathf.PI * 2f / 6f + Mathf.PI / 6f;
                var p = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * (Radius - 0.6f);
                var magic = MythicaDB.AllMagic[i];
                CreatureBuilder.Prim(PrimitiveType.Cube, t, p + Vector3.up * 0.6f, new Vector3(0.45f, 1.3f, 0.45f),
                    MythicaDB.MagicColor(magic), new Vector3(0, a * Mathf.Rad2Deg, 12));
                float ta = a + Mathf.PI / 6f;
                var tp = new Vector3(Mathf.Cos(ta), 0, Mathf.Sin(ta)) * (Radius - 0.5f);
                CreatureBuilder.Limb(t, tp, tp + Vector3.up * 0.9f, 0.25f, new Color(0.45f, 0.3f, 0.15f));
                CreatureBuilder.Prim(PrimitiveType.Sphere, t, tp + Vector3.up * 1.3f, Vector3.one * 1.1f, new Color(0.2f, 0.5f, 0.25f));
            }

            for (int x = -2; x <= 2; x++)
                for (int z = -2; z <= 2; z++)
                {
                    var p = new Vector3(x * SlotSpacing, 0, z * SlotSpacing);
                    if (p.magnitude > Radius - 1.6f) continue;
                    island.Slots.Add(p);
                    var m = GameObject.CreatePrimitive(PrimitiveType.Cylinder);   // keeps its collider for clicks
                    m.name = "Slot " + (island.Slots.Count - 1);
                    m.transform.SetParent(t, false);
                    m.transform.localPosition = p + Vector3.up * 0.03f;
                    m.transform.localScale = new Vector3(1.7f, 0.03f, 1.7f);
                    m.GetComponent<Renderer>().sharedMaterial = CreatureBuilder.Mat(new Color(1f, 0.95f, 0.55f));
                    m.SetActive(false);
                    island.markers.Add(m);
                }

            island.CreatureRoot = new GameObject("Creatures").transform;
            island.CreatureRoot.SetParent(t, false);
            return island;
        }

        public void ShowFreeSlots(HashSet<int> occupied)
        {
            for (int i = 0; i < markers.Count; i++) markers[i].SetActive(occupied == null ? false : !occupied.Contains(i));
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
}
