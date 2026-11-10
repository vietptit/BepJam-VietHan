# Album photo validation

- Nine reviewed Commons photographs installed; source metadata and file/licensing URLs are preserved in `selected.json` and `../album_credits.md`.
- Visually inspected the center-cropped contact sheet; no added watermarks or text overlays. Visible signs belong to the photographed places.
- `python Tools/photos/verify_photos.py` passes: nine 1024px-wide JPEG sources, nine 512×512 PNG outputs, original-image backups, and existing Unity sprite metadata present.
- All ten original PNGs are backed up in `../backup_v8/Album_illustrated/`. p50 is still its original illustration, pending a verified photo of the correct village; it receives no Commons attribution.
- Both detail and reward views reserve a separate 64-unit-high, muted credit label with 72-unit horizontal margins and `UIKit.Fit(24, 18)`. Caption viewports begin below the credit label, with existing button/next-destination padding retained. Missing IDs and null entries produce no credit label.
- `compile_check.ps1` checks actual runtime sources against installed Unity assemblies without launching Unity. Current project compilation fails in unrelated popup work: `Popups.cs` references missing `Culture.History`; `AutoPilot.cs` references missing `Popups.NewDish`.
- `compile_check.ps1 -StablePopupBaseline` passes, compiling the album changes and remaining current runtime scripts with the existing v8 backups of `Popups.cs` and `PopupsCulture.cs`. This is a scoped verification, not a claim that the entire current project compiles.
- Unity and device visual checks have not run, as requested.

Rebuild installed photo assets: `python Tools/photos/prepare_photos.py --install` then `python Tools/photos/write_credits.py`. Existing original backups are never overwritten.
