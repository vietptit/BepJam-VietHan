import json
import requests
from search_commons import ROOT, UA
queries = {
 'tam_geo': dict(action='query',list='geosearch',gscoord='15.569|108.536',gsradius=10000,gsnamespace=6,gslimit=100),
 'tam_titles': dict(action='query',list='search',srsearch='intitle:"Tam Thanh"',srnamespace=6,srlimit=100),
 'tam_category': dict(action='query',list='categorymembers',cmtitle='Category:Tam Kỳ',cmlimit=500),
 'tam_mural': dict(action='query',list='search',srsearch='"Tam Thanh" mural',srnamespace=6,srlimit=100),
}
for name, params in queries.items():
    params['format']='json'
    r=requests.get('https://commons.wikimedia.org/w/api.php',params=params,headers=UA,timeout=60)
    r.raise_for_status()
    data=r.json()
    (ROOT/(name+'.json')).write_text(json.dumps(data,ensure_ascii=False,indent=2),encoding='utf-8')
    print(name, json.dumps(data,ensure_ascii=False))
