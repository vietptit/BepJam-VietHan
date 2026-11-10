"""Review-only Venba-inspired chibi customers and colourful pottery.

Run: python Tools/gen_chibi_v3.py
Dependencies: Pillow and numpy (plus the Python standard library).

All geometry, texture and preview lettering are rendered at 4x, then reduced
with Lanczos. No input artwork, system fonts, network or existing generators
are used. Seeds are stable across processes; a calm/happy pair shares exactly
the same head, hair, clothing and pigment texture. Deterministic for a fixed
Pillow/numpy version. The ONLY generated files are the 17 PNGs in
Tools/review/v3/, located relative to this script, regardless of working dir.
This module does nothing when imported. It never reads or writes Assets/.
"""

from pathlib import Path
import hashlib
import math

import numpy as np
from PIL import Image, ImageChops, ImageDraw, ImageFilter, ImageFont


SCALE = 4
SEED = "bep-jam-chibi-v3-2026-11"
OUT = Path(__file__).resolve().parent / "review" / "v3"
CUSTOMER_SIZE = (360, 360)
PLATE_SIZE = (352, 232)
RESAMPLE = Image.Resampling.LANCZOS

# Chromatic darks, never black contour lines. Each material has one flat shade.
CREAM = "#F3E3C5"
CREAM_SHADE = "#D8C5A6"
INK = "#493442"
HAIR = "#37333F"
HAIR_SHADE = "#292A35"
TAN = "#D7A079"
TAN_SHADE = "#BB7C63"
FAIR = "#EBC1A3"
FAIR_SHADE = "#CF9B87"
BLUSH_TAN = "#C87D70"
BLUSH_FAIR = "#D9948A"
WHITE_HAIR = "#E7DFCB"
WHITE_HAIR_SHADE = "#C6C3B5"
BLUE = "#91B8BD"
BLUE_SHADE = "#6C949F"
BROWN = "#9A705B"
BROWN_SHADE = "#785548"
RED = "#B94E52"
RED_SHADE = "#943D49"
PINK = "#C98591"
PINK_SHADE = "#A76378"
NAVY = "#4D5970"
NAVY_SHADE = "#384359"
MUSTARD = "#C5A052"
MUSTARD_SHADE = "#A67C40"
TERRACOTTA = "#B76F5F"

CUSTOMERS = (
    "c_vn_woman",
    "c_vn_grandpa",
    "c_vn_boy",
    "c_kr_woman",
    "c_kr_man",
    "c_kr_girl",
)
PLATE_PALETTES = (
    ("dusty teal", "#739F9B", "#527E80"),
    ("terracotta", "#C17B63", "#9D5C52"),
    ("mustard", "#C8A052", "#A47B43"),
    ("lavender", "#A49AB9", "#807895"),
)


def rng_for(key):
    """Never use Python's process-randomized hash() for artwork seeds."""
    digest = hashlib.sha256((SEED + "/" + key).encode("utf-8")).digest()
    return np.random.Generator(np.random.PCG64(int.from_bytes(digest[:8], "little")))


def rgb(colour):
    return tuple(int(colour[i:i + 2], 16) for i in (1, 3, 5))


def bezier(commands):
    """Flatten authored M/L/C paths, in final-image coordinates."""
    points = []
    for command in commands:
        op = command[0]
        if op in ("M", "L"):
            points.append((float(command[1]), float(command[2])))
        elif op == "C":
            x0, y0 = points[-1]
            x1, y1, x2, y2, x3, y3 = command[1:]
            for step in range(1, 33):
                t = step / 32.0
                u = 1.0 - t
                points.append((
                    u**3*x0 + 3*u*u*t*x1 + 3*u*t*t*x2 + t**3*x3,
                    u**3*y0 + 3*u*u*t*y1 + 3*u*t*t*y2 + t**3*y3,
                ))
        else:
            raise ValueError("Unsupported path command: " + str(op))
    return points


def oval(bounds, count=144):
    x0, y0, x1, y1 = bounds
    cx, cy = (x0 + x1) * .5, (y0 + y1) * .5
    rx, ry = (x1 - x0) * .5, (y1 - y0) * .5
    return [(cx + rx*math.cos(t), cy + ry*math.sin(t))
            for t in np.linspace(0.0, math.tau, count, endpoint=False)]


