"""Generate review-only, text-free Venba-inspired UI sprites.

Run from any directory: python Tools/gen_ui_venba.py
Dependencies: Pillow and numpy (plus the Python standard library).
Every illustration is drawn at 3x and reduced with Lanczos. No external fonts,
reference images, network access, timestamps, or Python hash seeds are used.
The only generated files go beside this script in review/ui/, never Assets/.
"""

from pathlib import Path
import json
import math

import numpy as np
from PIL import Image, ImageDraw, ImageFont


SCALE = 3
SEED = 271828
OUT = Path(__file__).resolve().parent / "review" / "ui"
PALETTE = {
    "paper": "#F6ECD9", "aged": "#EADBC2", "ink": "#3B2A3A",
    "red": "#D9414F", "pink": "#E8829A", "plum": "#7A3550",
    "purple": "#6B4AA0", "teal": "#4E8E8A", "mustard": "#E3A33B",
    "terracotta": "#C8573E",
}


def rgb(value):
    value = PALETTE.get(value, value).lstrip("#")
    return tuple(int(value[i:i + 2], 16) for i in (0, 2, 4))


def mix(a, b, amount):
    return tuple(round(x * (1 - amount) + y * amount)
                 for x, y in zip(rgb(a) if isinstance(a, str) else a,
                                 rgb(b) if isinstance(b, str) else b))


def canvas(size):
    return Image.new("RGBA", (size[0] * SCALE, size[1] * SCALE))


def polygon(draw, points, fill):
    draw.polygon([(round(x * SCALE), round(y * SCALE)) for x, y in points],
                 fill=fill)


def line(draw, points, fill, width=1):
    draw.line([(round(x * SCALE), round(y * SCALE)) for x, y in points],
              fill=fill, width=max(1, round(width * SCALE)), joint="curve")


