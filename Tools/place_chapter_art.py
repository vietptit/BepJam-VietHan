import os,sys
from PIL import Image
RAW='Tools/gemini/raw'; BG='Assets/Resources/BG'; AL='Assets/Resources/Album'
for f in sorted(os.listdir(RAW)):
    if not f.startswith('c_'): continue
    n=f[2:-4]; im=Image.open(os.path.join(RAW,f)).convert('RGB'); w,h=im.size
    if n.startswith('ch'):
        m=int(w*0.04); W=w-2*m; H=int(W/2.57)
        top=max(0,int(h*0.86)-H); out=im.crop((m,top,m+W,top+H)).resize((771,300),Image.LANCZOS)
        out.save(os.path.join(BG,n+'.png'))
    else:
        s=min(w,h); c=int(s*0.06); x=(w-s)//2; y=(h-s)//2
        im.crop((x+c,y+c,x+s-c,y+s-c)).resize((512,512),Image.LANCZOS).save(os.path.join(AL,n+'.png'))
    print('placed',n)
