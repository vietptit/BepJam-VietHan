using System;
using UnityEngine;
using UnityEngine.UI;

namespace BepJam
{
    /// <summary>Short, plate-local cooking stories. All artwork is owned by a disposable FX root.</summary>
    public static class CookFx
    {
        const float LongDuration = 1.08f;

        public static Color LiquidColor(string ing)
        {
            switch (ing)
            {
                case "sua_dac": return Pal.Hex("FFF1D6");
                case "ca_phe": return Pal.Hex("6B3E26");
                case "nuoc_nong": return Pal.Hex("BFE8F7");
                case "mut_yuja": return Pal.Hex("FFC93C");
                case "sot_tteok": return Pal.Hex("E8442E");
                default: return Pal.Butter;
            }
        }

        // One clock per recipe avoids delayed tweens capturing a stale starting pose.
        // Children inherit the canvas scale; all choreography below uses 140-unit design space.
        sealed class Stage
        {
            public readonly RectTransform root;
            public readonly PlateView plate;
            public readonly float size;
            public Stage(PlateView plate, Vector3 world, float size)
            {
                this.plate = plate; this.size = size;
                root = UIKit.Node("Cooking story", VFX.Layer);
                plate.StopCookingStory();
                plate.cookingStory = root;
                root.position = world;
                root.localScale = Vector3.one * (size / 140f);
            }
            public bool Alive => root && plate && plate.rt && plate.item && plate.itemRt && plate.plateRt;
            public Image Food(string id, float w, float h) => Make(Art.Food(id), w, h, Color.white);
            public Image Fx(string id, float w, float h, Color color) => Make(Art.FX(id), w, h, color);
            Image Make(Sprite sprite, float w, float h, Color color)
            {
                var image = UIKit.Img("Cooking prop", root, sprite, false, false);
                image.type = Image.Type.Simple;
                image.color = color;
                image.rectTransform.sizeDelta = new Vector2(w, h);
                image.gameObject.SetActive(false);
                return image;
            }
            public void Run(float duration, Action<float, float> frame)
            {
                float previous = -1f;
                // The plate owns this root. Its Clear/OnDisable also disposes it if the clock is cancelled.
                Tween.Float(0, duration, duration, t =>
                {
                    if (!Alive)
                    {
                        if (root) UnityEngine.Object.Destroy(root.gameObject);
                        return;
                    }
                    frame(t, previous);
                    previous = t;
                    if (t >= duration && root) UnityEngine.Object.Destroy(root.gameObject);
                }, root).SetOwner(plate).OnCancel(() =>
                {
                    if (plate && plate.cookingStory == root) plate.StopCookingStory();
                    else if (root) UnityEngine.Object.Destroy(root.gameObject);
                }).OnComplete(() =>
                {
                    if (plate && plate.cookingStory == root)
                    {
                        plate.itemRt.localScale = Vector3.one;
                        plate.itemRt.localRotation = Quaternion.identity;
                        plate.cookingStory = null;
                    }
                    if (root) UnityEngine.Object.Destroy(root.gameObject);
                });
            }
        }

        static float Phase(float t, float start, float end) => Mathf.Clamp01((t - start) / (end - start));
        static float Smooth(float p) => p * p * (3f - 2f * p);
        static float Out(float p) => 1f - (1f - p) * (1f - p);
        static bool Beat(float t, float previous, float at) => previous < at && t >= at;
        static void Sound(float t, float previous, float at, string name, float volume, float pitch)
        {
            if (Beat(t, previous, at)) Sfx.Play(name, volume, pitch);
        }
        static void Pose(Image image, bool visible, float x, float y, float angle = 0,
            float sx = 1, float sy = 1, float alpha = 1)
        {
            if (!image) return;
            image.gameObject.SetActive(visible);
            if (!visible) return;
            var rt = image.rectTransform;
            rt.anchoredPosition = new Vector2(x, y);
            rt.localRotation = Quaternion.Euler(0, 0, angle);
            rt.localScale = new Vector3(sx, sy, 1);
            var c = image.color; c.a = alpha; image.color = c;
        }
        static void Outline(Image image, float width = 1.5f)
        {
            if (!image) return;
            var outline = image.gameObject.AddComponent<Outline>();
            outline.effectColor = Pal.Outline;
            outline.effectDistance = new Vector2(width, -width);
        }

