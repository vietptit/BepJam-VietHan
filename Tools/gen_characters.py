"""Deterministic, 4x supersampled BepJam customer sprite generator.

Run from any directory with Python + Pillow + numpy. Writes only the character
PNGs, characters.json, and Tools/characters_preview.png.
"""
from pathlib import Path
import json
import math
import numpy as np
from PIL import Image, ImageDraw, ImageFilter, ImageFont

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / 'Assets/Resources/Characters'
S = 4
SIZE = (360, 440)
INK = '#4A2F26'
CREAM = '#FFF4E1'
LAYERS = ('body', 'head', 'eyes_open', 'eyes_closed', 'eyes_sad',
          'mouth_idle', 'mouth_happy', 'mouth_sad', 'blush')
DATA = [('bear', 'Gấu Xanh', '#A6D365', '#8DB74F'),
        ('bunny', 'Thỏ Bông', '#FFF9EE', '#E9DCCE'),
        ('cat', 'Mèo Cam', '#F4B061', '#D98B47'),
        ('panda', 'Gấu Trúc', '#FFF9EE', '#E5D9CA'),
        ('puppy', 'Cún Nâu', '#CD966A', '#AF754F'),
        ('fox', 'Cáo Nhỏ', '#EEA052', '#D7803F')]

def canvas():
    return Image.new('RGBA', (360*S, 440*S))

def box(b):
    return tuple(round(v*S) for v in b)

def ellipse(im, b, fill, outline=INK, width=7):
    ImageDraw.Draw(im).ellipse(box(b), fill, outline, round(width*S))

def poly(im, pts, fill, outline=INK, width=7):
    d = ImageDraw.Draw(im)
    p = [(round(x*S), round(y*S)) for x, y in pts]
    d.polygon(p, fill)
    if outline:
        d.line(p+[p[0]], outline, round(width*S), joint='curve')

def line(im, pts, fill=INK, width=6):
    p = [(round(x*S), round(y*S)) for x, y in pts]
    d = ImageDraw.Draw(im)
    d.line(p, fill, round(width*S), joint='curve')
    r = width*S/2
    for x, y in (p[0], p[-1]):
        d.ellipse((int(x-r), int(y-r), int(x+r), int(y+r)), fill)

def arc(im, b, a, z, fill=INK, width=6):
    x,y,r,t = b
    pts = [( (x+r)/2+(r-x)/2*math.cos(math.radians(v)),
             (y+t)/2+(t-y)/2*math.sin(math.radians(v)))
           for v in np.linspace(a,z,50)]
    line(im, pts, fill, width)

