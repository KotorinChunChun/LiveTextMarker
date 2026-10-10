using System.Windows.Automation;
using System.Windows.Automation.Text;
using Tesseract;
namespace PointerUnderline;
internal sealed class Detector : IDisposable
{
 private TesseractEngine? engine;
 public Detection Uia(int x,int y)
 {
  try {
   var element=AutomationElement.FromPoint(new System.Windows.Point(x,y));
   for(int depth=0;element!=null && depth<12;depth++,element=TreeWalker.ControlViewWalker.GetParent(element)) {
    if(element.Current.IsPassword) return new(Sensitive:true);
    if(element.Current.ProcessId==Environment.ProcessId) continue;
    if(!element.TryGetCurrentPattern(TextPattern.Pattern,out var pattern)) continue;
    var range=((TextPattern)pattern).RangeFromPoint(new System.Windows.Point(x,y));
    range.ExpandToEnclosingUnit(TextUnit.Line);
    var text=range.GetText(4096);
    if(string.IsNullOrWhiteSpace(text)) continue;
    // Drop indentation/newline/trailing spaces when provider exposes character endpoints.
    if(text.Length<4096) {
     int leading=text.Length-text.TrimStart().Length;
     int trailing=text.Length-text.TrimEnd().Length;
     if(trailing>0) range.MoveEndpointByUnit(TextPatternRangeEndpoint.End,TextUnit.Character,-trailing);
     if(leading>0) range.MoveEndpointByUnit(TextPatternRangeEndpoint.Start,TextUnit.Character,leading);
    }
    var boxes=range.GetBoundingRectangles().Select(r=>new Box(r.X,r.Y,r.Width,r.Height));
    var picked=Geometry.Pick(boxes,x,y);
    if(picked!=null) return new(picked,"UI Automation");
   }
   return new();
  } catch(ElementNotAvailableException) { return new(); }
    catch(Exception ex) { return new(Error:"UI Automation: "+ex.GetType().Name); }
 }
 public Detection Ocr(Request req)
 {
  try {
   engine ??= new TesseractEngine(Path.Combine(AppContext.BaseDirectory,"tessdata"),"jpn+eng",EngineMode.LstmOnly);
   using var pix=Pix.LoadFromMemory(Convert.FromBase64String(req.Image!));
   using var page=engine.Process(pix,PageSegMode.SparseText);
   using var iterator=page.GetIterator();
   iterator.Begin(); var boxes=new List<Box>();
   do {
    if(iterator.GetConfidence(PageIteratorLevel.TextLine)<25 || string.IsNullOrWhiteSpace(iterator.GetText(PageIteratorLevel.TextLine))) continue;
    if(iterator.TryGetBoundingBox(PageIteratorLevel.TextLine,out var r)) boxes.Add(new Box(req.OriginX+r.X1,req.OriginY+r.Y1,r.Width,r.Height));
   } while(iterator.Next(PageIteratorLevel.TextLine));
   return new(Geometry.Pick(boxes,req.X,req.Y),"OCR");
  } catch(Exception ex) { return new(Error:"OCR: "+ex.GetType().Name+". Check tessdata and Microsoft Visual C++ 2015–2022 x64 runtime."); }
 }
 public void Dispose()=>engine?.Dispose();
}
