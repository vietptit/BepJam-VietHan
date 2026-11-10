using UnityEngine;

namespace BepJam
{
    /// <summary>A padded recipe equation shared by tips, recipe cards and the notebook.</summary>
    public static class RecipeUI
    {
        public static void Draw(RectTransform row, DishDef dish, float width, float maxIcon,
            bool names = true, bool animate = false, float delay = 0, bool unscaled = false, bool resultRays = false)
        {
            int count = dish.parts.Length + 1;
            float gap = Mathf.Min(32, width / (count * 4));
            float size = Mathf.Max(12, Mathf.Min(maxIcon * Mathf.Min(1, 4f / count),
                (width - 32f - (count - 1) * gap) / count, row.rect.height - (names ? 54 : 4)));
            float total = size * count + gap * (count - 1);
            for (int i = 0; i < count; i++)
            {
                bool result = i == count - 1;
                float x = -total * 0.5f + size * 0.5f + i * (size + gap);
                var holder = UIKit.Node("Part" + i, row).Anchor(new Vector2(0.5f, 0.5f),
                    new Vector2(x, names ? 25 : 0), new Vector2(size, size));
                if (result && resultRays)
                {
                    var rays = UIKit.Img("Rays", holder, Art.FX("rays"), false, false);
                    rays.rectTransform.Stretch();
                    rays.color = new Color(1f, 0.85f, 0.4f, 0.55f);
                    rays.gameObject.AddComponent<Spin>().speed = 25;
                }
                var icon = UIKit.Img("Icon", holder, result ? dish.Icon : Art.Food(dish.parts[i]));
                icon.rectTransform.Stretch();
                if (names)
                {
                    var label = UIKit.Label("Name", holder, result ? dish.ShortName : GameData.Ingredients[dish.parts[i]].name, 28, Pal.TextBrown);
                    label.rectTransform.Anchor(new Vector2(0.5f, 0), new Vector2(0, -24), new Vector2(size + gap - 8, 44));
                    UIKit.FitLines(label, 28, 14, 2);
                }
                if (animate)
                {
                    holder.localScale = Vector3.zero;
                    Tween.Scale(holder, Vector3.one, 0.35f).SetEase(Ease.OutCubic).SetDelay(delay + i * 0.1f).SetUnscaled(unscaled);
                }
                if (!result)
                {
                    var op = UIKit.Label("Operator", row, i == count - 2 ? "=" : "+", 36, Pal.Coral);
                    op.rectTransform.Anchor(new Vector2(0.5f, 0.5f), new Vector2(x + (size + gap) * 0.5f, names ? 25 : 0), new Vector2(gap, 48));
                    UIKit.Fit(op, 36, 20);
                }
            }
        }
    }
}
