"""Rebuild BepJam's procedural UI FX sprites. Requires Pillow and numpy."""
from pathlib import Path
import math
import json
import subprocess
import tempfile
try:
    import numpy as np
    from PIL import Image, ImageDraw, ImageFilter, ImageFont
    FALLBACK = False
except ImportError:
    # Some Windows sandboxes expose only Unity's bundled Python, without wheels.
    # Record the same 4x geometry and use the Windows drawing framework in that
    # case. The embedded renderer still performs premultiplied Lanczos reduction.
    FALLBACK = True

    class ScalarNP:
        pi = math.pi
        exp = staticmethod(math.exp)
        sin = staticmethod(math.sin)
        cos = staticmethod(math.cos)
        hypot = staticmethod(math.hypot)
        arctan2 = staticmethod(math.atan2)
        @staticmethod
        def linspace(a,b,n): return [a+(b-a)*i/(n-1) for i in range(n)]
        @staticmethod
        def clip(v,a,b): return min(b,max(a,v))
        class maximum:
            reduce = staticmethod(max)
    np = ScalarNP()

    class Scene:
        def __init__(self,size): self.width,self.height=size;self.size=size;self.ops=[]
        def alpha_composite(self,other): self.ops.append({'op':'composite','scene':other.data()})
        def filter(self,f): self.ops.append({'op':'blur','radius':f});return self
        def data(self): return {'width':self.width,'height':self.height,'ops':self.ops}

    class Image:
        @staticmethod
        def new(mode,size,*args): return Scene(size)

    class Drawer:
        def __init__(self,im): self.im=im
        def ellipse(self,box,fill=None,outline=None,width=1): self.im.ops.append(dict(op='ellipse',box=box,fill=fill,outline=outline,width=width))
        def rounded_rectangle(self,box,radius,fill=None,outline=None,width=1): self.im.ops.append(dict(op='rect',box=box,radius=radius,fill=fill,outline=outline,width=width))
        def polygon(self,pts,fill): self.im.ops.append(dict(op='polygon',pts=pts,fill=fill))
        def line(self,pts,fill,width=1,**kw): self.im.ops.append(dict(op='line',pts=pts,fill=fill,width=width))
        def arc(self,box,start,end,fill,width=1): self.im.ops.append(dict(op='arc',box=box,start=start,end=end,fill=fill,width=width))
    class ImageDraw:
        Draw=Drawer
    class ImageFilter:
        GaussianBlur=staticmethod(lambda radius:radius)

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / 'Assets/Resources/FX'
S = 4
BROWN = '#4A2F26'
CREAM = '#FFF4E1'
GOLD = '#FFD66B'
CORAL = '#F28B7B'
GREEN = '#A6D365'
BLUE = '#7DC8E8'
items = []


class Art:
    def __init__(self, w=96, h=96):
        self.w, self.h = w, h
        self.im = Image.new('RGBA', (w*S, h*S))
        self.d = ImageDraw.Draw(self.im)

    def draw(self, method, *args, **kwargs):
        fill=kwargs.get('fill')
        if not FALLBACK and isinstance(fill,tuple) and len(fill)==4 and fill[3]<255:
            overlay=Image.new('RGBA',self.im.size)
            getattr(ImageDraw.Draw(overlay),method)(*args,**kwargs)
            self.im.alpha_composite(overlay)
            self.d=ImageDraw.Draw(self.im)
        else:
            getattr(self.d,method)(*args,**kwargs)

    def ellipse(self, box, fill, outline=None, width=5):
        self.draw('ellipse',tuple(round(v*S) for v in box),fill=fill,outline=outline,width=round(width*S))

    def rect(self, box, fill, radius=0, outline=None, width=5):
        self.draw('rounded_rectangle',tuple(round(v*S) for v in box),round(radius*S),fill=fill,outline=outline,width=round(width*S))

    def poly(self, pts, fill, outline=None, width=5):
        p = [(round(x*S), round(y*S)) for x, y in pts]
        self.d.polygon(p, fill)
        if outline:
            self.d.line(p + [p[0]], outline, round(width*S), joint='curve')
            for x,y in p:
                r=width*S/2
                self.d.ellipse((int(x-r),int(y-r),int(x+r),int(y+r)), fill=outline)

    def line(self, pts, fill, width=5):
        p = [(round(x*S), round(y*S)) for x, y in pts]
        self.draw('line',p,fill=fill,width=round(width*S),joint='curve')
        r=width*S/2
        for x,y in (p[0],p[-1]):
            self.draw('ellipse',(int(x-r),int(y-r),int(x+r),int(y+r)),fill=fill)

    def path(self, pts, fill, outline=None, width=5):
        # Dense sampled curves make silhouettes smooth without extra dependencies.
        self.poly(pts,fill)
        if outline:self.line(pts+[pts[0]],outline,width)


