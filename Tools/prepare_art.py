"""Copy the artist's sprites into Assets/Resources with clean ASCII names and build a text-free frame.

Run from anywhere:  python -I Tools/prepare_art.py
"""
import os, shutil, sys
from PIL import Image, ImageDraw, ImageFilter
import numpy as np

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))          # BepJam2D
ART = os.path.dirname(ROOT)                                                 # GAME VN - HQ
RES = os.path.join(ROOT, "Assets", "Resources")

# ingredient / dish id -> source file (note: some source file names are swapped, mapping fixed by eye)
FOOD = {
    "banh_mi": "nguyenlieu/banh mi.png",
    "cha_lua": "nguyenlieu/chagio.png",
    "com": "nguyenlieu/com-1.png",
    "kimchi": "nguyenlieu/kimchi.png",
    "ca_phe": "nguyenlieu/caphe.png",
    "sua_dac": "nguyenlieu/suadac.png",
    "mut_yuja": "nguyenlieu/mut juja.png",
    "nuoc_nong": "nguyenlieu/nuoc.png",
    "gao_nep": "nguyenlieu/com.png",
    "dau_xanh": "nguyenlieu/dau xanh.png",
    "bot_banh_ca": "nguyenlieu/dau do.png",
    "dau_do": "nguyenlieu/bot banh ca.png",
    "trung": "nguyenlieu/trung.png",
    "banh_gao": "nguyenlieu/tobokki.png",
    "sot_tteok": "nguyenlieu/sot tok.png",
    # dishes
    "d_banh_mi_cha": "nguyenlieu/Banh mi cha.png",
    "d_com_kimchi": "nguyenlieu/Com tron kim chi.png",
    "d_ca_phe_sua": "nguyenlieu/Cafe sua dac.png",
    "d_tra_yuja": "nguyenlieu/Tra juja.png",
    "d_xoi_dau_xanh": "nguyenlieu/xoi dau xanh.png",
    "d_bungeoppang": "nguyenlieu/Bungeoppang dau do.png",
    "d_banh_mi_trung": "nguyenlieu/banh mi trung.png",
    "d_tteokbokki": "nguyenlieu/tokboki.png",
}
UI = {
    "plate": "UI/dia.png",
    "stove": "UI/bep ga.png",
    "oven": "UI/lo vi song.png",
    "bottle_yellow": "UI/tuong ca.png",
    "bottle_red": "UI/tuong ot.png",
    "curtain": "UI/thanh treo.png",
    "tile": "UI/select food.png",
    "btn_back": "UI/return button.png",
    "btn_pause": "UI/pause butoon.png",
    "board": "UI/Painterly Wooden Game UI Board.png",
    "bg_lake": "UI/Sunny Lakeside Pagoda Promenade.png",
    "ref_full": "UI/FULL.png",
}


def trim(im, pad=4):
    """Crop transparent margins (keeps a small pad) so RectTransforms match the visible art."""
    a = np.array(im.split()[-1])
    ys, xs = np.where(a > 8)
    if len(xs) == 0:
        return im
    x0, x1 = max(0, xs.min() - pad), min(im.width, xs.max() + 1 + pad)
    y0, y1 = max(0, ys.min() - pad), min(im.height, ys.max() + 1 + pad)
    return im.crop((x0, y0, x1, y1))


def bleed(im):
    """Give fully transparent pixels the colour of their nearest opaque neighbour (no dark fringes when filtered)."""
    arr = np.array(im).astype(np.float32)
    alpha = arr[..., 3:4] / 255.0
    rgb = arr[..., :3] * alpha
    w = alpha.copy()
    for _ in range(6):
        rgb_b = Image.fromarray(np.clip(rgb, 0, 255).astype(np.uint8)).filter(ImageFilter.BoxBlur(2))
        w_b = Image.fromarray(np.clip(w[..., 0] * 255, 0, 255).astype(np.uint8)).filter(ImageFilter.BoxBlur(2))
        rgb = np.maximum(rgb, np.array(rgb_b).astype(np.float32) * (w <= 0.01))
        w = np.maximum(w, (np.array(w_b).astype(np.float32)[..., None] / 255.0) * (w <= 0.01))
    col = np.where(w > 0.001, rgb / np.maximum(w, 1e-3), arr[..., :3])
    out = arr.copy()
    trans = arr[..., 3] < 2
    out[..., :3][trans] = np.clip(col[trans], 0, 255)
    return Image.fromarray(out.astype(np.uint8), "RGBA")