def roughen(points, rng, amount):
    """Small normal displacements: paper fibres, not a wobbly outline stroke."""
    p = np.asarray(points, dtype=np.float64)
    if np.linalg.norm(p[0] - p[-1]) < 1e-7:
        p = p[:-1]
    loop = np.vstack((p, p[0]))
    lengths = np.linalg.norm(np.diff(loop, axis=0), axis=1)
    keep = np.concatenate(([True], lengths > 1e-7))
    loop = loop[keep]
    lengths = np.linalg.norm(np.diff(loop, axis=0), axis=1)
    distance = np.concatenate(([0.0], np.cumsum(lengths)))
    total = distance[-1]
    if total < 1e-6:
        raise ValueError("A painted shape must have non-zero perimeter")
    count = max(24, int(math.ceil(total / .85)))
    positions = np.linspace(0.0, total, count, endpoint=False)
    p = np.column_stack((np.interp(positions, distance, loop[:, 0]),
                         np.interp(positions, distance, loop[:, 1])))
    tangent = np.roll(p, -1, axis=0) - np.roll(p, 1, axis=0)
    normal = np.column_stack((-tangent[:, 1], tangent[:, 0]))
    normal /= np.maximum(np.linalg.norm(normal, axis=1, keepdims=True), 1e-6)
    fibres = rng.normal(0.0, 1.0, count)
    fibres = (np.roll(fibres, -1) + 2*fibres + np.roll(fibres, 1)) / 4.0
    broad = rng.normal(0.0, .55, max(8, count // 9))
    broad = np.append(broad, broad[0])
    waviness = np.interp(np.linspace(0, len(broad)-1, count, endpoint=False),
                         np.arange(len(broad)), broad)
    displacement = np.clip(.65*fibres + .6*waviness, -1.5, 1.5) * amount
    return p + normal * displacement[:, None]


def texture_tile(mask, colour, rng, strength=1.0, opacity=255):
    """Near-flat pigment with fine speckles and sparse dry perimeter breaks.

    Work only on the shape's bounding tile. Interior alpha stays solid: the
    speckles are pigment variation rather than holes through faces or clothes.
    There is deliberately no lighting ramp, blur or gradient shading.
    """
    width, height = mask.size
    grain_size = (max(2, math.ceil(width / 5)), max(2, math.ceil(height / 5)))
    coarse = rng.integers(0, 256, (grain_size[1], grain_size[0]), dtype=np.uint8)
    paper = Image.fromarray(coarse).resize((width, height), Image.Resampling.BILINEAR)
    grain = (np.asarray(paper, dtype=np.float32) - 127.5) / 127.5
    grain *= 5.5 * strength
    grain += rng.normal(0.0, 1.1*strength, (height, width)).astype(np.float32)

    # Two working pixels per fleck = about half a final pixel before reduction.
    dot_h, dot_w = max(2, math.ceil(height / 2)), max(2, math.ceil(width / 2))
    flecks = rng.random((dot_h, dot_w), dtype=np.float32)
    flecks = np.repeat(np.repeat(flecks, 2, axis=0), 2, axis=1)[:height, :width]
    grain[flecks < .025] -= 17.0 * strength
    grain[flecks > .978] += 13.0 * strength
    base = np.asarray(rgb(colour), dtype=np.float32)
    pigment = np.clip(base[None, None, :] + grain[:, :, None], 0, 255).astype(np.uint8)

    alpha = np.array(mask, dtype=np.uint8)
    edge = np.asarray(mask) > np.asarray(mask.filter(ImageFilter.MinFilter(7)))
    dry = edge & (flecks > .89)
    alpha[dry] = (alpha[dry].astype(np.float32) * .42).astype(np.uint8)
    if opacity != 255:
        alpha = np.rint(alpha.astype(np.float32) * opacity / 255.0).astype(np.uint8)
    return Image.fromarray(np.dstack((pigment, alpha)))


class Canvas:
    """A 4x RGBA surface; all public coordinates are final pixels."""

    def __init__(self, size, key):
        self.size = size
        self.key = key
        self.serial = 0
        self.im = Image.new("RGBA", (size[0]*SCALE, size[1]*SCALE))

    def copy(self):
        other = Canvas.__new__(Canvas)
        other.size, other.key, other.serial = self.size, self.key, self.serial
        other.im = self.im.copy()
        return other

    def shape(self, points, colour, rough=.48, grain=1.0, opacity=255, clip=None):
        """Paint a textured fill, with no outline. Return an optional clip mask."""
        rng = rng_for(self.key + "/shape/" + str(self.serial))
        self.serial += 1
        p = roughen(points, rng, rough)
        left = max(0, math.floor(float(p[:, 0].min())*SCALE) - 3)
        top = max(0, math.floor(float(p[:, 1].min())*SCALE) - 3)
        right = min(self.im.width, math.ceil(float(p[:, 0].max())*SCALE) + 4)
        bottom = min(self.im.height, math.ceil(float(p[:, 1].max())*SCALE) + 4)
        if right <= left or bottom <= top:
            raise ValueError("Shape falls completely outside the canvas")
        mask = Image.new("L", (right-left, bottom-top))
        vertices = [(round(x*SCALE-left), round(y*SCALE-top)) for x, y in p]
        ImageDraw.Draw(mask).polygon(vertices, fill=255)
        if clip is not None:
            clip_mask, clip_left, clip_top = clip
            local_clip = Image.new("L", mask.size)
            local_clip.paste(clip_mask, (clip_left-left, clip_top-top))
            mask = ImageChops.multiply(mask, local_clip)
        tile = texture_tile(mask, colour, rng, grain, opacity)
        self.im.alpha_composite(tile, (left, top))
        return mask, left, top

    def ellipse(self, bounds, colour, **kwargs):
        return self.shape(oval(bounds), colour, **kwargs)

    def path(self, commands, colour, **kwargs):
        return self.shape(bezier(commands), colour, **kwargs)

    def stroke(self, points, colour, width=3.0, **kwargs):
        """A small round-ended filled ribbon for brows, smiles, pins or glasses.

        These are isolated features, never contour outlines around a sprite.
        """
        p = np.asarray(points, dtype=np.float64)
        delta = np.diff(p, axis=0)
        keep = np.concatenate(([True], np.linalg.norm(delta, axis=1) > 1e-7))
        p = p[keep]
        if len(p) < 2:
            raise ValueError("A stroke needs at least two distinct points")
        tangent = np.empty_like(p)
        tangent[0], tangent[-1] = p[1]-p[0], p[-1]-p[-2]
        if len(p) > 2:
            tangent[1:-1] = p[2:]-p[:-2]
        tangent /= np.maximum(np.linalg.norm(tangent, axis=1, keepdims=True), 1e-6)
        normal = np.column_stack((-tangent[:, 1], tangent[:, 0]))
        radius = width / 2.0
        edge_a, edge_b = p + radius*normal, p - radius*normal
        start_angle = math.atan2(normal[0, 1], normal[0, 0])
        end_angle = math.atan2(normal[-1, 1], normal[-1, 0])
        end_cap = [(p[-1, 0] + radius*math.cos(a), p[-1, 1] + radius*math.sin(a))
                   for a in np.linspace(end_angle, end_angle-math.pi, 12)]
        start_cap = [(p[0, 0] + radius*math.cos(a), p[0, 1] + radius*math.sin(a))
                     for a in np.linspace(start_angle-math.pi, start_angle-2*math.pi, 12)]
        outline = list(map(tuple, edge_a)) + end_cap + list(map(tuple, edge_b[::-1])) + start_cap
        kwargs.setdefault("rough", .14)
        kwargs.setdefault("grain", .55)
        return self.shape(outline, colour, **kwargs)

    def curve(self, commands, colour, width=3.0, **kwargs):
        return self.stroke(bezier(commands), colour, width, **kwargs)


def shoulders(c, colour, shade):
    # The head is roughly 250 px tall; the visible outfit is only 65-75 px.
    # Extend below 360 so the exported bottom is a clean, flat shoulder crop.
    body = c.path([
        ("M", 80, 372), ("L", 84, 335),
        ("C", 88, 308, 125, 296, 151, 294),
        ("L", 209, 294), ("C", 235, 296, 272, 308, 276, 335),
        ("L", 280, 372),
    ], colour)
    c.path([
        ("M", 231, 303), ("C", 257, 314, 267, 329, 268, 372),
        ("L", 284, 372), ("L", 281, 322), ("L", 254, 300),
    ], shade, clip=body)


def neck(c, skin, shade):
    c.path([
        ("M", 157, 276), ("L", 203, 276), ("L", 204, 310),
        ("C", 194, 323, 166, 323, 156, 310),
    ], skin)
    c.path([
        ("M", 157, 283), ("L", 203, 283), ("L", 203, 301),
        ("C", 187, 309, 171, 308, 157, 300),
    ], shade)


def head(c, skin, shade):
    for x in (59, 301):
        c.ellipse((x-15, 166, x+15, 216), skin)
        c.ellipse((x-7, 183, x+7, 201), shade, rough=.30, grain=.7)
    face = c.ellipse((57, 50, 303, 301), skin)
    c.path([
        ("M", 283, 102), ("C", 315, 158, 318, 239, 274, 281),
        ("C", 251, 303, 219, 308, 186, 304),
        ("C", 239, 292, 270, 264, 279, 226),
        ("C", 291, 180, 286, 132, 283, 102),
    ], shade, clip=face)
    return face


def clothes(c, kind, skin, shade):
    outfit = {
        "c_vn_woman": (BLUE, BLUE_SHADE),
        "c_vn_grandpa": (BROWN, BROWN_SHADE),
        "c_vn_boy": (CREAM, CREAM_SHADE),
        "c_kr_woman": (PINK, PINK_SHADE),
        "c_kr_man": (NAVY, NAVY_SHADE),
        "c_kr_girl": (MUSTARD, MUSTARD_SHADE),
    }
    base, dark = outfit[kind]
    shoulders(c, base, dark)
    neck(c, skin, shade)
    if kind == "c_vn_woman":
        # High, close-fitting ao dai collar and tiny side fastenings.
        c.path([
            ("M", 157, 288), ("C", 169, 296, 191, 296, 203, 288),
            ("L", 205, 319), ("C", 192, 328, 168, 327, 155, 317),
        ], BLUE)
        c.path([
            ("M", 195, 297), ("L", 204, 292), ("L", 208, 324),
            ("L", 220, 360), ("L", 211, 366), ("L", 199, 322),
        ], BLUE_SHADE)
        c.curve([("M", 160, 302), ("C", 172, 309, 188, 310, 198, 303)], CREAM, 1.8)
        for x, y in ((201, 320), (206, 335)):
            c.ellipse((x-2.2, y-2.2, x+2.2, y+2.2), CREAM, rough=.1)
    elif kind == "c_vn_grandpa":
        c.shape([(149, 294), (176, 312), (163, 329), (141, 307)], BROWN_SHADE)
        c.shape([(209, 294), (182, 312), (195, 329), (218, 307)], BROWN_SHADE)
        c.stroke([(181, 318), (181, 364)], BROWN_SHADE, 2.3)
        for y in (336, 352):
            c.ellipse((177.7, y-2.5, 182.7, y+2.5), CREAM_SHADE, rough=.15)
    elif kind == "c_vn_boy":
        c.shape([(148, 293), (180, 315), (164, 331), (136, 306)], CREAM)
        c.shape([(212, 293), (180, 315), (196, 331), (224, 306)], CREAM)
        # Red pioneer neckerchief: folded neck band, knot and pointed tails.
        c.shape([(143, 303), (180, 318), (217, 303), (208, 321),
                 (180, 332), (153, 320)], RED)
        c.shape([(175, 326), (160, 356), (180, 348), (188, 329)], RED)
        c.shape([(184, 327), (197, 360), (207, 348), (193, 325)], RED)
        c.shape([(176, 320), (187, 319), (193, 328), (181, 335), (172, 328)], RED_SHADE)
    elif kind == "c_kr_woman":
        # Crossed white dongjeong collar and small jeogori ribbon.
        c.shape([(149, 293), (185, 319), (175, 331), (136, 306)], CREAM)
        c.shape([(210, 292), (224, 305), (181, 339), (169, 325)], CREAM)
        c.shape([(219, 308), (225, 314), (187, 346), (179, 338)], PINK_SHADE)
        c.shape([(197, 333), (218, 326), (216, 339), (200, 340)], PINK_SHADE)
        c.shape([(200, 337), (193, 357), (204, 351), (207, 338)], PINK_SHADE)
        c.shape([(207, 338), (221, 357), (225, 349), (214, 335)], PINK_SHADE)
        c.ellipse((199, 333, 210, 342), PINK_SHADE, rough=.2)
    elif kind == "c_kr_man":
        c.shape([(153, 293), (180, 310), (208, 293), (215, 365), (145, 365)], CREAM)
        c.shape([(153, 293), (180, 311), (165, 326), (146, 305)], CREAM_SHADE)
        c.shape([(208, 293), (180, 311), (195, 326), (215, 305)], CREAM_SHADE)
        c.shape([(176, 313), (185, 313), (190, 323), (181, 331), (172, 322)], RED)
        c.shape([(177, 329), (185, 329), (191, 361), (181, 370), (171, 361)], RED)
        c.shape([(145, 296), (169, 332), (179, 371), (147, 339),
                 (154, 329), (137, 323)], NAVY_SHADE)
        c.shape([(215, 296), (192, 332), (182, 371), (214, 339),
                 (207, 329), (224, 323)], NAVY_SHADE)
    else:
        # Hood, a face-framing inner fold, and two tiny cream drawstrings.
        c.path([
            ("M", 133, 295), ("C", 146, 286, 214, 286, 227, 295),
            ("L", 220, 322), ("C", 204, 341, 156, 341, 140, 322),
        ], MUSTARD_SHADE)
        c.path([
            ("M", 143, 298), ("C", 155, 315, 205, 315, 217, 298),
            ("L", 221, 316), ("C", 206, 342, 155, 340, 139, 317),
        ], MUSTARD)
        c.stroke([(164, 329), (161, 350)], CREAM, 2.5)
        c.stroke([(196, 329), (199, 350)], CREAM, 2.5)


def hair_back(c, kind):
    if kind == "c_vn_woman":
        c.path([
            ("M", 70, 88), ("C", 84, 31, 269, 26, 292, 87),
            ("C", 311, 139, 304, 256, 321, 330),
            ("C", 291, 349, 272, 340, 252, 321),
            ("L", 107, 321), ("C", 84, 343, 63, 347, 40, 331),
            ("C", 56, 264, 42, 151, 70, 88),
        ], HAIR)
        c.path([
            ("M", 280, 136), ("C", 291, 213, 284, 298, 306, 337),
            ("L", 323, 332), ("C", 306, 235, 308, 163, 295, 116),
        ], HAIR_SHADE)
    elif kind == "c_kr_woman":
        # Low bun peeks out beside the lower rear of the head.
        c.ellipse((264, 158, 324, 226), HAIR)
        c.path([
            ("M", 303, 167), ("C", 326, 176, 333, 208, 307, 224),
            ("L", 293, 223), ("C", 315, 205, 315, 187, 303, 167),
        ], HAIR_SHADE)
        c.path([
            ("M", 60, 117), ("C", 64, 31, 281, 21, 301, 115),
            ("C", 315, 165, 309, 244, 284, 274),
            ("L", 77, 274), ("C", 49, 238, 45, 169, 60, 117),
        ], HAIR)
    elif kind == "c_kr_girl":
        # Silhouette-first, tapered pigtails; not ear-like round pom-poms.
        for commands in (
            [("M", 73, 166), ("C", 43, 154, 27, 182, 33, 217),
             ("C", 37, 242, 32, 266, 23, 282),
             ("C", 61, 282, 88, 254, 83, 221), ("L", 88, 185)],
            [("M", 287, 166), ("C", 317, 154, 333, 182, 327, 217),
             ("C", 323, 242, 328, 266, 337, 282),
             ("C", 299, 282, 272, 254, 277, 221), ("L", 272, 185)],
        ):
            c.path(commands, HAIR)
        c.path([
            ("M", 58, 105), ("C", 66, 24, 294, 24, 302, 105),
            ("L", 305, 219), ("L", 55, 219),
        ], HAIR)


def hair_front(c, kind):
    if kind == "c_vn_woman":
        c.path([
            ("M", 77, 111), ("C", 101, 68, 153, 67, 178, 81),
            ("C", 163, 118, 129, 135, 89, 144),
            ("C", 81, 188, 87, 263, 103, 300),
            ("C", 67, 289, 52, 227, 61, 164), ("L", 66, 126),
        ], HAIR)
        c.path([
            ("M", 179, 81), ("C", 217, 64, 272, 78, 287, 115),
            ("C", 306, 171, 295, 266, 267, 301),
            ("C", 276, 242, 279, 182, 269, 143),
            ("C", 231, 135, 202, 113, 179, 81),
        ], HAIR)
    elif kind == "c_vn_grandpa":
        # Leave the broad central scalp bald, with two soft white side tufts.
        for commands in (
            [("M", 73, 103), ("C", 48, 121, 46, 160, 57, 181),
             ("L", 63, 211), ("L", 76, 198), ("L", 80, 160),
             ("L", 88, 132), ("L", 84, 115), ("L", 74, 125)],
            [("M", 287, 103), ("C", 312, 121, 314, 160, 303, 181),
             ("L", 297, 211), ("L", 284, 198), ("L", 280, 160),
             ("L", 272, 132), ("L", 276, 115), ("L", 286, 125)],
        ):
            c.path(commands, WHITE_HAIR)
        c.curve([("M", 61, 148), ("C", 59, 165, 64, 177, 65, 189)], WHITE_HAIR_SHADE, 3)
        c.curve([("M", 299, 148), ("C", 301, 165, 296, 177, 295, 189)], WHITE_HAIR_SHADE, 3)
        c.curve([("M", 156, 112), ("C", 169, 109, 185, 109, 197, 112)], TAN_SHADE, 1.6)
    elif kind == "c_vn_boy":
        c.path([
            ("M", 59, 162), ("L", 48, 110), ("L", 62, 93),
            ("L", 50, 74), ("L", 89, 76), ("L", 89, 45),
            ("L", 123, 55), ("L", 137, 27), ("L", 163, 46),
            ("L", 189, 22), ("L", 204, 49), ("L", 237, 34),
            ("L", 240, 63), ("L", 276, 59), ("L", 274, 83),
            ("L", 307, 94), ("L", 299, 122), ("L", 303, 162),
            ("L", 282, 179), ("L", 278, 129), ("L", 250, 120),
            ("L", 242, 133), ("L", 220, 114), ("L", 197, 126),
            ("L", 174, 110), ("L", 148, 126), ("L", 126, 115),
            ("L", 102, 134), ("L", 82, 131), ("L", 78, 177),
        ], HAIR)
        c.shape([(228, 69), (263, 85), (285, 109), (279, 139),
                 (252, 118), (242, 131), (223, 115)], HAIR_SHADE)
    elif kind == "c_kr_woman":
        c.path([
            ("M", 57, 157), ("C", 48, 97, 77, 39, 151, 38),
            ("C", 168, 36, 179, 44, 186, 55),
            ("C", 179, 94, 139, 120, 91, 129),
            ("L", 78, 192), ("L", 61, 217),
        ], HAIR)
        c.path([
            ("M", 184, 54), ("C", 213, 22, 294, 59, 303, 115),
            ("C", 309, 150, 303, 196, 288, 220),
            ("L", 273, 182), ("L", 269, 128),
            ("C", 231, 117, 203, 94, 184, 54),
        ], HAIR)
        c.path([
            ("M", 275, 110), ("C", 294, 145, 294, 184, 286, 212),
            ("L", 296, 224), ("C", 312, 181, 310, 141, 298, 114),
        ], HAIR_SHADE)
        # Binyeo: a single muted brass shaft with a small jade finial.
        c.stroke([(284, 177), (332, 162)], MUSTARD, 4.0)
        c.ellipse((326, 154, 339, 168), "#83A49A", rough=.25)
        c.ellipse((332, 158, 338, 165), "#65877F", rough=.2)
    elif kind == "c_kr_man":
        c.path([
            ("M", 58, 167), ("C", 48, 137, 48, 107, 66, 86),
            ("C", 73, 52, 117, 34, 170, 37),
            ("C", 191, 20, 243, 32, 262, 56),
            ("C", 301, 65, 315, 112, 300, 164),
            ("L", 282, 181), ("L", 277, 129),
            ("C", 250, 119, 230, 93, 216, 74),
            ("C", 191, 112, 145, 132, 87, 132),
            ("L", 78, 180), ("L", 64, 185),
        ], HAIR)
        c.path([
            ("M", 218, 59), ("C", 252, 65, 280, 89, 285, 123),
            ("L", 279, 139), ("C", 257, 119, 231, 91, 218, 59),
        ], HAIR_SHADE)
        c.curve([("M", 218, 52), ("C", 215, 70, 202, 87, 187, 98)], FAIR_SHADE, 2.6)
    else:
        c.path([
            ("M", 57, 181), ("L", 55, 119),
            ("C", 56, 61, 102, 37, 179, 37),
            ("C", 256, 37, 304, 61, 305, 119),
            ("L", 303, 183), ("L", 284, 202), ("L", 278, 139),
            ("L", 250, 141), ("L", 248, 132), ("L", 243, 142),
            ("L", 213, 142), ("L", 211, 134), ("L", 205, 143),
            ("L", 177, 142), ("L", 175, 133), ("L", 169, 142),
            ("L", 141, 142), ("L", 139, 133), ("L", 134, 141),
            ("L", 83, 139), ("L", 77, 202),
        ], HAIR)
        c.shape([(274, 111), (292, 117), (300, 174), (285, 196), (279, 140)], HAIR_SHADE)
        c.stroke([(48, 182), (73, 186)], PINK, 7.0)
        c.stroke([(287, 186), (312, 182)], PINK, 7.0)


def conical_hat(c):
    # Broad conical crown, gently bowed brim, restrained straw structure.
    hat = c.path([
        ("M", 180, 15), ("C", 163, 24, 61, 96, 17, 125),
        ("C", 76, 154, 284, 154, 343, 125),
        ("C", 296, 95, 199, 23, 180, 15),
    ], "#DAC17C", rough=.65)
    c.path([
        ("M", 17, 125), ("C", 107, 139, 258, 139, 343, 125),
        ("C", 291, 154, 70, 157, 17, 125),
    ], "#B6975F", rough=.5, clip=hat)
    for x, y in ((67, 126), (118, 134), (244, 134), (295, 126)):
        c.curve([
            ("M", 180, 28),
            ("C", 180+(x-180)*.25, 54, x+(180-x)*.12, 104, x, y),
        ], "#B6975F", 1.25, grain=.5, clip=hat)


def expression(c, kind, happy):
    elderly = kind == "c_vn_grandpa"
    tan = kind.startswith("c_vn_")
    blush = BLUSH_TAN if tan else BLUSH_FAIR
    for x in (107, 253):
        c.ellipse((x-14, 218, x+14, 231), blush, rough=.4, grain=.8, opacity=142)

    brows = WHITE_HAIR if elderly else HAIR
    for left, right, lift in ((122, 145, -1), (215, 238, 1)):
        c.curve([
            ("M", left, 178+lift-(2 if happy else 0)),
            ("C", left+7, 175+lift-(2 if happy else 0),
             right-7, 175-(2 if happy else 0), right, 177-(2 if happy else 0)),
        ], brows, 4.7 if elderly else 3.0)

    for x in (133, 227):
        if happy:
            c.curve([
                ("M", x-9, 202),
                ("C", x-5, 192, x+5, 192, x+9, 202),
            ], INK, 3.4)
        else:
            c.ellipse((x-4.7, 192, x+4.7, 203.5), INK, rough=.14, grain=.4)

    # One tiny skin-tone nose mark; no face contour or nose outline.
    c.curve([
        ("M", 177, 216), ("C", 179, 218, 181, 218, 183, 216),
    ], TAN_SHADE if tan else FAIR_SHADE, 1.7, grain=.4)
    if happy:
        mouth = c.path([
            ("M", 165, 236), ("C", 175, 239, 185, 239, 195, 236),
            ("C", 194, 262, 167, 262, 165, 236),
        ], "#824953", rough=.24, grain=.6)
        c.ellipse((174, 249, 191, 261), "#D98D85", rough=.2, grain=.6, clip=mouth)
    else:
        c.curve([
            ("M", 169, 239), ("C", 175, 247, 185, 247, 191, 239),
        ], INK, 2.8)

    if elderly:
        # Short, thin goatee below the mouth, tapered to a soft point.
        c.path([
            ("M", 167, 263), ("C", 174, 269, 186, 269, 193, 263),
            ("C", 191, 277, 186, 288, 180, 295),
            ("C", 173, 287, 169, 277, 167, 263),
        ], WHITE_HAIR, rough=.45)
        c.path([
            ("M", 185, 272), ("L", 190, 268),
            ("C", 187, 283, 182, 292, 180, 295), ("L", 181, 284),
        ], WHITE_HAIR_SHADE)
        for x, sign in ((113, -1), (247, 1)):
            c.stroke([(x, 211), (x+sign*7, 213)], TAN_SHADE, 1.5)

    if kind == "c_kr_man":
        # Unfilled round frames in muted plum; eyes remain clearly visible.
        frame = "#665363"
        for x in (133, 227):
            ring = oval((x-24, 173, x+24, 221), 112)
            c.stroke(ring + [ring[0]], frame, 2.5, rough=.10)
        c.curve([("M", 157, 195), ("C", 172, 189, 188, 189, 203, 195)], frame, 2.5)
        c.stroke([(109, 191), (84, 185)], frame, 2.5)
        c.stroke([(251, 191), (276, 185)], frame, 2.5)


def customer_base(kind):
    c = Canvas(CUSTOMER_SIZE, kind)
    tan = kind.startswith("c_vn_")
    skin, shade = (TAN, TAN_SHADE) if tan else (FAIR, FAIR_SHADE)
    hair_back(c, kind)
    clothes(c, kind, skin, shade)
    head(c, skin, shade)
    hair_front(c, kind)
    if kind == "c_vn_woman":
        conical_hat(c)
    return c


def make_plate(index):
    c = Canvas(PLATE_SIZE, "plate_" + str(index))
    _, rim, shade = PLATE_PALETTES[index]
    # Small, crisp contact shadow and a shallow ceramic sidewall, no blur.
    c.ellipse((40, 168, 326, 220), "#745260", rough=.7, grain=.7, opacity=66)
    c.ellipse((15, 35, 337, 211), shade, rough=.65)
    c.ellipse((15, 25, 337, 201), rim, rough=.65)
    well = c.ellipse((41, 46, 311, 178), CREAM, rough=.5)
    c.path([
        ("M", 41, 112), ("C", 41, 72, 100, 46, 176, 46),
        ("C", 250, 46, 311, 73, 311, 112),
        ("C", 298, 78, 244, 56, 176, 56),
        ("C", 109, 56, 57, 78, 41, 112),
    ], CREAM_SHADE, rough=.35, clip=well)
    # Three small cream slip marks are decoration, not glossy highlights.
    for points in (
        [(80, 54), (87, 51)], [(96, 48), (103, 46)], [(113, 43), (120, 41)],
    ):
        c.stroke(points, CREAM, 2.6, rough=.22, grain=.9)
    return c


def downsample(c):
    # Filter associated alpha to avoid dark fringes on transparent sprites.
    result = c.im.convert("RGBa").resize(c.size, RESAMPLE).convert("RGBA")
    a = np.array(result, dtype=np.uint8)
    known = a[:, :, 3] > 0
    # A small RGB bleed outside the alpha edge protects review compositing.
    # Explicit slices prevent colour wrapping from one canvas side to another.
    for _ in range(4):
        previous, previous_rgb = known.copy(), a[:, :, :3].copy()
        for dy, dx in ((-1, 0), (1, 0), (0, -1), (0, 1),
                       (-1, -1), (-1, 1), (1, -1), (1, 1)):
            sy = slice(max(0, -dy), min(a.shape[0], a.shape[0]-dy))
            sx = slice(max(0, -dx), min(a.shape[1], a.shape[1]-dx))
            ty = slice(max(0, dy), min(a.shape[0], a.shape[0]+dy))
            tx = slice(max(0, dx), min(a.shape[1], a.shape[1]+dx))
            take = (~known[ty, tx]) & previous[sy, sx]
            target = a[ty, tx, :3]
            target[take] = previous_rgb[sy, sx][take]
            known[ty, tx] |= take
    return Image.fromarray(a)


def save_sprite(c, name):
    result = downsample(c)
    if result.mode != "RGBA" or result.size != c.size:
        raise RuntimeError("Unexpected sprite format for " + name)
    if name.startswith("c_"):
        alpha = np.asarray(result.getchannel("A"))
        if not np.all(alpha[-1, 100:260] == 255):
            raise RuntimeError("Shoulders must meet the flat bottom crop: " + name)
        if alpha[0].any() or alpha[:, 0].any() or alpha[:, -1].any():
            raise RuntimeError("Headwear or hair touches the outer canvas: " + name)
    result.save(OUT / (name + ".png"), format="PNG", optimize=False, compress_level=9)


def preview_text(im, xy, text, size, colour=CREAM):
    """Use Pillow's bundled font so the preview needs no local font files."""
    pos = (round(xy[0]*SCALE), round(xy[1]*SCALE))
    try:
        font = ImageFont.load_default(size=round(size*SCALE))
    except TypeError:
        # Older Pillow has a fixed-size default bitmap font. Render that font
        # into a separate high-resolution tile rather than use a system font.
        font = ImageFont.load_default()
        box = font.getbbox(text)
        tile = Image.new("RGBA", (box[2]-box[0]+2, box[3]-box[1]+2))
        ImageDraw.Draw(tile).text((1-box[0], 1-box[1]), text, font=font, fill=colour)
        factor = size*SCALE / max(1, box[3]-box[1])
        tile = tile.resize((max(1, round(tile.width*factor)),
                            max(1, round(tile.height*factor))), Image.Resampling.NEAREST)
        im.paste(tile, pos, tile)
    else:
        ImageDraw.Draw(im).text(pos, text, font=font, fill=colour)


def main():
    OUT.mkdir(parents=True, exist_ok=True)
    # Native-size sprite contact sheet: every character appears in both rows.
    # Paste working-resolution artwork directly, then downsample only once.
    preview_size = (2320, 1316)
    preview = Image.new("RGB", (preview_size[0]*SCALE, preview_size[1]*SCALE), TERRACOTTA)
    preview_text(preview, (40, 27), "BEP JAM / CHIBI V3", 31)
    preview_text(preview, (41, 70), "6 CUSTOMERS   /   2 EXPRESSIONS   /   4 POTTERY PALETTES", 15)
    preview_text(preview, (40, 112), "CALM SMILE", 15)
    preview_text(preview, (40, 536), "HAPPY", 15)

    written = []
    for column, kind in enumerate(CUSTOMERS):
        base = customer_base(kind)
        x = 40 + column*376
        for happy in (False, True):
            c = base.copy()
            expression(c, kind, happy)
            name = kind + ("_happy" if happy else "")
            save_sprite(c, name)
            written.append(name + ".png")
            y = 572 if happy else 148
            preview.paste(c.im, (x*SCALE, y*SCALE), c.im)
            preview_text(preview, (x+10, y+365), name, 16)

    preview_text(preview, (40, 1000), "COLOURFUL PLATES / CREAM CERAMIC WELLS", 18)
    for index, (label, _, _) in enumerate(PLATE_PALETTES):
        c = make_plate(index)
        name = "plate_" + str(index)
        save_sprite(c, name)
        written.append(name + ".png")
        x, y = 336 + index*432, 1043
        preview.paste(c.im, (x*SCALE, y*SCALE), c.im)
        preview_text(preview, (x+20, 1282), name + " / " + label, 16)

    preview.resize(preview_size, RESAMPLE).save(
        OUT / "chibi_preview.png", format="PNG", optimize=False, compress_level=9)
    written.append("chibi_preview.png")
    if len(written) != 17 or len(set(written)) != 17:
        raise RuntimeError("Expected twelve customers, four plates and one preview")
    print("Wrote 17 review PNGs to " + str(OUT))


if __name__ == "__main__":
    main()
