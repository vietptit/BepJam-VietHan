"""Deterministic, hand-inked food icons. Run with Python + Pillow + numpy.

Only writes Tools/redraw/Food/*.png and Tools/redraw/food_preview.png.
Geometry is authored in 256px coordinates, inked at 4x, then reduced in
premultiplied alpha. A nearest-edge RGB bleed protects transparent texels.
"""
from pathlib import Path
import math
import numpy as np
from PIL import Image, ImageDraw, ImageFont

S = 4
INK = '#4A2F26'
CREAM = '#FFF4E1'
TEAL = '#7ED6C6'
GOLD = '#FFD66B'
RED = '#E65A4F'
GREEN = '#A6D365'
WHITE = '#FFFCF3'
OUT = Path(__file__).resolve().parent / 'redraw'


class Art:
    def __init__(self):
        self.im = Image.new('RGBA', (256*S, 256*S))
        self.d = ImageDraw.Draw(self.im)

    def ellipse(self, box, fill, stroke=INK, w=6.5):
        self.d.ellipse(tuple(round(v*S) for v in box), fill=fill,
                       outline=stroke, width=round(w*S) if stroke else 1)

    def rr(self, box, radius, fill, stroke=INK, w=6.5):
        self.d.rounded_rectangle(tuple(round(v*S) for v in box), round(radius*S),
                                fill=fill, outline=stroke,
                                width=round(w*S) if stroke else 1)

    def line(self, pts, color=INK, w=6.5):
        p = [(round(x*S), round(y*S)) for x, y in pts]
        self.d.line(p, fill=color, width=round(w*S), joint='curve')
        r = w*S/2
        for x, y in (p[0], p[-1]):
            self.d.ellipse((round(x-r), round(y-r), round(x+r), round(y+r)), fill=color)

    def path(self, commands, fill, stroke=INK, w=6.5):
        pts = []
        for c in commands:
            if len(c) == 2:
                pts.append(c)
            elif len(c) == 4:
                x, y = pts[-1]
                for i in range(1, 25):
                    t = i/24; u = 1-t
                    pts.append((u*u*x+2*u*t*c[0]+t*t*c[2],
                                u*u*y+2*u*t*c[1]+t*t*c[3]))
            else:
                x, y = pts[-1]
                for i in range(1, 33):
                    t = i/32; u = 1-t
                    pts.append((u**3*x+3*u*u*t*c[0]+3*u*t*t*c[2]+t**3*c[4],
                                u**3*y+3*u*u*t*c[1]+3*u*t*t*c[3]+t**3*c[5]))
        self.d.polygon([(round(x*S), round(y*S)) for x,y in pts], fill=fill)
        if stroke:
            self.line(pts+[pts[0]], stroke, w)

    def shine(self, box):
        layer = Image.new('RGBA', self.im.size)
        ImageDraw.Draw(layer).ellipse(tuple(round(v*S) for v in box), fill=(255,255,255,180))
        self.im = Image.alpha_composite(self.im, layer)
        self.d = ImageDraw.Draw(self.im)

    def bear(self, x=128, y=183, color=CREAM):
        self.ellipse((x-12,y-12,x+12,y+10), color, None)
        for dx in (-10,10):
            self.ellipse((x+dx-5,y-17,x+dx+5,y-7),color,None)

    def flower(self, x, y, color=CREAM):
        for i in range(5):
            a = i*math.tau/5-math.pi/2
            cx,cy=x+6*math.cos(a),y+6*math.sin(a)
            self.ellipse((cx-5,cy-5,cx+5,cy+5),color,None)
        self.ellipse((x-3,y-3,x+3,y+3),'#DDA772',None)


def bowl(a, color=TEAL, shade='#4AA997', motif='bear'):
    a.ellipse((86,208,170,235),shade)
    a.path([(20,119),(22,205,66,226,128,228),(190,226,234,205,236,119)],color)
    a.path([(214,146),(204,197,173,215,126,219),(81,220,53,199,44,182),
            (61,217,91,226,128,225),(190,223,225,192,230,145)],shade,None)
    a.ellipse((19,77,237,160),CREAM)
    a.ellipse((31,89,225,149),'#DDA772',None)
    a.shine((35,157,47,184))
    if motif=='bear': a.bear()
    elif motif=='flower':
        for x,y in [(75,181),(128,193),(181,181)]: a.flower(x,y)
    elif motif=='dots':
        for x,y in [(61,176),(104,192),(151,192),(194,176)]:
            a.ellipse((x-5,y-5,x+5,y+5),CREAM,None)


