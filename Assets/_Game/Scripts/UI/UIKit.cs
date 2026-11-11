using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace BepJam
{
    /// <summary>Code-first uGUI helpers. Layout coordinates are given in the artist's mock-up space
    /// (853 x 1844 px, origin top-left) and scaled to the 1080-wide phone canvas.</summary>
    public static class UIKit
    {
        public const float ArtW = 853f, ArtH = 1844f;
        public const float K = 1080f / ArtW;               // mock-up px → canvas units
        public static float S(float artPx) => artPx * K;

        public static RectTransform Node(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = 5;
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            return rt;
        }

        /// <summary>Place by mock-up rect (x,y = top-left, art px) inside a parent that is the full phone.</summary>
        public static RectTransform PlaceArt(this RectTransform rt, float x, float y, float w, float h)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(S(w), S(h));
            rt.anchoredPosition = new Vector2(S(x + w / 2f), -S(y + h / 2f));
            return rt;
        }

        public static RectTransform Stretch(this RectTransform rt, float l = 0, float t = 0, float r = 0, float b = 0)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(l, b); rt.offsetMax = new Vector2(-r, -t);
            return rt;
        }

        public static RectTransform Center(this RectTransform rt, float x, float y, float w, float h)
        {
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(w, h); rt.anchoredPosition = new Vector2(x, y);
            return rt;
        }

        public static RectTransform Anchor(this RectTransform rt, Vector2 anchor, Vector2 pos, Vector2 size, Vector2? pivot = null)
        {
            rt.anchorMin = rt.anchorMax = anchor; rt.pivot = pivot ?? new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size; rt.anchoredPosition = pos;
            return rt;
        }

        public static Image Img(string name, Transform parent, Sprite sprite, bool raycast = false, bool preserve = true)
        {
            var rt = Node(name, parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite; img.raycastTarget = raycast; img.preserveAspect = preserve;
            if (sprite != null && sprite.border != Vector4.zero) { img.type = Image.Type.Sliced; img.preserveAspect = false; }
            return img;
        }

        public static Image Sliced(string name, Transform parent, Sprite sprite, Color? color = null, float ppu = 1f)
        {
            var img = Img(name, parent, sprite, false, false);
            img.type = Image.Type.Sliced; img.pixelsPerUnitMultiplier = ppu;
            if (color.HasValue) img.color = color.Value;
            return img;
        }

        public static Text Label(string name, Transform parent, string text, int size, Color color, TextAnchor align = TextAnchor.MiddleCenter,
                                 float outline = 0f, Color? outlineColor = null, bool shadow = false)
        {
            var rt = Node(name, parent);
            var t = rt.gameObject.AddComponent<Text>();
            t.font = Art.Font; t.text = text; t.fontSize = size; t.color = color; t.alignment = align;
            t.raycastTarget = false; t.horizontalOverflow = HorizontalWrapMode.Wrap; t.verticalOverflow = VerticalWrapMode.Overflow;
            t.supportRichText = true; t.lineSpacing = 0.95f;
            if (outline > 0)
            {
                var o = rt.gameObject.AddComponent<Outline>();
                o.effectColor = outlineColor ?? Pal.Outline; o.effectDistance = new Vector2(outline, -outline);
                var o2 = rt.gameObject.AddComponent<Outline>();
                o2.effectColor = outlineColor ?? Pal.Outline; o2.effectDistance = new Vector2(-outline, outline);
            }
            if (shadow)
            {
                var s = rt.gameObject.AddComponent<Shadow>();
                s.effectColor = new Color(0.29f, 0.18f, 0.15f, 0.55f); s.effectDistance = new Vector2(0, -Mathf.Max(3, outline + 3));
            }
            return t;
        }

        /// <summary>Juicy button: squash on press, bounce on release, click sound.</summary>
        public static JuicyButton Button(string name, Transform parent, Sprite bg, string text, int fontSize, Action onClick,
                                         Sprite icon = null, Color? textColor = null)
        {
            var img = Img(name, parent, bg, true, bg != null && bg.border == Vector4.zero);
            var jb = img.gameObject.AddComponent<JuicyButton>();
            jb.onClick = onClick;
            if (icon != null)
            {
                var ic = Img("Icon", img.transform, icon);
                jb.icon = ic;
                ((RectTransform)ic.transform).Stretch(18, 14, 18, 22);
            }
            if (!string.IsNullOrEmpty(text))
            {
                // Venba style: cream lettering, soft drop shadow, padded and auto-fitted so it never overflows
                var t = Label("Text", img.transform, text, fontSize, textColor ?? Pal.Hex("FBF1DE"), TextAnchor.MiddleCenter, 0f, null, true);
                ((RectTransform)t.transform).Stretch(30, 24, 30, 24);
                Fit(t, fontSize, 18);
                jb.label = t;
            }
            return jb;
        }

        /// <summary>Wide CTA with a leading icon: the icon sits on a cream disc so it reads on every button colour
        /// (a teal icon on a teal button disappears), and the label is centred in the space to the right of it.</summary>
        public static Image CtaIcon(JuicyButton b, Sprite icon, float height)
        {
            if (icon == null) return null;
            var rt = (RectTransform)b.transform;
            float d = height * 0.66f, left = height * 0.2f;
            var disc = Img("IconDisc", rt, Art.FX("btn_round_cream"));
            disc.rectTransform.Anchor(new Vector2(0, 0.5f), new Vector2(left, height * 0.03f), new Vector2(d, d), new Vector2(0, 0.5f));
            var ic = Img("Icon", disc.transform, icon);
            ic.rectTransform.Stretch(d * 0.2f, d * 0.17f, d * 0.2f, d * 0.23f);
            if (b.label)
            {
                var lr = b.label.rectTransform;
                lr.Stretch(left + d + 16, 24, Mathf.Max(30, left + 10), 24);
            }
            return ic;
        }

        /// <summary>Shrink-to-fit inside the label's (already padded) rect: never overflow, never wrap mid-word badly.</summary>
        public static Text Fit(Text t, int max, int min = 16)
        {
            t.resizeTextForBestFit = true; t.resizeTextMaxSize = max; t.resizeTextMinSize = Mathf.Min(min, max);
            t.horizontalOverflow = HorizontalWrapMode.Wrap; t.verticalOverflow = VerticalWrapMode.Truncate;
            return t;
        }

        public static Text FitLines(Text t, int max, int min = 16, int lines = 2)
        {
            Fit(t, max, min);
            var settings = t.GetGenerationSettings(new Vector2(t.rectTransform.rect.width, 0));
            settings.resizeTextForBestFit = false;
            settings.verticalOverflow = VerticalWrapMode.Overflow;
            int size = max;
            for (; size > min; size--)
            {
                settings.fontSize = size;
                t.cachedTextGeneratorForLayout.Populate(t.text, settings);
                if (t.cachedTextGeneratorForLayout.lineCount <= lines &&
                    t.cachedTextGeneratorForLayout.GetPreferredHeight(t.text, settings) / t.pixelsPerUnit <= t.rectTransform.rect.height) break;
            }
            t.resizeTextMaxSize = size;
            return t;
        }

        public static CanvasGroup Group(GameObject go)
        {
            var cg = go.GetComponent<CanvasGroup>();
            return cg ? cg : go.AddComponent<CanvasGroup>();
        }

        public static void SetAlpha(Graphic g, float a) { var c = g.color; c.a = a; g.color = c; }
    }

    public class JuicyButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerClickHandler, IPointerExitHandler, IPointerMoveHandler
    {
        public Action onClick;
        public Image icon;
        public Text label;
        public bool interactable = true;
        public string sound = "button";
        public bool quickPress;
        public Action<bool> onPressChanged;
        Vector3 baseScale = Vector3.one;
        Vector2 pressPosition;
        int pressPointer;
        bool down, pointerPressed, tapArmed, dragCancelled;
        TweenHandle pressTween;

        void Awake() { baseScale = transform.localScale; }

        public void SetBaseScale(Vector3 s) { baseScale = s; transform.localScale = s; }

        public void OnPointerDown(PointerEventData e)
        {
            if (e.button != PointerEventData.InputButton.Left) return;
            pressPosition = e.position;
            pressPointer = e.pointerId;
            pointerPressed = tapArmed = true;
            dragCancelled = false;
            onPressChanged?.Invoke(true);
            if (!interactable) return;
            down = true;
            if (quickPress) pressTween?.Kill();
            else Tween.KillAll(transform);
            var pressedScale = quickPress ? baseScale * 0.95f : new Vector3(baseScale.x * 1.08f, baseScale.y * 0.86f, 1);
            pressTween = Tween.Scale(transform, pressedScale, 0.08f).SetEase(Ease.OutQuad).SetUnscaled();
        }

        public void OnPointerUp(PointerEventData e)
        {
            if (e.button != PointerEventData.InputButton.Left || e.pointerId != pressPointer) return;
            CheckDrag(e);
            if (pointerPressed)
            {
                pointerPressed = false;
                onPressChanged?.Invoke(false);
            }
            ReleaseVisual();
        }

        void ReleaseVisual()
        {
            if (!down) return;
            down = false;
            if (quickPress) pressTween?.Kill();
            else Tween.KillAll(transform);
            pressTween = Tween.Scale(transform, baseScale, quickPress ? 0.12f : 0.35f)
                .SetEase(quickPress ? Ease.OutCubic : Ease.OutElastic).SetUnscaled();
        }

        void CheckDrag(PointerEventData e)
        {
            int threshold = EventSystem.current ? Mathf.Max(1, EventSystem.current.pixelDragThreshold) : 10;
            if (e.dragging || (e.position - pressPosition).sqrMagnitude >= threshold * threshold) dragCancelled = true;
        }

        public void OnPointerMove(PointerEventData e)
        {
            if (pointerPressed && e.pointerId == pressPointer) CheckDrag(e);
        }

        public void OnPointerExit(PointerEventData e)
        {
            if (e.pointerId != pressPointer) return;
            if (pointerPressed) dragCancelled = true;
            ReleaseVisual();
        }

        public void OnPointerClick(PointerEventData e)
        {
            if (e.button != PointerEventData.InputButton.Left || e.pointerId != pressPointer || !tapArmed) return;
            CheckDrag(e);
            tapArmed = false;
            if (dragCancelled || !e.eligibleForClick) return;
            if (!interactable)
            {
                if (!quickPress) Tween.PunchRotation(transform, 8, 0.3f).SetUnscaled();
                Sfx.Play("error", 0.6f); return;
            }
            if (!string.IsNullOrEmpty(sound)) Sfx.PlayVaried(sound, 0.9f, 0.05f);
            onClick?.Invoke();
        }

        void OnDisable()
        {
            if (pointerPressed) onPressChanged?.Invoke(false);
            down = pointerPressed = tapArmed = false;
            pressTween?.Kill();
            pressTween = null;
            transform.localScale = baseScale;
        }
    }

    /// <summary>Gentle looping idle motion (bob / breathe / sway) – used everywhere for "alive" UI.</summary>
    public class Idle : MonoBehaviour
    {
        public float bob, bobSpeed = 2f, breathe, breatheSpeed = 2.2f, sway, swaySpeed = 1.3f, phase;
        public bool unscaled;
        Vector2 basePos; Vector3 baseScale; float baseRot; bool init;
        float clock;

        public static Idle Add(Component c, float bob = 0, float breathe = 0, float sway = 0, float speed = 1f)
        {
            var i = c.gameObject.GetComponent<Idle>() ?? c.gameObject.AddComponent<Idle>();
            i.bob = bob; i.breathe = breathe; i.sway = sway;
            i.bobSpeed *= speed; i.breatheSpeed *= speed; i.swaySpeed *= speed;
            i.phase = UnityEngine.Random.value * 10f;
            return i;
        }

        public void Rebase()
        {
            var rt = (RectTransform)transform;
            basePos = rt.anchoredPosition; baseScale = rt.localScale; baseRot = rt.localEulerAngles.z; init = true;
        }

        void Update()
        {
            if (GameView.MotionPaused(this)) return;
            var rt = (RectTransform)transform;
            if (!init) Rebase();
            clock += unscaled ? Time.unscaledDeltaTime : Time.deltaTime;
            float t = clock + phase;
            if (bob != 0) rt.anchoredPosition = basePos + new Vector2(0, Mathf.Sin(t * bobSpeed) * bob);
            if (breathe != 0) rt.localScale = new Vector3(baseScale.x * (1 - Mathf.Sin(t * breatheSpeed) * breathe * 0.5f), baseScale.y * (1 + Mathf.Sin(t * breatheSpeed) * breathe), 1);
            if (sway != 0) rt.localEulerAngles = new Vector3(0, 0, baseRot + Mathf.Sin(t * swaySpeed) * sway);
        }
    }
}
