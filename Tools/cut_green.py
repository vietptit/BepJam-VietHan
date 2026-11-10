"""Chroma-key cut for Gemini sheets drawn on flat green (#00C853).
usage: python -I Tools/cut_green.py <sheet.png> <cols> <rows> name1,name2,...   (outputs to Tools/gemini/cut/)
Keeps only the largest connected blob per cell (drops the Gemini sparkle watermark and stray specks)."""
import os, sys
import numpy as np
from PIL import Image, ImageFilter
from scipy import ndimage as ndi  # noqa: optional

def key(im):
    a = np.array(im.convert("RGB")).astype(np.float32)
    r, g, b = a[..., 0], a[..., 1], a[..., 2]
    k = np.clip((g - np.maximum(r, b) - 30) / 45.0, 0, 1)
    alpha = 1 - k
    mrb = np.maximum(r, b)
    g2 = np.where(g > mrb, mrb + (g - mrb) * 0.1, g)          # despill
    out = np.dstack([r, g2, b, alpha * 255]).astype(np.uint8)
    img = Image.fromarray(out, "RGBA")
    img.putalpha(img.split()[-1].filter(ImageFilter.MinFilter(3)).filter(ImageFilter.GaussianBlur(0.6)))
    return img

def largest(cell):
    a = np.array(cell)
    mask = a[..., 3] > 40
    lab, n = ndi.label(mask)
    if n > 1:
        sizes = ndi.sum(mask, lab, range(1, n + 1))
        keep = 1 + int(np.argmax(sizes))
        big = sizes.max()
        ok = [i + 1 for i, s in enumerate(sizes) if s > big * 0.02 and i + 1 != keep]
        sel = np.isin(lab, [keep] + ok)
        sel = ndi.binary_dilation(sel, iterations=3)
        a[..., 3] = np.where(sel, a[..., 3], 0)
    img = Image.fromarray(a, "RGBA")
    bb = img.split()[-1].point(lambda v: 255 if v > 20 else 0).getbbox()
    return img.crop((max(0, bb[0] - 6), max(0, bb[1] - 6), min(img.width, bb[2] + 6), min(img.height, bb[3] + 6))) if bb else img

def main():
    sheet, cols, rows, names = sys.argv[1], int(sys.argv[2]), int(sys.argv[3]), sys.argv[4].split(",")
    out = os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(sheet))), "cut")
    os.makedirs(out, exist_ok=True)
    img = key(Image.open(sheet)); W, H = img.size
    for i, n in enumerate(names):
        if n == "-": continue
        c, r = i % cols, i // cols
        cell = largest(img.crop((c * W // cols, r * H // rows, (c + 1) * W // cols, (r + 1) * H // rows)))
        cell.thumbnail((512, 512), Image.LANCZOS)
        cell.save(os.path.join(out, n + ".png")); print("cut", n, cell.size)

if __name__ == "__main__":
    main()