def rice(a, mung=False):
    a.path([(38,126),(34,109,48,98,60,101),(57,82,73,76,86,82),
            (84,63,103,58,115,69),(123,49,146,53,151,71),
            (172,57,188,70,185,86),(207,79,220,99,212,110),
            (232,121,216,141,192,143),(122,159,62,149,38,126)],WHITE)
    # Large, deliberate rice marks, never texture/noise.
    for x,y,ang in [(65,114,0),(91,94,0),(121,83,0),(151,99,0),
                    (183,113,0),(112,125,0),(151,133,0),(80,134,0)]:
        a.ellipse((x-8,y-5,x+8,y+5),'#F8D6A4',None)
    a.shine((82,78,101,88))
    if mung:
        for x,y in [(95,94),(118,82),(142,85),(162,99),(139,111),(112,108),(91,118),(166,121)]:
            bean(a,x,y,GOLD,'#DDAF43')


def bean(a,x,y,color=GREEN,shade='#79A84D',size=12):
    a.ellipse((x-size,y-size*.7,x+size,y+size*.7),color,INK,3)
    a.path([(x-7,y+4),(x+6,y+8,x+10,y-1),(x+7,y+7,x-3,y+7)],shade,None)
    a.ellipse((x-6,y-5,x,y-2),CREAM,None)


def scallions(a):
    for x,y in [(78,103),(109,75),(153,91),(183,117)]:
        a.ellipse((x-9,y-7,x+9,y+7),'#6CB94F',INK,3)
        a.ellipse((x-3,y-2,x+3,y+2),GOLD,None)


def kimchi(a, fried=False):
    if fried: rice(a)
    else:
        a.ellipse((35,94,221,148),'#D74032',INK,4)
    for dx,dy in [(-28,8),(24,-3),(0,24)]:
        a.path([(67+dx,99+dy),(85+dx,72+dy,117+dx,67+dy),
                (133+dx,82+dy,169+dx,87+dy),(159+dx,108+dy,134+dx,128+dy),
                (100+dx,137+dy,75+dx,122+dy)],'#FF934E',INK,4)
        for j in range(3):
            x=88+dx+j*18
            a.line([(x,103+dy),(x+17,88+dy),(x+28,88+dy)],GOLD,4)
    scallions(a)


def bread(a, sandwich=None):
    if sandwich:
        a.path([(22,163),(72,122,176,100,228,118),(249,145,223,180,178,200),
                (104,232,32,232,19,204),(10,192,12,175,22,163)],'#F8B94F')
        a.path([(22,187),(89,202,190,169,229,139),(233,166,208,186,176,199),
                (98,230,30,223,22,202)],'#DF8A36',None)
        a.path([(25,166),(43,146,66,168,80,144),(106,120,117,151,139,124),
                (163,109,178,133,198,116),(221,103,233,125,221,143),
                (188,168,132,186,79,200),(49,209,27,193,25,166)],GREEN,INK,5)
        if sandwich=='cha':
            for x,y in [(63,175),(111,157),(164,137)]:
                a.ellipse((x-27,y-17,x+28,y+16),'#F5BDA2',INK,4)
                a.line([(x-17,y-3),(x+14,y+5)],CREAM,4)
            for x,y in [(76,158),(125,141),(178,123)]:
                a.line([(x-12,y-8),(x+15,y+7)],'#FF8D43',5)
        else:
            a.path([(42,164),(47,144,72,148,82,139),(98,123,117,139,135,125),
                    (159,113,190,119,203,138),(227,145,210,165,187,166),
                    (163,163,143,188,122,183),(102,195,79,178,62,184),
                    (41,188,32,176,42,164)],WHITE,INK,4)
            a.ellipse((110,137,165,172),'#FFBF2F',INK,3)
            a.shine((120,142,136,150))
    a.path([(20,143 if sandwich else 158),(40,95,138,27,202,35),
            (230,38,244,62,232,87),(206,114,108,145,42,174),
            (23,181,11,167,20,143 if sandwich else 158)],'#FFC65C')
    a.path([(26,157),(80,147,195,100,236,65),(242,91,189,119,142,138),
            (77,163,34,183,24,168)],'#E59A3E',None)
    for x,y in [(65,126),(117,91),(172,65)]:
        a.path([(x-14,y-8),(x-12,y-22,x+8,y-22),(x+25,y-10,x+29,y+3),
                (x+6,y-1,x-1,y-8)],'#FFE8A1',None)
    a.shine((38,132,57,141))