        // Owned ballistic flecks: no callbacks can outlive their scene or create particles in a new layer.
        static Action<float> Flecks(Stage s, string sprite, int count, Color color, float start,
            float life, float x, float y, float width, float height, float speed = 100)
        {
            var images = new Image[count];
            for (int i = 0; i < count; i++) images[i] = s.Fx(sprite, width, height, color);
            return t =>
            {
                if (!s.Alive) return;
                for (int i = 0; i < count; i++)
                {
                    float age = t - start - i * .008f;
                    float a = (25f + i * 137.5f % 130f) * Mathf.Deg2Rad;
                    float v = speed * (.75f + (i % 3) * .18f);
                    float p = Mathf.Clamp01(age / life);
                    Pose(images[i], age >= 0 && age < life,
                        x + Mathf.Cos(a) * v * age, y + Mathf.Sin(a) * v * age - 220 * age * age,
                        i * 47 + age * (i % 2 == 0 ? 240 : -300), 1 - p * .4f, 1 - p * .4f,
                        color.a * (1 - Smooth(Phase(p, .55f, 1))));
                }
            };
        }

        public static void Play(DishDef dish, string incomingIngredient, string waitingIngredient,
            Vector3 worldPos, float size, PlateView plate)
        {
            if (dish == null || !plate || !plate.item || !plate.itemRt || !plate.plateRt || !VFX.Layer) return;
            Tween.KillAll(plate.itemRt);
            plate.itemRt.localScale = Vector3.one;
            plate.itemRt.localRotation = Quaternion.identity;
            plate.item.enabled = false;
            var s = new Stage(plate, worldPos, size);
            // The egg recipe is named "Chiên, kẹp", so ingredient identity matters too.
            if (incomingIngredient == "banh_mi" || waitingIngredient == "banh_mi")
                Sandwich(s, dish, incomingIngredient == "banh_mi" ? waitingIngredient : incomingIngredient);
            else if (dish.verb == "Pha") Pour(s, dish, incomingIngredient, waitingIngredient);
            else if (dish.verb == "Trộn") Stir(s, dish, incomingIngredient == "com" ? waitingIngredient : incomingIngredient);
            else DropIn(s, dish, incomingIngredient, waitingIngredient);
        }

        // Reveal through the existing finish beat, then explicitly settle before Kitchen can deliver.
        static void FinishPop(Stage s, DishDef dish)
        {
            if (!s.Alive) return;
            s.plate.item.type = Image.Type.Simple;
            s.plate.Show(dish.id, true, false);
            s.plate.Land();
            // Land schedules longer squash tweens; this story owns the shorter final settle.
            Tween.KillAll(s.plate.itemRt);
            Tween.KillAll(s.plate.plateRt, true);
            VFX.Flash(s.root.position, s.size * 1.45f, .18f);
            VFX.Ring(s.root.position, Pal.Butter, s.size * 1.4f, .22f);
            Sfx.Play("merge", .75f, 1.08f);
        }
        static void Settle(Stage s, float t, float reveal, float end)
        {
            if (!s.Alive) return;
            float p = Phase(t, reveal, end);
            float wave = Mathf.Sin(p * Mathf.PI * 4) * (1 - p) * (1 - p);
            s.plate.itemRt.localScale = new Vector3(1 + wave * .15f, 1 - wave * .13f, 1);
            s.plate.itemRt.localRotation = Quaternion.Euler(0, 0, wave * 5);
        }

        /// <summary>Bread already halved on the cutting board → the sandwich animation skips its own knife slash.</summary>
        static bool PreCut() { var g = GameBoot.I != null ? GameBoot.I.CurrentGame : null; return g != null && g.def.NeedsCut("banh_mi"); }

