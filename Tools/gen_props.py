"""Deterministic, hand-inked Bep Jam UI redraws. Requires Pillow and numpy.

Run from any directory. Outputs only beneath Tools/redraw/; never reads or
modifies Assets. Geometry is authored in final pixels and rasterized at 4x.
Suggested Unity sprite borders: tile 40/40/40/40; board 110/110/110/110.
"""
from pathlib import Path
import math

import numpy as np
from PIL import Image, ImageDraw, ImageFont

S = 4
OUT = Path(__file__).resolve().parent / "redraw"
INK = "#4A2F26"
CREAM = "#FFF4E1"
BEIGE = "#F8D6A4"
CORAL = "#F28B7B"
WHITE = "#FFFCF4"
SIZES = {
    "plate": (352, 232), "stove": (440, 366), "oven": (540, 324),
    "bottle_yellow": (132, 276), "bottle_red": (132, 276),
    "tile": (360, 300), "board": (1438, 1094), "curtain": (853, 174),
    "btn_back": (132, 130), "btn_pause": (132, 130),
}


class Canvas:
    def __init__(self, name):
        self.name = name
        w, h = SIZES[name]
        self.im = Image.new("RGBA", (w*S, h*S), (74, 47, 38, 0))
        self.d = ImageDraw.Draw(self.im)

    def box(self, b):
        return tuple(round(v*S) for v in b)

    def rr(self, b, r, fill, outline=INK, width=6):
        self.d.rounded_rectangle(self.box(b), round(r*S), fill=fill,
                                 outline=outline, width=round(width*S))

    def ellipse(self, b, fill, outline=None, width=6):
        self.d.ellipse(self.box(b), fill=fill, outline=outline, width=round(width*S))

    def line(self, pts, fill=INK, width=6):
        pts = [(round(x*S), round(y*S)) for x, y in pts]
        self.d.line(pts, fill=fill, width=round(width*S), joint="curve")
        r = width*S/2
        for x, y in (pts[0], pts[-1]):
            self.d.ellipse((round(x-r), round(y-r), round(x+r), round(y+r)), fill=fill)

    def poly(self, pts, fill, outline=None, width=6):
        self.d.polygon([(round(x*S), round(y*S)) for x, y in pts], fill=fill)
        if outline:
            self.line(pts + [pts[0]], outline, width)

    def path(self, commands, fill, outline=INK, width=6):
        """Closed silhouette built from M/L/C commands, with smooth cubic ink."""
        pts = []
        for cmd in commands:
            if cmd[0] in ("M", "L"):
                pts.append((cmd[1], cmd[2]))
            elif cmd[0] == "C":
                x0, y0 = pts[-1]
                x1, y1, x2, y2, x3, y3 = cmd[1:]
                for i in range(1, 41):
                    t = i/40
                    u = 1-t
                    pts.append((u**3*x0+3*u*u*t*x1+3*u*t*t*x2+t**3*x3,
                                u**3*y0+3*u*u*t*y1+3*u*t*t*y2+t**3*y3))
        self.poly(pts, fill, outline, width)

    def save(self):
        # Filter associated alpha so invisible RGB never contaminates AA edges.
        small = self.im.convert("RGBa").resize(SIZES[self.name], Image.Resampling.LANCZOS).convert("RGBA")
        a = np.array(small)
        known = a[:, :, 3] > 0
        # Bleed the nearest local edge colours into transparent texels. No wrap.
        # Four pixels covers the LANCZOS footprint and bilinear sampling gutter.
        for _ in range(4):
            previous = known.copy()
            rgb = a[:, :, :3].copy()
            for dy, dx in ((-1, 0), (1, 0), (0, -1), (0, 1),
                           (-1, -1), (-1, 1), (1, -1), (1, 1)):
                sy = slice(max(0, -dy), min(a.shape[0], a.shape[0]-dy))
                sx = slice(max(0, -dx), min(a.shape[1], a.shape[1]-dx))
                ty = slice(max(0, dy), min(a.shape[0], a.shape[0]+dy))
                tx = slice(max(0, dx), min(a.shape[1], a.shape[1]+dx))
                take = (~known[ty, tx]) & previous[sy, sx]
                target = a[ty, tx, :3]
                target[take] = rgb[sy, sx][take]
                known[ty, tx] |= take
        a[~known, :3] = (74, 47, 38)
        result = Image.fromarray(a)
        result.save(OUT / "UI" / (self.name + ".png"))
        return result


