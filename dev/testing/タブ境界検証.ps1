param([switch]$Live)
$ErrorActionPreference='Stop'
. 'C:/Users/kotorichun/.codex/skills/windows-ui-automation/scripts/ui-toolkit.ps1'
. "$PSScriptRoot/検証ウィンドウ共通.ps1"
Add-Type @'
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
public static class TabProbe {
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
$fixture=Start-Process (Get-Command pwsh).Source -ArgumentList @('-NoProfile','-STA','-File',"`"$PSScriptRoot/タブテスト画面.ps1`"") -WindowStyle Hidden -PassThru
$worker=$null; $held=$false
try {
 $target=Wait-AppWindow -Title 'LiveTextMarker タブ境界テスト'; [WUA.Win]::ShowWindow($target,5) | Out-Null
 if(-not (Save-WindowScreenshot -WindowHandle $target -Path "$PSScriptRoot/タブ境界.png")){throw '撮影失敗'}
 $root=[System.Windows.Automation.AutomationElement]::FromHandle($target)
 $tabs=$root.FindAll([System.Windows.Automation.TreeScope]::Descendants,[System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::ControlTypeProperty,[System.Windows.Automation.ControlType]::TabItem))
 if($tabs.Count -ne 7){throw 'タブ構成の取得失敗'}
 $si=[Diagnostics.ProcessStartInfo]::new("$PSScriptRoot/../../source/out/LiveTextMarker/LiveTextMarker.exe",'--worker'); $si.UseShellExecute=$false; $si.CreateNoWindow=$true; $si.RedirectStandardInput=$true; $si.RedirectStandardOutput=$true; $worker=[Diagnostics.Process]::Start($si)
 foreach($tab in $tabs) {
  $r=$tab.Current.BoundingRectangle; $x=[int]($r.X+$r.Width/2); $y=[int]($r.Y+$r.Height/2)
  $worker.StandardInput.WriteLine((@{Kind='uia';X=$x;Y=$y;TargetWindow=$target.ToInt64()} | ConvertTo-Json -Compress))
  $task=$worker.StandardOutput.ReadLineAsync(); if(-not $task.Wait(5000)){throw 'timeout'}; $result=$task.Result | ConvertFrom-Json
  if(-not $result.Scope -or $result.Scope.X -lt $r.X-1 -or $result.Scope.Right -gt $r.Right+1 -or $result.Scope.Y -lt $r.Y-1 -or $result.Scope.Bottom -gt $r.Bottom+1){throw 'タブ見出し外の領域を検出'}
 }
 'PASS: 7個のTabItemを個別の認識境界として取得'
 if($Live) {
  $main=Wait-AppWindow -Title '設定 - LiveTextMarker - ver 1.1.0'
  [WUA.Win]::ShowWindow($main,5) | Out-Null
  if(-not (Save-WindowScreenshot -WindowHandle $main -Path "$PSScriptRoot/タブ検証-設定.png")){throw '設定画面の準備失敗'}
  $appRoot=[System.Windows.Automation.AutomationElement]::FromHandle($main); $appId=$appRoot.Current.ProcessId
  $combo=$appRoot.FindFirst([System.Windows.Automation.TreeScope]::Descendants,[System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::ControlTypeProperty,[System.Windows.Automation.ControlType]::ComboBox))
  $drawn=0
  foreach($mode in @('下線','塗り')) {
   $combo.SetFocus(); [System.Windows.Forms.SendKeys]::SendWait($(if($mode -eq '塗り'){'{HOME}{DOWN}'}else{'{HOME}'}))
   $start=Find-InWindow -WindowHandle $main -Name '一時停止 / Pause' -TimeoutMs 200
   if($start){$p=Get-ElementCenter -Element $start; Invoke-Click -X $p[0] -Y $p[1]}
   [WUA.Win]::SetForegroundWindow($target) | Out-Null
   foreach($tab in $tabs) {
    $r=$tab.Current.BoundingRectangle; [WUA.Win]::SetCursorPos([int]($r.X+$r.Width/2),[int]($r.Y+$r.Height/2))
    [TabProbe]::keybd_event(0x11,0,0,[UIntPtr]::Zero); $held=$true
    Start-Sleep -Milliseconds 1100
    $areas=[TabProbe]::Areas($appId)
    foreach($area in $areas){if($area.Left -lt $r.X-1 -or $area.Right -gt $r.Right+1 -or $area.Top -lt $r.Y-1 -or $area.Bottom -gt $r.Bottom+1){throw '描画が隣のタブへ伸びています'}}
    if($areas.Count -gt 0){$drawn++}
    [TabProbe]::keybd_event(0x11,0,2,[UIntPtr]::Zero); $held=$false; Start-Sleep -Milliseconds 100
   }
   "PASS: $mode の7タブで境界外の描画なし"
  }
  if($drawn -eq 0){throw 'タブの文字を表示できませんでした'}
  "表示できた組合せ: $drawn / 14"
  $combo.SetFocus(); [System.Windows.Forms.SendKeys]::SendWait('{HOME}')
 }
} finally {
 if($held){[TabProbe]::keybd_event(0x11,0,2,[UIntPtr]::Zero)}
 if($worker){$worker.Kill();$worker.Dispose()}
 Close-TestWindow $fixture
}
