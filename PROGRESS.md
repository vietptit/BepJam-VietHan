# Bếp Jam: Việt – Hàn — Ghi chú tiến độ

_Cập nhật: 07/11/2026_

## Tổng quan
Game 2D Unity (6000.3.21f1, Built-in RP, uGUI), dọc 1080x2336. Lối chơi lấy từ bản HTML "Bếp Jam": chạm khay nguyên liệu → nguyên liệu bay lên đĩa → 2 nguyên liệu khớp công thức tự ghép thành món → món cần nấu thì vào lò/bếp → món bay tới khách. Thùng sau chỉ mở khi lấy hết thùng trước; 4 đĩa chờ; booster Gợi ý / Trả đĩa; tính giờ, chuỗi combo, 3 sao.
- 8 món (theo PDF Lv1–8): Bánh mì chả lụa, Cơm trộn kimchi, Cà phê sữa, Trà yuja, Xôi đậu xanh (bếp ga), Bungeoppang (lò), Bánh mì trứng (lò), Tteokbokki (bếp ga).
- 12 màn ("Ngày 1–12"), bố cục thùng sinh tự động và **được bộ giải chứng minh thắng được** (`Core/LevelGen.cs`).

## Mở & chạy
- Mở project `BepJam2D` bằng Unity 6000.3.21f1 → menu **BepJam → Play (portrait 1080x2340)**.
- QA tự động: tạo file `Library/bj_qa_once` rồi để Unity reload script → game tự chơi và chụp ảnh vào `Tools/qa/`.
- Build Windows: menu **BepJam → 2. Build Windows** (đầu ra `Builds/Windows/`).

## Cấu trúc code (`Assets/_Game/Scripts`)
| File | Vai trò |
|---|---|
| `Data/GameData.cs` | Nguyên liệu, món, 12 màn, bảng màu, cache sprite |
| `Core/Kitchen.cs` | Model gameplay theo thời gian, phát sự kiện cho view |
| `Core/LevelGen.cs` | Sinh bố cục thùng + DFS solver + tỉ lệ thua ngẫu nhiên |
| `Game/GameView.cs` | Dựng màn chơi, xử lý sự kiện → animation/VFX/âm thanh, HUD, tooltip công thức |
| `Game/Views.cs` | TileView (giữ 0.4s để xem thông tin), PlateView, StationView (lửa, hơi nước, vòng tiến độ), CustomerView |
| `Game/CookFx.cs` | Animation chế biến theo động từ: Pha (rót), Kẹp (cắt đôi bánh mì), Trộn (đũa đảo), món bếp — **Codex viết** |
| `Juice/Tween.cs`, `Juice/VFX.cs` | Tween engine + hạt UI (Codex viết) |
| `Audio/AudioManager.cs` | Nhạc + SFX (Codex viết; âm thanh sinh bằng `Tools/gen_audio.py`) |
| `UI/Popups.cs`, `UI/HomeView.cs`, `UI/UIKit.cs` | Popup, màn hình chính, helper uGUI |
| `Editor/*` | Import sprite, dựng scene, build, AutoPlay/QA |

## Phong cách đã chốt
- **Venba**: phẳng, không viền, hạt nhiễu risograph, bảng màu đất nung / mận / teal / mù tạt / kem.
- Đồ ăn (23) + đạo cụ (lò, bếp, chai) vẽ bằng **Gemini** → `Tools/gemini/raw` (gốc) và `Tools/gemini/cut` (đã tách nền). Asset gốc ban đầu sao lưu ở `Tools/backup_art/`.
- Khay nguyên liệu: khay gỗ Gemini + viền nâu đậm (`Assets/Resources/UI/tile.png`), không có chữ (giữ để xem thông tin).

## Bố cục màn chơi đang làm (Foodie-Pop-like, toạ độ canvas 1080x2336)
1. HUD trên cùng (Khách · Ngày/Giờ · Chuỗi, nút về sảnh / cài đặt).
2. Rèm noren xanh trên cùng (đã bỏ mái hiên sọc) — y 95–300.
3. Khách **chibi** ló đầu qua quầy, x ≈ 230 / 540 / 850, y 330–600.
4. Quầy gỗ y 600–760: 4 **đĩa nhiều màu** tại x ≈ 200 / 427 / 653 / 880, y ≈ 650.
5. Hàng bếp y 760–1010: **lò vi sóng** x 60–360, **nồi lẩu/bếp ga** x 390–690, **thớt chặt** x 720–1020 (thớt hiện chỉ trang trí).
6. Bàn khăn mận thẳng x 60–1020, y 1050–2010, lưới **4×4** khay ~210x175 tại x 195/425/655/885, y 1140/1370/1600/1830.
7. Dải sàn gạch bông y 2040–2336: 3 nút tròn Gợi ý / Trả đĩa / Công thức.

