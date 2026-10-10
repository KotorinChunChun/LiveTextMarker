namespace LiveTextMarker;
internal static class WindowRegion
{
 public static Box? Visible(nint target,Box scope,int x,int y,IEnumerable<nint> ignored) {
  var ignore=ignored.ToHashSet(); var covers=new List<Box>(); bool found=false;
  Native.EnumWindows((h,_)=>{
   if(h==target) { found=true; return false; }
   if(ignore.Contains(h) || !Native.IsWindowVisible(h)) return true;
   if(Native.DwmGetWindowAttribute(h,9,out var r,16)==0) { var box=new Box(r.Left,r.Top,r.Right-r.Left,r.Bottom-r.Top); if(box.Valid) covers.Add(box); }
   return true;
  },0);
  return found?Geometry.VisibleRegion(scope,covers,x,y):null;
 }
}
