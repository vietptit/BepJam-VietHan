# Chapter locations, window backgrounds and album postcards

This is the current art handoff for `Data/Chapters.cs` and `Data/Album.cs`, superseding the older dish-led `album_prompts.md`. Each chapter contains five levels. **The photo earned after level 5k records chapter k, the place just completed: p05 = Hồ Gươm, p50 = Gwanghwamun.** Menus, `NewDishDay`, recipes and difficulty remain unchanged. These are prompts for future assets; no PNG is generated or installed here.

## Itinerary

An asterisk marks the chapter's signature dish (`Entry.dish`, used by the album chip). Numbers in parentheses are introduction levels, taken from `NewDishDay`, rather than the recipe-order comments in GameData. Other previously unlocked dishes still appear according to the existing menu generator.

| Chapter / levels | Place / ID / country | Newly introduced dishes | Night levels | End-of-chapter photo / dish ID |
| --- | --- | --- | --- | --- |
| 1 / 1–5 | Hồ Gươm, Hà Nội / `ho_guom` / VN | Bánh mì chả lụa* (1), cơm trộn kimchi (2), cà phê sữa (3), trà yuja (4), xôi đậu xanh (5) | None | `Album/p05` / `banh_mi_cha` |
| 2 / 6–10 | Phố tteokbokki Sindang · 신당동 떡볶이 골목, Seoul / `sindang` / KR | Bungeoppang đậu đỏ (6), bánh mì trứng (7), tteokbokki* (8), gỏi cuốn tôm (10) | 9 | `Album/p10` / `tteokbokki` |
| 3 / 11–15 | Chợ Gwangjang · 광장시장, Seoul / `gwangjang` / KR | Gimbap mini* (12), trà tắc (14) | 11, 13 | `Album/p15` / `gimbap` |
| 4 / 16–20 | Chợ Bến Thành, Sài Gòn / `ben_thanh` / VN | Misugaru mật ong (16), cơm tấm sườn* (18) | None | `Album/p20` / `com_tam` |
| 5 / 21–25 | Quảng trường BIFF · 비프광장, Busan / `biff` / KR | Hotteok* (22), bánh mì thịt nướng (24) | None | `Album/p25` / `hotteok` |
| 6 / 26–30 | Bến Ninh Kiều, Cần Thơ / `ninh_kieu` / VN | Bulgogi (26), bánh xèo* (28) | None | `Album/p30` / `banh_xeo` |
| 7 / 31–35 | Thành cổ Dongnae · 동래읍성, Busan / `dongnae` / KR | Pajeon hải sản* (32), trà đào cam sả (34) | None | `Album/p35` / `pajeon` |
| 8 / 36–40 | Phố cổ Hà Nội / `hanoi_old_quarter` / VN | Sikhye (36), phở bò* (38) | 37, 39, 40 | `Album/p40` / `pho_bo` |
| 9 / 41–45 | Làng Hanok Jeonju · 전주한옥마을 / `jeonju_hanok` / KR | Bibimbap* (42), bún chả (44) | 41 | `Album/p45` / `bibimbap` |
| 10 / 46–50 | Quảng trường Gwanghwamun · 광화문광장, Seoul / `gwanghwamun` / KR | Kimchi jjigae* (46) | None | `Album/p50` / `kimchi_jjigae` |

Countries alternate where the food schedule supports it. Chapters 2–3 stay in Seoul to move from tteokbokki to Gwangjang's small gimbap; chapters 9–10 stay in Korea for Jeonju bibimbap and Seoul kimchi stew. Vietnamese dishes in these mixed menus remain Vietnamese; the chosen setting does not claim to be every dish's birthplace. Misugaru, yuja, sikhye and bungeoppang are broader Korean associations, not exclusive local inventions. Dongnae's regional pancake inspires the setting; the game recipe is simplified. Gwanghwamun has documented kimchi-jjigae restaurants; the palace is not presented as the dish's origin.

Night selection uses the **actual level title**, case-insensitively containing `đêm` or `tối`, including boss titles. A newly introduced dish can replace a theme title, so not every level in a nominal night-themed week is a night level. The table reflects current titles; no extra night flags or menu changes were added.

## Asset contract and framing

Measured from the current files with Pillow:

