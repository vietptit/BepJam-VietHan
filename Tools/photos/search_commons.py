"""Search Commons using the project's descriptive, non-personal User-Agent."""
import concurrent.futures
import html
import json
from pathlib import Path
import re
import requests

ROOT = Path(__file__).resolve().parent
UA = {'User-Agent': 'BepJamAlbum/1.0 (educational game project)'}
QUERIES = {
    'p05': 'Sindang tteokbokki street',
    'p10': 'Gwangjang Market',
    'p15': 'Ben Thanh Market',
    'p20': 'BIFF Square',
    'p25': 'Ninh Kieu wharf',
    'p30': 'Dongnae eupseong',
    'p35': 'Hanoi Old Quarter street',
    'p40': 'Jeonju Hanok Village',
    'p45': 'Gwanghwamun Gate',
    'p50': 'Tam Thanh mural village',
}

def plain(value):
    return html.unescape(re.sub('<[^>]+>', '', value)).strip()

def search(item):
    key, query = item
    r = requests.get('https://commons.wikimedia.org/w/api.php', headers=UA, timeout=60,
        params=dict(action='query', generator='search', gsrnamespace=6, gsrsearch=query,
                    gsrlimit=10, prop='imageinfo', iiprop='url|extmetadata|size', iiurlwidth=1024, format='json'))
    r.raise_for_status()
    data = r.json()
    (ROOT / (key + '_search.json')).write_text(json.dumps(data, ensure_ascii=False, indent=2), encoding='utf-8')
    lines = [key]
    for p in sorted(data.get('query', {}).get('pages', {}).values(), key=lambda p: p.get('index', 0)):
        info = p['imageinfo'][0]
        meta = info['extmetadata']
        lines.append(' | '.join([p['title'], str(info['width'])+'x'+str(info['height']),
            meta.get('LicenseShortName',{}).get('value',''), plain(meta.get('Artist',{}).get('value',''))[:180],
            plain(meta.get('ImageDescription',{}).get('value',''))[:250]]))
    return '\n'.join(lines)

if __name__ == '__main__':
    with concurrent.futures.ThreadPoolExecutor(max_workers=3) as pool:
        for result in pool.map(search, QUERIES.items()):
            print(result)