def bez(a,b,c,d,n=24):
    return [((1-t)**3*a[0]+3*(1-t)**2*t*b[0]+3*(1-t)*t*t*c[0]+t**3*d[0],
             (1-t)**3*a[1]+3*(1-t)**2*t*b[1]+3*(1-t)*t*t*c[1]+t**3*d[1]) for t in np.linspace(0,1,n)]


def star(cx,cy,r,inner,points=5,angle=-math.pi/2):
    return [(cx+math.cos(angle+i*math.pi/points)*(r if i%2==0 else inner),cy+math.sin(angle+i*math.pi/points)*(r if i%2==0 else inner)) for i in range(points*2)]


def gradient_shape(a,box,radius,top,bottom,outline=BROWN,width=7):
    if FALLBACK:
        a.im.ops.append(dict(op='gradient',box=[v*S for v in box],radius=radius*S,top=top,bottom=bottom))
        a.rect(box,None,radius,outline,width)
        return
    mask=Image.new('L',a.im.size);md=ImageDraw.Draw(mask)
    md.rounded_rectangle(tuple(int(v*S) for v in box),int(radius*S),fill=255)
    from PIL import ImageColor
    c1=np.array(ImageColor.getrgb(top));c2=np.array(ImageColor.getrgb(bottom))
    t=np.clip((np.arange(a.h*S)/S-box[1])/(box[3]-box[1]),0,1)[:,None,None]
    rgb=np.broadcast_to((c1[None,None,:]*(1-t)+c2[None,None,:]*t).astype(np.uint8),(a.h*S,a.w*S,3))
    arr=np.dstack((rgb,np.array(mask)))
    a.im.alpha_composite(Image.fromarray(arr));a.d=ImageDraw.Draw(a.im)
    a.rect(box,None,radius,outline,width)