def flower(c, x, y, r, color, center):
    for i in range(5):
        angle = -math.pi/2 + i*math.tau/5
        px, py = x+math.cos(angle)*r*.58, y+math.sin(angle)*r*.58
        c.ellipse((px-r*.42, py-r*.42, px+r*.42, py+r*.42), color)
    c.ellipse((x-r*.22, y-r*.22, x+r*.22, y+r*.22), center)


def plate():
    c = Canvas("plate")
    c.ellipse((8, 29, 344, 222), "#DDA772", INK)
    c.ellipse((8, 14, 344, 207), CREAM, INK)
    c.ellipse((34, 37, 318, 182), CORAL)
    c.ellipse((39, 42, 313, 177), "#FDE7C8")
    c.path([("M", 48, 114), ("C", 70, 164, 230, 190, 299, 130),
            ("C", 283, 185, 83, 192, 48, 114)], "#F0D0A6", None)
    c.path([("M", 66, 61), ("C", 92, 43, 140, 36, 172, 40),
            ("C", 139, 45, 98, 53, 76, 68), ("C", 70, 71, 63, 68, 66, 61)], WHITE, None)
    return c.save()


def stove():
    c = Canvas("stove")
    for x in (43, 347):
        c.rr((x, 333, x+46, 360), 12, "#7A5B49")
    c.path([("M", 39, 231), ("L", 401, 231), ("L", 428, 275),
            ("L", 12, 275)], "#7DC8E8")
    c.rr((9, 264, 431, 347), 22, "#7DC8E8")
    c.rr((17, 314, 423, 339), 13, "#4D97BF", None)
    c.line([(29, 281), (277, 281)], "#C9F1F5", 7)
    c.rr((67, 305, 179, 318), 6, "#4D97BF", INK, 4)
    c.ellipse((65, 225, 365, 271), "#62636C", INK)
    c.ellipse((109, 230, 321, 259), "#7DC8E8", INK, 4)
    c.line([(85, 233), (117, 253)], INK, 9)
    c.line([(341, 233), (309, 253)], INK, 9)
    c.ellipse((321, 280, 390, 340), "#FFD66B", INK)
    c.ellipse((331, 289, 381, 330), "#E9AF52")
    c.rr((348, 288, 361, 313), 6, CREAM, None)
    for left, right in ((23, 100), (340, 417)):
        c.rr((left, 95, right, 150), 22, "#FFD66B")
        c.rr((left+15, 109, right-15, 132), 9, "#DDA772", INK, 4)
    c.path([("M", 83, 85), ("L", 357, 85), ("L", 349, 205),
            ("C", 344, 248, 97, 248, 91, 205)], CREAM)
    c.path([("M", 99, 190), ("C", 165, 211, 277, 211, 346, 190),
            ("L", 344, 211), ("C", 302, 239, 130, 241, 97, 212)], "#F0D0A6", None)
    for x, y, r in ((139, 159, 20), (220, 171, 25), (301, 159, 20)):
        flower(c, x, y, r, CORAL, "#FFD66B")
    c.path([("M", 80, 86), ("C", 91, 21, 343, 21, 360, 86),
            ("C", 345, 116, 99, 116, 80, 86)], CORAL)
    c.path([("M", 89, 86), ("C", 158, 100, 291, 100, 351, 85),
            ("L", 351, 94), ("C", 283, 117, 147, 113, 89, 94)], "#D86E61", None)
    c.path([("M", 112, 64), ("C", 132, 49, 163, 45, 185, 47),
            ("C", 159, 53, 136, 59, 119, 69), ("C", 113, 72, 108, 68, 112, 64)], WHITE, None)
    c.rr((196, 12, 244, 43), 13, CORAL)
    c.ellipse((192, 6, 248, 29), CORAL, INK)
    c.ellipse((204, 12, 232, 18), WHITE)
    return c.save()


