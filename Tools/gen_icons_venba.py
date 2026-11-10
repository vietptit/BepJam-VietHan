"""Generate review-only Venba-style icons; requires Pillow and numpy only.

Run from any directory: python Tools/gen_icons_venba.py
All shapes are drawn at 4x and reduced with premultiplied-alpha Lanczos.
No external fonts, images, network access, timestamps, or randomized Python
hashes are used. The built-in Pillow font is used only for contact-sheet labels.
Outputs: 18 transparent 192x192 PNGs and icons_preview.png, exclusively in
Tools/review/icons/. This script never imports/runs the UI sprite generator.
"""

from pathlib import Path
import math

import numpy as np
from PIL import Image, ImageDraw, ImageFont


SCALE = 4
SIZE = (192, 192)
SEED = 271828
OUT = Path(__file__).resolve().parent / "review" / "icons"
PALETTE = {
    "paper": "#F6ECD9", "ink": "#3B2A3A", "red": "#D9414F",
    "pink": "#E8829A", "plum": "#7A3550", "teal": "#4E8E8A",
    "mustard": "#E3A33B", "terracotta": "#C8573E",
}
NAMES = (
    "home", "bulb", "undo", "book", "gear", "pause", "play", "retry",
    "music", "sound", "star_full", "star_empty", "coin", "lock", "clock",
    "heart", "check", "cross",
)


def rgb(value):
    value = PALETTE.get(value, value).lstrip("#")
    return tuple(int(value[i:i + 2], 16) for i in (0, 2, 4))


def mix(a, b, amount):
    return tuple(round(x * (1 - amount) + y * amount)
                 for x, y in zip(rgb(a) if isinstance(a, str) else a,
                                 rgb(b) if isinstance(b, str) else b))


def canvas(size=SIZE):
    return Image.new("RGBA", (size[0] * SCALE, size[1] * SCALE))


def polygon(draw, points, fill):
    draw.polygon([(round(x * SCALE), round(y * SCALE)) for x, y in points],
                 fill=fill)


