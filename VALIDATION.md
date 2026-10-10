# Validation report — 2026-10-09

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