## File duyệt (chưa đưa vào Unity)
- `Tools/review/layout_mock_v6b.jpg` — mô phỏng mới nhất (v6 + rèm noren thay mái hiên, bản ghép nhanh).
- `Tools/review/v6/` (nền v6), `Tools/review/v3/` (khách chibi `c_*.png` + `c_*_happy.png`, đĩa `plate_0..3.png`).
- `Tools/review/v7/` + `Tools/review/layout_mock_v7.jpg` — **bản mới nhất** (Codex vẽ): rèm noren 6 tấm trải ngang dưới dầm gỗ, chờ duyệt. Script `Tools/compose_shop_v7.py`.
- Script nền: `Tools/compose_shop_v3..v7.py` (Codex viết, chạy bằng `python -I`).

## Việc tiếp theo
1. Duyệt bản v7 → copy `bg_shop.png`, `bg_front.png` vào `Assets/Resources/UI/`; khách chibi + mặt vui vào `Assets/Resources/Characters/`; đĩa nhiều màu vào `Assets/Resources/UI/plate_0..3`.
2. Cập nhật toạ độ trong `GameView.cs` theo bố cục trên (code bố cục 4×4 đã có, cần chỉnh lò/bếp vào hàng mới, đĩa đổi màu theo index, khách chibi dùng sprite `_happy` khi được phục vụ, vùng mask khách theo dải 150–600).
3. Biên dịch + chạy QA, xem `Tools/qa/`.
4. Đồng bộ phong cách Venba cho màn hình chính, popup, icon nút.
5. (Tuỳ chọn) biến thớt chặt thành trạm thật cho món "Kẹp".

## Lưu ý quy trình
- Gemini tải ảnh qua hộp thoại Save của trình duyệt trong app → cần người bấm Save; khi vắng mặt dùng Codex (vẽ bằng Python) — sandbox Codex không chạy được Python nên Claude chạy script sau.
- Mọi bản vẽ mới xuất ra `Tools/review/` để chủ dự án duyệt trước khi đưa vào Unity.

## Cập nhật 07/11/2026 (tối) — v7 đã vào Unity
- Đã copy `Tools/review/v7/bg_shop.png`, `bg_front.png` → `Assets/Resources/UI/`; khách chibi `c_*.png` + `c_*_happy.png` → `Assets/Resources/Characters/`; đĩa `plate_0..3.png` → `Assets/Resources/UI/` (đĩa thứ i dùng màu i).
- `GameView.cs`: toạ độ theo v7 (khách 200x200 art, đáy 486; đĩa y 513; lò (47,628) · nồi (308,596); lưới 4×4 khay 166x138, hàng 0 sát hàng bếp ở y 900 → các hàng sau xuống dưới).
- Animation khách chibi: **ló đầu lên** từ sau quầy (OutBack) khi đến, đổi sang **mặt vui** `_happy` khi được phục vụ, **lắc rồi thụt xuống** khi rời đi.
- QA tự động chạy trọn luồng, không lỗi (ảnh ở `Tools/qa/`).
- Còn lại: màn ít nguyên liệu để trống nửa dưới bàn (do số thùng của màn); màn hình chính / popup / icon chưa theo Venba; thớt chặt mới là hình nền.