def mug(a, tea=False):
    a.path([(191,99),(244,84,250,134,230,156),(219,171,205,174,188,165),
            (189,148),(219,151,229,119,207,118),(192,121)],'#F8D6A4')
    a.path([(40,83),(45,174),(46,216,74,229,124,229),
            (173,229,200,211,201,176),(207,83)],'#FFF0D0')
    a.path([(180,110),(178,198,158,215,97,218),(154,237,197,219,198,183),
            (204,106)],'#E5BE88',None)
    a.ellipse((39,53,208,119),CREAM)
    a.ellipse((51,65,196,106),'#FFD04F' if tea else '#8ED8E7',INK,3)
    a.shine((64,76,98,85))
    a.bear(125,172)
    a.shine((53,128,63,172))


def citrus(a,x,y,r=29):
    a.ellipse((x-r,y-r,x+r,y+r),'#FFBD35',INK,5)
    a.ellipse((x-r+6,y-r+6,x+r-6,y+r-6),'#FFE788',None)
    for i in range(6):
        t=i*math.tau/6
        a.line([(x,y),(x+(r-10)*math.cos(t),y+(r-10)*math.sin(t))],CREAM,3)
    a.ellipse((x-3,y-3,x+3,y+3),CREAM,None)


def glass(a,milk=False):
    a.path([(47,69),(59,219),(64,238,190,238,196,219),(210,69)],'#E6F2EB')
    a.path([(58,95),(69,213),(77,224,180,224,185,210),(199,95)],'#703C28',None)
    a.path([(174,112),(181,196,157,215,105,217),(181,225,184,219,187,209),
            (198,106)],'#542E24',None)
    if milk:
        a.path([(66,174),(87,185,97,167,114,181),(136,191,151,174,167,181),
                (181,185),(184,214),(140,230,83,221,72,214)],'#FFDB91',None)
    a.ellipse((47,42,210,100),CREAM)
    a.ellipse((58,53,199,88),'#794128',None)
    for x,y in [(88,78),(148,84)]:
        a.path([(x-21,y),(x-8,y-22),(x+17,y-10),(x+9,y+15)],'#DDA772',INK,4)
        a.line([(x-13,y),(x-6,y-12),(x+7,y-6)],CREAM,3)
    a.line([(162,76),(195,18)],INK,13)
    a.line([(162,76),(195,18)],'#F28B7B',6)
    a.shine((62,108,71,159))


def fish(a,cx,cy,scale=1,split=False):
    # Local coordinate drawing keeps the pair's ink weight identical.
    def p(x,y): return (cx+x*scale,cy+y*scale)
    seq=[p(-76,0),(*p(-51,-45),*p(7,-44)),(*p(28,-41),*p(40,-23)),
         p(71,-43),(*p(88,-42),*p(79,-18)),p(65,0),p(80,23),
         (*p(89,43),*p(70,41)),p(39,23),(*p(11,49),*p(-44,39)),
         (*p(-77,26),*p(-76,0))]
    a.path(seq,'#FFC65C')
    a.line([p(48,-17),p(61,-25)],'#C97930',4)
    a.line([p(48,15),p(63,25)],'#C97930',4)
    if split:
        a.ellipse((*p(-72,-29),*p(-12,30)),CREAM,INK,5)
        a.path([p(-61,17),(*p(-70,-4),*p(-50,-9)),
                (*p(-51,-27),*p(-34,-20)),(*p(-13,-18),*p(-23,0)),
                (*p(-7,18),*p(-32,21)),p(-61,17)],'#9D4B48',INK,4)
        for x,y in [(-48,-3),(-32,9),(-43,12)]:
            a.ellipse((*p(x-4,y-3),*p(x+4,y+3)),'#C96F65',None)
    else:
        a.ellipse((*p(-53,-13),*p(-42,-2)),INK,None)
        a.line([p(-30,-26),p(-21,-13),p(-20,10),p(-30,26)],'#C97930',4)
        a.shine((*p(-52,-25),*p(-36,-19)))
    for x in ([4,23] if split else [0,19]):
        for y in [-17,2,21]:
            a.line([p(x,y-6),p(x+6,y),p(x,y+6)],'#C97930',3.5)


