"""Download reviewed Commons candidates; install only with --install after visual review."""
import hashlib
import io
import json
from pathlib import Path
import shutil
import sys
import time
import requests
from PIL import Image, ImageDraw, ImageOps
from search_commons import ROOT, UA, plain

PROJECT = ROOT.parent.parent
CHOSEN = {
    'p05': 'File:Sindang-dong Tteokbokki town in 2020.jpg',
    'p10': 'File:Gwangjang Market, Seoul 01.jpg',
    'p15': 'File:Ben Thanh, Ciudad Ho Chi Minh, Vietnam, 2013-08-14, DD 01.JPG',
    'p20': 'File:BIFF Square in Busan.jpg',
    'p25': 'File:Ninh Kieu Quay.jpg',
    'p30': 'File:North Gate of the Dongnae eupseong site.JPG',
    'p35': 'File:Old Quarter street scene, Hanoi (7) (24624495318).jpg',
    'p40': 'File:Jeonju Hanok Maeul 02.jpg',
    'p45': 'File:Gwanghwamun December 2023.jpg',
}

def main():
    raw = ROOT / 'raw'
    raw.mkdir(exist_ok=True)
    manifest = []
    sheet = Image.new('RGB', (5 * 256, 2 * 288), 'white')
    draw = ImageDraw.Draw(sheet)
    for i, (key, title) in enumerate(CHOSEN.items()):
        data = json.loads((ROOT / (key + '_search.json')).read_text(encoding='utf-8'))
        page = next(p for p in data['query']['pages'].values() if p['title'] == title)
        info = page['imageinfo'][0]
        meta = info['extmetadata']
        license = meta['LicenseShortName']['value']
        assert license == 'CC0' or license == 'Public domain' or license.startswith(('CC BY ', 'CC BY-SA ')), license
        path = raw / (key + '.jpg')
        source_record = raw / (key + '_source.txt')
        if not path.exists() or not source_record.exists() or source_record.read_text(encoding='utf-8') != title:
            for attempt in range(4):
                response = requests.get(info['thumburl'], headers=UA, timeout=90)
                if response.status_code != 429:
                    break
                time.sleep(5 * (attempt + 1))
            response.raise_for_status()
            path.write_bytes(response.content)
            source_record.write_text(title, encoding='utf-8')
        image = ImageOps.exif_transpose(Image.open(path)).convert('RGB')
        # The API may supply a bucketed thumbnail (1280px); retain requested 1024px width.
        if image.width != 1024:
            image = image.resize((1024, round(image.height * 1024 / image.width)), Image.Resampling.LANCZOS)
            image.save(path, quality=95)
        square = ImageOps.fit(image, (512, 512), Image.Resampling.LANCZOS, centering=(0.5, 0.5))
        square.save(ROOT / (key + '_preview.png'))
        sheet.paste(square.resize((256,256)), ((i%5)*256,(i//5)*288))
        draw.text(((i%5)*256+8,(i//5)*288+260),key,fill='black')
        entry = dict(id=key, title=title, author=plain(meta['Artist']['value']), license=license,
            license_url=meta.get('LicenseUrl',{}).get('value',''), commons_url=info['descriptionurl'],
            thumbnail_url=info['thumburl'], original_url=info['url'],
            modifications='Center-cropped to square and resized to 512 × 512; no other edits.',
            metadata=meta)
        manifest.append(entry)
        if '--install' in sys.argv:
            target = PROJECT / 'Assets/Resources/Album' / (key+'.png')
            backup = PROJECT / 'Tools/backup_v8/Album_illustrated' / target.name
            backup.parent.mkdir(parents=True, exist_ok=True)
            if not backup.exists():
                shutil.copy2(target, backup)
            square.save(target)
        print(key, title, license, flush=True)
    (ROOT / 'selected.json').write_text(json.dumps(manifest, ensure_ascii=False, indent=2), encoding='utf-8')
    sheet.save(ROOT / 'contact_sheet.jpg', quality=92)

if __name__ == '__main__':
    main()