def save(im, sub, name):
    d = os.path.join(RES, sub)
    os.makedirs(d, exist_ok=True)
    im.save(os.path.join(d, name + ".png"))


def clean_frame():
    """frame.png has HUD text and ingredient tiles painted in. Erase the text in the HUD boxes
    (row-median fill inside each rounded box) so the game can draw live values on top."""
    fr = Image.open(os.path.join(ART, "UI/frame.png")).convert("RGBA")
    a = np.array(fr).astype(np.int32)
    h, w = a.shape[:2]

    def dark(px):
        return px[3] > 200 and px[0] < 130 and px[1] < 100 and px[2] < 90

    # find box outlines along a scanline through the HUD (y=96 is inside every box, above text baseline)
    y = 96
    edges = [x for x in range(10, w - 10) if dark(a[y, x]) and not dark(a[y, x - 1])]
    print("HUD outline edges at y=96:", edges)
    boxes = []
    # boxes known approximately from the mock-up; snap to detected edges when close
    approx = [(24, 461), (474, 581), (589, 690), (699, 826)]
    for (x0, x1) in approx:
        sx0 = min(edges, key=lambda e: abs(e - x0)) if edges else x0
        sx1 = min(edges, key=lambda e: abs(e - x1)) if edges else x1
        if abs(sx0 - x0) > 12: sx0 = x0
        if abs(sx1 - x1) > 12: sx1 = x1
        boxes.append((sx0, 0, sx1, 0))
    # vertical extents measured on the mock-up (title box is taller than the stat chips)
    boxes = [(24, 80, 461, 177), (476, 83, 580, 171), (591, 83, 689, 171), (701, 83, 828, 171)]
    print("HUD boxes:", boxes)

    out = a.copy()
    for (x0, y0, x1, y1) in boxes:
        inset = 9
        ix0, iy0, ix1, iy1 = x0 + inset, y0 + inset, x1 - inset, y1 - inset
        mask = Image.new("L", (w, h), 0)
        ImageDraw.Draw(mask).rounded_rectangle((ix0, iy0, ix1, iy1), radius=12, fill=255)
        m = np.array(mask) > 0
        for yy in range(iy0, iy1 + 1):
            row = a[yy, ix0:ix1 + 1, :3]
            lum = row.sum(axis=1)
            light = row[lum > np.percentile(lum, 45)]
            med = np.median(light, axis=0) if len(light) else np.median(row, axis=0)
            sel = m[yy]
            out[yy, sel, :3] = med
    img = Image.fromarray(np.clip(out, 0, 255).astype(np.uint8), "RGBA")
    # soften the filled areas a touch (vertical gradient stays)
    return img, boxes


def main():
    for k, src in FOOD.items():
        im = Image.open(os.path.join(ART, src)).convert("RGBA")
        save(bleed(trim(im)), "Food", k)
    for k, src in UI.items():
        im = Image.open(os.path.join(ART, src)).convert("RGBA")
        if k not in ("bg_lake", "ref_full", "board", "curtain"):
            im = trim(im)
        if k != "bg_lake" and k != "ref_full":
            im = bleed(im)
        save(im, "UI", k)
    frame, boxes = clean_frame()
    save(frame, "UI", "frame")
    fdir = os.path.join(RES, "Fonts")
    os.makedirs(fdir, exist_ok=True)
    shutil.copyfile(os.path.join(ART, "Asset", "SRN-CookieRun-Black-1.otf"), os.path.join(fdir, "CookieRun.otf"))
    # homepage design reference (Unity flattens PSDs on import)
    rdir = os.path.join(ROOT, "Assets", "_Game", "ArtRef")
    os.makedirs(rdir, exist_ok=True)
    for n in ("homepage.psd",):
        shutil.copyfile(os.path.join(ART, "Asset", n), os.path.join(rdir, n))
    print("done:", len(FOOD), "food,", len(UI), "ui")


if __name__ == "__main__":
    main()
