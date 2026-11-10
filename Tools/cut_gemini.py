"""Cut Gemini sprite sheets (items on plain white background, laid out in a grid) into transparent PNGs.

usage: python -I Tools/cut_gemini.py <sheet.png> <cols> <rows> name1,name2,... [out_dir]
Names are read row by row; use '-' to skip a cell.
"""
import os, sys
import numpy as np
from PIL import Image, ImageFilter
from collections import deque


def remove_white(im, tol=18):
    a = np.array(im.convert("RGB")).astype(np.int16)
    h, w = a.shape[:2]
    mn = a.min(axis=2); mx = a.max(axis=2)
    bgish = (mn > 255 - tol - 22) & (mx - mn < 22)          # near-white, low saturation
    seen = np.zeros((h, w), bool)
    q = deque()
    for x in range(w):
        for y in (0, h - 1):
            if bgish[y, x] and not seen[y, x]: seen[y, x] = True; q.append((y, x))
    for y in range(h):
        for x in (0, w - 1):
            if bgish[y, x] and not seen[y, x]: seen[y, x] = True; q.append((y, x))
    while q:
        y, x = q.popleft()
        for dy, dx in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            ny, nx = y + dy, x + dx
            if 0 <= ny < h and 0 <= nx < w and not seen[ny, nx] and bgish[ny, nx]:
                seen[ny, nx] = True; q.append((ny, nx))
    alpha = np.where(seen, 0, 255).astype(np.uint8)
    # soft edge: shrink 1px then blur a little
    am = Image.fromarray(alpha).filter(ImageFilter.MinFilter(3)).filter(ImageFilter.GaussianBlur(0.8))
    rgba = np.dstack([a.astype(np.uint8), np.array(am)])
    return Image.fromarray(rgba, "RGBA")


def trim(im, pad=6):
    bb = im.split()[-1].point(lambda v: 255 if v > 10 else 0).getbbox()
    if not bb: return im
    x0, y0, x1, y1 = bb
    return im.crop((max(0, x0 - pad), max(0, y0 - pad), min(im.width, x1 + pad), min(im.height, y1 + pad)))


def main():
    sheet, cols, rows, names = sys.argv[1], int(sys.argv[2]), int(sys.argv[3]), sys.argv[4].split(",")
    out = sys.argv[5] if len(sys.argv) > 5 else os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(sheet))), "cut")
    os.makedirs(out, exist_ok=True)
    im = remove_white(Image.open(sheet))
    W, H = im.size
    for i, n in enumerate(names):
        if n == "-" or i >= cols * rows: continue
        c, r = i % cols, i // cols
        cell = im.crop((c * W // cols, r * H // rows, (c + 1) * W // cols, (r + 1) * H // rows))
        cell = trim(cell)
        # keep sprites a sane size for the game (max 512 px)
        cell.thumbnail((512, 512), Image.LANCZOS)
        cell.save(os.path.join(out, n + ".png"))
        print("cut", n, cell.size)


if __name__ == "__main__":
    main()