def save(name,a):
    # Resize premultiplied data to prevent dark fringes. Extend edge RGB into
    # fully transparent texels for clean filtering by Unity's texture sampler.
    im=a.im if isinstance(a,Art) else a
    if FALLBACK:
        items.append((name,im))
        return
    im=im.convert('RGBa').resize((im.width//S,im.height//S),Image.Resampling.LANCZOS).convert('RGBA')
    arr=np.array(im);alpha=arr[:,:,3];known=alpha>0
    while not known.all():
        rgb=arr[:,:,:3].copy();new=known.copy()
        for dy,dx in ((1,0),(-1,0),(0,1),(0,-1)):
            k=np.roll(known,(dy,dx),(0,1));v=np.roll(rgb,(dy,dx),(0,1));take=~new & k
            if dy==1:take[0,:]=False
            if dy==-1:take[-1,:]=False
            if dx==1:take[:,0]=False
            if dx==-1:take[:,-1]=False
            arr[:,:,:3][take]=v[take];new|=take
        if np.array_equal(known,new):break
        known=new
    OUT.mkdir(parents=True,exist_ok=True);im=Image.fromarray(arr);im.save(OUT/(name+'.png'));items.append((name,im))


def soft(name,w,h,fn,color=(255,255,255)):
    if FALLBACK:
        im=Scene((w*S,h*S));im.ops.append(dict(op='soft',name=name,color=color));save(name,im);return
    yy,xx=np.mgrid[:h*S,:w*S];x=(xx+.5)/S;y=(yy+.5)/S
    alpha=np.clip(fn(x,y),0,1)
    arr=np.empty((h*S,w*S,4),dtype=np.uint8);arr[:,:,:3]=color;arr[:,:,3]=(alpha*255).astype(np.uint8)
    save(name,Image.fromarray(arr))


def heart(a,fill,outline=None):
    p=bez((48,83),(34,70),(7,50),(12,28))+bez((12,28),(17,7),(39,9),(48,28))+bez((48,28),(58,9),(81,7),(85,29))+bez((85,29),(91,49),(63,73),(48,83))
    a.path(p,fill,outline,5)


def arrow(a,pts,color=BLUE,width=13):
    a.line(pts,BROWN,width+7);a.line(pts,color,width)


def main():
    soft('glow',128,128,lambda x,y:np.exp(-((x-64)**2+(y-64)**2)/(2*22**2))*np.clip((64-np.hypot(x-64,y-64))/12,0,1))
    a=Art(64,64);a.ellipse((3,3,61,61),'white');save('circle',a)
    soft('ring',128,128,lambda x,y:np.exp(-((np.hypot(x-64,y-64)-49)/4.2)**4/2))
    a=Art();a.path(star(48,49,41,19),'white');a.im=a.im.filter(ImageFilter.GaussianBlur(.4*S));save('star',a)
    a=Art();a.path(star(48,48,42,10,4),'white');glow=Image.new('RGBA',a.im.size);gd=ImageDraw.Draw(glow);gd.ellipse((27*S,27*S,69*S,69*S),fill=(255,255,255,80));glow=glow.filter(ImageFilter.GaussianBlur(10*S));glow.alpha_composite(a.im);save('sparkle',glow)
    a=Art();heart(a,CORAL,BROWN);a.line([(24,28),(28,23),(34,23)],'white',5);save('heart',a)
    a=Art();heart(a,'white');save('heart_white',a)
    a=Art(32,20);a.rect((2,2,30,18),'white',5);save('confetti',a)
    a=Art();gradient_shape(a,(9,8,87,88),39,'#FFE795','#F2B33D',width=6);a.ellipse((18,17,78,77),None,'#D69535',3);a.path(star(48,47,22,10),'#FFF0A9',None);a.line([(25,25),(30,21),(36,19)],'white',5);a.ellipse((70,65,75,70),'#FFF4CF');save('coin',a)
    for name,fill in [('check','#7CC35A'),('cross','#E65A4F')]:
        a=Art();a.ellipse((8,8,88,88),fill,BROWN,6);a.ellipse((20,16,32,22),(255,255,255,160))
        if name=='check':a.line([(27,48),(42,63),(69,34)],'white',9)
        else:a.line([(32,32),(64,64)],'white',9);a.line([(64,32),(32,64)],'white',9)
        save(name,a)
    soft('smoke',128,128,lambda x,y:np.clip(sum(np.exp(-(((x-cx)/rx)**2+((y-cy)/ry)**2)*1.8) for cx,cy,rx,ry in [(34,70,35,36),(56,44,39,39),(85,54,31,34),(83,81,33,29)])*.85,0,.92))
    def rays(x,y):
        r=np.hypot(x-256,y-256)/253;theta=np.arctan2(y-256,x-256);beam=np.clip((np.cos(theta*16)-.15)/.4,0,1)
        return beam*np.clip(1-r,0,1)**1.25*np.clip(r*8,0,1)
    soft('rays',512,512,rays)
    for name,fill in [('star_full',GOLD),('star_empty','#C9A27A')]:
        a=Art(128,128);p=star(64,65,54,26);a.path(p,fill,BROWN,6)
        if name=='star_full':a.path(star(64,62,39,20),'#FFE899');a.line([(49,42),(59,25)],'white',5)
        else:a.path(star(64,68,38,18),'#AF825F');a.path(star(64,62,34,16),'#C9A27A')
        save(name,a)
    a=Art(160,160)
    # One continuous glove silhouette: long up-left index, folded fingers, thumb.
    p=bez((69,125),(56,116),(54,96),(44,77))+bez((44,77),(33,58),(17,34),(22,23))+bez((22,23),(28,11),(39,16),(46,29))+bez((46,29),(54,41),(67,62),(72,71))+bez((72,71),(74,51),(86,49),(93,65))+bez((93,65),(103,53),(113,62),(116,73))+bez((116,73),(132,67),(142,80),(136,103))+bez((136,103),(134,121),(119,132),(110,137))
    a.path(p,'#FFFDF5',BROWN,6);a.path(bez((68,101),(77,78),(92,85),(101,98)),'#F4E7D9');a.line(bez((69,101),(77,78),(92,85),(101,98)),BROWN,4);a.line([(94,68),(101,82)],BROWN,4);a.line([(116,76),(119,87)],BROWN,4);a.poly([(66,122),(112,130),(106,151),(61,142)],CORAL,BROWN,6);a.line([(71,132),(101,138)],'#FFB8A7',4);save('hand',a)
    a=Art();a.d.arc((24*S,9*S,72*S,65*S),180,360,fill=BROWN,width=12*S);a.d.arc((24*S,9*S,72*S,65*S),180,360,fill='#F8D6A4',width=5*S);a.rect((14,39,82,86),'#DDA772',14,BROWN,6);a.ellipse((41,52,55,66),BROWN);a.rect((45,61,51,74),BROWN,3);a.line([(24,49),(34,49)],'#FFE7BC',4);save('lock',a)
    a=Art();a.line([(27,77),(22,87)],BROWN,6);a.line([(69,77),(75,87)],BROWN,6);a.ellipse((8,8,39,32),CORAL,BROWN,5);a.ellipse((57,8,88,32),CORAL,BROWN,5);a.ellipse((12,17,84,86),CREAM,BROWN,6);a.ellipse((21,26,75,78),'white','#DDA772',3);a.line([(48,34),(48,52),(62,58)],BROWN,5);a.ellipse((44,48,52,56),BROWN);save('clock',a)
    a=Art();a.ellipse((20,7,76,65),GOLD,BROWN,6);a.poly([(32,53),(64,53),(60,76),(36,76)],GOLD);a.line([(38,49),(44,63),(44,74)],'#DDA772',4);a.line([(58,49),(52,63),(52,74)],'#DDA772',4);a.rect((33,70,63,84),'#B1C1BF',5,BROWN,5);a.line([(42,89),(54,89)],BROWN,5);a.line([(29,28),(34,20)],'white',5);save('bulb',a)
    a=Art();a.ellipse((7,12,89,87),'#F8D6A4',BROWN,5);a.ellipse((16,22,80,78),CREAM,'#DDA772',4);pts=bez((70,61),(87,25),(36,16),(26,44));arrow(a,pts,GREEN,10);a.poly([(15,32),(27,52),(42,35)],GREEN,BROWN,4);save('undo',a)
    a=Art();a.rect((15,9,81,88),'#E65A4F',9,BROWN,6);a.rect((22,70,78,83),CREAM,4,BROWN,3);a.line([(25,17),(25,65)],'#FFAE94',4);a.rect((34,24,67,58),'#FFF4E1',5,BROWN,3);a.ellipse((42,31,60,48),GOLD);a.line([(43,52),(59,52)],'#DDA772',3);save('book',a)
    a=Art();pts=[]
    for i in range(48):
        angle=i*2*math.pi/48;r=40 if i%6 in (1,2,3,4) else 30;pts.append((48+math.cos(angle)*r,48+math.sin(angle)*r))
    a.path(pts,'#DDA772',BROWN,5);a.ellipse((27,27,69,69),'#F8D6A4',BROWN,5);a.ellipse((39,39,57,57),CREAM,BROWN,4);save('gear',a)
    a=Art();a.rect((22,40,75,86),CREAM,6,BROWN,5);a.poly([(9,44),(48,10),(87,44),(76,51),(48,27),(21,51)],CORAL,BROWN,5);a.rect((41,57,59,85),'#DDA772',4,BROWN,4);a.rect((26,53,35,64),BLUE,2,BROWN,2);save('home',a)
    a=Art();pts=[(48+32*math.cos(t),48+32*math.sin(t)) for t in np.linspace(-.4,4.6,80)];arrow(a,pts,GREEN,11);a.poly([(36,4),(61,16),(36,29)],GREEN,BROWN,4);save('retry',a)
    a=Art();a.path([(27,12),(80,48),(27,84)],GREEN,BROWN,7);a.line([(35,28),(35,56)],'#D5EAA4',4);save('play',a)
    a=Art();a.line([(40,67),(40,23),(77,14),(77,58)],BROWN,13);a.line([(40,67),(40,23),(77,14),(77,58)],CORAL,6);a.line([(42,34),(75,26)],BROWN,8);a.ellipse((15,58,43,81),CORAL,BROWN,5);a.ellipse((52,50,80,73),CORAL,BROWN,5);save('music',a)
    a=Art();a.path([(12,35),(29,35),(51,17),(51,79),(29,61),(12,61)],BLUE,BROWN,5)
    for r in (19,32):
        pts=[(51+r*math.cos(t),48+r*math.sin(t)) for t in np.linspace(-.8,.8,35)];a.line(pts,BROWN,7);a.line(pts,'#7ED6C6',3)
    save('sound',a)
    a=Art(64,64);a.rect((0,0,63.75,63.75),'white',22);save('rounded',a)
    a=Art(256,256);a.rect((4,4,252,252),BROWN,56);a.rect((12,12,244,244),'#E8C99A',48);a.rect((12,12,244,234),CREAM,48);a.rect((17,17,239,229),'#FFFCF4',44);a.rect((22,22,234,229),CREAM,40);save('panel',a)
    a=Art(160,96);a.rect((3,3,157,93),BROWN,40);a.rect((9,9,151,87),'#E8C99A',34);a.rect((9,9,151,80),CREAM,34);save('pill',a)
    for name,top,bottom,lip in [('green','#A6D365','#79B84A','#5C9638'),('yellow','#FFE08A','#FFC145','#D79932'),('red','#FF9C8A','#E65A4F','#BE433C'),('blue','#9EDCF2','#5DB3DD','#438CB7')]:
        a=Art(256,128);a.rect((4,4,252,124),lip,55,BROWN,7);gradient_shape(a,(7,5,249,110),51,top,bottom,width=6);a.rect((26,16,230,40),(255,255,255,75),13);a.line([(39,22),(87,22)],(255,255,255,150),4);save('btn_'+name,a)
    a=Art(128,128);a.ellipse((5,5,123,123),'#C76A5E',BROWN,7);a.ellipse((8,6,120,110),CORAL,BROWN,6);a.d.arc((23*S,16*S,105*S,88*S),205,290,fill='#FFC6B7',width=7*S);a.ellipse((31,24,39,32),'white');save('btn_round',a)
    a=Art(128,128);a.rect((3,3,125,125),'white',40,BROWN,6);save('bubble',a)
    a=Art(48,40);a.poly([(3,-5),(24,34),(45,-5)],'white');a.line([(3,0),(24,34),(45,0)],BROWN,6);save('bubble_tail',a)
    a=Art(640,160);a.poly([(7,38),(110,38),(110,147),(7,147),(35,92)],'#CF544B',BROWN,7);a.poly([(530,38),(633,38),(605,92),(633,147),(530,147)],'#CF544B',BROWN,7);a.poly([(77,115),(109,145),(109,112)],'#A84339',BROWN,5);a.poly([(531,112),(531,145),(563,115)],'#A84339',BROWN,5);a.rect((77,12,563,120),'#E65A4F',17,BROWN,7);a.line([(98,26),(540,26)],'#FFAA91',5);save('ribbon',a)
    a=Art(64,64);a.ellipse((1,1,63,63),(74,47,38,140));save('progress_bg',a)
    soft('shine',128,256,lambda x,y:np.exp(-((x-(100-y*.29))/13)**2)*np.sin(np.pi*np.clip(y/256,0,1))**.6*.9)
    soft('dot',24,24,lambda x,y:np.exp(-((x-12)**2+(y-12)**2)/25)*np.clip((12-np.hypot(x-12,y-12))/3,0,1))
    a=Art(64,48);p=bez((7,39),(5,14),(35,4),(58,8))+bez((58,8),(55,32),(35,46),(7,39));a.path(p,GREEN,'#668D43',3);a.line([(12,36),(46,16)],'#D8EDB5',2);save('leaf',a)
    a=Art(48,40);p=bez((6,32),(2,13),(18,1),(26,7))+bez((26,7),(39,-1),(50,14),(39,27))+bez((39,27),(29,38),(15,38),(6,32));a.path(p,'#F5A3AB');a.line([(11,29),(26,15)],'#FFD9D9',3);save('petal',a)
    preview()
    print('\n'.join(str(OUT/(name+'.png')) for name,_ in items))
    print(str(ROOT/'Tools/fx_preview.png'))


def preview():
    if FALLBACK:
        render_windows()
        return
    cols=6;cw,ch=190,182;rows=math.ceil(len(items)/cols)
    sheet=Image.new('RGB',(cols*cw,rows*ch),(133,133,133));d=ImageDraw.Draw(sheet)
    try:font=ImageFont.truetype('C:/Windows/Fonts/arial.ttf',16)
    except OSError:font=ImageFont.load_default()
    for i,(name,im) in enumerate(items):
        x=(i%cols)*cw;y=(i//cols)*ch
        thumb=im.copy();thumb.thumbnail((166,140),Image.Resampling.LANCZOS)
        sheet.paste(thumb,(x+(cw-thumb.width)//2,y+9+(140-thumb.height)//2),thumb)
        box=d.textbbox((0,0),name,font=font);d.text((x+(cw-box[2])/2,y+155),name,font=font,fill='white')
    sheet.save(ROOT/'Tools/fx_preview.png')


def render_windows():
    """Dependency-free Windows fallback for the identical vector recipes above."""
    OUT.mkdir(parents=True,exist_ok=True)
    payload=dict(out=str(OUT),preview=str(ROOT/'Tools/fx_preview.png'),
                 items=[dict(name=n,scene=im.data()) for n,im in items])
    with tempfile.TemporaryDirectory(prefix='bepjam-fx-') as tmp:
        tmp=Path(tmp)
        (tmp/'scene.json').write_text(json.dumps(payload),encoding='utf-8')
        (tmp/'renderer.cs').write_text(WINDOWS_RENDERER,encoding='utf-8')
        (tmp/'run.ps1').write_text(
            "param([string]$TaskDir)\n"
            "Add-Type -AssemblyName System.Drawing\n"
            "Add-Type -AssemblyName System.Web.Extensions\n"
            "$source = Get-Content -LiteralPath (Join-Path $TaskDir 'renderer.cs') -Raw\n"
            "Add-Type -TypeDefinition $source -ReferencedAssemblies System.Drawing,System.Web.Extensions\n"
            "[FxRenderer]::Run((Join-Path $TaskDir 'scene.json'))\n",encoding='utf-8')
        subprocess.run(['powershell.exe','-NoProfile','-ExecutionPolicy','Bypass','-File',str(tmp/'run.ps1'),str(tmp)],check=True)


WINDOWS_RENDERER = r'''
using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Web.Script.Serialization;

public static class FxRenderer
{
    static Dictionary<string,object> Obj(object v) { return (Dictionary<string,object>)v; }
    static object[] Arr(object v) { return (object[])v; }
    static float F(object v) { return Convert.ToSingle(v); }
    static float N(Dictionary<string,object> v,string k) { return F(v[k]); }
    static Color C(object v)
    {
        if(v==null)return Color.Transparent;
        if(v is string)return ColorTranslator.FromHtml((string)v);
        var a=Arr(v);return Color.FromArgb(a.Length>3?(int)F(a[3]):255,(int)F(a[0]),(int)F(a[1]),(int)F(a[2]));
    }
    static RectangleF Box(object v) { var a=Arr(v);return new RectangleF(F(a[0]),F(a[1]),F(a[2])-F(a[0]),F(a[3])-F(a[1])); }
    static PointF[] Points(object v)
    {
        var a=Arr(v);var p=new PointF[a.Length];for(int i=0;i<a.Length;i++){var q=Arr(a[i]);p[i]=new PointF(F(q[0]),F(q[1]));}return p;
    }
    static GraphicsPath Rounded(RectangleF b,float r)
    {
        r=Math.Min(r,Math.Min(b.Width,b.Height)/2);var p=new GraphicsPath();
        if(r<=0){p.AddRectangle(b);return p;}float d=r*2;
        p.AddArc(b.X,b.Y,d,d,180,90);p.AddArc(b.Right-d,b.Y,d,d,270,90);
        p.AddArc(b.Right-d,b.Bottom-d,d,d,0,90);p.AddArc(b.X,b.Bottom-d,d,d,90,90);p.CloseFigure();return p;
    }
    static Bitmap Scene(Dictionary<string,object> s)
    {
        int w=(int)N(s,"width"),h=(int)N(s,"height");var b=new Bitmap(w,h,PixelFormat.Format32bppArgb);
        foreach(object raw in Arr(s["ops"]))
        {
            var o=Obj(raw);string op=(string)o["op"];
            if(op=="soft"){b.Dispose();b=Soft(w,h,(string)o["name"],C(o["color"]));continue;}
            if(op=="blur"){var blurred=Blur(b,N(o,"radius"));b.Dispose();b=blurred;continue;}
            using(var g=Graphics.FromImage(b))
            {
                g.SmoothingMode=SmoothingMode.None;g.PixelOffsetMode=PixelOffsetMode.Half;
                if(op=="composite"){using(var other=Scene(Obj(o["scene"])))g.DrawImageUnscaled(other,0,0);continue;}
                if(op=="gradient")
                {
                    var box=Box(o["box"]);using(var path=Rounded(box,N(o,"radius")))using(var brush=new LinearGradientBrush(box,C(o["top"]),C(o["bottom"]),90f))g.FillPath(brush,path);continue;
                }
                if(op=="polygon"){using(var brush=new SolidBrush(C(o["fill"])))g.FillPolygon(brush,Points(o["pts"]));continue;}
                if(op=="line"||op=="arc")
                {
                    using(var pen=new Pen(C(o["fill"]),N(o,"width"))){pen.LineJoin=LineJoin.Round;
                    if(op=="line")g.DrawLines(pen,Points(o["pts"]));else g.DrawArc(pen,Box(o["box"]),N(o,"start"),N(o,"end")-N(o,"start"));}continue;
                }
                // Pillow strokes are inset. Drawing an outer fill followed by an
                // inset fill matches that convention at the supersampled scale.
                var rbox=Box(o["box"]);float rad=op=="rect"?N(o,"radius"):0;
                Action<RectangleF,float,Color> fill=(box,r,color)=>{if(box.Width<=0||box.Height<=0)return;using(var brush=new SolidBrush(color)){if(op=="ellipse")g.FillEllipse(brush,box);else using(var path=Rounded(box,r))g.FillPath(brush,path);}};
                if(o["outline"]!=null)
                {
                    Color stroke=C(o["outline"]);float strokeWidth=N(o,"width");
                    if(o["fill"]!=null){fill(rbox,rad,stroke);rbox.Inflate(-strokeWidth,-strokeWidth);fill(rbox,Math.Max(0,rad-strokeWidth),C(o["fill"]));}
                    else {var inset=rbox;inset.Inflate(-strokeWidth/2,-strokeWidth/2);using(var pen=new Pen(stroke,strokeWidth)){if(op=="ellipse")g.DrawEllipse(pen,inset);else using(var path=Rounded(inset,Math.Max(0,rad-strokeWidth/2)))g.DrawPath(pen,path);}}
                }
                else if(o["fill"]!=null)fill(rbox,rad,C(o["fill"]));
            }
        }
        return b;
    }
    static double Clamp(double x,double a=0,double b=1){return Math.Min(b,Math.Max(a,x));}
    static Bitmap Soft(int w,int h,string name,Color color)
    {
        byte[] px=new byte[w*h*4];for(int y=0;y<h;y++)for(int x=0;x<w;x++)
        {
            double u=(x+.5)/4,v=(y+.5)/4,a=0,r;
            switch(name)
            {
                case "glow":r=Math.Sqrt((u-64)*(u-64)+(v-64)*(v-64));a=Math.Exp(-r*r/(2*22*22))*Clamp((64-r)/12);break;
                case "ring":r=Math.Sqrt((u-64)*(u-64)+(v-64)*(v-64));a=Math.Exp(-Math.Pow((r-49)/4.2,4)/2);break;
                case "smoke":double[,] p={{34,70,35,36},{56,44,39,39},{85,54,31,34},{83,81,33,29}};for(int i=0;i<4;i++)a+=Math.Exp(-1.8*(Math.Pow((u-p[i,0])/p[i,2],2)+Math.Pow((v-p[i,1])/p[i,3],2)));a=Clamp(a*.85,0,.92);break;
                case "rays":r=Math.Sqrt((u-256)*(u-256)+(v-256)*(v-256))/253;a=Clamp((Math.Cos(Math.Atan2(v-256,u-256)*16)-.15)/.4)*Math.Pow(Clamp(1-r),1.25)*Clamp(r*8);break;
                case "shine":a=Math.Exp(-Math.Pow((u-(100-v*.29))/13,2))*Math.Pow(Math.Sin(Math.PI*Clamp(v/256)),.6)*.9;break;
                case "dot":r=Math.Sqrt((u-12)*(u-12)+(v-12)*(v-12));a=Math.Exp(-r*r/25)*Clamp((12-r)/3);break;
            }
            int k=(y*w+x)*4;px[k]=color.B;px[k+1]=color.G;px[k+2]=color.R;px[k+3]=(byte)(Clamp(a)*255);
        }return FromBytes(w,h,px);
    }
    static byte[] Bytes(Bitmap b)
    {
        var d=b.LockBits(new Rectangle(0,0,b.Width,b.Height),ImageLockMode.ReadOnly,PixelFormat.Format32bppArgb);var px=new byte[b.Width*b.Height*4];Marshal.Copy(d.Scan0,px,0,px.Length);b.UnlockBits(d);return px;
    }
    static Bitmap FromBytes(int w,int h,byte[] px)
    {
        var b=new Bitmap(w,h,PixelFormat.Format32bppArgb);var d=b.LockBits(new Rectangle(0,0,w,h),ImageLockMode.WriteOnly,PixelFormat.Format32bppArgb);Marshal.Copy(px,0,d.Scan0,px.Length);b.UnlockBits(d);return b;
    }
    static Bitmap Blur(Bitmap b,float radius)
    {
        int w=b.Width,h=b.Height,r=Math.Max(1,(int)(radius*2));double sigma=Math.Max(.1,radius);var kernel=new float[2*r+1];double sum=0;
        for(int i=-r;i<=r;i++){kernel[i+r]=(float)Math.Exp(-i*i/(2*sigma*sigma));sum+=kernel[i+r];}for(int i=0;i<kernel.Length;i++)kernel[i]/=(float)sum;
        byte[] src=Bytes(b);var tmp=new float[src.Length];var dst=new byte[src.Length];
        for(int y=0;y<h;y++)for(int x=0;x<w;x++)for(int c=0;c<4;c++){float n=0;for(int k=-r;k<=r;k++){int xx=x+k;if(xx<0||xx>=w)continue;int q=(y*w+xx)*4;n+=src[q+c]*(c==3?1:src[q+3]/255f)*kernel[k+r];}tmp[(y*w+x)*4+c]=n;}
        for(int y=0;y<h;y++)for(int x=0;x<w;x++){float[] v=new float[4];for(int k=-r;k<=r;k++){int yy=y+k;if(yy<0||yy>=h)continue;for(int c=0;c<4;c++)v[c]+=tmp[(yy*w+x)*4+c]*kernel[k+r];}int q=(y*w+x)*4;for(int c=0;c<3;c++)dst[q+c]=(byte)Clamp(v[3]>0?v[c]*255/v[3]:255,0,255);dst[q+3]=(byte)Clamp(v[3],0,255);}
        return FromBytes(w,h,dst);
    }
    static double Lanczos(double x){x=Math.Abs(x);if(x<1e-8)return 1;if(x>=3)return 0;return 3*Math.Sin(Math.PI*x)*Math.Sin(Math.PI*x/3)/(Math.PI*Math.PI*x*x);}
    static Bitmap Reduce(Bitmap b)
    {
        int sw=b.Width,sh=b.Height,w=sw/4,h=sh/4;byte[] src=Bytes(b);var tmp=new float[w*sh*4];var dst=new byte[w*h*4];
        var weights=new float[24];double total=0;for(int k=0;k<24;k++){weights[k]=(float)Lanczos((k-11.5)/4);total+=weights[k];}for(int k=0;k<24;k++)weights[k]/=(float)total;
        for(int y=0;y<sh;y++)for(int x=0;x<w;x++)for(int k=0;k<24;k++)
        {
            int xx=Math.Max(0,Math.Min(sw-1,x*4-10+k)),q=(y*sw+xx)*4,t=(y*w+x)*4;float a=src[q+3]/255f;
            for(int c=0;c<3;c++)tmp[t+c]+=src[q+c]*a*weights[k];tmp[t+3]+=src[q+3]*weights[k];
        }
        for(int y=0;y<h;y++)for(int x=0;x<w;x++)
        {
            float[] v=new float[4];for(int k=0;k<24;k++){int yy=Math.Max(0,Math.Min(sh-1,y*4-10+k)),q=(yy*w+x)*4;for(int c=0;c<4;c++)v[c]+=tmp[q+c]*weights[k];}
            int t=(y*w+x)*4;for(int c=0;c<3;c++)dst[t+c]=(byte)Clamp(v[3]>.001?v[c]*255/v[3]:255,0,255);dst[t+3]=(byte)Clamp(v[3],0,255);
        }
        // Extend opaque edge colors into transparent texels, preserving alpha.
        var known=new bool[w*h];for(int i=0;i<known.Length;i++)known[i]=dst[i*4+3]>0;
        while(true)
        {
            bool changed=false;var next=(bool[])known.Clone();byte[] old=(byte[])dst.Clone();for(int y=0;y<h;y++)for(int x=0;x<w;x++){int q=(y*w+x)*4;if(known[q/4])continue;
            int[] ns={x>0?q-4:-1,x+1<w?q+4:-1,y>0?q-w*4:-1,y+1<h?q+w*4:-1};
            foreach(int n in ns)if(n>=0 && known[n/4]){dst[q]=old[n];dst[q+1]=old[n+1];dst[q+2]=old[n+2];next[q/4]=true;changed=true;break;}}
            if(!changed)break;known=next;
        }
        return FromBytes(w,h,dst);
    }
    public static void Run(string json)
    {
        var js=new JavaScriptSerializer();js.MaxJsonLength=int.MaxValue;js.RecursionLimit=200;var root=Obj(js.DeserializeObject(File.ReadAllText(json)));var entries=Arr(root["items"]);var thumbs=new List<Bitmap>();
        foreach(object raw in entries){var entry=Obj(raw);using(var high=Scene(Obj(entry["scene"]))) {var small=Reduce(high);small.Save(Path.Combine((string)root["out"],(string)entry["name"]+".png"),ImageFormat.Png);thumbs.Add(small);}}
        int cols=6,cw=190,ch=182,rows=(entries.Length+cols-1)/cols;using(var preview=new Bitmap(cols*cw,rows*ch))using(var g=Graphics.FromImage(preview))using(var font=new Font("Arial",12))
        {g.Clear(Color.FromArgb(133,133,133));g.InterpolationMode=InterpolationMode.HighQualityBicubic;for(int i=0;i<entries.Length;i++){var im=thumbs[i];float scale=Math.Min(1,Math.Min(166f/im.Width,140f/im.Height));float w=im.Width*scale,h=im.Height*scale;int x=i%cols*cw,y=i/cols*ch;g.DrawImage(im,x+(cw-w)/2,y+9+(140-h)/2,w,h);string name=(string)Obj(entries[i])["name"];float tw=g.MeasureString(name,font).Width;g.DrawString(name,font,Brushes.White,x+(cw-tw)/2,y+155);im.Dispose();}preview.Save((string)root["preview"],ImageFormat.Png);}
    }
}
'''


if __name__=='__main__':main()