- `Assets/Resources/UI/bg_shop.png`: **1080 × 2336 px, RGB**, an opaque full kitchen containing the Hồ Gươm exterior.
- `UI/bg_front.png`: **1080 × 2336 px, RGBA**; its opaque counter begins at source y=600.
- `UI/bg_lake.png`: 941 × 1672 px, RGBA; this legacy image is **not** the current GameView backdrop.
- Current `Album/p05.png`: 512 × 511 px; the album displays a square 308 × 308 print. Replacement postcards should be **512 × 512**. The separate `polaroid.png` is 440 × 512 and stays UI-owned.

GameView retains `bg_shop` and overlays **only the exterior opening**, source rect **x=158, y=300, width=771, height=300** in that full kitchen image (top-left pixel coordinates; ratio 2.57:1). The placement divides these pixel values by the existing 1.2661 art scale. Customers are drawn above this plate, the existing counter above the customers. The curtain, jambs, walls, appliances and interface remain supplied by the game. Make all background plates **771 × 300 px**, opaque, with identical eye-level framing. If Gemini cannot export that exact resolution, request a wide image with enough margins, then crop/resize to this size before import. Do not stretch a square scene.

Import as **Sprite (2D and UI), Single, no sprite border** into `Assets/Resources/BG/ch01.png` … `ch10.png`; variants are `ch01_night.png` … `ch10_night.png`. Resource keys omit `.png`. No art is generated or installed by this change. Importing these future assets is an artist handoff outside the writable script/Tools folders.

Runtime selection: night sprite → chapter day sprite → no overlay, leaving the original Hồ Gươm sprite visible. Day levels use day sprite → original Hồ Gươm. Missing optional sprites are cached without warnings. A null level returns no overlay. Missing a night plate deliberately uses that chapter's day art before the legacy scene. Chapter 1 may reuse the existing view indefinitely; its night prompt is supplied for visual completeness even though levels 1–5 currently have no night titles.

For **every background prompt below**, supply the existing `UI/bg_shop.png` as a visual reference and use this English instruction as the first paragraph:

> Create an opaque 771 by 300 pixel horizontal exterior background plate, aspect ratio 2.57:1, seen at seated eye level through the existing restaurant window in the attached 1080 by 2336 pixel kitchen reference. Paint only the outdoor view for source rectangle x158 y300 w771 h300; do not redraw the interior, window frame, curtains, sill, counter, customers or UI. Match the cozy flat Venba-like 2D game style: broad simple shapes, slightly irregular hand-painted edges, subtle paper-grain gouache texture, muted ochre, terracotta, cream, jade and dusty blue, soft ambient light, very little perspective distortion. Keep the identifying landmark in the upper-middle half and the lower third quiet so customer heads and order bubbles can overlap. No lettering, signs with readable text, logos, watermark or decorative border; no photorealism, 3D rendering, dramatic lens or foreground food.

The prompts below complete that paragraph. For a night variant, attach its finished day plate as an additional reference and apply the night prompt, preserving the same camera, architecture and silhouettes.

For **every postcard**, use the following English first paragraph and then its location prompt. Generate the illustration only; the game supplies title, chip, flags and polaroid frame:

> Paint a square 512 by 512 pixel landmark postcard in the same cozy flat Venba-like hand-painted gouache style as the game, with simple readable silhouettes, subtle paper grain and warm muted colors. Make the place the main subject, recognizable at thumbnail size, with a small unobtrusive food vignette connecting it to the chapter menu. Leave the lower-left corner relatively quiet for a UI dish chip. No text, labels, flags, logos, watermark, inset panels or polaroid border. Use an interpretive contemporary tourist view, not a claim that the food was invented at this landmark.

Replace existing postcard PNGs with the corresponding new landmark art when ready; metadata changes cannot repaint older images. Existing album save keys and chapter-end reward levels remain valid.

## 1 · Hồ Gươm · ch01 / p05

**Day — `BG/ch01`:** A peaceful view over Hoan Kiem Lake in Hanoi, matching the reference's current lake scene as closely as possible. The small pale stone Turtle Tower sits alone on its island at upper center, framed by soft leafy branches at both sides, jade water and a low lakeside promenade. Warm hazy morning light; keep the tower separate from Ngoc Son Temple and do not put the red The Huc Bridge on Turtle Tower's island. Calm uncluttered lower third.

