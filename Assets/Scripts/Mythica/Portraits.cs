using System.Collections.Generic;
using UnityEngine;

namespace Mythica
{
    // Renders each creature variant once into a small texture for menu cards, so the Market,
    // Nursery and Bestiary show the real creature instead of a generic icon.
    public static class Portraits
    {
        public const int Layer = 31;
        const int Size = 160;
        static readonly Vector3 Stage = new Vector3(0, -200f, 0);

        static readonly Dictionary<string, Texture> cache = new Dictionary<string, Texture>();
        static Camera cam;

        public static Texture Get(Species s, Magic m)
        {
            string key = MythicaDB.VariantKey(s.Id, m);
            Texture tex;
            if (cache.TryGetValue(key, out tex) && tex != null) return tex;
            var built = CreatureBuilder.Build(s, m);
            return cache[key] = Render(built.Root, built.Height, 150f);
        }

        public static Texture Get(DecorationDef d)
        {
            string key = "decor:" + d.Id;
            Texture tex;
            if (cache.TryGetValue(key, out tex) && tex != null) return tex;
            return cache[key] = Render(Scenery.Decoration(d, null), d.Id == "lantern" || d.Id == "tree" ? 2.2f : 1.4f, 160f);
        }

        static Texture Render(GameObject root, float h, float yaw)
        {
            if (cam == null)
            {
                var go = new GameObject("Portrait Camera");
                cam = go.AddComponent<Camera>();
                cam.enabled = false;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0, 0, 0, 0);
                cam.cullingMask = 1 << Layer;
                cam.fieldOfView = 30f;
                cam.nearClipPlane = 0.1f;
                cam.farClipPlane = 50f;
            }

            root.transform.position = Stage;
            root.transform.rotation = Quaternion.Euler(0, yaw, 0);
            SetLayer(root.transform);

            var look = Stage + Vector3.up * h * 0.5f;
            float dist = Mathf.Max(h, 1.8f) * 2.3f + 0.6f;
            cam.transform.position = look + new Vector3(0, h * 0.25f, -dist);
            cam.transform.LookAt(look);

            var rt = new RenderTexture(Size, Size, 16, RenderTextureFormat.ARGB32);
            rt.Create();
            cam.targetTexture = rt;
            cam.Render();
            cam.targetTexture = null;

            root.SetActive(false);
            Object.Destroy(root);
            return rt;
        }

        static void SetLayer(Transform t)
        {
            t.gameObject.layer = Layer;
            foreach (Transform child in t) SetLayer(child);
        }
    }
}
