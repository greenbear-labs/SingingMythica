using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Mythica
{
    // The whole game UI, in the style of a monster-sanctuary game: currency counters and level up top,
    // round Market and menu buttons at the bottom, a card for the tapped creature, floating coin
    // bubbles over the island, and pop-up panels for the Market, buildings and Bestiary.
    public class GameHUD : MonoBehaviour
    {
        GameManager gm;
        Canvas canvas;
        CanvasScaler scaler;
        RectTransform root, worldLayer, hudLayer, fxLayer, modalLayer, toastLayer;
        Vector2 lastSize;
        bool portrait;

        // Top bar
        RectTransform levelBox, coinBox, foodBox, crystalBox, coinIcon, foodIcon, crystalIcon, starIcon;
        Text levelText, xpText, coinText, foodText, crystalText;
        RectTransform xpFill;
        double shownCoins, shownFood, shownCrystals;

        // Bottom buttons
        RectTransform leftButtons, rightButtons;

        // Selection cards
        RectTransform card, decorCard;
        RawImage cardPortrait;
        Image cardRing;
        Text cardName, cardInfo, cardCoins, cardFeedHint, decorName, decorInfo;
        readonly List<Image> cardPips = new List<Image>();
        Button feedButton, muteButton, soloButton, moveButton, sellButton, decorSellButton;
        Creature cardCreature;

        // Placement banner
        RectTransform banner;
        Text bannerText;

        // World bubbles
        readonly List<Bubble> bubbles = new List<Bubble>();
        readonly Dictionary<StructureKind, StructureTag> tags = new Dictionary<StructureKind, StructureTag>();

        // Modal
        RectTransform modal, modalContent;
        Action<RectTransform> modalBuilder;
        readonly List<Action> refreshers = new List<Action>();
        float modalScroll = 1f;
        int pendingLevelUp;

        int marketTab;
        Creature breedA, breedB;
        float resetArmedUntil;

        GameObject startScreen;

        class Bubble : MonoBehaviour { public Creature Target; }

        // Keeps a scrolling grid exactly as tall as its rows, growing downward from the top.
        class GridHeight : MonoBehaviour
        {
            public RectTransform View;

            void LateUpdate() { Apply(); }

            public void Apply()
            {
                var rt = (RectTransform)transform;
                var grid = GetComponent<GridLayoutGroup>();
                float width = View.rect.width - grid.padding.horizontal;
                int cols = Mathf.Max(1, Mathf.FloorToInt((width + grid.spacing.x) / (grid.cellSize.x + grid.spacing.x)));
                int rows = Mathf.CeilToInt(transform.childCount / (float)cols);
                float h = grid.padding.vertical + rows * grid.cellSize.y + Mathf.Max(0, rows - 1) * grid.spacing.y;
                if (Mathf.Abs(rt.sizeDelta.y - h) > 0.5f) rt.sizeDelta = new Vector2(rt.sizeDelta.x, h);
            }
        }

        class StructureTag
        {
            public RectTransform Rect;
            public Text Status;
            public Image Icon;
        }

        void Awake()
        {
            gm = GameManager.Instance;
            UIKit.Init();
            BuildCanvas();
            BuildTopBar();
            BuildBottomButtons();
            BuildCards();
            BuildBanner();
            BuildStructureTags();
            BuildStartScreen();

            gm.Toasted += ShowToast;
            gm.Gained += OnGained;
            gm.LeveledUp += OnLevelUp;
            gm.StructureTapped += OnStructureTapped;
            gm.SelectionChanged += OnSelectionChanged;
            gm.StateChanged += OnStateChanged;

            shownCoins = gm.Save.coins;
            shownFood = gm.Save.food;
            shownCrystals = gm.Save.crystals;
            OnSelectionChanged();
        }

        // ------------------------------------------------------------ canvas and layout

        void BuildCanvas()
        {
            if (EventSystem.current == null && FindEventSystem() == null)
            {
                var es = new GameObject("EventSystem");
                es.AddComponent<EventSystem>();
                es.AddComponent<StandaloneInputModule>();
            }
            var go = new GameObject("HUD", typeof(RectTransform));
            canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            go.AddComponent<GraphicRaycaster>();
            root = (RectTransform)go.transform;
            ApplyScaler();

            worldLayer = UIKit.Stretch(UIKit.Rect("World", root));
            hudLayer = UIKit.Stretch(UIKit.Rect("Hud", root));
            fxLayer = UIKit.Stretch(UIKit.Rect("Fx", root));
            modalLayer = UIKit.Stretch(UIKit.Rect("Modal", root));
            toastLayer = UIKit.Stretch(UIKit.Rect("Toasts", root));
        }

        static EventSystem FindEventSystem()
        {
#if UNITY_2023_1_OR_NEWER
            return FindAnyObjectByType<EventSystem>();
#else
            return FindObjectOfType<EventSystem>();
#endif
        }

        void ApplyScaler()
        {
            portrait = Screen.height > Screen.width;
            // Phones in portrait get a narrower reference, so buttons stay finger sized.
            scaler.referenceResolution = portrait ? new Vector2(720, 1280) : new Vector2(1280, 720);
            scaler.matchWidthOrHeight = portrait ? 0f : 1f;
        }

        Vector2 CanvasSize { get { return root.rect.size; } }

        void Relayout()
        {
            ApplyScaler();
            UIKit.Place(levelBox, new Vector2(0, 1), new Vector2(0, 1), new Vector2(16, -14), new Vector2(290, 72));
            if (portrait)
            {
                float w = (CanvasSize.x - 32f) / 3f;
                UIKit.Place(coinBox, new Vector2(0, 1), new Vector2(0, 1), new Vector2(16, -96), new Vector2(w, 60));
                UIKit.Place(foodBox, new Vector2(0, 1), new Vector2(0, 1), new Vector2(16 + w, -96), new Vector2(w, 60));
                UIKit.Place(crystalBox, new Vector2(0, 1), new Vector2(0, 1), new Vector2(16 + 2 * w, -96), new Vector2(w, 60));
            }
            else
            {
                UIKit.Place(crystalBox, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-16, -14), new Vector2(170, 60));
                UIKit.Place(foodBox, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-196, -14), new Vector2(190, 60));
                UIKit.Place(coinBox, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-396, -14), new Vector2(210, 60));
            }
            foreach (var box in new[] { coinBox, foodBox, crystalBox })
                box.Find("Counter").GetComponent<RectTransform>().sizeDelta = new Vector2(box.sizeDelta.x - 34, 44);

            // Wide screens fit the card between the corner buttons; narrower ones lift it above them.
            bool between = !portrait && CanvasSize.x >= 1220f;
            float bottomOffset = between ? 12f : 150f;
            float cardW = between ? Mathf.Min(760f, CanvasSize.x - 530f) : Mathf.Min(780f, CanvasSize.x - 24f);
            UIKit.Place(card, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(between ? -10f : 0f, bottomOffset), new Vector2(cardW, 186));
            UIKit.Place(decorCard, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, bottomOffset), new Vector2(Mathf.Min(520f, CanvasSize.x - 24f), 120));
            UIKit.Place(banner, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, bottomOffset + 6), new Vector2(Mathf.Min(640f, CanvasSize.x - 24f), 92));
        }

        // ------------------------------------------------------------ top bar

        void BuildTopBar()
        {
            levelBox = UIKit.Rect("Level", hudLayer);
            var barBg = UIKit.Img(levelBox, UIKit.Rounded, new Color(0.1f, 0.06f, 0.03f, 0.6f), "XpBar");
            UIKit.Place(barBg.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(40, -6), new Vector2(230, 40));
            barBg.raycastTarget = false;
            var fill = UIKit.Img(barBg.transform, UIKit.Rounded, UIKit.Green, "Fill");
            xpFill = fill.rectTransform;
            xpFill.anchorMin = Vector2.zero;
            xpFill.anchorMax = new Vector2(0.5f, 1f);
            xpFill.offsetMin = new Vector2(4, 4);
            xpFill.offsetMax = new Vector2(-4, -4);
            fill.raycastTarget = false;
            xpText = UIKit.Label(barBg.transform, "", 18, Color.white, TextAnchor.MiddleCenter, true);
            UIKit.Stretch(xpText.rectTransform);
            xpText.rectTransform.offsetMin = new Vector2(44, 0);
            var star = UIKit.Img(levelBox, UIKit.Star, Color.white, "Star");
            starIcon = star.rectTransform;
            UIKit.Place(starIcon, new Vector2(0, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(38, -2), new Vector2(76, 76));
            star.raycastTarget = false;
            levelText = UIKit.Label(star.transform, "1", 28, Color.white, TextAnchor.MiddleCenter, true);
            UIKit.Stretch(levelText.rectTransform);
            levelText.rectTransform.offsetMin = new Vector2(0, -4);

            coinBox = CounterBox("Coins", UIKit.Coin, out coinText, out coinIcon);
            foodBox = CounterBox("Food", UIKit.Food, out foodText, out foodIcon);
            crystalBox = CounterBox("Crystals", UIKit.Crystal, out crystalText, out crystalIcon);
        }

        RectTransform CounterBox(string name, Sprite icon, out Text text, out RectTransform iconRect)
        {
            var box = UIKit.Rect(name, hudLayer);
            text = UIKit.Counter(box, icon, new Vector2(0, 1), Vector2.zero, 150, out iconRect);
            return box;
        }

        // ------------------------------------------------------------ bottom buttons

        void BuildBottomButtons()
        {
            leftButtons = UIKit.Place(UIKit.Rect("Left Buttons", hudLayer), Vector2.zero, Vector2.zero, new Vector2(14, 12), new Vector2(240, 120));
            RoundButton(leftButtons, UIKit.Gear, UIKit.Blue, "Settings", new Vector2(50, 66), 84, OpenSettings);
            RoundButton(leftButtons, UIKit.Book, UIKit.Purple, "Bestiary", new Vector2(150, 66), 84, OpenBestiary);

            rightButtons = UIKit.Place(UIKit.Rect("Right Buttons", hudLayer), new Vector2(1, 0), new Vector2(1, 0), new Vector2(-14, 12), new Vector2(260, 150));
            RoundButton(rightButtons, UIKit.Coin, UIKit.Gold, "Collect", new Vector2(60, 58), 84, () => gm.CollectAll());
            RoundButton(rightButtons, UIKit.Bag, UIKit.Green, "Market", new Vector2(186, 76), 128, () => OpenMarket(marketTab));
        }

        Button RoundButton(Transform parent, Sprite icon, Color color, string caption, Vector2 center, float size, Action onClick)
        {
            var b = UIKit.Button(parent, "", color, onClick, 24, icon, UIKit.Circle);
            UIKit.Place((RectTransform)b.transform, Vector2.zero, new Vector2(0.5f, 0.5f), center, new Vector2(size, size));
            var t = UIKit.Label(b.transform, caption, size > 100 ? 26 : 19, Color.white, TextAnchor.MiddleCenter, true);
            UIKit.Place(t.rectTransform, new Vector2(0.5f, 0), new Vector2(0.5f, 0.5f), new Vector2(0, 2), new Vector2(size + 40, 30));
            return b;
        }

        // ------------------------------------------------------------ creature and decoration cards

        void BuildCards()
        {
            card = UIKit.Panel(hudLayer).rectTransform;
            card.name = "Creature Card";
            cardRing = UIKit.Img(card, UIKit.Circle, Color.white, "Ring");
            UIKit.Place(cardRing.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(14, 0), new Vector2(128, 128));
            var inner = UIKit.Img(cardRing.transform, UIKit.Circle, new Color(1f, 1f, 1f, 0.85f), "Inner");
            UIKit.Stretch(inner.rectTransform, 6f);
            cardPortrait = new GameObject("Portrait", typeof(RectTransform)).AddComponent<RawImage>();
            cardPortrait.transform.SetParent(cardRing.transform, false);
            UIKit.Stretch(cardPortrait.rectTransform, -6f);
            cardPortrait.raycastTarget = false;

            cardName = L(card, "", 30, UIKit.Ink, new Vector2(0, 1), new Vector2(158, -12), new Vector2(420, 36), TextAnchor.MiddleLeft);
            cardInfo = L(card, "", 19, UIKit.Wood, new Vector2(0, 1), new Vector2(158, -48), new Vector2(460, 26), TextAnchor.MiddleLeft);
            cardCoins = L(card, "", 19, UIKit.Wood, new Vector2(0, 1), new Vector2(158, -74), new Vector2(460, 26), TextAnchor.MiddleLeft);
            for (int i = 0; i < Progression.FeedsPerLevel; i++)
            {
                var pip = UIKit.Img(card, UIKit.Rounded, UIKit.Grey, "Pip");
                UIKit.Place(pip.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(158 + i * 40, -106), new Vector2(34, 16));
                cardPips.Add(pip);
            }
            cardFeedHint = L(card, "", 16, UIKit.Wood, new Vector2(0, 1), new Vector2(158 + Progression.FeedsPerLevel * 40 + 6, -100), new Vector2(240, 26), TextAnchor.MiddleLeft);

            var row = UIKit.Rect("Buttons", card);
            row.anchorMin = new Vector2(0, 0);
            row.anchorMax = new Vector2(1, 0);
            row.pivot = new Vector2(0, 0);
            row.offsetMin = new Vector2(150, 10);
            row.offsetMax = new Vector2(-12, 66);
            var h = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.spacing = 8;
            h.childControlWidth = h.childControlHeight = true;
            h.childForceExpandWidth = h.childForceExpandHeight = true;

            feedButton = UIKit.Button(row, "Feed", UIKit.Green, () => { if (cardCreature != null) gm.Feed(cardCreature); }, 20, UIKit.Food);
            feedButton.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1.7f;
            muteButton = UIKit.Button(row, "Mute", UIKit.Blue, () => { if (cardCreature != null) gm.ToggleMute(cardCreature); }, 19);
            soloButton = UIKit.Button(row, "Solo", UIKit.Purple, () => { if (cardCreature != null) gm.ToggleSolo(cardCreature); }, 19);
            moveButton = UIKit.Button(row, "Move", UIKit.Orange, () => { if (cardCreature != null) gm.BeginMoving(cardCreature); }, 19);
            sellButton = UIKit.Button(row, "Sell", UIKit.Red, () => { if (cardCreature != null) ConfirmSell(cardCreature); }, 19);
            foreach (var b in new[] { muteButton, soloButton, moveButton, sellButton }) b.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;

            decorCard = UIKit.Panel(hudLayer).rectTransform;
            decorCard.name = "Decoration Card";
            decorName = L(decorCard, "", 28, UIKit.Ink, new Vector2(0, 1), new Vector2(20, -12), new Vector2(300, 36), TextAnchor.MiddleLeft);
            decorInfo = L(decorCard, "", 18, UIKit.Wood, new Vector2(0, 1), new Vector2(20, -48), new Vector2(300, 26), TextAnchor.MiddleLeft);
            B(decorCard, "Move", UIKit.Orange, () => { if (gm.SelectedDecor != null) gm.BeginMovingDecor(gm.SelectedDecor); },
                new Vector2(1, 0.5f), new Vector2(-150, 0), new Vector2(110, 62), 20);
            decorSellButton = B(decorCard, "Sell", UIKit.Red, () => { if (gm.SelectedDecor != null) gm.SellDecoration(gm.SelectedDecor); },
                new Vector2(1, 0.5f), new Vector2(-16, 0), new Vector2(120, 62), 20, UIKit.Coin);
        }

        void OnSelectionChanged()
        {
            cardCreature = gm.Selected;
            card.gameObject.SetActive(cardCreature != null);
            if (cardCreature != null)
            {
                cardPortrait.texture = Portraits.Get(cardCreature.Species, cardCreature.Magic);
                cardRing.color = MythicaDB.MagicColor(cardCreature.Magic);
                StartCoroutine(Tween.PopIn(card, 0.2f));
            }
            decorCard.gameObject.SetActive(gm.SelectedDecor != null);
            if (gm.SelectedDecor != null)
            {
                decorName.text = gm.SelectedDecor.Def.Name;
                decorInfo.text = "Decoration";
                UIKit.SetButtonLabel(decorSellButton, "+" + gm.DecorSellPrice(gm.SelectedDecor));
                StartCoroutine(Tween.PopIn(decorCard, 0.2f));
            }
        }

        void RefreshCard()
        {
            var c = cardCreature;
            if (c == null) return;
            long now = SaveSystem.Now;
            cardName.text = GameManager.Name(c);
            cardInfo.text = "Level " + c.Level + "  ·  " + c.Magic + " magic  ·  sings " + PartName(c.Species.Part);
            cardCoins.text = Mathf.RoundToInt(c.CoinsPerMinute) + " coins/min  ·  " + UIKit.Num(c.StoredCoins(now)) + " / " + UIKit.Num(c.CoinCap) + " stored";
            bool maxed = !gm.CanFeed(c);
            for (int i = 0; i < cardPips.Count; i++)
                cardPips[i].color = maxed || i < c.Data.feeds ? UIKit.Green : new Color(0.8f, 0.76f, 0.68f);
            cardFeedHint.text = maxed ? "Fully grown!" : c.Level < Progression.BreedMinLevel
                ? "Feed to grow. Breeds at level " + Progression.BreedMinLevel : "Feed to grow and earn more";
            UIKit.SetButtonLabel(feedButton, maxed ? "Max" : gm.FeedCost(c).ToString());
            UIKit.SetInteractable(feedButton, !maxed && gm.Save.food >= gm.FeedCost(c));
            UIKit.SetButtonLabel(muteButton, c.Data.muted ? "Unmute" : "Mute");
            UIKit.SetButtonLabel(soloButton, gm.Soloist == c ? "All" : "Solo");
        }

        static string PartName(Part p)
        {
            switch (p)
            {
                case Part.Kick: return "the kick drum";
                case Part.Snare: return "the snare";
                case Part.Hat: return "the hi-hats";
                case Part.Tom: return "the toms";
                case Part.Click: return "the clicks";
                case Part.Bass: case Part.SubBass: case Part.WalkBass: return "the bass";
                case Part.Riff: return "a riff";
                case Part.Pad: case Part.LowPad: return "the chords";
                case Part.Arp: case Part.HighArp: return "an arpeggio";
                case Part.Harp: return "the harp";
                case Part.Stabs: case Part.Skank: return "the off-beats";
                case Part.Bells: return "the bells";
                case Part.Counter: return "a countermelody";
                default: return "the melody";
            }
        }

        void ConfirmSell(Creature c)
        {
            OpenModal("Sell creature?", UIKit.Red, new Vector2(560, 360), content =>
            {
                L(content, "Return " + GameManager.Name(c) + " to the wild?\nYou'll get " + gm.SellPrice(c) + " coins.", 24, UIKit.Ink,
                    new Vector2(0.5f, 1), new Vector2(0, -10), new Vector2(480, 110));
                B(content, "Keep", UIKit.Green, CloseModal, new Vector2(0.5f, 0), new Vector2(-110, 10), new Vector2(190, 70), 24);
                B(content, "Sell", UIKit.Red, () => { CloseModal(); gm.Sell(c); }, new Vector2(0.5f, 0), new Vector2(110, 10), new Vector2(190, 70), 24, UIKit.Coin);
            });
        }

        // ------------------------------------------------------------ placement banner

        void BuildBanner()
        {
            banner = UIKit.Panel(hudLayer, new Color(1f, 0.93f, 0.6f)).rectTransform;
            banner.name = "Banner";
            bannerText = L(banner, "", 24, UIKit.Ink, new Vector2(0, 0.5f), new Vector2(22, 0), new Vector2(420, 70), TextAnchor.MiddleLeft);
            B(banner, "Cancel", UIKit.Red, () => gm.CancelMode(), new Vector2(1, 0.5f), new Vector2(-14, 0), new Vector2(140, 62), 22);
            banner.gameObject.SetActive(false);
        }

        // ------------------------------------------------------------ world bubbles and building tags

        void BuildStructureTags()
        {
            AddTag(StructureKind.Grove, "Breeding Grove", UIKit.Heart, OpenBreeding);
            AddTag(StructureKind.Nursery, "Nursery", UIKit.Egg, OpenNursery);
            AddTag(StructureKind.Bakery, "Bakery", UIKit.Food, () =>
            {
                if (gm.BakeReady) gm.CollectBake();
                else OpenBakery();
            });
        }

        void AddTag(StructureKind kind, string title, Sprite icon, Action onClick)
        {
            var b = UIKit.Button(worldLayer, "", new Color(0.32f, 0.2f, 0.1f, 0.92f), onClick);
            var rt = (RectTransform)b.transform;
            UIKit.Place(rt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 1), Vector2.zero, new Vector2(156, 56));
            var ic = UIKit.Img(rt.Find("Face"), icon, Color.white, "Icon");
            UIKit.Place(ic.rectTransform, new Vector2(0, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(4, 0), new Vector2(48, 48));
            var t = L(rt.Find("Face"), title, 16, Color.white, new Vector2(0, 1), new Vector2(32, -3), new Vector2(122, 22), TextAnchor.MiddleLeft, true);
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            var status = L(rt.Find("Face"), "", 14, new Color(1f, 0.93f, 0.6f), new Vector2(0, 0), new Vector2(32, 3), new Vector2(122, 22), TextAnchor.MiddleLeft, true);
            tags[kind] = new StructureTag { Rect = rt, Status = status, Icon = ic };
        }

        Structure StructureFor(StructureKind k)
        {
            return k == StructureKind.Grove ? gm.Island.Grove : k == StructureKind.Nursery ? gm.Island.Nursery : gm.Island.Bakery;
        }

        bool ToLocal(Vector3 world, RectTransform layer, out Vector2 local)
        {
            var sp = gm.Rig.Cam.WorldToScreenPoint(world);
            local = Vector2.zero;
            if (sp.z < 0) return false;
            return RectTransformUtility.ScreenPointToLocalPointInRectangle(layer, sp, null, out local);
        }

        void UpdateWorldUI()
        {
            long now = SaveSystem.Now;
            bool show = gm.Started && gm.CurrentMode == GameManager.Mode.Normal;
            float bob = Mathf.Sin(Time.unscaledTime * 3f) * 4f;

            foreach (var pair in tags)
            {
                var tag = pair.Value;
                var s = StructureFor(pair.Key);
                Vector2 p = Vector2.zero;
                bool on = show && ToLocal(s.transform.position + new Vector3(0, 0, -1.6f), worldLayer, out p);
                tag.Rect.gameObject.SetActive(on);
                if (!on) continue;
                tag.Rect.anchoredPosition = p;
                bool alert;
                tag.Status.text = StructureStatus(pair.Key, now, out alert);
                tag.Icon.rectTransform.anchoredPosition = new Vector2(4, alert ? bob : 0);
            }

            int used = 0;
            if (show)
            {
                foreach (var c in gm.Creatures)
                {
                    int stored = c.StoredCoins(now);
                    if (stored < Mathf.Max(3f, c.CoinsPerMinute * 2f)) continue;
                    Vector2 p;
                    if (!ToLocal(c.transform.position + Vector3.up * (c.Height * c.transform.localScale.y + 0.5f), worldLayer, out p)) continue;
                    if (used >= bubbles.Count) bubbles.Add(MakeBubble());
                    var b = bubbles[used++];
                    b.Target = c;
                    b.gameObject.SetActive(true);
                    ((RectTransform)b.transform).anchoredPosition = p + new Vector2(0, bob);
                }
            }
            for (int i = used; i < bubbles.Count; i++) bubbles[i].gameObject.SetActive(false);
        }

        string StructureStatus(StructureKind k, long now, out bool alert)
        {
            alert = false;
            switch (k)
            {
                case StructureKind.Grove:
                    if (!gm.Save.breeding.active) return "Tap to breed";
                    if (now >= gm.Save.breeding.doneAt) { alert = true; return "Nursery full!"; }
                    return "Breeding " + UIKit.Time(gm.Save.breeding.doneAt - now);
                case StructureKind.Nursery:
                    foreach (var e in gm.Save.eggs) if (now >= e.hatchAt) { alert = true; return "Egg ready!"; }
                    if (gm.Save.eggs.Count > 0)
                    {
                        long next = long.MaxValue;
                        foreach (var e in gm.Save.eggs) next = Math.Min(next, e.hatchAt);
                        return "Hatching " + UIKit.Time(next - now);
                    }
                    return gm.Save.eggs.Count + " / " + gm.NestCapacity + " eggs";
                default:
                    if (gm.BakeReady) { alert = true; return "Tap to collect!"; }
                    if (gm.Save.bake.active) return "Baking " + UIKit.Time(gm.Save.bake.doneAt - now);
                    return "Tap to bake food";
            }
        }

        Bubble MakeBubble()
        {
            var bg = UIKit.Img(worldLayer, UIKit.Circle, Color.white, "Coin Bubble");
            var rt = bg.rectTransform;
            UIKit.Place(rt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0), Vector2.zero, new Vector2(62, 62));
            var sh = bg.gameObject.AddComponent<Shadow>();
            sh.effectColor = new Color(0, 0, 0, 0.3f);
            sh.effectDistance = new Vector2(0, -4);
            var ic = UIKit.Img(rt, UIKit.Coin, Color.white, "Coin");
            UIKit.Stretch(ic.rectTransform, 8f);
            ic.raycastTarget = false;
            var bubble = bg.gameObject.AddComponent<Bubble>();
            var button = bg.gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.onClick.AddListener(() =>
            {
                if (bubble.Target == null) return;
                bubble.Target.Hop();
                if (gm.Collect(bubble.Target) > 0) gm.SaveNow();
            });
            return bubble;
        }

        void OnStructureTapped(StructureKind k)
        {
            if (k == StructureKind.Grove) OpenBreeding();
            else if (k == StructureKind.Nursery) OpenNursery();
            else OpenBakery();
        }

        // ------------------------------------------------------------ per frame

        void Update()
        {
            if (CanvasSize != lastSize || (Screen.height > Screen.width) != portrait)
            {
                lastSize = CanvasSize;
                Relayout();
            }

            var save = gm.Save;
            shownCoins = Approach(shownCoins, save.coins);
            shownFood = Approach(shownFood, save.food);
            shownCrystals = Approach(shownCrystals, save.crystals);
            coinText.text = UIKit.Num((long)Math.Round(shownCoins));
            foodText.text = UIKit.Num((long)Math.Round(shownFood));
            crystalText.text = UIKit.Num((long)Math.Round(shownCrystals));
            levelText.text = save.level.ToString();
            bool maxLevel = save.level >= Progression.MaxPlayerLevel;
            float xpT = maxLevel ? 1f : save.xp / (float)gm.XpToNext;
            xpFill.anchorMax = new Vector2(Mathf.Lerp(0.2f, 1f, Mathf.Clamp01(xpT)), 1f);
            xpText.text = maxLevel ? "Max level" : save.xp + " / " + gm.XpToNext + " XP";

            bool placing = gm.CurrentMode != GameManager.Mode.Normal;
            banner.gameObject.SetActive(placing && gm.Started);
            if (placing) bannerText.text = gm.ModeLabel + "\n<size=18>Tap a glowing spot on the island</size>";
            if (placing && card.gameObject.activeSelf) card.gameObject.SetActive(false);
            if (cardCreature == null && card.gameObject.activeSelf) card.gameObject.SetActive(false);
            RefreshCard();

            UpdateWorldUI();
            foreach (var r in refreshers) r();

            if (pendingLevelUp > 0 && modal == null && !placing)
            {
                int lvl = pendingLevelUp;
                pendingLevelUp = 0;
                ShowLevelUp(lvl);
            }

            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (modal != null) CloseModal();
                else if (placing) gm.CancelMode();
                else gm.ClearSelection();
            }
        }

        static double Approach(double shown, long real)
        {
            double diff = real - shown;
            if (Math.Abs(diff) < 1) return real;
            return shown + diff * Mathf.Clamp01(Time.unscaledDeltaTime * 6f) + Math.Sign(diff) * 0.5;
        }

        void OnStateChanged()
        {
            if (modal != null && modalBuilder != null) RebuildModal();
        }

        // ------------------------------------------------------------ gains, toasts, level ups

        void OnGained(Vector3 world, Currency what, int amount)
        {
            if (what == Currency.Xp)
            {
                FloatText("+" + amount + " XP", starIcon, new Color(0.7f, 1f, 0.5f));
                return;
            }
            RectTransform target = what == Currency.Coins ? coinIcon : what == Currency.Food ? foodIcon : crystalIcon;
            Sprite icon = what == Currency.Coins ? UIKit.Coin : what == Currency.Food ? UIKit.Food : UIKit.Crystal;
            Vector2 from;
            if (world == Vector3.zero || !ToLocal(world, fxLayer, out from)) from = Vector2.zero;
            Vector2 to;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(fxLayer, RectTransformUtility.WorldToScreenPoint(null, target.position), null, out to);
            int count = Mathf.Clamp(amount / 8 + 1, 1, 7);
            for (int i = 0; i < count; i++) StartCoroutine(Fly(icon, from, to, i * 0.06f, target, i == count - 1));
            FloatTextAt("+" + UIKit.Num(amount), from + new Vector2(0, 30), Color.white);
        }

        IEnumerator Fly(Sprite icon, Vector2 from, Vector2 to, float delay, RectTransform target, bool last)
        {
            var img = UIKit.Img(fxLayer, icon, Color.white, "Fly");
            img.raycastTarget = false;
            var rt = img.rectTransform;
            UIKit.Place(rt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), from, new Vector2(52, 52));
            Vector2 scatter = from + UnityEngine.Random.insideUnitCircle * 50f + new Vector2(0, 40);
            for (float t = 0; t < delay + 0.18f; t += Time.unscaledDeltaTime)
            {
                rt.anchoredPosition = Vector2.Lerp(from, scatter, Mathf.Clamp01((t - delay) / 0.18f));
                yield return null;
            }
            const float dur = 0.55f;
            for (float t = 0; t < dur; t += Time.unscaledDeltaTime)
            {
                float k = t / dur;
                float e = k * k;
                rt.anchoredPosition = Vector2.Lerp(scatter, to, e) + new Vector2(0, Mathf.Sin(k * Mathf.PI) * 60f);
                rt.localScale = Vector3.one * Mathf.Lerp(1f, 0.7f, k);
                yield return null;
            }
            Destroy(img.gameObject);
            if (last) StartCoroutine(Tween.Punch(target, 0.3f, 0.25f));
        }

        void FloatText(string text, RectTransform near, Color color)
        {
            Vector2 p;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(fxLayer, RectTransformUtility.WorldToScreenPoint(null, near.position), null, out p);
            FloatTextAt(text, p + new Vector2(70, -40), color);
        }

        void FloatTextAt(string text, Vector2 p, Color color)
        {
            var t = UIKit.Label(fxLayer, text, 26, color, TextAnchor.MiddleCenter, true);
            UIKit.Place(t.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), p, new Vector2(240, 40));
            StartCoroutine(Rise(t));
        }

        IEnumerator Rise(Text t)
        {
            var rt = t.rectTransform;
            Vector2 start = rt.anchoredPosition;
            for (float e = 0; e < 1f; e += Time.unscaledDeltaTime)
            {
                rt.anchoredPosition = start + new Vector2(0, e * 50f);
                var c = t.color;
                c.a = 1f - Mathf.Clamp01((e - 0.6f) / 0.4f);
                t.color = c;
                yield return null;
            }
            Destroy(t.gameObject);
        }

        void ShowToast(string text)
        {
            var pill = UIKit.Img(toastLayer, UIKit.Rounded, new Color(0.12f, 0.07f, 0.04f, 0.88f), "Toast");
            pill.raycastTarget = false;
            var rt = pill.rectTransform;
            float y = portrait ? -176 : -96;
            // Push older toasts down.
            foreach (Transform other in toastLayer)
                if (other != rt) ((RectTransform)other).anchoredPosition += new Vector2(0, -58);
            UIKit.Place(rt, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, y), new Vector2(Mathf.Min(680f, CanvasSize.x - 40f), 50));
            var t = UIKit.Label(rt, text, 21, Color.white);
            UIKit.Stretch(t.rectTransform, 8f);
            StartCoroutine(Tween.PopIn(rt, 0.2f));
            StartCoroutine(FadeToast(pill, t));
        }

        IEnumerator FadeToast(Image pill, Text t)
        {
            for (float e = 0; e < 3.4f; e += Time.unscaledDeltaTime)
            {
                if (pill == null) yield break;
                float a = 1f - Mathf.Clamp01((e - 2.9f) / 0.5f);
                pill.color = new Color(0.12f, 0.07f, 0.04f, 0.88f * a);
                t.color = new Color(1, 1, 1, a);
                yield return null;
            }
            if (pill != null) Destroy(pill.gameObject);
        }

        void OnLevelUp(int level)
        {
            if (modal == null && gm.CurrentMode == GameManager.Mode.Normal) ShowLevelUp(level);
            else pendingLevelUp = Mathf.Max(pendingLevelUp, level);
        }

        void ShowLevelUp(int level)
        {
            var unlocks = new List<string>();
            foreach (var s in MythicaDB.All)
                if (MythicaDB.TierUnlockLevel[s.Tier - 1] == level) unlocks.Add(s.Name);
            foreach (var d in MythicaDB.Decorations)
                if (d.Level == level) unlocks.Add(d.Name);
            if (level == Progression.UpgradeLevelNeeded(gm.Save.nurseryLevel)) unlocks.Add("Nursery upgrade");
            if (level == Progression.UpgradeLevelNeeded(gm.Save.groveLevel)) unlocks.Add("Grove upgrade");

            OpenModal("Level Up!", UIKit.Gold, new Vector2(560, 470), content =>
            {
                var star = Icon(content, UIKit.Star, new Vector2(0.5f, 1), new Vector2(0, 0), 130);
                var n = UIKit.Label(star.transform, level.ToString(), 46, Color.white, TextAnchor.MiddleCenter, true);
                UIKit.Stretch(n.rectTransform);
                StartCoroutine(Tween.Punch(star.transform, 0.3f, 0.5f));
                L(content, "You reached level " + level + "!", 26, UIKit.Ink, new Vector2(0.5f, 1), new Vector2(0, -136), new Vector2(480, 34));
                L(content, "+" + GameManager.LevelCrystals(level) + " crystal" + (GameManager.LevelCrystals(level) > 1 ? "s" : "") + "    +" + GameManager.LevelCoins(level) + " coins",
                    22, UIKit.Wood, new Vector2(0.5f, 1), new Vector2(0, -172), new Vector2(480, 30));
                L(content, unlocks.Count > 0 ? "New in the Market: " + string.Join(", ", unlocks.ToArray()) : "Keep feeding and breeding to unlock more.",
                    19, UIKit.Wood, new Vector2(0.5f, 1), new Vector2(0, -206), new Vector2(480, 60));
                B(content, "Great!", UIKit.Green, CloseModal, new Vector2(0.5f, 0), new Vector2(0, 6), new Vector2(220, 72), 26);
            });
        }

        // ------------------------------------------------------------ modal plumbing

        void OpenModal(string title, Color color, Vector2 maxSize, Action<RectTransform> builder)
        {
            CloseModal();
            gm.ClearSelection();
            modalScroll = 1f;
            var layer = UIKit.Stretch(UIKit.Rect("Modal", modalLayer));
            var dim = layer.gameObject.AddComponent<Image>();
            dim.color = new Color(0.05f, 0.08f, 0.15f, 0.55f);
            var dimButton = layer.gameObject.AddComponent<Button>();
            dimButton.transition = Selectable.Transition.None;
            dimButton.onClick.AddListener(CloseModal);

            var panel = UIKit.Panel(layer).rectTransform;
            var size = new Vector2(Mathf.Min(maxSize.x, CanvasSize.x - 24f), Mathf.Min(maxSize.y, CanvasSize.y - 60f));
            UIKit.Place(panel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -10), size);
            panel.gameObject.AddComponent<Button>().transition = Selectable.Transition.None;   // swallow taps on the panel

            var ribbon = UIKit.Img(panel, UIKit.Rounded, color, "Title");
            UIKit.Place(ribbon.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 0.5f), new Vector2(0, 0), new Vector2(Mathf.Min(420f, size.x - 120f), 64));
            var ribbonShadow = ribbon.gameObject.AddComponent<Shadow>();
            ribbonShadow.effectColor = new Color(0, 0, 0, 0.35f);
            ribbonShadow.effectDistance = new Vector2(0, -5);
            var tt = UIKit.Label(ribbon.transform, title, 32, Color.white, TextAnchor.MiddleCenter, true);
            UIKit.Stretch(tt.rectTransform);

            var close = UIKit.Button(panel, "", UIKit.Red, CloseModal, 24, null, UIKit.Circle);
            UIKit.Place((RectTransform)close.transform, new Vector2(1, 1), new Vector2(0.5f, 0.5f), new Vector2(-14, -14), new Vector2(64, 64));
            var x = UIKit.Label(close.transform.Find("Face"), "X", 30, Color.white, TextAnchor.MiddleCenter, true);
            UIKit.Stretch(x.rectTransform);

            modal = layer;
            modalContent = UIKit.Rect("Content", panel);
            UIKit.Stretch(modalContent, 22f);
            modalContent.offsetMax = new Vector2(-22, -44);
            modalBuilder = builder;
            builder(modalContent);
            StartCoroutine(RestoreScroll());
            StartCoroutine(Tween.PopIn(panel, 0.25f));
        }

        void RebuildModal()
        {
            var scroll = modalContent.GetComponentInChildren<ScrollRect>();
            if (scroll != null) modalScroll = scroll.verticalNormalizedPosition;
            refreshers.Clear();
            foreach (Transform child in modalContent) Destroy(child.gameObject);
            modalBuilder(modalContent);
            StartCoroutine(RestoreScroll());
        }

        IEnumerator RestoreScroll()
        {
            yield return null;
            if (modalContent == null) yield break;
            var scroll = modalContent.GetComponentInChildren<ScrollRect>();
            if (scroll == null) yield break;
            var height = scroll.content.GetComponent<GridHeight>();
            if (height != null) height.Apply();
            scroll.StopMovement();
            if (modalScroll >= 0.999f) scroll.content.anchoredPosition = Vector2.zero;
            else scroll.verticalNormalizedPosition = modalScroll;
        }

        void CloseModal()
        {
            if (modal != null) Destroy(modal.gameObject);
            modal = null;
            modalContent = null;
            modalBuilder = null;
            refreshers.Clear();
        }

        // A vertical scrolling grid filling the given area.
        RectTransform ScrollGrid(RectTransform parent, float top, Vector2 cell)
        {
            var view = UIKit.Rect("Scroll", parent);
            UIKit.Stretch(view);
            view.offsetMax = new Vector2(0, -top);
            var bg = view.gameObject.AddComponent<Image>();
            bg.color = new Color(0, 0, 0, 0.06f);
            bg.sprite = UIKit.Rounded;
            bg.type = Image.Type.Sliced;
            view.gameObject.AddComponent<RectMask2D>();
            var content = UIKit.Rect("Grid", view);
            content.anchorMin = new Vector2(0, 1);
            content.anchorMax = new Vector2(1, 1);
            content.pivot = new Vector2(0.5f, 1);
            content.offsetMin = content.offsetMax = Vector2.zero;
            var grid = content.gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = cell;
            grid.spacing = new Vector2(12, 12);
            grid.padding = new RectOffset(12, 12, 12, 12);
            grid.childAlignment = TextAnchor.UpperCenter;
            // Size the grid ourselves: a ContentSizeFitter inside a ScrollRect left menus opening half scrolled.
            content.gameObject.AddComponent<GridHeight>().View = view;
            var scroll = view.gameObject.AddComponent<ScrollRect>();
            scroll.content = content;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 30f;
            scroll.viewport = view;
            return content;
        }

        RectTransform Cell(RectTransform grid, Color fill)
        {
            return UIKit.Panel(grid, fill).rectTransform;
        }

        // Small layout helpers: anchor doubles as pivot, so (0,1) means "from the top left".
        Text L(Transform parent, string text, int size, Color color, Vector2 anchor, Vector2 pos, Vector2 box,
            TextAnchor align = TextAnchor.MiddleCenter, bool outline = false)
        {
            var t = UIKit.Label(parent, text, size, color, align, outline);
            UIKit.Place(t.rectTransform, anchor, anchor, pos, box);
            return t;
        }

        Button B(Transform parent, string label, Color color, Action onClick, Vector2 anchor, Vector2 pos, Vector2 size, int font = 22, Sprite icon = null)
        {
            var b = UIKit.Button(parent, label, color, onClick, font, icon);
            UIKit.Place((RectTransform)b.transform, anchor, anchor, pos, size);
            return b;
        }

        Image Icon(Transform parent, Sprite s, Vector2 anchor, Vector2 pos, float size)
        {
            var img = UIKit.Img(parent, s, Color.white, "Icon");
            img.raycastTarget = false;
            UIKit.Place(img.rectTransform, anchor, anchor, pos, new Vector2(size, size));
            return img;
        }

        RawImage Portrait(Transform parent, Texture tex, Vector2 anchor, Vector2 pos, float size, Color? tint = null)
        {
            var img = new GameObject("Portrait", typeof(RectTransform)).AddComponent<RawImage>();
            img.transform.SetParent(parent, false);
            img.texture = tex;
            img.color = tint ?? Color.white;
            img.raycastTarget = false;
            UIKit.Place(img.rectTransform, anchor, anchor, pos, new Vector2(size, size));
            return img;
        }

        RectTransform Bar(Transform parent, Vector2 anchor, Vector2 pos, Vector2 size, Color color)
        {
            var bg = UIKit.Img(parent, UIKit.Rounded, new Color(0.3f, 0.2f, 0.12f, 0.35f), "Bar");
            bg.raycastTarget = false;
            UIKit.Place(bg.rectTransform, anchor, anchor, pos, size);
            var fill = UIKit.Img(bg.transform, UIKit.Rounded, color, "Fill");
            fill.raycastTarget = false;
            var rt = fill.rectTransform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = new Vector2(0.2f, 1f);
            rt.offsetMin = new Vector2(3, 3);
            rt.offsetMax = new Vector2(-3, -3);
            return rt;
        }

        static void SetBar(RectTransform fill, float t)
        {
            fill.anchorMax = new Vector2(Mathf.Lerp(0.12f, 1f, Mathf.Clamp01(t)), 1f);
        }

        // ------------------------------------------------------------ Market

        void OpenMarket(int tab)
        {
            marketTab = tab;
            OpenModal("Market", UIKit.Green, new Vector2(1060, 660), BuildMarket);
        }

        void BuildMarket(RectTransform content)
        {
            string[] names = { "Creatures", "Decorations", "Buildings" };
            float tabW = Mathf.Min(220f, (content.rect.width > 0 ? content.rect.width : 600f) / 3f - 10f);
            for (int i = 0; i < names.Length; i++)
            {
                int tab = i;
                B(content, names[i], i == marketTab ? UIKit.Orange : UIKit.Grey, () => { marketTab = tab; modalScroll = 1f; RebuildModal(); },
                    new Vector2(0.5f, 1), new Vector2((i - 1) * (tabW + 10), -6), new Vector2(tabW, 56), 21);
            }
            if (marketTab == 0) BuildCreatureShop(content);
            else if (marketTab == 1) BuildDecorShop(content);
            else BuildBuildingShop(content);
        }

        void BuildCreatureShop(RectTransform content)
        {
            var grid = ScrollGrid(content, 74, new Vector2(190, 262));
            foreach (var s in MythicaDB.All)
            {
                var species = s;
                bool unlocked = gm.TierUnlocked(s.Tier);
                var cell = Cell(grid, Color.Lerp(UIKit.Cream, MythicaDB.MagicColor(s.Native), 0.25f));
                var ring = UIKit.Img(cell, UIKit.Circle, new Color(1, 1, 1, 0.65f), "Ring");
                ring.raycastTarget = false;
                UIKit.Place(ring.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -8), new Vector2(132, 132));
                Portrait(cell, Portraits.Get(s, s.Native), new Vector2(0.5f, 1), new Vector2(0, -4), 140, unlocked ? (Color?)null : new Color(0.1f, 0.1f, 0.15f, 0.8f));
                L(cell, s.Name, 21, UIKit.Ink, new Vector2(0.5f, 1), new Vector2(0, -144), new Vector2(180, 28));
                L(cell, "Tier " + s.Tier + "  ·  " + s.Native + "  ·  " + Mathf.RoundToInt(MythicaDB.TierCoinsPerMinute[s.Tier - 1]) + "/min",
                    15, UIKit.Wood, new Vector2(0.5f, 1), new Vector2(0, -170), new Vector2(180, 22));
                if (unlocked)
                {
                    var buy = B(cell, UIKit.Num(s.Cost), UIKit.Green, () =>
                    {
                        gm.Buy(species);
                        if (gm.CurrentMode != GameManager.Mode.Normal) CloseModal();
                    }, new Vector2(0.5f, 0), new Vector2(0, 10), new Vector2(160, 56), 22, UIKit.Coin);
                    UIKit.SetInteractable(buy, gm.Save.coins >= s.Cost);
                    buy.interactable = true;   // still tappable, so the toast can explain
                }
                else
                {
                    Icon(cell, UIKit.Lock, new Vector2(0.5f, 1), new Vector2(0, -40), 60);
                    L(cell, "Level " + MythicaDB.TierUnlockLevel[s.Tier - 1], 22, UIKit.Wood, new Vector2(0.5f, 0), new Vector2(0, 22), new Vector2(170, 40));
                }
            }
        }

        void BuildDecorShop(RectTransform content)
        {
            var grid = ScrollGrid(content, 74, new Vector2(190, 250));
            foreach (var d in MythicaDB.Decorations)
            {
                var def = d;
                bool unlocked = gm.Save.level >= d.Level;
                var cell = Cell(grid, UIKit.Cream);
                Portrait(cell, Portraits.Get(d), new Vector2(0.5f, 1), new Vector2(0, -6), 140, unlocked ? (Color?)null : new Color(0.1f, 0.1f, 0.15f, 0.8f));
                L(cell, d.Name, 21, UIKit.Ink, new Vector2(0.5f, 1), new Vector2(0, -148), new Vector2(180, 28));
                if (unlocked)
                {
                    var buy = B(cell, UIKit.Num(d.Cost), UIKit.Green, () =>
                    {
                        gm.BuyDecoration(def);
                        if (gm.CurrentMode != GameManager.Mode.Normal) CloseModal();
                    }, new Vector2(0.5f, 0), new Vector2(0, 10), new Vector2(160, 56), 22, UIKit.Coin);
                    UIKit.SetInteractable(buy, gm.Save.coins >= d.Cost);
                    buy.interactable = true;
                }
                else
                {
                    Icon(cell, UIKit.Lock, new Vector2(0.5f, 1), new Vector2(0, -40), 60);
                    L(cell, "Level " + d.Level, 22, UIKit.Wood, new Vector2(0.5f, 0), new Vector2(0, 22), new Vector2(170, 40));
                }
            }
        }

        void BuildBuildingShop(RectTransform content)
        {
            var grid = ScrollGrid(content, 74, new Vector2(300, 300));
            int nl = gm.Save.nurseryLevel, gl = gm.Save.groveLevel;
            BuildingCell(grid, UIKit.Egg, "Nursery", "Level " + nl + "  ·  holds " + gm.NestCapacity + " eggs",
                nl >= Progression.MaxNurseryLevel ? null : "+1 egg slot", Progression.NurseryUpgradeCost(nl), Progression.UpgradeLevelNeeded(nl),
                gm.UpgradeNursery, OpenNursery);
            BuildingCell(grid, UIKit.Heart, "Breeding Grove", "Level " + gl + "  ·  " + Mathf.RoundToInt(100f / Progression.GroveSpeed(gl)) + "% speed",
                gl >= Progression.MaxGroveLevel ? null : "Breed 20% faster", Progression.GroveUpgradeCost(gl), Progression.UpgradeLevelNeeded(gl),
                gm.UpgradeGrove, OpenBreeding);
            BuildingCell(grid, UIKit.Food, "Bakery", "Turns coins into food", null, 0, 0, null, OpenBakery);
        }

        void BuildingCell(RectTransform grid, Sprite icon, string title, string info, string upgrade, int cost, int levelNeeded, Action onUpgrade, Action onVisit)
        {
            var cell = Cell(grid, UIKit.Cream);
            Icon(cell, icon, new Vector2(0.5f, 1), new Vector2(0, -10), 90);
            L(cell, title, 24, UIKit.Ink, new Vector2(0.5f, 1), new Vector2(0, -104), new Vector2(280, 30));
            L(cell, info, 17, UIKit.Wood, new Vector2(0.5f, 1), new Vector2(0, -134), new Vector2(280, 24));
            if (upgrade != null)
            {
                bool canLevel = gm.Save.level >= levelNeeded;
                L(cell, canLevel ? "Upgrade: " + upgrade : "Upgrade at level " + levelNeeded, 17, UIKit.Wood, new Vector2(0.5f, 1), new Vector2(0, -160), new Vector2(280, 24));
                var b = B(cell, UIKit.Num(cost), UIKit.Orange, onUpgrade, new Vector2(0, 0), new Vector2(14, 12), new Vector2(130, 56), 20, UIKit.Coin);
                UIKit.SetInteractable(b, canLevel && gm.Save.coins >= cost);
                b.interactable = true;
            }
            else if (title != "Bakery")
                L(cell, "Fully upgraded!", 18, UIKit.Green, new Vector2(0.5f, 1), new Vector2(0, -160), new Vector2(280, 24));
            B(cell, "Visit", UIKit.Blue, () => onVisit(), new Vector2(1, 0), new Vector2(-14, 12), new Vector2(130, 56), 20);
        }

        // ------------------------------------------------------------ Breeding Grove

        void OpenBreeding()
        {
            if (breedA == null || !gm.Creatures.Contains(breedA)) breedA = null;
            if (breedB == null || !gm.Creatures.Contains(breedB)) breedB = null;
            OpenModal("Breeding Grove", new Color(0.95f, 0.45f, 0.65f), new Vector2(1000, 660), BuildBreeding);
        }

        void BuildBreeding(RectTransform content)
        {
            var job = gm.Save.breeding;
            if (job.active)
            {
                var s = MythicaDB.Get(job.species);
                Icon(content, UIKit.Heart, new Vector2(0.5f, 1), new Vector2(0, -10), 120);
                L(content, job.parents, 24, UIKit.Ink, new Vector2(0.5f, 1), new Vector2(0, -140), new Vector2(700, 34));
                L(content, "are breeding. The egg's a surprise until it arrives in the Nursery!", 19, UIKit.Wood,
                    new Vector2(0.5f, 1), new Vector2(0, -176), new Vector2(700, 30));
                var fill = Bar(content, new Vector2(0.5f, 1), new Vector2(0, -226), new Vector2(460, 40), new Color(0.95f, 0.45f, 0.65f));
                var timer = L(content, "", 22, Color.white, new Vector2(0.5f, 1), new Vector2(0, -226), new Vector2(460, 40), TextAnchor.MiddleCenter, true);
                var speed = B(content, "", UIKit.Purple, gm.SpeedUpBreeding, new Vector2(0.5f, 1), new Vector2(0, -290), new Vector2(240, 64), 22, UIKit.Crystal);
                int total = Mathf.Max(1, Progression.BreedSecondsPerTier * s.Tier);
                refreshers.Add(() =>
                {
                    long left = job.doneAt - SaveSystem.Now;
                    SetBar(fill, 1f - left / (float)total);
                    timer.text = left > 0 ? UIKit.Time(left) : gm.NestFull ? "Waiting for room in the Nursery" : "Done!";
                    speed.gameObject.SetActive(left > 0);
                    UIKit.SetButtonLabel(speed, "Finish  " + Progression.SpeedUpCost(left));
                });
                return;
            }

            // Two parent slots and what they might make.
            ParentSlot(content, breedA, new Vector2(-170, -6), () => { breedA = null; RebuildModal(); });
            L(content, "+", 50, UIKit.Wood, new Vector2(0.5f, 1), new Vector2(0, -40), new Vector2(60, 60));
            ParentSlot(content, breedB, new Vector2(170, -6), () => { breedB = null; RebuildModal(); });

            string hint;
            bool ready = breedA != null && breedB != null;
            if (ready)
            {
                var outcomes = MythicaDB.BreedOutcomes(breedA.Species, breedA.Magic, breedB.Species, breedB.Magic);
                var parts = new List<string>();
                foreach (var o in outcomes)
                {
                    bool known = gm.Save.discovered.Contains(MythicaDB.VariantKey(o.Species.Id, o.Magic));
                    parts.Add((known ? MythicaDB.VariantName(o.Species, o.Magic) : "??? " + o.Species.Name + " (" + o.Magic + ")") + " " + Mathf.RoundToInt(o.Chance * 100) + "%");
                }
                hint = "Could hatch: " + string.Join("  or  ", parts.ToArray());
            }
            else hint = "Pick two creatures at level " + Progression.BreedMinLevel + " or higher. Different magic types make new variants!";
            L(content, hint, 18, UIKit.Wood, new Vector2(0.5f, 1), new Vector2(0, -132), new Vector2(Mathf.Max(400f, content.rect.width - 20f), 44));
            var go = B(content, ready ? "Breed  (" + UIKit.Time(gm.BreedSeconds(breedA, breedB)) + ")" : "Breed", new Color(0.95f, 0.45f, 0.65f), () =>
            {
                gm.StartBreeding(breedA, breedB);
                if (gm.Save.breeding.active) { breedA = breedB = null; }
            }, new Vector2(0.5f, 1), new Vector2(0, -178), new Vector2(260, 60), 22, UIKit.Heart);
            UIKit.SetInteractable(go, ready);

            var grid = ScrollGrid(content, 250, new Vector2(150, 190));
            foreach (var c in gm.Creatures)
            {
                var creature = c;
                bool chosen = c == breedA || c == breedB;
                bool able = gm.CanBreed(c);
                var cell = UIKit.Button(grid, "", chosen ? new Color(1f, 0.75f, 0.85f) : able ? UIKit.Cream : new Color(0.85f, 0.82f, 0.78f), () =>
                {
                    if (!gm.CanBreed(creature)) { gm.Toast(GameManager.Name(creature) + " needs to be level " + Progression.BreedMinLevel + ". Feed it!"); return; }
                    if (creature == breedA) breedA = null;
                    else if (creature == breedB) breedB = null;
                    else if (breedA == null) breedA = creature;
                    else breedB = creature;
                    RebuildModal();
                });
                var face = cell.transform.Find("Face");
                Portrait(face, Portraits.Get(c.Species, c.Magic), new Vector2(0.5f, 1), new Vector2(0, -2), 120, able ? (Color?)null : new Color(0.6f, 0.6f, 0.6f, 0.8f));
                L(face, GameManager.Name(c), 15, UIKit.Ink, new Vector2(0.5f, 0), new Vector2(0, 30), new Vector2(144, 36));
                L(face, able ? "Level " + c.Level : "Lv " + c.Level + " (needs " + Progression.BreedMinLevel + ")", 14, able ? UIKit.Wood : UIKit.Red,
                    new Vector2(0.5f, 0), new Vector2(0, 8), new Vector2(144, 22));
            }
        }

        void ParentSlot(RectTransform content, Creature c, Vector2 pos, Action clear)
        {
            var slot = UIKit.Img(content, UIKit.Circle, c != null ? MythicaDB.MagicColor(c.Magic) : new Color(0.3f, 0.2f, 0.1f, 0.18f), "Parent");
            UIKit.Place(slot.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), pos, new Vector2(116, 116));
            if (c == null)
            {
                var q = UIKit.Label(slot.transform, "?", 50, new Color(1, 1, 1, 0.8f), TextAnchor.MiddleCenter, true);
                UIKit.Stretch(q.rectTransform);
                return;
            }
            Portrait(slot.transform, Portraits.Get(c.Species, c.Magic), new Vector2(0.5f, 0.5f), Vector2.zero, 128);
            var b = slot.gameObject.AddComponent<Button>();
            b.transition = Selectable.Transition.None;
            b.onClick.AddListener(() => clear());
            L(content, GameManager.Name(c), 17, UIKit.Ink, new Vector2(0.5f, 1), pos + new Vector2(0, -114), new Vector2(220, 22));
        }

        // ------------------------------------------------------------ Nursery

        void OpenNursery() { OpenModal("Nursery", UIKit.Orange, new Vector2(900, 560), BuildNursery); }

        void BuildNursery(RectTransform content)
        {
            L(content, "Eggs from the Breeding Grove hatch here.  " + gm.Save.eggs.Count + " / " + gm.NestCapacity + " eggs", 19, UIKit.Wood,
                new Vector2(0.5f, 1), new Vector2(0, -8), new Vector2(760, 30));
            var grid = ScrollGrid(content, 50, new Vector2(200, 290));
            for (int i = 0; i < gm.NestCapacity; i++)
            {
                var cell = Cell(grid, UIKit.Cream);
                if (i >= gm.Save.eggs.Count)
                {
                    var ghost = Icon(cell, UIKit.Egg, new Vector2(0.5f, 1), new Vector2(0, -30), 110);
                    ghost.color = new Color(0, 0, 0, 0.15f);
                    L(cell, "Empty", 20, UIKit.Grey, new Vector2(0.5f, 1), new Vector2(0, -160), new Vector2(180, 30));
                    continue;
                }
                int index = i;
                var egg = gm.Save.eggs[i];
                var s = MythicaDB.Get(egg.species);
                var eggIcon = Icon(cell, UIKit.Egg, new Vector2(0.5f, 1), new Vector2(0, -16), 120);
                eggIcon.color = Color.Lerp(Color.white, MythicaDB.MagicColor(egg.magic), 0.45f);
                L(cell, MythicaDB.VariantName(s, egg.magic), 19, UIKit.Ink, new Vector2(0.5f, 1), new Vector2(0, -144), new Vector2(190, 50));
                var timer = L(cell, "", 18, UIKit.Wood, new Vector2(0.5f, 1), new Vector2(0, -192), new Vector2(190, 24));
                var hatch = B(cell, "Hatch!", UIKit.Green, () =>
                {
                    gm.PlaceEgg(index);
                    if (gm.CurrentMode != GameManager.Mode.Normal) CloseModal();
                }, new Vector2(0.5f, 0), new Vector2(0, 10), new Vector2(170, 56), 22);
                var speed = B(cell, "", UIKit.Purple, () => gm.SpeedUpEgg(index), new Vector2(0.5f, 0), new Vector2(0, 10), new Vector2(170, 56), 20, UIKit.Crystal);
                var eggT = eggIcon.rectTransform;
                refreshers.Add(() =>
                {
                    long left = egg.hatchAt - SaveSystem.Now;
                    bool ready = left <= 0;
                    timer.text = ready ? "Ready to hatch!" : "Hatches in " + UIKit.Time(left);
                    hatch.gameObject.SetActive(ready);
                    speed.gameObject.SetActive(!ready);
                    if (!ready) UIKit.SetButtonLabel(speed, "Hatch  " + Progression.SpeedUpCost(left));
                    eggT.localRotation = Quaternion.Euler(0, 0, ready ? Mathf.Sin(Time.unscaledTime * 10f) * 10f : 0f);
                });
            }
        }

        // ------------------------------------------------------------ Bakery

        void OpenBakery() { OpenModal("Bakery", new Color(0.85f, 0.45f, 0.3f), new Vector2(900, 560), BuildBakery); }

        void BuildBakery(RectTransform content)
        {
            var bake = gm.Save.bake;
            if (bake.active)
            {
                var r = MythicaDB.Recipes[bake.recipe];
                Icon(content, UIKit.Food, new Vector2(0.5f, 1), new Vector2(0, -10), 120);
                L(content, r.Name + "  (+" + r.Food + " food)", 26, UIKit.Ink, new Vector2(0.5f, 1), new Vector2(0, -140), new Vector2(600, 34));
                var fill = Bar(content, new Vector2(0.5f, 1), new Vector2(0, -196), new Vector2(460, 40), UIKit.Orange);
                var timer = L(content, "", 22, Color.white, new Vector2(0.5f, 1), new Vector2(0, -196), new Vector2(460, 40), TextAnchor.MiddleCenter, true);
                var collect = B(content, "Collect", UIKit.Green, () => { gm.CollectBake(); CloseModal(); }, new Vector2(0.5f, 1), new Vector2(0, -262), new Vector2(240, 66), 24, UIKit.Food);
                var speed = B(content, "", UIKit.Purple, gm.SpeedUpBake, new Vector2(0.5f, 1), new Vector2(0, -262), new Vector2(240, 66), 22, UIKit.Crystal);
                refreshers.Add(() =>
                {
                    long left = bake.doneAt - SaveSystem.Now;
                    SetBar(fill, 1f - left / (float)r.Seconds);
                    timer.text = left > 0 ? UIKit.Time(left) : "Fresh out of the oven!";
                    collect.gameObject.SetActive(left <= 0);
                    speed.gameObject.SetActive(left > 0);
                    UIKit.SetButtonLabel(speed, "Finish  " + Progression.SpeedUpCost(left));
                });
                return;
            }

            L(content, "Bake food to feed your creatures. Fed creatures grow, sing louder and earn more coins.", 19, UIKit.Wood,
                new Vector2(0.5f, 1), new Vector2(0, -8), new Vector2(760, 30));
            var grid = ScrollGrid(content, 50, new Vector2(240, 300));
            for (int i = 0; i < MythicaDB.Recipes.Length; i++)
            {
                int index = i;
                var r = MythicaDB.Recipes[i];
                var cell = Cell(grid, UIKit.Cream);
                Icon(cell, UIKit.Food, new Vector2(0.5f, 1), new Vector2(0, -14), 70 + i * 20);
                L(cell, r.Name, 22, UIKit.Ink, new Vector2(0.5f, 1), new Vector2(0, -132), new Vector2(220, 30));
                L(cell, "+" + r.Food + " food  ·  " + UIKit.Time(r.Seconds), 18, UIKit.Wood, new Vector2(0.5f, 1), new Vector2(0, -164), new Vector2(220, 26));
                var b = B(cell, UIKit.Num(r.Cost), UIKit.Green, () => gm.StartBake(index), new Vector2(0.5f, 0), new Vector2(0, 12), new Vector2(180, 58), 22, UIKit.Coin);
                UIKit.SetInteractable(b, gm.Save.coins >= r.Cost);
                b.interactable = true;
            }
        }

        // ------------------------------------------------------------ Bestiary

        void OpenBestiary() { OpenModal("Bestiary", UIKit.Purple, new Vector2(1060, 660), BuildBestiary); }

        void BuildBestiary(RectTransform content)
        {
            L(content, "Discovered " + gm.Save.discovered.Count + " of " + MythicaDB.VariantCount + " creatures. Breed different magic types to find them all!",
                19, UIKit.Wood, new Vector2(0.5f, 1), new Vector2(0, -8), new Vector2(900, 30));
            var grid = ScrollGrid(content, 50, new Vector2(180, 240));
            foreach (var s in MythicaDB.All)
            {
                Magic? shown = null;
                int found = 0;
                foreach (var m in MythicaDB.AllMagic)
                    if (gm.Save.discovered.Contains(MythicaDB.VariantKey(s.Id, m)))
                    {
                        found++;
                        if (shown == null || m == s.Native) shown = m;
                    }
                var cell = Cell(grid, found > 0 ? UIKit.Cream : new Color(0.82f, 0.8f, 0.76f));
                Portrait(cell, Portraits.Get(s, shown ?? s.Native), new Vector2(0.5f, 1), new Vector2(0, -4), 130,
                    found > 0 ? (Color?)null : new Color(0.08f, 0.06f, 0.1f, 0.85f));
                L(cell, found > 0 ? s.Name : "???", 20, UIKit.Ink, new Vector2(0.5f, 1), new Vector2(0, -134), new Vector2(170, 28));
                L(cell, found + " / " + MythicaDB.AllMagic.Length, 16, UIKit.Wood, new Vector2(0.5f, 1), new Vector2(0, -160), new Vector2(170, 22));
                for (int i = 0; i < MythicaDB.AllMagic.Length; i++)
                {
                    var m = MythicaDB.AllMagic[i];
                    bool has = gm.Save.discovered.Contains(MythicaDB.VariantKey(s.Id, m));
                    var dot = UIKit.Img(cell, UIKit.Circle, has ? MythicaDB.MagicColor(m) : new Color(0, 0, 0, 0.15f), "Dot");
                    dot.raycastTarget = false;
                    UIKit.Place(dot.rectTransform, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2((i - 2.5f) * 24f, 16), new Vector2(20, 20));
                }
            }
        }

        // ------------------------------------------------------------ Settings

        void OpenSettings() { OpenModal("Settings", UIKit.Blue, new Vector2(640, 560), BuildSettings); }

        void BuildSettings(RectTransform content)
        {
            B(content, gm.Save.soundOn ? "Sound: On" : "Sound: Off", gm.Save.soundOn ? UIKit.Green : UIKit.Grey, gm.ToggleSound,
                new Vector2(0.5f, 1), new Vector2(0, -10), new Vector2(300, 64), 24, gm.Save.soundOn ? UIKit.Speaker : UIKit.Muted);
            L(content,
                "How to play\n" +
                "<size=17>- Tap creatures or their coin bubbles to collect coins.\n" +
                "· Feed creatures with food from the Bakery to level them up.\n" +
                "· At level " + Progression.BreedMinLevel + ", breed two creatures in the Breeding Grove. Different magic types make new variants.\n" +
                "· Hatch eggs in the Nursery, and buy creatures and decorations in the Market.\n" +
                "· Drag to look around, pinch or scroll to zoom.</size>",
                22, UIKit.Ink, new Vector2(0.5f, 1), new Vector2(0, -92), new Vector2(Mathf.Min(560f, content.rect.width > 0 ? content.rect.width : 560f), 250), TextAnchor.UpperLeft);
            B(content, Time.unscaledTime < resetArmedUntil ? "Tap again to erase" : "Start over", UIKit.Red, () =>
            {
                if (Time.unscaledTime < resetArmedUntil)
                {
                    resetArmedUntil = 0;
                    CloseModal();
                    gm.ResetGame();
                    gm.Toast("A fresh island. Let the song begin again!");
                }
                else
                {
                    resetArmedUntil = Time.unscaledTime + 3f;
                    RebuildModal();
                }
            }, new Vector2(0.5f, 0), new Vector2(0, 6), new Vector2(280, 60), 22);
        }

        // ------------------------------------------------------------ start screen

        void BuildStartScreen()
        {
            var layer = UIKit.Stretch(UIKit.Rect("Start", root));
            startScreen = layer.gameObject;
            var bg = layer.gameObject.AddComponent<Image>();
            bg.color = new Color(0.12f, 0.2f, 0.38f, 0.55f);

            var title = UIKit.Label(layer, "Singing Mythica", 84, UIKit.Gold, TextAnchor.MiddleCenter, true);
            UIKit.Place(title.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 150), new Vector2(1000, 120));
            title.GetComponent<Outline>().effectDistance = new Vector2(4, -4);
            var sub = UIKit.Label(layer, "Grow a chorus of mythical creatures", 30, Color.white, TextAnchor.MiddleCenter, true);
            UIKit.Place(sub.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 70), new Vector2(900, 50));

            var play = UIKit.Button(layer, "Start the song!", UIKit.Green, () =>
            {
                gm.BeginSession();
                StartCoroutine(HideStart());
            }, 36, UIKit.Note);
            UIKit.Place((RectTransform)play.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -50), new Vector2(420, 100));
            StartCoroutine(Pulse(play.transform));

            var tip = UIKit.Label(layer, "Sound on! Every creature sings its own part of the song.", 22, new Color(1, 1, 1, 0.85f), TextAnchor.MiddleCenter, true);
            UIKit.Place(tip.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -150), new Vector2(900, 40));
        }

        IEnumerator Pulse(Transform t)
        {
            while (t != null && startScreen != null && startScreen.activeSelf)
            {
                float k = 1f + Mathf.Sin(Time.unscaledTime * 4f) * 0.04f;
                t.localScale = new Vector3(k, k, 1);
                yield return null;
            }
        }

        IEnumerator HideStart()
        {
            var group = startScreen.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            for (float e = 0; e < 0.35f; e += Time.unscaledDeltaTime)
            {
                group.alpha = 1f - e / 0.35f;
                yield return null;
            }
            startScreen.SetActive(false);
            if (gm.Save.creatures.Count <= 2 && gm.Save.level == 1)
                ShowToast("Tap your creatures to collect coins, and feed them to help them grow!");
        }
    }
}
