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
   Detection? best=null;
   for(int depth=0;element!=null && depth<12;depth++,element=TreeWalker.ControlViewWalker.GetParent(element)) {
    if(element.Current.IsPassword) return new(Sensitive:true);
    if(element.Current.ProcessId==Environment.ProcessId) continue;
    if(!element.TryGetCurrentPattern(TextPattern.Pattern,out var pattern)) continue;
    var range=((TextPattern)pattern).RangeFromPoint(new System.Windows.Point(x,y));
    var line=range.Clone(); line.ExpandToEnclosingUnit(TextUnit.Line);
    range.ExpandToEnclosingUnit(TextUnit.Paragraph);
    var text=range.GetText(16385);
    if(text.Length>16384 || range.GetBoundingRectangles().Length>40) { range=line; text=range.GetText(16385); }
    if(string.IsNullOrWhiteSpace(text)) continue;
    // UIA providers may return CRLF/CRCRLF for one logical character.
    // Trim only when moving the endpoint preserves every non-whitespace character.
    if(text.Length<16385) {
     foreach(var endpoint in new[]{TextPatternRangeEndpoint.End,TextPatternRangeEndpoint.Start}) {
      for(int step=0;step<64 && text.Length>0 && char.IsWhiteSpace(endpoint==TextPatternRangeEndpoint.End?text[^1]:text[0]);step++) {
       var trimmed=range.Clone();
       if(trimmed.MoveEndpointByUnit(endpoint,TextUnit.Character,endpoint==TextPatternRangeEndpoint.End?-1:1)==0) break;
       var nextText=trimmed.GetText(16385);
       if(nextText.Trim()!=text.Trim()) break;
       range=trimmed; text=nextText;
      }
     }
    }
    var boxes=Geometry.MergeRows(range.GetBoundingRectangles().Select(r=>new Box(r.X,r.Y,r.Width,r.Height)));
    var picked=Geometry.Pick(boxes,x,y);
    if(picked!=null && (best==null || boxes.Sum(b=>b.Width)>best.Lines!.Sum(b=>b.Width))) best=new(picked,"UI Automation",Lines:boxes);
   }
   return best ?? new();
  } catch(ElementNotAvailableException) { return new(); }
    catch(Exception ex) { return new(Error:"UI Automation: "+ex.GetType().Name); }
 }
 public Detection Ocr(Request req)
 {
  try {
   engine ??= new TesseractEngine(Path.Combine(AppContext.BaseDirectory,"tessdata"),"jpn+eng",EngineMode.LstmOnly);
   using var pix=Pix.LoadFromMemory(Convert.FromBase64String(req.Image!));
   using var page=engine.Process(pix,PageSegMode.Auto);
   using var iterator=page.GetIterator();
   iterator.Begin(); var paragraphs=new List<List<Box>>(); var boxes=new List<Box>();
   do {
    if(iterator.IsAtBeginningOf(PageIteratorLevel.Para) && boxes.Count>0) { paragraphs.Add(boxes); boxes=new List<Box>(); }
    if(iterator.GetConfidence(PageIteratorLevel.TextLine)<25 || string.IsNullOrWhiteSpace(iterator.GetText(PageIteratorLevel.TextLine))) continue;
    if(iterator.TryGetBoundingBox(PageIteratorLevel.TextLine,out var r)) boxes.Add(new Box(req.OriginX+r.X1,req.OriginY+r.Y1,r.Width,r.Height));
   } while(iterator.Next(PageIteratorLevel.TextLine));
   if(boxes.Count>0) paragraphs.Add(boxes);
   foreach(var paragraph in paragraphs) {
    var rows=Geometry.MergeRows(paragraph);
    var picked=Geometry.Pick(rows,req.X,req.Y);
    if(picked!=null) return new(picked,"OCR",Lines:rows);
   }
   return new(Source:"OCR");
  } catch(Exception ex) { return new(Error:"OCR: "+ex.GetType().Name+". Check tessdata and Microsoft Visual C++ 2015–2022 x64 runtime."); }
 }
 public void Dispose()=>engine?.Dispose();
}
