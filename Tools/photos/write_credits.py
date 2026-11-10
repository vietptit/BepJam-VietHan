"""Generate matching game credits and distribution credits from reviewed metadata."""
import json
import uuid
from prepare_photos import PROJECT, ROOT

photos = json.loads((ROOT/'selected.json').read_text(encoding='utf-8'))
def cs(value):
    return json.dumps(value,ensure_ascii=False)

lines = ['# Album photo credits', '',
    'Photos retrieved from Wikimedia Commons using the Commons API. User-Agent: `BepJamAlbum/1.0 (educational game project)`.', '',
    'Each installed photo was center-cropped to square and resized to 512 × 512 PNG. The 1024px-wide downloaded source is kept in `Tools/photos/raw/`. The original album PNGs are backed up in `Tools/backup_v8/Album_illustrated/`.', '',
    'The adapted photos retain the individual licenses linked below, including ShareAlike where applicable. These photo licenses apply to the photo assets; no endorsement by their authors is implied.', '']
rows=[]
for p in photos:
    if not p['license_url'] and p['license']=='CC0':
        p['license_url']='https://creativecommons.org/publicdomain/zero/1.0/'
    lines += [f"## {p['id']} — {p['title']}", '',
        f"- Game asset: `Assets/Resources/Album/{p['id']}.png`",
        f"- File title: {p['title']}", f"- Author: {p['author']}",
        f"- Attribution requested by source: {p['metadata'].get('Attribution', {}).get('value', p['author'])}",
        f"- License: [{p['license']}]({p['license_url']})",
        f"- Commons page: [{p['title']}]({p['commons_url']})",
        f"- Changes: {p['modifications']}", '']
    rows.append('            { '+cs(p['id'])+', new Credit('+', '.join(cs(p[k]) for k in ['author','license','license_url','commons_url'])+') },')
lines += ['## p50 — Tam Thanh mural village (pending)', '',
    'No verified Commons photograph of this mural village was found through English/Vietnamese/Korean name searches, Tam Kỳ/Tam Ky searches and nearby geotagged files. The existing illustration is retained, with no Commons attribution. A photograph of the beach, a different Tam Thanh or another mural village is not a substitute.', '']
(PROJECT/'Tools/album_credits.md').write_text('\n'.join(lines),encoding='utf-8')
source='''using System.Collections.Generic;

namespace BepJam
{
    /// <summary>Photo attribution; full titles and modification notices are in Tools/album_credits.md.</summary>
    public static class AlbumCredits
    {
        public sealed class Credit
        {
            public readonly string Author, License, LicenseUrl, CommonsUrl;
            public Credit(string author, string license, string licenseUrl, string commonsUrl)
            {
                Author = author; License = license; LicenseUrl = licenseUrl; CommonsUrl = commonsUrl;
            }
        }

        public static readonly IReadOnlyDictionary<string, Credit> Photos =
            new System.Collections.ObjectModel.ReadOnlyDictionary<string, Credit>(new Dictionary<string, Credit>
        {
'''+ '\n'.join(rows)+'''
        });

        public static string Line(string id)
        {
            if (string.IsNullOrEmpty(id) || !Photos.TryGetValue(id, out var credit) || credit == null)
                return string.Empty;
            return "Ảnh: " + credit.Author + " · " + credit.License + ", Wikimedia Commons";
        }
    }
}
'''
target=PROJECT/'Assets/_Game/Scripts/Data/AlbumCredits.cs'
target.write_text(source,encoding='utf-8')
meta=target.with_suffix('.cs.meta')
if not meta.exists():
    meta.write_text('fileFormatVersion: 2\nguid: '+uuid.uuid4().hex+'\n',encoding='utf-8')
