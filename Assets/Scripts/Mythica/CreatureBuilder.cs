using System.Collections.Generic;
using UnityEngine;

namespace Mythica
{
    // Builds placeholder creature models out of Unity primitives. Replace with real art later by
    // swapping Build() for a prefab lookup keyed on species id.
    public static class CreatureBuilder
    {
        static Material baseMaterial;
        static readonly Dictionary<Color, Material> materials = new Dictionary<Color, Material>();

        public static Material Mat(Color c)
        {
            Material m;
            if (materials.TryGetValue(c, out m)) return m;
            if (baseMaterial == null)
            {
                // The default primitive material exists in every render pipeline and is always in the build.
                var probe = GameObject.CreatePrimitive(PrimitiveType.Cube);
                baseMaterial = probe.GetComponent<Renderer>().sharedMaterial;
                Object.Destroy(probe);
            }
            m = new Material(baseMaterial);
            m.color = c;
            materials[c] = m;
            return m;
        }

        public static Transform Prim(PrimitiveType type, Transform parent, Vector3 pos, Vector3 scale, Color c, Vector3 euler = default(Vector3))
        {
            var go = GameObject.CreatePrimitive(type);
            Object.Destroy(go.GetComponent<Collider>());
            var t = go.transform;
            t.SetParent(parent, false);
            t.localPosition = pos;
            t.localScale = scale;
            t.localEulerAngles = euler;
            go.GetComponent<Renderer>().sharedMaterial = Mat(c);
            return t;
        }

        // A cylinder stretched between two points: legs, necks, horns, tentacles.
        public static Transform Limb(Transform parent, Vector3 a, Vector3 b, float radius, Color c)
        {
            var t = Prim(PrimitiveType.Cylinder, parent, (a + b) * 0.5f,
                new Vector3(radius, Vector3.Distance(a, b) * 0.5f, radius), c);
            t.localRotation = Quaternion.FromToRotation(Vector3.up, b - a);
            return t;
        }

        static Color Shade(Color c, float k) { return new Color(c.r * k, c.g * k, c.b * k, 1f); }

        public class Result
        {
            public GameObject Root;
            public Transform Body;   // animated part
            public float Height;
            public List<Transform> Mouths = new List<Transform>();
        }