def draw(name):
    a=Art()
    if name in ('banh_mi','d_banh_mi_cha','d_banh_mi_trung'):
        bread(a, {'d_banh_mi_cha':'cha','d_banh_mi_trung':'egg'}.get(name))
    elif name=='cha_lua':
        a.path([(35,190),(85,155),(221,147),(238,204),(193,229),(49,224)],'#6EAA54')
        a.line([(64,211),(210,185)],'#427D43',4)
        a.path([(101,54),(188,32),(232,53),(239,134),(208,166),(124,186)],'#568E47')
        a.path([(187,37),(214,52),(224,131),(201,157),(182,163)],'#37683B',None)
        for x in [146,191]: a.line([(x,48),(x+15,151)],GOLD,8)
        a.ellipse((61,53,151,185),'#F5C1A6')
        for x,y in [(91,86),(121,105),(93,141)]: a.ellipse((x-4,y-4,x+4,y+4),'#D99681',None)
        for box in [(28,164,154,214),(36,143,160,192)]:
            a.ellipse(box,'#F5C1A6')
            x,y=box[0]+28,box[1]+16
            a.line([(x,y),(x+38,y+8)],CREAM,5)
    elif name in ('com','gao_nep','d_xoi_dau_xanh'):
        bowl(a, '#F3A59A' if name=='gao_nep' else TEAL,
             '#D47C71' if name=='gao_nep' else '#4AA997',
             'flower' if name!='com' else 'bear')
        rice(a,name=='d_xoi_dau_xanh')
    elif name in ('kimchi','d_com_kimchi'):
        bowl(a,TEAL if name.startswith('d_') else CREAM,
             '#4AA997' if name.startswith('d_') else '#DDA772')
        kimchi(a,name.startswith('d_'))
    elif name in ('ca_phe','d_ca_phe_sua'): glass(a,name.startswith('d_'))
    elif name in ('nuoc_nong','d_tra_yuja'):
        mug(a,name.startswith('d_'))
        if name.startswith('d_'):
            citrus(a,151,88,20); citrus(a,192,207,32)
            a.ellipse((16,185,81,237),GOLD)
            a.path([(38,185),(20,162,60,159,65,177),(50,186)],GREEN,INK,4)
        else:
            for x,y in [(81,16),(129,10),(173,18)]:
                a.line([(x,y+25),(x-5,y+16),(x+3,y+6)],'#DDA772',5)
    elif name=='dau_xanh':
        bowl(a,'#7DC8E8','#4D9EBE','dots')
        for y,xs in [(125,[55,84,113,142,171,200]),(104,[70,99,128,157,186]),(83,[98,128,158]),(65,[128])]:
            for x in xs: bean(a,x,y,'#D5D956','#9BB64C')
    elif name=='dau_do':
        bowl(a,'#F3A59A','#D47C71','flower')
        a.path([(40,127),(39,105,61,100,72,103),(64,80,89,70,104,79),
                (110,50,147,51,158,78),(183,66,202,88,193,103),
                (221,99,230,130,204,139),(146,157,72,150,40,127)],'#AD5650')
        for x,y in [(78,120),(116,94),(152,124),(181,103)]:
            bean(a,x,y,'#8D403F','#713535',10)
        a.shine((108,68,126,77))
    elif name=='banh_gao':
        bowl(a)
        for x,y in [(83,100),(142,87),(175,113),(105,129)]:
            a.path([(x-37,y-9),(x-42,y-24,x-22,y-28),(x+34,y-30),
                    (x+49,y-25,x+42,y-8),(x-19,y+14),(x-32,y+14,x-37,y-9)],WHITE,INK,4.5)
            a.line([(x-22,y+5),(x+29,y-13)],'#F8D6A4',6)
            a.shine((x-26,y-19,x-10,y-13))
    elif name=='trung':
        a.path([(33,169),(8,126,43,42,80,24),(116,10,154,94,148,139),
                (149,197,62,220,33,169)],'#FFC58E')
        a.path([(37,172),(93,195,131,145,140,117),(158,175,84,218,37,172)],'#E9A06E',None)
        a.shine((55,55,78,89))
        a.ellipse((86,108,237,232),CREAM)
        a.ellipse((87,99,236,218),WHITE)
        a.ellipse((123,125,202,191),'#FFBE2F',INK,4)
        a.shine((136,134,156,146))
    elif name=='sua_dac':
        a.rr((52,42,204,225),19,'#7DC8E8')
        a.path([(179,50),(199,50),(199,211),(182,219),(176,80)],'#4DA1C3',None)
        a.rr((56,88,200,191),8,CREAM,None)
        a.ellipse((51,20,205,68),'#D5D8CD')
        a.ellipse((64,31,192,54),CREAM,INK,3)
        a.ellipse((140,35,173,49),'#A6B3AD',INK,3)
        # Cow face, ears, horns and muzzle, legible without tiny type.
        for x in (97,159): a.ellipse((x-12,118,x+12,136),'#DDA772',INK,4)
        a.line([(110,121),(106,109)],INK,5); a.line([(146,121),(151,109)],INK,5)
        a.ellipse((99,118,158,175),WHITE,INK,4)
        a.ellipse((104,148,153,177),'#F5B6A6',INK,4)
        for x in (116,141):
            a.ellipse((x-2,137,x+2,142),INK,None)
            a.ellipse((x-2,158,x+2,162),INK,None)
        a.shine((64,65,73,83))
    elif name=='mut_yuja':
        a.rr((49,64,207,232),24,'#FFCA3C')
        a.path([(184,86),(183,199,175,215,96,221),(191,234,201,220,201,197),(201,85)],'#E6A329',None)
        a.rr((65,119,191,202),13,CREAM,INK,4)
        citrus(a,135,162,28)
        a.path([(103,145),(85,126,119,119,126,139),(112,149)],GREEN,INK,3)
        a.rr((41,35,215,85),13,CREAM)
        # Gingham: broad flat tiles, clipped to the inner lid rectangle.
        for x in range(51,205,24):
            a.rr((x,41,x+11,78),2,GOLD,None)
        a.line([(49,60),(207,60)],'#EAA948',9)
        a.line([(52,82),(204,82)],INK,5)
        a.shine((59,95,68,115))
    elif name=='bot_banh_ca':
        a.path([(53,26),(201,26),(192,64),(218,226),(35,226),(60,64)],'#FFDB80')
        a.path([(190,65),(210,218),(181,218),(173,67)],'#EAB45C',None)
        a.rr((61,83,191,194),16,CREAM,INK,4)
        fish(a,126,140,.65)
        a.line([(67,43),(186,43)],INK,5)
        a.line([(61,210),(189,210)],'#DDA772',4)
        a.shine((65,56,77,78))
    elif name=='sot_tteok':
        a.path([(111,18),(142,18),(153,62),(100,62)],RED)
        a.rr((83,56,171,90),10,'#FF934E')
        a.path([(86,88),(58,99,56,120,53,145),(43,221),(74,241,182,241,213,221),
                (202,139),(202,102,183,89)],RED)
        a.path([(185,104),(197,214),(186,226),(150,230),(177,213),(173,110)],'#C63E36',None)
        a.rr((66,123,191,210),22,CREAM,INK,4)
        a.path([(91,181),(129,188,139,168,153,153),(167,169,146,198,119,198),
                (99,198,87,189,91,181)],RED,INK,4)
        a.line([(151,156),(160,145),(170,149)],'#6B9D48',5)
        a.shine((69,107,83,115))
    elif name=='d_bungeoppang':
        fish(a,111,91,1.1)
        fish(a,151,185,1.05,True)
    elif name=='d_tteokbokki':
        bowl(a,CREAM,'#DDA772','flower')
        a.ellipse((34,91,223,150),'#D93F30',INK,4)
        for x,y in [(86,104),(155,105),(113,132),(180,132)]:
            a.path([(x-29,y-9),(x-24,y-20,x-12,y-17),(x+29,y-5),
                    (x+35,y+5,x+25,y+14),(x-28,y+8),(x-34,y+3,x-29,y-9)],'#FF8E39',INK,4)
            a.line([(x-21,y-8),(x+16,y+2)],'#FFCC68',4)
        scallions(a)
        for x,y in [(77,123),(107,111),(142,137),(170,104),(191,135)]:
            a.line([(x,y),(x+3,y-3)],CREAM,3)
    else:
        raise ValueError(name)
    return a.im


