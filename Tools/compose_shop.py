"""Recompose the Bep Jam restaurant for the 1080 x 2336 phone canvas.

Run from any directory:  python Tools/compose_shop.py

Only Pillow, numpy, and the Python standard library are required. The source's
awning and lake-facing opening are recomposed from y=0..1424; its old counter,
table, and floor are discarded. New artwork uses flat, unoutlined silhouettes,
one warm shadow ink, muted Venba colours, and deterministic paper grain.

Outputs, relative to the project root:
    Assets/Resources/UI/bg_shop.png   -- 1080 x 2336, RGB
    Assets/Resources/UI/bg_front.png  -- same RGB; alpha 0 at y<600, 255 otherwise
    Tools/shop_preview.jpg           -- 540 x 1168, RGB

All drawing coordinates are FINAL canvas pixels, measured from the top left.
Paint at 2x, then reduce once. Plates, customers, appliances, ingredient trays,
and buttons are supplied by the game, so their spaces stay empty in this art.
"""

from math import cos, hypot, pi, sin
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw, ImageFilter


# ---------------------------------------------------------------------------
# 1. Canvas, gameplay anchors, source crops, and the muted Venba palette.
# ---------------------------------------------------------------------------
ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / "Tools" / "gemini" / "raw" / "bg_shop.png"
UI_DIR = ROOT / "Assets" / "Resources" / "UI"
PREVIEW = ROOT / "Tools" / "shop_preview.jpg"

SOURCE_SIZE = (1536, 2752)
W, H = 1080, 2336
SCALE = 2
HI_SIZE = (W * SCALE, H * SCALE)
SEED = 271828

COUNTER_Y = 600                 # Exact, hard foreground occlusion boundary.
COUNTER_FRONT = 700
COUNTER_BOTTOM = 760
COOK_BOTTOM = 990
PANTRY = (20, 990, 1060, 2000)
PANTRY_BACK = (42, 1016, 1038, 1980)
SHELF_Y = (1200, 1440, 1680, 1920)
DIVIDER_X = (300, 545, 790)
FLOOR_Y = 2010

# These guides reserve gameplay space; they are deliberately NOT painted.
CUSTOMER_X = (230, 540, 850)
PLATE_X = (200, 427, 653, 880)
TRAY_X = (175, 422, 668, 915)
TRAY_Y = (1110, 1350, 1590, 1830)
TRAY_SIZE = (230, 190)
OVEN_SPACE = (60, 770, 360, 940)
STOVE_SPACE = (720, 770, 1020, 940)

# The clear opening is 976 px wide, without the old side shelves or fridge.
# Its modest independent X/Y scaling opens up room for three customer sprites.
OPENING = (52, 150, 1028, COUNTER_Y)
LAKE_CROP = (344, 812, 1208, 1424)
AWNING_CROP = (0, 0, 1536, 302)
AWNING_BOX = (0, 0, W, 124)

P = {
    "terracotta": "#C8573E",
    "peach": "#F2B48C",
    "mustard": "#E3A33B",
    "teal": "#4E8E8A",
    "navy": "#2E3A59",
    "cream": "#FBEBD3",
    "wood": "#CF8F55",
    "wood_mid": "#B9773F",
    "wood_shadow": "#8E4D2F",
    "wood_light": "#E3B17C",
    "brass": "#D8AD61",
    "floor": "#4C716D",
    "floor_alt": "#527570",
    "tile_cream": "#B4B6A0",
    "tile_terra": "#A76D55",
}
SHADOW = P["wood_shadow"]       # One flat shadow tone, at restrained opacity.


def rgb(colour):
    """Accept a palette hex string or an RGB tuple."""
    if isinstance(colour, str):
        return tuple(int(colour[i:i + 2], 16) for i in (1, 3, 5))
    return tuple(int(c) for c in colour)


def mix(a, b, amount):
    return tuple(round(x * (1 - amount) + y * amount)
                 for x, y in zip(rgb(a), rgb(b)))