def oven():
    c = Canvas("oven")
    for x in (54, 435):
        c.rr((x, 284, x+48, 317), 13, "#7A5B49")
    c.rr((10, 10, 530, 301), 43, "#DDA772")
    c.rr((10, 10, 514, 287), 38, CREAM)
    c.rr((22, 240, 502, 280), 20, "#F0D0A6", None)
    c.line([(43, 30), (463, 30)], WHITE, 8)
    c.rr((43, 61, 367, 249), 26, "#7A5B49")
    c.rr((54, 72, 356, 238), 18, "#454B5B", INK, 4)
    c.poly([(75, 228), (196, 81), (247, 81), (126, 228)], "#697184")
    c.line([(379, 69), (379, 242)], "#DDA772", 6)
    c.rr((345, 112, 360, 198), 7, CREAM, INK, 4)
    for y in (99, 199):
        c.ellipse((412, y-31, 474, y+31), "#FFD66B", INK)
        c.ellipse((422, y-18, 467, y+22), "#E9AF52")
        c.line([(443, y-18), (443, y-2)], WHITE, 6)
    return c.save()


def bottle(name, fill, shade):
    c = Canvas(name)
    c.path([("M", 51, 49), ("L", 56, 13), ("C", 57, 3, 75, 3, 76, 13),
            ("L", 81, 49)], WHITE)
    c.path([("M", 34, 78), ("C", 31, 92, 14, 99, 13, 121),
            ("L", 9, 245), ("C", 9, 273, 123, 273, 123, 245),
            ("L", 119, 121), ("C", 118, 99, 101, 92, 98, 78)], fill)
    c.path([("M", 102, 104), ("C", 105, 110, 108, 116, 108, 126),
            ("L", 113, 241), ("C", 110, 258, 31, 261, 20, 244),
            ("C", 51, 247, 93, 247, 96, 230), ("L", 96, 127),
            ("C", 96, 116, 94, 109, 91, 104)], shade, None)
    c.rr((28, 46, 104, 87), 12, WHITE)
    c.line([(36, 76), (96, 76)], "#E5D4BD", 5)
    c.path([("M", 34, 120), ("C", 34, 110, 47, 109, 47, 120),
            ("L", 43, 216), ("C", 43, 227, 29, 227, 30, 215)], "#FFF0D0", None)
    return c.save()


def tile():
    c = Canvas("tile")
    c.rr((3, 3, 357, 297), 36, "#DDA772")
    c.rr((3, 3, 357, 283), 36, "#FDE7C8")
    c.rr((12, 12, 348, 273), 27, None, "#FFF4E1", 4)
    return c.save()


def board():
    c = Canvas("board")
    c.rr((5, 5, 1433, 1089), 61, "#B77B4D")
    c.rr((5, 5, 1433, 1075), 61, "#DDA772")
    c.rr((15, 15, 1423, 1065), 51, None, "#F8D6A4", 5)
    # Straight frame seams stay in the fixed 110px border, outside the panel.
    for y in (43, 1050):
        c.line([(110, y), (1328, y)], "#B77B4D", 4)
    c.rr((76, 76, 1362, 1018), 30, "#B77B4D")
    c.rr((86, 92, 1352, 1008), 23, "#C78C59", None)
    # Sparse plank joins, deliberately flat: no grain/noise or painted texture.
    for y in (320, 550, 780):
        c.line([(89, y), (1349, y)], "#B77B4D", 4)
    for x in (45, 1393):
        for y in (45, 1049):
            c.ellipse((x-15, y-15, x+15, y+15), "#FFD66B", INK, 4)
            c.line([(x-5, y+5), (x+5, y-5)], "#B77B4D", 3)
            c.ellipse((x-8, y-9, x-2, y-3), WHITE)
    return c.save()