**Night — `BG/ch01_night`:** Keep the identical Hoan Kiem composition and geometry. Change the sky to dusty indigo and the lake to deep teal, with a softly lit Turtle Tower, restrained amber promenade lamps and broken reflections. Retain readable pale tower edges and broad foliage shapes; no fireworks or new structures.

**Postcard - `Album/p05`:** Hoan Kiem Lake and Turtle Tower on its own small grassy island in warm morning light, with leafy branches and a soft reflection. Make the landmark dominant and recognizable at thumbnail size, leaving lower-left water quiet for the banh mi chip. If desired, include only a tiny banh mi breakfast cart at the right edge. Keep The Huc Bridge on its separate Ngoc Son island: it must not lead to Turtle Tower.

## 2 · Sindang tteokbokki street · ch02 / p10

**Day — `BG/ch02`:** Look from a small restaurant across a compact contemporary lane in Sindang-dong, Seoul, known for tteokbokki restaurants. Use low-rise shopfronts, fabric awnings, a simplified street entrance feature and a glimpse of a tabletop cooking pot behind distant glass. Small red-orange accents suggest spicy rice cakes without large food illustrations. Soft cool-season daylight; distinguish this neighborhood food street from a palace or covered market. Do not reproduce real business branding.

**Night — `BG/ch02_night`:** Preserve the lane and storefront positions. Set an indigo early-evening sky above warm amber restaurant windows, restrained coral awning light and a small wisp of distant cooking steam. Keep the lower third simple and the scene welcoming, with no neon lettering or new crowd blocking the view.

**Postcard — `Album/p10`:** A welcoming entrance view into Sindang's tteokbokki restaurant street, recognizable as a modern neighborhood food lane. At the right foreground, show a small pot of cylindrical rice cakes in glossy red sauce and two fish-shaped bungeoppang with a modest red-bean filling glimpse. The fish shape is pastry, not seafood. Connect these snacks to the chapter just completed, with no claim that bungeoppang originated here.

## 3 · Gwangjang Market · ch03 / p15

**Day — `BG/ch03`:** From a restaurant at the edge of Gwangjang Market in Seoul, look into a covered food aisle. Repeating steel roof trusses, translucent roof panels, warm stall awnings and orderly counters recede gently. Small distant seaweed rice-roll trays hint at mini gimbap; a folded fabric display at one side recalls the market's textile trade. Soft daylight filters through the roof. Keep the center landmark geometry high and the foreground clear.

**Night — `BG/ch03_night`:** Preserve the covered aisle and roof trusses exactly. Replace roof daylight with dusky blue, warm hanging stall lamps and low amber shop lights. Keep counters and roof structure legible, with tiny restrained steam shapes and no added readable stall signs.

**Postcard — `Album/p15`:** A characterful Gwangjang Market aisle with roof trusses and compact food stalls as the main subject. At the right foreground place a small plate of short mini gimbap rolls with rice and yellow pickled radish visible in the ends. A tiny glass of Vietnamese kumquat tea is a guest-cuisine accent. Do not replace the game's mini gimbap with giant sushi rolls or portray tea as a local Korean specialty.

## 4 · Bến Thành Market · ch04 / p20

**Day — `BG/ch04`:** Look across a modest open pedestrian foreground toward Ben Thanh Market's familiar pale-yellow entrance and square clock tower in central Saigon. Use the clock face as a simple clear geometric shape, with red tiled roofs, soft palms at the sides and only a few distant traffic silhouettes. Sunny tropical late-morning light, no lake, no invented cathedral spires. Keep the market entrance centered above the quiet lower third.

**Night — `BG/ch04_night`:** Preserve the clock tower, roofline and camera position. Introduce a muted deep-blue sky, gentle amber architectural lighting and warm market windows. A few distant stall lights suggest evening street life. No excessive neon, fireworks or new foreground vehicles.

**Postcard — `Album/p20`:** Ben Thanh Market's yellow clock tower and entrance in warm Saigon daylight. At the right foreground show a small plate of broken rice, a grilled pork chop and pickled vegetables, clearly matching com tam suon. A small milk-colored misugaru drink may appear as a visiting Korean menu item. Keep the market dominant and avoid implying that this landmark invented either recipe.

