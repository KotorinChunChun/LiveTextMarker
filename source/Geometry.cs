namespace PointerUnderline;
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
 public static Box Underline(Box text, Box screen, double scale, int thickness, int offset) => new Box(text.X,text.Bottom+offset*scale,text.Width,Math.Max(1,thickness*scale)).Intersect(screen);
}
public sealed record Request(string Kind, int X, int Y, string? Image = null, int OriginX = 0, int OriginY = 0);
public sealed record Detection(Box? Bounds = null, string Source = "", string? Error = null, bool Sensitive = false);
public static class DetectionGate
{
 public static bool Accept(Box? bounds, bool enabled, bool closing, int requestGeneration, int currentGeneration, long sourceWindow, long liveWindow, long sourceForeground, long liveForeground, double x, double y, double requestY, double scale) =>
  enabled && !closing && requestGeneration==currentGeneration && sourceWindow==liveWindow && sourceForeground==liveForeground && bounds is {} box && box.Contains(x,y) && Math.Abs(y-requestY)<12*scale;
}
