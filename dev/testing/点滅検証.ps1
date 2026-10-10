param([switch]$Ocr,[switch]$AssertStable,[switch]$Highlight)
$ErrorActionPreference='Stop'
. 'C:/Users/kotorichun/.codex/skills/windows-ui-automation/scripts/ui-toolkit.ps1'
. "$PSScriptRoot/検証ウィンドウ共通.ps1"
Add-Type @'
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
public static class FlickerProbe {
 public delegate bool EnumCallback(IntPtr handle,IntPtr data);
 public delegate void EventCallback(IntPtr hook,uint evt,IntPtr handle,int obj,int child,uint thread,uint time);
 [DllImport("user32.dll")] public static extern void keybd_event(byte key,byte scan,uint flags,UIntPtr extra);
 [DllImport("user32.dll")] static extern bool EnumWindows(EnumCallback callback,IntPtr data);
 [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr handle);
 [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr handle,out uint pid);
 [DllImport("user32.dll")] static extern IntPtr GetWindowLongPtr(IntPtr handle,int index);
 [DllImport("user32.dll")] static extern bool GetWindowDisplayAffinity(IntPtr handle,out uint affinity);
 [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
 [DllImport("user32.dll")] static extern IntPtr SetWinEventHook(uint min,uint max,IntPtr module,EventCallback callback,uint pid,uint thread,uint flags);
 [DllImport("user32.dll")] static extern bool UnhookWinEvent(IntPtr hook);
 static EventCallback callback=OnEvent; static IntPtr hook,expected; static uint app;
 public static int Hides,Shows,ForegroundChanges;
 static bool Overlay(IntPtr h) { GetWindowThreadProcessId(h,out var owner); return owner==app && (GetWindowLongPtr(h,-20).ToInt64()&0x20)!=0; }
 static void OnEvent(IntPtr h,uint evt,IntPtr window,int obj,int child,uint thread,uint time) {
  if(evt==3 && window!=expected) ForegroundChanges++;
  if(obj!=0 || child!=0 || !Overlay(window)) return;
  if(evt==0x8002) Shows++; if(evt==0x8003) Hides++;
 }
 public static void Start(uint pid,IntPtr foreground) { app=pid; expected=foreground; Hides=Shows=ForegroundChanges=0; hook=SetWinEventHook(3,0x8003,IntPtr.Zero,callback,0,0,0); if(hook==IntPtr.Zero) throw new Exception("イベント監視開始失敗"); }
 public static void Stop() { if(hook!=IntPtr.Zero) UnhookWinEvent(hook); hook=IntPtr.Zero; }
 public static IntPtr[] Lines(uint pid) { app=pid; var result=new List<IntPtr>(); EnumWindows((h,d)=>{ if(Overlay(h)&&IsWindowVisible(h)) result.Add(h); return true; },IntPtr.Zero); return result.ToArray(); }
 public static bool CaptureRestored(uint pid) { foreach(var h in Lines(pid)) { if(!GetWindowDisplayAffinity(h,out var affinity)||affinity!=0) return false; } return true; }
}
'@
$main=Wait-AppWindow -Title '設定 - LiveTextMarker - ver 1.1.0'
$appRoot=[System.Windows.Automation.AutomationElement]::FromHandle($main)
$appId=$appRoot.Current.ProcessId
Save-WindowScreenshot -WindowHandle $main -Path "$PSScriptRoot/点滅検証-設定.png" | Out-Null
$display=$appRoot.FindFirst([System.Windows.Automation.TreeScope]::Descendants,[System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::ControlTypeProperty,[System.Windows.Automation.ControlType]::ComboBox))
$display.SetFocus()
[System.Windows.Forms.SendKeys]::SendWait($(if($Highlight){'{HOME}{DOWN}'}else{'{HOME}'}))
$start=Find-InWindow -WindowHandle $main -Name '一時停止 / Pause' -TimeoutMs 200
if($start) { $p=Get-ElementCenter -Element $start; Invoke-Click -X $p[0] -Y $p[1] }
$arguments=@('-NoProfile','-STA','-File',"`"$PSScriptRoot/文章テスト画面.ps1`"")
if($Ocr) { $arguments+='-Ocr' }
$fixture=Start-Process -FilePath (Get-Command pwsh).Source -ArgumentList $arguments -WindowStyle Hidden -PassThru
$held=$false
try {
 $target=Wait-AppWindow -Title 'LiveTextMarker 認識テスト'
 [WUA.Win]::ShowWindow($target,5) | Out-Null
 Save-WindowScreenshot -WindowHandle $target -Path "$PSScriptRoot/点滅検証-対象.png" | Out-Null
 $targetRoot=[System.Windows.Automation.AutomationElement]::FromHandle($target)
 $element=$targetRoot.FindFirst([System.Windows.Automation.TreeScope]::Descendants,[System.Windows.Automation.Condition]::TrueCondition)
 $r=$element.Current.BoundingRectangle
 [WUA.Win]::SetCursorPos([int]($r.X+40),[int]($r.Y+25))
 [FlickerProbe]::keybd_event(0x11,0,0,[UIntPtr]::Zero); $held=$true
 $deadline=(Get-Date).AddSeconds(8)
 while([FlickerProbe]::Lines($appId).Count -eq 0 -and (Get-Date) -lt $deadline) { [System.Windows.Forms.Application]::DoEvents(); Start-Sleep -Milliseconds 20 }
 if([FlickerProbe]::Lines($appId).Count -eq 0) { throw '認識結果が表示されません' }
 Start-Sleep -Seconds 2
 [FlickerProbe]::Start($appId,$target)
 $missing=0; $samples=0; $deadline=(Get-Date).AddSeconds(6)
 while((Get-Date) -lt $deadline) {
  [System.Windows.Forms.Application]::DoEvents()
  if([FlickerProbe]::Lines($appId).Count -eq 0) { $missing++ }
  $samples++; Start-Sleep -Milliseconds 15
 }
 [System.Windows.Forms.Application]::DoEvents(); [FlickerProbe]::Stop()
 $restoreDeadline=(Get-Date).AddMilliseconds(500)
 while(-not [FlickerProbe]::CaptureRestored($appId) -and (Get-Date) -lt $restoreDeadline) { Start-Sleep -Milliseconds 5 }
 $result=[pscustomobject]@{方式=$(if($Ocr){'OCR'}else{'UIA'});非表示イベント=[FlickerProbe]::Hides;表示イベント=[FlickerProbe]::Shows;前景切替=[FlickerProbe]::ForegroundChanges;消失サンプル=$missing;サンプル数=$samples;キャプチャ設定復元=[FlickerProbe]::CaptureRestored($appId)}
 $result | Format-List
 $result | ConvertTo-Json | Set-Content -LiteralPath "$PSScriptRoot/点滅実測-$(if($Ocr){'OCR'}else{'UIA'})-$(if($AssertStable){'修正後'}else{'修正前'})$(if($Highlight){'-塗り'}).json" -Encoding utf8
 if($AssertStable -and ($result.非表示イベント -ne 0 -or $result.前景切替 -ne 0 -or $missing -ne 0 -or -not $result.キャプチャ設定復元)) { throw '点滅または前景変更を検出しました' }
 if($AssertStable) {
  [WUA.Win]::SetCursorPos([int]($r.X+550),[int]($r.Y+120))
  Start-Sleep -Milliseconds 800
  if([FlickerProbe]::Lines($appId).Count -ne 0 -or [FlickerProbe]::GetForegroundWindow() -ne $target) { throw '空白への移動後の非表示か前景保持に失敗しました' }
  [WUA.Win]::SetCursorPos([int]($r.X+40),[int]($r.Y+25))
  $deadline=(Get-Date).AddSeconds(5)
  while([FlickerProbe]::Lines($appId).Count -eq 0 -and (Get-Date) -lt $deadline) { Start-Sleep -Milliseconds 20 }
  if([FlickerProbe]::Lines($appId).Count -eq 0 -or [FlickerProbe]::GetForegroundWindow() -ne $target) { throw '再表示か前景保持に失敗しました' }
  'PASS: 空白で消去、文章で再表示、対象アプリの前景保持'
 }
 [FlickerProbe]::keybd_event(0x11,0,2,[UIntPtr]::Zero); $held=$false
 Start-Sleep -Milliseconds 300
 if([FlickerProbe]::Lines($appId).Count -ne 0) { throw 'Ctrl解除後に下線が残っています' }
 'PASS: Ctrl解除時に下線消去'
} finally {
 [FlickerProbe]::Stop()
 if($held) { [FlickerProbe]::keybd_event(0x11,0,2,[UIntPtr]::Zero) }
 Close-TestWindow $fixture
}
