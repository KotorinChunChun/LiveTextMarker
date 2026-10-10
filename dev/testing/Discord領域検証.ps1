param([string]$Exe="$PSScriptRoot/../../source/out/LiveTextMarker/LiveTextMarker.exe")
$ErrorActionPreference='Stop'
Add-Type -AssemblyName UIAutomationClient,UIAutomationTypes
$root=[System.Windows.Automation.AutomationElement]::RootElement.FindAll([System.Windows.Automation.TreeScope]::Children,[System.Windows.Automation.Condition]::TrueCondition) | Where-Object { (Get-Process -Id $_.Current.ProcessId -ErrorAction SilentlyContinue).ProcessName -eq 'Discord' } | Select-Object -First 1
if(-not $root){throw 'Discordが起動していません'}
$nodes=$root.FindAll([System.Windows.Automation.TreeScope]::Descendants,[System.Windows.Automation.Condition]::TrueCondition)
$si=[Diagnostics.ProcessStartInfo]::new([IO.Path]::GetFullPath($Exe),'--worker'); $si.UseShellExecute=$false; $si.CreateNoWindow=$true; $si.RedirectStandardInput=$true; $si.RedirectStandardOutput=$true
$p=[Diagnostics.Process]::Start($si); $checked=0; $wrapped=0
try {
 foreach($node in $nodes) {
  $b=$node.Current.BoundingRectangle
  if($node.Current.ControlType -ne [System.Windows.Automation.ControlType]::Text -or $b.Width -lt 10 -or $b.Height -lt 3 -or $b.Height -gt 100){continue}
  $x=[int]($b.X+5);$y=[int]($b.Y+$b.Height/2)
  $at=[System.Windows.Automation.AutomationElement]::FromPoint([System.Windows.Point]::new($x,$y))
  if(($at.GetRuntimeId() -join '-') -ne ($node.GetRuntimeId() -join '-')){continue}
  $p.StandardInput.WriteLine((@{Kind='uia';X=$x;Y=$y;TargetWindow=$root.Current.NativeWindowHandle}|ConvertTo-Json -Compress))
  $task=$p.StandardOutput.ReadLineAsync(); if(-not $task.Wait(5000)){throw 'timeout'}
  $result=$task.Result | ConvertFrom-Json
  if(-not $result.Bounds){continue}
  if($result.Scope.X -lt $b.X-1 -or $result.Scope.Right -gt $b.Right+1 -or $result.Scope.Y -lt $b.Y-1 -or $result.Scope.Bottom -gt $b.Bottom+1){throw '本文要素より広い領域を検出'}
  foreach($row in $result.Lines){if($row.X -lt $b.X-1 -or $row.Right -gt $b.Right+1 -or $row.Y -lt $b.Y-1 -or $row.Bottom -gt $b.Bottom+1){throw '本文外の矩形を検出'}}
  $checked++; if($result.Lines.Count -gt 1){$wrapped++}
 }
 if($checked -eq 0){throw '検証可能なDiscordの文字がありません'}
 [pscustomobject]@{確認要素数=$checked;複数行要素数=$wrapped;本文外矩形数=0} | ConvertTo-Json | Set-Content "$PSScriptRoot/Discord領域実測.json" -Encoding utf8
 "PASS: Discordの文字$checked 要素、複数行$wrapped 要素、本文外の矩形0"
} finally {$p.Kill();$p.Dispose()}