## Cập nhật 07/11/2026 (khuya) — UI Venba, 50 màn, văn hoá, hướng dẫn
- **UI Venba** đã vào game: nền màn hình chính (Gemini, `Assets/Resources/UI/home_bg.png`), bộ UI giấy xé + 18 icon (Codex, `Tools/gen_ui_venba.py`, `Tools/gen_icons_venba.py`) được copy đè lên tên sprite cũ trong `Assets/Resources/FX/` (bản gốc ở `Tools/backup_art/FX/`). Nút chữ kem có padding + tự co chữ (`UIKit.Fit`).
- **Màn hình chính** (`UI/HomeView.cs`): logo giấy xé, 2 khách chibi chào, bảng chọn ngày dạng **tờ lịch giấy rách**, 12 ngày/trang, **vuốt hoặc mũi tên**, chấm trang, tự mở trang có ngày đang chơi.
- **50 màn** (`Data/GameData.cs`): 1–12 thiết kế tay, 13–50 theo 8 khối chủ đề, nhịp dễ→khó→nghỉ→khó, **Đại tiệc** ngày 20/30/40/50. Tài liệu: `Tools/review/level_design.md`.
- **Hướng dẫn** (`GameView.Onboarding/Coach`): bàn tay + bong bóng chỉ vào món gọi, đĩa, đồng hồ, thùng ẩn, bếp, lò, nguyên liệu dùng chung, đơn 2 món.
- **Booster mở khoá dần**: Công thức ngày 2, Gợi ý ngày 3, Trả đĩa ngày 6; trước đó có ổ khoá; ngày mở khoá có thẻ "Trợ giúp mới!" rồi bàn tay chỉ nút.
- **Văn hoá** (`Data/Culture.cs`, `UI/PopupsCulture.cs`): thẻ **Món mới!** (con dấu, tia sáng, hơi nước, chữ viết dần, nhạc đàn bầu/gayageum, có mục **Lịch sử & văn hoá**), thẻ **Chuyện bên mâm** (lật trang, chữ gõ dần, xoay vòng chuyện), **Sổ tay ẩm thực**, **Cài đặt** (nhạc/âm thanh/rung, chơi lại từ đầu có xác nhận). Âm thanh mới: `Tools/gen_audio_ui.py`.
- **Cầu nối nền** (`Editor/BackgroundBridge.cs`): ghi `refresh` / `qa` / `play` / `stop` vào `Library/bj_cmd.txt` để điều khiển Unity mà không cần focus cửa sổ.
- QA tự động chạy trọn luồng không lỗi (ảnh ở `Tools/qa/`).

## Cập nhật 07/11/2026 (đêm) — padding + VFX/animation
- Padding: nút cài đặt/mũi tên trang/tờ lịch ở màn hình chính, chữ trong Sổ tay, nút HUD trong màn chơi; bong bóng gọi món nằm **trên đầu khách** (không chạm lề, không chen nhau).
- Chuyển cảnh **giấy xé quét ngang** giữa màn hình chính ↔ màn chơi (`GameBoot.PaperWipe`, coroutine, không bị KillAllTweens cắt).
- Popup đóng = tờ giấy **bị ném đi** (rơi + xoay).
- Đầu ngày: biển **"Ngày N · Mở hàng!"** rơi xuống (Đại tiệc có rung + con dấu).
- Phục vụ món: **xu bay** về ô Khách; chuỗi combo = **con dấu đỏ "CHUỖI xN"** (x3+ loé sáng + rung).
- 10 giây cuối: **viền đỏ nhấp nháy**, đồng hồ lắc.
- Không khí: **bụi nắng** lơ lửng ở cửa sổ, nồi trên bếp **bốc hơi** cả lúc rảnh.

## Build Android
- APK: `Builds/Android/BepJam_VietHan.apk` (~47 MB, IL2CPP, ARM64 + ARMv7, Android 7.0+, package `com.bepjam.viethan`, ký bằng debug key – dùng để cài thử/nộp demo, chưa phải bản phát hành Play Store).
- Build lại: ghi `apk` vào `Library/bj_cmd.txt` khi Unity đang mở, hoặc menu **BepJam → 4. Build Android APK**.
- Icon app: `Assets/_Game/Icon/app_icon.png`. Ảnh nén ASTC 6x6 riêng cho Android (`Editor/SpriteImportSettings.cs`).
- Lưu ý: Unity hiện đang ở nền tảng Android (Build Settings) sau khi build.

## Gameplay v2 (08/11)
- 24 món (2–4 nguyên liệu), đĩa gom dần (chip 2/4), thớt cắt từ ngày 9, khách VIP có thanh kiên nhẫn từ ngày 11 (hết giờ = thua), nguyên liệu gây nhiễu từ ngày 13, 50 màn tạo lại trong GameData.BuildLevels. Code gameplay do Codex viết (Kitchen/LevelGen/Views/GameView/RecipeUI). 60 hình mới vẽ bằng Gemini (Tools/gemini/raw/ing_A..D, dish_E). Lưu ảnh Gemini chạy ngầm bằng Tools/auto_save.ps1 (WM_SETTEXT, không chiếm focus).
- Chưa có nội dung văn hoá cho 16 món mới.

