param([switch]$Highlight,[switch]$Dark)
$ErrorActionPreference = 'Stop'
. 'C:/Users/kotorichun/.codex/skills/windows-ui-automation/scripts/ui-toolkit.ps1'
. "$PSScriptRoot/検証ウィンドウ共通.ps1"
Add-Type @'
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
public static class PointerSmoke {
 public delegate bool Callback(IntPtr handle,IntPtr data);
 [DllImport("user32.dll")] public static extern void keybd_event(byte key,byte scan,uint flags,UIntPtr extra);
 [DllImport("user32.dll")] static extern bool EnumWindows(Callback callback,IntPtr data);
 [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr handle);
 [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr handle,out uint pid);
 [DllImport("user32.dll")] static extern IntPtr GetWindowLongPtr(IntPtr handle,int index);
 [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr handle,out Rect rect);
 [DllImport("user32.dll")] static extern bool GetLayeredWindowAttributes(IntPtr handle,out uint key,out byte alpha,out uint flags);
 [StructLayout(LayoutKind.Sequential)] public struct Rect { public int Left,Top,Right,Bottom; }
 public static Rect[] Lines(uint pid) {
  var result=new List<Rect>();
  EnumWindows((handle,data)=>{ GetWindowThreadProcessId(handle,out var owner); if(owner==pid && IsWindowVisible(handle) && (GetWindowLongPtr(handle,-20).ToInt64()&0x20)!=0) { GetWindowRect(handle,out var rect); result.Add(rect); } return true; },IntPtr.Zero);
  return result.ToArray();
 }
 public static bool Alpha(uint pid,byte expected) { bool valid=true; EnumWindows((h,d)=>{ GetWindowThreadProcessId(h,out var owner); if(owner==pid && IsWindowVisible(h) && (GetWindowLongPtr(h,-20).ToInt64()&0x20)!=0) { if(!GetLayeredWindowAttributes(h,out var key,out var alpha,out var flags)||alpha!=expected) valid=false; } return true; },IntPtr.Zero); return valid; }
}
'@
$window = Wait-AppWindow -Title 'LiveTextMarker'
$root = [System.Windows.Automation.AutomationElement]::FromHandle($window)
$appId = $root.Current.ProcessId
[WUA.Win]::SetForegroundWindow($window) | Out-Null
Save-WindowScreenshot -WindowHandle $window -Path "$PSScriptRoot/設定画面.png" | Out-Null
$display=$root.FindFirst([System.Windows.Automation.TreeScope]::Descendants,[System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::ControlTypeProperty,[System.Windows.Automation.ControlType]::ComboBox))
$display.SetFocus()
[System.Windows.Forms.SendKeys]::SendWait($(if($Highlight){'{HOME}{DOWN}'}else{'{HOME}'}))
$mode = Find-InWindow -WindowHandle $window -Name 'Ctrl押下で実行'
if($mode.GetCurrentPattern([System.Windows.Automation.TogglePattern]::Pattern).Current.ToggleState -eq [System.Windows.Automation.ToggleState]::Off) {
 $point = Get-ElementCenter -Element $mode
 Invoke-Click -X $point[0] -Y $point[1]
}
$start = Find-InWindow -WindowHandle $window -Name '一時停止 / Pause' -TimeoutMs 200
if($start) { $point=Get-ElementCenter -Element $start; Invoke-Click -X $point[0] -Y $point[1] }
$arguments=@('-NoProfile','-STA','-File',"`"$PSScriptRoot/文章テスト画面.ps1`"")
if($Dark) { $arguments+='-Dark' }
$fixture = Start-Process -FilePath (Get-Command pwsh).Source -ArgumentList $arguments -WindowStyle Hidden -PassThru
$held = $false
try {
 $fixtureWindow = Wait-AppWindow -Title 'PointerUnderline 認識テスト'
 [WUA.Win]::ShowWindow($fixtureWindow,5) | Out-Null
 [WUA.Win]::SetForegroundWindow($fixtureWindow) | Out-Null
 Start-Sleep -Milliseconds 300
 if(-not (Save-WindowScreenshot -WindowHandle $fixtureWindow -Path "$PSScriptRoot/文章認識テスト.png")) { throw '検証画面を最前面にできません' }
 $fixtureRoot = [System.Windows.Automation.AutomationElement]::FromHandle($fixtureWindow)
 $edit = $fixtureRoot.FindFirst([System.Windows.Automation.TreeScope]::Descendants,[System.Windows.Automation.Condition]::TrueCondition)
 $rect = $edit.Current.BoundingRectangle
 [WUA.Win]::SetCursorPos([int]($rect.X+30),[int]($rect.Y+20))
 $cursor=[System.Windows.Forms.Cursor]::Position
 $under=[System.Windows.Automation.AutomationElement]::FromPoint([System.Windows.Point]::new($cursor.X,$cursor.Y))
 if($under.Current.ProcessId -ne $fixtureRoot.Current.ProcessId) { throw "検証文章の上にポインターを配置できません: $($under.Current.Name)" }
 Start-Sleep -Milliseconds 400
 if([PointerSmoke]::Lines($appId).Count -ne 0) { throw 'Ctrlなしで下線が表示されています' }
 [PointerSmoke]::keybd_event(0x11,0,0,[UIntPtr]::Zero); $held=$true
 Start-Sleep -Seconds 2
 $visible = [PointerSmoke]::Lines($appId)
 if($visible.Count -ne 3) { throw "Ctrl押下時の下線数が不正です: $($visible.Count)" }
 if(-not [PointerSmoke]::Alpha($appId,$(if($Highlight){41}else{255}))) { throw '描画の透明度が一致しません' }
 if($Highlight -and ($visible | Where-Object { $_.Bottom-$_.Top -lt 20 })) { throw '塗りの高さが文字範囲を覆っていません' }
  if(-not $Highlight -and @($visible | ForEach-Object { $_.Bottom-$_.Top } | Sort-Object -Unique).Count -ne 1){throw '行ごとの下線の太さが異なります'}
 'PASS: 開始操作なしのCtrl押下で3行を表示、下線の太さが一致'
 $visible | Sort-Object Top | Format-Table -AutoSize
 $captureName=if($Highlight){"塗りハイライト-$(if($Dark){'暗色'}else{'白'})"}else{'文章認識テスト'}
 if(-not (Save-WindowScreenshot -WindowHandle $fixtureWindow -Path "$PSScriptRoot/$captureName.png")) { throw '撮影に失敗しました' }
 [PointerSmoke]::keybd_event(0x11,0,2,[UIntPtr]::Zero); $held=$false
 Start-Sleep -Milliseconds 400
 if([PointerSmoke]::Lines($appId).Count -ne 0) { throw 'Ctrl解除後も下線が表示されています' }
  'PASS: 一時停止中のCtrl解除で消去'
 function Toggle-Continuous {
  [PointerSmoke]::keybd_event(0x11,0,0,[UIntPtr]::Zero)
  [PointerSmoke]::keybd_event(0x12,0,0,[UIntPtr]::Zero)
  [PointerSmoke]::keybd_event(0x55,0,0,[UIntPtr]::Zero)
  [PointerSmoke]::keybd_event(0x55,0,2,[UIntPtr]::Zero)
  [PointerSmoke]::keybd_event(0x12,0,2,[UIntPtr]::Zero)
  [PointerSmoke]::keybd_event(0x11,0,2,[UIntPtr]::Zero)
 }
 Toggle-Continuous
 Start-Sleep -Milliseconds 1200
 if([PointerSmoke]::Lines($appId).Count -ne 3){throw 'Ctrl+Alt+Uで開始後にCtrlなしの表示が継続しません'}
 'PASS: Ctrl+Alt+Uで常時表示を開始し、Ctrl解除後も継続'
 Toggle-Continuous
 Start-Sleep -Milliseconds 400
 if([PointerSmoke]::Lines($appId).Count -ne 0){throw 'Ctrl+Alt+Uで一時停止後に表示が残ります'}
 [PointerSmoke]::keybd_event(0x11,0,0,[UIntPtr]::Zero); $held=$true
 Start-Sleep -Milliseconds 1200
 if([PointerSmoke]::Lines($appId).Count -ne 3){throw '一時停止後のCtrl押下が動きません'}
 'PASS: ショートカットで一時停止した後もCtrl押下で実行'
 [PointerSmoke]::keybd_event(0x11,0,2,[UIntPtr]::Zero); $held=$false
} finally {
 if($held) { [PointerSmoke]::keybd_event(0x11,0,2,[UIntPtr]::Zero) }
 $fixture.Refresh()
 Close-TestWindow $fixture
}
$log = $root.FindFirst([System.Windows.Automation.TreeScope]::Descendants,[System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::ControlTypeProperty,[System.Windows.Automation.ControlType]::List))
if($log.Current.BoundingRectangle.Height -lt 100) { throw 'ログ欄の高さが不足しています' }
$entries = $log.FindAll([System.Windows.Automation.TreeScope]::Children,[System.Windows.Automation.Condition]::TrueCondition)
if($entries.Count -gt 10 -or -not ($entries | Where-Object { $_.Current.Name -match 'UI Automation.*3行' })) { throw '検出ログが正しく表示されていません' }
'PASS: ログ欄の表示面積とUIAの3行検出ログ'
Save-WindowScreenshot -WindowHandle $window -Path "$PSScriptRoot/設定画面.png" | Out-Null