class Paint:
    """Logical-coordinate Pillow facade; alpha blends directly onto RGB art."""

    def __init__(self, image):
        self.image = image
        self.draw = ImageDraw.Draw(image, "RGBA")

    @staticmethod
    def xy(point):
        x, y = point
        return round(x * SCALE), round(y * SCALE)

    @classmethod
    def box(cls, box):
        x0, y0 = cls.xy(box[:2])
        x1, y1 = cls.xy(box[2:])
        # Pillow boxes include the final pixel. Logical boxes are half-open.
        return x0, y0, x1 - 1, y1 - 1

    @staticmethod
    def ink(colour, alpha):
        return rgb(colour) + (int(alpha),)

    def rect(self, box, colour, alpha=255):
        self.draw.rectangle(self.box(box), fill=self.ink(colour, alpha))

    def rounded(self, box, radius, colour, alpha=255):
        self.draw.rounded_rectangle(self.box(box), radius=round(radius * SCALE),
                                    fill=self.ink(colour, alpha))

    def ellipse(self, box, colour, alpha=255):
        self.draw.ellipse(self.box(box), fill=self.ink(colour, alpha))

    def poly(self, points, colour, alpha=255):
        self.draw.polygon([self.xy(pt) for pt in points],
                          fill=self.ink(colour, alpha))

    def line(self, points, colour, width=1, alpha=255):
        # Lines describe narrow physical parts or material fibres, not outlines.
        self.draw.line([self.xy(pt) for pt in points],
                       fill=self.ink(colour, alpha),
                       width=max(1, round(width * SCALE)), joint="curve")

    def arc(self, box, start, end, colour, width=1, alpha=255):
        self.draw.arc(self.box(box), start, end, fill=self.ink(colour, alpha),
                      width=max(1, round(width * SCALE)))

    def path(self, start, segments, colour, alpha=255):
        """Filled closed shape with L endpoints or C cubic Bezier segments."""
        points = [start]
        current = start
        for segment in segments:
            if segment[0] == "L":
                current = (segment[1], segment[2])
                points.append(current)
            elif segment[0] == "C":
                x0, y0 = current
                _, x1, y1, x2, y2, x3, y3 = segment
                for t in np.linspace(0, 1, 21)[1:]:
                    u = 1 - t
                    points.append((u**3 * x0 + 3 * u*u*t * x1
                                   + 3 * u*t*t * x2 + t**3 * x3,
                                   u**3 * y0 + 3 * u*u*t * y1
                                   + 3 * u*t*t * y2 + t**3 * y3))
                current = (x3, y3)
            else:
                raise ValueError("Unsupported path command: " + segment[0])
        self.poly(points, colour, alpha)


def rounded_mask(box, radius):
    mask = Image.new("L", HI_SIZE, 0)
    ImageDraw.Draw(mask).rounded_rectangle(Paint.box(box),
                                           radius=round(radius * SCALE), fill=255)
    return mask


def soft_shadow(p, box, radius, offset=(0, 5), blur=5, opacity=35):
    """Only contact shadows soften; every shadow uses the same warm ink."""
    dx, dy = offset
    shifted = (box[0] + dx, box[1] + dy, box[2] + dx, box[3] + dy)
    mask = rounded_mask(shifted, radius)
    mask = mask.filter(ImageFilter.GaussianBlur(blur * SCALE))
    mask = mask.point([round(i * opacity / 255) for i in range(256)])
    p.image.paste(rgb(SHADOW), (0, 0), mask)


def leaf(p, base, tip, width, colour, alpha=255, shadow=None):
    """Two cubic curves form a pointed, cut-paper leaf (or tile petal)."""
    bx, by = base
    tx, ty = tip
    dx, dy = tx - bx, ty - by
    length = max(hypot(dx, dy), 0.001)
    nx, ny = -dy / length * width, dx / length * width
    side_a = ("C", bx + dx * .22 + nx, by + dy * .22 + ny,
              bx + dx * .68 + nx, by + dy * .68 + ny, tx, ty)
    side_b = ("C", bx + dx * .68 - nx, by + dy * .68 - ny,
              bx + dx * .22 - nx, by + dy * .22 - ny, bx, by)
    p.path(base, [side_a, side_b], colour, alpha)
    if shadow is not None:
        p.path(base, [side_a, ("L", bx, by)], shadow, alpha)


def paste_crop(p, source, crop_box, destination, cream_wash=0):
    """Reframe just one useful source region into a logical canvas box."""
    x0, y0, x1, y1 = destination
    size = (round((x1 - x0) * SCALE), round((y1 - y0) * SCALE))
    patch = source.crop(crop_box).resize(size, Image.Resampling.LANCZOS)
    if cream_wash:
        patch = Image.blend(patch, Image.new("RGB", size, rgb(P["cream"])),
                            cream_wash)
    p.image.paste(patch, Paint.xy((x0, y0)))