        static void Sandwich(Stage s, DishDef dish, string filling)
        {
            // real bread halves (bottom = crumb face up, top = crusty lid) when available; otherwise mask one loaf in two
            bool realHalves = Resources.Load<Sprite>("Food/banh_mi_bot") != null && Resources.Load<Sprite>("Food/banh_mi_top") != null;
            var bottom = s.Food(realHalves ? "banh_mi_bot" : "banh_mi", realHalves ? 150 : 140, realHalves ? 62 : 140);
            var top = s.Food(realHalves ? "banh_mi_top" : "banh_mi", realHalves ? 150 : 140, realHalves ? 62 : 140);
            if (!realHalves)
                foreach (var half in new[] { bottom, top })
                {
                    half.type = Image.Type.Filled;
                    half.fillMethod = Image.FillMethod.Vertical;
                    half.fillOrigin = half == top ? 1 : 0;
                    half.fillAmount = .5f;
                }
            // Rotate the lid around its left edge rather than spinning it around its centre.
            top.rectTransform.pivot = new Vector2(.18f, .5f);
            var bottomFace = s.Fx("dot", 109, 19, Pal.Hex("F9E7BF")); Outline(bottomFace);
            var fill = s.Food(filling, 91, 80);
            var eggWhite = filling == "trung" ? s.Fx("dot", 88, 48, Pal.Hex("FFF8E7")) : null;
            var eggYolk = filling == "trung" ? s.Fx("dot", 29, 26, Pal.Hex("FFC543")) : null;
            if (eggWhite) Outline(eggWhite, 1);
            var topFace = s.Fx("dot", 109, 19, Pal.Hex("FFF0CE")); Outline(topFace);
            top.rectTransform.SetAsLastSibling();
            var knife = s.Fx("rounded", 93, 12, Pal.Hex("EDF6FA")); Outline(knife);
            var handle = s.Fx("rounded", 32, 15, Pal.Hex("80503A")); Outline(handle);
            var shine = s.Fx("shine", 35, 70, Color.white);
            var herbs = new Image[6];
            for (int i = 0; i < herbs.Length; i++)
                herbs[i] = s.Fx(i < 3 ? "leaf" : "rounded", i < 3 ? 21 : 4,
                    i < 3 ? 13 : 27, i < 3 ? Pal.Leaf : Pal.Hex("F69938"));
            // Fill and garnish sit between the bread halves in the sibling order.
            foreach (var herb in herbs) herb.rectTransform.SetSiblingIndex(top.rectTransform.GetSiblingIndex());
            bottom.rectTransform.SetAsFirstSibling(); bottomFace.rectTransform.SetSiblingIndex(1);
            var crumbs = Flecks(s, "dot", 10, Pal.Hex("EBC17E"), .77f, .27f, 0, 0, 5, 4, 150);
            bool pc = PreCut();   // bread already halved on the cutting board: no knife, bun starts open
            s.Run(LongDuration, (t, previous) =>
            {
                if (!s.Alive) return;
                bool visible = t < .84f;
                float squat = Mathf.Sin(Phase(t, 0, .12f) * Mathf.PI) * .22f;
                float hop = Mathf.Sin(Phase(t, .08f, .30f) * Mathf.PI) * 27;
                float open = (pc ? Out(Phase(t, 0f, .12f)) : Out(Phase(t, .24f, .40f))) * (1 - Smooth(Phase(t, .65f, .77f)));
                float press = Mathf.Sin(Phase(t, .77f, .84f) * Mathf.PI) * .20f;
                float bounce = Mathf.Sin(Phase(t, .32f, .47f) * Mathf.PI * 2) * 5 * (1 - Phase(t, .32f, .47f));
                float bOff = realHalves ? -20 : 0, tClosed = realHalves ? 24 : 6, hinge = realHalves ? -48f : -44.8f;
                Pose(bottom, visible, 0, bOff + hop - open * 7 + bounce, 0, 1 + squat + press, 1 - squat - press);
                Pose(top, visible, hinge - open * 7, hop + open * 57 + (1 - open) * tClosed,
                    open * 25, 1 + squat + press, 1 - squat - press);
                Pose(bottomFace, visible && !realHalves && (pc || t >= .26f), 0, 3 + bounce, 18, 1 + press, open * .95f);
                Pose(topFace, visible && !realHalves && (pc || t >= .26f), -open * 12, 10 + open * 58, 18 + open * 25, 1, open);
                float cut = Smooth(Phase(t, .15f, .28f));
                Pose(knife, visible && !pc && t >= .15f && t < .30f, Mathf.Lerp(-105, 100, cut), 29 + hop, -18);
                Pose(handle, visible && !pc && t >= .15f && t < .30f, Mathf.Lerp(-162, 43, cut), 47 + hop, -18);
                Pose(shine, visible && !pc && t >= .21f && t < .32f, 5, 27, -65, 1, 1, 1 - Phase(t, .23f, .32f));
                float fall = Phase(t, .35f, .55f);
                float impact = Mathf.Sin(Phase(t, .55f, .65f) * Mathf.PI) * .32f;
                Pose(fill, visible && filling != "trung" && t >= .35f, Mathf.Lerp(-55, 0, fall),
                    Mathf.Lerp(145, 13, fall) + Mathf.Sin(fall * Mathf.PI) * 40,
                    Mathf.Lerp(-145, 0, Out(fall)), 1 + impact + press * 1.7f, 1 - impact - press * .7f);
                Pose(eggWhite, visible && t >= .35f, Mathf.Lerp(-55, 0, fall),
                    Mathf.Lerp(145, 13, fall) + Mathf.Sin(fall * Mathf.PI) * 40,
                    Mathf.Lerp(-145, 0, Out(fall)), 1 + impact + press * 1.7f, 1 - impact - press * .7f);
                Pose(eggYolk, visible && t >= .35f, Mathf.Lerp(-55, 0, fall),
                    Mathf.Lerp(145, 16, fall) + Mathf.Sin(fall * Mathf.PI) * 40,
                    Mathf.Lerp(-145, 0, Out(fall)), 1 + impact + press, 1 - impact - press * .7f);
                for (int i = 0; i < herbs.Length; i++)
                {
                    float p = Phase(t, .48f + i * .014f, .65f + i * .014f);
                    float targetX = (i % 3 - 1) * 24;
                    Pose(herbs[i], visible && t >= .48f + i * .014f,
                        targetX + Mathf.Sin(p * Mathf.PI * 2 + i) * 17 * (1 - p),
                        Mathf.Lerp(103 + i * 4, 17 + i % 2 * 7, Out(p)),
                        i * 37 + (1 - p) * 150, 1 + press, 1 - press);
                }
                crumbs(t);
                Sound(t, previous, 0, "tick", .35f, .9f);
                if (!pc) { Sound(t, previous, .13f, "whoosh", .45f, 1.55f); Sound(t, previous, .25f, "click", .65f, 1.4f); }
                Sound(t, previous, .34f, "pop", .45f, 1.3f);
                Sound(t, previous, .55f, "land", .8f, 1.15f);
                Sound(t, previous, .65f, "whoosh", .35f, 1.1f);
                Sound(t, previous, .77f, "pop", .85f, .75f);
                if (Beat(t, previous, .84f)) FinishPop(s, dish);
                if (t >= .84f) Settle(s, t, .84f, LongDuration);
            });
        }