ITEMS = [
    ('banh_mi','Baguette'), ('cha_lua','Cha lua'), ('com','Rice'),
    ('kimchi','Kimchi'), ('ca_phe','Iced coffee'), ('sua_dac','Condensed milk'),
    ('mut_yuja','Yuja jam'), ('nuoc_nong','Hot water'), ('gao_nep','Sticky rice'),
    ('dau_xanh','Mung beans'), ('bot_banh_ca','Fish waffle mix'),
    ('dau_do','Red bean paste'), ('trung','Eggs'), ('banh_gao','Rice cakes'),
    ('sot_tteok','Tteok sauce'), ('d_banh_mi_cha','Cha lua banh mi'),
    ('d_com_kimchi','Kimchi fried rice'), ('d_ca_phe_sua','Milk coffee'),
    ('d_tra_yuja','Yuja tea'), ('d_xoi_dau_xanh','Mung sticky rice'),
    ('d_bungeoppang','Bungeoppang'), ('d_banh_mi_trung','Egg banh mi'),
    ('d_tteokbokki','Tteokbokki'),
]


def finish(im):
    # Fit every subject into the same 240px safe box, including steam/straw.
    box=im.getbbox()
    if box is None: raise ValueError('Empty icon')
    crop=im.crop(box)
    factor=min(240*S/crop.width,240*S/crop.height)
    size=(round(crop.width*factor),round(crop.height*factor))
    crop=crop.convert('RGBa').resize(size,Image.Resampling.LANCZOS)
    canvas=Image.new('RGBa',(256*S,256*S))
    canvas.paste(crop,((256*S-size[0])//2,(256*S-size[1])//2))
    im=canvas.resize((256,256),Image.Resampling.LANCZOS).convert('RGBA')
    # Extend nearest visible RGB through fully transparent pixels. This avoids
    # black/white fringes in straight-alpha texture tools without changing alpha.
    data=np.array(im)
    known=data[:,:,3]>0
    for _ in range(256):
        if known.all(): break
        prev=known.copy()
        for dy,dx in ((-1,0),(1,0),(0,-1),(0,1)):
            src=np.roll(prev,(dy,dx),(0,1))
            if dy==-1: src[-1,:]=False
            if dy==1: src[0,:]=False
            if dx==-1: src[:,-1]=False
            if dx==1: src[:,0]=False
            take=(~known)&src
            shifted=np.roll(data[:,:,:3],(dy,dx),(0,1))
            data[take,:3]=shifted[take]
            known[take]=True
    result=Image.fromarray(data)
    assert result.mode=='RGBA' and result.size==(256,256)
    return result


def main():
    folder=OUT/'Food'
    folder.mkdir(parents=True,exist_ok=True)
    cols=5; cell_w=280; cell_h=302
    preview=Image.new('RGB',(cols*cell_w,math.ceil(len(ITEMS)/cols)*cell_h),CREAM)
    pd=ImageDraw.Draw(preview)
    font=ImageFont.load_default(size=16)
    small=ImageFont.load_default(size=12)
    for i,(name,label) in enumerate(ITEMS):
        icon=finish(draw(name))
        icon.save(folder/(name+'.png'))
        x=(i%cols)*cell_w; y=(i//cols)*cell_h
        preview.paste(icon,(x+12,y+4),icon)
        pd.text((x+cell_w/2,y+265),label,font=font,fill=INK,anchor='mt')
        pd.text((x+cell_w/2,y+286),name,font=small,fill=INK,anchor='mt')
    preview.save(OUT/'food_preview.png')
    print(f'Wrote {len(ITEMS)} icons and {OUT / "food_preview.png"}')


if __name__=='__main__':
    main()
