"""Generate the cutting-board knife. Run: python Tools/gen_knife.py (requires Pillow)."""
from pathlib import Path
from PIL import Image, ImageDraw


def main():
    # Draw large and downsample for smooth, slightly irregular paper-cut edges.
    scale = 4
    image = Image.new("RGBA", (160 * scale, 160 * scale), (0, 0, 0, 0))
    draw = ImageDraw.Draw(image)

    def polygon(points, color):
        draw.polygon([(round(x * scale), round(y * scale)) for x, y in points], fill=color)

    # Broad grey blade, pale cutting edge and a warm brown wooden handle.
    polygon([(13, 63), (88, 58), (95, 100), (23, 107), (12, 101)], "#596365")
    polygon([(16, 66), (86, 62), (91, 95), (24, 101), (16, 98)], "#ADB5B0")
    polygon([(19, 94), (91, 89), (92, 98), (23, 104), (15, 100)], "#DDE0CF")
    polygon([(83, 64), (94, 62), (96, 83), (88, 85)], "#667071")
    polygon([(91, 59), (139, 56), (148, 61), (149, 79), (141, 85), (94, 85)], "#68452F")
    polygon([(97, 62), (137, 60), (143, 64), (143, 77), (138, 79), (98, 81)], "#996846")
    polygon([(101, 64), (137, 62), (138, 66), (101, 68)], "#B58255")
    for x, y in [(106, 73), (133, 70)]:
        draw.ellipse(((x - 2) * scale, (y - 2) * scale, (x + 2) * scale, (y + 2) * scale), fill="#D5BA81")

    target = Path(__file__).resolve().parents[1] / "Assets/Resources/FX/knife.png"
    target.parent.mkdir(parents=True, exist_ok=True)
    image.resize((160, 160), Image.Resampling.LANCZOS).save(target)
    print(target)


if __name__ == "__main__":
    main()
