# LiveTextMarker の開発ルール

- 全独自ウィンドウのタイトルは `画面名 - LiveTextMarker - ver X.Y.Z`。メイン画面のみ画面名を省略する。半角ハイフンで区切り、WindowTitle.Createでアセンブリ版から生成する。操作説明と作者名はタイトルへ追加しない。

- 親フォルダの AGENTS.md に従う。PowerShell は pwsh を使用する。
- 文書・コミットは日本語で残す。既存の source/ と app/ の構成を維持する。
- 利用方法は README.md、認識処理は source/Detector.cs、表示範囲と検証は source/Geometry.cs と source/tests/ を参照する。
- 改善計画と実測結果は dev/testing/ に記録する。必要な資料だけを読む。
- OCR画像と認識した文章をログへ保存しない。ログは方式・範囲・結果だけをメモリ上に保持する。
- app/ は配備成果物。ビルドと検証を通した後、通常終了したアプリを更新して再起動する。
- dev/private/ と dev/temp/ は Git・通常の調査から除外する。
- 現行の製品仕様は docs/仕様.md、実測済み・未測定の範囲は VALIDATION.md を参照する。dev/testing/ の過去の記録や画像は当時の証跡であり、現行の利用手順と混同しない。
