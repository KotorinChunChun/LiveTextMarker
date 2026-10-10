$ErrorActionPreference = 'Stop'
. 'C:/Users/kotorichun/.codex/skills/windows-ui-automation/scripts/ui-toolkit.ps1'
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
 [StructLayout(LayoutKind.Sequential)] public struct Rect { public int Left,Top,Right,Bottom; }
 public static Rect[] Lines(uint pid) {
  var result=new List<Rect>();
  EnumWindows((handle,data)=>{ GetWindowThreadProcessId(handle,out var owner); if(owner==pid && IsWindowVisible(handle) && (GetWindowLongPtr(handle,-20).ToInt64()&0x20)!=0) { GetWindowRect(handle,out var rect); result.Add(rect); } return true; },IntPtr.Zero);
  return result.ToArray();
 }
}
'@
$window = Wait-AppWindow -Title 'Pointer Underline'
$root = [System.Windows.Automation.AutomationElement]::FromHandle($window)
$appId = $root.Current.ProcessId
[WUA.Win]::SetForegroundWindow($window) | Out-Null
$mode = Find-InWindow -WindowHandle $window -Name 'Ctrlを押している間だけ'
if($mode.GetCurrentPattern([System.Windows.Automation.TogglePattern]::Pattern).Current.ToggleState -eq [System.Windows.Automation.ToggleState]::Off) {
 $point = Get-ElementCenter -Element $mode
 Invoke-Click -X $point[0] -Y $point[1]
}
$start = Find-InWindow -WindowHandle $window -Name '開始 / Start' -TimeoutMs 200
if($start) { $point=Get-ElementCenter -Element $start; Invoke-Click -X $point[0] -Y $point[1] }
$fixture = Start-Process -FilePath (Get-Command pwsh).Source -ArgumentList @('-NoProfile','-STA','-File',"`"$PSScriptRoot/文章テスト画面.ps1`"") -WindowStyle Hidden -PassThru
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
 'PASS: Ctrl押下中に3行を表示'
 $visible | Sort-Object Top | Format-Table -AutoSize
 if(-not (Save-WindowScreenshot -WindowHandle $fixtureWindow -Path "$PSScriptRoot/文章認識テスト.png")) { throw '撮影に失敗しました' }
 [PointerSmoke]::keybd_event(0x11,0,2,[UIntPtr]::Zero); $held=$false
 Start-Sleep -Milliseconds 400
 if([PointerSmoke]::Lines($appId).Count -ne 0) { throw 'Ctrl解除後も下線が表示されています' }
 'PASS: Ctrl解除で下線消去'
} finally {
 if($held) { [PointerSmoke]::keybd_event(0x11,0,2,[UIntPtr]::Zero) }
 $fixture.Refresh()
 if(-not $fixture.HasExited) { $fixture.CloseMainWindow() | Out-Null; $fixture.WaitForExit(3000) | Out-Null }
}
$log = $root.FindFirst([System.Windows.Automation.TreeScope]::Descendants,[System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::ControlTypeProperty,[System.Windows.Automation.ControlType]::List))
if($log.Current.BoundingRectangle.Height -lt 100) { throw 'ログ欄の高さが不足しています' }
$entries = $log.FindAll([System.Windows.Automation.TreeScope]::Children,[System.Windows.Automation.Condition]::TrueCondition)
if($entries.Count -gt 10 -or -not ($entries | Where-Object { $_.Current.Name -match 'UI Automation.*3行' })) { throw '検出ログが正しく表示されていません' }
'PASS: ログ欄の表示面積とUIAの3行検出ログ'
Save-WindowScreenshot -WindowHandle $window -Path "$PSScriptRoot/設定画面.png" | Out-Null
