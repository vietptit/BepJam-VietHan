import json
import requests
from PIL import Image, ImageOps, ImageDraw
from search_commons import ROOT, UA
for key in ['p25', 'p35']:
    data = json.loads((ROOT / (key+'_search.json')).read_text(encoding='utf-8'))
    sheet = Image.new('RGB', (1000, 480), 'white')
    draw = ImageDraw.Draw(sheet)
    for i,p in enumerate(sorted(data['query']['pages'].values(), key=lambda p:p['index'])):
        info=p['imageinfo'][0]
        path=ROOT/(key+'_candidate_'+str(p['index'])+'.jpg')
        if not path.exists():
            r=requests.get(info['thumburl'],headers=UA,timeout=60)
            r.raise_for_status()
            path.write_bytes(r.content)
        image=Image.open(path)
        sheet.paste(ImageOps.fit(image,(200,200)),((i%5)*200,(i//5)*240))
        draw.text(((i%5)*200+6,(i//5)*240+202),str(p['index']), fill='black')
    sheet.save(ROOT/(key+'_candidates.jpg'))
