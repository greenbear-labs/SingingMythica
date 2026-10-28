using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Mythica
{
    // Everything the UI is made of, generated in code: rounded sprites, icons, chunky buttons, panels.
    // Swap these sprites for painted art later without touching the screens that use them.
    public static class UIKit
    {
        public static Font Font;
        public static Sprite Rounded;      // 9-sliced rounded rectangle
        public static Sprite Circle;
        public static Sprite Coin, Food, Crystal, Star, Egg, Lock, Heart, Bag, Book, Gear, Note, Clock, Leaf, Speaker, Muted;

        // Palette: warm cartoon panels with saturated buttons.
        public static readonly Color Cream = new Color(1f, 0.96f, 0.86f);
        public static readonly Color Wood = new Color(0.42f, 0.26f, 0.14f);
        public static readonly Color Ink = new Color(0.16f, 0.1f, 0.06f);
        public static readonly Color Green = new Color(0.36f, 0.78f, 0.25f);
        public static readonly Color Orange = new Color(1f, 0.62f, 0.15f);
        public static readonly Color Blue = new Color(0.25f, 0.6f, 0.95f);
        public static readonly Color Purple = new Color(0.62f, 0.36f, 0.9f);
        public static readonly Color Red = new Color(0.92f, 0.3f, 0.28f);
        public static readonly Color Grey = new Color(0.62f, 0.6f, 0.58f);
        public static readonly Color Gold = new Color(1f, 0.82f, 0.2f);

        static bool ready;

        public static void Init()
        {
            if (ready) return;
            ready = true;
            Font = LoadFont();
            Rounded = Sliced(Paint(96, p => Fill(RoundBox(p, 0.92f, 0.92f, 0.5f), Color.white)), 34);
            Circle = MakeSprite(Paint(128, p => Fill(Disc(p, 0.96f), Color.white)));

            Coin = Icon(p =>
            {
                Color c = Fill(Disc(p, 0.92f), new Color(0.85f, 0.55f, 0.08f));
                c = Over(c, Fill(Disc(p, 0.74f), new Color(1f, 0.82f, 0.2f)));
                c = Over(c, Fill(StarShape(p * 1.45f, 0.62f, 0.27f), new Color(1f, 0.93f, 0.55f)));
                return Over(c, Fill(Ellipse(p - new Vector2(-0.32f, 0.38f), 0.2f, 0.12f), new Color(1, 1, 1, 0.7f)));
            });
            Food = Icon(p =>
            {
                Color c = Fill(Disc(p - new Vector2(0, -0.12f), 0.72f), new Color(0.88f, 0.16f, 0.32f));
                c = Over(c, Fill(Ellipse(p - new Vector2(-0.25f, 0.12f), 0.16f, 0.24f), new Color(1, 1, 1, 0.55f)));
                c = Over(c, Fill(Ellipse(Rot(p - new Vector2(0.22f, 0.66f), 0.6f), 0.36f, 0.16f), new Color(0.3f, 0.75f, 0.25f)));
                return Over(c, Fill(Box(p - new Vector2(0.02f, 0.62f), 0.05f, 0.18f), new Color(0.4f, 0.25f, 0.1f)));
            });
            Crystal = Icon(p =>
            {
                var poly = new[] { new Vector2(0, 0.95f), new Vector2(0.62f, 0.35f), new Vector2(0.4f, -0.6f), new Vector2(0, -0.95f), new Vector2(-0.4f, -0.6f), new Vector2(-0.62f, 0.35f) };
                Color c = Fill(Poly(p, poly), new Color(0.55f, 0.35f, 0.95f));
                var facet = new[] { new Vector2(0, 0.95f), new Vector2(0, -0.95f), new Vector2(-0.4f, -0.6f), new Vector2(-0.62f, 0.35f) };
                c = Over(c, Fill(Poly(p, facet), new Color(0.75f, 0.6f, 1f)));
                return Over(c, Fill(Ellipse(p - new Vector2(-0.2f, 0.42f), 0.1f, 0.2f), new Color(1, 1, 1, 0.8f)));
            });
            Star = Icon(p => Over(Fill(StarShape(p, 0.98f, 0.45f), new Color(0.95f, 0.6f, 0.05f)),
                                  Fill(StarShape(p * 1.25f, 0.98f, 0.45f), new Color(1f, 0.86f, 0.25f))));
            Egg = Icon(p =>
            {
                var q = new Vector2(p.x * (p.y < 0 ? 1.25f : 1.25f + p.y * 0.55f), p.y);
                Color c = Fill(Disc(q, 0.88f), new Color(1f, 0.95f, 0.8f));
                c = Over(c, Fill(Disc(p - new Vector2(0.2f, -0.2f), 0.13f), new Color(0.55f, 0.75f, 1f)));
                c = Over(c, Fill(Disc(p - new Vector2(-0.22f, 0.25f), 0.1f), new Color(1f, 0.6f, 0.75f)));
                return Over(c, Fill(Disc(p - new Vector2(-0.1f, -0.5f), 0.09f), new Color(0.6f, 0.9f, 0.5f)));
            });
            Lock = Icon(p =>
            {
                Color c = Fill(Ring(p - new Vector2(0, 0.25f), 0.42f, 0.13f) * Step(p.y - 0.2f), new Color(0.55f, 0.55f, 0.6f));
                return Over(c, Fill(RoundBox(p - new Vector2(0, -0.3f), 0.68f, 0.52f, 0.15f), new Color(1f, 0.75f, 0.2f)));
            });
            Heart = Icon(p =>
            {
                var q = new Vector2(p.x, p.y + 0.15f);
                float h = Mathf.Max(Disc(q - new Vector2(-0.33f, 0.25f), 0.42f), Disc(q - new Vector2(0.33f, 0.25f), 0.42f));
                h = Mathf.Max(h, Poly(q, new[] { new Vector2(-0.73f, 0.12f), new Vector2(0.73f, 0.12f), new Vector2(0, -0.78f) }));
                return Over(Fill(h, new Color(0.95f, 0.3f, 0.5f)), Fill(Ellipse(q - new Vector2(-0.38f, 0.38f), 0.12f, 0.09f), new Color(1, 1, 1, 0.7f)));
            });
            Bag = Icon(p =>
            {
                Color c = Fill(Ring(p - new Vector2(0, 0.35f), 0.32f, 0.1f) * Step(p.y - 0.35f), new Color(0.45f, 0.25f, 0.1f));
                c = Over(c, Fill(RoundBox(p - new Vector2(0, -0.2f), 0.85f, 0.7f, 0.18f), new Color(1f, 0.55f, 0.15f)));
                return Over(c, Fill(StarShape((p - new Vector2(0, -0.2f)) * 2.6f, 0.9f, 0.42f), new Color(1f, 0.9f, 0.4f)));
            });
            Book = Icon(p =>
            {
                Color c = Fill(RoundBox(p, 0.8f, 0.9f, 0.1f), new Color(0.35f, 0.5f, 0.9f));
                c = Over(c, Fill(Box(p - new Vector2(0.55f, 0), 0.12f, 0.8f), new Color(1f, 0.95f, 0.85f)));
                return Over(c, Fill(StarShape((p - new Vector2(-0.12f, 0.05f)) * 2.4f, 0.9f, 0.42f), new Color(1f, 0.85f, 0.3f)));
            });
            Gear = Icon(p =>
            {
                float a = Mathf.Atan2(p.y, p.x);
                float r = 0.72f + 0.16f * Mathf.Clamp(Mathf.Sin(a * 8f) * 3f, -1f, 1f);
                float g = Mathf.Clamp01((r - p.magnitude) * 40f) * (1f - Disc(p, 0.3f));
                return Fill(g, new Color(0.9f, 0.88f, 0.85f));
            });
            Note = Icon(p =>
            {
                Color c = Fill(Ellipse(Rot(p - new Vector2(-0.22f, -0.55f), 0.4f), 0.32f, 0.22f), Color.white);
                c = Over(c, Fill(Box(p - new Vector2(0.06f, 0.1f), 0.07f, 0.65f), Color.white));
                return Over(c, Fill(Poly(p, new[] { new Vector2(0.0f, 0.75f), new Vector2(0.55f, 0.45f), new Vector2(0.55f, 0.25f), new Vector2(0.0f, 0.5f) }), Color.white));
            });
            Clock = Icon(p =>
            {
                Color c = Fill(Disc(p, 0.9f), new Color(0.3f, 0.65f, 0.95f));
                c = Over(c, Fill(Disc(p, 0.72f), Color.white));
                c = Over(c, Fill(Box(p - new Vector2(0, 0.25f), 0.06f, 0.3f), Ink));
                return Over(c, Fill(Box(Rot(p, -1.0f) - new Vector2(0.18f, 0), 0.2f, 0.06f), Ink));
            });
            Leaf = Icon(p => Fill(Ellipse(Rot(p, 0.7f), 0.85f, 0.42f), new Color(0.35f, 0.78f, 0.3f)));
            Speaker = Icon(p =>
            {
                Color c = Fill(Poly(p, new[] { new Vector2(-0.75f, 0.25f), new Vector2(-0.4f, 0.25f), new Vector2(0.05f, 0.7f), new Vector2(0.05f, -0.7f), new Vector2(-0.4f, -0.25f), new Vector2(-0.75f, -0.25f) }), Color.white);
                c = Over(c, Fill(Ring(p - new Vector2(0.05f, 0), 0.38f, 0.08f) * Step(p.x - 0.2f), Color.white));
                return Over(c, Fill(Ring(p - new Vector2(0.05f, 0), 0.68f, 0.08f) * Step(p.x - 0.3f), Color.white));
            });
            Muted = Icon(p =>
            {
                Color c = Fill(Poly(p, new[] { new Vector2(-0.75f, 0.25f), new Vector2(-0.4f, 0.25f), new Vector2(0.05f, 0.7f), new Vector2(0.05f, -0.7f), new Vector2(-0.4f, -0.25f), new Vector2(-0.75f, -0.25f) }), Color.white);
                c = Over(c, Fill(Box(Rot(p - new Vector2(0.48f, 0), 0.785f), 0.07f, 0.35f), new Color(1f, 0.4f, 0.4f)));
                return Over(c, Fill(Box(Rot(p - new Vector2(0.48f, 0), -0.785f), 0.07f, 0.35f), new Color(1f, 0.4f, 0.4f)));
            });
        }

        static Font LoadFont()
        {
            Font f = null;
            foreach (var name in new[] { "LegacyRuntime.ttf", "Arial.ttf" })
            {
                try { f = Resources.GetBuiltinResource<Font>(name); } catch (Exception) { f = null; }
                if (f != null) return f;
            }
            return f;
        }

        // ------------------------------------------------------------ texture painting

        const int SS = 2;   // supersamples per axis; enough to smooth edges at icon sizes

        // Paints a size x size texture from a shader taking coords in -1..1 (y up), with SS x SS supersampling.
        static Texture2D Paint(int size, Func<Vector2, Color> shader)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            var px = new Color[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    Color acc = Color.clear;
                    for (int sy = 0; sy < SS; sy++)
                        for (int sx = 0; sx < SS; sx++)
                        {
                            var p = new Vector2((x + (sx + 0.5f) / SS) / size * 2f - 1f, (y + (sy + 0.5f) / SS) / size * 2f - 1f);
                            Color c = shader(p);
                            acc += new Color(c.r * c.a, c.g * c.a, c.b * c.a, c.a);
                        }
                    acc /= SS * SS;
                    px[y * size + x] = acc.a > 0.0001f ? new Color(acc.r / acc.a, acc.g / acc.a, acc.b / acc.a, acc.a) : Color.clear;
                }
            tex.SetPixels(px);
            tex.Apply();
            return tex;
        }

        static Sprite Icon(Func<Vector2, Color> shader) { return MakeSprite(Paint(128, shader)); }

        static Sprite MakeSprite(Texture2D t)
        {
            return UnityEngine.Sprite.Create(t, new Rect(0, 0, t.width, t.height), new Vector2(0.5f, 0.5f), 100f);
        }

        static Sprite Sliced(Texture2D t, float border)
        {
            return UnityEngine.Sprite.Create(t, new Rect(0, 0, t.width, t.height), new Vector2(0.5f, 0.5f), 100f, 0,
                SpriteMeshType.FullRect, new Vector4(border, border, border, border));
        }

        // Coverage functions return 0..1 (hard edges; supersampling smooths them).
        static float Step(float v) { return v >= 0f ? 1f : 0f; }
        static float Disc(Vector2 p, float r) { return Step(r - p.magnitude); }
        static float Ellipse(Vector2 p, float rx, float ry) { return Step(1f - (p.x * p.x) / (rx * rx) - (p.y * p.y) / (ry * ry)); }
        static float Ring(Vector2 p, float r, float w) { return Step(w - Mathf.Abs(p.magnitude - r)); }
        static float Box(Vector2 p, float hx, float hy) { return Step(hx - Mathf.Abs(p.x)) * Step(hy - Mathf.Abs(p.y)); }

        static float RoundBox(Vector2 p, float hx, float hy, float r)
        {
            var q = new Vector2(Mathf.Abs(p.x) - (hx - r), Mathf.Abs(p.y) - (hy - r));
            float outside = new Vector2(Mathf.Max(q.x, 0), Mathf.Max(q.y, 0)).magnitude + Mathf.Min(Mathf.Max(q.x, q.y), 0f) - r;
            return Step(-outside);
        }

        static float Poly(Vector2 p, Vector2[] pts)
        {
            bool inside = false;
            for (int i = 0, j = pts.Length - 1; i < pts.Length; j = i++)
                if ((pts[i].y > p.y) != (pts[j].y > p.y) &&
                    p.x < (pts[j].x - pts[i].x) * (p.y - pts[i].y) / (pts[j].y - pts[i].y) + pts[i].x)
                    inside = !inside;
            return inside ? 1f : 0f;
        }

        static float StarShape(Vector2 p, float outer, float inner)
        {
            var pts = new Vector2[10];
            for (int i = 0; i < 10; i++)
            {
                float a = Mathf.PI / 2f + i * Mathf.PI / 5f;
                float r = i % 2 == 0 ? outer : inner;
                pts[i] = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
            }
            return Poly(p, pts);
        }

        static Vector2 Rot(Vector2 p, float a)
        {
            float c = Mathf.Cos(a), s = Mathf.Sin(a);
            return new Vector2(p.x * c - p.y * s, p.x * s + p.y * c);
        }

        static Color Fill(float coverage, Color c) { return new Color(c.r, c.g, c.b, c.a * coverage); }

        static Color Over(Color under, Color over)
        {
            float a = over.a + under.a * (1f - over.a);
            if (a < 0.0001f) return Color.clear;
            Color rgb = (over * over.a + under * under.a * (1f - over.a)) / a;
            return new Color(rgb.r, rgb.g, rgb.b, a);
        }

        // ------------------------------------------------------------ building blocks

        public static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        // Places a rect by anchor point (0..1 of parent), pivot and size.
        public static RectTransform Place(RectTransform rt, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            return rt;
        }

        public static RectTransform Stretch(RectTransform rt, float inset = 0f)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(inset, inset);
            rt.offsetMax = new Vector2(-inset, -inset);
            return rt;
        }

        public static Image Img(Transform parent, Sprite sprite, Color color, string name = "Image")
        {
            var img = Rect(name, parent).gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            if (sprite == Rounded) img.type = Image.Type.Sliced;
            else img.preserveAspect = true;
            return img;
        }

        public static Text Label(Transform parent, string text, int size, Color color, TextAnchor align = TextAnchor.MiddleCenter, bool outline = false)
        {
            var t = Rect("Text", parent).gameObject.AddComponent<Text>();
            t.font = Font;
            t.text = text;
            t.fontSize = size;
            t.fontStyle = FontStyle.Bold;
            t.color = color;
            t.alignment = align;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            if (outline)
            {
                var o = t.gameObject.AddComponent<Outline>();
                o.effectColor = new Color(0.12f, 0.07f, 0.04f, 0.9f);
                o.effectDistance = new Vector2(2f, -2f);
            }
            return t;
        }

        // A cream panel with a dark wooden edge, like a storybook card.
        public static Image Panel(Transform parent, Color? fill = null)
        {
            var edge = Img(parent, Rounded, Wood, "Panel");
            var sh = edge.gameObject.AddComponent<Shadow>();
            sh.effectColor = new Color(0, 0, 0, 0.35f);
            sh.effectDistance = new Vector2(0, -6);
            var inner = Img(edge.transform, Rounded, fill ?? Cream, "Fill");
            Stretch(inner.rectTransform, 5f);
            inner.raycastTarget = false;
            return edge;
        }

        // A chunky button: a darker base with a raised face that sinks when pressed.
        public static Button Button(Transform parent, string label, Color color, Action onClick, int fontSize = 24, Sprite icon = null, Sprite shape = null)
        {
            shape = shape ?? Rounded;
            var root = Img(parent, shape, Darken(color, 0.62f), "Button");
            if (shape != Rounded) root.preserveAspect = false;
            var face = Img(root.transform, shape, color, "Face");
            if (shape != Rounded) face.preserveAspect = false;
            face.raycastTarget = false;
            var fr = face.rectTransform;
            fr.anchorMin = Vector2.zero;
            fr.anchorMax = Vector2.one;
            fr.offsetMin = new Vector2(0, 6);
            fr.offsetMax = Vector2.zero;
            var shine = Img(face.transform, shape, new Color(1, 1, 1, 0.22f), "Shine");
            if (shape != Rounded) shine.preserveAspect = false;
            shine.raycastTarget = false;
            var sr = shine.rectTransform;
            sr.anchorMin = new Vector2(0.06f, 0.55f);
            sr.anchorMax = new Vector2(0.94f, 0.92f);
            sr.offsetMin = sr.offsetMax = Vector2.zero;

            var content = Rect("Content", face.transform);
            Stretch(content);
            if (icon != null && string.IsNullOrEmpty(label))
            {
                var ic = Img(content, icon, Color.white, "Icon");
                Stretch(ic.rectTransform, 10f);
            }
            else if (icon != null)
            {
                var ic = Img(content, icon, Color.white, "Icon");
                ic.rectTransform.anchorMin = new Vector2(0, 0.5f);
                ic.rectTransform.anchorMax = new Vector2(0, 0.5f);
                ic.rectTransform.pivot = new Vector2(0, 0.5f);
                ic.rectTransform.anchoredPosition = new Vector2(10, 0);
                ic.rectTransform.sizeDelta = new Vector2(fontSize * 1.4f, fontSize * 1.4f);
                var t = Label(content, label, fontSize, Color.white, TextAnchor.MiddleCenter, true);
                Stretch(t.rectTransform);
                t.rectTransform.offsetMin = new Vector2(fontSize * 1.4f + 12, 0);
                t.rectTransform.offsetMax = new Vector2(-8, 0);
            }
            else
            {
                var t = Label(content, label, fontSize, Color.white, TextAnchor.MiddleCenter, true);
                Stretch(t.rectTransform, 4f);
            }

            var b = root.gameObject.AddComponent<Button>();
            b.transition = Selectable.Transition.None;
            b.targetGraphic = root;
            if (onClick != null) b.onClick.AddListener(() => onClick());
            root.gameObject.AddComponent<PressFeel>().Face = fr;
            return b;
        }

        public static void SetButtonLabel(Button b, string text)
        {
            var t = b.GetComponentInChildren<Text>();
            if (t != null) t.text = text;
        }

        public static void SetButtonColor(Button b, Color c)
        {
            b.GetComponent<Image>().color = Darken(c, 0.62f);
            b.transform.Find("Face").GetComponent<Image>().color = c;
        }

        public static void SetInteractable(Button b, bool on)
        {
            b.interactable = on;
            var cg = b.GetComponent<CanvasGroup>() ?? b.gameObject.AddComponent<CanvasGroup>();
            cg.alpha = on ? 1f : 0.5f;
        }

        public static Color Darken(Color c, float k) { return new Color(c.r * k, c.g * k, c.b * k, c.a); }

        // A dark translucent pill with an icon on the left and a number: the currency counters.
        public static Text Counter(Transform parent, Sprite icon, Vector2 anchor, Vector2 pos, float width, out RectTransform iconRect)
        {
            var pill = Img(parent, Rounded, new Color(0.1f, 0.06f, 0.03f, 0.6f), "Counter");
            Place(pill.rectTransform, anchor, new Vector2(0, 1), pos + new Vector2(22, -6), new Vector2(width, 44));
            pill.raycastTarget = false;
            var ic = Img(parent, icon, Color.white, "CounterIcon");
            Place(ic.rectTransform, anchor, new Vector2(0.5f, 0.5f), pos + new Vector2(22, -28), new Vector2(58, 58));
            ic.raycastTarget = false;
            iconRect = ic.rectTransform;
            var t = Label(pill.transform, "0", 26, Color.white, TextAnchor.MiddleRight, true);
            Stretch(t.rectTransform);
            t.rectTransform.offsetMin = new Vector2(34, 0);
            t.rectTransform.offsetMax = new Vector2(-14, 0);
            return t;
        }

        // ------------------------------------------------------------ formatting

        public static string Num(long n)
        {
            if (n >= 1000000) return (n / 1000000f).ToString(n >= 10000000 ? "0" : "0.0") + "M";
            if (n >= 10000) return (n / 1000f).ToString(n >= 100000 ? "0" : "0.0") + "K";
            return n.ToString("N0");
        }

        public static string Time(long seconds)
        {
            if (seconds < 0) seconds = 0;
            if (seconds >= 3600) return (seconds / 3600) + "h " + (seconds % 3600 / 60) + "m";
            if (seconds >= 60) return (seconds / 60) + "m " + (seconds % 60).ToString("00") + "s";
            return seconds + "s";
        }
    }

    // Button press feel: the face sinks into its base and the whole button squishes a little.
    public class PressFeel : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        public RectTransform Face;
        bool down;

        public void OnPointerDown(PointerEventData e)
        {
            var b = GetComponent<Button>();
            if (b != null && !b.interactable) return;
            down = true;
            Face.offsetMin = new Vector2(0, 1);
            Face.offsetMax = new Vector2(0, -5);
            transform.localScale = new Vector3(0.96f, 0.96f, 1f);
        }

        public void OnPointerUp(PointerEventData e) { Release(); }
        public void OnPointerExit(PointerEventData e) { if (down) Release(); }

        void Release()
        {
            down = false;
            Face.offsetMin = new Vector2(0, 6);
            Face.offsetMax = Vector2.zero;
            transform.localScale = Vector3.one;
            if (isActiveAndEnabled) StartCoroutine(Tween.Punch(transform, 0.08f));
        }
    }

    public static class Tween
    {
        static float EaseOutBack(float t)
        {
            const float c1 = 1.70158f, c3 = c1 + 1f;
            return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
        }

        public static IEnumerator PopIn(Transform t, float duration = 0.28f)
        {
            for (float e = 0; e < duration; e += UnityEngine.Time.unscaledDeltaTime)
            {
                if (t == null) yield break;
                float s = EaseOutBack(Mathf.Clamp01(e / duration));
                t.localScale = new Vector3(s, s, 1f);
                yield return null;
            }
            if (t != null) t.localScale = Vector3.one;
        }

        public static IEnumerator Punch(Transform t, float amount = 0.15f, float duration = 0.22f)
        {
            for (float e = 0; e < duration; e += UnityEngine.Time.unscaledDeltaTime)
            {
                if (t == null) yield break;
                float k = 1f + amount * Mathf.Sin(e / duration * Mathf.PI);
                t.localScale = new Vector3(k, k, 1f);
                yield return null;
            }
            if (t != null) t.localScale = Vector3.one;
        }
    }
}