## 5 · BIFF Square · ch05 / p25

**Day — `BG/ch05`:** Look across BIFF Square's compact pedestrian cinema street in Nampo-dong, Busan. Suggest its film heritage with a restrained cinema facade, simple unlettered marquee and a few small pavement handprint plaques near the sides; include a modest street-food cart in the mid-distance. Cool clear coastal daylight. Do not insert a beach, harbor panorama or Seoul tower into this enclosed neighborhood view.

**Night — `BG/ch05_night`:** Preserve the pedestrian lane, cinema and stall arrangement. Warm the unlettered marquee and food-cart lamps against dusky blue buildings and sky. Use gentle golden reflections on paving, no flashing advertisements and no large crowd in front of the scene.

**Postcard — `Album/p25`:** BIFF Square's cinema street and a modest pavement handprint plaque establish the Busan location. At the right edge a small street-food tray holds golden hotteok, one opened to reveal brown sugar and crushed seeds or nuts, inspired by Busan's ssiat hotteok. A discreet grilled-meat banh mi can represent the Vietnamese guest dish. No film logos, celebrity likenesses or claim that all hotteok began at BIFF Square.

## 6 · Ninh Kiều Wharf · ch06 / p30

**Day — `BG/ch06`:** View Ninh Kieu's riverside promenade in Can Tho through the restaurant window: broad calm river water, a low distant opposite bank, leafy tropical trees framing the sides, a small local passenger boat and a restrained glimpse of the pedestrian bridge toward one side. Humid warm daylight and jade-green water. Depict the wharf and promenade rather than moving Cai Rang's busy floating market into this exact location. No mountains or seaside surf.

**Night — `BG/ch06_night`:** Keep the identical riverbank, trees, bridge silhouette and boat scale. Use deep teal water, an indigo sky and restrained warm promenade and bridge lights, with soft short reflections. Avoid a dense neon skyline or fireworks; keep the lower third quiet.

**Postcard — `Album/p30`:** Ninh Kieu's riverfront promenade in Can Tho, with a small boat and a broad low river horizon. At the right foreground show a large thin golden Vietnamese banh xeo folded around shrimp and bean sprouts, with fresh greens nearby. A small side plate of soy-glazed bulgogi can signal the Korean guest menu. Emphasize Mekong Delta daily life rather than claiming the dish was invented at this wharf.

## 7 · Dongnae Fortress · ch07 / p35

**Day — `BG/ch07`:** A gentle view toward Dongnae Eupseong's North Gate in Busan, with a stone base and arched passage, a modest traditional tiled gate pavilion above, short stone walls and softly wooded hills. Use a simplified visitor viewpoint from outside the gate, not a fortress encircling all modern Busan. Pale spring daylight, muted stone gray and warm timber accents; preserve the gate silhouette in the upper middle.

**Night — `BG/ch07_night`:** Keep the exact gate structure, wall lengths and wooded horizon. Use an indigo sky, darker green hills and soft warm visitor-path lighting that gently reveals the stone arch and tiled pavilion. No invented palace lantern festival or guard procession.

**Postcard — `Album/p35`:** Dongnae Eupseong's North Gate and low stone walls frame a calm historic Busan view. At the right foreground a small green-onion pancake includes visible long scallions, shrimp and squid, connecting the chapter's simplified haemul pajeon to Dongnae's pancake tradition. Add a tiny peach-orange-lemongrass tea as a Vietnamese guest drink if space permits. Do not imply the game's recipe reproduces every ingredient of regional Dongnae pajeon.

## 8 · Hanoi Old Quarter · ch08 / p40

**Day — `BG/ch08`:** Look across a narrow Hanoi Old Quarter street lined with narrow-fronted tube houses, weathered ochre plaster, wooden shutters, modest tile roofs and one leafy tree. A small distant pho stall and parked bicycle suggest everyday street cooking; keep signage unlettered and the lower third calm. Warm soft morning light. This is a street view distinct from chapter 1's Hoan Kiem lake, with no Turtle Tower or invented giant monument.

**Night — `BG/ch08_night`:** Preserve the houses, bicycle and pho-stall positions. Replace daylight with deep dusty blue and warm lamps from small shopfronts. Add a little soft cooking steam and restrained amber paving reflections, retaining warm ochre facades and clear street depth. No loud nightlife crowd or neon lettering.

