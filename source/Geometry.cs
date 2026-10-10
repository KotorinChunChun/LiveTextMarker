namespace LiveTextMarker;
public readonly record struct Box(double X, double Y, double Width, double Height)
{
 public double Right => X + Width;
 public double Bottom => Y + Height;
 public bool Valid => double.IsFinite(X) && double.IsFinite(Y) && double.IsFinite(Width) && double.IsFinite(Height) && Width >= 2 && Height >= 3 && Width < 20000 && Height < 1000;
 public bool Contains(double x, double y, double tolerance = 2) => Valid && x >= X-tolerance && x <= Right+tolerance && y >= Y-tolerance && y <= Bottom+tolerance;
 public Box Intersect(Box b) => new(Math.Max(X,b.X), Math.Max(Y,b.Y), Math.Min(Right,b.Right)-Math.Max(X,b.X), Math.Min(Bottom,b.Bottom)-Math.Max(Y,b.Y));
}
public static class Geometry
{
 public static Box? Pick(IEnumerable<Box> boxes, double x, double y) => boxes.Where(b=>b.Contains(x,y)).OrderBy(b=>Math.Abs((b.Y+b.Height/2)-y)).ThenBy(b=>b.Width).Cast<Box?>().FirstOrDefault();
 public static Box[] MergeRows(IEnumerable<Box> boxes)
 {
  var rows=new List<Box>();
  foreach(var box in boxes.Where(b=>b.Valid).OrderBy(b=>b.Y).ThenBy(b=>b.X)) {
   var index=rows.FindIndex(row=>Math.Abs(row.Y+row.Height/2-box.Y-box.Height/2)<=Math.Min(row.Height,box.Height)*0.4 && box.X<=row.Right+Math.Max(24,Math.Max(row.Height,box.Height)*2) && box.Right>=row.X-24);
   if(index<0) rows.Add(box);
   else { var row=rows[index]; rows[index]=new Box(Math.Min(row.X,box.X),Math.Min(row.Y,box.Y),Math.Max(row.Right,box.Right)-Math.Min(row.X,box.X),Math.Max(row.Bottom,box.Bottom)-Math.Min(row.Y,box.Y)); }
  }
  return rows.OrderBy(b=>b.Y).ThenBy(b=>b.X).ToArray();
 }
 public static Box Underline(Box text, Box screen, double scale, int thickness, double offset) {
  var height=Math.Max(1,Math.Round(thickness*scale,MidpointRounding.AwayFromZero));
  var top=Math.Ceiling(screen.Y); var bottom=Math.Floor(screen.Bottom);
  if(bottom-top<height) return default;
  var y=Math.Clamp(Math.Round(text.Bottom+offset*scale,MidpointRounding.AwayFromZero),top,bottom-height);
  return new Box(text.X,y,text.Width,height).Intersect(screen);
 }
 public static Box Highlight(Box text,Box screen)=>text.Intersect(screen);
 public static byte HighlightAlpha(int percent)=>(byte)Math.Round(Math.Clamp(percent,8,30)*255/100.0,MidpointRounding.AwayFromZero);
 public static Box[] ClipRows(IEnumerable<Box> rows,Box scope)=>rows.Select(b=>b.Intersect(scope)).Where(b=>b.Valid).ToArray();
 public static Box? VisibleRegion(Box scope,IEnumerable<Box> occluders,double x,double y) {
  if(!scope.Contains(x,y,0)) return null;
  foreach(var other in occluders) {
   var overlap=scope.Intersect(other);
   if(overlap.Width<=0 || overlap.Height<=0) continue;
   if(overlap.Contains(x,y,0)) return null;
   var candidates=new[]{new Box(scope.X,scope.Y,overlap.X-scope.X,scope.Height),new Box(overlap.Right,scope.Y,scope.Right-overlap.Right,scope.Height),new Box(scope.X,scope.Y,scope.Width,overlap.Y-scope.Y),new Box(scope.X,overlap.Bottom,scope.Width,scope.Bottom-overlap.Bottom)};
   var visible=candidates.Where(b=>b.Contains(x,y,0)).OrderByDescending(b=>b.Width*b.Height).Cast<Box?>().FirstOrDefault();
   if(visible==null) return null;
   scope=visible.Value;
  }
  return scope;
 }
}
public sealed record Request(string Kind, int X, int Y, string? Image = null, int OriginX = 0, int OriginY = 0, long TargetWindow=0, Box? Scope=null, string? ScopeId=null);
public sealed record Detection(Box? Bounds = null, string Source = "", string? Error = null, bool Sensitive = false, Box[]? Lines = null, Box? Scope=null, string? ScopeId=null);
public static class DetectionGate
{
 public static bool Accept(Box? bounds, bool enabled, bool closing, int requestGeneration, int currentGeneration, long sourceWindow, long liveWindow, long sourceForeground, long liveForeground, double x, double y, double requestY, double scale) =>
  enabled && !closing && requestGeneration==currentGeneration && sourceWindow==liveWindow && sourceForeground==liveForeground && bounds is {} box && box.Contains(x,y) && Math.Abs(y-requestY)<12*scale;
}
public static class ActivationGate
{
 public static bool Active(bool enabled,bool ctrlOnly,bool ctrlDown)=>enabled || (ctrlOnly && ctrlDown);
}
