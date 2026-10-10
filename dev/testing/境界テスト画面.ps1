param([switch]$Ocr,[switch]$Cover,[int]$CoverX=0,[int]$CoverY=0)
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
$form=[System.Windows.Forms.Form]::new()
if($Cover) {
 $form.Text='LiveTextMarker 境界カバー'; $form.StartPosition='Manual'; $form.Location=[System.Drawing.Point]::new($CoverX,$CoverY)
 $form.ClientSize=[System.Drawing.Size]::new(220,350); $form.TopMost=$true
 $form.BackColor=[System.Drawing.Color]::LightBlue
} else {
 $form.Text='LiveTextMarker フレーム境界テスト'; $form.ClientSize=[System.Drawing.Size]::new(900,320); $form.StartPosition='CenterScreen'
 $layout=[System.Windows.Forms.TableLayoutPanel]::new(); $layout.Dock='Fill'; $layout.ColumnCount=2
 $layout.ColumnStyles.Add([System.Windows.Forms.ColumnStyle]::new([System.Windows.Forms.SizeType]::Percent,50)) | Out-Null
 $layout.ColumnStyles.Add([System.Windows.Forms.ColumnStyle]::new([System.Windows.Forms.SizeType]::Percent,50)) | Out-Null
 foreach($side in @('左','右')) {
  if($Ocr) {
   $image=[System.Drawing.Bitmap]::new(440,300); $graphics=[System.Drawing.Graphics]::FromImage($image); $graphics.Clear([System.Drawing.Color]::White)
   $font=[System.Drawing.Font]::new('Yu Gothic UI',16)
   $graphics.DrawString("${side}側の画像だけを認識します。",$font,[System.Drawing.Brushes]::Black,10,25)
   $graphics.DrawString('別のペインには伸ばしません。',$font,[System.Drawing.Brushes]::Black,10,65)
   $graphics.Dispose(); $font.Dispose()
   $control=[System.Windows.Forms.PictureBox]::new(); $control.Image=$image
  } else {
   $control=[System.Windows.Forms.RichTextBox]::new(); $control.Font=[System.Drawing.Font]::new('Yu Gothic UI',16); $control.ReadOnly=$true
   $control.Text="${side}側のフレーム内にある文章です。折り返しは同じフレームの中だけで認識し、隣のフレームへ下線や塗りを伸ばしません。"
  }
  $control.Dock='Fill'; $control.AccessibleName="${side}フレーム"; $layout.Controls.Add($control)
 }
 $form.Controls.Add($layout)
}
[System.Windows.Forms.Application]::Run($form)
