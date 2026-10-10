Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
$form = [System.Windows.Forms.Form]::new()
$form.Text = 'PointerUnderline 認識テスト'
$form.ClientSize = [System.Drawing.Size]::new(580,260)
$form.StartPosition = 'CenterScreen'
$text = [System.Windows.Forms.RichTextBox]::new()
$text.Dock = 'Fill'
$text.Font = [System.Drawing.Font]::new('Yu Gothic UI',18)
$text.ReadOnly = $true
$text.Text = "これは文章の行頭から行末までを確認するためのテストです。文章が途中で折り返されても、最後の文字までそれぞれの表示行に下線を引きます。`r`n`r`n別の段落には下線を引きません。"
$text.Select(12,10)
$text.SelectionColor = [System.Drawing.Color]::Blue
$text.Select(0,0)
$form.Controls.Add($text)
[System.Windows.Forms.Application]::Run($form)
