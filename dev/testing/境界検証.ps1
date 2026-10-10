param([switch]$Ocr,[switch]$Highlight)
$ErrorActionPreference='Stop'
. 'C:/Users/kotorichun/.codex/skills/windows-ui-automation/scripts/ui-toolkit.ps1'
. "$PSScriptRoot/検証ウィンドウ共通.ps1"
Add-Type -AssemblyName System.Drawing
Add-Type @'
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
public static class BoundaryProbe {
 public delegate bool Callback(IntPtr h,IntPtr data);
 [DllImport("user32.dll")] public static extern void keybd_event(byte key,byte scan,uint flags,UIntPtr extra);
 [DllImport("user32.dll")] static extern bool EnumWindows(Callback callback,IntPtr data);
 [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr h);
 [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h,out uint pid);
 [DllImport("user32.dll")] static extern IntPtr GetWindowLongPtr(IntPtr h,int index);
 [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr h,out Rect r);
 [StructLayout(LayoutKind.Sequential)] public struct Rect { public int Left,Top,Right,Bottom; }
 public static Rect[] Areas(uint pid) { var result=new List<Rect>(); EnumWindows((h,d)=>{ GetWindowThreadProcessId(h,out var owner); if(owner==pid && IsWindowVisible(h) && (GetWindowLongPtr(h,-20).ToInt64()&0x20)!=0) { GetWindowRect(h,out var r); result.Add(r); } return true; },IntPtr.Zero); return result.ToArray(); }
}
'@
function Query-Worker($request) {
 $worker.StandardInput.WriteLine(($request | ConvertTo-Json -Compress -Depth 8))
 $answer=$worker.StandardOutput.ReadLineAsync()
 if(-not $answer.Wait(8000)) { throw '認識タイムアウト' }
 return $answer.Result | ConvertFrom-Json
}
$main=Wait-AppWindow -Title '設定 - LiveTextMarker - ver 1.1.0'
$appRoot=[System.Windows.Automation.AutomationElement]::FromHandle($main); $appId=$appRoot.Current.ProcessId
Save-WindowScreenshot -WindowHandle $main -Path "$PSScriptRoot/境界検証-設定.png" | Out-Null
$combo=$appRoot.FindFirst([System.Windows.Automation.TreeScope]::Descendants,[System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::ControlTypeProperty,[System.Windows.Automation.ControlType]::ComboBox))
$combo.SetFocus(); [System.Windows.Forms.SendKeys]::SendWait($(if($Highlight){'{HOME}{DOWN}'}else{'{HOME}'}))
$start=Find-InWindow -WindowHandle $main -Name '一時停止 / Pause' -TimeoutMs 200
if($start) { $p=Get-ElementCenter -Element $start; Invoke-Click -X $p[0] -Y $p[1] }
$arguments=@('-NoProfile','-STA','-File',"`"$PSScriptRoot/境界テスト画面.ps1`"")
if($Ocr) { $arguments+='-Ocr' }
$fixture=Start-Process -FilePath (Get-Command pwsh).Source -ArgumentList $arguments -WindowStyle Hidden -PassThru
$cover=$null; $worker=$null; $held=$false
$name="境界-$(if($Ocr){'OCR'}else{'UIA'})-$(if($Highlight){'塗り'}else{'下線'})"
try {
 $target=Wait-AppWindow -Title 'LiveTextMarker フレーム境界テスト'
 [WUA.Win]::ShowWindow($target,5) | Out-Null
 if(-not (Save-WindowScreenshot -WindowHandle $target -Path "$PSScriptRoot/$name.png")) { throw '撮影失敗' }
 $root=[System.Windows.Automation.AutomationElement]::FromHandle($target)
 $controls=@($root.FindAll([System.Windows.Automation.TreeScope]::Descendants,[System.Windows.Automation.Condition]::TrueCondition) | Where-Object { $_.Current.BoundingRectangle.Width -gt 100 -and $_.Current.BoundingRectangle.Width -lt $root.Current.BoundingRectangle.Width*0.75 } | Sort-Object { $_.Current.BoundingRectangle.X })
 if($controls.Count -lt 2) { throw '検証用の左右フレームを取得できません' }
 $left=$controls[0]; $right=$controls[-1]
 $leftRect=$left.Current.BoundingRectangle; $rightRect=$right.Current.BoundingRectangle
 $launch=[System.Diagnostics.ProcessStartInfo]::new("$PSScriptRoot/../../app/LiveTextMarker.exe",'--worker')
 $launch.UseShellExecute=$false; $launch.CreateNoWindow=$true; $launch.RedirectStandardInput=$true; $launch.RedirectStandardOutput=$true
 $worker=[System.Diagnostics.Process]::Start($launch)
 foreach($rect in @($leftRect,$rightRect)) {
  $pointX=[int]($rect.X+40); $pointY=[int]($rect.Y+40)
  $detected=Query-Worker @{Kind='uia';X=$pointX;Y=$pointY;TargetWindow=$target.ToInt64()}
  if(-not $detected.Scope -or $detected.Scope.Width -gt $rect.Width+2) { throw "別フレームを含む領域です: $($detected | ConvertTo-Json -Compress -Depth 5) / $rect" }
  if($Ocr) {
   $nativeRect=New-Object WUA.Win+RECT
   [WUA.Win]::DwmGetWindowAttribute($target,9,[ref]$nativeRect,16) | Out-Null
   $source=[System.Drawing.Bitmap]::new("$PSScriptRoot/$name.png")
   try {
    $scope=$detected.Scope
    $cropRect=[System.Drawing.Rectangle]::FromLTRB([int]($scope.X-$nativeRect.Left),[int]($scope.Y-$nativeRect.Top),[int]($scope.Right-$nativeRect.Left),[int]($scope.Bottom-$nativeRect.Top))
    $crop=$source.Clone($cropRect,[System.Drawing.Imaging.PixelFormat]::Format24bppRgb)
    try { $stream=[IO.MemoryStream]::new(); try { $crop.Save($stream,[System.Drawing.Imaging.ImageFormat]::Png); $image=[Convert]::ToBase64String($stream.ToArray()) } finally { $stream.Dispose() } } finally { $crop.Dispose() }
   } finally { $source.Dispose() }
   $detected=Query-Worker @{Kind='ocr';X=$pointX;Y=$pointY;OriginX=[int]$scope.X;OriginY=[int]$scope.Y;Image=$image;Scope=$scope;ScopeId=$detected.ScopeId;TargetWindow=$target.ToInt64()}
  }
  if(-not $detected.Bounds -or $detected.Lines.Count -eq 0) { throw '文字を認識できません' }
  foreach($line in $detected.Lines) { if($line.X -lt $rect.X-1 -or $line.Right -gt $rect.Right+1 -or $line.Y -lt $rect.Y-1 -or $line.Bottom -gt $rect.Bottom+1) { throw '文字範囲が別フレームへ伸びています' } }
  "PASS: $($detected.Source) 左右の別フレームを独立して検出"
 }
 # A separate topmost window covers the right part of the left frame.
 $coverX=[int]($leftRect.X+220); $coverY=[int]($leftRect.Y-20)
 $cover=Start-Process -FilePath (Get-Command pwsh).Source -ArgumentList @('-NoProfile','-STA','-File',"`"$PSScriptRoot/境界テスト画面.ps1`"",'-Cover','-CoverX',$coverX,'-CoverY',$coverY) -WindowStyle Hidden -PassThru
 $coverWindow=Wait-AppWindow -Title 'LiveTextMarker 境界カバー'; [WUA.Win]::ShowWindow($coverWindow,5) | Out-Null
 $coverNative=New-Object WUA.Win+RECT; [WUA.Win]::DwmGetWindowAttribute($coverWindow,9,[ref]$coverNative,16) | Out-Null
 if(-not (Save-WindowScreenshot -WindowHandle $target -Path "$PSScriptRoot/$name.png")) { throw '前景設定失敗' }
 [WUA.Win]::SetCursorPos([int]($leftRect.X+40),[int]($leftRect.Y+40))
 [BoundaryProbe]::keybd_event(0x11,0,0,[UIntPtr]::Zero); $held=$true
 $deadline=(Get-Date).AddSeconds(8)
 while([BoundaryProbe]::Areas($appId).Count -eq 0 -and (Get-Date) -lt $deadline) { Start-Sleep -Milliseconds 20 }
 Start-Sleep -Milliseconds 700
 $areas=[BoundaryProbe]::Areas($appId)
 if($areas.Count -eq 0) { throw '重なりを除いた領域を表示できません' }
 foreach($area in $areas) { if($area.Left -lt $leftRect.X-1 -or $area.Right -gt $coverNative.Left+1) { throw '表示が別ウィンドウを飛び越えています' } }
 'PASS: 別ウィンドウの重なり部分を除外して描画'
 $areas | ConvertTo-Json | Set-Content -LiteralPath "$PSScriptRoot/$name.json" -Encoding utf8
 # Close the cover while stationary: subsequent detection may safely expand again.
 Close-TestWindow $cover; $cover=$null
 Start-Sleep -Milliseconds 1200
 $areas=[BoundaryProbe]::Areas($appId)
 foreach($area in $areas) { if($area.Left -lt $leftRect.X-1 -or $area.Right -gt $leftRect.Right+1) { throw '重なり解除後に別フレームへ伸びています' } }
 'PASS: 重なり解除後もフレーム内に制限'
} finally {
 if($held) { [BoundaryProbe]::keybd_event(0x11,0,2,[UIntPtr]::Zero) }
 if($worker) { if(-not $worker.HasExited) { $worker.Kill() }; $worker.Dispose() }
 Close-TestWindow $cover
 Close-TestWindow $fixture
}
