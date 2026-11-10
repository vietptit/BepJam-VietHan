# Bếp Jam: Việt – Hàn — guide for Claude / contributors

Cozy cooking-merge puzzle (Venba-like art) for a Vietnam–Korea contest. **Unity 6000.3.21f1**, uGUI built entirely from code,
portrait 1080×2336, Android target. Read `PROGRESS.md` for the full change log / design history.

## Layout
- `Assets/_Game/Scripts/`
  - `Core/Kitchen.cs` – game rules (6 plates, 1 item per plate, auto-gather when a recipe set is on plates, VIP-first priority,
    lose = VIP timeout / stuck / no ingredients, `Revive`). `Core/LevelGen.cs` – pantry generation, DFS solvability proof,
    careless-player simulation for difficulty bands.
  - `Data/GameData.cs` – dishes, recipes, 50 levels (difficulty ramp). `Data/Chapters.cs` – 10 chapters × 5 levels, place,
    day/night backgrounds `Resources/BG/chNN[_night]`. `Data/Album.cs` – photo won at level 5k = next chapter's place;
    p50 = finale. `Data/AlbumCredits.cs` – Wikimedia Commons attribution. `Data/Culture.cs` – dish history text.
  - `Game/` – `GameBoot.cs` (`UnlockAll` testing switch, keep **false** for release), `GameView.cs` (HUD, stations, background),
    `Views.cs` (customers, plates, board/cut FX), `CookFx.cs`, `HomeView.cs`, `AutoPilot.cs` (automated QA run).
  - `UI/` – `UIKit.cs` (layout helpers, `UIKit.Fit` for text), `Popups*.cs` (intro, win, lose, album, notebook…).
  - `Editor/` – `BackgroundBridge.cs` (file-command bridge), `DifficultyReport.cs` (menu BepJam/Report Difficulty),
    `LogicAudit/` (headless replay of all levels).
- `Assets/Resources/` – all art loaded by name (`Food/`, `UI/`, `FX/`, `BG/`, `Album/`, `Characters/`, `Audio/`).
- `Tools/` – art pipeline scripts (`cut_gemini.py` sheet → sprites, `place_chapter_art.py`), `chapter_art.md` (art prompts),
  `album_credits.md`, `difficulty_report.txt`, `DifficultyHarness/`.

## Working without touching the Editor UI
With the project open in Unity, write one word to `Library/bj_cmd.txt`:
- `refresh` – reimport + compile. Check `%LOCALAPPDATA%/Unity/Editor/Editor.log` for `error CS` / "Domain Reload Profiling".
- `qa` – AutoPilot plays and saves screenshots to `Tools/qa/` (+ `qa-errors.txt`); start level from `Library/bj_cmd.txt.level`.
- `apk` – builds `Builds/Android/BepJam_VietHan.apk`.

## Conventions
- All UI is code-built; padding ≥ 24 px, every text through `UIKit.Fit`, nothing may overlap the HUD / plates / pantry.
- Art style: flat 2D, Venba-like (muted pastel, thin plum-brown outlines, hatching shading). New sprites keep existing names.
- Every level must stay solvable (LevelGen proof) with exact stock (no leftover ingredients on win).
- Facts in captions/culture text must be accurate; cautious wording when uncertain. Album photos are CC-licensed — keep credits.