        // Models face +Z. The island rotates them to face the camera.
        public static Result Build(Species s, Magic m)
        {
            var root = new GameObject(MythicaDB.VariantName(s, m));
            var body = new GameObject("Body").transform;
            body.SetParent(root.transform, false);

            Color main = MythicaDB.MagicColor(m);
            Color dark = Shade(main, 0.6f);
            Color accent = s.Accent;
            bool stone = s.Has(Feature.Stone);
            var result = new Result();
            var heads = new List<Vector3>();
            float headSize = 0.5f;
            float height = 1.5f;
            Vector3 tailBase = new Vector3(0, 0.9f, -0.55f);
            Vector3 wingAnchor = new Vector3(0.4f, 1.0f, 0.1f);
            bool drawHeads = true;

            switch (s.Body)
            {
                case BodyKind.Beast:
                    Prim(PrimitiveType.Sphere, body, new Vector3(0, 0.75f, 0), new Vector3(0.75f, 0.65f, 1.15f), main);
                    FourLegs(body, 0.25f, 0.35f, 0.5f, 0.09f, dark);
                    if (s.Has(Feature.ThreeHeads))
                    {
                        heads.Add(new Vector3(-0.32f, 1.15f, 0.55f));
                        heads.Add(new Vector3(0f, 1.3f, 0.65f));
                        heads.Add(new Vector3(0.32f, 1.15f, 0.55f));
                        headSize = 0.4f;
                    }
                    else { heads.Add(new Vector3(0, 1.2f, 0.6f)); headSize = 0.55f; }
                    height = 1.7f;
                    break;

                case BodyKind.SmallBeast:
                    Prim(PrimitiveType.Sphere, body, new Vector3(0, 0.45f, 0), new Vector3(0.6f, 0.55f, 0.7f), main);
                    FourLegs(body, 0.18f, 0.2f, 0.22f, 0.07f, dark);
                    heads.Add(new Vector3(0, 0.85f, 0.3f));
                    headSize = 0.5f;
                    tailBase = new Vector3(0, 0.5f, -0.35f);
                    if (s.Has(Feature.BushyTail))
                        Prim(PrimitiveType.Sphere, body, new Vector3(0, 0.85f, -0.45f), new Vector3(0.35f, 0.75f, 0.3f), accent, new Vector3(-15, 0, 0));
                    else
                        Prim(PrimitiveType.Sphere, body, new Vector3(0, 0.5f, -0.38f), Vector3.one * 0.3f, Color.white);
                    wingAnchor = new Vector3(0.3f, 0.6f, 0f);
                    height = 1.4f;
                    break;

                case BodyKind.Horse:
                    Prim(PrimitiveType.Sphere, body, new Vector3(0, 1.05f, 0), new Vector3(0.7f, 0.7f, 1.3f), main);
                    FourLegs(body, 0.22f, 0.42f, 0.8f, 0.08f, dark);
                    Limb(body, new Vector3(0, 1.2f, 0.45f), new Vector3(0, 1.75f, 0.75f), 0.25f, main);
                    heads.Add(new Vector3(0, 1.85f, 0.85f));
                    headSize = 0.45f;
                    tailBase = new Vector3(0, 1.15f, -0.6f);
                    wingAnchor = new Vector3(0.35f, 1.3f, 0.1f);
                    height = 2.3f;
                    break;

                case BodyKind.Bird:
                    Prim(PrimitiveType.Sphere, body, new Vector3(0, 0.85f, 0), new Vector3(0.75f, 0.85f, 0.8f), main);
                    Limb(body, new Vector3(-0.15f, 0.5f, 0), new Vector3(-0.15f, 0f, 0.05f), 0.06f, accent);
                    Limb(body, new Vector3(0.15f, 0.5f, 0), new Vector3(0.15f, 0f, 0.05f), 0.06f, accent);
                    Prim(PrimitiveType.Cube, body, new Vector3(0, 0.75f, -0.5f), new Vector3(0.4f, 0.06f, 0.6f), accent, new Vector3(-25, 0, 0));
                    heads.Add(new Vector3(0, 1.45f, 0.25f));
                    headSize = 0.5f;
                    wingAnchor = new Vector3(0.35f, 1.0f, 0f);
                    tailBase = Vector3.zero;
                    height = 1.9f;
                    break;

                case BodyKind.Fish:
                {
                    float y = s.Has(Feature.Hover) ? 1.0f : 0.75f;
                    Prim(PrimitiveType.Sphere, body, new Vector3(0, y, 0), new Vector3(0.6f, 0.65f, 1.5f), main);
                    Prim(PrimitiveType.Cube, body, new Vector3(0, y + 0.1f, -0.85f), new Vector3(0.06f, 0.65f, 0.4f), accent, new Vector3(20, 0, 0));
                    Prim(PrimitiveType.Cube, body, new Vector3(0, y + 0.4f, -0.05f), new Vector3(0.05f, 0.4f, 0.35f), accent, new Vector3(-30, 0, 0));
                    Prim(PrimitiveType.Cube, body, new Vector3(-0.32f, y - 0.1f, 0.25f), new Vector3(0.3f, 0.04f, 0.2f), accent, new Vector3(0, 0, 25));
                    Prim(PrimitiveType.Cube, body, new Vector3(0.32f, y - 0.1f, 0.25f), new Vector3(0.3f, 0.04f, 0.2f), accent, new Vector3(0, 0, -25));
                    if (s.Has(Feature.Legs)) FourLegs(body, 0.2f, 0.4f, 0.32f, 0.08f, dark);
                    heads.Add(new Vector3(0, y + 0.05f, 0.62f));
                    drawHeads = false;
                    headSize = 0.55f;
                    tailBase = Vector3.zero;
                    height = y + 1.0f;
                    break;
                }

                case BodyKind.Tentacled:
                    Prim(PrimitiveType.Sphere, body, new Vector3(0, 1.3f, 0), new Vector3(0.95f, 1.25f, 0.95f), main);
                    for (int i = 0; i < 8; i++)
                    {
                        float a = i * Mathf.PI * 2f / 8f;
                        var dir = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                        Limb(body, dir * 0.3f + Vector3.up * 0.85f, dir * 0.75f + Vector3.up * 0.05f, 0.13f, i % 2 == 0 ? main : dark);
                    }
                    heads.Add(new Vector3(0, 1.05f, 0.3f));
                    drawHeads = false;
                    headSize = 0.65f;
                    tailBase = Vector3.zero;
                    height = 2.1f;
                    break;

                case BodyKind.Giant:
                {
                    var torso = stone ? PrimitiveType.Cube : PrimitiveType.Sphere;
                    Color fur = accent;   // stone for Golem, fur for Yeti
                    Prim(torso, body, new Vector3(0, 1.0f, 0), new Vector3(1.0f, 1.0f, 0.7f), fur);
                    Prim(torso, body, new Vector3(0, 1.0f, 0.2f), new Vector3(0.6f, 0.6f, 0.4f), main);   // magic-coloured chest
                    Limb(body, new Vector3(-0.25f, 0.6f, 0), new Vector3(-0.28f, 0f, 0), 0.28f, Shade(fur, 0.8f));
                    Limb(body, new Vector3(0.25f, 0.6f, 0), new Vector3(0.28f, 0f, 0), 0.28f, Shade(fur, 0.8f));
                    Limb(body, new Vector3(-0.55f, 1.35f, 0), new Vector3(-0.75f, 0.55f, 0.1f), 0.26f, fur);
                    Limb(body, new Vector3(0.55f, 1.35f, 0), new Vector3(0.75f, 0.55f, 0.1f), 0.26f, fur);
                    heads.Add(new Vector3(0, 1.75f, 0.05f));
                    headSize = 0.55f;
                    tailBase = Vector3.zero;
                    height = 2.2f;
                    break;
                }

                case BodyKind.Turtle:
                    Prim(PrimitiveType.Sphere, body, new Vector3(0, 0.55f, 0), new Vector3(1.4f, 0.75f, 1.5f), main);
                    Prim(PrimitiveType.Sphere, body, new Vector3(0, 0.35f, 0), new Vector3(1.2f, 0.4f, 1.3f), accent);
                    FourLegs(body, 0.5f, 0.5f, 0.3f, 0.12f, accent);
                    heads.Add(new Vector3(0, 0.55f, 0.85f));
                    headSize = 0.4f;
                    // The island on its back.
                    Limb(body, new Vector3(0, 0.85f, 0), new Vector3(0, 1.3f, 0), 0.12f, new Color(0.45f, 0.3f, 0.15f));
                    Prim(PrimitiveType.Sphere, body, new Vector3(0, 1.5f, 0), Vector3.one * 0.6f, new Color(0.25f, 0.6f, 0.25f));
                    tailBase = Vector3.zero;
                    height = 1.9f;
                    break;

                case BodyKind.Lizard:
                    Prim(PrimitiveType.Sphere, body, new Vector3(0, 0.45f, 0), new Vector3(0.6f, 0.45f, 1.4f), main);
                    FourLegs(body, 0.35f, 0.4f, 0.3f, 0.07f, dark);
                    Limb(body, new Vector3(0, 0.45f, -0.6f), new Vector3(0, 0.2f, -1.3f), 0.14f, dark);
                    if (s.Has(Feature.LongNecks))
                    {
                        heads.Add(new Vector3(-0.45f, 1.5f, 0.75f));
                        heads.Add(new Vector3(0f, 1.75f, 0.85f));
                        heads.Add(new Vector3(0.45f, 1.5f, 0.75f));
                        foreach (var h in heads) Limb(body, new Vector3(h.x * 0.4f, 0.6f, 0.45f), h, 0.16f, main);
                        headSize = 0.38f;
                        height = 2.1f;
                    }
                    else
                    {
                        heads.Add(new Vector3(0, 0.6f, 0.85f));
                        headSize = 0.42f;
                        height = 1.2f;
                    }
                    tailBase = Vector3.zero;
                    wingAnchor = new Vector3(0.3f, 0.7f, 0.1f);
                    break;
            }

            // Heads and head features.
            foreach (var h in heads)
            {
                float hs = headSize;
                Color headColor = s.Has(Feature.Skull) ? new Color(0.93f, 0.9f, 0.82f) : main;
                if (drawHeads)
                    Prim(stone ? PrimitiveType.Cube : PrimitiveType.Sphere, body, h, Vector3.one * hs, stone ? accent : headColor);

                Color eye = s.Has(Feature.Skull) ? new Color(0.8f, 0.1f, 0.1f) : new Color(0.05f, 0.05f, 0.08f);
                float eyeZ = drawHeads ? hs * 0.42f : 0.1f;
                Prim(PrimitiveType.Sphere, body, h + new Vector3(-hs * 0.22f, hs * 0.12f, eyeZ), Vector3.one * hs * 0.2f, eye);
                Prim(PrimitiveType.Sphere, body, h + new Vector3(hs * 0.22f, hs * 0.12f, eyeZ), Vector3.one * hs * 0.2f, eye);
                // White eye shines make them read as cartoon eyes from the camera distance.
                Prim(PrimitiveType.Sphere, body, h + new Vector3(-hs * 0.19f, hs * 0.17f, eyeZ + hs * 0.08f), Vector3.one * hs * 0.07f, Color.white);
                Prim(PrimitiveType.Sphere, body, h + new Vector3(hs * 0.25f, hs * 0.17f, eyeZ + hs * 0.08f), Vector3.one * hs * 0.07f, Color.white);
                // The mouth opens on each note (see Creature).
                var mouth = Prim(PrimitiveType.Sphere, body, h + new Vector3(0, -hs * 0.18f, eyeZ - hs * 0.02f),
                    new Vector3(hs * 0.3f, hs * 0.04f, hs * 0.12f), new Color(0.35f, 0.05f, 0.08f));
                result.Mouths.Add(mouth);

                if (s.Has(Feature.BigEars))
                {
                    float earLen = s.Body == BodyKind.SmallBeast ? 0.6f : 0.4f;
                    Prim(PrimitiveType.Cube, body, h + new Vector3(-hs * 0.28f, hs * (0.35f + earLen * 0.5f), -hs * 0.05f),
                        new Vector3(hs * 0.18f, hs * earLen * 1.4f, hs * 0.07f), dark, new Vector3(0, 0, 15));
                    Prim(PrimitiveType.Cube, body, h + new Vector3(hs * 0.28f, hs * (0.35f + earLen * 0.5f), -hs * 0.05f),
                        new Vector3(hs * 0.18f, hs * earLen * 1.4f, hs * 0.07f), dark, new Vector3(0, 0, -15));
                }
                if (s.Has(Feature.Horns))
                {
                    Limb(body, h + new Vector3(-hs * 0.18f, hs * 0.38f, 0), h + new Vector3(-hs * 0.32f, hs * 0.95f, -hs * 0.15f), hs * 0.1f, accent);
                    Limb(body, h + new Vector3(hs * 0.18f, hs * 0.38f, 0), h + new Vector3(hs * 0.32f, hs * 0.95f, -hs * 0.15f), hs * 0.1f, accent);
                }
                if (s.Has(Feature.Antlers))
                {
                    Color bone = s.Has(Feature.Skull) ? new Color(0.85f, 0.82f, 0.72f) : accent;
                    for (int side = -1; side <= 1; side += 2)
                    {
                        Vector3 b0 = h + new Vector3(side * hs * 0.2f, hs * 0.4f, 0);
                        Vector3 b1 = h + new Vector3(side * hs * 0.6f, hs * 1.1f, -hs * 0.1f);
                        Vector3 b2 = h + new Vector3(side * hs * 0.25f, hs * 1.25f, hs * 0.05f);
                        Vector3 b3 = h + new Vector3(side * hs * 0.95f, hs * 1.35f, -hs * 0.15f);
                        Limb(body, b0, b1, hs * 0.08f, bone);
                        Limb(body, Vector3.Lerp(b0, b1, 0.5f), b2, hs * 0.06f, bone);
                        Limb(body, b1, b3, hs * 0.06f, bone);
                    }
                }
                if (s.Has(Feature.Beak) || s.Body == BodyKind.Bird)
                    Prim(PrimitiveType.Cube, body, h + new Vector3(0, -hs * 0.05f, hs * 0.55f), new Vector3(hs * 0.25f, hs * 0.2f, hs * 0.4f),
                        new Color(1f, 0.75f, 0.2f), new Vector3(20, 0, 0));
                if (s.Has(Feature.Crest))
                    for (int i = -1; i <= 1; i++)
                        Prim(PrimitiveType.Cube, body, h + new Vector3(i * hs * 0.15f, hs * 0.65f, -hs * 0.1f), new Vector3(hs * 0.08f, hs * 0.5f, hs * 0.08f),
                            accent, new Vector3(-20, 0, i * -20));
                if (s.Has(Feature.Whiskers))
                    for (int side = -1; side <= 1; side += 2)
                        Limb(body, h + new Vector3(side * 0.15f, -0.05f, 0.1f), h + new Vector3(side * 0.6f, -0.3f, 0.35f), 0.03f, accent);
                if (s.Has(Feature.Mane))
                    Prim(PrimitiveType.Sphere, body, h + new Vector3(0, 0, -hs * 0.2f), Vector3.one * hs * 1.4f, accent);
            }

            // Body features.
            if (s.Has(Feature.Wings))
            {
                for (int side = -1; side <= 1; side += 2)
                {
                    var anchor = new Vector3(wingAnchor.x * side, wingAnchor.y, wingAnchor.z);
                    Prim(PrimitiveType.Cube, body, anchor + new Vector3(side * 0.4f, 0.25f, -0.05f), new Vector3(0.9f, 0.05f, 0.5f),
                        accent, new Vector3(0, 0, side * 30));
                }
            }
            if (s.Has(Feature.Stinger))
            {
                Vector3 a = tailBase, b = a + new Vector3(0, 0.55f, -0.3f), c = b + new Vector3(0, 0.45f, 0.3f);
                Limb(body, a, b, 0.12f, dark);
                Limb(body, b, c, 0.1f, dark);
                Prim(PrimitiveType.Sphere, body, c + new Vector3(0, 0, 0.1f), new Vector3(0.18f, 0.18f, 0.3f), accent);
            }
            else if (tailBase != Vector3.zero && s.Body != BodyKind.SmallBeast)
            {
                Limb(body, tailBase, tailBase + new Vector3(0, 0.35f, -0.45f), 0.08f, s.Has(Feature.Mane) ? accent : dark);
            }
            if (s.Has(Feature.Spikes))
                for (int i = 0; i < 4; i++)
                    Prim(PrimitiveType.Cube, body, new Vector3(0, 0.85f, 0.45f - i * 0.3f), Vector3.one * 0.18f, accent, new Vector3(45, 0, 0));

            var col = root.AddComponent<CapsuleCollider>();
            col.center = new Vector3(0, height * 0.5f, 0);
            col.height = height;
            col.radius = 0.75f;

            result.Root = root;
            result.Body = body;
            result.Height = height;
            return result;
        }

        static void FourLegs(Transform body, float x, float z, float hipY, float radius, Color c)
        {
            for (int sx = -1; sx <= 1; sx += 2)
                for (int sz = -1; sz <= 1; sz += 2)
                    Limb(body, new Vector3(sx * x, hipY, sz * z), new Vector3(sx * x, 0f, sz * z), radius * 2f, c);
        }
    }
}