**Postcard — `Album/p40`:** A charming Old Quarter street in Hanoi with narrow tube-house facades, wooden shutters and a modest street pho stall. At the right edge place a small bowl of pho bo with flat rice noodles, beef and herbs in clear broth; a tiny sikhye cup is a Korean guest-menu accent. Keep architecture dominant and avoid claiming a definitive inventor or a single proven birthplace for pho.

## 9 · Jeonju Hanok Village · ch09 / p45

**Day — `BG/ch09`:** A low eye-level view along a quiet lane in Jeonju Hanok Village: layered dark curved roof tiles, pale plaster walls, warm wooden doors and one small courtyard tree. Distinguish the domestic hanok neighborhood from a royal palace: no throne hall or monumental gate. Soft sunny afternoon light, gentle rolling roof rhythm and restrained distant hills, with the lane remaining open for customers to overlap.

**Night — `BG/ch09_night`:** Preserve every roofline, doorway and courtyard tree. Use dusky indigo above warm paper-window glows, a few modest amber lanterns and muted stone paving. Maintain readable roof edges and a quiet lane; no festival crowds or new ornate palace architecture.

**Postcard — `Album/p45`:** Jeonju Hanok Village's tiled roofs and warm timber courtyard houses make the main landmark composition. At the right foreground show a small bibimbap bowl with rice, distinct colored vegetables, egg and a dab of red gochujang, matching the game's four-part recipe. A small Vietnamese bun cha serving may be a secondary guest dish. Do not add an unsupported royal-origin story or claim this simplified bowl contains all regional Jeonju ingredients.

## 10 · Gwanghwamun Square · ch10 / p50

**Day — `BG/ch10`:** From an imagined nearby restaurant viewpoint, look north across Gwanghwamun Square in Seoul toward Gyeongbokgung's Gwanghwamun Gate: pale stone base with three arched passages and a two-tier dark tiled pavilion, framed by a restrained distant mountain silhouette and low urban edges. Soft clear daylight. This is a scenic compressed illustration, not a surveyed real restaurant sightline. Avoid copying the smaller Dongnae gate from chapter 7; keep the central palace gate broad and distinct.

**Night — `BG/ch10_night`:** Preserve the broad palace gate, three arches, mountain silhouette and camera position. Use a deep-blue sky and gentle warm architectural illumination, with restrained square lighting and cool paving. No festival projection, fireworks or new crowds; keep the landmark legible at small scale.

**Postcard — `Album/p50`:** Gwanghwamun Square looking toward the broad main gate of Gyeongbokgung Palace, with a softly simplified mountain backdrop. At the right edge a small earthenware pot of kimchi jjigae shows red kimchi broth, pork pieces and pale tofu cubes. Present this as a contemporary comforting meal found in the surrounding Seoul neighborhood, not palace cuisine or a claim of invention at the gate. Warm inviting daylight and a clear landmark silhouette.

## Caption references and factual boundaries

Checked 2026-11-10. Captions are original Vietnamese paraphrases, each two introduction/menu sentences followed by `Bạn có biết?` and one fact sentence. Mixed-menu arrangements are game art direction, not historical claims. Place names avoid unnecessary claims about current administrative boundaries.