def rough_path(corners, rng, rough=.75):
    """Reuse gen_ui_venba.rectangle_path's coarse + fine edge displacement.

    Extend its hand-cut construction to arbitrary closed silhouettes. Keep
    vertices fixed so arrow tips, star points and joins stay recognizable.
    """
    points = []
    for index, corner in enumerate(corners):
        a = np.asarray(corner, dtype=float)
        b = np.asarray(corners[(index + 1) % len(corners)], dtype=float)
        delta = b - a
        length = float(np.linalg.norm(delta))
        if length < .001:
            continue
        count = max(3, int(length / 2.5))
        coarse = rng.uniform(-rough, rough, max(3, count // 5))
        offsets = np.interp(np.linspace(0, 1, count),
                            np.linspace(0, 1, len(coarse)), coarse)
        offsets += rng.uniform(-rough * .4, rough * .4, count)
        normal = np.array([-delta[1], delta[0]]) / length
        ts = np.linspace(0, 1, count, endpoint=False)
        for t, displacement in zip(ts, offsets):
            p = a + delta * t + normal * displacement * min(1, t * 8, (1 - t) * 8)
            points.append(tuple(p))
    return points


def circle_path(centre, radii, rng):
    """Adapt gen_ui_venba.circle_path's two-frequency pigment edge jitter."""
    cx, cy = centre
    rx, ry = radii
    angles = np.linspace(0, 2 * math.pi, 240, endpoint=False)
    coarse = rng.uniform(-.9, .9, 25)
    coarse[-1] = coarse[0]
    jitter = np.interp(angles, np.linspace(0, 2 * math.pi, 25), coarse)
    jitter += rng.uniform(-.35, .35, len(angles))
    return [(cx + (rx + j) * math.cos(t), cy + (ry + j) * math.sin(t))
            for t, j in zip(angles, jitter)]


def texture(image, rng):
    """Reuse the UI generator's fine pigment noise and low-opacity speckles.

    Clip every speckle to the original silhouette (including negative spaces).
    Tiny transparent pinholes add dry pigment without changing the flat fill.
    """
    original = np.array(image)
    data = original.astype(np.float32)
    noise = rng.normal(0, 1.15, data.shape[:2])
    data[:, :, :3] = np.clip(data[:, :, :3] + noise[:, :, None], 0, 255)
    image = Image.fromarray(data.astype(np.uint8))
    layer = canvas()
    draw = ImageDraw.Draw(layer)
    for _ in range(int(SIZE[0] * SIZE[1] / 145)):
        x, y = rng.uniform(4, 188), rng.uniform(4, 188)
        radius = rng.uniform(.18, .55)
        colour = (*rgb("paper"), int(rng.integers(22, 57)))
        draw.ellipse(((x - radius) * SCALE, (y - radius) * SCALE,
                      (x + radius) * SCALE, (y + radius) * SCALE), fill=colour)
    image = Image.alpha_composite(image, layer)
    result = np.array(image)
    result[:, :, 3] = original[:, :, 3]
    holes = rng.random(result.shape[:2]) < .008
    result[:, :, 3][holes] = (result[:, :, 3][holes] * .45).astype(np.uint8)
    return Image.fromarray(result)


def downsample(image, size):
    # Reused from gen_ui_venba: no dark fringe on a coloured button.
    return image.convert("RGBa").resize(size, Image.Resampling.LANCZOS).convert("RGBA")


class Shapes:
    """Filled mask primitives only: no outlines, bevels, or gradient shading."""

    def __init__(self, rng):
        self.rng = rng
        self.mask = Image.new("L", (SIZE[0] * SCALE, SIZE[1] * SCALE))
        self.draw = ImageDraw.Draw(self.mask)

    def poly(self, points, fill=255):
        polygon(self.draw, rough_path(points, self.rng), fill)

    def box(self, left, top, right, bottom, fill=255):
        self.poly([(left, top), (right, top), (right, bottom), (left, bottom)], fill)

    def oval(self, cx, cy, rx, ry=None, fill=255):
        polygon(self.draw, circle_path((cx, cy), (rx, rx if ry is None else ry),
                                       self.rng), fill)

    def arc(self, cx, cy, radius, width, start, end):
        angles = np.linspace(math.radians(start), math.radians(end), 90)
        outer = [(cx + (radius + width / 2) * math.cos(t),
                  cy + (radius + width / 2) * math.sin(t)) for t in angles]
        inner = [(cx + (radius - width / 2) * math.cos(t),
                  cy + (radius - width / 2) * math.sin(t)) for t in angles[::-1]]
        self.poly(outer + inner)

    def arrow(self, cx, cy, radius, width, start, end):
        self.arc(cx, cy, radius, width, start, end)
        theta = math.radians(end)
        direction = 1 if end > start else -1
        tangent = np.array([-math.sin(theta), math.cos(theta)]) * direction
        radial = np.array([math.cos(theta), math.sin(theta)])
        point = np.array([cx, cy]) + radial * radius
        self.poly([tuple(point + tangent * 19),
                   tuple(point - tangent * 10 + radial * 24),
                   tuple(point - tangent * 10 - radial * 24)])


def star_points(cx=96, cy=96, outer=77, inner=35):
    return [(cx + (outer if i % 2 == 0 else inner) * math.cos(-math.pi / 2 + i * math.pi / 5),
             cy + (outer if i % 2 == 0 else inner) * math.sin(-math.pi / 2 + i * math.pi / 5))
            for i in range(10)]


def make_icon(name, rng):
    shape = Shapes(rng)
    detail = Shapes(rng)
    colour = "ink"
    detail_colour = "paper"
    if name == "home":
        colour = "red"
        shape.poly([(20, 88), (96, 22), (172, 88), (151, 88),
                    (151, 160), (41, 160), (41, 88)])
        shape.box(82, 111, 110, 163, 0)
        detail.box(53, 96, 72, 117)
    elif name == "bulb":
        colour = "mustard"
        shape.oval(96, 76, 49, 51)
        shape.poly([(60, 101), (132, 101), (119, 136), (73, 136)])
        shape.box(75, 141, 117, 153)
        shape.poly([(79, 157), (113, 157), (105, 168), (87, 168)])
        detail.poly([(81, 75), (94, 92), (110, 72), (115, 78),
                     (101, 101), (101, 125), (91, 125), (91, 102), (75, 81)])
    elif name == "undo":
        colour = "plum"
        shape.arrow(96, 91, 51, 20, 20, -180)
        shape.oval(96, 147, 60, 17)
        detail.oval(96, 142, 42, 5)
    elif name == "book":
        colour = "teal"
        shape.poly([(22, 39), (64, 34), (96, 48), (128, 34), (170, 39),
                    (170, 151), (129, 146), (96, 160), (63, 146), (22, 151)])
        # A negative gutter preserves the open-book silhouette at 64 pixels.
        shape.box(92, 51, 100, 153, 0)
        detail.poly([(36, 52), (61, 49), (80, 57), (80, 103), (60, 95), (36, 98)])
        for y in (112, 129):
            detail.poly([(36, y), (62, y - 2), (80, y + 5),
                         (80, y + 11), (60, y + 5), (36, y + 7)])
        for y in (61, 87, 113):
            detail.poly([(114, y + 4), (133, y - 3), (155, y),
                         (155, y + 9), (134, y + 6), (114, y + 13)])
    elif name == "gear":
        colour = "teal"
        points = []
        for tooth in range(10):
            for fraction, radius in ((0, 59), (.17, 75), (.64, 75), (.81, 59)):
                angle = (tooth + fraction) * math.tau / 10
                points.append((96 + radius * math.cos(angle), 96 + radius * math.sin(angle)))
        shape.poly(points)
        shape.oval(96, 96, 25, fill=0)
    elif name == "pause":
        colour = "plum"
        shape.box(48, 30, 82, 160)
        shape.box(110, 30, 144, 160)
    elif name == "play":
        colour = "teal"
        shape.poly([(46, 24), (164, 95), (46, 166)])
    elif name == "retry":
        colour = "red"
        shape.arrow(96, 94, 56, 23, -135, 140)
    elif name == "music":
        colour = "plum"
        shape.poly([(65, 48), (153, 25), (153, 139), (134, 139),
                    (134, 65), (84, 79), (84, 150), (65, 150)])
        shape.oval(60, 150, 25, 18)
        shape.oval(129, 139, 25, 18)
    elif name == "sound":
        colour = "ink"
        shape.poly([(24, 75), (53, 75), (95, 38), (95, 154), (53, 117), (24, 117)])
        shape.arc(89, 96, 40, 13, -54, 54)
        shape.arc(89, 96, 70, 14, -54, 54)
    elif name in ("star_full", "star_empty"):
        # Both stars use the exact same path, grain and shadow coverage.
        colour = "mustard" if name == "star_full" else mix("paper", "ink", .25)
        shape.poly(star_points())
    elif name == "coin":
        colour = "mustard"
        shape.oval(96, 94, 70)
        # A broad stamped glint, rather than a thin outlined coin rim.
        detail.poly([(66, 50), (88, 43), (82, 64), (68, 78), (55, 73)])
        detail.poly([(100, 61), (117, 61), (100, 87), (124, 87),
                     (90, 128), (99, 101), (78, 101)])
    elif name == "lock":
        colour = "mustard"
        shape.oval(96, 76, 43, 51)
        shape.oval(96, 76, 23, 31, fill=0)
        shape.box(38, 83, 154, 161)
        shape.oval(96, 114, 10, fill=0)
        shape.poly([(91, 116), (101, 116), (105, 140), (87, 140)], 0)
    elif name == "clock":
        colour = "teal"
        shape.oval(96, 94, 72)
        detail.poly([(89, 46), (102, 46), (102, 87), (132, 104),
                     (125, 116), (89, 96)])
        for x, y in ((96, 33), (157, 94), (96, 155), (35, 94)):
            detail.oval(x, y, 4)
    elif name == "heart":
        colour = "red"
        # Sample two cubic lobes, then meet at one broad pointed base.
        points = []
        segments = [((96, 57), (51, -1), (3, 49), (33, 97)),
                    ((33, 97), (48, 122), (77, 150), (96, 167)),
                    ((96, 167), (115, 150), (144, 122), (159, 97)),
                    ((159, 97), (189, 49), (141, -1), (96, 57))]
        for a, b, c, d in segments:
            for t in np.linspace(0, 1, 45, endpoint=False):
                p = ((1 - t) ** 3 * np.array(a) + 3 * (1 - t) ** 2 * t * np.array(b)
                     + 3 * (1 - t) * t ** 2 * np.array(c) + t ** 3 * np.array(d))
                points.append(tuple(p))
        shape.poly(points)
    elif name == "check":
        colour = "teal"
        shape.poly([(23, 98), (45, 77), (77, 110), (146, 38),
                    (169, 60), (78, 155)])
    elif name == "cross":
        colour = "red"
        shape.poly([(49, 27), (96, 74), (143, 27), (165, 49), (118, 96),
                    (165, 143), (143, 165), (96, 118), (49, 165),
                    (27, 143), (74, 96), (27, 49)])
    else:
        raise ValueError("Unknown icon: " + name)

    base_colour = rgb(colour) if isinstance(colour, str) else colour
    # One flat, slightly offset underprint. Never stroke the silhouette.
    shadow_colour = mix(base_colour, "ink", .28)
    image = canvas()
    shadow = canvas()
    shadow.paste((*shadow_colour, 255), (0, 4 * SCALE), shape.mask)
    image = Image.alpha_composite(image, shadow)
    face = canvas()
    face.paste((*base_colour, 255), (0, 0), shape.mask)
    image = Image.alpha_composite(image, face)
    # Internal printed marks are clipped to the face, including its cutouts.
    clipped = Image.fromarray(np.minimum(np.array(detail.mask), np.array(shape.mask)))
    marks = canvas()
    marks.paste((*rgb(detail_colour), 255), (0, 0), clipped)
    image = Image.alpha_composite(image, marks)
    return downsample(texture(image, rng), SIZE)


def preview(sprites):
    """Terracotta contact sheet with 192px art and 64px button checks."""
    width, height = 1200, 1480
    image = canvas((width, height))
    image.paste((*rgb("terracotta"), 255), (0, 0, image.width, image.height))
    font = ImageFont.load_default()

    def label(x, y, text, factor=1):
        bbox = font.getbbox(text)
        tile = Image.new("RGBA", (bbox[2] - bbox[0] + 4, bbox[3] - bbox[1] + 4))
        ImageDraw.Draw(tile).text((2 - bbox[0], 2 - bbox[1]), text,
                                 font=font, fill=rgb("paper"))
        tile = tile.resize((tile.width * factor * SCALE, tile.height * factor * SCALE),
                           Image.Resampling.NEAREST)
        image.alpha_composite(tile, (x * SCALE, y * SCALE))

    label(24, 16, "PAPER / PIGMENT - ICON REVIEW", 2)
    label(24, 51, "192px RGBA | 4x drawing | 64px checks on cream / teal")
    for index, name in enumerate(NAMES):
        x, y = 24 + (index % 5) * 236, 90 + (index // 5) * 344
        label(x, y, name)
        sprite = sprites[name]
        tile = sprite.convert("RGBa").resize((192 * SCALE, 192 * SCALE),
                                              Image.Resampling.LANCZOS).convert("RGBA")
        image.alpha_composite(tile, ((x + 10) * SCALE, (y + 25) * SCALE))
        # Downsample to the actual 64px review size before enlarging for sheet.
        small = downsample(sprite, (64, 64))
        small = small.resize((64 * SCALE, 64 * SCALE), Image.Resampling.NEAREST)
        for offset, colour in ((12, "paper"), (122, "teal")):
            button = Image.new("RGBA", (88 * SCALE, 88 * SCALE), rgb(colour))
            button.alpha_composite(small, (12 * SCALE, 12 * SCALE))
            image.alpha_composite(button, ((x + offset) * SCALE, (y + 232) * SCALE))
    return downsample(image, (width, height))


def main():
    sprites = {}
    for index, name in enumerate(NAMES):
        # Fixed independent seeds; the two stars deliberately share everything
        # except pigment colour. No dependence on Python's randomized hash().
        seed = NAMES.index("star_full") if name == "star_empty" else index
        sprite = make_icon(name, np.random.default_rng(SEED + 1000 + seed))
        assert sprite.mode == "RGBA" and sprite.size == SIZE, name
        bbox = sprite.getchannel("A").getbbox()
        assert bbox is not None, name + ": empty icon"
        assert min(bbox[0], bbox[1], 192 - bbox[2], 192 - bbox[3]) >= 12, (
            name + ": insufficient transparent margin")
        sprites[name] = sprite
    assert np.array_equal(np.array(sprites["star_full"].getchannel("A")),
                          np.array(sprites["star_empty"].getchannel("A")))
    sheet = preview(sprites)
    # Build and validate everything in memory before writing review outputs.
    OUT.mkdir(parents=True, exist_ok=True)
    for name, sprite in sprites.items():
        sprite.save(OUT / (name + ".png"), format="PNG", optimize=False)
    sheet.save(OUT / "icons_preview.png", format="PNG", optimize=False)
    print("Wrote %d icons and icons_preview.png to %s" % (len(sprites), OUT))


if __name__ == "__main__":
    main()