def union_head(kind, color, shade):
    mask = Image.new('L', (360*S,440*S))
    d = ImageDraw.Draw(mask)
    d.ellipse(box((57,92,303,309)), fill=255)
    if kind in ('bear','panda'):
        for b in ((53,69,124,147),(236,69,307,147)):
            d.ellipse(box(b), fill=255)
    elif kind == 'bunny':
        for b in ((84,18,143,161),(217,18,276,161)):
            d.ellipse(box(b), fill=255)
    elif kind in ('cat','fox'):
        for pts in (((64,156),(67,48),(143,111)), ((217,111),(293,48),(296,156))):
            d.polygon([(x*S,y*S) for x,y in pts], fill=255)
    else:
        for b in ((34,100,108,264),(252,100,326,264)):
            d.ellipse(box(b), fill=255)
    im = canvas()
    border = mask.filter(ImageFilter.MaxFilter(49))
    im.paste(INK, (0,0), border)
    im.paste(color,(0,0),mask)
    shadow = canvas()
    ellipse(shadow,(85,159,312,327),shade,None)
    # Only a crescent at the lower/right perimeter.
    cut = Image.new('L',mask.size)
    ImageDraw.Draw(cut).ellipse(box((39,60,288,288)),fill=255)
    alpha = np.minimum(np.array(shadow.getchannel('A')),np.array(mask))
    alpha[np.array(cut)>0]=0
    shadow.putalpha(Image.fromarray(alpha))
    im.alpha_composite(shadow)
    if kind in ('bear','panda'):
        for b in ((72,88,106,126),(254,88,288,126)):
            ellipse(im,b,'#7ED6C6' if kind=='bear' else INK,None)
    if kind == 'bunny':
        for b in ((99,35,129,126),(231,35,261,126)):
            ellipse(im,b,'#F4B3B4',None)
    if kind in ('cat','fox'):
        for pts in (((78,126),(79,72),(119,113)),((241,113),(281,72),(282,126))):
            poly(im,pts,'#F4B3A6',None)
    if kind == 'puppy':
        for b in ((36,107,94,254),(266,107,324,254)):
            ellipse(im,b,shade)
    if kind == 'cat':
        for pts in ([(151,104),(159,137)],[(180,101),(180,138)],[(209,104),(201,137)],
                    [(65,195),(92,204)],[(268,204),(295,195)]):
            line(im,pts,shade,10)
    if kind == 'panda':
        for b in ((97,167,149,227),(211,167,263,227)):
            ellipse(im,b,INK,None)
    # A broad cream muzzle leaves clear room for the eye layers above it.
    if kind != 'cat':
        ellipse(im,(128,224,232,287),CREAM,None)
    else:
        ellipse(im,(135,229,225,279),CREAM,None)
    ellipse(im,(87,137,101,161),'#FFFFFF',None)
    ellipse(im,(103,129,111,137),'#FFFFFF',None)
    if kind == 'bear':
        poly(im,[(45,113),(180,26),(315,113),(271,126),(89,126)],'#F3D9A0')
        for x in (91,122,151,209,238,269):
            line(im,[(180,39),(x,111)],'#D8B47D',2)
        line(im,[(75,105),(285,105)],'#D8B47D',3)
        arc(im,(62,99,298,124),0,180,'#B78C58',3)
    if kind == 'panda':
        arc(im,(64,70,296,172),190,350,'#E65A4F',13)
        poly(im,[(137,96),(137,66),(155,47),(205,47),(223,66),(223,96)],'#E65A4F')
        line(im,[(151,76),(209,76)],'#FFD66B',5)
        ellipse(im,(172,57,188,73),'#FFD66B',None)
    return im

def body(kind,color,shade):
    im=canvas()
    outfit={'bear':'#DDA772','bunny':'#F2ABB5','cat':'#7ED6C6',
            'panda':'#A6B9DE','puppy':'#FFD66B','fox':'#7DC8E8'}[kind]
    ellipse(im,(105,282,255,438),outfit)
    ellipse(im,(121,400,170,434),shade)
    ellipse(im,(190,400,239,434),shade)
    ellipse(im,(83,312,132,380),color)
    ellipse(im,(228,312,277,380),color)
    ellipse(im,(91,347,126,380),shade,None)
    ellipse(im,(234,347,269,380),shade,None)
    poly(im,[(140,291),(180,311),(220,291),(211,326),(149,326)],CREAM,width=5)
    if kind in ('bunny','fox'):
        poly(im,[(144,290),(180,307),(212,291),(219,304),(181,328),(138,306)],CREAM,width=4)
        knot='#E65A4F' if kind=='bunny' else '#FFD66B'
        ellipse(im,(184,316,200,332),knot, width=3)
        line(im,[(192,326),(177,355)],knot,8)
        line(im,[(197,326),(214,349)],knot,8)
        if kind=='bunny':
            arc(im,(168,315,193,334),0,310,knot,5)
            arc(im,(195,315,220,334),-130,180,knot,5)
    if kind=='bear':
        poly(im,[(136,312),(151,319),(151,360),(209,360),(209,319),(224,312),(231,407),(129,407)],'#C88B48',width=5)
        arc(im,(160,364,200,395),0,180,INK,4)
    if kind=='cat':
        line(im,[(180,315),(180,406)],'#4F9E91',3)
        ellipse(im,(206,329,228,351),'#E65A4F',width=3)
        star=[]
        for i in range(10):
            a=-math.pi/2+i*math.pi/5; r=8 if i%2==0 else 3.5
            star.append((217+math.cos(a)*r,340+math.sin(a)*r))
        poly(im,star,'#FFD66B',None)
    if kind=='puppy':
        line(im,[(137,310),(131,393)],'#A97650',13)
        line(im,[(223,310),(229,393)],'#A97650',13)
        poly(im,[(136,291),(180,306),(225,291),(221,318),(176,327),(139,317)],'#E65A4F',width=5)
        poly(im,[(192,318),(225,323),(217,363),(196,350)],'#E65A4F',width=5)
    ellipse(im,(152,339,160,347),'#FFFFFF',None)
    return im

