if(-not ('FixtureWindow' -as [type])) {
 Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class FixtureWindow {
 public delegate bool Callback(IntPtr h,IntPtr data);
 [DllImport("user32.dll")] static extern bool EnumWindows(Callback callback,IntPtr data);
 [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h,out uint pid);
 [DllImport("user32.dll")] static extern bool PostMessage(IntPtr h,uint message,IntPtr w,IntPtr l);
 public static void Close(uint pid) { EnumWindows((h,d)=>{ GetWindowThreadProcessId(h,out var owner); if(owner==pid) PostMessage(h,0x10,IntPtr.Zero,IntPtr.Zero); return true; },IntPtr.Zero); }
}
'@
}
function Close-TestWindow($process) {
 if($process) { $process.Refresh(); if(-not $process.HasExited) { [FixtureWindow]::Close($process.Id); if(-not $process.WaitForExit(5000)) { throw "検証ウィンドウを終了できません: $($process.Id)" } } }
}
