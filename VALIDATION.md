# Validation report — 2026-10-09

2026-10-10 の改善版で Windows 実機のUIA・OCR・Ctrlモード・設定保存・ログ配置を確認済み。最新の結果と再現手順は [認識改善記録](dev/testing/20261010-認識改善.md) を参照。以下は初版時点の記録。

Passed:
- .NET SDK 8.0.425 Release publish, win-x64, self-contained, 0 warnings / 0 errors.
- 18 automated geometry and stale-result gate assertions on .NET 8.
- UIA MTA worker process, bounded send/read deadlines, click-through styles, and negative-coordinate/DPI handling reviewed.
- Both Japanese/English models and native x64 Tesseract/Leptonica DLLs present in release.
- App executable, managed dependencies, runtime, source, instructions, and notices included.

Not executed:
- Windows GUI startup / native OCR DLL loading.
- Actual PowerPoint, PDF, browser UIA providers.
- Click-through and focus behavior, mixed-DPI physical monitors, screen sharing.

This is a first Windows build, not a claim of completed Windows end-to-end QA. See README for pre-presentation checks and limitations.
