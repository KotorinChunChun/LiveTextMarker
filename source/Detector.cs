using System.Windows.Automation;
using System.Windows.Automation.Text;
using Tesseract;
namespace PointerUnderline;
internal sealed class Detector : IDisposable
{
 private TesseractEngine? engine;
 public Detection Uia(Request request)
 {
  int x=request.X,y=request.Y;
  Box? scope=null; string? scopeId=null;
  try {
   var element=AutomationElement.FromPoint(new System.Windows.Point(x,y));
   var target=(nint)request.TargetWindow;
   if(target==0) target=Native.GetAncestor(Native.WindowFromPoint(new Native.POINT { X=x,Y=y }),2);
   Native.GetWindowThreadProcessId(target,out var targetPid);
   if(element.Current.ProcessId!=(int)targetPid) return new(Error:"対象ウィンドウが変わりました。");
   var node=element;
   for(int depth=0;node!=null && depth<24;depth++,node=TreeWalker.ControlViewWalker.GetParent(node)) {
    if(node.Current.ProcessId!=(int)targetPid) break;
    if(node.Current.IsPassword) return new(Sensitive:true);
    var type=node.Current.ControlType;
    bool frame=type==ControlType.Document || type==ControlType.Edit || type==ControlType.Pane || type==ControlType.Group || type==ControlType.List || type==ControlType.DataGrid || type==ControlType.Table || type==ControlType.Tree || type==ControlType.Image || type==ControlType.Window;
    var r=node.Current.BoundingRectangle;
    var box=new Box(r.X,r.Y,r.Width,r.Height);
    if(frame && box.Contains(x,y,0)) { scope=box; scopeId=string.Join("-",node.GetRuntimeId()); break; }
   }
   if(scope==null) return new(Error:"対象フレームの領域を取得できません。");
   if(Native.DwmGetWindowAttribute(target,9,out var nativeRect,16)==0) scope=scope.Value.Intersect(new Box(nativeRect.Left,nativeRect.Top,nativeRect.Right-nativeRect.Left,nativeRect.Bottom-nativeRect.Top));
   if(!scope.Value.Contains(x,y,0)) return new(Error:"対象領域の外です。");
   if(request.Kind=="scope") return new(Scope:scope,ScopeId:scopeId);
   for(int depth=0;element!=null && depth<12;depth++,element=TreeWalker.ControlViewWalker.GetParent(element)) {
    if(element.Current.ProcessId!=(int)targetPid) break;
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
    var boxes=Geometry.MergeRows(Geometry.ClipRows(range.GetBoundingRectangles().Select(r=>new Box(r.X,r.Y,r.Width,r.Height)),scope.Value));
    var picked=Geometry.Pick(boxes,x,y);
    if(picked!=null) return new(picked,"UI Automation",Lines:boxes,Scope:scope,ScopeId:scopeId);
    if(string.Join("-",element.GetRuntimeId())==scopeId) break;
   }
   return new(Scope:scope,ScopeId:scopeId);
  } catch(ElementNotAvailableException) { return new(); }
    catch(Exception ex) { return new(Error:"UI Automation: "+ex.GetType().Name,Scope:scope,ScopeId:scopeId); }
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
    var rows=Geometry.MergeRows(req.Scope is {} limit?Geometry.ClipRows(paragraph,limit):paragraph);
    var picked=Geometry.Pick(rows,req.X,req.Y);
    if(picked!=null) return new(picked,"OCR",Lines:rows,Scope:req.Scope,ScopeId:req.ScopeId);
   }
   return new(Source:"OCR",Scope:req.Scope,ScopeId:req.ScopeId);
  } catch(Exception ex) { return new(Error:"OCR: "+ex.GetType().Name+". Check tessdata and Microsoft Visual C++ 2015–2022 x64 runtime."); }
 }
 public void Dispose()=>engine?.Dispose();
}