| Photo / chapter | Primary references |
| --- | --- |
| p05 / 1 | [Vietnam Tourism: Old Quarter and Hoàn Kiếm](https://vietnam.travel/things-to-do/explore-old-quarter-your-way/) supports lake setting, Turtle Tower and nearby landmarks. The Huc Bridge leads to Ngoc Son Temple on a separate island from Turtle Tower. |
| p10 / 2 | [Visit Seoul: Sindang-dong](https://english.visitseoul.net/area/Sindang-dong/ENP4loe2g) documents the tteokbokki street and tabletop jeukseok cooking. [KTO: bungeoppang](https://english.visitkorea.or.kr/svc/contents/contentsView.do?vcontsId=180769) supports fish-shaped molds and red-bean filling; it is not claimed as a Sindang invention. |
| p15 / 3 | [Seoul Metropolitan Government: Gwangjang Market](https://english.seoul.go.kr/gwangjang-market/) documents small gimbap, textiles and hanbok. We avoid exclusive origin claims and the older food nickname. |
| p20 / 4 | [Visit HCMC: Bến Thành Market](https://visithcmc.net/en/news/kham-pha-cho-ben-thanh-diem-den-khong-the-bo-qua-o-tp-ho-chi-minh) supports the entrance clock tower. [Vietnam Tourism: Ho Chi Minh City dishes](https://vietnam.travel/things-to-do/explore-delicious-dishes-ho-chi-minh-city/), [Vietnam Tourism: 21 dishes](https://vietnam.travel/things-to-do/21-must-try-vietnamese-dishes/) support cơm tấm, pork chop, pickles and broken rice; the market is the city landmark, not an asserted birthplace. |
| p25 / 5 | [Visit Busan: historic downtown](https://www.visitbusan.net/index.do?lang_cd=en&menuCd=DOM_000000202012001000&uc_seq=2809) documents BIFF Square's film connection and Nampo-dong seed hotteok. The game uses its existing brown-sugar-and-crushed-seed/nut recipe. |
| p30 / 6 | [Cần Thơ Tourism: Ninh Kiều Quay](https://canthotourism.vn/en/benninhkieu) supports the river confluence. [Vietnam Tourism: Cần Thơ](https://vietnam.travel/things-to-do/can-tho-glimpse-river-and-garden/) supports the riverfront setting and local crispy pancake; [Dining in the Delta](https://vietnam.travel/things-to-do/dining-mekong-delta/) supports southern bánh xèo and its sizzling-name explanation. Ninh Kiều and Cái Răng are not the same landmark. |
| p35 / 7 | [Visit Busan: Dongnae historic area](https://www.visitbusan.net/en/index.do?lang_cd=en&menuCd=DOM_000000302002001000&uc_seq=1152), [Visit Busan: Dongnae pajeon](https://www.visitbusan.net/index.do?menuCd=DOM_000000301002002001&uc_seq=1607) support the gate/walls setting, thick green-onion pancake and seafood. [KTO: Dongnaeeupseong](https://english.visitkorea.or.kr/svc/whereToGo/locIntrdn/rgnContentsView.do?vcontsId=59929) identifies the restored North Gate. No exact restoration date or regional-recipe equivalence is claimed. |
| p40 / 8 | [Vietnam Tourism: Old Quarter](https://vietnam.travel/things-to-do/explore-old-quarter-your-way/) documents street pho and the historical guild-street names retained in the caption. No exclusive birthplace or exact invention date is asserted. |
| p45 / 9 | [Korea Tourism Organization: Jeonju bibimbap](https://english.visitkorea.or.kr/svc/contents/contentsView.do?menuSn=904&vcontsId=221301) supports the hanok-village connection and uncertain origins; [Jeonju municipal food experiences](https://hanok.jeonju.go.kr/contents/en_exp7) links the village to bibimbap. The game is a simplified mixed rice recipe. |
| p50 / 10 | [Visit Seoul: Gwanghwamun Jip](https://english.visitseoul.net/eat/Gwanghwamun-Jip/ENP010933) documents neighborhood kimchi stew; [KTO: Gwanghwamun Gate](https://english.visitkorea.or.kr/svc/contents/contentsView.do?vcontsId=110592) identifies Gwanghwamun as the south gate of Gyeongbokgung. No royal-origin claim. |

## Verification and short summary

Run `Tools/qa/chapters/check.ps1` to compile the real chapter, album and menu-data sources as C# 9 against small Unity API stubs. It checks all 50 chapter mappings, chapter-end photo IDs, signature availability and actual menu presence, Korean titles, current night levels, partial/missing art, silent caching and existing album saves. `Tools/qa/ui_audit/check.ps1` compiles all runtime scripts against the installed Unity 6 assemblies without launching Unity. Editor Play Mode and on-device visual checks remain for after art installation.

Create **ten day window plates, ten night variants and ten matching landmark postcards**. Keep `Album/p05` through `Album/p50`; each photo records the chapter just completed, including **p05 = Hồ Gươm** and **p50 = Gwanghwamun**. Missing backgrounds retain the current lake scene. Dishes, recipes, difficulty and saves remain unchanged. Home's fixed twelve-card calendar is retained because five-level chapter headers would split its four-column rows and consume existing gaps.
