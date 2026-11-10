"""Paint the vintage Hanoi restaurant v5, for REVIEW ONLY.

Run from any directory with Pillow and numpy installed::

    python Tools/compose_shop_v5.py

Reads Tools/gemini/raw/bg_shop.png (1536 x 2752). Writes exactly three files:
    Tools/review/v5/bg_shop.png   -- 1080 x 2336 RGB
    Tools/review/v5/bg_front.png  -- matching RGBA, alpha 0 above y=600
    Tools/review/v5/preview.jpg   -- 540 x 1168 RGB, the unannotated painting

No game assets are changed. Customers, plates, oven, stove, ingredient trays,
and floor buttons remain live game sprites; their anchors below are clearance
guides, not objects to bake into the background. V5 keeps those live sprites:
the microwave's x=260..520 bay and hotpot/stove's x=560..840 bay now share
the stainless wire trolley's top surface at y=900. No appliance art is baked.

V5 changes: one much larger wooden table, x=-40..1180/y=1010..1990,
rotates +15 degrees CLOCKWISE ON SCREEN about (560, 1500). Its left end is
higher; its right end runs beyond the screen. Two visible sturdy wooden legs,
a cross-bar and flat foot shadows establish furniture above the floor. The
dusty plum cloth, faint stripes, banana leaf and fork share that transform;
the sixteen 210x175 live tray cells stay calm. A three-tier silver wire rack
trolley replaces the old bench, with a bread board and bread knife on its
middle tier. The side cutting board and floor cloth are removed. The recipe
book keeps its small right wall shelf. Dark worn gach bong tiles surround the
table, with subdued floor from y=2040 down. The shop front, blue noren,
counter, rough edges, fixed grain seeds and Venba palette are retained.

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
OUT_DIR = ROOT / "Tools" / "review" / "v5"
SOURCE_SIZE = (1536, 2752)
W, H, SCALE = 1080, 2336, 2
HI_SIZE = (W * SCALE, H * SCALE)
SEED = 319271

COUNTER_Y, COUNTER_BOTTOM = 600, 760
# V5: a freestanding wire trolley replaces the old wooden bench completely.
TROLLEY_X = (232, 852)
TROLLEY_TIERS = (900, 960, 1008)  # Front edges; top is the appliance plane.
FLOOR_START = 952
CUSTOMER_X = (230, 540, 850)
CUSTOMER_BAND = (330, 600)
PLATE_X = (200, 427, 653, 880)
PLATE_CLEAR = tuple((x - 92, 607, x + 92, 701) for x in PLATE_X)
OVEN_SPACE = (260, 768, 520, TROLLEY_TIERS[0])
STOVE_SPACE = (560, 780, 840, TROLLEY_TIERS[0])
TRAY_X = (218, 446, 674, 902)
TRAY_Y = (1130, 1360, 1590, 1820)
TRAY_SIZE = (210, 175)
TABLE_BOX = (-40, 1010, 1180, 1990)
TABLE_RADIUS = 26
TABLE_PIVOT = (560, 1500)  # Exact gameplay pivot, not the rectangle's centre.
TABLE_ANGLE = 15.0  # Positive in Paint's y-down coordinates means CLOCKWISE.
CLOTH_BOX = (-31, 1013, 1171, 1988)
# Absolute UNROTATED centres, additional angles, and local silhouette bounds.
# V5: only a banana leaf and fork, together on the free lower-left cloth corner.
TABLE_PROPS = (
    ("banana_leaf", (205, 1946), -11, (-39, -13, 39, 13)),
    ("fork", (300, 1944), -13, (-30, -5, 30, 5)),
)
PROP_CLEARANCE = 7  # Includes rough edges, down-right shadow, and blur.
# Leg attachment coordinates are UNROTATED. Feet stay vertical in screen
# space and on the floor, below the near rim, clear of the calm button band.
TABLE_LEGS = (((205, 1985), (86, 2022), 38),
              ((540, 1985), (425, 2034), 40))
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
    # V5 silver uses flat light grey-blue inks, never black contour outlines.
    "rack_steel": "#B4C2C6", "rack_light": "#D5DEE0",
    "rack_shadow": "#7D8C96", "wheel": "#343B42",
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
    """A shared RGB surface with rotated local coordinates for diagonal props.

    No bitmap rotations: rotate the shape geometry before roughening, keeping
    outlines, checks, leaf veins, knife, and shadow edges equally hand-painted.
    Masks are cropped to each shape's bounds to keep memory use modest at 2x.
    """

    def __init__(self, image, tooth, rng=None, origin=(0, 0), angle=0):
        self.image, self.tooth = image, tooth
        self.rng = np.random.default_rng(SEED + 2) if rng is None else rng
        self.origin = origin
        self.angle = angle
        self.c, self.s = cos(angle), sin(angle)

    def point(self, pt):
        x, y = pt
        return (self.origin[0] + x * self.c - y * self.s,
                self.origin[1] + x * self.s + y * self.c)

    def frame(self, cx, cy, degrees):
        return Paint(self.image, self.tooth, self.rng,
                     self.point((cx, cy)), self.angle + degrees * pi / 180)

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
        # Offset in CANVAS coordinates, so all rotated props cast down-right.
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
    """Sutherland-Hodgman clipping for diagonal planks inside a flat surface."""
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


def wood(p, box, colour, slope=.26, plank=68, contrast=.10, fibres=25):
    """Diagonal boards with broken, parallel grain; no perspective gradients."""
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
    wood(p, (130, 149, 153, 600), P["wood_mid"], .7, 55, .08, 9)
    wood(p, (929, 149, 952, 600), P["wood_mid"], .7, 55, .08, 9)
    p.rect((130, 151, 135, 599), P["wood_light"], 130)
    p.rect((945, 153, 952, 600), P["wood_dark"], 100)
    p.rect((153, 260, 158, 600), SHADOW, 38)
    for x in (140, 941):
        for y in (287, 560):
            p.ellipse((x - 1.5, y - 1.5, x + 1.5, y + 1.5), P["plum"], 170)
            p.line(((x - 1, y), (x + 1, y - .5)), P["brass"], .6, 150)

    draw_wall_clock(p.frame(69, 321, -4))
    draw_calendar(p.frame(1007, 422, 6))
    draw_faded_sign(p.frame(73, 471, -8), 72, 101)
    draw_faded_sign(p.frame(1011, 290, 9), 83, 51)

    # Reuse the source canopy, then weather its hem and frame. All original
    # source lanterns inside the lake crop remain behind the hanging curtain.
    source_crop(p, source, AWNING_CROP, (0, 0, W, 150), wash=.14)
    wood(p, (0, 137, W, 155), P["wood_mid"], .018, 30, .06, 17)
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
# 4. Serving counter, y=600..760: diagonal worn planks, four clean plate bays.
# ---------------------------------------------------------------------------
def draw_counter(p):
    wood(p, (0, 600, W, 704), P["wood_light"], slope=.22,
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
    draw_banana_leaf_and_chilli(p.frame(51, 653, -24))
    draw_checked_napkin(p.frame(1025, 631, 18))
    draw_teapot(p.frame(1028, 684, -12))


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
# 5. V5 cooking line: old wall and a three-tier stainless wire rack trolley.
# ---------------------------------------------------------------------------
def draw_cooking_line(p):
    # No backsplash/worktop sandwich: the weathered shop plaster reaches the
    # floor behind a freestanding trolley. Only small remnants of old wall tiles
    # remain at the sides; there is no wall-to-wall tiled counter silhouette.
    wall = mix(P["plaster_light"], P["cream"], .34)
    p.rect((0, COUNTER_BOTTOM, W, 949), wall)
    p.poly(((0, 940), (W, 940), (W, 967), (915, 964), (749, 969),
            (566, 962), (347, 968), (160, 964), (0, 970)), wall, rough=1.3)
    rng = np.random.default_rng(SEED + 51)
    for _ in range(76):
        x, y = float(rng.uniform(7, W - 7)), float(rng.uniform(773, 950))
        rx, ry = float(rng.uniform(3, 21)), float(rng.uniform(1, 5))
        p.poly(((x - rx, y), (x - rx * .35, y - ry),
                (x + rx, y - ry * .25), (x + rx * .4, y + ry)),
               P["cream"] if rng.random() < .7 else P["terracotta"],
               int(rng.integers(18, 44)), rough=1.1)
    for x0 in (15, 893):
        for row in range(2):
            for col in range(2):
                x, y = x0 + col * 81, 784 + row * 78
                p.rounded((x, y, x + 76, y + 73), 3,
                          mix(P["cream"], wall, .25), 180, rough=1.2)
                p.line(((x + 2, y + 72), (x + 74, y + 72)),
                       P["wood_mid"], .9, 40)
                if (row + col) % 2 == 0:
                    flower(p, x + 38, y + 36, 15, P["teal"], 29, petals=4)
    p.line(((241, 882), (249, 895), (245, 905), (252, 917)),
           P["wood_mid"], .8, 29)
    p.line(((873, 801), (868, 814), (871, 824)), P["wood_mid"], .8, 27)
    draw_hanging_rack(p)

    # V5: open wire shelves, four slim tubular uprights and real castors replace
    # every part of the bench. The live microwave and hotpot use the y=900
    # top plane. The tilted table naturally hides the trolley's lower left;
    # its middle/lower right shelves and right wheels remain visible.
    draw_trolley(p)

    # Keep the recipe book and utensil jar on their two short right shelves.
    # There is no longer any cutting board, draped cloth or stool at the side.
    for box in ((1000, 875, 1070, 882), (882, 948, 1066, 956)):
        p.shadow(rectangle_points(box), 42, (2, 4), .8)
        wood(p, box, P["wood_mid"], .015, 30, .06, 4)
        p.line(((box[0] + 2, box[1]), (box[2] - 2, box[1])),
               P["wood_light"], 1.4, 140)
    draw_utensil_jar(p.frame(1033, 832, 8))
    draw_recipe_book(p.frame(967, 888, 17))


def draw_wire_tube(p, points, width=4.6, alpha=255):
    """A silver tube with an offset grey cast shadow, not a dark outline."""
    # Parallel, separate ink strokes keep the flat Venba treatment. Roughened
    # geometry and the shared paper mask also apply to these very thin tubes.
    p.line([(x + 1.4, y + 2) for x, y in points],
           P["rack_shadow"], width + .7, round(alpha * .62), rough=.35)
    p.line(points, P["rack_steel"], width, alpha, rough=.35)
    if width >= 3:
        p.line([(x - .7, y - .6) for x, y in points],
               P["rack_light"], 1.1, round(alpha * .78), rough=.18)


def draw_wire_shelf(p, front_y):
    """Shallow rounded rectangular rim and open slats, with NO solid panel."""
    left, right = TROLLEY_X
    rear_y = front_y - 22
    # Rear rim, then the small-depth wires: wall/floor shows through every gap.
    draw_wire_tube(p, ((left + 17, rear_y), (right - 17, rear_y)), 3.4)
    for x in range(left + 25, right - 17, 26):
        draw_wire_tube(p, ((x + 4, rear_y + 1), (x, front_y - 1)), 1.65, 225)
    for y in (front_y - 15, front_y - 7):
        draw_wire_tube(p, ((left + 9, y), (right - 9, y)), 1.45, 205)
    # Rounded corners and a doubled front rail echo the reference wire cart.
    rim = ((left + 17, rear_y), (left + 8, rear_y + 1),
           (left + 3, rear_y + 6), (left + 2, front_y - 7),
           (left + 8, front_y), (right - 8, front_y),
           (right - 2, front_y - 7), (right - 3, rear_y + 6),
           (right - 8, rear_y + 1), (right - 17, rear_y))
    draw_wire_tube(p, rim, 4.3)
    draw_wire_tube(p, ((left + 10, front_y + 5),
                       (right - 10, front_y + 5)), 2.5, 235)


def draw_trolley(p):
    """Front view, x~230..850: y=900/960/1008 tiers and four small wheels."""
    left, right = TROLLEY_X
    # Flat small wheel shadows sit on the exposed floor, never on the wall.
    for x, y, width in ((left + 21, 1018, 10), (right - 21, 1018, 10),
                         (left + 5, 1035, 13), (right - 5, 1035, 13)):
        p.ellipse((x - 16, y + 4, x + 20, y + 10), SHADOW, 55, rough=.7)
        draw_wire_tube(p, ((x - 2, y - 18), (x - 2, y - 7),
                           (x + 3, y - 4)), 3.2)
        p.rounded((x - width / 2, y - 8, x + width / 2, y + 8),
                  4, P["wheel"], rough=.65)
        p.ellipse((x - 2, y - 2, x + 2, y + 2), P["rack_shadow"], 215)

    for x in (left + 18, right - 18):
        draw_wire_tube(p, ((x, 876), (x, 1002), (x - 1, 1010)), 4.5, 240)
    # Paint all three shelves back-to-front in depth. The top is at y=900,
    # not y=985 as in v4. There is no wide wooden worktop hiding the wires.
    for y in reversed(TROLLEY_TIERS):
        draw_wire_shelf(p, y)
    # A shallow foreshortened bread board sits ON the middle wire shelf; its
    # knife is clear of the microwave/hotpot plane and of the rotated table.
    draw_cutting_board(p.frame(675, 940, -2))
    for x in (left + 5, right - 5):
        draw_wire_tube(p, ((x, 886), (x, 1021)), 5.1)
        for y in TROLLEY_TIERS:
            p.rounded((x - 4, y - 2, x + 4, y + 3), 2,
                      P["rack_light"], 185, rough=.35)
    # One small push handle, confined to the right end of the top shelf.
    draw_wire_tube(p, ((right - 17, 879), (right - 17, 869),
                       (right - 10, 865), (right + 9, 865),
                       (right + 14, 871), (right + 14, 892)), 4.1)


def draw_hanging_rack(p):
    p.shadow(rectangle_points((278, 776, 811, 781)), 40, (2, 3), .8)
    p.rounded((275, 773, 809, 780), 3, P["brass"], 220)
    for x in (289, 795):
        p.ellipse((x - 3, 774, x + 3, 780), P["wood_dark"], 140)
    # Retain v4's short utensils on the old wall. Live appliances render in
    # front of them, now resting on the trolley's y=900 plane.
    for i, x in enumerate((327, 400, 475, 602, 678, 756)):
        ink = P["steel"] if i % 2 == 0 else P["wood_mid"]
        p.arc((x - 3, 779, x + 3, 787), 165, 355, ink, 1.7, 225)
        p.line(((x, 785), (x - 1.5, 800)), ink, 2.7, 235)
        if i % 3 == 0:
            p.ellipse((x - 7, 798, x + 6, 810), ink)
            p.arc((x - 4, 801, x + 4, 808), 20, 150, P["plum"], 1, 60)
        elif i % 3 == 1:
            p.rounded((x - 6, 798, x + 6, 811), 2, ink)
            for shift in (-2.5, 1.5):
                p.line(((x + shift, 800), (x + shift, 808)), P["cream"], 1, 180)
        else:
            p.ellipse((x - 5, 798, x + 5, 810), ink)


def draw_cutting_board(p):
    """V5: a foreshortened wooden bread board, only on the middle tier."""
    points = [(-105, -12), (70, -12), (73, -5), (96, -5),
              (102, 0), (97, 9), (73, 9), (70, 16), (-105, 16)]
    p.shadow(points, 48, (2, 3), .5)
    p.poly([(x, y + 3) for x, y in points], P["wood_dark"], rough=1)
    p.poly(points, P["wood_light"], rough=1.1)
    p.ellipse((88, -1, 94, 4), P["wood_dark"], 170)
    for y in (-8, 1, 11):
        p.line(((-97, y), (-24, y + .5), (65, y - .5)),
               P["wood_mid"], .7, 72)
    for x in (-67, -35, 3, 45):
        p.line(((x, 7), (x + 7, 11)), P["cream"], .65, 64)
    knife = p.frame(-1, -1, 1)
    # Long blunt-ended blade with little serrations along the bread edge.
    blade = [(-82, -6), (29, -6), (30, 3)]
    for x in range(26, -75, -7):
        blade.extend(((x, 3), (x - 2, 5), (x - 4, 3)))
    blade.extend(((-82, 3), (-85, 0)))
    knife.shadow(blade, 40, (1, 2), .4)
    knife.poly(blade, P["rack_steel"], rough=.35)
    knife.line(((-79, -4), (25, -4)), P["rack_light"], .9, 175)
    knife.rounded((29, -7, 78, 4), 3, P["wood_dark"], rough=.5)
    for x in (40, 63):
        knife.ellipse((x - 1.1, -3.5, x + 1.1, -1.3), P["brass"], 195)


def draw_utensil_jar(p):
    # Local handle directions fan out; the container is also tilted 8 degrees.
    for x, top, shift in ((-15, -49, -12), (-4, -62, -3), (9, -53, 7)):
        p.line(((x, 5), (x + shift, top)), P["wood_mid"], 3, 235)
        p.ellipse((x + shift - 5, top - 10, x + shift + 5, top + 5),
                  P["wood_light"])
    p.shadow(ellipse_points((-23, -2, 24, 43)), 58, (4, 5), .9)
    p.poly(((-23, -1), (23, -1), (19, 39), (-18, 40)), P["lavender"])
    p.ellipse((-23, -6, 23, 5), P["plum"], 200)
    p.ellipse((-18, -3, 18, 2), P["wood_dark"], 150)
    p.poly(((12, 4), (22, 2), (19, 39), (10, 40)), P["plum"], 40)
    p.line(((-17, 29), (0, 30), (18, 28)), P["cream"], 3, 160)
    flower(p, -2, 16, 5, P["cream"], 155)


def draw_recipe_book(p):
    cover = [(-77, -38), (-6, -42), (0, -34), (7, -41),
             (76, -34), (78, 38), (5, 43), (-2, 37), (-77, 40)]
    p.shadow(cover, 70, (5, 7), 1.3)
    p.poly(cover, P["terracotta"], rough=1.1)
    p.poly(((-72, -34), (-8, -38), (-2, -31), (-2, 33), (-69, 34)), P["cream"])
    p.poly(((3, -31), (9, -37), (71, -30), (73, 33), (5, 37)), P["chalk"])
    p.poly(((-8, -37), (-1, -31), (3, 36), (-6, 33)), P["wood_mid"], 65)
    p.line(((0, -29), (0, 0), (2, 32)), P["terracotta"], 1, 115)
    # A recipe drawing, page rules and small ingredient dashes, NO readable text.
    flower(p, -36, -13, 12, P["terracotta"], 125, petals=4)
    p.ellipse((-50, -1, -24, 7), P["mustard"], 160)
    for y, end in ((13, -14), (20, -21), (27, -16)):
        p.line(((-59, y), (end, y - 1)), P["terracotta"], 1.5, 125)
    for y, end in ((-20, 57), (-12, 62), (-4, 48), (11, 58), (20, 54), (28, 62)):
        p.line(((16, y), (end, y + 1)), P["pink"], 1.8, 180)
    p.poly(((56, 29), (71, 23), (73, 33)), P["pink_light"], 190)
    p.poly(((5, 36), (9, 43), (12, 35)), P["teal"], 190)


# ---------------------------------------------------------------------------
# 6. V5 table: +15 degrees CLOCKWISE, visible legs and two free-corner props.
# ---------------------------------------------------------------------------
def draw_table_legs(p, table):
    """Two sturdy legs under the near rim, a low stretcher, flat floor shadows.

    Rotate the attachment points with the table, then draw upright legs in
    SCREEN coordinates. Rotating entire legs with the cloth would make them
    lean sideways instead of standing on the floor. Perspective foreshortens
    the second exposed leg; both feet stop before the button band at y=2040.
    """
    cx, cy = TABLE_PIVOT
    for _, (fx, fy), width in TABLE_LEGS:
        p.ellipse((fx - width * .85, fy - 3, fx + width * 1.15, fy + 4),
                  SHADOW, 83, rough=1)
        p.ellipse((fx - width * .55, fy - 2, fx + width * .65, fy + 2),
                  P["wood_dark"], 80, rough=.8)
    # The stretcher joins the two leg shafts below the cloth, visible against
    # the tile floor at lower-left/bottom. It is wood, not a tabletop border.
    (_, (ax, ay), _), (_, (bx, by), _) = TABLE_LEGS[:2]
    bar = ((ax - 3, ay - 35), (bx + 5, by - 35),
           (bx + 5, by - 20), (ax - 3, ay - 20))
    p.poly(bar, P["wood_dark"], rough=1.1)
    p.poly(((ax - 3, ay - 35), (bx + 5, by - 35),
            (bx + 5, by - 29), (ax - 3, ay - 29)),
           P["wood_mid"], rough=.8)
    p.line(((ax + 7, ay - 32), (bx - 9, by - 32)),
           P["wood_light"], 1.1, 115)
    for (ux, uy), (fx, fy), width in TABLE_LEGS:
        tx, ty = table.point((ux - cx, uy - cy))
        leg = ((tx - width / 2, ty - 6), (tx + width / 2, ty - 6),
               (fx + width * .44, fy), (fx - width * .44, fy))
        p.poly(leg, P["wood_mid"], rough=1.25)
        p.poly(((tx + width * .20, ty - 5), (tx + width / 2, ty - 6),
                (fx + width * .44, fy), (fx + width * .15, fy - 1)),
               P["wood_dark"], rough=.9)
        p.line(((tx - width * .23, ty + 10),
                (fx - width * .23, fy - 10)), P["wood_light"], 2.6, 140)
        p.line(((tx - 1, ty + 16), (fx - 2, fy - 8)),
               P["wood_dark"], 1, 75)
        p.poly(((fx - width * .42, fy - 5), (fx + width * .14, fy - 4),
                (fx + width * .13, fy), (fx - width * .44, fy)),
               P["wood_dark"], 90, rough=.8)


def draw_table(p):
    # Rotate the WHOLE surface, hanging cloth and props, never just the grain.
    # Paint.point is x'=cx+x*cos(a)-y*sin(a), y'=cy+x*sin(a)+y*cos(a).
    # Positive +15 means CLOCKWISE on a y-down screen: the right top corner
    # is LOWER. This is the opposite of Pillow.Image.rotate's angle sign.
    table = p.frame(TABLE_PIVOT[0], TABLE_PIVOT[1], TABLE_ANGLE)
    cx, cy = TABLE_PIVOT
    x0, y0, x1, y1 = (TABLE_BOX[0] - cx, TABLE_BOX[1] - cy,
                      TABLE_BOX[2] - cx, TABLE_BOX[3] - cy)

    # One flat offset shadow, then the legs BEFORE the surface: the slab and
    # skirt occlude their tops naturally. Nothing is drawn over live tray cells.
    table.shadow(rectangle_points((x0 + 4, y0 + 6, x1 - 4, y1 + 12)),
                 83, (10, 20), 0)
    draw_table_legs(p, table)
    # The requested unrotated rectangle is deliberately wider than the phone.
    # Rotated corners are approximately (107,871), (1286,1187),
    # (1032,2134), (-146,1818): overflow is intentional, never fitted back in.
    table.rounded((x0, y0 + 16, x1, y1 + 16), TABLE_RADIUS,
                  P["wood_dark"], rough=1.5)
    table.rounded((x0, y0, x1, y1), TABLE_RADIUS,
                  P["wood_light"], rough=1.3)
    table.rounded((x0 + 3, y0 + 3, x1 - 3, y1 - 3), TABLE_RADIUS - 3,
                  P["wood_mid"], rough=1)
    # Broken wood fibres stay on the narrow exposed rim, away from the cells.
    for side in (x0 + 5, x1 - 7):
        for y in range(-447, 455, 79):
            table.line(((side, y), (side + .8, y + 17), (side, y + 31)),
                       P["cream"], .8, 62)
    for x, y in ((x0 + 8, -273), (x0 + 7, 342),
                 (x1 - 8, -212), (x1 - 7, 310)):
        table.poly(((x - 2, y - 6), (x + 2, y - 3),
                    (x + 1, y + 6), (x - 2, y + 3)),
                   P["wood_light"], 130, rough=.7)

    cloth = (CLOTH_BOX[0] - cx, CLOTH_BOX[1] - cy,
             CLOTH_BOX[2] - cx, CLOTH_BOX[3] - cy)
    # Dusty plum covers every full 210x175 tray envelope. The slim wood rim
    # remains visible; all fold/hem details live outside those envelopes.
    table.rounded(cloth, 23, P["cloth"], rough=1.5)
    # A second ink pass settles the #8E3B4F field, retaining paper dropout and
    # ragged outer edges without letting dark floor patterns busy the cells.
    table.rounded((cloth[0] + 1.5, cloth[1] + 1.5,
                   cloth[2] - 1.5, cloth[3] - 1.5), 22,
                  P["cloth"], rough=.8)
    # Subtle parallel woven stripes follow the table's +15 degree orientation.
    # No trays, grid lines, shelf faces, labels, food, or knots are baked in.
    for y in range(int(cloth[1]) + 16, int(cloth[3]) - 6, 31):
        table.line(((cloth[0] + 4, y), (-128, y + .25),
                    (167, y - .2), (cloth[2] - 4, y)),
                   P["cloth_stripe"], 1.2, 30, rough=.22)
        table.line(((cloth[0] + 5, y + 3), (cloth[2] - 5, y + 3)),
                   P["cloth_light"], .55, 17, rough=.1)

    # A slight left-side drop and an irregular 20px near skirt read as cloth
    # hanging over a solid table. Keep every dark fold in the free edge margin.
    table.poly(((cloth[0], cloth[1] + 24), (cloth[0] + 5, cloth[1] + 24),
                (cloth[0] + 5, cloth[3] - 2), (cloth[0] - 3, cloth[3] + 13),
                (cloth[0] - 6, cloth[3] - 7)),
               P["cloth_fold"], 110, rough=1.3)
    edge = cloth[3]
    hem = [(cloth[0], edge - 5), (cloth[2], edge - 5),
           (cloth[2] + 2, edge + 17), (505, edge + 20),
           (352, edge + 16), (180, edge + 24), (18, edge + 19),
           (-151, edge + 22), (-315, edge + 17), (-457, edge + 23),
           (cloth[0] - 2, edge + 17)]
    table.shadow(hem, 35, (2, 3), 0)
    table.poly(hem, P["cloth"], rough=1.5)
    table.poly([(cloth[0], edge + 4), (cloth[2], edge + 4)] + hem[2:],
               P["cloth_fold"], 66, rough=1.2)
    # Folds and sparse fraying stop above y=2040 in SCREEN space. The broad
    # cloth extension at lower right stays a quiet field behind floor buttons.
    for x in (-530, -408, -270, -104, 58, 176):
        if table.point((x + 2, edge + 25))[1] < BUTTON_BAND[0] - 3:
            table.line(((x - 3, edge - 3), (x + 1, edge + 8),
                        (x + 2, edge + 19)), P["cloth_light"], 1.3, 53)
            table.line(((x, edge + 19), (x - 1, edge + 24)),
                       P["pink_light"], .6, 72)

    # Exactly two small props on the free lower-left corner. Their shadows,
    # banana-leaf veins and fork tines receive the same +15 degree transform.
    drawers = {"banana_leaf": draw_table_leaf, "fork": draw_table_fork}
    for name, (x, y), angle, _ in TABLE_PROPS:
        drawers[name](table.frame(x - cx, y - cy, angle))


def draw_table_leaf(p):
    points = [(-38, -10), (-18, -13), (21, -11), (39, 0),
              (17, 12), (-35, 11), (-32, 3)]
    p.shadow(points, 53, (2, 3), .6)
    p.poly(points, P["leaf"], rough=1.1)
    p.poly(((-36, -9), (-18, -12), (21, -10), (36, -1), (-34, 1)),
           P["leaf_light"], 110, rough=.65)
    p.line(((-36, 1), (-2, 0), (35, -1)), P["cream"], .8, 140)
    for x in (-26, -14, -2, 10, 22):
        p.line(((x - 7, -9), (x, 0), (x - 6, 9)), P["cream"], .6, 74)
    p.line(((14, -11), (11, -6)), P["cloth_fold"], .9, 90)


def draw_table_fork(p):
    # Horizontal local fork; the prop angle and table angle apply together.
    silhouette = [(-30, -5), (-16, -5), (-11, -2), (27, -2),
                  (30, 0), (27, 2), (-11, 2), (-16, 5), (-30, 5)]
    p.shadow(silhouette, 47, (2, 3), .6)
    p.rounded((-11, -1.7, 30, 1.7), 1.7, P["brass"], rough=.3)
    p.poly(((-19, -5), (-12, -4), (-10, -1), (-10, 1),
            (-12, 4), (-19, 5)), P["steel"], rough=.3)
    for y in (-4, -1.3, 1.3, 4):
        p.line(((-30, y), (-18, y)), P["steel"], 1.1, 245, rough=.2)
    p.line(((-9, -.8), (25, -.8)), P["cream"], .6, 112)


# ---------------------------------------------------------------------------
# 7. V5 floor: dark worn gach bong; the full y=2040..2336 band stays calm.
# ---------------------------------------------------------------------------
def draw_floor(p):
    # A single continuous floor from behind the trolley to the bottom, so
    # the wedges exposed by the rotated table have the SAME worn tile pattern.
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
    if (W, H) != (1080, 2336) or COUNTER_Y != 600:
        raise ValueError("Phone canvas and foreground boundary must stay exact.")
    if TROLLEY_X != (232, 852) or TROLLEY_TIERS != (900, 960, 1008):
        raise ValueError("Keep the freestanding three-tier wire trolley and y=900 top.")
    if OVEN_SPACE != (260, 768, 520, 900) or STOVE_SPACE != (560, 780, 840, 900):
        raise ValueError("Both live appliance bays must rest on the wire trolley.")
    if (TABLE_BOX != (-40, 1010, 1180, 1990) or TABLE_RADIUS != 26
            or TABLE_PIVOT != (560, 1500) or TABLE_ANGLE != 15.0):
        raise ValueError("Keep the exact big table, pivot and +15 screen-clockwise angle.")
    if (TRAY_X != (218, 446, 674, 902) or TRAY_Y != (1130, 1360, 1590, 1820)
            or TRAY_SIZE != (210, 175)):
        raise ValueError("Keep the sixteen game tray centres in unrotated space.")
    if P["cloth"] != "#8E3B4F":
        raise ValueError("The tablecloth's base ink must be dusty plum #8E3B4F.")
    # V5: use the complete 210x175 tray silhouettes, not merely their centres.
    # Testing in unrotated table space is exact because every tray and prop
    # receives the same rigid +15 degree transform afterwards.
    trays = tuple((x - TRAY_SIZE[0] / 2, y - TRAY_SIZE[1] / 2,
                   x + TRAY_SIZE[0] / 2, y + TRAY_SIZE[1] / 2)
                  for y in TRAY_Y for x in TRAY_X)
    for box in trays:
        if not (CLOTH_BOX[0] <= box[0] < box[2] <= CLOTH_BOX[2]
                and CLOTH_BOX[1] <= box[1] < box[3] <= CLOTH_BOX[3]):
            raise ValueError("Every live tray must remain on the calm cloth field.")
    if len(TABLE_PROPS) != 2 or {item[0] for item in TABLE_PROPS} != {
            "banana_leaf", "fork"}:
        raise ValueError("Only the banana leaf and fork belong on the free cloth corner.")
    for name, (cx, cy), degrees, box in TABLE_PROPS:
        angle = degrees * pi / 180
        c, s = cos(angle), sin(angle)
        corners = [(cx + x * c - y * s, cy + x * s + y * c)
                   for x, y in rectangle_points(box)]
        bounds = (min(x for x, _ in corners) - PROP_CLEARANCE,
                  min(y for _, y in corners) - PROP_CLEARANCE,
                  max(x for x, _ in corners) + PROP_CLEARANCE,
                  max(y for _, y in corners) + PROP_CLEARANCE)
        if not (CLOTH_BOX[0] <= bounds[0] < bounds[2] <= CLOTH_BOX[2]
                and CLOTH_BOX[1] <= bounds[1] < bounds[3] <= CLOTH_BOX[3]):
            raise ValueError(f"The {name} and its shadow must stay on the cloth.")
        for tray in trays:
            if (bounds[0] < tray[2] and bounds[2] > tray[0]
                    and bounds[1] < tray[3] and bounds[3] > tray[1]):
                raise ValueError(f"The {name} or its shadow would overlap a live tray.")
    # Explicitly guard the SCREEN-space sign, not Pillow.rotate's convention.
    a = TABLE_ANGLE * pi / 180
    left_y = (TABLE_PIVOT[1] + (TABLE_BOX[0] - TABLE_PIVOT[0]) * sin(a)
              + (TABLE_BOX[1] - TABLE_PIVOT[1]) * cos(a))
    right_y = (TABLE_PIVOT[1] + (TABLE_BOX[2] - TABLE_PIVOT[0]) * sin(a)
               + (TABLE_BOX[1] - TABLE_PIVOT[1]) * cos(a))
    if right_y <= left_y or right_y - left_y < 315:
        raise ValueError("The table's top edge must descend strongly to the right.")
    c, s = cos(a), sin(a)
    cx, cy = TABLE_PIVOT
    table_corners = [(cx + (x - cx) * c - (y - cy) * s,
                      cy + (x - cx) * s + (y - cy) * c)
                     for x, y in rectangle_points(TABLE_BOX)]
    if not (min(x for x, _ in table_corners) < 0
            and max(x for x, _ in table_corners) > W
            and max(y for _, y in table_corners) > BUTTON_BAND[0]):
        raise ValueError("The rotated table must overflow left/right and extend below y=2040.")
    if BUTTON_BAND != (2040, 2336) or not 2 <= len(TABLE_LEGS) <= 3:
        raise ValueError("Keep two or three visible legs and the full calm bottom band.")
    for (ux, uy), (fx, fy), width in TABLE_LEGS:
        attachment_y = cy + (ux - cx) * s + (uy - cy) * c
        if not (width >= 32 and attachment_y + 30 < fy < BUTTON_BAND[0] - 4
                and 0 < fx - width < fx + width < W):
            raise ValueError("Sturdy table legs must stand on the exposed floor above buttons.")
    for crop in (AWNING_CROP, LAKE_CROP):
        if not (0 <= crop[0] < crop[2] <= SOURCE_SIZE[0]
                and 0 <= crop[1] < crop[3] <= 1424):
            raise ValueError("Only crop the useful upper part of the source.")
    # Fail closed if somebody edits OUT_DIR to an asset path or a symlink leads
    # elsewhere. Every output is a direct child of this one review directory.
    expected = ROOT / "Tools" / "review" / "v5"
    if OUT_DIR != expected or expected.resolve() != expected:
        raise ValueError("Outputs must remain inside Tools/review/v5.")


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