# ---------------------------------------------------------------------------
# 2. Shop front / customer band, y=0..600: awning, lanterns, broad lake view.
# ---------------------------------------------------------------------------
def draw_shop_front(p, source):
    p.rect((0, 0, W, COUNTER_Y), P["terracotta"])
    p.rect((0, 124, W, 150), P["wood_mid"])

    # This is the actual open lake view, not a shrunken whole-shop photograph.
    # Source lanterns remain in the upper corners. A cream wash lowers contrast
    # in the trees and street so customers at x=230,540,850 remain the focus.
    paste_crop(p, source, LAKE_CROP, OPENING, cream_wash=.17)
    paste_crop(p, source, AWNING_CROP, AWNING_BOX)

    # Flush side jambs leave 976 px of uninterrupted opening, centred at 540.
    # Thin highlight strips describe wood thickness rather than dark outlines.
    p.rect((28, 148, 52, COUNTER_Y), P["wood"])
    p.rect((28, 148, 35, COUNTER_Y), P["wood_light"])
    p.rect((1028, 148, 1052, COUNTER_Y), P["wood"])
    p.rect((1045, 148, 1052, COUNTER_Y), SHADOW, 65)
    p.rect((0, 124, W, 130), P["wood_light"])
    p.rect((52, 150, 1028, 157), SHADOW, 30)
    for x in (31, 1031):
        p.rect((x, 135, x + 18, 142), P["brass"])
        p.ellipse((x + 7, 137, x + 10, 140), SHADOW, 100)

    # Two small pendant lamps tuck under the awning, clear of customer heads.
    # The retained lanterns in the source supply the Vietnamese-Korean accent.
    shade = mix(P["wood_mid"], P["cream"], .32)
    for cx in (112, 968):
        p.rect((cx - 1.5, 121, cx + 1.5, 144), P["brass"])
        p.rounded((cx - 7, 137, cx + 7, 150), 5, P["brass"])
        p.path((cx - 35, 164), [
            ("C", cx - 32, 146, cx - 14, 142, cx, 142),
            ("C", cx + 14, 142, cx + 32, 146, cx + 35, 164),
        ], shade)
        p.ellipse((cx - 35, 157, cx + 35, 169), P["wood_light"])
        p.ellipse((cx - 27, 159, cx + 27, 167), P["cream"])


# ---------------------------------------------------------------------------
# 3. Floor, y=2010..2336: subdued gach bong beneath the three round buttons.
# ---------------------------------------------------------------------------
def draw_floor(p):
    # Continue behind the rack, avoiding any unpainted gap at its feet.
    p.rect((0, COOK_BOTTOM, W, H), P["floor"])
    tile = 144
    for row, y in enumerate(range(FLOOR_Y, H, tile)):
        for col, x in enumerate(range(-36, W, tile)):
            if (row + col) % 2:
                p.rect((x + 1, y + 1, x + tile, y + tile), P["floor_alt"])
            p.rect((x, y, x + tile, y + 1), SHADOW, 38)
            p.rect((x, y, x + 1, y + tile), SHADOW, 38)
            cx, cy = x + tile / 2, y + tile / 2
            # Restrained four-petal cement-tile motif: no busy frame or border.
            for a in (0, pi / 2, pi, 3 * pi / 2):
                leaf(p, (cx + 5 * cos(a), cy + 5 * sin(a)),
                     (cx + 47 * cos(a), cy + 47 * sin(a)), 14,
                     P["tile_cream"], alpha=42)
            p.poly(((cx, cy - 12), (cx + 12, cy),
                    (cx, cy + 12), (cx - 12, cy)), P["tile_terra"], 65)
            p.ellipse((cx - 3, cy - 3, cx + 3, cy + 3), P["tile_cream"], 80)
            for sx, sy in ((-1, -1), (1, -1), (-1, 1), (1, 1)):
                corner = (cx + sx * 66, cy + sy * 66)
                leaf(p, corner, (corner[0] - sx * 23, corner[1] - sy * 23),
                     9, P["tile_terra"], alpha=47)

    # A single flat tonal wash calms the full button band; the tiles still show.
    p.rect((0, FLOOR_Y, W, H), P["navy"], 27)
    p.rect((0, 1996, W, FLOOR_Y), P["wood_mid"])
    p.rect((0, 2005, W, FLOOR_Y), SHADOW)


