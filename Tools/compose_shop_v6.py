"""Paint the straight, tidy vintage Hanoi restaurant v6, for REVIEW ONLY.

Run from any directory with Pillow and numpy installed::

    python Tools/compose_shop_v6.py

Reads Tools/gemini/raw/bg_shop.png (1536 x 2752). Writes exactly three files:
    Tools/review/v6/bg_shop.png   -- 1080 x 2336 RGB
    Tools/review/v6/bg_front.png  -- matching RGBA, alpha 0 above y=600
    Tools/review/v6/preview.jpg   -- 540 x 1168 RGB, the unannotated painting

No game assets are changed. Customers, plates, oven, stove, ingredient trays,
and floor buttons remain live game sprites; their anchors below are clearance
guides, not objects to bake into the background. The live microwave occupies
x=60..360 and the hotpot/stove x=390..690. Only the third station, a large
wooden chopping board and horizontal cleaver at x=720..1020, is painted.

V6 changes from v5: ALL local frames are translation-only; there is no
rotation. A straight full-width wooden worktop has its shallow top at
y=985..1010 and short front at y=1010..1030, against calm old backsplash
tiles. Remove the wire trolley, shelves, hanging utensils and tilted props.
The pantry table is a straight rounded rectangle x=60..1020/y=1050..2010,
radius 26, leaving about 60px of visible floor on BOTH sides. Its exposed
wooden rim surrounds dusty plum #8E3B4F cloth, faint horizontal woven stripes
and a short rough hanging hem. Two short upright legs peek out at x=90/990.
Sixteen calm 210x175 cells are centred at x=195/425/655/885 and
y=1140/1370/1600/1830, in final screen coordinates. No table props remain.
Dark worn gach bong tiles surround the furniture; y=2040..2336 stays calm
for three live round buttons. Retain the awning, blue noren, old walls,
clock, calendar, serving counter, colourful-plate clearance, rough edges,
fixed grain seeds and Venba palette. Straighten the small retained wall and
counter decorations too, without adding any live game sprites to the art.

All coordinates are final pixels. Paint at 2x using irregular cut-paper masks,
paper-tooth ink dropout, and short broken strokes, then reduce once and apply
fine print grain. All random generators have fixed, independent seeds. There
are no fonts, external textures, optional imports, or dependencies beyond
Pillow, numpy, and the Python standard library.
"""

from math import ceil, cos, floor, hypot, pi, sin
from pathlib import Path

import numpy as np
from PIL import Image, ImageChops, ImageDraw, ImageFilter


# ---------------------------------------------------------------------------
# 1. Review paths, exact gameplay zones, and muted printing inks.
# ---------------------------------------------------------------------------
ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / "Tools" / "gemini" / "raw" / "bg_shop.png"
OUT_DIR = ROOT / "Tools" / "review" / "v6"
SOURCE_SIZE = (1536, 2752)
W, H, SCALE = 1080, 2336, 2
HI_SIZE = (W * SCALE, H * SCALE)
SEED = 319271

COUNTER_Y, COUNTER_BOTTOM = 600, 760
# V6: one horizontal worktop, three aligned station bays; no wire trolley.
WORKTOP_TOP = (0, 985, W, 1010)
WORKTOP_FRONT = (0, 1010, W, 1030)
FLOOR_START = 1030
CUSTOMER_X = (230, 540, 850)
CUSTOMER_BAND = (330, 600)
PLATE_X = (200, 427, 653, 880)
PLATE_CLEAR = tuple((x - 92, 607, x + 92, 701) for x in PLATE_X)
OVEN_SPACE = (60, 780, 360, 1000)
STOVE_SPACE = (390, 780, 690, 1000)
BOARD_BOX = (720, 850, 1020, 996)
BOARD_THICKNESS = 7
TRAY_X = (195, 425, 655, 885)
TRAY_Y = (1140, 1370, 1600, 1830)
TRAY_SIZE = (210, 175)
TABLE_BOX = (60, 1050, 1020, 2010)
TABLE_RADIUS = 26
TABLE_FACE_DEPTH = 8
# The first tray row starts at y=1052.5: keep the cloth's top nearly flush
# with the wood, and show most of the exposed rim along its sides/near edge.
CLOTH_BOX = (74, 1051, 1006, 1988)
CLOTH_RADIUS = 18
CLOTH_HEM_BOTTOM = 2009
# (centre x, attachment y, foot y, width), all in unrotated SCREEN pixels.
TABLE_LEGS = ((90, 1998, 2032, 38), (990, 1998, 2032, 38))
BUTTON_BAND = (2040, H)

# Keep the base's awning, inner hanging lanterns, lake, and Hanoi street. Never
# shrink the whole source: its obsolete lower counter/furniture are discarded.
AWNING_CROP = (0, 0, 1536, 302)
LAKE_CROP = (344, 812, 1208, 1424)
OPENING = (150, 224, 930, COUNTER_Y)

P = {
    "plaster": "#C68B79", "plaster_light": "#DBAD91",
    "terracotta": "#AA6357", "pink": "#D69A9D",
    "pink_light": "#E7BDB4", "plum": "#594055",
    "lavender": "#9291AC", "indigo": "#2C4A7A",
    "blue_light": "#6681A2", "teal": "#587F7B",
    "teal_light": "#90ABA0", "mustard": "#CDA952",
    "leaf": "#818958", "leaf_light": "#A3A46C",
    "cream": "#EADBC1", "chalk": "#F1E4D0",
    "wood": "#A57860", "wood_light": "#C5987B",
    "wood_mid": "#94654F", "wood_dark": "#694B4C",
    "brass": "#B69A60", "steel": "#A6B8AF",
    "floor": "#405755", "floor_alt": "#495D59",
    "floor_plum": "#53545D", "grout": "#354946",
    "chilli": "#AF514D",
    "cloth": "#8E3B4F", "cloth_stripe": "#B87987",
    "cloth_fold": "#713344", "cloth_light": "#A75C6E",
    # V6: the cleaver keeps the old flat grey-blue metal inks.
    "blade": "#B4C2C6", "blade_light": "#D5DEE0",
    "blade_shadow": "#7D8C96",
}
SHADOW = P["plum"]


def rgb(colour):
    if isinstance(colour, str):
        return tuple(int(colour[i:i + 2], 16) for i in (1, 3, 5))
    return tuple(int(v) for v in colour[:3])


def mix(a, b, t):
    return tuple(round(x * (1 - t) + y * t)
                 for x, y in zip(rgb(a), rgb(b)))


def ellipse_points(box, steps=36):
    x0, y0, x1, y1 = box
    cx, cy = (x0 + x1) / 2, (y0 + y1) / 2
    rx, ry = (x1 - x0) / 2, (y1 - y0) / 2
    return [(cx + rx * cos(a), cy + ry * sin(a))
            for a in np.linspace(0, 2 * pi, steps, endpoint=False)]