        static void Pour(Stage s, DishDef dish, string incoming, string waiting)
        {
            bool hot = dish.id == "tra_yuja";
            string source = incoming == "sua_dac" || incoming == "nuoc_nong" ? incoming : waiting;
            Color liquid = LiquidColor(source);
            var cup = MakeCup(s, dish.Icon);
            var filled = MakeCup(s, dish.Icon);
            cup.color = new Color(1, 1, 1, .25f);
            filled.type = Image.Type.Filled; filled.fillMethod = Image.FillMethod.Vertical; filled.fillOrigin = 0;
            var sourceArt = s.Food(source, 94, 94);
            var stream = s.Fx("rounded", 10, 100, liquid); Outline(stream, .7f);
            stream.rectTransform.pivot = new Vector2(.5f, 1);
            var surface = new Image[7];
            for (int i = 0; i < surface.Length; i++) surface[i] = s.Fx("rounded", 9, 3, liquid);
            var bubbles = new Image[4];
            for (int i = 0; i < bubbles.Length; i++) bubbles[i] = s.Fx("bubble", 7 + i % 2 * 3, 7 + i % 2 * 3, Color.white);
            var curls = new Image[3];
            for (int i = 0; i < curls.Length; i++) curls[i] = s.Fx(hot ? "smoke" : "sparkle", hot ? 21 : 18, hot ? 28 : 18, Color.white);
            var splash = Flecks(s, "dot", 9, liquid, .35f, .34f, 8, 3, 5, 8, 90);
            s.Run(LongDuration, (t, previous) =>
            {
                if (!s.Alive) return;
                bool visible = t < .84f;
                float lift = Out(Phase(t, 0, .18f));
                float returnHome = Smooth(Phase(t, .67f, .81f));
                float pour = Phase(t, .25f, .70f);
                float height = Mathf.Lerp(.08f, 1, Smooth(pour));
                float wobble = Mathf.Sin(t * 48) * (1 - pour) * 2;
                Pose(cup, visible, 0, 0, 0, 1, 1);
                Pose(filled, visible, 0, 0, wobble * .4f); filled.fillAmount = height;
                float anticipation = Mathf.Sin(Phase(t, 0, .16f) * Mathf.PI);
                Pose(sourceArt, visible && t < .82f, Mathf.Lerp(94, 55, lift) + returnHome * 35,
                    104 + lift * 25 + returnHome * 22, Mathf.Lerp(-12 * anticipation, 112, Smooth(Phase(t, .12f, .26f))) * (1 - returnHome),
                    1 + anticipation * .09f, 1 - anticipation * .09f, 1 - returnHome);
                float taper = 1 - Smooth(Phase(t, .65f, .75f));
                float grow = Out(Phase(t, .24f, .31f));
                float surfaceY = Mathf.Lerp(-43, 32, height);
                stream.rectTransform.sizeDelta = new Vector2((9 + Mathf.Sin(t * 65) * 1.6f) * taper, (112 - surfaceY) * grow);
                Pose(stream, visible && t >= .24f && t < .75f, 8 + wobble * .3f, 112);
                for (int i = 0; i < surface.Length; i++)
                    Pose(surface[i], visible && t >= .28f && t < .77f, (i - 3) * 7,
                        surfaceY + Mathf.Sin(t * 35 + i * .8f) * 2.3f * taper, Mathf.Cos(t * 35 + i) * 8, 1, 1, .8f * taper);
                for (int i = 0; i < bubbles.Length; i++)
                {
                    float p = Phase(t, .34f + i * .065f, .68f + i * .025f);
                    Pose(bubbles[i], visible && p > 0 && p < 1, -18 + i * 12 + Mathf.Sin(p * 7) * 3,
                        Mathf.Lerp(-32, surfaceY, p), 0, .6f + p * .5f, .6f + p * .5f, Mathf.Sin(p * Mathf.PI) * .7f);
                }
                for (int i = 0; i < curls.Length; i++)
                {
                    float p = Phase(t, .73f + i * .025f, 1.06f);
                    Pose(curls[i], t >= .73f + i * .025f, (i - 1) * 17 + (hot ? Mathf.Sin(p * 8 + i) * 8 : 0),
                        38 + p * (hot ? 43 : 18), hot ? p * 35 : p * 100,
                        hot ? .6f + p : Mathf.Sin(p * Mathf.PI), hot ? .7f + p : Mathf.Sin(p * Mathf.PI), (1 - p) * .65f);
                }
                splash(t);
                Sound(t, previous, .08f, "whoosh", .4f, .85f);
                Sound(t, previous, .27f, "sizzle", .22f, hot ? 1.3f : 1.8f);
                Sound(t, previous, .42f, "pop", .25f, 1.6f);
                Sound(t, previous, .74f, "ding", .55f, hot ? 1.4f : 1.9f);
                if (!hot) Sound(t, previous, .79f, "click", .35f, 1.7f);
                if (Beat(t, previous, .84f)) FinishPop(s, dish);
                if (t >= .84f) Settle(s, t, .84f, LongDuration);
            });
        }

