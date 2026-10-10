using System.Runtime.InteropServices;
namespace LiveTextMarker;
internal static class Native
{
 [StructLayout(LayoutKind.Sequential)] public struct POINT { public int X,Y; }
 [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left,Top,Right,Bottom; }
 [DllImport("dwmapi.dll")] public static extern int DwmGetWindowAttribute(nint h,int attribute,out RECT r,int size);
 [DllImport("user32.dll")] public static extern bool GetCursorPos(out POINT p);
 [DllImport("user32.dll")] public static extern short GetAsyncKeyState(int key);
 [DllImport("user32.dll")] public static extern nint WindowFromPoint(POINT p);
 [DllImport("user32.dll")] public static extern nint GetAncestor(nint h,uint flags);
 [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(nint h,out uint id);
 [DllImport("user32.dll")] public static extern nint GetForegroundWindow();
 [DllImport("user32.dll")] public static extern bool RegisterHotKey(nint h,int id,uint modifiers,uint key);
 [DllImport("user32.dll")] public static extern bool UnregisterHotKey(nint h,int id);
 [DllImport("user32.dll")] public static extern bool SetWindowPos(nint h,nint after,int x,int y,int w,int height,uint flags);
 [DllImport("user32.dll")] public static extern bool SetLayeredWindowAttributes(nint h,uint key,byte alpha,uint flags);
 [DllImport("user32.dll")] public static extern bool IsWindowVisible(nint h);
 [DllImport("user32.dll")] public static extern bool SetWindowDisplayAffinity(nint h,uint affinity);
 public delegate bool EnumWindowsCallback(nint h,nint data);
 [DllImport("user32.dll")] public static extern bool EnumWindows(EnumWindowsCallback callback,nint data);
 [DllImport("dwmapi.dll")] public static extern int DwmFlush();
 [DllImport("shcore.dll")] public static extern int GetDpiForMonitor(nint h,int type,out uint x,out uint y);
 [DllImport("user32.dll")] public static extern nint MonitorFromPoint(POINT p,uint flags);
 public static double Scale(POINT p) { try { return GetDpiForMonitor(MonitorFromPoint(p,2),0,out var x,out _) == 0 ? x/96.0 : 1; } catch { return 1; } }
}