## 2026-11-10 (đêm) – Gameplay v3, Album, UI audit
- 6 đĩa, mỗi đĩa 1 nguyên liệu; đủ bộ nguyên liệu của một món đang gọi thì tự gom (Gather) rồi ghép. Bỏ chip "2/3".
- Kho đúng số nguyên liệu (bỏ decoy) → thắng là hết sạch. Lưới 4×4 luôn đầy, khay úp chia đều sau các cột.
- Độ khó: máy chơi bừa 200 lần/màn; 48/50 màn đúng dải mục tiêu (màn 1–4 ≈0%, ~10–25: 40–70%, 26+: 70–90%, boss ≥85%).
  Báo cáo: Tools/difficulty_report.txt (menu BepJam/Report Difficulty).
- Màn thua mới (hết giờ / đầy đĩa / VIP bỏ về / hết nguyên liệu) + nút "Trả 1 đĩa & chơi tiếp"; VIP có hào quang, vương miện, ruy băng.
- Album Việt – Hàn: 10 ảnh (màn 5,10,…,50), popup thưởng sau khi thắng, nút Album ở Home. Data/Album.cs, UI/PopupsAlbum.cs.
- UI audit: tip/coach lên lớp trên cùng, không còn bị khay che; popup nhiều món cuộn/thu nhỏ; dọn animation sót.
  QA cuối (level 49 + boss 50 + album): 22 ảnh trong Tools/qa, 0 exception.
- Bánh mì: chém chéo 1 nhát rồi bay ngay về đĩa; bỏ vạch xanh khi trộn trong bát.
- Sao lưu: Tools/backup_v3..v6. UnlockAll = true (GameBoot.cs) để test. Chưa build APK.

## 2026-11-10 — Không giới hạn thời gian, VIP dễ hơn, hướng dẫn gọn
- Bỏ đồng hồ màn chơi, thua do hết giờ, cảnh báo/viền đỏ/tiếng tích tắc. HUD giữa: Ngày + số VIP còn lại; intro dùng số VIP; thắng không còn thống kê giây. Thua chỉ còn VIP, kẹt đĩa hoặc hết nguyên liệu.
- Sao theo trợ giúp: không dùng Gợi ý/Trả đĩa = 3 sao; dùng tổng 1–2 lần = 2 sao; từ 3 lần = 1 sao. Chờ lâu không giảm sao.
- Kiên nhẫn VIP ×1,8, giảm dần theo ngày (~79,8 giây ngày 11 → 54 giây ngày 50, luôn trên 45 giây). Icon đơn hàng 66 → 99, ghế rộng 228; đơn 2 món vẫn nằm ngang và không chạm HUD/ghế bên.
- Coach và tip đầu game thành dải không chặn chạm, mỗi nội dung một lần/save, tự ẩn sau ~3 giây hoặc lần chạm tiếp theo, cách nhau ≥6 giây; đợi hết bay/ghép/cắt/nấu và ngừng chạm nhanh. Không còn modal giới thiệu booster; bỏ tip đồng hồ và tip lặp. Tay ngày 1 kết thúc sau món đầu tiên. Xoá tiến trình sẽ xoá lịch sử tip.
- Harness: 50/50 replay thắng, kiểm tra chờ 10 phút rồi thắng 3 sao, VIP, kẹt/khôi phục, thiếu nguyên liệu và sao trợ giúp đều qua. Báo cáo không còn cột thời gian: 48/50 đúng dải; ngày 5/6 thiếu 1,0/2,5 điểm %, vẫn trong mức cho phép, kết quả dải không đổi.
- Biên dịch C# 9 runtime + UNITY_EDITOR với assembly Unity 6/uGUI qua; 16 kiểm tra tween qua. AutoPilot kiểm tra thắng/thua VIP, tip không chặn lấy nguyên liệu, gap 6 giây, tự ẩn 3 giây, tay ngày 1, intro 49 và boss 50. Ảnh và ghi chú: Tools/qa/timer_free/README.md.