# ---------------------------------------------------------------------------
# 4. Serving counter, y=600..760: clean plate surface, panelled wood front.
# ---------------------------------------------------------------------------
def draw_counter(p):
    top = P["wood_light"]
    p.rect((0, COUNTER_Y, W, COUNTER_FRONT), top)
    p.rect((0, COUNTER_Y, W, COUNTER_Y + 5), mix(top, P["cream"], .20))

    # Sparse, faint wood fibres only. No mats, dishes, or marks at plate anchors.
    for start, end, y in ((116, 961, 625), (125, 952, 675)):
        points = [(start + (end - start) * t, y + .6 * sin(t * pi * 2))
                  for t in np.linspace(0, 1, 30)]
        p.line(points, SHADOW, .6, 10)

    # Softly lit front lip, with all panel seams below the plate surface.
    p.rect((0, 694, W, COUNTER_FRONT), mix(top, P["cream"], .35))
    p.rect((0, 694, W, 695.5), P["cream"], 65)
    p.rect((0, COUNTER_FRONT, W, COUNTER_BOTTOM), P["wood_mid"])
    p.rect((0, COUNTER_FRONT, W, 705), SHADOW, 100)
    for index, x in enumerate(range(0, W, 135)):
        if index % 2:
            p.rect((x + 2, 706, x + 134, 753), SHADOW, 13)
        if index:
            p.rect((x, 706, x + 2, 753), SHADOW, 105)
            p.rect((x + 2, 707, x + 3, 752), P["wood_light"], 40)
    p.rect((0, 753, W, COUNTER_BOTTOM), SHADOW)

    # Brass joinery is restricted to the two far ends of the front face.
    for x in (21, 1034):
        p.poly(((x, 712), (x + 25, 712), (x + 25, 717),
                (x + 6, 717), (x + 6, 735), (x, 735)), P["brass"])
        p.ellipse((x + 2, 714, x + 5, 717), SHADOW, 130)

    draw_herbs(p)               # Entire silhouette stays at x<110 and y>=600.
    draw_teapot(p)              # Entire silhouette stays at x>970 and y>=600.


def draw_herbs(p):
    """A small, two-tone basil pot at the extreme left of the serving top."""
    p.ellipse((21, 680, 100, 692), SHADOW, 42)
    p.poly(((31, 657), (87, 657), (80, 687), (39, 687)), P["terracotta"])
    p.poly(((73, 657), (87, 657), (80, 687), (69, 687)), SHADOW, 90)
    p.ellipse((29, 651, 89, 663), P["terracotta"])
    p.ellipse((36, 654, 82, 660), SHADOW, 150)
    green = mix(P["teal"], P["mustard"], .22)
    for tip in ((39, 622), (61, 612), (84, 630)):
        p.line(((59, 656), tip), green, 2)
    for base, tip, width in (
            ((46, 639), (24, 630), 6), ((44, 632), (37, 613), 6),
            ((57, 636), (52, 612), 7), ((61, 631), (70, 611), 7),
            ((66, 641), (90, 621), 7), ((76, 644), (97, 639), 6),
            ((54, 650), (29, 648), 6), ((59, 651), (78, 637), 6)):
        leaf(p, base, tip, width, green)


def draw_teapot(p):
    """One small teal pot at the extreme right; no menu or extra tableware."""
    p.ellipse((978, 682, 1068, 692), SHADOW, 42)
    # This arc is the physical open handle, not a silhouette outline.
    p.arc((1036, 647, 1064, 679), 0, 360, P["teal"], 5)
    p.poly(((1005, 658), (991, 651), (983, 641), (978, 645),
            (986, 668), (1005, 677)), P["teal"])
    p.ellipse((995, 650, 1048, 689), P["teal"])
    p.path((1029, 651), [
        ("C", 1040, 652, 1048, 662, 1048, 671),
        ("C", 1048, 682, 1038, 689, 1021, 689),
        ("C", 1034, 679, 1039, 660, 1029, 651),
    ], SHADOW, 48)
    p.ellipse((1002, 646, 1041, 656), P["teal"])
    p.ellipse((1017, 639, 1027, 649), P["mustard"])