        static Image MakeCup(Stage s, Sprite sprite)
        {
            var image = UIKit.Img("Cup", s.root, sprite, false, true);
            image.type = Image.Type.Simple;
            image.rectTransform.sizeDelta = new Vector2(140, 140);
            image.gameObject.SetActive(false);
            return image;
        }

        static void Stir(Stage s, DishDef dish, string topping)
        {
            var bowl = s.Food("com", 140, 140);
            var mixed = s.Food("d_" + dish.id, 140, 140);
            var food = s.Food(topping, 85, 85);
            var sticks = new Image[2];
            for (int i = 0; i < sticks.Length; i++)
            {
                sticks[i] = s.Fx("rounded", 6, 137, Pal.Hex("D9A066"));
                sticks[i].rectTransform.pivot = new Vector2(.5f, .08f);
                Outline(sticks[i], 1.2f);
            }
            var grains = Flecks(s, "rounded", 7, Pal.Hex("FFF4E1"), .34f, .33f, 0, 12, 4, 8, 105);
            var kimchi = Flecks(s, "rounded", 5, Pal.Tomato, .53f, .28f, 0, 15, 8, 5, 125);
            var flick = Flecks(s, "rounded", 4, Pal.Hex("FFF4E1"), .73f, .25f, 24, 37, 4, 7, 105);
            s.Run(LongDuration, (t, previous) =>
            {
                if (!s.Alive) return;
                bool visible = t < .84f;
                float drop = Phase(t, .06f, .24f);
                float impact = Mathf.Sin(Phase(t, .24f, .36f) * Mathf.PI) * .20f;
                float stir = Phase(t, .31f, .71f);
                float angle = stir * Mathf.PI * 4;
                float envelope = Mathf.Sin(stir * Mathf.PI);
                float wobble = Mathf.Sin(angle) * 6 * envelope;
                float morph = Smooth(Phase(t, .62f, .81f));
                Pose(bowl, visible, 0, -impact * 8, wobble, 1 + impact, 1 - impact, 1 - morph);
                Pose(mixed, visible && t >= .62f, 0, Mathf.Sin(morph * Mathf.PI) * 12, wobble * (1 - morph),
                    1 + Mathf.Sin(morph * Mathf.PI) * .08f, 1 - Mathf.Sin(morph * Mathf.PI) * .06f, morph);
                Pose(food, visible && t < .34f, Mathf.Lerp(-35, 0, drop), Mathf.Lerp(156, 16, drop * drop),
                    Mathf.Lerp(-110, 15, drop), 1 + impact, 1 - impact, 1 - Phase(t, .27f, .34f));
                float enter = Out(Phase(t, .23f, .34f));
                float exit = Smooth(Phase(t, .72f, .82f));
                for (int i = 0; i < sticks.Length; i++)
                {
                    // sin(a), sin(2a) crosses the centre twice: a genuine figure eight.
                    Pose(sticks[i], visible && t >= .23f, 14 + i * 12 + Mathf.Sin(angle) * 27 * envelope + exit * 44,
                        23 + (1 - enter) * 100 + Mathf.Sin(angle * 2) * 12 * envelope + exit * 90,
                        23 - i * 9 + Mathf.Cos(angle) * 13 * envelope - exit * 25, 1, 1, 1 - exit);
                }
                grains(t); kimchi(t); flick(t);
                Sound(t, previous, .06f, "whoosh", .4f, 1.3f);
                Sound(t, previous, .24f, "land", .7f, .95f);
                Sound(t, previous, .33f, "sizzle", .23f, 1.65f);
                Sound(t, previous, .45f, "tick", .4f, 1.1f);
                Sound(t, previous, .59f, "tick", .4f, 1.4f);
                Sound(t, previous, .73f, "whoosh", .4f, 1.7f);
                if (Beat(t, previous, .84f)) FinishPop(s, dish);
                if (t >= .84f) Settle(s, t, .84f, LongDuration);
            });
        }

