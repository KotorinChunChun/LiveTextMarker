param([string]$Exe = "$PSScriptRoot/../../source/out/LiveTextMarker/LiveTextMarker.exe")
$ErrorActionPreference = 'Stop'
. 'C:/Users/kotorichun/.codex/skills/windows-ui-automation/scripts/ui-toolkit.ps1'
. "$PSScriptRoot/検証ウィンドウ共通.ps1"
$fixture = Start-Process -FilePath (Get-Command pwsh).Source -ArgumentList @('-NoProfile','-STA','-File',"`"$PSScriptRoot/文章テスト画面.ps1`"") -WindowStyle Hidden -PassThru
$worker = $null
try {
 $window = Wait-AppWindow -Title 'LiveTextMarker 認識テスト'
 [WUA.Win]::ShowWindow($window,5) | Out-Null
 [WUA.Win]::SetForegroundWindow($window) | Out-Null
 Start-Sleep -Milliseconds 500
 $root = [System.Windows.Automation.AutomationElement]::FromHandle($window)
 $edit = $root.FindFirst([System.Windows.Automation.TreeScope]::Descendants,[System.Windows.Automation.Condition]::TrueCondition)
 $pattern = $edit.GetCurrentPattern([System.Windows.Automation.TextPattern]::Pattern)
 $rect = $edit.Current.BoundingRectangle
 $paragraph = $pattern.RangeFromPoint([System.Windows.Point]::new($rect.X+30,$rect.Y+20))
 $paragraph.ExpandToEnclosingUnit([System.Windows.Automation.Text.TextUnit]::Paragraph)
 $expected = $paragraph.GetBoundingRectangles()
 $ending = $pattern.DocumentRange.FindText('引きます。',$false,$false).GetBoundingRectangles()[-1]
 $start = [System.Diagnostics.ProcessStartInfo]::new([IO.Path]::GetFullPath($Exe),'--worker')
 $start.UseShellExecute=$false; $start.CreateNoWindow=$true
 $start.RedirectStandardInput=$true; $start.RedirectStandardOutput=$true
 $worker = [System.Diagnostics.Process]::Start($start)
 foreach($line in @($expected[0],$expected[-1])) {
  $request = @{Kind='uia';X=[int]($line.X+20);Y=[int]($line.Y+$line.Height/2)} | ConvertTo-Json -Compress
  $worker.StandardInput.WriteLine($request)
  $answer = $worker.StandardOutput.ReadLineAsync()
  if(-not $answer.Wait(8000)) { throw '認識タイムアウト' }
  $result = $answer.Result | ConvertFrom-Json
  if($result.Source -ne 'UI Automation' -or $result.Lines.Count -ne $expected.Count) { throw '折り返し行数が一致しません' }
  if([Math]::Abs($result.Lines[-1].Right-($ending.X+$ending.Width)) -gt 2) { throw '末尾の文字範囲が欠けています' }
  "PASS: UIAの行頭・行末・折り返し $($result.Lines.Count)行"
 }
} finally {
 if($worker) { if(-not $worker.HasExited) { $worker.Kill() }; $worker.Dispose() }
 $fixture.Refresh()
 Close-TestWindow $fixture
}
