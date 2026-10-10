Add-Type -AssemblyName System.Windows.Forms,System.Drawing
$form=[Windows.Forms.Form]::new(); $form.Text='LiveTextMarker タブ境界テスト'; $form.ClientSize=[Drawing.Size]::new(820,240); $form.StartPosition='CenterScreen'
$tabs=[Windows.Forms.TabControl]::new(); $tabs.Dock='Fill'; $tabs.Font=[Drawing.Font]::new('Yu Gothic UI',12)
foreach($name in @('音声','認識比較','操作','操作履歴','技','ナレッジ','設定')) { $page=[Windows.Forms.TabPage]::new($name); $label=[Windows.Forms.Label]::new(); $label.Text='本文領域はタブ見出しと区別します。'; $label.AutoSize=$true; $label.Location=[Drawing.Point]::new(20,40); $page.Controls.Add($label); $tabs.TabPages.Add($page) }
$form.Controls.Add($tabs); [Windows.Forms.Application]::Run($form)