# ---------------------------------------------------------------------------
# 5. Cooking line, y=760..980: tiled wall and worktop for game appliances.
# ---------------------------------------------------------------------------
def draw_cooking_line(p):
    wall = mix(P["cream"], P["teal"], .08)
    grout = mix(P["cream"], P["teal"], .23)
    p.rect((0, COUNTER_BOTTOM, W, COOK_BOTTOM), wall)
    tile = 88
    for row, y in enumerate(range(770, 932, tile)):
        for col, x in enumerate(range(-32, W, tile)):
            if (row + col) % 2:
                p.rect((x + 1, y + 1, x + tile, min(y + tile, 932)),
                       P["cream"], 85)
            p.rect((x, y, x + tile, y + 1), grout)
            p.rect((x, y, x + 1, min(y + tile, 932)), grout)
            # A tiny low-contrast floral insert in alternating backsplash tiles.
            if (row + col) % 2 == 0:
                cx, cy = x + tile / 2, y + tile / 2
                for a in (0, pi / 2, pi, 3 * pi / 2):
                    leaf(p, (cx, cy), (cx + 18 * cos(a), cy + 18 * sin(a)),
                         5, P["teal"], alpha=28)

    p.rect((0, 760, W, 770), P["wood_mid"])
    p.rect((0, 768, W, 773), SHADOW, 50)
    draw_utensils_and_spices(p)

    # The oven at x=60..360 and stove at x=720..1020 are live game sprites.
    # Keep both appliance footprints clear of shelf brackets and decoration.
    p.rect((0, 932, W, 956), P["wood_light"])
    p.rect((0, 932, W, 936), mix(P["wood_light"], P["cream"], .32))
    p.rect((0, 953, W, 958), P["wood"])
    p.rect((0, 958, W, 982), P["wood_mid"])
    p.rect((0, 958, W, 962), SHADOW, 55)
    p.rect((0, 982, W, COOK_BOTTOM), SHADOW)


def draw_utensils_and_spices(p):
    """Low-contrast centre details, wholly between the two appliance zones."""
    metal = mix(P["teal"], P["cream"], .52)
    brass = mix(P["brass"], P["cream"], .34)
    p.rounded((398, 793, 552, 799), 3, brass)
    for cx in (414, 536):
        p.ellipse((cx - 3, 792, cx + 3, 800), brass)

    # Ladle: slim handle and a filled bowl; there is no dark contour.
    p.arc((421, 798, 431, 813), 170, 350, metal, 3)
    p.rounded((424, 809, 428, 866), 2, metal)
    p.ellipse((416, 859, 437, 885), metal)
    p.ellipse((420, 876, 433, 884), SHADOW, 22)

    # Slotted spatula. The three short cream gaps describe real openings.
    p.arc((469, 798, 479, 813), 170, 350, metal, 3)
    p.rounded((472, 809, 476, 860), 2, metal)
    p.rounded((464, 856, 484, 888), 5, metal)
    for x in (469, 474, 479):
        p.rounded((x, 863, x + 2, 880), 1, P["cream"], 150)

    # Small rice spoon, echoing the Korean tableware without busy detail.
    p.arc((517, 798, 527, 813), 170, 350, metal, 3)
    p.rounded((520, 809, 524, 866), 2, metal)
    p.ellipse((515, 859, 530, 884), metal)

    # Three quiet spice jars; all shelf parts stay within x=572..698.
    for cx, colour, height in ((591, P["peach"], 32),
                               (630, P["cream"], 39),
                               (669, P["teal"], 29)):
        p.rounded((cx - 11, 888 - height, cx + 11, 888), 4,
                  mix(colour, P["cream"], .30))
        p.rect((cx + 6, 892 - height, cx + 10, 886), SHADOW, 17)
        p.rounded((cx - 12, 884 - height, cx + 12, 891 - height), 2, brass)
    p.rect((572, 888, 698, 893), P["wood_light"])
    p.rect((572, 893, 698, 899), P["wood"])
    for x in (582, 681):
        p.poly(((x, 899), (x + 7, 899), (x + 7, 912)), brass)