def rectangle_points(box):
    x0, y0, x1, y1 = box
    return [(x0, y0), (x1, y0), (x1, y1), (x0, y1)]


def make_ink_tooth():
    """One shared paper field: adjacent inks catch the same paper fibres."""
    rng = np.random.default_rng(SEED + 1)
    low = rng.integers(222, 256, size=(H // 5 + 1, W // 5 + 1), dtype=np.uint8)
    low = Image.fromarray(low).resize((W, H), Image.Resampling.BILINEAR)
    tooth = np.asarray(low).copy()
    roll = rng.random((H, W))
    tooth[roll < .035] = 160
    tooth[roll < .0045] = 65
    tooth[roll < .0006] = 0
    ink = Image.fromarray(tooth)
    # Small stretched voids echo a dry brush, rather than only digital noise.
    draw = ImageDraw.Draw(ink)
    for _ in range(6200):
        x, y = int(rng.integers(W)), int(rng.integers(H))
        length = int(rng.integers(1, 5))
        draw.line((x, y, x + length, y - 1), fill=int(rng.integers(125, 215)))
    return ink.resize(HI_SIZE, Image.Resampling.NEAREST)


# ---------------------------------------------------------------------------
# 2. Rough drawing helpers: EVERY new shape passes through a textured mask.
# ---------------------------------------------------------------------------
class Paint:
    """A shared RGB surface with translation-only local coordinates for v6.

    V6 deliberately removes rotation support: furniture, cloth, knife and
    retained decorations stay straight. Rough masks preserve hand-painted
    edges. Masks are cropped to each shape's bounds to limit memory at 2x.
    """

    def __init__(self, image, tooth, rng=None, origin=(0, 0)):
        self.image, self.tooth = image, tooth
        self.rng = np.random.default_rng(SEED + 2) if rng is None else rng
        self.origin = origin

    def point(self, pt):
        x, y = pt
        return (self.origin[0] + x, self.origin[1] + y)

    def frame(self, cx, cy):
        return Paint(self.image, self.tooth, self.rng,
                     self.point((cx, cy)))

    def _roughen(self, points, amount):
        """Subdivide long edges; vary their normal, leaving tiny shapes legible."""
        result = []
        for start, end in zip(points, points[1:] + points[:1]):
            dx, dy = end[0] - start[0], end[1] - start[1]
            length = max(hypot(dx, dy), .001)
            count = max(1, ceil(length / 5.5))
            # Taper the perturbation on short edges and narrow physical details.
            strength = min(amount, length * .12)
            for i in range(count):
                t = i / count
                jitter = float(self.rng.uniform(-strength, strength))
                result.append((start[0] + dx * t - dy / length * jitter,
                               start[1] + dy * t + dx / length * jitter))
        return result

    def _stamp(self, points, colour, alpha, rough, blur=0):
        if alpha <= 0 or len(points) < 3:
            return
        points = self._roughen(list(points), rough)
        pad = ceil(blur * 3 + 2)
        x0 = max(0, floor((min(x for x, _ in points) - pad) * SCALE))
        y0 = max(0, floor((min(y for _, y in points) - pad) * SCALE))
        x1 = min(HI_SIZE[0], ceil((max(x for x, _ in points) + pad) * SCALE))
        y1 = min(HI_SIZE[1], ceil((max(y for _, y in points) + pad) * SCALE))
        if x1 <= x0 or y1 <= y0:
            return
        mask = Image.new("L", (x1 - x0, y1 - y0), 0)
        ImageDraw.Draw(mask).polygon(
            [(round(x * SCALE - x0), round(y * SCALE - y0)) for x, y in points],
            fill=255)
        if blur:
            mask = mask.filter(ImageFilter.GaussianBlur(blur * SCALE))
        mask = ImageChops.multiply(mask, self.tooth.crop((x0, y0, x1, y1)))
        if alpha != 255:
            mask = mask.point([round(v * alpha / 255) for v in range(256)])
        self.image.paste(rgb(colour), (x0, y0), mask)

    def poly(self, points, colour, alpha=255, rough=.8):
        self._stamp([self.point(pt) for pt in points], colour, alpha, rough)

    def rect(self, box, colour, alpha=255, rough=.65):
        self.poly(rectangle_points(box), colour, alpha, rough)

    def ellipse(self, box, colour, alpha=255, rough=.5):
        self.poly(ellipse_points(box), colour, alpha, rough)

    def rounded(self, box, radius, colour, alpha=255, rough=.7):
        x0, y0, x1, y1 = box
        r = min(radius, (x1 - x0) / 2, (y1 - y0) / 2)
        points = []
        for cx, cy, start in ((x1 - r, y0 + r, -pi / 2),
                              (x1 - r, y1 - r, 0),
                              (x0 + r, y1 - r, pi / 2),
                              (x0 + r, y0 + r, pi)):
            points.extend((cx + r * cos(a), cy + r * sin(a))
                          for a in np.linspace(start, start + pi / 2, 7))
        self.poly(points, colour, alpha, rough)

    def line(self, points, colour, width=1, alpha=255, rough=.25):
        # A narrow polygon rather than a smooth Pillow line. Its two edges get
        # different small perturbations, including on the material fibres.
        points = list(points)
        if len(points) < 2:
            return
        left, right = [], []
        for i, (x, y) in enumerate(points):
            a, b = points[max(0, i - 1)], points[min(len(points) - 1, i + 1)]
            dx, dy = b[0] - a[0], b[1] - a[1]
            length = max(hypot(dx, dy), .001)
            nx, ny = -dy / length * width / 2, dx / length * width / 2
            left.append((x + nx, y + ny))
            right.append((x - nx, y - ny))
        self.poly(left + right[::-1], colour, alpha, min(rough, width * .22))

    def arc(self, box, start, end, colour, width=1, alpha=255):
        x0, y0, x1, y1 = box
        points = [((x0 + x1) / 2 + (x1 - x0) / 2 * cos(a),
                   (y0 + y1) / 2 + (y1 - y0) / 2 * sin(a))
                  for a in np.linspace(start * pi / 180, end * pi / 180, 40)]
        self.line(points, colour, width, alpha)

    def shadow(self, points, alpha=65, offset=(4, 6), blur=1.2):
        # A flat, small down-right offset; local frames never rotate in v6.
        absolute = [self.point(pt) for pt in points]
        absolute = [(x + offset[0], y + offset[1]) for x, y in absolute]
        self._stamp(absolute, SHADOW, alpha, .8, blur)


def leaf_points(base, tip, width, steps=18):
    bx, by = base
    dx, dy = tip[0] - bx, tip[1] - by
    length = max(hypot(dx, dy), .001)
    nx, ny = -dy / length * width, dx / length * width
    sides = []
    for sign, times in ((1, np.linspace(0, 1, steps)),
                        (-1, np.linspace(1, 0, steps))):
        for t in times:
            swell = sin(pi * t) ** .8
            sides.append((bx + dx * t + nx * sign * swell,
                          by + dy * t + ny * sign * swell))
    return sides


def leaf(p, base, tip, width, colour, alpha=255):
    p.poly(leaf_points(base, tip, width), colour, alpha, rough=.6)


def flower(p, cx, cy, radius, colour, alpha=255, petals=5):
    for a in np.linspace(0, 2 * pi, petals, endpoint=False):
        leaf(p, (cx + cos(a), cy + sin(a)),
             (cx + radius * cos(a), cy + radius * sin(a)),
             radius * .24, colour, alpha)
    p.ellipse((cx - 1.5, cy - 1.5, cx + 1.5, cy + 1.5), colour, alpha)


def clip_box(points, box):
    """Sutherland-Hodgman clipping keeps every plank inside its flat surface."""
    x0, y0, x1, y1 = box
    for axis, bound, keep_greater in ((0, x0, True), (0, x1, False),
                                     (1, y0, True), (1, y1, False)):
        previous = points
        points = []
        if not previous:
            break
        for a, b in zip(previous, previous[1:] + previous[:1]):
            ia = a[axis] >= bound if keep_greater else a[axis] <= bound
            ib = b[axis] >= bound if keep_greater else b[axis] <= bound
            if ia:
                points.append(a)
            if ia != ib:
                t = (bound - a[axis]) / (b[axis] - a[axis])
                points.append((a[0] + t * (b[0] - a[0]),
                               a[1] + t * (b[1] - a[1])))
    return points


def wood(p, box, colour, slope=0, plank=68, contrast=.10, fibres=25):
    """Straight planks and broken parallel grain; no perspective gradients."""
    x0, y0, x1, y1 = box
    p.rect(box, colour)
    span = slope * (x1 - x0)
    low = floor((y0 - max(0, span) - plank) / plank) * plank
    high = ceil((y1 - min(0, span) + plank) / plank) * plank
    for index, y in enumerate(range(int(low), int(high), plank)):
        points = [(x0, y), (x1, y + span),
                  (x1, y + span + plank - 1.5), (x0, y + plank - 1.5)]
        points = clip_box(points, box)
        if len(points) >= 3:
            tint = P["cream"] if index % 3 == 0 else P["wood_dark"]
            p.poly(points, mix(colour, tint, contrast), rough=.8)
        seam = clip_box([(x0, y), (x1, y + span),
                         (x1, y + span + 1.3), (x0, y + 1.3)], box)
        if len(seam) >= 3:
            p.poly(seam, P["wood_dark"], 30, rough=.3)
    rng = np.random.default_rng(SEED + int(y0) * 3 + int(x0) + 11)
    for _ in range(fibres):
        x = float(rng.uniform(x0 + 3, x1 - 3))
        y = float(rng.uniform(y0 + 3, y1 - 3))
        length = float(rng.uniform(10, 56))
        end_x = min(x + length, x1 - 2)
        end_y = y + slope * (end_x - x)
        if y0 + 2 < end_y < y1 - 2:
            p.line(((x, y), ((x + end_x) / 2, (y + end_y) / 2 + .6),
                    (end_x, end_y)), P["wood_dark"], .65, 23)


def source_crop(p, source, crop, destination, wash):
    """Reduce glossy source shading to a few faded, printed colour fields."""
    x0, y0, x1, y1 = destination
    size = (round((x1 - x0) * SCALE), round((y1 - y0) * SCALE))
    patch = source.crop(crop).resize((size[0] // 2, size[1] // 2),
                                     Image.Resampling.LANCZOS)
    patch = patch.quantize(colors=32, method=Image.Quantize.MEDIANCUT,
                           dither=Image.Dither.NONE).convert("RGB")
    patch = Image.blend(patch, Image.new("RGB", patch.size, rgb(P["cream"])), wash)
    patch = patch.resize(size, Image.Resampling.BILINEAR)
    p.image.paste(patch, (round(x0 * SCALE), round(y0 * SCALE)))


# ---------------------------------------------------------------------------
# 3. Shop front, y=0..600: weathered Hanoi walls, lake, blue five-panel noren.
# ---------------------------------------------------------------------------
def draw_shop_front(p, source):
    p.rect((0, 0, W, COUNTER_Y + 2), P["plaster"])
    p.rect((0, 148, W, 599), P["terracotta"], 58)
    source_crop(p, source, LAKE_CROP, OPENING, wash=.28)

    # Chipped limewash is confined to the wall returns. The three customer
    # head/shoulder columns see only the muted, unobstructed street/lake view.
    rng = np.random.default_rng(SEED + 31)
    for left, right in ((4, 128), (952, 1075)):
        for _ in range(44):
            x, y = float(rng.uniform(left, right)), float(rng.uniform(165, 587))
            rx, ry = float(rng.uniform(2, 13)), float(rng.uniform(1, 5))
            colour = P["plaster_light"] if rng.random() < .72 else P["plum"]
            p.poly(((x - rx, y), (x - rx * .4, y - ry),
                    (x + rx, y - ry * .4), (x + rx * .6, y + ry)),
                   colour, int(rng.integers(24, 65)), rough=1)
        # Faded horizontal mortar and short, uneven brush marks.
        for y in (279, 392, 512, 569):
            p.line(((left + 6, y), (left + 33, y + 1), (right - 4, y - .5)),
                   P["plaster_light"], 1.2, 37)

    # Substantial old jambs frame a broad opening; no side cupboards/fridge.
    wood(p, (130, 149, 153, 600), P["wood_mid"], 0, 55, .08, 9)
    wood(p, (929, 149, 952, 600), P["wood_mid"], 0, 55, .08, 9)
    p.rect((130, 151, 135, 599), P["wood_light"], 130)
    p.rect((945, 153, 952, 600), P["wood_dark"], 100)
    p.rect((153, 260, 158, 600), SHADOW, 38)
    for x in (140, 941):
        for y in (287, 560):
            p.ellipse((x - 1.5, y - 1.5, x + 1.5, y + 1.5), P["plum"], 170)
            p.line(((x - 1, y), (x + 1, y - .5)), P["brass"], .6, 150)

    # V6: preserve these shop-front details, now upright and level.
    draw_wall_clock(p.frame(69, 321))
    draw_calendar(p.frame(1007, 422))
    draw_faded_sign(p.frame(73, 471), 72, 101)
    draw_faded_sign(p.frame(1011, 290), 83, 51)

    # Reuse the source canopy, then weather its hem and frame. All original
    # source lanterns inside the lake crop remain behind the hanging curtain.
    source_crop(p, source, AWNING_CROP, (0, 0, W, 150), wash=.14)
    wood(p, (0, 137, W, 155), P["wood_mid"], 0, 30, .06, 17)
    p.line(((0, 140), (355, 141), (715, 140), (1080, 142)),
           P["wood_light"], 2.5, 130)
    for x in (24, 1056):
        p.rect((x - 8, 143, x + 8, 150), P["brass"], 170)
        p.ellipse((x - 1, 145, x + 1, 148), P["plum"], 170)
    draw_noren(p)


def draw_noren(p):
    # Indigo is deliberately the client's exact #2C4A7A. Different fabric
    # planes use a second ink, rather than smooth bevels or airbrushed shading.
    p.shadow(rectangle_points((147, 153, 933, 250)), 50, (3, 6), 1.5)
    panel_width = 786 / 5
    for i in range(5):
        x0, x1 = 147 + i * panel_width + 2.5, 147 + (i + 1) * panel_width - 2.5
        bottom = [(x1 - (x1 - x0) * t,
                   253 + 3.2 * sin(t * pi * 2 + i * .8) + 2 * sin(t * pi))
                  for t in np.linspace(0, 1, 18)]
        p.poly([(x0, 151), (x1, 151)] + bottom, P["indigo"], rough=1.15)
        p.rect((x0 + .8, 174, x1 - .8, 190), P["blue_light"], 185, rough=.8)
        p.poly(((x1 - 10, 153), (x1 - 3, 153),
                (x1 - 5, 249), (x1 - 12, 250)), P["plum"], 38)
        p.line(((x0 + 14, 156), (x0 + 13, 213), (x0 + 16, 244)),
               P["blue_light"], 1.1, 55)
        p.line(((x0 + 5, 245), ((x0 + x1) / 2, 247), (x1 - 5, 244)),
               P["blue_light"], .9, 80)
        flower(p, (x0 + x1) / 2, 221, 9, P["chalk"], 230)


def draw_wall_clock(p):
    case = ellipse_points((-37, -39, 37, 39))
    p.shadow(case, 74, (3, 5), 1.1)
    p.poly(case, P["wood_dark"], rough=.8)
    p.ellipse((-32, -34, 32, 34), P["mustard"], 170)
    p.ellipse((-29, -31, 29, 31), P["cream"])
    for i in range(12):
        a = i * pi / 6
        p.line(((23 * sin(a), -25 * cos(a)),
                ((20 if i % 3 == 0 else 21) * sin(a),
                 -(22 if i % 3 == 0 else 23) * cos(a))),
               P["plum"], 1.5 if i % 3 == 0 else .8, 185)
    p.line(((-15, -11), (0, 0), (13, -19)), P["plum"], 2.3, 240)
    p.ellipse((-2.5, -2.5, 2.5, 2.5), P["terracotta"])
    p.line(((-19, 18), (-9, 19), (-5, 18)), P["wood_mid"], .7, 38)


def draw_calendar(p):
    points = [(-38, -43), (38, -43), (37, 40), (28, 43),
              (14, 40), (2, 44), (-13, 41), (-36, 43)]
    p.shadow(points, 55)
    p.poly(points, P["cream"], rough=1.1)
    p.rect((-38, -43, 38, -26), P["terracotta"], 205)
    for x in (-22, 0, 22):
        p.line(((x, -46), (x, -35)), P["plum"], 2.2, 210)
    # Calendar cells and a highlighted date, with intentionally NO glyphs.
    for row in range(4):
        for col in range(5):
            x, y = -27 + col * 12, -13 + row * 12
            p.rect((x, y, x + 5, y + 3), P["wood_mid"], 70, rough=.3)
    p.rect((-3, 10, 5, 18), P["terracotta"], 120)
    p.poly(((20, 40), (37, 29), (37, 40)), P["pink_light"], 170)


def draw_faded_sign(p, width, height):
    box = (-width / 2, -height / 2, width / 2, height / 2)
    p.shadow(rectangle_points(box), 42, (2, 4), .8)
    p.rect(box, P["wood_dark"], 145)
    p.rect((box[0] + 3, box[1] + 3, box[2] - 3, box[3] - 3), P["teal"], 200)
    # Broad, broken paint blocks: signs suggest old paint, never readable text.
    for i in range(3):
        y = -height * .28 + i * height * .25
        p.poly(((-width * .31, y), (width * .23, y - 2),
                (width * .29, y + height * .095), (-width * .27, y + height * .11)),
               P["cream"] if i != 1 else P["mustard"], 115, rough=1.7)
    p.poly(((-width * .4, height * .37), (-width * .08, height * .31),
            (-width * .15, height * .44)), P["wood_light"], 160)


# ---------------------------------------------------------------------------
# 4. Serving counter, y=600..760: straight worn planks, four clean plate bays.
# ---------------------------------------------------------------------------
def draw_counter(p):
    wood(p, (0, 600, W, 704), P["wood_light"], slope=0,
         plank=66, contrast=.065, fibres=35)
    p.line(((0, 602), (310, 602), (620, 603), (1080, 602)), P["cream"], 2, 70)
    p.rect((0, 699, W, 711), P["wood_mid"])
    p.rect((0, 711, W, 756), P["wood_mid"])
    for i, x in enumerate(range(0, W, 135)):
        p.rect((x + 2, 714, x + 132, 752),
               P["terracotta"] if i % 3 == 0 else P["wood_dark"], 23)
        p.line(((x, 716), (x + 1, 751)), P["wood_dark"], 1.4, 115)
        p.line(((x + 11, 741), (x + 45, 740)), P["wood_light"], 1, 50)
    p.rect((0, 753, W, 760), P["wood_dark"])
    # Brass corners and visible screw slots are below the plate surface.
    for x in (16, 1037):
        p.poly(((x, 716), (x + 27, 716), (x + 27, 721),
                (x + 6, 721), (x + 6, 742), (x, 742)), P["brass"])
        for sx, sy in ((x + 3, 719), (x + 23, 719), (x + 3, 737)):
            p.ellipse((sx - 1, sy - 1, sx + 1.5, sy + 1.5), P["plum"], 170)
    # All decorative silhouettes and their shadows stay x<108 or x>972.
    draw_banana_leaf_and_chilli(p.frame(51, 653))
    draw_checked_napkin(p.frame(1025, 631))
    draw_teapot(p.frame(1028, 684))


def draw_banana_leaf_and_chilli(p):
    points = [(-23, -39), (18, -42), (28, -25), (25, 35),
              (7, 42), (-25, 33), (-29, 9)]
    p.shadow(points, 60, (3, 5), 1)
    p.poly(points, P["leaf"], rough=1.2)
    p.poly(((-23, -39), (-1, -40), (1, 39), (-25, 33)), P["leaf_light"], 100)
    p.line(((0, -38), (1, 0), (0, 38)), P["cream"], 1.1, 120)
    for y in range(-29, 33, 10):
        p.line(((-22, y + 8), (0, y), (23, y - 8)), P["cream"], .7, 66)
    p.ellipse((-19, -17, 22, 16), P["plum"], 180)
    p.ellipse((-20, -20, 20, 12), P["cream"])
    p.ellipse((-16, -16, 16, 8), P["chilli"])
    for x, y in ((-8, -7), (3, -10), (9, -2), (-2, 2), (-10, -1)):
        p.line(((x, y), (x + 4, y - 1)), P["mustard"], 1.4, 180)


def draw_checked_napkin(p):
    points = [(-33, -16), (31, -15), (33, 15), (-30, 17)]
    p.shadow(points, 50, (3, 4), .9)
    p.poly(points, P["pink_light"], rough=1)
    for x in (-24, -7, 10, 26):
        p.rect((x, -15, x + 5, 15), P["pink"], 180, rough=.45)
    for y in (-10, 4):
        p.rect((-31, y, 32, y + 5), P["pink"], 165, rough=.45)
    p.poly(((-30, 13), (30, 9), (32, 15), (-30, 17)), P["plum"], 35)
    p.line(((-28, -12), (1, -11), (29, -12)), P["chalk"], .8, 160)
    for x in (-27, -16, -3, 10, 25):
        p.line(((x, 15), (x - 1, 18)), P["cream"], .7, 130)


def draw_teapot(p):
    p.shadow(ellipse_points((-43, -21, 40, 22)), 65, (3, 5), 1.2)
    p.arc((15, -20, 40, 12), 0, 360, P["wood_dark"], 7, 200)
    p.arc((15, -20, 40, 12), 0, 360, P["teal"], 4.5)
    p.poly(((-21, -6), (-36, -14), (-41, -25), (-47, -23),
            (-40, -1), (-23, 11)), P["teal"], rough=.7)
    p.ellipse((-28, -17, 26, 24), P["teal"])
    p.poly(((11, -15), (24, -3), (24, 12), (11, 23), (2, 22), (12, 6)),
           P["plum"], 42)
    p.ellipse((-23, -22, 20, -11), P["teal_light"])
    p.ellipse((-7, -29, 4, -19), P["mustard"])
    flower(p, -9, 1, 6, P["cream"], 135)
    p.poly(((-22, 11), (-16, 13), (-19, 18), (-24, 15)), P["cream"], 95)


# ---------------------------------------------------------------------------
# 5. V6 cooking line: calm old tiles, straight worktop, one large chopping board.
# ---------------------------------------------------------------------------
def draw_cooking_line(p):
    # V6 replaces EVERY trolley part and loose shelf prop with a quiet tiled
    # wall. The first two 300px bays stay empty for the game's live appliances.
    wall = mix(P["plaster_light"], P["cream"], .34)
    grout = mix(wall, P["wood_mid"], .12)
    p.rect((0, COUNTER_BOTTOM, W, WORKTOP_TOP[1]), grout)
    rng = np.random.default_rng(SEED + 51)
    tile = 108
    for row, y in enumerate(range(767, WORKTOP_TOP[1], tile)):
        for col, x in enumerate(range(0, W, tile)):
            bottom = min(y + tile - 2, WORKTOP_TOP[1])
            tint = mix(wall, P["cream"], float(rng.uniform(.08, .18)))
            p.rounded((x + 1.5, y + 1.5, x + tile - 1.5, bottom),
                      2, tint, rough=.7)
            # Broken low-contrast grout and a few faded edge chips suggest
            # aged ceramic without ornaments competing with the stations.
            p.line(((x + 8, bottom - 1), (x + 46, bottom - 1),
                    (x + tile - 8, bottom - 1)),
                   P["wood_mid"], .7, 22)
            if (row + col) % 4 == 0:
                p.line(((x + 7, y + 8), (x + 16, y + 8)),
                       P["chalk"], 1, 65)
                p.rect((x + 3, y + 17, x + 5, y + 23),
                       P["wood_mid"], 23, rough=.5)

    # V6: wall-to-wall wooden kitchen worktop, all major edges horizontal.
    # A flat contact shadow and 20px front face give the shallow top weight.
    p.rect((0, WORKTOP_FRONT[3], W, WORKTOP_FRONT[3] + 4), SHADOW, 48)
    wood(p, WORKTOP_FRONT, P["wood_mid"], slope=0,
         plank=30, contrast=.045, fibres=16)
    p.rect((0, 1026, W, 1030), P["wood_dark"], 195)
    wood(p, WORKTOP_TOP, P["wood_light"], slope=0,
         plank=40, contrast=.045, fibres=27)
    p.line(((0, 987), (354, 987), (716, 987), (W, 987)),
           P["cream"], 1.4, 92)
    p.line(((0, 1009), (W, 1009)), P["wood_dark"], 1.5, 135)
    p.line(((0, 1013), (W, 1013)), P["wood_light"], .9, 85)
    draw_cutting_board(p)


def draw_cutting_board(p):
    """V6 third station: large straight board, shallow depth, level cleaver."""
    x0, y0, x1, y1 = BOARD_BOX
    # The front edge and its tiny flat shadow rest on the y=985..1010 top.
    # Short front thickness, rounded corners and compressed depth give the
    # board a view slightly from above without a rotated bounding rectangle.
    p.shadow(rectangle_points((x0 + 5, y0 + 7, x1 - 4,
                               y1 + BOARD_THICKNESS)),
             65, (3, 3), 0)
    p.rounded((x0, y0 + BOARD_THICKNESS, x1, y1 + BOARD_THICKNESS),
              14, P["wood_dark"], rough=1.2)
    p.rounded(BOARD_BOX, 14, P["wood_light"], rough=1.3)
    p.rounded((x0 + 3, y0 + 3, x1 - 3, y1 - 3), 12,
              mix(P["wood_light"], P["mustard"], .11), rough=.85)
    wood(p, (x0 + 10, y0 + 9, x1 - 10, y1 - 9),
         mix(P["wood_light"], P["mustard"], .11), slope=0,
         plank=43, contrast=.035, fibres=38)
    p.line(((x0 + 17, y1 - 1), (870, y1 - 1), (x1 - 16, y1 - 1)),
           P["cream"], 1.1, 85)
    p.line(((x0 + 19, y1 + 4), (x1 - 19, y1 + 4)),
           P["wood_mid"], 1.2, 140)
    # Broken wood fibres and a handful of old straight chopping marks.
    for x, y, length in ((744, 876, 37), (818, 864, 53),
                          (914, 978, 61), (756, 967, 47)):
        p.line(((x, y), (x + length * .45, y), (x + length, y)),
               P["wood_mid"], .8, 55)
    for x in (802, 816, 927, 940):
        p.line(((x, 969), (x, 978)), P["cream"], .6, 68)
    for x, y in ((725, 887), (1015, 959)):
        p.rect((x, y, x + 2, y + 7), P["wood_dark"], 78, rough=.8)

    # V6: a horizontal cleaver replaces the tiny angled trolley bread knife.
    # No loose food or extra station decoration is baked into this surface.
    knife = p.frame((x0 + x1) / 2, (y0 + y1) / 2)
    blade = [(-113, -27), (28, -27), (34, -20), (34, 21),
             (-107, 21), (-113, 13)]
    handle = (28, -17, 123, 6)
    knife.shadow(blade, 54, (2, 3), 0)
    knife.shadow(rectangle_points(handle), 54, (2, 3), 0)
    knife.poly(blade, P["blade"], rough=.5)
    knife.poly(((-112, 15), (34, 15), (34, 21), (-107, 21)),
               P["blade_light"], 215, rough=.3)
    knife.line(((-108, -24), (26, -24)), P["blade_light"], 1.3, 170)
    knife.line(((-93, -13), (-15, -13)), P["blade_shadow"], .7, 53)
    knife.ellipse((-102, -19, -94, -11), P["wood_dark"], 205)
    knife.ellipse((-100, -17, -96, -13), P["wood_light"], 185)
    knife.rounded(handle, 4, P["wood_dark"], rough=.7)
    knife.line(((37, -13), (114, -13)), P["wood_light"], 1.3, 82)
    knife.rect((29, -17, 35, 6), P["blade_shadow"], 240, rough=.4)
    for x in (48, 78, 107):
        knife.ellipse((x - 1.6, -7, x + 1.6, -3.8), P["brass"], 220)


# ---------------------------------------------------------------------------
# 6. V6 pantry: straight top-down table, side floor gaps, two short upright legs.
# ---------------------------------------------------------------------------
def draw_table_legs(p):
    """Two sturdy end legs; no stretcher, tilt or detail in the button band."""
    for x, top, foot, width in TABLE_LEGS:
        p.ellipse((x - 25, foot - 3, x + 29, foot + 3),
                  SHADOW, 76, rough=.7)
        # Slab and cloth are drawn afterwards, naturally hiding the leg tops.
        p.rounded((x - width / 2, top, x + width / 2, foot),
                  2, P["wood_mid"], rough=1)
        p.rect((x + width / 2 - 7, top, x + width / 2, foot),
               P["wood_dark"], 215, rough=.8)
        p.line(((x - width / 2 + 7, top + 4),
                (x - width / 2 + 7, foot - 4)),
               P["wood_light"], 2.3, 125)
        p.line(((x - 1, top + 5), (x - 1, foot - 4)),
               P["wood_dark"], .8, 68)
        p.rect((x - width / 2 + 1, foot - 4,
                x + width / 2 - 1, foot), P["wood_dark"], 88, rough=.7)


def draw_table(p):
    # V6: use final screen coordinates directly. The whole x=60..1020 table
    # is visible; about 60px of dark tile remains exposed at BOTH sides.
    x0, y0, x1, y1 = TABLE_BOX
    p.rounded((x0 + 6, y0 + 7, x1 + 6, y1 + 14),
              TABLE_RADIUS, SHADOW, 70, rough=1)
    draw_table_legs(p)
    p.rounded((x0, y0 + TABLE_FACE_DEPTH, x1, y1 + TABLE_FACE_DEPTH),
              TABLE_RADIUS, P["wood_dark"], rough=1.2)
    p.rounded(TABLE_BOX, TABLE_RADIUS, P["wood_light"], rough=1.2)
    p.rounded((x0 + 3, y0 + 3, x1 - 3, y1 - 3), TABLE_RADIUS - 3,
              P["wood_mid"], rough=.85)
    # Exposed rim fibres are straight and sparse. The near wooden face stays
    # visible below the short cloth hem; leg feet finish before y=2040.
    for side in (x0 + 6, x1 - 6):
        for y in range(y0 + 42, y1 - 26, 93):
            p.line(((side, y), (side, y + 29)), P["cream"], .75, 58)
    p.line(((x0 + 24, y1 + 3), (392, y1 + 3),
            (710, y1 + 3), (x1 - 24, y1 + 3)),
           P["wood_light"], 1, 90)
    for x, y in ((66, 1217), (1012, 1480), (66, 1784), (1012, 1910)):
        p.rect((x, y, x + 2, y + 7), P["wood_light"], 130, rough=.7)

    # V6: the first 210x175 row reaches y=1052.5, so the upper cloth edge
    # starts at y=1051. The wider side rim still clearly reads as real wood.
    cx0, cy0, cx1, cy1 = CLOTH_BOX
    p.rounded(CLOTH_BOX, CLOTH_RADIUS, P["cloth"], rough=1.3)
    p.rounded((cx0 + .6, cy0 + .6, cx1 - .6, cy1 - .6),
              CLOTH_RADIUS - 1, P["cloth"], rough=.65)
    # Faint horizontal woven stripes, with no perspective slope or rotation.
    for y in range(cy0 + 24, cy1 - 8, 31):
        p.line(((cx0 + 4, y), (377, y), (706, y), (cx1 - 4, y)),
               P["cloth_stripe"], .95, 20, rough=.16)
        p.line(((cx0 + 4, y + 3), (cx1 - 4, y + 3)),
               P["cloth_light"], .5, 12, rough=.1)
    # Optional faint lighter cell fields, never drawn trays or strong outlines.
    # Exactly the requested final centres; all food, trays and locks are live.
    for y in TRAY_Y:
        for x in TRAY_X:
            p.rounded((x - TRAY_SIZE[0] / 2, y - TRAY_SIZE[1] / 2,
                       x + TRAY_SIZE[0] / 2, y + TRAY_SIZE[1] / 2),
                      14, P["cloth_light"], 12, rough=.3)

    # Short side drops retain a slim visible wooden edge. The hanging cloth
    # is ragged by a few pixels, not sloped; folds stay far below the last row.
    p.poly(((cx0, cy0 + 28), (cx0 + 4, cy0 + 28),
            (cx0 + 4, cy1 - 2), (cx0 - 1, cy1 + 15),
            (cx0 - 4, cy1 + 7)), P["cloth_fold"], 95, rough=1)
    p.poly(((cx1 - 4, cy0 + 28), (cx1, cy0 + 28),
            (cx1 + 4, cy1 + 7), (cx1 + 1, cy1 + 15),
            (cx1 - 4, cy1 - 2)), P["cloth_fold"], 95, rough=1)
    hem = [(cx0, cy1 - 5), (cx1, cy1 - 5),
           (cx1 + 1, 2005), (905, 2008), (790, 2004),
           (657, CLOTH_HEM_BOTTOM), (523, 2006), (388, 2008),
           (250, 2004), (136, CLOTH_HEM_BOTTOM), (cx0 - 2, 2005)]
    p.shadow(hem, 34, (1, 2), 0)
    p.poly(hem, P["cloth"], rough=1.3)
    p.poly([(cx0, cy1 + 5), (cx1, cy1 + 5)] + hem[2:],
           P["cloth_fold"], 58, rough=.9)
    for x in (94, 240, 420, 660, 840, 985):
        p.line(((x, cy1 - 2), (x, cy1 + 16)),
               P["cloth_light"], 1.1, 49)
        p.line(((x + 2, cy1 + 15), (x + 2, cy1 + 20)),
               P["pink_light"], .55, 66)


# ---------------------------------------------------------------------------
# 7. V6 floor: dark worn gach bong; the full y=2040..2336 band stays calm.
# ---------------------------------------------------------------------------
def draw_floor(p):
    # V6: continuous dark tile through the 20px furniture gap, both 60px
    # side aisles and the floor below the straight pantry table.
    p.rect((0, FLOOR_START, W, H + 2), P["floor"])
    tile = 136
    rng = np.random.default_rng(SEED + 71)
    for row, y in enumerate(range(FLOOR_START, H, tile)):
        for col, x in enumerate(range(-40, W, tile)):
            quiet = y + tile >= BUTTON_BAND[0]
            grout = mix(P["floor"], P["grout"], .25) if quiet else P["grout"]
            p.rect((x, y, x + tile, min(H + 2, y + tile)), grout, rough=.6)
            tone = (P["floor"], P["floor_alt"], P["floor_plum"])[(row + col) % 3]
            if quiet:
                tone = mix(P["floor"], tone, .22)
            p.rect((x + 1.8, y + 1.8, x + tile - 1.2, min(H + 2, y + tile - 1.2)),
                   mix(tone, P["cream"], float(rng.uniform(0, .012 if quiet else .035))),
                   rough=1)
            cx, cy = x + tile / 2, y + tile / 2
            # Worn cement tiles use two subdued inks and small corner diamonds.
            # No floral motif can straddle the calm band's y=2040 boundary.
            if cy + 44 < BUTTON_BAND[0]:
                flower(p, cx, cy, 43, P["teal_light"], 35, petals=4)
                flower(p, cx, cy, 29, P["floor"], 160, petals=4)
                p.poly(((cx, cy - 10), (cx + 10, cy),
                        (cx, cy + 10), (cx - 10, cy)), P["mustard"], 29)
            for sx, sy in ((-1, -1), (1, -1), (-1, 1), (1, 1)):
                ax, ay = cx + sx * 55, cy + sy * 55
                if ay + 7 < BUTTON_BAND[0]:
                    p.poly(((ax - 6, ay), (ax, ay - 6),
                            (ax + 6, ay), (ax, ay + 6)), P["pink"], 22)
            # A few scuffs near tile edges; avoid scratch marks through buttons.
            if (row + col) % 3 == 0 and y + 14 < BUTTON_BAND[0]:
                p.line(((x + 10, y + 8), (x + 22, y + 12), (x + 34, y + 10)),
                       P["cream"], .8, 30)
    p.rect((0, FLOOR_START, W, H + 2), P["plum"], 30)
    # Low-contrast grout and this same-floor ink wash calm ALL of the bottom
    # band. No drawn buttons, wells, UI panel or strong new border is added.
    p.rect((0, BUTTON_BAND[0], W, BUTTON_BAND[1] + 2), P["floor"], 95)


# ---------------------------------------------------------------------------
# 8. Final paper grain: fine speckles survive downsampling at phone resolution.
# ---------------------------------------------------------------------------
def paper_grain(image):
    rng = np.random.default_rng(SEED + 81)
    tooth = rng.normal(128, 12, (H // 3 + 1, W // 3 + 1))
    tooth = Image.fromarray(np.clip(np.rint(tooth), 0, 255).astype(np.uint8))
    tooth = tooth.resize((W, H), Image.Resampling.BILINEAR)
    arr = np.array(image)
    for y in range(0, H, 128):
        end = min(H, y + 128)
        coarse = np.asarray(tooth.crop((0, y, W, end)), dtype=np.float32) - 128
        fine = rng.normal(0, 1.65, (end - y, W)).astype(np.float32)
        speckle = rng.random((end - y, W))
        fine[speckle < .008] -= 7
        fine[speckle > .994] += 6
        grain = coarse * .30 + fine
        chunk = arr[y:end].astype(np.float32) + grain[..., None]
        arr[y:end] = np.clip(np.rint(chunk), 0, 255).astype(np.uint8)
    return Image.fromarray(arr)


# ---------------------------------------------------------------------------
# 9. Layout contracts, exact alpha boundary, and REVIEW-ONLY export.
# ---------------------------------------------------------------------------
def check_layout():
    """V6 contracts: straight geometry, complete live-tray clearance, review only."""
    if (W, H) != (1080, 2336) or COUNTER_Y != 600:
        raise ValueError("Phone canvas and foreground boundary must stay exact.")
    if (WORKTOP_TOP != (0, 985, 1080, 1010)
            or WORKTOP_FRONT != (0, 1010, 1080, 1030)
            or FLOOR_START != WORKTOP_FRONT[3]):
        raise ValueError("Keep the straight full-width worktop at y=985..1030.")
    if (OVEN_SPACE != (60, 780, 360, 1000)
            or STOVE_SPACE != (390, 780, 690, 1000)):
        raise ValueError("Keep the two 300px live appliance bays on the wooden top.")
    if BOARD_BOX != (720, 850, 1020, 996) or BOARD_THICKNESS != 7:
        raise ValueError("The large straight chopping board belongs in the third bay.")
    # The slab front sits within the worktop's shallow support plane. Its
    # complete flat shadow stops before the front face, not over the pantry.
    if not (WORKTOP_TOP[1] <= BOARD_BOX[3]
            < BOARD_BOX[3] + BOARD_THICKNESS + 4 < WORKTOP_TOP[3]):
        raise ValueError("The chopping board must visibly rest on the worktop.")
    for bay in (OVEN_SPACE, STOVE_SPACE):
        if not (WORKTOP_TOP[1] <= bay[3] <= WORKTOP_TOP[3]):
            raise ValueError("Both live appliances must sit on the same worktop plane.")
    if (TABLE_BOX != (60, 1050, 1020, 2010) or TABLE_RADIUS != 26
            or TABLE_FACE_DEPTH != 8):
        raise ValueError("Keep the straight x=60..1020 pantry table and 26px corners.")
    if not (TABLE_BOX[0] == W - TABLE_BOX[2] == 60
            and TABLE_BOX[1] - WORKTOP_FRONT[3] == 20):
        raise ValueError("Keep visible floor at both sides and between the furniture.")
    if (TRAY_X != (195, 425, 655, 885)
            or TRAY_Y != (1140, 1370, 1600, 1830)
            or TRAY_SIZE != (210, 175)):
        raise ValueError("Keep all sixteen tray centres in final screen coordinates.")
    if (P["cloth"] != "#8E3B4F" or CLOTH_BOX != (74, 1051, 1006, 1988)
            or CLOTH_RADIUS != 18 or CLOTH_HEM_BOTTOM != 2009):
        raise ValueError("Keep the dusty plum cloth, narrow wood rim and short rough hem.")
    if not (TABLE_BOX[0] < CLOTH_BOX[0] < CLOTH_BOX[2] < TABLE_BOX[2]
            and TABLE_BOX[1] <= CLOTH_BOX[1] < CLOTH_BOX[3] < TABLE_BOX[3]
            and CLOTH_HEM_BOTTOM + 4 < TABLE_BOX[3] + TABLE_FACE_DEPTH):
        raise ValueError("The cloth must leave a visible wooden rim and near face.")
    # Check FULL 210x175 cells, including the very high first row, rather than
    # just their centres. Rounded-cloth corner clearance is checked too.
    trays = tuple((x - TRAY_SIZE[0] / 2, y - TRAY_SIZE[1] / 2,
                   x + TRAY_SIZE[0] / 2, y + TRAY_SIZE[1] / 2)
                  for y in TRAY_Y for x in TRAY_X)
    for box in trays:
        if not (CLOTH_BOX[0] <= box[0] < box[2] <= CLOTH_BOX[2]
                and CLOTH_BOX[1] <= box[1] < box[3] <= CLOTH_BOX[3]):
            raise ValueError("Every complete live tray must fit on the calm cloth.")
        for x, y in rectangle_points(box):
            nearest_x = min(max(x, CLOTH_BOX[0] + CLOTH_RADIUS),
                            CLOTH_BOX[2] - CLOTH_RADIUS)
            nearest_y = min(max(y, CLOTH_BOX[1] + CLOTH_RADIUS),
                            CLOTH_BOX[3] - CLOTH_RADIUS)
            if (x - nearest_x) ** 2 + (y - nearest_y) ** 2 > CLOTH_RADIUS ** 2:
                raise ValueError("A tray corner would extend beyond the rounded cloth.")
    if (BUTTON_BAND != (2040, 2336)
            or TABLE_LEGS != ((90, 1998, 2032, 38), (990, 1998, 2032, 38))):
        raise ValueError("Keep two short upright end legs and the full calm button band.")
    if TABLE_BOX[3] + TABLE_FACE_DEPTH + 2 >= BUTTON_BAND[0]:
        raise ValueError("The wooden table face must stop above the floor buttons.")
    for x, top, foot, width in TABLE_LEGS:
        if not (width >= 32 and top < TABLE_BOX[3] + TABLE_FACE_DEPTH < foot
                and top + 30 <= foot and foot + 4 < BUTTON_BAND[0]
                and TABLE_BOX[0] < x - width / 2 < x + width / 2 < TABLE_BOX[2]):
            raise ValueError("Both sturdy legs and their shadows must clear the buttons.")
    for crop in (AWNING_CROP, LAKE_CROP):
        if not (0 <= crop[0] < crop[2] <= SOURCE_SIZE[0]
                and 0 <= crop[1] < crop[3] <= 1424):
            raise ValueError("Only crop the useful upper part of the source.")
    # Fail closed if the directory or an ancestor redirects outside v6 review.
    # Saving the three direct children below can never overwrite game assets.
    expected = ROOT / "Tools" / "review" / "v6"
    if OUT_DIR != expected or expected.resolve() != expected:
        raise ValueError("Outputs must remain inside Tools/review/v6.")


def validate_images(background, front):
    if background.mode != "RGB" or background.size != (W, H):
        raise RuntimeError("bg_shop.png must be 1080 x 2336 RGB.")
    if front.mode != "RGBA" or front.size != (W, H):
        raise RuntimeError("bg_front.png must be matching RGBA.")
    if front.convert("RGB").tobytes() != background.tobytes():
        raise RuntimeError("The foreground and background RGB must match.")
    alpha = front.getchannel("A")
    if alpha.crop((0, 0, W, 600)).getextrema() != (0, 0):
        raise RuntimeError("Every foreground pixel above y=600 must be transparent.")
    if alpha.crop((0, 600, W, H)).getextrema() != (255, 255):
        raise RuntimeError("Every foreground pixel at/below y=600 must be opaque.")


def main():
    check_layout()
    with Image.open(SOURCE) as original:
        if original.size != SOURCE_SIZE:
            raise ValueError(f"Expected source {SOURCE_SIZE}, got {original.size}: {SOURCE}")
        source = original.convert("RGB")
    canvas = Image.new("RGB", HI_SIZE, rgb(P["cream"]))
    tooth = make_ink_tooth()
    p = Paint(canvas, tooth)
    draw_floor(p)
    draw_shop_front(p, source)
    draw_cooking_line(p)
    draw_table(p)
    draw_counter(p)
    del p, tooth, source

    background = canvas.resize((W, H), Image.Resampling.LANCZOS)
    del canvas
    background = paper_grain(background)
    # Apply alpha AFTER every filtering/grain operation. There is no feather at
    # the occlusion boundary: row 599 is 0, and row 600 is exactly 255.
    front = background.convert("RGBA")
    alpha = Image.new("L", (W, H), 0)
    alpha.paste(255, (0, COUNTER_Y, W, H))
    front.putalpha(alpha)
    validate_images(background, front)
    preview = background.resize((W // 2, H // 2), Image.Resampling.LANCZOS)

    outputs = (OUT_DIR / "bg_shop.png", OUT_DIR / "bg_front.png", OUT_DIR / "preview.jpg")
    # Reject existing output-file symlinks too; never follow one out to assets.
    for target in outputs:
        if target.is_symlink() or target.resolve().parent != OUT_DIR.resolve():
            raise ValueError(f"Refusing an output path outside the review folder: {target}")
    OUT_DIR.mkdir(parents=True, exist_ok=True)
    background.save(outputs[0], format="PNG", optimize=True)
    front.save(outputs[1], format="PNG", optimize=True)
    preview.save(outputs[2], format="JPEG", quality=94, subsampling=0, optimize=True)
    for target in outputs:
        print(f"Saved {target.relative_to(ROOT)}")


if __name__ == "__main__":
    main()