def face_layer(kind, layer):
    im=canvas()
    if layer=='blush':
        for b in ((88,226,124,247),(236,226,272,247)):
            ellipse(im,b,'#7ED6C6' if kind=='bear' else '#F28B7B',None)
    elif layer=='eyes_open' or layer=='eyes_sad':
        for x in (130,230):
            if kind=='panda':
                ellipse(im,(x-13,182,x+13,214),CREAM,None)
            ellipse(im,(x-10,185,x+10,211),INK,None)
            ellipse(im,(x-5,188,x,195),'#FFFFFF',None)
        if layer=='eyes_sad':
            line(im,[(116,177),(137,169)],width=5)
            line(im,[(223,169),(244,177)],width=5)
    elif layer=='eyes_closed':
        for x in (130,230):
            if kind=='panda':
                arc(im,(x-13,188,x+13,213),205,335,CREAM,10)
            arc(im,(x-13,188,x+13,213),205,335,width=6)
    elif layer=='mouth_idle':
        ellipse(im,(173,233,187,243),INK,None)
        line(im,[(180,242),(180,250)],width=4)
        arc(im,(163,242,180,258),0,160,width=4)
        arc(im,(180,242,197,258),20,180,width=4)
    elif layer=='mouth_happy':
        ellipse(im,(162,237,198,273),'#743B36',width=5)
        ellipse(im,(171,256,190,268),'#F28B7B',None)
        line(im,[(170,241),(189,241)],CREAM,4)
    elif layer=='mouth_sad':
        ellipse(im,(175,233,185,240),INK,None)
        line(im,[(165,260),(173,253),(180,256),(187,253),(195,260)],width=4)
    return im

def export(im,path):
    # Pillow resizes RGBA in premultiplied form. Extend RGB into all zero-alpha
    # texels to avoid dark/white fringes in straight-alpha sprite importers.
    im=im.resize(SIZE,Image.Resampling.LANCZOS)
    a=np.array(im)
    known=a[:,:,3]>0
    rgb=a[:,:,:3].copy()
    for _ in range(440):
        if known.all(): break
        nxt=known.copy()
        for dy,dx in ((1,0),(-1,0),(0,1),(0,-1)):
            shifted=np.roll(known,(dy,dx),(0,1))
            if dy==1: shifted[0,:]=False
            if dy==-1: shifted[-1,:]=False
            if dx==1: shifted[:,0]=False
            if dx==-1: shifted[:,-1]=False
            take=~nxt & shifted
            rgb[take]=np.roll(rgb,(dy,dx),(0,1))[take]
            nxt[take]=True
        known=nxt
    a[:,:,:3]=rgb
    Image.fromarray(a).save(path)
    return Image.fromarray(a)

def main():
    OUT.mkdir(parents=True,exist_ok=True)
    preview=Image.new('RGBA',(3*360,6*480),'#D9EFF3')
    font_path=Path('C:/Windows/Fonts/arial.ttf')
    font=ImageFont.truetype(str(font_path),22) if font_path.exists() else ImageFont.load_default()
    for row,(kind,name,color,shade) in enumerate(DATA):
        imgs={}
        for layer in LAYERS:
            raw=body(kind,color,shade) if layer=='body' else union_head(kind,color,shade) if layer=='head' else face_layer(kind,layer)
            imgs[layer]=export(raw,OUT/f'{kind}_{layer}.png')
        for col,(emotion,eyes,mouth) in enumerate((('idle','open','idle'),('happy','closed','happy'),('sad','sad','sad'))):
            assembled=Image.new('RGBA',SIZE)
            for layer in ('body','head',f'eyes_{eyes}',f'mouth_{mouth}','blush'):
                assembled.alpha_composite(imgs[layer])
            preview.alpha_composite(assembled,(col*360,row*480+32))
            ImageDraw.Draw(preview).text((col*360+180,row*480+7),f'{name} · {emotion}',font=font,fill=INK,anchor='mt')
    preview.save(ROOT/'Tools/characters_preview.png')
    metadata={'canvas':list(SIZE),'characters':[{'id':k,'name':n,'neck':[0.5,0.36],'headCenter':[0.5,0.55]} for k,n,_,_ in DATA]}
    (OUT/'characters.json').write_text(json.dumps(metadata,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    print(f'Generated {len(DATA)*len(LAYERS)} aligned RGBA sprites, metadata, and preview.')

if __name__=='__main__':
    main()
