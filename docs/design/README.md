# 設定画面のデザイン

## 現状評価

ユーザー提供の画面を評価した。入力欄の位置・幅が揃っていない、設定の意味ごとのまとまりが弱い、操作ボタンと状態表示が分離している点を改善対象とした。ログ欄の見やすさと画面全体のコンパクトさは維持する。

## 採用したデザイン

[画像生成した案](設定画面案.png) に沿い、キー設定と表示設定を白いパネルで整理する。ラベル列と入力欄を整列し、余白は共通化する。開始ボタンだけを青いアクセントとし、状態表示を操作行の右へ配置する。ログは残りの高さを使い、フッターの終了ボタンは右下に置く。説明ラベル・ページ見出しは追加しない。

[実装画面](設定画面実装.png) は対象ウィンドウだけを撮影したもの。画像の寸法は682×572ピクセル、コード上のClientSizeは680×540、フォントはYu Gothic UI 10pt。ログ欄は約180ピクセルの高さを確保した。生成案の架空のログは実装へ取り込まず、起動後の実際のログだけを表示する。

タイトル、修飾キー4種＋最後のキーの横並び、既定Ctrl実行、設定保存、×でトレイ格納、右下の「アプリを終了させる」を維持する。狭いウィンドウで設定行を折り返さないよう、最小サイズを設ける。サイズ変更・複数DPIの動作テストは行っていない。

## 画像生成

組み込みimage_genを使用した。以下が使用した最終プロンプト。参考画像はユーザーのスクリーンショットを会話で評価し、新規の画像として生成した。

```text
Create one polished realistic Windows 11 desktop settings window UI design mockup, straight-on, no background surrounding window, approximately 680 by 550 logical pixels, Japanese text crisp. This is a NEW design reference for implementation in WinForms, not a futuristic web dashboard. Native Windows title bar exact text '設定 - LiveTextMarker - ver 1.1.0', with normal minimize/maximize/close controls. Absolutely NO page heading, no English subtitle, no logo, no decorative illustrations, no explanatory captions. Warm pale gray app background, two compact white flat bordered panels with subtle gray dividers, dark readable Yu Gothic UI text, restrained blue accent on selected checkboxes and primary Start button. Window content padding24, consistent aligned labels105wide, consistent input heights30. Top white panel contains exactly two horizontal keyboard shortcut rows: label '開始／一時停止' then Ctrl checkbox checked, Alt checkbox checked, Shift unchecked, Win unchecked, then combo box showing U, at far right. Second row label 'アプリ終了', same four checkboxes, combo Q. All modifier checkboxes and last-key combo MUST stay on same row. Beneath shortcut panel, two existing checkboxes in one vertical compact section: '画像・スライドにも対応（ローカルOCR）' checked, 'Ctrl押下で実行' checked. Second white panel has two aligned rows. First row: '表示方法' with dropdown '下線', then '濃さ（%）' and disabled spinner16. Second row: '太さ' spinner3, '上下位置' spinner -1.5, button '色を選ぶ'. Below panels one action row: blue '開始 / Start' button, neutral '通知領域へ' button, status 'Ctrl待機中' aligned right. Beneath it a spacious white bordered recent log list taking remaining available height, several compact timestamp lines showing UI Automation and OCR detection, no extra section title. Bottom footer '直近10件' on left and neutral outlined button exact text 'アプリを終了させる' at bottom RIGHT. Avoid large margins, giant cards, rounded pill controls, tall headings. Show all controls without clipping or scrolling the whole settings page. This must look achievable with native WinForms controls.
```
