# Third-party components

- Tesseract .NET wrapper 5.2.0, Copyright Charles Weld, Apache License 2.0. https://github.com/charlesw/tesseract ; installed from the official NuGet package ID Tesseract, version 5.2.0.
- Tesseract OCR native engine, Apache License 2.0. https://github.com/tesseract-ocr/tesseract . Native binaries distributed in the Tesseract NuGet package.
- Leptonica native image library, BSD-style license. https://github.com/DanBloomberg/leptonica . Native binaries distributed in the Tesseract NuGet package.
- Japanese and English OCR models from tessdata_fast (downloaded 2026-10-09), Apache License 2.0. https://github.com/tesseract-ocr/tessdata_fast . Model SHA-256 values are recorded in SHA256SUMS.txt.
- Microsoft .NET runtime / Windows Desktop runtime 8.0.31 and System.Reflection.Emit package 4.7.0, MIT and third-party notices. https://github.com/dotnet/runtime ; https://github.com/dotnet/wpf ; https://github.com/dotnet/winforms . Runtime license and third-party notice files are retained in licenses/.

Full available license texts are included in licenses/. No modifications were made to third-party binaries or models. Microsoft Visual C++ Runtime is not included: use Microsoft's official installer if required, subject to its terms. The application does not install dependencies automatically.
