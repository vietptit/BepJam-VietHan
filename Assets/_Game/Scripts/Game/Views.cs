using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace BepJam
{
    // =====================================================================================  TILE (crate)
    public class TileView : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerClickHandler, IPointerExitHandler
    {
        public Crate crate; public int col, row;
        public RectTransform rt, visual;
        public Image bg, icon, glow, shine; public Text nameText, countText; public RectTransform countBadge, shineRt;
        public Action<TileView> onTap;
        public static Action<TileView> Held;
        bool pressed, held; float downAt;

        public static TileView Create(Transform parent, Crate c, Vector2 size)
        {
            var rt = UIKit.Node("Tile_" + c.id, parent);
            rt.sizeDelta = size;
            var hit = rt.gameObject.AddComponent<Image>(); hit.color = new Color(1, 1, 1, 0); hit.raycastTarget = true;
            var t = rt.gameObject.AddComponent<TileView>();
            t.rt = rt; t.crate = c;
            t.visual = UIKit.Node("Visual", rt); t.visual.Stretch();
            t.glow = UIKit.Img("Glow", t.visual, Art.FX("glow"), false, false);
            ((RectTransform)t.glow.transform).Stretch(-40, -40, -40, -40);
            t.glow.color = new Color(1f, 0.85f, 0.3f, 0f);
            t.bg = UIKit.Img("Bg", t.visual, Art.UI("tile"), false, true);
            ((RectTransform)t.bg.transform).Stretch();
            var shineMask = UIKit.Node("ShineMask", t.visual); shineMask.Stretch(8, 8, 8, 12);
            shineMask.gameObject.AddComponent<RectMask2D>();
            t.shine = UIKit.Img("Shine", shineMask, Art.FX("shine"), false, false);
            t.shineRt = ((RectTransform)t.shine.transform).Anchor(new Vector2(0, 0.5f), new Vector2(-80, 0), new Vector2(70, size.y * 1.4f));
            t.shine.color = new Color(1, 1, 1, 0.8f);
            t.icon = UIKit.Img("Icon", t.visual, Art.Food(c.ing));
            t.icon.rectTransform.Stretch(24, 24, 24, 24);
            t.nameText = UIKit.Label("Name", t.visual, GameData.Ingredients[c.ing].name, 34, Pal.TextBrown, TextAnchor.MiddleCenter);
            ((RectTransform)t.nameText.transform).Anchor(new Vector2(0.5f, 0), new Vector2(0, size.y * 0.19f), new Vector2(size.x - 16, 40));
            UIKit.Fit(t.nameText, 28, 18);
            t.nameText.enabled = false;   // names shown on press-and-hold instead
            var badge = UIKit.Sliced("Count", t.visual, Art.FX("pill"), Pal.Coral);
            t.countBadge = (RectTransform)badge.transform;
            t.countBadge.Anchor(new Vector2(1, 1), new Vector2(-8, -8), new Vector2(84, 58), new Vector2(1, 1));
            t.countText = UIKit.Label("N", t.countBadge, "x" + c.count, 36, Color.white, TextAnchor.MiddleCenter, 2.5f);
            ((RectTransform)t.countText.transform).Stretch(0, 0, 0, 6);
            UIKit.Fit(t.countText, 36, 18);
            t.RefreshCount(false);
            return t;
        }

        public void RefreshCount(bool punch)
        {
            countBadge.gameObject.SetActive(crate.count > 1);
            countText.text = "x" + crate.count;
            if (punch && crate.count > 1) Tween.PunchScale(countBadge, Vector3.one * 0.35f, 0.35f);
        }

        void Update()
        {
            if (GameView.MotionPaused(this)) return;
            if (pressed && !held && Time.unscaledTime - downAt > 0.4f) { held = true; Held?.Invoke(this); OnPointerUp(null); pressed = false; }
        }

        public void OnPointerDown(PointerEventData e)
        {
            if (GameView.MotionPaused(this)) return;
            held = false; downAt = Time.unscaledTime;
            pressed = true;
            Tween.KillAll(visual);
            Tween.Scale(visual, new Vector3(0.9f, 0.86f, 1), 0.07f).SetEase(Ease.OutQuad);
        }
        public void OnPointerUp(PointerEventData e)
        {
            if (!pressed) return; pressed = false;
            Tween.KillAll(visual);
            Tween.Scale(visual, Vector3.one, 0.4f).SetEase(Ease.OutElastic);
        }
        public void OnPointerExit(PointerEventData e) { if (pressed) OnPointerUp(e); }
        public void OnPointerClick(PointerEventData e) { if (held) { held = false; return; } onTap?.Invoke(this); }

        public void Shine()
        {
            Tween.KillAll(shineRt);
            shineRt.anchoredPosition = new Vector2(-80, 0);
            Tween.Move(shineRt, new Vector2(rt.rect.width + 80, 0), 0.55f).SetEase(Ease.InOutSine);
        }

        public void Jelly() { Tween.KillAll(visual); visual.localScale = Vector3.one; Tween.Squash(visual, 0.22f, 0.4f); }

        public void Shake()
        {
            Tween.Shake(visual, 14, 0.35f);
            bg.color = new Color(1f, 0.6f, 0.55f);
            Tween.Color(bg, Color.white, 0.5f).SetEase(Ease.InQuad);
        }

        public void HintPulse(float duration, bool unscaled = false)
        {
            Tween.KillAll(glow);
            UIKit.SetAlpha(glow, 0);
            Tween.Fade(glow, 0.95f, 0.35f).SetLoops((int)(duration / 0.35f) | 1, true).SetEase(Ease.InOutSine).SetUnscaled(unscaled)
                 .OnComplete(() => UIKit.SetAlpha(glow, 0));
        }
    }

    // =====================================================================================  PLATE
    public class PlateView : MonoBehaviour
    {
        public RectTransform rt, plateRt, itemRt, waitBadge;
        public Image plate, item, waitIcon;
        RectTransform knifeBadge;
        Vector2 itemHome;
        public string key;
        internal RectTransform cookingStory;

        public static PlateView Create(Transform parent, int index)
        {
            var rt = UIKit.Node("Plate_" + index, parent);
            var p = rt.gameObject.AddComponent<PlateView>();
            p.rt = rt;
            p.plate = UIKit.Img("Plate", rt, Art.UI("plate_" + (index % 4)) ?? Art.UI("plate"));
            p.plateRt = (RectTransform)p.plate.transform; p.plateRt.Stretch();
            p.item = UIKit.Img("Item", rt, null);
            p.itemRt = (RectTransform)p.item.transform;
            p.itemRt.Stretch(24, 24, 24, 24);
            p.itemHome = p.itemRt.anchoredPosition;
            p.item.enabled = false;
            var wb = UIKit.Img("Wait", rt, Art.FX("bubble"), false, false);
            p.waitBadge = (RectTransform)wb.transform;
            p.waitBadge.Anchor(new Vector2(1, 1), new Vector2(UIKit.S(-6), UIKit.S(6)), new Vector2(UIKit.S(58), UIKit.S(52)), new Vector2(0.5f, 0.5f));
            p.waitIcon = UIKit.Img("Ic", p.waitBadge, Art.FX("clock"));
            ((RectTransform)p.waitIcon.transform).Stretch(10, 8, 10, 10);
            p.waitBadge.gameObject.SetActive(false);
            var rawBadge = UIKit.Img("Uncut", rt, Art.FX("knife"));
            p.knifeBadge = rawBadge.rectTransform;
            p.knifeBadge.Anchor(new Vector2(1, 1), new Vector2(UIKit.S(-8), UIKit.S(8)), new Vector2(UIKit.S(36), UIKit.S(36)));
            p.ShowUncut(false);
            return p;
        }

        public Vector3 ItemWorld => itemRt.position;

        public void Show(string k, bool dish, bool pop)
        {
            key = k;
            item.sprite = dish ? Art.Food("d_" + k) : Art.Food(k);
            item.enabled = true; item.type = Image.Type.Simple; item.fillAmount = 1f;
            Tween.KillAll(itemRt);
            itemRt.anchoredPosition = itemHome;
            itemRt.localRotation = Quaternion.identity;
            if (pop) { itemRt.localScale = Vector3.zero; Tween.Scale(itemRt, Vector3.one, 0.45f).SetEase(Ease.OutBack); }
            else itemRt.localScale = Vector3.one;
        }

        public void StopCookingStory()
        {
            if (!cookingStory) return;
            Tween.KillHierarchy(cookingStory);
            Destroy(cookingStory.gameObject); cookingStory = null;
        }

        public void StopEffects()
        {
            StopCookingStory();
            Tween.KillHierarchy(rt);
            plateRt.localScale = Vector3.one;
            itemRt.localScale = Vector3.one; itemRt.localRotation = Quaternion.identity;
            ShowWait(false, StationType.None);
        }
        void OnDisable() => StopEffects();

        public void Clear()
        {
            StopCookingStory();
            ShowUncut(false);
            key = null; item.enabled = false; Tween.KillAll(itemRt); itemRt.localScale = Vector3.one;
            itemRt.anchoredPosition = itemHome; itemRt.localRotation = Quaternion.identity;
            ShowWait(false, StationType.None);
        }

        public void ShowUncut(bool on) => knifeBadge.gameObject.SetActive(on);

        public void Land()
        {
            Tween.Squash(plateRt, 0.18f, 0.35f);
            Tween.Squash(itemRt, 0.25f, 0.4f);
        }

        public void ShowWait(bool on, StationType st)
        {
            waitBadge.gameObject.SetActive(on);
            Tween.KillAll(waitBadge);
            if (on)
            {
                waitBadge.localScale = Vector3.zero;
                Tween.Scale(waitBadge, Vector3.one, 0.35f).SetEase(Ease.OutBack)
                     .OnComplete(() => Tween.Scale(waitBadge, Vector3.one * 1.12f, 0.45f).SetLoops(-1, true).SetEase(Ease.InOutSine));
            }
        }
    }

    // =====================================================================================  STATION (stove / oven)
    public class StationView : MonoBehaviour
    {
        public StationType type;
        public RectTransform rt, visual, bubble, ringRt;
        public Image art, glow, ringBg, ring, dishIcon, doneCheck;
        public string dish;
        float cookT, cookTotal, steamClock, bubbleClock; bool cooking, loopPaused;
        int cookCycle;
        Image[] flames; Image windowGlow;
        AudioSource loop;
        Image cutIcon, knife, cutLine; Image[] halves; bool breadSplit;
        RectTransform boardOverlay;
        float cutT, cutDuration; int chopCount; bool cutting;

        public static StationView Create(Transform parent, StationType type, Sprite sprite)
        {
            var rt = UIKit.Node(type.ToString(), parent);
            var s = rt.gameObject.AddComponent<StationView>();
            s.type = type; s.rt = rt;
            s.glow = UIKit.Img("Glow", rt, Art.FX("glow"), false, false);
            ((RectTransform)s.glow.transform).Stretch(-60, -40, -60, -20);
            s.glow.color = type == StationType.Stove ? new Color(1f, 0.55f, 0.2f, 0) : new Color(1f, 0.9f, 0.4f, 0);
            // A soft contact shadow stays still while the appliance animates above it.
            var shadow = UIKit.Img("ContactShadow", rt, Art.FX("glow"), false, false);
            shadow.color = new Color(0.12f, 0.08f, 0.06f, 0.25f);
            shadow.rectTransform.anchorMin = new Vector2(0.05f, 0);
            shadow.rectTransform.anchorMax = new Vector2(0.95f, 0.18f);
            shadow.rectTransform.offsetMin = new Vector2(0, -UIKit.S(10));
            shadow.rectTransform.offsetMax = new Vector2(0, -UIKit.S(10));
            s.visual = UIKit.Node("Visual", rt); s.visual.Stretch();
            s.visual.pivot = new Vector2(0.5f, 0f);
            s.visual.anchoredPosition = new Vector2(0, 0);
            s.art = UIKit.Img("Art", s.visual, sprite);
            s.art.rectTransform.Stretch();
            s.art.type = Image.Type.Simple;
            s.art.preserveAspect = true;
            if (sprite != null)
            {
                var aspect = s.art.gameObject.AddComponent<AspectRatioFitter>();
                aspect.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
                aspect.aspectRatio = sprite.rect.width / Mathf.Max(1, sprite.rect.height);
            }
            if (type == StationType.Board)
            {
                s.BuildBoard();
                return s;
            }
            // bubble above with dish + radial timer
            s.bubble = UIKit.Node("Bubble", rt);
            s.bubble.Anchor(new Vector2(0.5f, 1f), new Vector2(0, UIKit.S(48)), new Vector2(UIKit.S(92), UIKit.S(92)));
            s.ringBg = UIKit.Img("RingBg", s.bubble, Art.FX("circle"), false, false);
            ((RectTransform)s.ringBg.transform).Stretch(-8, -8, -8, -8);
            s.ringBg.color = Pal.Outline;
            var inner = UIKit.Img("Inner", s.bubble, Art.FX("circle"), false, false);
            ((RectTransform)inner.transform).Stretch(6, 6, 6, 6);
            inner.color = Pal.Cream;
            s.ring = UIKit.Img("Ring", s.bubble, Art.FX("ring"), false, false);
            s.ringRt = (RectTransform)s.ring.transform; s.ringRt.Stretch(-4, -4, -4, -4);
            s.ring.type = Image.Type.Filled; s.ring.fillMethod = Image.FillMethod.Radial360; s.ring.fillOrigin = 2; s.ring.fillClockwise = true;
            s.ring.color = Pal.Leaf;
            s.dishIcon = UIKit.Img("Dish", s.bubble, null);
            ((RectTransform)s.dishIcon.transform).Stretch(14, 14, 14, 14);
            s.doneCheck = UIKit.Img("Check", s.bubble, Art.FX("check"));
            ((RectTransform)s.doneCheck.transform).Anchor(new Vector2(1, 0), new Vector2(-6, 6), new Vector2(54, 54));
            s.doneCheck.enabled = false;
            s.bubble.gameObject.SetActive(false);
            return s;
        }

        public Vector3 MouthWorld => type == StationType.Board ? cutIcon.rectTransform.position
            : art.rectTransform.position + Vector3.up * (art.rectTransform.rect.height * 0.1f * art.rectTransform.lossyScale.y);
        public Vector3 BubbleWorld => bubble.position;

        public void Receive(string d)
        {
            dish = d;
            if (type == StationType.Board) return;
            int cycle = cookCycle;
            Tween.Delay(Kitchen.TO_STATION * 0.9f, () =>
            {
                if (this == null || cycle != cookCycle) return;
                Tween.Squash(visual, 0.2f, 0.4f);
                VFX.Poof(MouthWorld, 0.7f);
                Sfx.Play("cook_start", 0.8f);
            }, owner: this);
        }

        void BuildBoard()
        {
            boardOverlay = UIKit.Node("BoardOverlay", rt); boardOverlay.Stretch();
            cutIcon = UIKit.Img("Ingredient", boardOverlay, null);
            cutIcon.rectTransform.Anchor(new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(UIKit.S(86), UIKit.S(78)));
            cutIcon.enabled = false;
            halves = new Image[2];
            for (int i = 0; i < 2; i++)
            {
                halves[i] = UIKit.Img("Half" + i, boardOverlay, null);
                halves[i].rectTransform.Anchor(new Vector2(0.5f, 0.5f), Vector2.zero, cutIcon.rectTransform.sizeDelta);
                halves[i].type = Image.Type.Filled; halves[i].fillMethod = Image.FillMethod.Horizontal;
                halves[i].fillOrigin = i; halves[i].fillAmount = 0.5f; halves[i].enabled = false;
            }
            cutLine = UIKit.Img("CutLine", boardOverlay, Art.FX("rounded"));
            cutLine.color = Pal.Hex("FFF3D6");
            cutLine.rectTransform.Anchor(new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(0, UIKit.S(5)));
            cutLine.rectTransform.pivot = new Vector2(1, 0.5f);
            cutLine.enabled = false;
            knife = UIKit.Img("Knife", boardOverlay, Art.FX("knife"));
            knife.rectTransform.Anchor(new Vector2(0.5f, 0.5f), new Vector2(UIKit.S(18), UIKit.S(60)), new Vector2(UIKit.S(170), UIKit.S(170)));
            knife.enabled = false;
        }

        public void RaiseFX(RectTransform layer)
        {
            if (bubble != null) bubble.SetParent(layer, true);
            if (boardOverlay != null)
            {
                // Freeze the stretched rect before changing its parent to the full-screen layer.
                var size = boardOverlay.rect.size;
                var position = boardOverlay.position;
                boardOverlay.SetParent(layer, true);
                boardOverlay.anchorMin = boardOverlay.anchorMax = new Vector2(0.5f, 0.5f);
                boardOverlay.sizeDelta = size;
                boardOverlay.position = position;
            }
        }

        public void StartCut(string ingredient, float duration)
        {
            Tween.KillAll(cutIcon.rectTransform);
            dish = ingredient; cutting = true; cutT = 0; cutDuration = Mathf.Max(0.01f, duration); chopCount = 0;
            cutIcon.sprite = Art.Food(ingredient); cutIcon.enabled = true;
            cutIcon.rectTransform.localScale = Vector3.one;
            knife.enabled = true;
            cutLine.enabled = false; splitShown = false;
            foreach (var half in halves) { Tween.KillAll(half.rectTransform); half.enabled = false; }
        }

        void TickCut(float dt)
        {
            if (!cutting) return;
            cutT += dt;
            float k = Mathf.Clamp01(cutT / cutDuration);
            bool bread = dish == "banh_mi";
            int chops = bread ? 1 : 6;                      // bread: one long slice · meat/veg: many chops
            float squash;
            int hits;
            if (bread)
            {
                // instant slash along the loaf's diagonal (upper-right -> lower-left), white streak flashes then fades
                Vector2 dir = new Vector2(Mathf.Cos(BreadAngle * Mathf.Deg2Rad), Mathf.Sin(BreadAngle * Mathf.Deg2Rad));
                Vector2 perp = new Vector2(-dir.y, dir.x);
                float stroke = 1 - Mathf.Pow(1 - Mathf.InverseLerp(0.02f, 0.15f, cutT), 3);   // ~0.13 s, very sharp
                float tipX = Mathf.Lerp(70, -75, stroke);
                Vector2 kp = dir * (tipX + 70) + perp * 4;
                knife.rectTransform.anchoredPosition = new Vector2(UIKit.S(kp.x), UIKit.S(kp.y));
                knife.rectTransform.localRotation = Quaternion.Euler(0, 0, BreadAngle - stroke * 8);
                knife.enabled = cutT < 0.24f;
                float fade = 1 - Mathf.InverseLerp(0.15f, 0.32f, cutT);
                cutLine.enabled = stroke > 0.01f && fade > 0;
                Vector2 rp = dir * 50 + perp * 1;
                cutLine.rectTransform.anchoredPosition = new Vector2(UIKit.S(rp.x), UIKit.S(rp.y));
                cutLine.rectTransform.localRotation = Quaternion.Euler(0, 0, BreadAngle);
                cutLine.rectTransform.sizeDelta = new Vector2(UIKit.S(Mathf.Clamp(50 - tipX, 0, 105)), UIKit.S(3 + 9 * fade));
                cutLine.color = new Color(1, 0.98f, 0.9f, fade);
                // tiny hit-stop: the loaf squashes hard right as the blade passes, then settles
                squash = stroke > 0.01f ? 0.35f * fade : 0;
                hits = stroke > 0.01f ? 1 : 0;
                if (cutT >= 0.17f && !splitShown) SplitVisual();   // halves pop apart right after the blade passes
            }
            else
            {
                float cycles = k * chops;
                float height = Mathf.Abs(Mathf.Cos(cycles * Mathf.PI));
                // the knife steps along the ingredient, one slice per chop
                float x = Mathf.Lerp(-55, 75, Mathf.Clamp01(cycles / chops));
                knife.rectTransform.anchoredPosition = new Vector2(UIKit.S(x), UIKit.S(36 + height * 58));
                knife.rectTransform.localRotation = Quaternion.Euler(0, 0, -10 + height * 14);
                squash = 1 - height;
                hits = Mathf.Min(chops, Mathf.FloorToInt(cycles + 0.5f));
            }
            cutIcon.rectTransform.localScale = new Vector3(1 + squash * 0.16f, 1 - squash * 0.22f, 1);
            while (chopCount < hits)
            {
                chopCount++;
                Sfx.PlayVaried(dish == "banh_mi" ? "whoosh" : "pop", dish == "banh_mi" ? 0.4f : 0.6f, 0.1f);
                VFX.Burst(MouthWorld, "circle", dish == "banh_mi" ? 4 : 5, new[] { Pal.Hex("E7CCA1") }, 60, 150, 7, 14, 0.35f, 300);
            }
        }

        public void CutDone()
        {
            TickCut(cutDuration - cutT); // Include the final chopping beat if the model ticks first.
            cutting = false; knife.enabled = false; cutLine.enabled = false;
            if (!splitShown) SplitVisual();
        }

        bool splitShown;
        void SplitVisual()
        {
            splitShown = true; cutIcon.enabled = false;
            // bread: real top/bottom halves, the lid lifts off; others: split left/right
            var topSpr = Resources.Load<Sprite>("Food/banh_mi_top"); var botSpr = Resources.Load<Sprite>("Food/banh_mi_bot");
            breadSplit = dish == "banh_mi" && topSpr != null && botSpr != null;
            var full = cutIcon.rectTransform.sizeDelta;
            for (int i = 0; i < halves.Length; i++)
            {
                var half = halves[i]; Tween.KillAll(half.rectTransform);
                half.enabled = true;
                half.rectTransform.localScale = Vector3.one;
                half.rectTransform.localRotation = Quaternion.identity;
                if (breadSplit)
                {
                    half.sprite = i == 0 ? topSpr : botSpr; half.type = Image.Type.Simple;
                    half.rectTransform.sizeDelta = new Vector2(full.x * 1.05f, full.x * 1.05f / 2.35f);
                    half.rectTransform.anchoredPosition = Rot(new Vector2(0, UIKit.S(i == 0 ? 9 : -9)));
                    half.rectTransform.localRotation = Quaternion.Euler(0, 0, BreadAngle);
                }
                else
                {
                    half.sprite = Art.Food(dish); half.type = Image.Type.Filled;
                    half.rectTransform.sizeDelta = full;
                    half.rectTransform.anchoredPosition = Vector2.zero;
                }
                Tween.Move(half.rectTransform, SplitPos(i), 0.12f).SetEase(Ease.OutBack);
            }
        }

        const float BreadAngle = 28f;   // the bánh mì sprite lies diagonally on the board
        static Vector2 Rot(Vector2 v) { float r = BreadAngle * Mathf.Deg2Rad, c = Mathf.Cos(r), s = Mathf.Sin(r); return new Vector2(v.x * c - v.y * s, v.x * s + v.y * c); }
        Vector2 SplitPos(int i) => breadSplit
            ? Rot(new Vector2(UIKit.S(i == 0 ? -4 : 2), UIKit.S(i == 0 ? 26 : -12)))
            : new Vector2(UIKit.S(i == 0 ? -14 : 14), 0);

        public void FlyCut(Vector3 target)
        {
            // Disposable copies cannot be overwritten by the board's next cutting cycle.
            for (int i = 0; i < halves.Length; i++)
            {
                var half = halves[i];
                var h = UIKit.Img("CutFlyer", boardOverlay, half.sprite);
                h.type = half.type; h.fillMethod = half.fillMethod; h.fillOrigin = half.fillOrigin; h.fillAmount = half.fillAmount;
                var hr = h.rectTransform;
                hr.sizeDelta = half.rectTransform.sizeDelta;
                hr.anchoredPosition = half.rectTransform.anchoredPosition;
                hr.localRotation = half.rectTransform.localRotation;
                half.enabled = false; Tween.KillAll(half.rectTransform);
                var start = hr.anchoredPosition; var split = SplitPos(i);
                var destination = (Vector2)boardOverlay.InverseTransformPoint(target);
                Tween.Float(0, 1, Kitchen.FLY, t =>
                {
                    if (t < 0.25f) hr.anchoredPosition = Vector2.Lerp(start, split, t * 4f);
                    else
                    {
                        float p = (t - 0.25f) / 0.75f;
                        hr.anchoredPosition = Vector2.Lerp(split, destination, p) + Vector2.up * (Mathf.Sin(p * Mathf.PI) * UIKit.S(80));
                    }
                }, hr).OnComplete(() => { if (h) Destroy(h.gameObject); });
            }
            cutIcon.enabled = false; knife.enabled = false; cutLine.enabled = false;
        }

        public void StartCook(float duration)
        {
            cookCycle++;
            Tween.KillAll(bubble);
            cooking = true; cookT = 0; cookTotal = Mathf.Max(0.01f, duration);
            bubble.gameObject.SetActive(true);
            dishIcon.sprite = Art.Food("d_" + dish);
            dishIcon.color = new Color(0.55f, 0.5f, 0.5f, 1);
            doneCheck.enabled = false;
            ring.fillAmount = 0; ring.color = Pal.Leaf;
            bubble.localScale = Vector3.zero;
            Tween.Scale(bubble, Vector3.one, 0.4f).SetEase(Ease.OutBack);
            Tween.KillAll(glow);
            Tween.Fade(glow, 0.55f, 0.3f).OnComplete(() => Tween.Fade(glow, 0.25f, 0.5f).SetLoops(-1, true).SetEase(Ease.InOutSine));
            if (loop) AudioManager.I.StopLoop(loop, 0f);
            loop = AudioManager.I.StartLoop("sizzle", 0.35f); loopPaused = false;
            if (type == StationType.Stove && flames == null)
            {
                flames = new Image[3];
                for (int i = 0; i < 3; i++)
                {
                    var f = UIKit.Img("Flame" + i, visual, Art.FX("glow"), false, false); flames[i] = f;
                    ((RectTransform)f.transform).Anchor(new Vector2(0.38f + i * 0.12f, 0.36f), Vector2.zero, new Vector2(60, 80));
                    f.color = new Color(1f, 0.55f, 0.15f, 0.9f);
                    f.transform.SetSiblingIndex(0);
                }
            }
            if (type == StationType.Oven && windowGlow == null)
            {
                windowGlow = UIKit.Img("WindowGlow", visual, Art.FX("rounded"), false, false);
                ((RectTransform)windowGlow.transform).Anchor(new Vector2(0.4f, 0.5f), new Vector2(0, 4), new Vector2(rt.rect.width * 0.5f, rt.rect.height * 0.5f));
                windowGlow.color = new Color(1f, 0.6f, 0.2f, 0f);
            }
            if (flames != null) foreach (var f in flames) if (f) f.enabled = true;
        }

        public void Done()
        {
            cooking = false;
            int cycle = cookCycle;
            if (loop != null) { AudioManager.I.StopLoop(loop); loop = null; }
            if (flames != null) foreach (var f in flames) if (f) f.enabled = false;
            if (windowGlow != null) UIKit.SetAlpha(windowGlow, 0);
            Sfx.Play("ding");
            ring.fillAmount = 1; ring.color = Pal.Butter;
            dishIcon.color = Color.white;
            doneCheck.enabled = true;
            Tween.PunchScale(bubble, Vector3.one * 0.35f, 0.4f);
            Tween.KillAll(visual); visual.localScale = Vector3.one; visual.localRotation = Quaternion.identity;
            Tween.Squash(visual, 0.28f, 0.45f);
            VFX.Flash(MouthWorld, 280, 0.35f);
            VFX.Sparkle(BubbleWorld, 12, 1.1f);
            var game = GameView.AnimationOwner(transform);
            if (game) game.Toast("Xong!", 0.8f);
            Tween.KillAll(glow);
            Tween.Fade(glow, 0f, 0.6f);
            Tween.Delay(0.25f, () =>
            {
                if (!this || cycle != cookCycle || cooking || !bubble) return;
                Tween.KillAll(bubble);
                Tween.Scale(bubble, Vector3.zero, 0.25f).SetEase(Ease.InBack).OnComplete(() =>
                { if (bubble && cycle == cookCycle && !cooking) bubble.gameObject.SetActive(false); });
            }, owner: this);
        }

        float idleSteam = 1f;
        void Update()
        {
            if (GameView.MotionPaused(this))
            {
                if (loop && !loopPaused) { loop.Pause(); loopPaused = true; }
                return;
            }
            if (loop && loopPaused) { loop.UnPause(); loopPaused = false; }
            if (type == StationType.Board)
            {
                var boardGame = GameBoot.I != null ? GameBoot.I.CurrentGame : null;
                if (boardGame == null || (!boardGame.Paused && !boardGame.kitchen.ended)) TickCut(Time.deltaTime);
                return;
            }
            if (!cooking)
            {
                // the enamel pot keeps simmering softly even when idle
                if (type == StationType.Stove && art != null)
                {
                    idleSteam -= Time.deltaTime;
                    if (idleSteam <= 0) { idleSteam = UnityEngine.Random.Range(1.2f, 2.2f); VFX.Steam(art.rectTransform.position + Vector3.up * (art.rectTransform.rect.height * 0.42f * art.rectTransform.lossyScale.y), 0.55f); }
                }
                return;
            }
            var game = GameBoot.I != null ? GameBoot.I.CurrentGame : null;
            if (game != null && game.Paused) return;
            cookT += Time.deltaTime;
            ring.fillAmount = Mathf.Clamp01(cookT / cookTotal);
            float wob = Mathf.Sin((GameView.AnimationOwner(transform)?.AnimationTime ?? Time.time) * 28f) * 2.2f;
            visual.localRotation = Quaternion.Euler(0, 0, wob);
            visual.localScale = new Vector3(1 + Mathf.Sin((GameView.AnimationOwner(transform)?.AnimationTime ?? Time.time) * 17f) * 0.015f, 1 - Mathf.Sin((GameView.AnimationOwner(transform)?.AnimationTime ?? Time.time) * 17f) * 0.02f, 1);
            if (flames != null)
                for (int i = 0; i < flames.Length; i++)
                {
                    float fl = 0.8f + Mathf.PerlinNoise((GameView.AnimationOwner(transform)?.AnimationTime ?? Time.time) * 9f, i * 3.1f) * 0.6f;
                    if (flames[i]) flames[i].rectTransform.localScale = new Vector3(0.8f + 0.2f * fl, fl, 1);
                }
            if (windowGlow != null) UIKit.SetAlpha(windowGlow, 0.35f + Mathf.Sin((GameView.AnimationOwner(transform)?.AnimationTime ?? Time.time) * 7f) * 0.2f);
            bubbleClock -= Time.deltaTime;
            if (bubbleClock <= 0 && type == StationType.Stove)
            {
                bubbleClock = 0.12f;
                var top = art.rectTransform.position + Vector3.up * (art.rectTransform.rect.height * 0.3f * art.rectTransform.lossyScale.y);
                VFX.Burst(top + Vector3.right * UnityEngine.Random.Range(-0.25f, 0.25f) * art.rectTransform.rect.width * art.rectTransform.lossyScale.x * 0.5f,
                          "ring", 1, new[] { new Color(1, 1, 1, 0.9f) }, 40, 90, 14, 26, 0.45f, -200f, 0, 80, 100);
            }
            steamClock -= Time.deltaTime;
            if (steamClock <= 0)
            {
                steamClock = 0.28f;
                var p = art.rectTransform.position + Vector3.up * (art.rectTransform.rect.height * 0.45f * art.rectTransform.lossyScale.y);
                VFX.Steam(p, type == StationType.Stove ? 1.1f : 0.8f);
            }
        }

        public void StopEffects()
        {
            cookCycle++; cooking = cutting = false;
            Tween.KillHierarchy(rt);
            if (loop && AudioManager.I) AudioManager.I.StopLoop(loop, 0f);
            loop = null; loopPaused = false;
            if (bubble) { Tween.KillHierarchy(bubble); bubble.gameObject.SetActive(false); }
            if (boardOverlay)
            {
                Tween.KillHierarchy(boardOverlay);
                foreach (Transform child in boardOverlay)
                    if (child.name == "CutFlyer") { child.gameObject.SetActive(false); Destroy(child.gameObject); }
            }
            if (knife) knife.enabled = false;
            if (cutLine) cutLine.enabled = false;
            if (cutIcon) cutIcon.enabled = false;
            if (halves != null) foreach (var half in halves) if (half) half.enabled = false;
            if (glow) UIKit.SetAlpha(glow, 0);
            if (windowGlow) UIKit.SetAlpha(windowGlow, 0);
            if (flames != null) foreach (var f in flames) if (f) f.enabled = false;
            if (visual) { visual.localRotation = Quaternion.identity; visual.localScale = Vector3.one; }
        }
        void OnDisable() => StopEffects();
        void OnDestroy()
        {
            StopEffects();
            if (bubble) Destroy(bubble.gameObject);
            if (boardOverlay) Destroy(boardOverlay.gameObject);
        }
    }

    // =====================================================================================  CUSTOMER
    public class ClickRelay : MonoBehaviour, IPointerClickHandler
    {
        public Action onClick;
        public void OnPointerClick(PointerEventData e) => onClick?.Invoke();
    }

    public class CustomerView : MonoBehaviour
    {
        public static Action<CustomerView, int> OrderTapped;
        public Customer customer;
        public RectTransform rt, visual, headPivot, bubble, bubbleTail;
        public Image body, head, eyes, mouth, blush;
        public readonly List<RectTransform> orderSlots = new List<RectTransform>();
        public readonly List<Image> orderIcons = new List<Image>();
        public readonly List<Image> orderChecks = new List<Image>();
        Text bubbleText;
        RectTransform patienceBar; Image patienceFill, vipHalo;
        float vipSparkClock = 1.2f, vipPulse;
        bool vipArrived;
        string id; float blinkT; bool happy, sad, leaving;
        Vector2 seatPos;

        public static CustomerView Create(Transform parent, Customer c, Vector2 seatPos, Vector2 size)
        {
            var rt = UIKit.Node("Customer_" + c.id, parent);
            rt.anchorMin = rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.sizeDelta = size;
            var v = rt.gameObject.AddComponent<CustomerView>();
            v.customer = c; v.rt = rt; v.id = c.charId; v.seatPos = seatPos;
            v.visual = UIKit.Node("Visual", rt); v.visual.Stretch();
            v.visual.pivot = new Vector2(0.5f, 0f);
            v.single = c.charId.StartsWith("c_");
            if (v.single)
            {
                // one illustrated bust (Venba style): breathes and sways as a whole
                var inner = UIKit.Node("Bust", v.visual); inner.Stretch(); inner.pivot = new Vector2(0.5f, 0f);
                v.body = UIKit.Img("bust", inner, Art.Char(c.charId), false, true);
                var brt = (RectTransform)v.body.transform; brt.Stretch(); brt.pivot = new Vector2(0.5f, 0f);
                v.body.preserveAspect = true;
                var bi = Idle.Add(inner, breathe: 0.02f, sway: 1.6f, speed: 0.9f); bi.phase = c.id * 1.3f;
                v.headPivot = UIKit.Node("Head", v.visual); v.headPivot.Stretch();
                v.head = v.Dummy(); v.blush = v.Dummy(); v.eyes = v.Dummy(); v.mouth = v.Dummy();
                v.BuildBubble(size);
                if (c.charId.EndsWith("boy") || c.charId.EndsWith("girl")) rt.localScale = Vector3.one * 0.92f;   // kids are a bit smaller
                return v;
            }
            v.body = v.Layer("body", v.visual);
            // head group rotates around the neck (normalized 0.5, 0.36 from bottom)
            v.headPivot = UIKit.Node("Head", v.visual);
            v.headPivot.anchorMin = v.headPivot.anchorMax = new Vector2(0.5f, 0.36f);
            v.headPivot.pivot = new Vector2(0.5f, 0.36f);
            v.headPivot.sizeDelta = size; v.headPivot.anchoredPosition = Vector2.zero;
            v.head = v.Layer("head", v.headPivot);
            v.blush = v.Layer("blush", v.headPivot);
            v.eyes = v.Layer("eyes_open", v.headPivot);
            v.mouth = v.Layer("mouth_idle", v.headPivot);
            var idle = Idle.Add(v.headPivot, sway: 3.5f, speed: 0.9f);
            idle.phase = c.id * 1.3f;
            var breathe = Idle.Add(v.body, breathe: 0.025f, speed: 1.1f);
            breathe.phase = c.id;
            v.blinkT = UnityEngine.Random.Range(1.5f, 4f);
            v.BuildBubble(size);
            return v;
        }

        bool single;
        Image Dummy() { var d = UIKit.Img("dummy", headPivot, null, false, false); d.enabled = false; return d; }

        Image Layer(string layer, Transform parent)
        {
            var img = UIKit.Img(layer, parent, Art.Char(id + "_" + layer), false, false);
            ((RectTransform)img.transform).Stretch();
            return img;
        }

        void BuildBubble(Vector2 size)
        {
            int n = customer.orders.Count;
            float pad = UIKit.S(10), gap = UIKit.S(8);
            float icon = Mathf.Min(UIKit.S(99), (size.x - pad * 2 - Mathf.Max(0, n - 1) * gap) / Mathf.Max(1, n));
            float w = n * icon + (n - 1) * gap + pad * 2, h = icon + pad * 2;
            bubble = UIKit.Node("Bubble", rt);
            // Enlarged cards fit the seat width; leave space above for VIP patience below the HUD.
            bubble.Anchor(new Vector2(0.5f, 1f), new Vector2(0, UIKit.S(6)), new Vector2(w, h), new Vector2(0.5f, 0f));
            var bg = UIKit.Sliced("Bg", bubble, Art.FX("bubble")); ((RectTransform)bg.transform).Stretch();
            var tail = UIKit.Img("Tail", bubble, Art.FX("bubble_tail"));
            bubbleTail = (RectTransform)tail.transform;
            bubbleTail.Anchor(new Vector2(0.5f, 0f), new Vector2(0, -UIKit.S(11)), new Vector2(UIKit.S(32), UIKit.S(26)));
            bubbleTail.localEulerAngles = Vector3.zero;
            for (int i = 0; i < n; i++)
            {
                var slot = UIKit.Node("Order" + i, bubble);
                slot.Anchor(new Vector2(0, 0.5f), new Vector2(pad + icon * 0.5f + i * (icon + gap), UIKit.S(2)), new Vector2(icon, icon));
                var ic = UIKit.Img("Icon", slot, Art.Food("d_" + customer.orders[i].dish));
                ((RectTransform)ic.transform).Stretch();
                Idle.Add(ic, bob: 3, speed: 1.2f).phase = i * 1.7f;
                var hit = slot.gameObject.AddComponent<Image>(); hit.color = new Color(1, 1, 1, 0); hit.raycastTarget = true;
                int oi = i;
                slot.gameObject.AddComponent<ClickRelay>().onClick = () => OrderTapped?.Invoke(this, oi);
                var chk = UIKit.Img("Check", slot, Art.FX("check"));
                ((RectTransform)chk.transform).Anchor(new Vector2(1, 0), new Vector2(-2, 4), new Vector2(UIKit.S(44), UIKit.S(44)));
                chk.enabled = false;
                orderSlots.Add(slot); orderIcons.Add(ic); orderChecks.Add(chk);
            }
            bubbleText = UIKit.Label("Text", bubble, "", 44, Pal.Coral, TextAnchor.MiddleCenter, 0);
            ((RectTransform)bubbleText.transform).Stretch(8, 8, 8, 14);
            UIKit.Fit(bubbleText, 44, 20);
            bubbleText.enabled = false;
            if (customer.vip) BuildVip(w);
        }

        void BuildVip(float bubbleWidth)
        {
            // The illustrated bust includes the head; keep its ornaments inside this seat's bounds.
            float headWidth = rt.sizeDelta.x * 0.56f;
            var haloSprite = Art.FX("vip_halo");
            if (haloSprite)
            {
                vipHalo = UIKit.Img("VipHalo", rt, haloSprite);
                // The rotating square's diagonal also stays within one seat and below its order card.
                vipHalo.rectTransform.Anchor(new Vector2(0.5f, 0.50f), Vector2.zero, Vector2.one * Mathf.Min(rt.sizeDelta.x * 0.72f, rt.sizeDelta.y * 0.72f));
                vipHalo.rectTransform.SetAsFirstSibling();
                vipHalo.color = new Color(1, 0.86f, 0.42f, 1f);
                vipHalo.gameObject.AddComponent<Spin>().speed = 9;
            }
            var crownSprite = Art.FX("vip_crown");
            // Ornaments use the seat frame, so a happy bust's squash/hop cannot push them into the order row.
            var crown = UIKit.Img("Crown", rt, crownSprite ? crownSprite : Art.FX("star_full"));
            float crownSize = crownSprite ? headWidth * 0.72f : UIKit.S(60);
            crown.rectTransform.Anchor(new Vector2(0.5f, 0.82f), Vector2.zero, new Vector2(crownSize, crownSize * 0.69f));
            crown.rectTransform.localRotation = Quaternion.Euler(0, 0, -6);
            if (!crownSprite) crown.color = Pal.Butter;
            Idle.Add(crown, bob: UIKit.S(2), speed: 0.65f);
            var ribbon = Art.FX("vip_ribbon");
            var badge = UIKit.Sliced("VIP", rt, ribbon ? ribbon : Art.FX("hud_bar"), ribbon ? Color.white : Pal.Hex("E6B841"));
            var badgeRt = badge.rectTransform.Anchor(new Vector2(0.5f, 0.20f), Vector2.zero, new Vector2(UIKit.S(150), UIKit.S(54)));
            badge.type = Image.Type.Simple;
            var label = UIKit.Label("Text", badgeRt, "VIP", 34, Pal.Hex("5A2E14"));
            label.fontStyle = FontStyle.Bold;
            label.rectTransform.Stretch(UIKit.S(30), UIKit.S(8), UIKit.S(30), UIKit.S(14)); UIKit.Fit(label, 34, 20);
            Color gold = Pal.Hex("F6D77A");
            var bg = bubble.Find("Bg").GetComponent<Image>(); bg.color = gold;
            bubbleTail.GetComponent<Image>().color = gold;
            var glowSprite = Art.FX("glow");
            if (glowSprite)
            {
                var glow = UIKit.Img("VipOrderGlow", bubble, glowSprite, false, false);
                glow.rectTransform.Stretch();
                glow.color = new Color(gold.r, gold.g, gold.b, 0.35f);
                glow.rectTransform.SetAsFirstSibling();
            }
            var bar = UIKit.Sliced("Patience", bubble, Art.FX("hud_bar"), Pal.Hex("D9A441"));
            patienceBar = bar.rectTransform.Anchor(new Vector2(0.5f, 1), new Vector2(0, UIKit.S(16)), new Vector2(bubbleWidth - UIKit.S(16), UIKit.S(20)));
            var inner = UIKit.Node("Inset", patienceBar); inner.Stretch(UIKit.S(3), UIKit.S(3), UIKit.S(3), UIKit.S(3));
            patienceFill = UIKit.Sliced("Fill", inner, Art.FX("hud_bar"), Pal.Leaf);
            patienceFill.rectTransform.Stretch();
        }

        void UpdatePatience()
        {
            if (patienceBar == null || leaving || GameView.MotionPaused(this)) return;
            patienceBar.gameObject.SetActive(!customer.finished);
            if (customer.finished) return;
            var game = GameBoot.I != null ? GameBoot.I.CurrentGame : null;
            if (game != null && (game.Paused || game.kitchen.ended)) return;
            float ratio = Mathf.Clamp01(customer.patience / Mathf.Max(0.01f, customer.patienceMax));
            patienceFill.rectTransform.anchorMax = new Vector2(ratio, 1);
            var color = ratio > 0.5f ? Color.Lerp(Pal.Butter, Pal.Leaf, (ratio - 0.5f) * 2) : Color.Lerp(Pal.Red, Pal.Butter, ratio * 2);
            if (ratio < 0.3f) { color = Pal.Red; color.a = 0.65f + 0.35f * Mathf.Abs(Mathf.Sin((GameView.AnimationOwner(transform)?.AnimationTime ?? Time.time) * 8)); }
            patienceFill.color = color;
            patienceBar.anchoredPosition = new Vector2(ratio < 0.15f ? Mathf.Sin((GameView.AnimationOwner(transform)?.AnimationTime ?? Time.time) * 32) * UIKit.S(3) : 0, UIKit.S(16));
            vipPulse += Time.deltaTime * (ratio < 0.3f ? 8 : 2);
            if (vipHalo)
            {
                var haloColor = ratio < 0.3f ? Pal.Hex("E97859") : Pal.Hex("FFD66B");
                haloColor.a = Mathf.Lerp(0.55f, 0.9f, (Mathf.Sin(vipPulse) + 1) * 0.5f);
                vipHalo.color = haloColor;
            }
            if (!vipArrived) return;
            vipSparkClock -= Time.deltaTime;
            if (vipSparkClock <= 0)
            {
                vipSparkClock = 1.2f;
                // Tiny, short-lived particles stay close to the face and below the order card.
                var pos = rt.TransformPoint(new Vector3(rt.sizeDelta.x * (UnityEngine.Random.value < 0.5f ? -0.28f : 0.28f), rt.sizeDelta.y * 0.60f, 0));
                VFX.Burst(pos, "star", 2, new[] { Pal.Butter }, UIKit.S(4), UIKit.S(10), UIKit.S(6), UIKit.S(11), 0.45f, 0, 60);
            }
        }

        public void AngryLeave()
        {
            SetSad(true);
            if (patienceBar) patienceBar.gameObject.SetActive(false);
            foreach (var order in orderSlots) order.gameObject.SetActive(false);
            bubbleText.enabled = true; bubbleText.text = "Bỏ đi!";
            body.color = Pal.Hex("E98B75");
            Tween.KillAll(visual); Tween.Shake(visual, UIKit.S(12), 0.35f);
            Tween.Delay(0.35f, () => { if (this) Leave(customer.seat != 0); }, owner: this);
        }

        public Vector3 OrderWorld(int i) => orderSlots[Mathf.Clamp(i, 0, orderSlots.Count - 1)].position;
        public Vector3 HeadWorld => headPivot.position + Vector3.up * (rt.rect.height * 0.25f * rt.lossyScale.y);

        public void Arrive(bool fromLeft)
        {
            if (customer.vip)
                Tween.Delay(single ? 0.55f : 0.75f, () =>
                {
                    if (!this || leaving || GameView.MotionPaused(this)) return;
                    vipArrived = true;
                    var game = GameBoot.I != null ? GameBoot.I.CurrentGame : null;
                    if (game != null) game.Toast("Khách VIP đến! Phục vụ trước nhé", 2.4f);
                    Sfx.Play("star1", 0.85f);
                    VFX.Ring(rt.TransformPoint(new Vector3(0, rt.sizeDelta.y * 0.60f, 0)), Pal.Butter, rt.sizeDelta.x * 0.5f, 0.45f);
                }, owner: this);
            if (single)
            {
                // chibi peeks up from behind the counter with a springy pop
                rt.anchoredPosition = seatPos - new Vector2(0, rt.rect.height * 0.9f);
                bubble.localScale = Vector3.zero;
                Tween.Move(rt, seatPos, 0.55f).SetEase(Ease.OutCubic);
                Tween.Delay(0.4f, () =>
                {
                    if (this == null) return;
                    Tween.Squash(visual, 0.22f, 0.45f);
                    Tween.Scale(bubble, Vector3.one, 0.45f).SetEase(Ease.OutCubic);
                    Sfx.Play("customer_in", 0.7f);
                }, owner: this);
                return;
            }
            float off = fromLeft ? -UIKit.S(520) : UIKit.S(520);
            rt.anchoredPosition = seatPos + new Vector2(off, 0);
            bubble.localScale = Vector3.zero;
            Tween.Move(rt, seatPos, 0.75f).SetEase(Ease.OutCubic);
            // walking hop
            Tween.Float(0, 1, 0.75f, t => { if (visual) visual.anchoredPosition = new Vector2(0, Mathf.Abs(Mathf.Sin(t * Mathf.PI * 4)) * 26f * (1 - t)); }, visual);
            Tween.Float(0, 1, 0.75f, t => { if (visual) visual.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(t * Mathf.PI * 4) * 6f * (1 - t)); }, visual.gameObject);
            Tween.Delay(0.62f, () =>
            {
                if (this == null) return;
                Tween.Scale(bubble, Vector3.one, 0.5f).SetEase(Ease.OutCubic);
                Tween.Squash(visual, 0.15f, 0.4f);
                Sfx.Play("customer_in", 0.7f);
            }, owner: this);
        }

        public void MarkReserved(int i)
        {
            if (i < 0 || i >= orderSlots.Count) return;
            Tween.PunchScale(orderSlots[i], Vector3.one * 0.08f, 0.35f);
        }

        public void Delivered(int i)
        {
            if (i < 0 || i >= orderSlots.Count) return;
            var chk = orderChecks[i];
            chk.enabled = true;
            chk.rectTransform.localScale = Vector3.one * 2.2f;
            Tween.Scale(chk.rectTransform, Vector3.one, 0.35f).SetEase(Ease.OutBack);
            orderIcons[i].color = new Color(1, 1, 1, 0.75f);
            Tween.PunchScale(orderSlots[i], Vector3.one * 0.08f, 0.4f);
            Tween.Squash(visual, 0.2f, 0.4f);
            Flash(0.6f);
        }

        public void Happy()
        {
            happy = true;
            if (!single) { eyes.sprite = Art.Char(id + "_eyes_closed"); mouth.sprite = Art.Char(id + "_mouth_happy"); }
            else { var hs = Art.Char(id + "_happy"); if (hs) body.sprite = hs; body.color = Color.white; }
            Tween.KillAll(visual);
            visual.localScale = Vector3.one;
            Tween.Float(0, 1, 0.55f, t => { if (visual) visual.anchoredPosition = new Vector2(0, Mathf.Sin(t * Mathf.PI) * 60f); }, visual).SetEase(Ease.Linear)
                 .SetLoops(2);
            Tween.PunchScale(blush.rectTransform, Vector3.one * 0.4f, 0.5f);
            VFX.Hearts(HeadWorld, 6);
            foreach (var ic in orderSlots) ic.gameObject.SetActive(false);
            bubbleText.enabled = true;
            bubbleText.text = UnityEngine.Random.value < 0.5f ? "Ngon quá!" : "맛있어요!";
            Tween.PunchScale(bubble, Vector3.one * 0.005f, 0.45f);
        }

        public void SetSad(bool on)
        {
            if (happy || sad == on) return;
            sad = on;
            if (single) { body.color = on ? new Color(0.85f, 0.88f, 1f) : Color.white; if (on) Tween.Shake(visual, 6, 0.4f); return; }
            eyes.sprite = Art.Char(id + (on ? "_eyes_sad" : "_eyes_open"));
            mouth.sprite = Art.Char(id + (on ? "_mouth_sad" : "_mouth_idle"));
        }

        public void Leave(bool toRight)
        {
            leaving = true;
            if (single)
            {
                // wave (wiggle) then duck down behind the counter
                Tween.Scale(bubble, Vector3.zero, 0.25f).SetEase(Ease.InBack);
                Tween.PunchRotation(visual, 12, 0.45f, 4);
                Tween.Delay(0.35f, () =>
                {
                    if (this == null) return;
                    Tween.Move(rt, seatPos - new Vector2(0, rt.rect.height), 0.4f).SetEase(Ease.InBack)
                         .OnComplete(() => { if (this) Destroy(gameObject); });
                }, owner: this);
                return;
            }
            Tween.Scale(bubble, Vector3.zero, 0.3f).SetEase(Ease.InBack);
            Tween.PunchRotation(visual, 10, 0.4f);
            Tween.Delay(0.25f, () =>
            {
                if (this == null) return;
                float off = toRight ? UIKit.S(560) : -UIKit.S(560);
                Tween.Move(rt, rt.anchoredPosition + new Vector2(off, 0), 0.8f).SetEase(Ease.InCubic).OnComplete(() => { if (this) Destroy(gameObject); });
                Tween.Float(0, 1, 0.8f, t => { if (visual) visual.anchoredPosition = new Vector2(0, Mathf.Abs(Mathf.Sin(t * Mathf.PI * 4)) * 24f); }, visual);
            }, owner: this);
        }

        void Flash(float a)
        {
            foreach (var g in new Graphic[] { head, body })
            {
                Tween.KillAll(g);
                g.color = new Color(1f, 1f, 0.75f);
                Tween.Color(g, Color.white, 0.4f);
            }
        }

        public void StopEffects()
        {
            Tween.KillHierarchy(rt);
            if (leaving) { gameObject.SetActive(false); Destroy(gameObject); return; }
            rt.anchoredPosition = seatPos;
            visual.anchoredPosition = Vector2.zero; visual.localScale = Vector3.one; visual.localRotation = Quaternion.identity;
            bubble.localScale = Vector3.one;
            body.color = Color.white; head.color = Color.white;
        }

        void Update()
        {
            if (GameView.MotionPaused(this)) return;
            UpdatePatience();
            if (happy || leaving || single) return;
            blinkT -= Time.deltaTime;
            if (blinkT <= 0)
            {
                blinkT = UnityEngine.Random.Range(2f, 5f);
                if (!sad)
                {
                    eyes.sprite = Art.Char(id + "_eyes_closed");
                    Tween.Delay(0.12f, () => { if (this && !happy) eyes.sprite = Art.Char(id + (sad ? "_eyes_sad" : "_eyes_open")); }, owner: this);
                }
            }
        }
    }
}