def rectangle_path(size, rng, inset=5, rough=2, ends_only=False,
                   torn_corner=False, bottom_offset=0):
    """A hand-cut polygon, with independent coarse and fine edge displacement."""
    w, h = size
    left, right = inset, w - 1 - inset
    top, bottom = inset, h - 1 - inset - bottom_offset
    corners = [(left, top), (right, top), (right, bottom), (left, bottom)]
    points = []
    for side in range(4):
        a, b = np.array(corners[side]), np.array(corners[(side + 1) % 4])
        count = max(3, int(np.linalg.norm(b - a) / 2.5))
        coarse = rng.uniform(-rough, rough, max(3, count // 5))
        offsets = np.interp(np.linspace(0, 1, count),
                            np.linspace(0, 1, len(coarse)), coarse)
        offsets += rng.uniform(-rough * .4, rough * .4, count)
        if ends_only and side in (0, 2):
            offsets *= .22
        normal = np.array([0, 1]) if side in (0, 2) else np.array([1, 0])
        for t, displacement in zip(np.linspace(0, 1, count, endpoint=False), offsets):
            p = a + (b - a) * t + normal * displacement
            # Diagonal missing bottom-right corner, safely inside the border.
            if torn_corner and p[0] > w - 34 and p[1] > h - 34:
                excess = p[0] + p[1] - (w + h - 44)
                if excess > 0:
                    p = p - np.array([excess / 2, excess / 2])
            points.append(tuple(p))
    return points


def circle_path(size, rng, radius, cy=None):
    w, h = size
    cy = h / 2 if cy is None else cy
    angles = np.linspace(0, 2 * math.pi, 240, endpoint=False)
    coarse = rng.uniform(-1.5, 1.5, 25)
    coarse[-1] = coarse[0]
    jitter = np.interp(angles, np.linspace(0, 2 * math.pi, 25), coarse)
    jitter += rng.uniform(-.55, .55, len(angles))
    return [(w / 2 + (radius + j) * math.cos(t),
             cy + (radius + j) * math.sin(t)) for t, j in zip(angles, jitter)]


def border_weight(size, borders):
    """Exactly zero throughout the 9-slice centre; feather toward its boundary."""
    w, h = size
    left, bottom, right, top = borders
    y, x = np.mgrid[0:h * SCALE, 0:w * SCALE].astype(np.float32) / SCALE
    weight = np.maximum.reduce([
        np.clip((left - x) / max(left, 1), 0, 1),
        np.clip((x - (w - right - 1)) / max(right, 1), 0, 1),
        np.clip((top - y) / max(top, 1), 0, 1),
        np.clip((y - (h - bottom - 1)) / max(bottom, 1), 0, 1),
    ])
    return weight


def texture(image, rng, borders=None, paper=False):
    """Fine pigment and fibres, confined to border zones for sliced sprites."""
    size = (image.width // SCALE, image.height // SCALE)
    weight = (border_weight(size, borders) if borders else
              np.ones((image.height, image.width), dtype=np.float32))
    original = np.array(image)
    data = original.astype(np.float32)
    noise = rng.normal(0, 1.65 if paper else 1.15, weight.shape)
    if paper:
        # A shallow edge stain, not a bevel or outline.
        delta = noise - 11 * weight ** 2
    else:
        delta = noise
    data[:, :, :3] = np.clip(data[:, :, :3] + (delta * weight)[:, :, None], 0, 255)
    image = Image.fromarray(data.astype(np.uint8))
    w, h = size
    layer = canvas(size)
    ld = ImageDraw.Draw(layer)
    for _ in range(int(w * h / (320 if paper else 230))):
        x, y = rng.uniform(4, w - 4), rng.uniform(4, h - 4)
        strength = float(weight[min(int(y * SCALE), image.height - 1),
                                min(int(x * SCALE), image.width - 1)])
        if rng.random() > strength:
            continue
        colour = (*rgb("ink" if paper else "paper"), int(12 + 32 * strength))
        # Draw on a separate layer so low-opacity pigment composites correctly.
        if paper and rng.random() < .65:
            line(ld, [(x, y), (x + rng.uniform(1, 4), y + rng.uniform(-.6, .6))],
                 colour, .25)
        else:
            radius = rng.uniform(.18, .48)
            ld.ellipse(((x - radius) * SCALE, (y - radius) * SCALE,
                        (x + radius) * SCALE, (y + radius) * SCALE), fill=colour)
    image = Image.alpha_composite(image, layer)
    if borders:
        # Fibres can cross their starting pixel: explicitly protect the centre.
        result = np.array(image)
        result[weight == 0] = original[weight == 0]
        image = Image.fromarray(result)
    return image


def clip_to(image, mask):
    image.putalpha(Image.fromarray(np.minimum(np.array(image.getchannel("A")),
                                             np.array(mask)).astype(np.uint8)))
    return image


def crayon(image, points, colour, rng, width=1.5, opacity=65):
    layer = Image.new("RGBA", image.size)
    draw = ImageDraw.Draw(layer)
    for _ in range(3):
        shifted = [(x + rng.uniform(-.65, .65), y + rng.uniform(-.65, .65))
                   for x, y in points]
        line(draw, shifted, (*rgb(colour), opacity), width / 2)
    return Image.alpha_composite(image, layer)


def paper(size, borders, rng, corner=False, locked=False):
    im = canvas(size)
    path = rectangle_path(size, rng, inset=7, rough=4, torn_corner=corner)
    polygon(ImageDraw.Draw(im), path, rgb("paper") if not locked else (216, 209, 196))
    mask = im.getchannel("A")
    im = texture(im, rng, borders, paper=True)
    w, h = size
    if corner:
        # Mustard washi tape is contained entirely in the top border.
        tape = [(w * .28, 5), (w * .73, 9), (w * .71, 39),
                (w * .67, 37), (w * .64, 40), (w * .26, 35)]
        polygon(ImageDraw.Draw(im), tape,
                mix("mustard", "paper", .44) if not locked else (181, 173, 155))
        for x in range(int(w * .30), int(w * .69), 9):
            im = crayon(im, [(x, 13), (x - 3, 29)], "paper", rng, .65, 48)
    if locked:
        # Two small crayon patches leave the stretchable centre completely flat.
        for x0, y0 in ((17, 174), (202, 65)):
            for i in range(5):
                im = crayon(im, [(x0 + 5 * i, y0), (x0 + 5 * i + 14, y0 + 32)],
                            "ink", rng, 1.1, 29)
                im = crayon(im, [(x0 + 5 * i, y0 + 32), (x0 + 5 * i + 14, y0)],
                            "ink", rng, .9, 23)
    return clip_to(im, mask)


def flat_panel(size, borders, colour, rng, ends_only=False):
    im = canvas(size)
    polygon(ImageDraw.Draw(im), rectangle_path(size, rng, rough=2.3,
                                              ends_only=ends_only), rgb(colour))
    mask = im.getchannel("A")
    return clip_to(texture(im, rng, borders), mask)


def button(size, borders, colour, rng, round_button=False):
    im = canvas(size)
    draw = ImageDraw.Draw(im)
    if round_button:
        path = circle_path(size, rng, 81, cy=89)
    else:
        path = rectangle_path(size, rng, inset=7, rough=2, bottom_offset=10)
    polygon(draw, [(x, y + 10) for x, y in path], mix(colour, "ink", .28))
    polygon(draw, path, rgb(colour))
    mask = im.getchannel("A")
    im = texture(im, rng, borders)
    if round_button:
        points = [(52, 37), (70, 29), (91, 27), (112, 30)]
    else:
        points = [(24, 19), (size[0] * .30, 17.5),
                  (size[0] * .60, 18), (size[0] - 29, 20)]
    im = crayon(im, points, "paper", rng, 2.1, 43)
    return clip_to(im, mask)


def pointer(size, colour, rng):
    im = canvas(size)
    # Wide, fully opaque top overlaps the box edge by 3px when assembled.
    polygon(ImageDraw.Draw(im), [(3, 0), (61, 0), (37, 44), (34, 45)], rgb(colour))
    mask = im.getchannel("A")
    im = texture(im, rng)
    # Preserve an exact-colour attachment band to avoid a seam.
    polygon(ImageDraw.Draw(im), [(3, 0), (61, 0), (58, 4), (6, 4)], rgb(colour))
    return clip_to(im, mask)


def badge(size, rng):
    im = canvas(size)
    polygon(ImageDraw.Draw(im), circle_path(size, rng, 41), rgb("red"))
    mask = im.getchannel("A")
    return clip_to(texture(im, rng), mask)


def stamp(size, rng):
    im = canvas(size)
    draw = ImageDraw.Draw(im)
    outer = circle_path(size, rng, 111)
    polygon(draw, outer, (*rgb("red"), 205))
    inner = circle_path(size, rng, 100)
    polygon(draw, inner, (0, 0, 0, 0))
    # A second broken, dry-ink ring: no text, stars, or solid central fill.
    angles = np.linspace(0, 2 * math.pi, 320)
    for i in range(len(angles) - 1):
        if rng.random() < .18 or 65 < i < 77 or 202 < i < 217:
            continue
        pts = [(128 + 91 * math.cos(t), 128 + 91 * math.sin(t))
               for t in angles[i:i + 2]]
        line(draw, pts, (*rgb("red"), int(rng.integers(75, 165))), 1.5)
    a = np.array(im)
    # Uneven pigment coverage, including tiny transparent holes in the ink.
    pigment = rng.uniform(.45, 1, a.shape[:2])
    pigment[rng.random(a.shape[:2]) < .16] = 0
    a[:, :, 3] = (a[:, :, 3].astype(np.float32) * pigment).astype(np.uint8)
    im = Image.fromarray(a)
    erase = ImageDraw.Draw(im)
    for _ in range(65):
        angle = rng.uniform(0, 2 * math.pi)
        r = rng.uniform(101, 111)
        x, y = 128 + r * math.cos(angle), 128 + r * math.sin(angle)
        line(erase, [(x, y), (x + rng.uniform(-2, 2), y + rng.uniform(1, 3))],
             (0, 0, 0, 0), rng.uniform(.3, 1.1))
    return im


def downsample(im, size):
    # Premultiplied alpha prevents dark/coloured fringes on transparent edges.
    return im.convert("RGBa").resize(size, Image.Resampling.LANCZOS).convert("RGBA")


def preview(sprites):
    """Labels exist only in this contact sheet, never inside a sprite."""
    size = (1440, 1580)
    im = Image.new("RGBA", (size[0] * SCALE, size[1] * SCALE), rgb("terracotta"))
    font = ImageFont.load_default()

    def label(x, y, text, factor=2):
        # Use Pillow's built-in font, avoiding machine-specific font discovery.
        bbox = font.getbbox(text)
        tile = Image.new("RGBA", (bbox[2] - bbox[0] + 4, bbox[3] - bbox[1] + 4))
        ImageDraw.Draw(tile).text((2 - bbox[0], 2 - bbox[1]), text,
                                 font=font, fill=rgb("paper"))
        tile = tile.resize((tile.width * factor * SCALE, tile.height * factor * SCALE),
                           Image.Resampling.NEAREST)
        im.alpha_composite(tile, (x * SCALE, y * SCALE))

    label(28, 18, "PAPER / PIGMENT - UI REVIEW", 2)
    label(28, 52, "3x drawing | transparent PNG | labels on this sheet only", 1)
    for index, (name, sprite) in enumerate(sprites.items()):
        col, row = index % 4, index // 4
        x, y = 24 + col * 354, 94 + row * 292
        label(x + 4, y + 2, name, 1)
        label(x + 4, y + 21, "%d x %d" % sprite.size, 1)
        fit = min(310 / sprite.width, 232 / sprite.height, 1)
        display_size = (max(1, round(sprite.width * fit * SCALE)),
                        max(1, round(sprite.height * fit * SCALE)))
        tile = sprite.convert("RGBa").resize(display_size, Image.Resampling.LANCZOS).convert("RGBA")
        im.alpha_composite(tile, (round((x + 164) * SCALE - tile.width / 2),
                                  round((y + 161) * SCALE - tile.height / 2)))
    return downsample(im, size)


def main():
    # Fixed per-sprite seeds ensure unrelated assets do not change when a new
    # sprite is added. Never use hash(name), which varies between Python runs.
    specs = [
        ("paper_panel", (512, 512), (90, 90, 90, 90), "paper", 1),
        ("paper_tag", (512, 160), (90, 50, 90, 50), "tag", 2),
        ("hud_bar", (512, 128), (70, 34, 70, 34), "paper", 3),
        ("btn_red", (384, 144), (56, 56, 56, 56), "red", 4),
        ("btn_teal", (384, 144), (56, 56, 56, 56), "teal", 5),
        ("btn_mustard", (384, 144), (56, 56, 56, 56), "mustard", 6),
        ("btn_plum", (384, 144), (56, 56, 56, 56), "plum", 7),
        ("btn_round_cream", (192, 192), (88, 88, 88, 88), "paper", 8),
        ("btn_round_red", (192, 192), (88, 88, 88, 88), "red", 9),
        ("speech_pink", (384, 192), (48, 48, 48, 48), "pink", 10),
        ("speech_pointer_pink", (64, 48), None, "pink", 11),
        ("speech_cream", (384, 192), (48, 48, 48, 48), "paper", 12),
        ("speech_pointer_cream", (64, 48), None, "paper", 13),
        ("badge_red", (96, 96), None, "red", 14),
        ("level_card", (256, 256), (60, 60, 60, 60), "paper", 15),
        ("level_card_locked", (256, 256), (60, 60, 60, 60), "paper", 15),
        ("ribbon_stamp", (256, 256), None, "red", 17),
    ]
    sprites = {}
    manifest = {
        "border_order": ["L", "B", "R", "T"],
        "units": "final PNG pixels; bottom-left sprite convention",
        "seed": SEED, "drawing_scale": SCALE,
        "notes": [
            "Nine-slice centres are opaque flat colour; texture is in the border zones.",
            "Uniform assets use zero slice borders; preserve their aspect ratio.",
            "Round buttons have safe 88px slice borders; prefer uniform scaling to preserve circles.",
            "Overlap each downward speech pointer's top by 3px with its matching box.",
            "Lanczos filtering has a 3px support radius next to a border boundary.",
            "Sprites contain no text; ui_preview.png is a labelled contact sheet.",
        ],
        "sprites": {},
    }
    for name, size, borders, colour, seed in specs:
        rng = np.random.default_rng(SEED + seed)
        if name.startswith("level_card"):
            im = paper(size, borders, rng, corner=True, locked=name.endswith("locked"))
        elif name in ("paper_panel", "hud_bar"):
            im = paper(size, borders, rng)
        elif name == "paper_tag":
            im = flat_panel(size, borders, "red", rng, ends_only=True)
        elif name.startswith("btn_"):
            im = button(size, borders, colour, rng, round_button="round" in name)
        elif name.startswith("speech_pointer"):
            im = pointer(size, colour, rng)
        elif name.startswith("speech_"):
            im = flat_panel(size, borders, colour, rng)
        elif name == "badge_red":
            im = badge(size, rng)
        else:
            im = stamp(size, rng)
        sprite = downsample(im, size)
        assert sprite.mode == "RGBA" and sprite.size == size
        if borders:
            left, bottom, right, top = borders
            # Verify a meaningful interior patch, excluding resampling support.
            centre = np.array(sprite)[top + 4:size[1] - bottom - 4,
                                      left + 4:size[0] - right - 4]
            assert centre.size and np.all(centre == centre[0, 0]), name + ": non-flat centre"
            assert centre[0, 0, 3] == 255, name + ": translucent centre"
        sprites[name + ".png"] = sprite
        manifest["sprites"][name + ".png"] = {
            "size": list(size), "border": list(borders or (0, 0, 0, 0)),
            "scale_mode": "nine_slice" if borders else "uniform",
        }
        if "btn_round" in name:
            manifest["sprites"][name + ".png"]["preferred_scale_mode"] = "uniform"

    # Finish generation/validation in memory before writing any review outputs.
    sheet = preview(sprites)
    OUT.mkdir(parents=True, exist_ok=True)
    for filename, sprite in sprites.items():
        sprite.save(OUT / filename, format="PNG", optimize=False)
    sheet.save(OUT / "ui_preview.png", format="PNG", optimize=False)
    (OUT / "borders.json").write_text(json.dumps(manifest, indent=2) + "\n", encoding="utf-8")
    for filename, entry in manifest["sprites"].items():
        print("%s: L,B,R,T=%s (%s)" % (filename, entry["border"], entry["scale_mode"]))
    print("Wrote %d sprites, ui_preview.png and borders.json to %s" % (len(sprites), OUT))


if __name__ == "__main__":
    main()