def curtain():
    c = Canvas("curtain")
    c.rr((4, 7, 849, 34), 12, "#DDA772")
    c.line([(20, 15), (833, 15)], BEIGE, 4)
    for i in range(5):
        x = 81+i*139
        # Two cloth loops per panel make the rod connection visible.
        for loop_x in (x+13, x+105):
            c.rr((loop_x, 3, loop_x+15, 53), 5, "#274B83", INK, 4)
        c.path([("M", x, 40), ("L", x+133, 40), ("L", x+133, 163),
                ("C", x+98, 155, x+39, 176, x, 163)], "#274B83", INK, 5)
        c.poly([(x+3, 68), (x+130, 68), (x+130, 87), (x+3, 87)], "#4D97BF")
        if i % 2 == 0:
            flower(c, x+66, 123, 17, WHITE, "#FFD66B")
    return c.save()


def button(name):
    c = Canvas(name)
    c.rr((4, 4, 128, 126), 28, "#D86E61")
    c.rr((4, 4, 128, 116), 28, CORAL)
    c.line([(27, 16), (99, 16)], "#FFC3AF", 5)
    if name == "btn_pause":
        c.rr((43, 39, 57, 87), 6, WHITE, None)
        c.rr((76, 39, 90, 87), 6, WHITE, None)
    else:
        c.path([("M", 26, 60), ("L", 58, 36), ("L", 58, 50),
                ("C", 88, 47, 104, 65, 103, 89),
                ("C", 91, 73, 80, 67, 58, 70), ("L", 58, 85)], WHITE, None)
    return c.save()


def preview(images):
    """Labeled contact sheet; cream and checkerboard expose silhouette/alpha."""
    sheet = Image.new("RGBA", (1500, 1430), CREAM)
    d = ImageDraw.Draw(sheet)
    font = ImageFont.load_default(size=21)
    title = ImageFont.load_default(size=32)
    d.text((30, 20), "BEP JAM  /  KITCHEN & BOARD UI", fill=INK, font=title)
    cells = [("plate", 20, 80, 380, 300), ("stove", 420, 80, 470, 390),
             ("oven", 920, 80, 550, 350),
             ("bottle_yellow", 30, 500, 170, 340),
             ("bottle_red", 210, 500, 170, 340),
             ("tile", 410, 500, 390, 340),
             ("btn_back", 850, 530, 170, 220),
             ("btn_pause", 1050, 530, 170, 220),
             ("board", 20, 870, 780, 530),
             ("curtain", 830, 890, 640, 230)]
    for name, x, y, w, h in cells:
        d.text((x+8, y), f"{name}  {SIZES[name][0]} x {SIZES[name][1]}", fill=INK, font=font)
        top = y+34
        for yy in range(top, y+h, 16):
            for xx in range(x, x+w, 16):
                color = "#F5E4CC" if ((xx-x)//16+(yy-top)//16) % 2 else "#FFF9EF"
                d.rectangle((xx, yy, min(xx+15, x+w-1), min(yy+15, y+h-1)), fill=color)
        im = images[name].copy()
        im.thumbnail((w-16, h-48), Image.Resampling.LANCZOS)
        sheet.alpha_composite(im, (x+(w-im.width)//2, top+(h-34-im.height)//2))
    sheet.save(OUT / "props_preview.png")


def main():
    (OUT / "UI").mkdir(parents=True, exist_ok=True)
    images = {"plate": plate(), "stove": stove(), "oven": oven(),
              "bottle_yellow": bottle("bottle_yellow", "#FFD66B", "#E9AF52"),
              "bottle_red": bottle("bottle_red", "#E65A4F", "#C8433C"),
              "tile": tile(), "board": board(), "curtain": curtain(),
              "btn_back": button("btn_back"), "btn_pause": button("btn_pause")}
    for name, im in images.items():
        assert im.mode == "RGBA" and im.size == SIZES[name], name
        alpha = np.array(im.getchannel("A"))
        assert alpha.min() == 0 and alpha.max() == 255, name
    preview(images)
    print(f"Wrote {len(images)} RGBA sprites and props_preview.png to {OUT}")


if __name__ == "__main__":
    main()