# ---------------------------------------------------------------------------
# 6. Pantry, y=990..2000: full-width, straight-on rack with four shelf boards.
# ---------------------------------------------------------------------------
def draw_pantry(p):
    # Narrow wall returns make the rack feel built in, rather than floating.
    p.rect((0, 990, 20, 2000), P["wood_mid"])
    p.rect((1060, 990, W, 2000), P["wood_mid"])
    soft_shadow(p, (20, 1978, 1060, 2000), 0, offset=(0, 5), blur=5, opacity=38)
    p.rect(PANTRY, SHADOW)
    back = mix(P["wood_mid"], SHADOW, .12)
    p.rect(PANTRY_BACK, back)

    # No card impressions, food, knots, or horizontal stripes behind the trays.
    # These faint dividers fall in the gaps between the four 230px tray columns.
    for x in DIVIDER_X:
        p.rect((x - 3, 1016, x + 3, 1980), SHADOW, 32)
        p.rect((x + 3, 1016, x + 4, 1980), P["wood_light"], 20)

    # Four full-width boards: a light top plane, flat front, one shadow below.
    # Tray bottoms land at y=1205,1445,1685,1925, overlapping the top by 5px.
    for y in SHELF_Y:
        p.rect((40, y + 20, 1040, y + 27), SHADOW, 70)
        p.rect((20, y, 1060, y + 7), P["wood_light"])
        p.rect((20, y + 7, 1060, y + 20), P["wood"])
        p.rect((20, y + 18, 1060, y + 20), SHADOW, 65)

    # Slim face posts and top/bottom joinery; never a perspective tabletop.
    p.rect((20, 1008, 42, 1985), P["wood"])
    p.rect((20, 1008, 25, 1985), P["wood_light"])
    p.rect((1038, 1008, 1060, 1985), P["wood"])
    p.rect((1054, 1008, 1060, 1985), SHADOW, 100)
    p.rect((20, 990, 1060, 1016), P["wood"])
    p.rect((20, 990, 1060, 995), P["wood_light"])
    p.rect((42, 1016, 1038, 1020), SHADOW, 60)
    p.rect((20, 1980, 1060, 1992), P["wood_mid"])
    p.rect((20, 1992, 1060, 2000), SHADOW)

    # Small brass shelf fasteners sit outside every ingredient tray footprint.
    for y in SHELF_Y:
        for x in (29, 1047):
            p.rect((x - 3, y + 6, x + 4, y + 16), P["brass"])
            p.ellipse((x - 1, y + 9, x + 2, y + 12), SHADOW, 110)


# ---------------------------------------------------------------------------
# 7. Warm lamp pools and subtle paper grain; no bevels or multi-tone shading.
# ---------------------------------------------------------------------------
def warm_lamp_light(image):
    """Broad, low-strength light from the two pendants onto the shop and wood."""
    arr = np.array(image)
    xs = np.arange(arr.shape[1], dtype=np.float32)[None, :] / SCALE
    cream = np.array(rgb(P["cream"]), dtype=np.float32)
    # Chunked arithmetic keeps the 2x portrait from becoming a huge float image.
    for y in range(0, min(arr.shape[0], 1050 * SCALE), 128):
        end = min(y + 128, arr.shape[0], 1050 * SCALE)
        ys = np.arange(y, end, dtype=np.float32)[:, None] / SCALE
        left = np.exp(-.5 * (((xs - 112) / 290)**2 + ((ys - 420) / 310)**2))
        right = np.exp(-.5 * (((xs - 968) / 290)**2 + ((ys - 420) / 310)**2))
        # Smoothly fade to zero before reaching the calm pantry back panels.
        fade = np.clip((1050 - ys) / 180, 0, 1)
        fade = fade * fade * (3 - 2 * fade)
        amount = (np.minimum(.065, .058 * (left + right)) * fade)[..., None]
        chunk = arr[y:end].astype(np.float32)
        chunk += (cream - chunk) * amount
        arr[y:end] = np.clip(np.rint(chunk), 0, 255).astype(np.uint8)
    return Image.fromarray(arr)


