using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace BepJam
{
    /// <summary>The kitchen screen. Builds the layered scene from the artist's mock-up coordinates
    /// and turns every model event into animation + VFX + sound.</summary>
    public class GameView : MonoBehaviour
    {
        // ---- layout (mock-up px, 853 x 1844, origin top-left)
        // Venba shop layout (Foodie-Pop-like structure): HUD → customers in the shop window → counter row
        // six counter plates → cooking row → pantry on the work table → round boosters. Units: canvas/1.2661.
        // Foodie-Pop-like stack (canvas/1.2661 units): customers band → counter plates → cooking line → 4x4 pantry → boosters
        // v7 layout: noren · chibi heads peeking over the counter · colourful plates · cooking row · 4x4 trays on the table
        static readonly float[] SeatX = { 182, 426, 671 };
        const float SeatBottom = 486f, CustW = 228f, CustH = 200f;
        const float PlateY = 544f, PlateW = 126f, PlateH = 84f, PlatePadding = 48f;
        const float TileW = 166f, TileH = 138f, TileSpacing = 181.7f, TableCX = 426.5f;
        // Reserve a real message band below the appliances; four trays still fit above the boosters.
        static readonly float[] RowY = { 1080f, 1230f, 1380f, 1530f };
        static readonly float[] RowS = { 1f, 1f, 1f, 1f };
        static float TileCX(int c, int r) => TableCX + (c - 1.5f) * TileSpacing * RowS[r];
        static readonly float[] PanelX = { 155f, 291f, 426f, 562f, 698f };   // noren curtain panels

        public LevelDef def;
        public Kitchen kitchen;
        RectTransform root, tilesLayer, customersLayer, slotsLayer, messagesLayer, coachRt, chainStampRt;
        readonly Dictionary<int, TileView> tiles = new Dictionary<int, TileView>();
        readonly Dictionary<int, CustomerView> custViews = new Dictionary<int, CustomerView>();
        readonly Dictionary<StationType, StationView> stations = new Dictionary<StationType, StationView>();
        PlateView[] plates;
        float plateScale;
        Text servedText, chainText, dayText, hintCount, returnCount;
        RectTransform servedBox, chainBox, dayBox, toastRt, handRt;
        JuicyButton hintBtn, returnBtn, bookBtn;
        Text toastText; CanvasGroup toastCg;
        readonly List<(RectTransform chip, Text text, Image[] ghosts)> stacks = new List<(RectTransform, Text, Image[])>();
        bool popupOpen, waitToastShown;
        int tutorialStep = -1;
        readonly Queue<(string key, string text)> coachQueue = new Queue<(string, string)>();
        readonly HashSet<string> queuedTips = new HashSet<string>();
        RectTransform coachStrip;
        float nextCoachAt, coachHideAt, lastPlayerTap = -10, motionUntil;
        bool ActiveTransition => AnimationTime < motionUntil || kitchen.slots.Any(s => s.timer > 0) ||
            kitchen.stations.Values.Any(s => s.state != StationState.Idle);
        public bool Paused => popupOpen || kitchen == null || kitchen.paused;
        public bool AnimationsPaused => Paused || !isActiveAndEnabled || !kitchen.started;
        // End events are drained before popupOpen is set, so their particles are still cleared with the game.
        public bool GameplayFxActive => !popupOpen && kitchen != null && kitchen.started;
        public float AnimationTime { get; private set; }

        public static GameView AnimationOwner(Transform target)
        {
            if (!target) return null;
            var g = target.GetComponentInParent<GameView>();
            if (g) return g;
            var scope = target.GetComponentInParent<GameplayAnimationScope>();
            return scope ? scope.game : null;
        }
        public static bool MotionPaused(Component target)
        {
            var g = target ? AnimationOwner(target.transform) : null;
            return g && (g.AnimationsPaused || g.kitchen.ended);
        }

        RectTransform tipRt; float shineT = 2f; float tipOpenedAt = -9f;

        void OnEnable() { CustomerView.OrderTapped += ShowRecipeTip; TileView.Held += ShowIngredientTip; }
        void OnDisable()
        {
            CustomerView.OrderTapped -= ShowRecipeTip; TileView.Held -= ShowIngredientTip;
            StopAllCoroutines();
            Tween.KillHierarchy(transform);
            if (messagesLayer) { Tween.KillHierarchy(messagesLayer); Destroy(messagesLayer.gameObject); }
            VFX.ClearGameplay(this);
        }

        // Canvas units: >=24 px clear of the stations and even a front tray's pop animation.
        RectTransform MessageCard(string name, float height = 204)
        {
            return UIKit.Node(name, messagesLayer).Anchor(new Vector2(0.5f, 1),
                new Vector2(0, -UIKit.S(888.5f)), new Vector2(960, height));
        }
        Text MessageText(RectTransform parent, string text, int size = 36)
        {
            var label = UIKit.Label("Text", parent, text, size, Pal.Hex("FBF1DE"));
            label.rectTransform.Stretch(32, 14, 32, 14);
            return UIKit.FitLines(label, size, 20, 2);
        }
        void HideToast()
        {
            if (chainStampRt) { Tween.KillHierarchy(chainStampRt); chainStampRt.gameObject.SetActive(false); Destroy(chainStampRt.gameObject); chainStampRt = null; }
            if (!toastRt) return;
            Tween.KillAll(toastCg); Tween.KillAll(toastRt);
            toastCg.alpha = 0; toastRt.gameObject.SetActive(false);
        }

        /// <summary>Ingredient combinations fit in a compact grid in the reserved, tap-through band.</summary>
        void ShowIngredientTip(TileView t)
        {
            if (!t || Paused || kitchen.ended) return;
            if (def.number <= 10)
            {
                var ingredient = GameData.Ingredients[t.crate.ing];
                var dishes = def.customers.SelectMany(c => c).Distinct().Select(id => GameData.Dishes[id])
                    .Where(d => d.parts.Contains(ingredient.id));
                QueueCoach("ingredient_" + ingredient.id, ingredient.name + ": " + string.Join(" · ", dishes.Select(d => d.ShortName)));
                return;
            }
            HideTip(); HideHand(); HideCoachStrip(); HideToast();
            tipOpenedAt = Time.unscaledTime;
            Sfx.Play("popup", 0.5f);
            var ing = GameData.Ingredients[t.crate.ing];
            var combos = GameData.Dishes.Values.Where(d => d.parts.Contains(ing.id)).ToList();
            tipRt = MessageCard("IngTip");
            var bg = UIKit.Sliced("Bg", tipRt, Art2("speech_cream")); bg.rectTransform.Stretch();
            var title = UIKit.Label("Name", tipRt, ing.name, 34, Pal.TextBrown);
            title.rectTransform.Anchor(new Vector2(0.5f, 1), new Vector2(0, -28), new Vector2(896, 40));
            UIKit.FitLines(title, 34, 20, 2);
            int cols = combos.Count > 2 ? 2 : 1;
            int rows = Mathf.Max(1, Mathf.CeilToInt(combos.Count / (float)cols));
            float rowH = 144f / rows, cellW = 896f / cols;
            for (int i = 0; i < combos.Count; i++)
            {
                var d = combos[i];
                var row = UIKit.Node("Row_" + d.id, tipRt).Anchor(new Vector2(0.5f, 1),
                    new Vector2((i % cols - (cols - 1) * 0.5f) * cellW, -52 - (i / cols + 0.5f) * rowH), new Vector2(cellW - 16, rowH));
                var name = UIKit.Label("DishName", row, d.ShortName, 24, Pal.TextBrown);
                name.rectTransform.Anchor(new Vector2(0.5f, 1), new Vector2(0, -13), new Vector2(cellW - 32, 24));
                UIKit.Fit(name, 24, 16);
                var equation = UIKit.Node("Equation", row).Stretch(8, 28, 8, 2);
                RecipeUI.Draw(equation, d, cellW - 32, Mathf.Min(66, rowH - 32), false);
            }
            RevealTip(3.2f);
        }

        /// <summary>Every part and the action fit below the stations, away from order cards and trays.</summary>
        void ShowRecipeTip(CustomerView cv, int idx)
        {
            if (!cv || Paused || kitchen.ended || idx < 0 || idx >= cv.customer.orders.Count) return;
            var d = GameData.Dishes[cv.customer.orders[idx].dish];
            if (def.number <= 10)
            {
                QueueCoach("recipe_" + d.id, d.ShortName + ": " +
                    string.Join(" + ", d.parts.Select(p => GameData.Ingredients[p].name)) + " · " + d.verb);
                return;
            }
            HideTip(); HideHand(); HideCoachStrip(); HideToast();
            tipOpenedAt = Time.unscaledTime;
            Sfx.Play("popup", 0.6f);
            tipRt = MessageCard("RecipeTip");
            var bg = UIKit.Sliced("Bg", tipRt, Art2("speech_cream")); bg.rectTransform.Stretch();
            string how = d.station == StationType.None ? d.verb : d.verb + " · " + GameData.StationName(d.station);
            var title = UIKit.Label("Name", tipRt, d.name + " · " + how, 34, Pal.TextBrown);
            title.rectTransform.Anchor(new Vector2(0.5f, 1), new Vector2(0, -32), new Vector2(896, 50));
            UIKit.FitLines(title, 34, 20, 2);
            var equation = UIKit.Node("Recipe", tipRt).Anchor(new Vector2(0.5f, 0), new Vector2(0, 70), new Vector2(896, 128));
            RecipeUI.Draw(equation, d, 896, 90, true, true, 0.02f);
            RevealTip(3.2f);
        }

        void RevealTip(float hold)
        {
            var mine = tipRt;
            mine.localScale = Vector3.one * 0.8f;
            Tween.Scale(mine, Vector3.one, 0.2f).SetEase(Ease.OutCubic);
            Tween.Delay(hold, () => { if (mine && mine == tipRt) HideTip(); }, owner: mine);
        }

        void HideTip()
        {
            if (tipRt == null) return;
            var t = tipRt; tipRt = null;
            // Retiring cards cannot share the safe band with the next message or survive cancelled tweens.
            Tween.KillHierarchy(t);
            t.gameObject.SetActive(false); Destroy(t.gameObject);
        }

        public static GameView Create(Transform parent, LevelDef def, bool skipIntro)
        {
            var rt = UIKit.Node("GameView", parent); rt.Stretch();
            var gv = rt.gameObject.AddComponent<GameView>();
            gv.def = def; gv.root = rt;
            var layout = LevelGen.Build(def);
#if UNITY_EDITOR
            Debug.Log(LevelGen.ReportLine(def, layout));
#endif
            gv.kitchen = new Kitchen(def, layout);
            gv.Build();
            if (skipIntro) gv.StartLevel();
            else gv.ShowIntro();
            return gv;
        }

        RectTransform Art(string name, Sprite s, float x, float y, float w, float h, Transform parent = null, bool preserve = true)
        {
            var img = UIKit.Img(name, parent ?? root, s, false, preserve);
            return ((RectTransform)img.transform).PlaceArt(x, y, w, h);
        }

        // ================================================================= build
        void Build()
        {
            // Venba-style shop interior (fills the phone: 1304x2336 canvas, centred)
            const float bgW = 1080f / 1.2661f, bgH = 2336f / 1.2661f, bgX = 0f;
            Art("Shop", BepJam.Art.UI("bg_shop"), bgX, 0, bgW, bgH, null, false);
            // bg_shop is opaque and includes the old lake. Replace only its clear opening:
            // source pixels x=158..929, y=300..600 in the 1080x2336 sprite. The curtain,
            // jambs and counter stay intact. No overlay means the original Hồ Gươm fallback.
            var scene = Chapters.WindowBackground(def);
            if (scene) Art("ChapterBackground", scene, 158f / 1.2661f, 300f / 1.2661f,
                771f / 1.2661f, 300f / 1.2661f, null, false);
            AmbientPetals();
            // customers stand behind the counter, visible only through the shop window
            var win = UIKit.Node("Window", root).PlaceArt(0, 128, 853, 346);
            win.gameObject.AddComponent<RectMask2D>();
            customersLayer = UIKit.Node("Customers", win);
            customersLayer.anchorMin = customersLayer.anchorMax = new Vector2(0, 1); customersLayer.pivot = new Vector2(0, 1);
            customersLayer.sizeDelta = new Vector2(1080, 2336);
            customersLayer.anchoredPosition = new Vector2(0, UIKit.S(128));   // so children use phone coordinates
            // counter + work table in front of the customers
            Art("Counter", BepJam.Art.UI("bg_front"), bgX, 0, bgW, bgH, null, false);

            // Cooking row. Keep station bubbles inside the appliances, below the plate row.
            stations[StationType.Oven] = StationView.Create(root, StationType.Oven, BepJam.Art.UI("oven"));
            stations[StationType.Oven].rt.PlaceArt(47, 628, 237, 158);
            stations[StationType.Stove] = StationView.Create(root, StationType.Stove, BepJam.Art.UI("stove"));
            stations[StationType.Stove].rt.PlaceArt(308, 596, 237, 190);
            stations[StationType.Board] = StationView.Create(root, StationType.Board, BepJam.Art.UI("board"));
            stations[StationType.Board].rt.PlaceArt(569, 671, 237, 111);
            foreach (var station in stations.Values)
                if (station.bubble != null) station.bubble.anchoredPosition = new Vector2(0, -UIKit.S(50));

            // One evenly spaced row, including scaled ingredients and knife/wait badges.
            plates = new PlateView[kitchen.slots.Length];
            float rowWidth = UIKit.ArtW - 2 * PlatePadding;
            plateScale = Mathf.Min(0.8f, rowWidth / Mathf.Max(1, plates.Length) / (PlateW + 12));
            float plateWidth = PlateW * plateScale;
            for (int i = 0; i < plates.Length; i++)
            {
                plates[i] = PlateView.Create(root, i);
                float px = plates.Length == 1 ? UIKit.ArtW / 2 : PlatePadding + plateWidth / 2 +
                    i * (rowWidth - plateWidth) / Mathf.Max(1, plates.Length - 1);
                plates[i].rt.PlaceArt(px - PlateW / 2, PlateY - PlateH / 2, PlateW, PlateH);
                plates[i].rt.localScale = Vector3.one * plateScale;
            }

            // pantry: soft shadow slots on the work table
            slotsLayer = UIKit.Node("Slots", root); slotsLayer.Stretch();
            for (int c = 0; c < LevelGen.COLS; c++)
                for (int r = 0; r < LevelGen.VISIBLE; r++)
                {
                    continue; // no grey slot shadows (client request)
#pragma warning disable CS0162
                    var hole = UIKit.Sliced("Hole", slotsLayer, BepJam.Art.FX("rounded"), new Color(0.35f, 0.18f, 0.1f, 0.18f));
                    float hw = (TileW - 10) * RowS[r], hh = (TileH - 8) * RowS[r];
                    ((RectTransform)hole.transform).PlaceArt(TileCX(c, r) - hw / 2, RowY[r] - hh / 2 + 6, hw, hh);
                }
            for (int c = 0; c < LevelGen.COLS; c++) BuildStack(c);
            tilesLayer = UIKit.Node("Tiles", root); tilesLayer.Stretch();
            for (int c = 0; c < kitchen.columns.Length; c++)
                for (int r = 0; r < kitchen.columns[c].visible.Count; r++)
                    MakeTile(kitchen.columns[c].visible[r], c, r);
            RefreshStacks();

            var stationFX = UIKit.Node("StationFX", root); stationFX.Stretch();
            Canvas.ForceUpdateCanvases();
            foreach (var station in stations.Values) station.RaiseFX(stationFX);

            BuildHud();
            messagesLayer = UIKit.Node("Messages_" + def.number, GameBoot.I.gameplayLayer).Stretch();
            messagesLayer.gameObject.AddComponent<GameplayAnimationScope>().game = this;
            var messageGroup = UIKit.Group(messagesLayer.gameObject);
            messageGroup.blocksRaycasts = false; messageGroup.interactable = false;
            BuildToast();
        }

        void BottleSquirt(RectTransform bottle)
        {
            bottle.pivot = new Vector2(0.5f, 0f); bottle.anchoredPosition -= new Vector2(0, bottle.sizeDelta.y / 2);
            var hit = bottle.GetComponent<Image>(); hit.raycastTarget = true;
            var jb = bottle.gameObject.AddComponent<JuicyButton>(); jb.sound = "pop";
            jb.onClick = () =>
            {
                Tween.Squash(bottle, 0.3f, 0.45f);
                VFX.Burst(bottle.position + Vector3.up * bottle.rect.height * bottle.lossyScale.y, "circle", 6,
                    new[] { bottle.name == "BottleY" ? Pal.Butter : Pal.Tomato }, 150, 320, 14, 26, 0.6f, 900f, 0, 60, 120);
            };
        }

        void AmbientPetals()
        {
            var amb = UIKit.Node("Ambient", root); amb.Stretch();
            amb.gameObject.AddComponent<AmbientDrift>().Init(amb);
            var dust = UIKit.Node("Dust", root); dust.Stretch();
            dust.gameObject.AddComponent<DustMotes>().Init(dust, new Rect(80, -560, 920, 380));
        }

        /// <summary>Shop-sign banner dropping from the top: "Ngày N · Mở hàng!".</summary>
        void OpenBanner()
        {
            Toast(def.boss ? "ĐẠI TIỆC · Ngày " + def.number : "Ngày " + def.number + " · Mở hàng!", 1.3f);
            Tween.Delay(0.18f, () => Sfx.Play("paper", 0.8f), owner: this);
            Tween.Delay(0.45f, () =>
            {
                Sfx.Play("customer_in", 0.6f);
                if (toastRt && toastRt.gameObject.activeSelf) VFX.Sparkle(toastRt.position, 8, 0.5f);
            }, owner: this);
            if (def.boss) Tween.Delay(0.5f, () => { VFX.ScreenShake(14, 0.3f); Sfx.Play("stamp"); }, owner: this);
        }

        void ChainStamp(int n)
        {
            Tween.PunchScale(chainBox, Vector3.one * 0.2f, 0.25f);
            if (handRt || tipRt || coachStrip) return;
            HideToast();
            var node = chainStampRt = MessageCard("ChainStamp", 96);
            node.sizeDelta = new Vector2(400, 96);
            var paper = UIKit.Sliced("Stamp", node, Art2("paper_tag")); paper.rectTransform.Stretch();
            MessageText(node, "CHUỖI x" + n, 46);
            node.localEulerAngles = new Vector3(0, 0, -4);
            node.localScale = Vector3.one * 1.1f;
            var cg = UIKit.Group(node.gameObject); cg.alpha = 0;
            Tween.Scale(node, Vector3.one, 0.16f).SetEase(Ease.InQuad);
            Tween.Fade(cg, 1, 0.1f);
            Tween.Fade(cg, 0, 0.25f).SetDelay(0.9f).OnComplete(() => { if (node) Destroy(node.gameObject); });
            Sfx.Play("stamp", 0.8f);
            VFX.Ring(node.position, Pal.Hex("D9414F"), 96, 0.4f);
            if (n >= 3) { VFX.Flash(node.position, 140, 0.3f); VFX.ScreenShake(8 + 2 * Mathf.Min(n, 6), 0.25f); }
        }

        void BuildStack(int c)
        {
            int top = LevelGen.VISIBLE - 1; float ts = RowS[top]; float x = TileCX(c, top) - TileW * ts / 2; float ty = RowY[top] - TileH * ts / 2;
            var ghosts = new Image[2];
            for (int g = 1; g >= 0; g--)
            {
                var gi = UIKit.Sliced("Ghost" + g, slotsLayer, BepJam.Art.UI("tile"), new Color(0.86f - g * 0.08f, 0.72f - g * 0.08f, 0.58f - g * 0.08f));
                ((RectTransform)gi.transform).PlaceArt(x + (8 + g * 6) * ts, ty - (8 + g * 7) * ts, (TileW - 16 - g * 12) * ts, TileH * ts);
                ghosts[g] = gi;
            }
            var chip = UIKit.Sliced("Stack", root, Art2("pill"), Pal.Caramel);
            var crt = ((RectTransform)chip.transform).PlaceArt(x - 8, ty - 20, 62, 40);
            var t = UIKit.Label("N", crt, "+0", 30, Color.white, TextAnchor.MiddleCenter, 2.5f);
            ((RectTransform)t.transform).Stretch(0, 0, 0, 5);
            stacks.Add((crt, t, ghosts));
        }

        static Sprite Art2(string fx) => BepJam.Art.FX(fx);

        void RefreshStacks(int punchCol = -1)
        {
            for (int c = 0; c < stacks.Count; c++)
            {
                int n = kitchen.HiddenCount(c);
                var (chip, text, ghosts) = stacks[c];
                chip.gameObject.SetActive(n > 0);
                text.text = "+" + n;
                ghosts[0].enabled = false; ghosts[1].enabled = false;
                if (c == punchCol && n > 0) Tween.PunchScale(chip, Vector3.one * 0.4f, 0.35f);
            }
            // Keep stock counts above their trays. The separate message layer remains above this whole screen.
            foreach (var s in stacks) s.chip.SetAsLastSibling();
        }

        Vector2 CellPos(int c, int r) => new Vector2(UIKit.S(TileCX(c, r)), -UIKit.S(RowY[r]));
        static Vector3 RowScale(int r) => Vector3.one * RowS[Mathf.Clamp(r, 0, RowS.Length - 1)];

        TileView MakeTile(Crate cr, int c, int r)
        {
            var t = TileView.Create(tilesLayer, cr, new Vector2(UIKit.S(TileW), UIKit.S(TileH)));
            t.col = c; t.row = r;
            t.rt.anchorMin = t.rt.anchorMax = new Vector2(0, 1);
            t.rt.anchoredPosition = CellPos(c, r);
            t.rt.localScale = RowScale(r);
            t.onTap = OnTileTap;
            tiles[cr.id] = t;
            return t;
        }

        static readonly Color Terra = Pal.Hex("C8573E"), Navy = Pal.Hex("2E3A59"), CreamUI = Pal.Hex("FBEBD3"), Ink = Pal.Hex("4A2F26");

        RectTransform FlatPanel(string name, Transform parent, Color c, float x, float y, float w, float h)
        {
            // Venba torn-paper strip
            var img = UIKit.Sliced(name, parent, BepJam.Art.FX("hud_bar"));
            return ((RectTransform)img.transform).PlaceArt(x, y, w, h);
        }

        JuicyButton RoundButton(string name, string icon, Color c, float x, float y, float d, System.Action click)
        {
            // hand-cut cream disc with a darker lip; the icon keeps its own colours so it never disappears
            var b = UIKit.Button(name, root, BepJam.Art.FX("btn_round_cream"), null, 0, click, BepJam.Art.FX(icon));
            ((RectTransform)b.transform).PlaceArt(x, y, d, d);
            b.icon.rectTransform.Stretch(UIKit.S(d) * 0.22f, UIKit.S(d) * 0.2f, UIKit.S(d) * 0.22f, UIKit.S(d) * 0.26f);
            return b;
        }

        void BuildHud()
        {
            RoundButton("Back", "home", Terra, 34, 34, 78, OpenPause);
            RoundButton("Pause", "gear", Terra, 741, 34, 78, OpenPause);
            var bar = FlatPanel("TopBar", root, CreamUI, 128, 30, 597, 86);
            // three stats inside the bar
            servedBox = Stat(bar, 0, "Khách", Navy, out servedText);
            dayBox = Stat(bar, 1, $"Ngày {def.number}", Terra, out dayText);
            chainBox = Stat(bar, 2, "Chuỗi", Navy, out chainText);
            dayText.fontSize = 26; UIKit.Fit(dayText, 26, 18);
            // tapping the day opens the recipe book
            var hit = dayBox.gameObject.AddComponent<Image>(); hit.color = new Color(1, 1, 1, 0); hit.raycastTarget = true;
            dayBox.gameObject.AddComponent<JuicyButton>().onClick = () => { if (!Paused) OpenRecipes(); };
            // bottom boosters
            hintBtn = Booster("Gợi ý", "bulb", Pal.Hex("E3A33B"), 158, out hintCount, () => { if (!Paused) UseHint(); });
            returnBtn = Booster("Trả đĩa", "undo", Pal.Hex("4E8E8A"), 354.5f, out returnCount, () => { if (!Paused) UseReturn(); });
            bookBtn = Booster("Công thức", "book", Terra, 551, out _, () => { if (!Paused) OpenRecipes(); });
            LockBooster(hintBtn, "hint"); LockBooster(returnBtn, "return"); LockBooster(bookBtn, "book");
            UpdateBoosters();
            UpdateHud();
        }

        RectTransform Stat(RectTransform bar, int i, string label, Color valueColor, out Text value)
        {
            float w = bar.rect.width / 3f;
            var box = UIKit.Node(label, bar);
            box.Anchor(new Vector2(0, 0.5f), new Vector2(w * (i + 0.5f), 0), new Vector2(w, bar.rect.height));
            var l = UIKit.Label("L", box, label, 28, Pal.Hex("8A6B53"));
            ((RectTransform)l.transform).Anchor(new Vector2(0.5f, 1f), new Vector2(0, -30), new Vector2(w - 24, 36));
            UIKit.Fit(l, 28, 16);
            value = UIKit.Label("V", box, "", 48, valueColor);
            ((RectTransform)value.transform).Anchor(new Vector2(0.5f, 0f), new Vector2(0, 40), new Vector2(w - 24, 56));
            UIKit.Fit(value, 48, 22);
            if (i > 0)
            {
                var sep = UIKit.Img("Sep", box, BepJam.Art.FX("rounded"), false, false); sep.color = new Color(0.55f, 0.4f, 0.3f, 0.25f);
                ((RectTransform)sep.transform).Anchor(new Vector2(0, 0.5f), Vector2.zero, new Vector2(4, bar.rect.height * 0.6f));
            }
            return box;
        }

        JuicyButton Booster(string label, string icon, Color c, float x, out Text count, System.Action click)
        {
            var b = RoundButton(label, icon, c, x, 1618, 143, click);
            var brt = (RectTransform)b.transform;
            var l = UIKit.Label("L", brt, label, 42, CreamUI, TextAnchor.MiddleCenter, 0f, null, true);
            ((RectTransform)l.transform).Anchor(new Vector2(0.5f, 0f), new Vector2(0, -52), new Vector2(224, 56));
            UIKit.Fit(l, 42, 24);
            count = null;
            if (icon != "book")
            {
                var badge = UIKit.Img("Badge", brt, BepJam.Art.FX("badge_red"), false, false);
                ((RectTransform)badge.transform).Anchor(new Vector2(1, 1), new Vector2(-4, -4), new Vector2(50, 50));
                count = UIKit.Label("C", badge.transform, "3", 32, Color.white);
                ((RectTransform)count.transform).Stretch(4, 4, 4, 4);
                UIKit.Fit(count, 32, 18);
            }
            return b;
        }

        void BuildToast()
        {
            toastRt = MessageCard("Toast", 108);
            toastCg = UIKit.Group(toastRt.gameObject); toastCg.alpha = 0; toastCg.blocksRaycasts = false;
            var bg = UIKit.Sliced("Bg", toastRt, Art2("speech_pink")); ((RectTransform)bg.transform).Stretch();
            toastText = MessageText(toastRt, "", 38);
            toastRt.gameObject.SetActive(false);
        }

        public void Toast(string msg, float hold = 1.8f)
        {
            if (!this || kitchen.ended || !toastRt || string.IsNullOrEmpty(msg) || handRt || tipRt || coachStrip) return;
            HideToast();
            toastRt.gameObject.SetActive(true);
            toastText.text = msg; UIKit.FitLines(toastText, 38, 20, 2);
            toastRt.localScale = Vector3.one * 0.8f;
            Tween.Scale(toastRt, Vector3.one, 0.2f).SetEase(Ease.OutCubic);
            Tween.Fade(toastCg, 1, 0.15f).OnComplete(() =>
            {
                if (toastCg) Tween.Fade(toastCg, 0, 0.25f).SetDelay(hold).OnComplete(() =>
                { if (toastRt) toastRt.gameObject.SetActive(false); });
            });
        }

        void StopGameplayEffects()
        {
            StopAllCoroutines();
            HideHand(); HideTip(); HideCoachStrip(); HideToast();
            coachQueue.Clear(); tutorialStep = -1;
            if (messagesLayer) Tween.KillHierarchy(messagesLayer);
            Tween.KillHierarchy(transform);
            VFX.ClearGameplay(this);
            foreach (var p in plates) if (p) p.StopEffects();
            foreach (var st in stations.Values) if (st) st.StopEffects();
            foreach (var cv in GetComponentsInChildren<CustomerView>()) cv.StopEffects();
            foreach (var t in tilesLayer.GetComponentsInChildren<TileView>())
            {
                if (t.crate.count <= 0) { t.gameObject.SetActive(false); Destroy(t.gameObject); }
                else { t.rt.anchoredPosition = CellPos(t.col, t.row); t.rt.localScale = RowScale(t.row); t.visual.localScale = Vector3.one; }
            }
            if (GameBoot.I.CurrentGame == this) GameBoot.I.content.localPosition = Vector3.zero;
        }

        // ================================================================= flow
        void ShowIntro()
        {
            popupOpen = true;
            Popups.LevelIntro(def, () => { popupOpen = false; StartLevel(); }, () => GameBoot.I.ShowHome());
        }

        void StartLevel()
        {
            kitchen.Start();
            OpenBanner();
            StartCoroutine(Onboarding());
        }

        // ================================================================= onboarding: once-per-save, tap-through strips
        System.Collections.IEnumerator Onboarding()
        {
            // Queue immediately so rapid play cannot miss a mechanic's introduction.
            if (!string.IsNullOrEmpty(def.boosterIntro))
                QueueCoach("booster_" + def.boosterIntro, BoosterCoachText(def.boosterIntro));
            if (def.coach != null)
                foreach (var key in def.coach)
                {
                    if (key == "tap_pair")
                    {
                        if (!Progress.TipSeen(key)) tutorialStep = 0;
                        continue;
                    }
                    QueueCoach(key, CoachText(key));
                }
            nextCoachAt = AnimationTime + 1.9f;
            yield break;
        }

        void QueueCoach(string key, string text)
        {
            if (string.IsNullOrEmpty(text) || Progress.TipSeen(key) || !queuedTips.Add(key)) return;
            coachQueue.Enqueue((key, text));
        }

        void HideCoachStrip()
        {
            if (!coachStrip) return;
            Tween.KillHierarchy(coachStrip);
            coachStrip.gameObject.SetActive(false); Destroy(coachStrip.gameObject); coachStrip = null;
            // Six clear seconds between strips, including one dismissed by a tap or motion.
            nextCoachAt = AnimationTime + 6f;
        }

        void UpdateCoaches()
        {
            if (coachStrip && (AnimationTime >= coachHideAt || ActiveTransition)) HideCoachStrip();
            if (tutorialStep >= 0 && ActiveTransition) HideHand();
            if (AnimationsPaused || kitchen.ended || ActiveTransition || AnimationTime - lastPlayerTap < 0.65f) return;
            if (tutorialStep >= 0)
            {
                if (!handRt && AnimationTime >= 1.2f) TutorialNext();
                return;
            }
            if (coachStrip || handRt || tipRt || AnimationTime < nextCoachAt ||
                (toastRt && toastRt.gameObject.activeSelf) || chainStampRt) return;
            while (coachQueue.Count > 0)
            {
                var (key, text) = coachQueue.Dequeue();
                if (Progress.TipSeen(key)) continue;
                HideToast();
                coachStrip = MessageCard("CoachStrip_" + key, 96);
                var group = UIKit.Group(coachStrip.gameObject);
                group.blocksRaycasts = false; group.interactable = false;
                var bg = UIKit.Sliced("Bg", coachStrip, Art2("speech_cream")); bg.rectTransform.Stretch();
                MessageText(coachStrip, text, 30).color = Pal.TextBrown;
                coachHideAt = AnimationTime + 3f;
                Progress.MarkTipSeen(key);
                break;
            }
        }

        static string BoosterCoachText(string id) => id == "hint" ? "Bí quá? Bấm Gợi ý để biết nên chạm khay nào!"
            : id == "return" ? "Đĩa đầy? Bấm Trả đĩa để cất bớt nguyên liệu về kho."
            : "Chạm món khách gọi hoặc nút Công thức để xem cách làm!";

        static string CoachText(string key)
        {
            switch (key)
            {
                case "plates": return "Mỗi đĩa một nguyên liệu – đủ bộ là tự ghép món khách gọi!";
                case "hidden": return "Lấy hết khay phía trước để mở khay phía sau!";
                case "stove": return "Món Hấp và Nấu sẽ tự vào bếp ga – mỗi lần nấu 1 món.";
                case "oven": return "Món Nướng vào lò – lò và bếp ga chạy song song!";
                case "shared": return "Bánh mì dùng cho cả hai món – ghép đúng món khách cần!";
                case "multi": return "Món 3 nguyên liệu: đặt đủ từng thứ lên đĩa – đủ bộ là tự ghép!";
                case "multi4": return "Món 4 nguyên liệu: cần 4 đĩa trống, tính trước nhé!";
                case "vip": return "Phục vụ VIP trước khi hết kiên nhẫn để nhận gấp đôi xu!";
                case "lookahead": return "Kho có cả phần của khách chưa tới – giữ chỗ cho 3 khách đang ngồi!";
            }
            return null;
        }

        void OpenPause()
        {
            if (popupOpen || kitchen.ended) return;
            popupOpen = true; kitchen.paused = true;
            Popups.Pause(() => { popupOpen = false; kitchen.paused = false; },
                         () => GameBoot.I.PlayLevel(def.number, true),
                         () => GameBoot.I.ShowHome());
        }

        void LockBooster(JuicyButton b, string id)
        {
            if (b == null || GameData.BoosterUnlocked(id, def.number)) return;
            int day = id == "book" ? GameData.BookDay : id == "hint" ? GameData.HintDay : GameData.ReturnDay;
            foreach (var g in b.GetComponentsInChildren<Image>()) g.color = new Color(0.55f, 0.5f, 0.5f);
            var lk = UIKit.Img("Lock", b.transform, Art2("lock"));
            ((RectTransform)lk.transform).Anchor(new Vector2(0.5f, 0.5f), new Vector2(0, 4), new Vector2(64, 64));
            foreach (var t in b.GetComponentsInChildren<Text>()) if (t.transform.parent.name == "Badge") t.transform.parent.gameObject.SetActive(false);
            b.onClick = () => { Toast($"Mở khoá ở Ngày {day}!"); Tween.PunchRotation(b.transform, 10, 0.3f); Sfx.Play("error", 0.6f); };
        }

        public void OpenRecipes()
        {
            if (Paused || kitchen.ended) return;
            HideTip(); HideCoachStrip(); HideToast();
            popupOpen = true; kitchen.paused = true;
            var dishes = def.customers.SelectMany(c => c).Distinct().ToList();
            Popups.Recipes(dishes, () => { popupOpen = false; kitchen.paused = false; });
        }

        void UseHint()
        {
            HideCoachStrip();
            if (kitchen.hintsLeft <= 0) { Toast("Hết lượt gợi ý rồi!"); Sfx.Play("error"); return; }
            int id = kitchen.Hint();
            if (id < 0) { Toast("Hãy dùng Trả đĩa hoặc đợi bếp nấu xong!"); return; }
            kitchen.hintsLeft--; UpdateBoosters();
        }

        void UseReturn()
        {
            HideCoachStrip();
            if (kitchen.returnsLeft <= 0) { Toast("Hết lượt trả đĩa!"); Sfx.Play("error"); return; }
            if (!kitchen.ReturnPlate()) { Toast("Chưa có đĩa nguyên liệu nào để trả."); Sfx.Play("error"); return; }
            UpdateBoosters();
        }

        void UpdateBoosters()
        {
            if (hintCount) hintCount.text = kitchen.hintsLeft.ToString();
            if (returnCount) returnCount.text = kitchen.returnsLeft.ToString();
            SetDim(hintBtn, kitchen.hintsLeft <= 0); SetDim(returnBtn, kitchen.returnsLeft <= 0);
        }

        static void SetDim(JuicyButton b, bool dim)
        {
            foreach (var g in b.GetComponentsInChildren<Image>()) if (g.gameObject.name != "Badge") g.color = dim ? new Color(0.65f, 0.65f, 0.65f) : Color.white;
        }

        void OnTileTap(TileView t)
        {
            if (Paused || kitchen.ended) return;
            HideHand(); HideCoachStrip(); HideTip();
            lastPlayerTap = AnimationTime;
            if (kitchen.Tap(t.crate.id)) motionUntil = AnimationTime + 0.7f;
        }

        // ================================================================= tutorial (level 1)
        void TutorialNext()
        {
            if (this == null || tutorialStep < 0 || kitchen.ended || ActiveTransition) return;
            var dish = GameData.Dishes[def.newDish ?? def.customers[0][0]];
            if (kitchen.customers[0].orders[0].reserved) return;
            var missing = new List<string>(dish.parts);
            foreach (var slot in kitchen.slots) if (slot.key != null) missing.Remove(slot.key);
            string want = missing.FirstOrDefault();
            var tile = want == null ? null : tiles.Values.FirstOrDefault(x => x != null && x.crate.count > 0 && x.crate.ing == want);
            if (tile == null) return;
            ShowHand(tile.rt, $"Chạm vào {GameData.Ingredients[want].name}");
            Progress.MarkTipSeen("tap_pair");
        }

        void ShowHand(RectTransform target, string tip)
        {
            HideHand();
            handRt = UIKit.Node("Hand", messagesLayer);
            handRt.anchorMin = handRt.anchorMax = new Vector2(0, 1);
            handRt.sizeDelta = new Vector2(150, 150);
            handRt.pivot = new Vector2(0.19f, 0.875f);
            handRt.position = target.position + new Vector3(0.05f, -0.1f, 0) * target.lossyScale.x * 100f;
            var img = handRt.gameObject.AddComponent<Image>(); img.sprite = Art2("hand"); img.raycastTarget = false;
            HideTip(); HideCoachStrip(); HideToast();
            coachRt = MessageCard("Coach", 108);
            var tipBg = UIKit.Sliced("Tip", coachRt, Art2("speech_pink")); tipBg.rectTransform.Stretch();
            MessageText(coachRt, tip);
            // Only the pointer moves. The text stays inside the reserved band throughout its tween.
            handRt.localScale = Vector3.zero;
            var hand = handRt;
            Tween.Scale(hand, Vector3.one, 0.3f).SetEase(Ease.OutCubic).SetUnscaled().OnComplete(() =>
            {
                if (hand && hand == handRt) Tween.Scale(hand, Vector3.one * 0.82f, 0.4f).SetLoops(-1, true).SetEase(Ease.InOutSine).SetUnscaled();
            });
            var tile = target.GetComponent<TileView>();
            if (tile) tile.HintPulse(30f, true);
        }

        void HideHand()
        {
            if (coachRt) { Destroy(coachRt.gameObject); coachRt = null; }
            if (handRt == null) return;
            Tween.KillAll(handRt);
            Destroy(handRt.gameObject); handRt = null;
            foreach (var t in tiles.Values) if (t) { Tween.KillAll(t.glow); UIKit.SetAlpha(t.glow, 0); }
        }

        // ================================================================= loop
        void Update()
        {
            if (kitchen == null) return;
            if (!AnimationsPaused && !kitchen.ended) AnimationTime += Time.deltaTime;
            if (!popupOpen) kitchen.Update(Time.deltaTime);
            int guard = 0;
            while (kitchen.events.Count > 0 && guard++ < 200) Handle(kitchen.events.Dequeue());
            UpdateHud();
            if (Input.GetKeyDown(KeyCode.Escape)) OpenPause();
            if (Input.GetMouseButtonDown(0))
            {
                lastPlayerTap = AnimationTime;
                HideCoachStrip();
                if (handRt && tutorialStep < 0) HideHand();
            }
            if (tipRt != null && Input.GetMouseButtonDown(0) && Time.unscaledTime - tipOpenedAt > 0.25f) HideTip();
            UpdateCoaches();
            if (AnimationsPaused || kitchen.ended) return;
            shineT -= Time.deltaTime;
            if (shineT <= 0 && tiles.Count > 0)
            {
                shineT = Random.Range(1.6f, 3.2f);
                var list = tiles.Values.Where(x => x != null).ToList();
                if (list.Count > 0) list[Random.Range(0, list.Count)].Shine();
            }
        }

        void UpdateHud()
        {
            servedText.text = $"{kitchen.served}/{kitchen.TotalServedTarget}";
            chainText.text = "x" + kitchen.chain;
            { int vipLeft = kitchen.customers.Count(c => c.vip && !c.finished); dayText.text = vipLeft > 0 ? $"Khách VIP: {vipLeft}" : ""; }
        }

        PlateView P(int i) => i >= 0 && i < plates.Length ? plates[i] : null;
        TileView T(int id) => tiles.TryGetValue(id, out var t) && t != null ? t : null;
        CustomerView C(Customer c) => c != null && custViews.TryGetValue(c.id, out var v) && v != null ? v : null;
        static float Sz(float art) => UIKit.S(art);

        /// <summary>Once an ingredient has been through the cutting board it shows its "_cut" sprite (halved bread, sliced meat…).</summary>
        string CutKey(string k, bool uncut)
        {
            if (uncut || !def.NeedsCut(k)) return k;
            return Resources.Load<Sprite>("Food/" + k + "_cut") != null ? k + "_cut" : k;
        }

        void Handle(Ev e)
        {
            switch (e.type)
            {
                case EvType.CrateTap:
                {
                    var t = T(e.crateId);
                    if (t) { t.Jelly(); t.RefreshCount(true); VFX.Sparkle(t.icon.rectTransform.position, 6, 0.8f); VFX.Ring(t.icon.rectTransform.position, Color.white, 190, 0.3f); t.Shine(); }
                    Sfx.PlayVaried("pop", 1f, 0.12f);
                    break;
                }
                case EvType.Fly:
                {
                    Vector3 from;
                    if (e.src == Src.Crate) { var t = T(e.crateId); from = t ? t.icon.rectTransform.position : tilesLayer.position; }
                    else if (e.src == Src.Station)
                    {
                        stations[e.station].FlyCut(P(e.toSlot).ItemWorld);
                        Sfx.PlayVaried("whoosh", 0.55f, 0.1f);
                        break;
                    }
                    else { var p = P(e.slot); from = p.ItemWorld; p.Clear(); }
                    var to = P(e.toSlot).ItemWorld;
                    VFX.FlyIcon(BepJam.Art.Food(e.key), from, to, Sz(118 * plateScale), Kitchen.FLY, null, Sz(150), true);
                    Sfx.PlayVaried("whoosh", 0.55f, 0.1f);
                    break;
                }
                case EvType.Land:
                {
                    var p = P(e.slot); p.Show(CutKey(e.key, e.uncut), false, true); p.ShowUncut(e.uncut); p.Land();
                    Sfx.PlayVaried("land", 0.9f, 0.08f);
                    VFX.Burst(p.plateRt.position, "smoke", 4, new[] { new Color(1, 1, 1, 0.8f) }, 60, 140, 30, 50, 0.45f, 0, 30, 150, 390);
                    break;
                }
                case EvType.Gather: GatherFx(e); break;
                case EvType.Merge: MergeFx(e); break;
                case EvType.Chain:
                    chainText.color = e.value >= 2 ? Pal.Tomato : Pal.TextBrown;
                    if (e.value > 0) Tween.PunchScale(chainBox, Vector3.one * (0.2f + 0.05f * Mathf.Min(e.value, 5)), 0.4f);
                    break;
                case EvType.ToStation:
                {
                    if (e.station == StationType.Board)
                    {
                        var tile = T(e.crateId); var board = stations[e.station];
                        var sourcePlate = e.src == Src.Slot ? P(e.slot) : null;
                        var source = sourcePlate != null ? sourcePlate.ItemWorld : tile ? tile.icon.rectTransform.position : tilesLayer.position;
                        if (sourcePlate != null) sourcePlate.Clear();
                        VFX.FlyIcon(BepJam.Art.Food(e.key), source,
                            board.MouthWorld, Sz(110), Kitchen.FLY, null, Sz(120), true);
                        board.Receive(e.key);
                        Sfx.PlayVaried("whoosh", 0.6f);
                        break;
                    }
                    var p = P(e.slot); var st = stations[e.station];
                    VFX.FlyIcon(BepJam.Art.Food("d_" + e.key), p.ItemWorld, st.MouthWorld, Sz(110), Kitchen.TO_STATION, null, Sz(170), true);
                    p.Clear(); st.Receive(e.key);
                    Sfx.PlayVaried("whoosh", 0.6f);
                    break;
                }
                case EvType.CutStart: stations[StationType.Board].StartCut(e.key, e.value / 1000f); break;
                case EvType.CutDone: stations[StationType.Board].CutDone(); break;
                case EvType.VipLeave:
                {
                    var vip = C(e.customer);
                    if (vip) vip.AngryLeave();
                    Sfx.Play("error");
                    break;
                }
                case EvType.CookStart: stations[e.station].StartCook(e.value / 1000f); break;
                case EvType.CookDone: stations[e.station].Done(); break;
                case EvType.WaitStation:
                    P(e.slot).ShowWait(true, e.station);
                    if (!waitToastShown) { waitToastShown = true; Toast($"{GameData.StationName(e.station)} đang bận – món sẽ chờ trên đĩa."); }
                    break;
                case EvType.Deliver:
                {
                    Vector3 from = e.src == Src.Slot ? P(e.slot).ItemWorld : stations[e.station].BubbleWorld;
                    if (e.src == Src.Slot) P(e.slot).Clear();
                    var cv = C(e.customer);
                    if (cv == null) break;
                    cv.MarkReserved(e.order.index);
                    VFX.FlyIcon(BepJam.Art.Food("d_" + e.key), from, cv.OrderWorld(e.order.index), Sz(105), Kitchen.DELIVER, null, Sz(240), true);
                    Sfx.PlayVaried("whoosh", 0.7f);
                    break;
                }
                case EvType.Delivered:
                {
                    if (tutorialStep >= 0) { tutorialStep = -1; HideHand(); }
                    var cv = C(e.customer);
                    if (cv == null) break;
                    cv.Delivered(e.order.index);
                    var pos = cv.OrderWorld(e.order.index);
                    VFX.Stars(pos, 10, 0.9f);
                    VFX.Ring(pos, Pal.Butter, 200, 0.4f);
                    VFX.FloatingText(pos, "+" + e.value, Pal.Butter, 58, 120, 0.9f);
                    Sfx.Play("serve");
                    VFX.CoinFly(pos, servedBox, Mathf.Clamp(e.value / 5, 2, 5), () => Sfx.PlayVaried("coin", 0.5f, 0.1f));
                    break;
                }
                case EvType.CustomerHappy:
                {
                    var cv = C(e.customer);
                    if (cv) cv.Happy();
                    Sfx.Play("cheer");
                    Tween.PunchScale(servedBox, Vector3.one * 0.3f, 0.45f);
                    VFX.Sparkle(servedBox.position, 10, 1f);
                    break;
                }
                case EvType.CustomerLeave:
                {
                    var cv = C(e.customer);
                    if (cv) cv.Leave(e.value != 0);
                    custViews.Remove(e.customer.id);
                    break;
                }
                case EvType.CustomerArrive:
                {
                    int seat = e.value;
                    float sx = kitchen.seats.Length == 3 ? SeatX[seat] : kitchen.seats.Length == 2 ? (seat == 0 ? 260 : 594) : 426;
                    var seatPos = new Vector2(UIKit.S(sx), -UIKit.S(SeatBottom));
                    var cv = CustomerView.Create(customersLayer, e.customer, seatPos, new Vector2(UIKit.S(CustW), UIKit.S(CustH)));
                    custViews[e.customer.id] = cv;
                    cv.Arrive(seat == 0 || (seat == 1 && e.customer.id % 2 == 0));
                    break;
                }
                case EvType.CrateEmptied:
                {
                    var t = T(e.crateId);
                    if (t == null) break;
                    tiles.Remove(e.crateId);
                    t.onTap = null;
                    t.GetComponent<Image>().raycastTarget = false;
                    VFX.Poof(t.rt.position, 1.1f);
                    VFX.Burst(t.rt.position, "star", 6, new[] { Pal.Butter, Pal.Coral, Pal.Mint }, 200, 420, 22, 40, 0.6f, 800);
                    Tween.KillAll(t.visual); t.visual.localScale = Vector3.one;
                    Tween.Scale(t.rt, RowScale(t.row) * 1.15f, 0.1f).SetEase(Ease.OutQuad)
                         .OnComplete(() => Tween.Scale(t.rt, Vector3.zero, 0.2f).SetEase(Ease.InCubic).OnComplete(() => { if (t) Destroy(t.gameObject); }));
                    Sfx.Play("empty", 0.8f);
                    break;
                }
                case EvType.CrateShift:
                {
                    var t = T(e.crateId);
                    if (t == null) break;
                    t.row = e.row;
                    var target = CellPos(e.col, e.row);
                    Tween.Move(t.rt, target, 0.32f).SetEase(Ease.OutBounce).SetDelay(0.08f);
                    Tween.Scale(t.rt, RowScale(e.row), 0.32f).SetEase(Ease.OutQuad).SetDelay(0.08f);
                    break;
                }
                case EvType.CrateReveal:
                {
                    var t = MakeTile(e.crate, e.col, e.row);
                    var target = t.rt.anchoredPosition;
                    t.rt.anchoredPosition = target + new Vector2(0, UIKit.S(26));
                    t.rt.localScale = new Vector3(0f, 0.6f, 1);
                    Tween.Scale(t.rt, RowScale(e.row), 0.45f).SetEase(Ease.OutBack).SetDelay(0.16f);
                    Tween.Move(t.rt, target, 0.45f).SetEase(Ease.OutBounce).SetDelay(0.16f);
                    Tween.Delay(0.3f, () => { if (t) { VFX.Sparkle(t.rt.position, 8, 1f); Sfx.Play("reveal", 0.8f); } }, owner: t);
                    RefreshStacks(e.col);
                    break;
                }
                case EvType.CrateAdd: { var t = T(e.crateId); if (t) { t.RefreshCount(true); t.Jelly(); } break; }
                case EvType.Return: ReturnFx(e); break;
                case EvType.Hint:
                {
                    var t = T(e.crateId);
                    if (t == null) break;
                    ShowHand(t.rt, "Thử chạm vào đây!");
                    var shownHand = handRt;
                    Tween.Delay(2.6f, () => { if (shownHand && handRt == shownHand && tutorialStep < 0) HideHand(); }, owner: shownHand);
                    Sfx.Play("hint");
                    break;
                }
                case EvType.Stuck:
                    Toast(e.text, 2.6f);
                    Tween.PunchScale(returnBtn.transform, Vector3.one * 0.25f, 0.5f);
                    Tween.Delay(0.6f, () => { if (returnBtn) Tween.PunchScale(returnBtn.transform, Vector3.one * 0.25f, 0.5f); }, owner: this);
                    break;
                case EvType.Blocked:
                {
                    var t = T(e.crateId);
                    if (t) t.Shake();
                    foreach (var p in plates) Tween.Shake(p.plateRt, 8, 0.3f);
                    Toast(e.text);
                    Sfx.Play("error");
                    break;
                }
                case EvType.Win:
                {
                    int stars = e.value;
                    var photo = Album.ForLevel(def.number);
                    bool newPhoto = photo != null && !Album.IsUnlocked(photo);
                    Progress.Complete(def.number, stars);
                    Progress.Coins += kitchen.coins;
                    StopGameplayEffects();
                    popupOpen = true;
                    System.Action showWin = () => Popups.Win(def, stars, kitchen,
                        () => GameBoot.I.PlayLevel(def.number + 1 > GameData.Levels.Count ? def.number : def.number + 1),
                        () => GameBoot.I.PlayLevel(def.number, true),
                        () => GameBoot.I.ShowHome(), newPhoto ? photo : null);
                    Tween.Delay(0.6f, () => showWin(), true, this);
                    break;
                }
                case EvType.Lose:
                    StopGameplayEffects();
                    popupOpen = true;
                    foreach (var cv in custViews.Values) if (cv) cv.SetSad(true);
                    Tween.Delay(0.35f, () => Popups.Lose(def, e.text, kitchen,
                        () => GameBoot.I.PlayLevel(def.number, true),
                        () => GameBoot.I.ShowHome(),
                        () =>
                        {
                            if (!this || !kitchen.Revive()) return;
                            popupOpen = false;
                            kitchen.paused = false;
                            foreach (var cv in custViews.Values) if (cv) cv.SetSad(false);
                            UpdateBoosters();
                            UpdateHud();
                        }), true, this);
                    break;
            }
        }

        void GatherFx(Ev e)
        {
            var target = P(e.slot);
            if (target == null || e.slots == null || e.keys == null) return;
            var customer = C(e.order?.customer);
            if (customer != null) customer.MarkReserved(e.order.index);
            for (int i = 0; i < e.slots.Length && i < e.keys.Length; i++)
            {
                var source = P(e.slots[i]);
                if (source == null) continue;
                Vector3 from = source.ItemWorld, to = target.ItemWorld;
                var icon = BepJam.Art.Food(CutKey(e.keys[i], false));
                // Stagger departures but land every source before the fixed Gather -> Merge beat.
                float delay = Mathf.Min(i * 0.05f, Kitchen.GATHER * 0.5f);
                float arc = Sz(70 + i * 12);
                System.Action launch = () =>
                {
                    if (!this || !source || !target) return;
                    source.Clear();
                    VFX.FlyIcon(icon, from, to, Sz(104 * plateScale), Kitchen.GATHER - delay, () =>
                    {
                        if (!this || !target) return;
                        Tween.Squash(target.plateRt, 0.12f, 0.2f);
                        Sfx.PlayVaried("land", 0.65f, 0.08f);
                    }, arc, true);
                    Sfx.PlayVaried("whoosh", 0.4f, 0.08f);
                };
                if (delay <= 0) launch();
                else Tween.Delay(delay, launch, owner: this);
            }
        }

        void MergeFx(Ev e)
        {
            var p = P(e.slot);
            var dish = GameData.Dishes[e.key];
            var pos = p.ItemWorld;
            // verb-specific cooking animation (pour / sandwich / stir / drop-in)
            Tween.KillAll(p.itemRt);
            p.itemRt.localRotation = Quaternion.identity; p.itemRt.localScale = Vector3.one;
            var parts = (e.text ?? "|").Split('|');
            CookFx.Play(dish, parts[0], parts.Length > 1 ? parts[1] : parts[0], pos, Sz(118 * plateScale), p);
            Toast(dish.verb + "!", 0.9f);
            Haptics.Buzz();
            int chain = Mathf.Clamp(e.value, 1, 5);
            Tween.Delay(0.1f, () => Sfx.Play("combo" + chain, 0.75f), owner: this);
            if (e.value >= 2)
            {
                int n = e.value;
                Tween.Delay(0.25f, () => { if (this) ChainStamp(n); }, owner: this);
            }
        }

        void ReturnFx(Ev e)
        {
            var p = P(e.slot);
            var from = p.ItemWorld;
            p.Clear();
            Vector3 to;
            TileView existing = e.crateId >= 0 ? T(e.crateId) : null;
            if (existing) to = existing.icon.rectTransform.position;
            else if (e.row >= 0) to = tilesLayer.TransformPoint(CellPos(e.col, e.row));
            else to = stacks[e.col].chip.position;
            Sfx.Play("trash");
            VFX.FlyIcon(BepJam.Art.Food(e.key), from, to, Sz(110), Kitchen.FLY, () =>
            {
                if (this == null) return;
                if (existing == null && e.row >= 0 && e.crate != null)
                {
                    var t = MakeTile(e.crate, e.col, e.row);
                    t.rt.localScale = Vector3.zero;
                    Tween.Scale(t.rt, RowScale(e.row), 0.4f).SetEase(Ease.OutBack);
                }
                VFX.Poof(to, 0.8f);
                RefreshStacks(e.col);
            }, Sz(200), true);
        }
    }

    internal sealed class GameplayAnimationScope : MonoBehaviour { public GameView game; }

    /// <summary>Petals and leaves drifting across the lake window – keeps the scene alive.</summary>
    public class AmbientDrift : MonoBehaviour
    {
        RectTransform layer; float t;
        readonly List<(RectTransform rt, Vector2 v, float spin, float life)> parts = new List<(RectTransform, Vector2, float, float)>();
        public void Init(RectTransform l) { layer = l; t = 1f; }
        void Update()
        {
            if (GameView.MotionPaused(this)) return;
            t -= Time.deltaTime;
            if (t <= 0 && parts.Count < 7)
            {
                t = Random.Range(1.2f, 2.8f);
                bool petal = Random.value < 0.6f;
                var img = UIKit.Img("Drift", layer, Art.FX(petal ? "petal" : "leaf"), false, true);
                var rt = (RectTransform)img.transform;
                rt.anchorMin = rt.anchorMax = new Vector2(0, 1);
                float s = Random.Range(26f, 44f);
                rt.sizeDelta = new Vector2(s, s * 0.8f);
                rt.anchoredPosition = new Vector2(-40, -UIKit.S(Random.Range(380f, 760f)));
                img.color = new Color(1, 1, 1, 0.9f);
                parts.Add((rt, new Vector2(Random.Range(70f, 140f), Random.Range(-30f, 10f)), Random.Range(-90f, 90f), 0));
            }
            for (int i = parts.Count - 1; i >= 0; i--)
            {
                var (rt, v, spin, life) = parts[i];
                life += Time.deltaTime;
                if (rt == null) { parts.RemoveAt(i); continue; }
                rt.anchoredPosition += new Vector2(v.x, v.y + Mathf.Sin(life * 2.2f) * 40f) * Time.deltaTime;
                rt.localEulerAngles = new Vector3(0, 0, life * spin);
                parts[i] = (rt, v, spin, life);
                if (rt.anchoredPosition.x > 1140) { Destroy(rt.gameObject); parts.RemoveAt(i); }
            }
        }
    }

    /// <summary>Soft sunlit dust specks floating in the shop window.</summary>
    public class DustMotes : MonoBehaviour
    {
        RectTransform layer; Rect area; float t;
        readonly List<(RectTransform rt, Image img, Vector2 v, float life, float max, float phase)> parts = new List<(RectTransform, Image, Vector2, float, float, float)>();
        public void Init(RectTransform l, Rect canvasArea) { layer = l; area = canvasArea; }
        void Update()
        {
            if (GameView.MotionPaused(this)) return;
            t -= Time.deltaTime;
            if (t <= 0 && parts.Count < 18)
            {
                t = Random.Range(0.15f, 0.4f);
                var img = UIKit.Img("Mote", layer, Art.FX("glow"), false, false);
                var rt = (RectTransform)img.transform;
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
                float s = Random.Range(8f, 20f); rt.sizeDelta = new Vector2(s, s);
                rt.anchoredPosition = new Vector2(Random.Range(area.xMin, area.xMax) - 540f, Random.Range(area.yMin, area.yMin + area.height));
                img.color = new Color(1f, 0.93f, 0.75f, 0f);
                parts.Add((rt, img, new Vector2(Random.Range(-8f, 8f), Random.Range(6f, 18f)), 0, Random.Range(3f, 6f), Random.value * 6f));
            }
            for (int i = parts.Count - 1; i >= 0; i--)
            {
                var p = parts[i];
                if (p.rt == null) { parts.RemoveAt(i); continue; }
                p.life += Time.deltaTime;
                float k = p.life / p.max;
                p.rt.anchoredPosition += new Vector2(p.v.x + Mathf.Sin(p.life * 1.7f + p.phase) * 10f, p.v.y) * Time.deltaTime;
                var c = p.img.color; c.a = Mathf.Sin(Mathf.Clamp01(k) * Mathf.PI) * 0.55f; p.img.color = c;
                parts[i] = p;
                if (k >= 1) { Destroy(p.rt.gameObject); parts.RemoveAt(i); }
            }
        }
    }
}
