# LiveTextMarker セキュリティ診断・改善実装レポート

診断日: 2026-10-15。対象: `6f9e4d1` / feature/full-refactoring / v1.1.0、開始時clean。UI Automation/OCRの入力、worker起動、管理IPC、NuGet依存を静的診断した。`dev/private`・`dev/temp`は対象外。

## 指摘と改善

|ID|重大度・成立条件|証拠|改善・実装状況|
|---|---|---|---|
|LTM-01|中。同一Windows利用者の巨大要求または巨大requestIdを含む未読応答により、管理pipeの資源消費/受付停止。|`source/ManagementEndpoint.cs:45` に読取り長上限なし、`:86` の応答書込みに期限なし。|共通IPC仕様として上限/期限を追加。今回の高優先修正対象外のため未実装。|
|LTM-02|低/運用。OCRのネイティブライブラリはNuGet advisoryだけでは全て評価できない。|`source/LiveTextMarker.csproj:10` のTesseract 5.2.0、`source/Detector.cs:73`。|ネイティブ依存版と配布元を台帳化し別途保守する。具体的CVE成立は今回確認していない。|

`source/Detector.cs:23`、`:36` はIsPasswordを検出してSensitiveを返し、UIA textは16385文字で上限を設ける（:44）。OCRの認識結果をファイルへ保存する処理は診断範囲で確認されなかった。workerは自身の実行ファイルをシェルなしで起動（`source/Worker.cs:12`）。管理pipeは利用者専用・2秒読取り期限。高/致命的の確定事項は見つからなかった。

## 依存・秘密・検証

`dotnet list source/LiveTextMarker.csproj package --vulnerable --include-transitive --format json --no-restore` は2026-10-15に終了コード0、NuGet.orgからの報告対象0。既存restore資産に対する照合であり、native DLLやOS/.NETの完全診断ではない。

追跡テキスト38ファイルのPEM秘密鍵/GitHub/AWS/OpenAI代表パターン値非表示検査は一致0。未追跡/無視対象・一般パスワード・過去履歴は未検査。

- [x] 静的診断、依存照合、改善案、文書差分検査。
- 製品コード変更なし。文書のみのため製品ビルド・GUIを形式的に実行しない。
- 実テキスト・実パスワード・OCR画像・既存個人設定は使用していない。外部公開/配備/親gitlink更新なし。