def add_paper_grain(image):
    """Deterministic luminance-only tooth, faint enough for the gameplay areas."""
    grain_rng = np.random.default_rng(SEED + 3)
    coarse_size = (W // 4, H // 4)
    tooth = grain_rng.normal(128, 11, (coarse_size[1], coarse_size[0]))
    tooth = Image.fromarray(np.clip(np.rint(tooth), 0, 255).astype(np.uint8))
    tooth = tooth.resize(HI_SIZE, Image.Resampling.BILINEAR)
    arr = np.array(image)
    for y in range(0, arr.shape[0], 128):
        end = min(y + 128, arr.shape[0])
        chunk = arr[y:end].astype(np.float32)
        paper = np.array(tooth.crop((0, y, arr.shape[1], end)), dtype=np.float32)
        fine = grain_rng.normal(0, .75, paper.shape).astype(np.float32)
        grain = (paper - 128) * .085 + fine
        chunk += grain[..., None]  # Same R/G/B offset: no coloured speckles.
        arr[y:end] = np.clip(np.rint(chunk), 0, 255).astype(np.uint8)
    return Image.fromarray(arr)


# ---------------------------------------------------------------------------
# 8. Assembly and export: exact phone size and an unfiltered alpha cut at 600.
# ---------------------------------------------------------------------------
def check_layout():
    """Catch accidental coordinate drift before overwriting the game's art."""
    if OPENING[2] - OPENING[0] < 900 or OPENING != (52, 150, 1028, 600):
        raise ValueError("The customer opening must stay wide and meet the counter.")
    for cx in TRAY_X:
        if cx - TRAY_SIZE[0] / 2 < PANTRY_BACK[0]:
            raise ValueError("An ingredient column falls outside the pantry back.")
        if cx + TRAY_SIZE[0] / 2 > PANTRY_BACK[2]:
            raise ValueError("An ingredient column falls outside the pantry back.")
    for cy, shelf in zip(TRAY_Y, SHELF_Y):
        if cy + TRAY_SIZE[1] / 2 != shelf + 5:
            raise ValueError("Each ingredient row must rest on its shelf board.")
    for crop in (LAKE_CROP, AWNING_CROP):
        if not (0 <= crop[0] < crop[2] <= SOURCE_SIZE[0]
                and 0 <= crop[1] < crop[3] <= 1424):
            raise ValueError("Use only the useful upper portion of the source art.")


def main():
    check_layout()
    with Image.open(SOURCE) as original:
        if original.size != SOURCE_SIZE:
            raise ValueError(f"Expected source {SOURCE_SIZE}, got {original.size}: {SOURCE}")
        source = original.convert("RGB")

    canvas = Image.new("RGB", HI_SIZE, rgb(P["cream"]))
    painter = Paint(canvas)
    draw_floor(painter)
    draw_shop_front(painter, source)
    draw_cooking_line(painter)
    draw_pantry(painter)
    draw_counter(painter)
    del painter, source

    lit = warm_lamp_light(canvas)
    del canvas
    textured = add_paper_grain(lit)
    del lit
    background = textured.resize((W, H), Image.Resampling.LANCZOS)
    del textured

    # Preserve background RGB even in transparent pixels to avoid dark fringes.
    # Construct alpha AFTER all resizing: row 599=0 and row 600=255 exactly.
    front = background.convert("RGBA")
    alpha = Image.new("L", (W, H), 0)
    alpha.paste(255, (0, COUNTER_Y, W, H))
    front.putalpha(alpha)

    if background.size != (1080, 2336) or background.mode != "RGB":
        raise RuntimeError("The shop must be a 1080 x 2336 RGB image.")
    if front.size != background.size or front.mode != "RGBA":
        raise RuntimeError("The foreground must be a matching RGBA image.")
    if front.convert("RGB").tobytes() != background.tobytes():
        raise RuntimeError("Foreground RGB does not match the shop background.")
    if alpha.crop((0, 0, W, COUNTER_Y)).getextrema() != (0, 0):
        raise RuntimeError("Foreground must be fully transparent above y=600.")
    if alpha.crop((0, COUNTER_Y, W, H)).getextrema() != (255, 255):
        raise RuntimeError("Foreground must be fully opaque from y=600 down.")

    UI_DIR.mkdir(parents=True, exist_ok=True)
    background.save(UI_DIR / "bg_shop.png", format="PNG", optimize=True)
    front.save(UI_DIR / "bg_front.png", format="PNG", optimize=True)
    preview = background.resize((W // 2, H // 2), Image.Resampling.LANCZOS)
    preview.save(PREVIEW, format="JPEG", quality=94, subsampling=0, optimize=True)
    for output in (UI_DIR / "bg_shop.png", UI_DIR / "bg_front.png", PREVIEW):
        print(f"Saved {output.relative_to(ROOT)}")


if __name__ == "__main__":
    main()
