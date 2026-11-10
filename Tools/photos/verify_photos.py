import hashlib
import json
from pathlib import Path
import shutil
from PIL import Image
from prepare_photos import PROJECT, ROOT

def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()

selected=json.loads((ROOT/'selected.json').read_text(encoding='utf-8'))
report=[]
for p in selected:
    key=p['id']
    target=PROJECT/'Assets/Resources/Album'/(key+'.png')
    raw=ROOT/'raw'/(key+'.jpg')
    backup=PROJECT/'Tools/backup_v8/Album_illustrated'/(key+'.png')
    meta=target.with_suffix('.png.meta')
    assert Image.open(raw).width==1024
    assert Image.open(raw).format=='JPEG'
    assert Image.open(target).size==(512,512)
    assert Image.open(target).format=='PNG'
    assert backup.exists() and digest(backup)!=digest(target)
    assert meta.exists()
    report.append(dict(id=key,size=[512,512],raw_width=1024,sha256=digest(target),backup_sha256=digest(backup),meta_sha256=digest(meta)))
pending=PROJECT/'Assets/Resources/Album/p50.png'
backup=PROJECT/'Tools/backup_v8/Album_illustrated/p50.png'
if not backup.exists():
    shutil.copy2(pending,backup)
assert digest(pending)==digest(backup)
(ROOT/'verification.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
print('Verified 9 JPEG sources, 9 square PNGs, backups and existing Unity metadata. p50 retained and backed up.')