        static void DropIn(Stage s, DishDef dish, string incoming, string waiting)
        {
            // A shallow outlined prep bowl, with both ingredients visible before stove handoff.
            var bowl = s.Fx("dot", 116, 58, Pal.Hex("C9E5DE")); Outline(bowl, 2);
            var inside = s.Fx("dot", 106, 28, Pal.Hex("F7ECD5")); Outline(inside);
            var a = s.Food(waiting, 63, 63);
            var b = s.Food(incoming, 67, 67);
            var puff = Flecks(s, "smoke", 5, new Color(1, 1, 1, .7f), .18f, .17f, 0, 10, 18, 15, 70);
            var drops = Flecks(s, "dot", 5, LiquidColor(incoming), .20f, .15f, 0, 8, 4, 6, 95);
            s.Run(.43f, (t, previous) =>
            {
                if (!s.Alive) return;
                bool visible = t < .32f;
                float hit = Mathf.Sin(Phase(t, .18f, .31f) * Mathf.PI);
                float wiggle = Mathf.Sin(Phase(t, .20f, .32f) * Mathf.PI * 4) * (1 - Phase(t, .20f, .32f));
                Pose(bowl, visible, 0, -17, wiggle * 5, 1 + hit * .12f, 1 - hit * .13f);
                Pose(inside, visible, 0, 2, wiggle * 3, 1 + hit * .1f, 1);
                float pa = Phase(t, 0, .18f), pb = Phase(t, .04f, .22f);
                Pose(a, visible, Mathf.Lerp(-33, -16, pa), Mathf.Lerp(89, 3, pa * pa), -28 + pa * 44, 1 + hit * .1f, 1 - hit * .25f);
                Pose(b, visible, Mathf.Lerp(43, 17, pb), Mathf.Lerp(125, 7, pb * pb), 42 - pb * 55, 1 + hit * .16f, 1 - hit * .2f);
                puff(t); drops(t);
                Sound(t, previous, 0, "whoosh", .4f, 1.5f);
                Sound(t, previous, .18f, "land", .65f, 1.2f);
                Sound(t, previous, .23f, "pop", .4f, .9f);
                if (Beat(t, previous, .32f)) FinishPop(s, dish);
                if (t >= .32f) Settle(s, t, .32f, .43f);
            });
        }
    }
}
