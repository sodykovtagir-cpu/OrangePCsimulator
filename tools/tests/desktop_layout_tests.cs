using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using PC.Component;
using PC.Component.Software;
using OS = PC.Component.Software.OS.OperatingSystem;

internal static class DesktopLayoutTests
{
    private static int cases;
    private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private static T Get<T>(object obj,string name)=>(T)obj.GetType().GetField(name,Flags).GetValue(obj);
    private static void Set(object obj,string name,object value)=>obj.GetType().GetField(name,Flags).SetValue(obj,value);
    private static object Call(object obj,string name,params object[] args)=>obj.GetType().GetMethod(name,Flags).Invoke(obj,args);
    private static void Assert(bool ok,string message) {if(!ok)throw new Exception(message);}
    private static void Equal(Vector2 expected,Vector2 actual,string message)
    {Assert(Vector2.Distance(expected,actual)<0.002f,message+": expected "+expected+", got "+actual);}
    private static void Run(string name,Action test) {test();cases++;Console.WriteLine("PASS: "+name);}

    private sealed class Fixture
    {
        public OS os;
        public Canvas canvas;
        public CanvasScaler scaler;
        public RectTransform viewport,desktop;
        public readonly List<File> files=new List<File>();
        public string Prefix=>os.SystemId.ToString("X8");
        public string PositionsKey=>"icon_positions_"+Prefix+"_shared_v2";
        public string SortKey=>"icon_sort_mode_"+Prefix+"_shared_v2";
        public Fixture(bool reset=true,int boardId=0x1234,int monitorId=1)
        {
            if(reset)PlayerPrefs.Clear();
            Screen.width=1920;Screen.height=1080;Screen.dpi=96;Input.held=false;Time.unscaledTime=0;
            var root=new GameObject();
            canvas=root.AddComponent<Canvas>();canvas.renderMode=RenderMode.WorldSpace;
            scaler=root.AddComponent<CanvasScaler>();
            os=root.AddComponent<OS>();
            os.Board=new Motherboard{Id=boardId,monitor=new PC.Component.Display{Id=monitorId}};
            os.AllStorage=new List<Storage>{new Storage{files=files}};
            os.FileManager=new PC.Component.Software.OS.FileManager(os);
            var desk=new GameObject();desk.transform.parent=root.transform;desktop=desk.transform;
            var group=desk.AddComponent<CanvasGroup>();
            var icons=new GameObject();icons.transform.parent=desk.transform;viewport=icons.transform;
            icons.AddComponent<RectMask2D>().padding=new Vector4(0,40,0,0);
            var prefab=new GameObject();var icon=prefab.AddComponent<FileIcon>();prefab.AddComponent<DesktopIconDragger>();
            Set(os,"desktop",group);Set(os,"iconParent",viewport);Set(os,"fileIconPrefab",icon);
        }
        public void Add(int count){for(int i=0;i<count;i++)files.Add(new File("file"+i.ToString("D3")+".txt","",false,i));}
        public void Load(int count=0){Add(count);os.RefreshDesktopIcon();}
        public Dictionary<string,FileIcon> Icons=>Get<Dictionary<string,FileIcon>>(os,"fileIcons");
        public Vector2 Position(string name)=>Icons[name].GetPosition();
        public Dictionary<string,Vector2> Snapshot()
        {var result=new Dictionary<string,Vector2>();foreach(var pair in Icons)result[pair.Key]=pair.Value.GetPosition();return result;}
        public void Same(Dictionary<string,Vector2> expected)
        {Assert(Icons.Count==expected.Count,"icon count changed");foreach(var pair in expected)Equal(pair.Value,Position(pair.Key),pair.Key+" moved");}
        public void Frame(RenderMode mode,float width,float height)
        {
            canvas.renderMode=mode;viewport.rect=new Rect(width,height);desktop.rect=new Rect(width,height);
            Call(os,"Update");
            foreach(var icon in new List<FileIcon>(Icons.Values))Call(icon.GetComponent<DesktopIconDragger>(),"Update");
            Time.unscaledTime+=0.3f;
            Call(os,"LateUpdate");
        }
        public void Tick(){Time.unscaledTime+=0.3f;Call(os,"LateUpdate");}
        public void Drop(string name,Vector2 position)
        {
            var icon=Icons[name];icon.SetPosition(position);
            Call(icon.GetComponent<DesktopIconDragger>(),"OnDragEnd");
        }
    }

    public static int Main()
    {
        try
        {
            Run("fresh desktop starts in cell (0,0), with no phantom occupied cell",()=>{
                var f=new Fixture();f.Load(1);Equal(new Vector2(55,-55),f.Position("file000.txt"),"first icon");
            });
            Run("default grid comes from fullscreen width, not a tiny physical monitor",()=>{
                var f=new Fixture();f.viewport.rect=new Rect(240,160);f.desktop.rect=new Rect(240,160);f.Load(20);
                Assert(Get<int>(f.os,"iconGridColumns")==9,"columns were fitted to physical monitor");
                Equal(new Vector2(775,-55),f.Position("file008.txt"),"last column");
                Equal(new Vector2(55,-145),f.Position("file009.txt"),"next row");
            });
            Run("100 focus switches preserve monitor coordinates and never persist fullscreen fitting",()=>{
                var f=new Fixture();f.Load(100);var before=f.Snapshot();int writes=PlayerPrefs.Writes;
                for(int i=0;i<100;i++){
                    f.Frame(RenderMode.WorldSpace,320,180);f.Same(before);
                    f.Frame(RenderMode.ScreenSpaceOverlay,1600,900);
                    f.Frame(RenderMode.ScreenSpaceCamera,888.889f,500);f.Same(before);
                }
                Assert(PlayerPrefs.Writes==writes,"mode switch persisted/reflowed layout");
            });
            Run("more icons than fit a monitor overflow without stacking or moving earlier icons",()=>{
                var f=new Fixture();f.Load(300);var cells=new HashSet<Vector2Int>();var grid=DesktopIconGrid.Default;
                foreach(var position in f.Snapshot().Values)Assert(cells.Add(grid.GetCell(position)),"duplicate cell");
                Assert(f.Position("file299.txt").y < -500,"icons did not overflow");
                Equal(new Vector2(55,-55),f.Position("file000.txt"),"first icon moved");
            });
            Run("refresh and new files do not resnap a manually placed or clipped icon",()=>{
                var f=new Fixture();f.Load(12);f.Drop("file000.txt",new Vector2(956,-326));
                Equal(new Vector2(955,-325),f.Position("file000.txt"),"manual position");var old=f.Snapshot();
                f.files.Add(new File("aaa-new.pic"));f.os.RefreshDesktopIcon();
                foreach(var pair in old)Equal(pair.Value,f.Position(pair.Key),"existing icon changed on refresh");
                Assert(f.Position("aaa-new.pic")!=f.Position("file000.txt"),"new icon collided");
            });
            Run("a restored cell is reserved even when its icon is created after a new file",()=>{
                var f=new Fixture();PlayerPrefs.SetString(f.PositionsKey,"Zulu.txt,55,-55");
                f.files.Add(new File("Able.txt"));f.files.Add(new File("Zulu.txt"));f.Load();
                Equal(new Vector2(55,-55),f.Position("Zulu.txt"),"restored icon moved");
                Equal(new Vector2(145,-55),f.Position("Able.txt"),"new icon stole restored cell");
            });
            Run("manual drag is shared, with offscreen positions retained after reconnect/reboot",()=>{
                var f=new Fixture();f.Load(10);f.Drop("file003.txt",new Vector2(1675,-1405));var saved=f.Snapshot();
                var g=new Fixture(false,0x1234,99);g.viewport.rect=new Rect(400,250);g.Load(10);g.Same(saved);
                Assert(g.Position("file003.txt").x>400 && g.Position("file003.txt").y < -250,"saved icon was clamped");
            });
            Run("same logical position can be clipped on a monitor and visible fullscreen",()=>{
                var f=new Fixture();f.Load(1);f.Drop("file000.txt",new Vector2(955,-325));var position=f.Position("file000.txt");
                Assert(position.x-35>840,"test icon should be outside 840-wide monitor");
                Assert(position.x+35<1280 && -position.y+35<720-40,"test icon should fit fullscreen");
                f.Frame(RenderMode.WorldSpace,840,500);Equal(position,f.Position("file000.txt"),"cropped icon moved");
                f.Frame(RenderMode.ScreenSpaceOverlay,1280,720);Equal(position,f.Position("file000.txt"),"visible icon moved");
            });
            Run("separate computers keep separate shared layouts",()=>{
                var f=new Fixture();f.Load(1);f.Drop("file000.txt",new Vector2(955,-685));
                var g=new Fixture(false,0x4567,1);g.Load(1);Equal(new Vector2(55,-55),g.Position("file000.txt"),"another computer inherited layout");
            });
            Run("explicit Auto Arrange updates both views but resizing alone never does",()=>{
                var f=new Fixture();f.Load(30);f.Drop("file000.txt",new Vector2(2305,-2305));
                Screen.width=3840;f.Frame(RenderMode.WorldSpace,240,160);
                Assert(Get<int>(f.os,"iconGridColumns")==9,"resize changed stored columns");
                f.os.AutoArrangeIcons();Assert(Get<int>(f.os,"iconGridColumns")>9,"manual arrangement did not use fullscreen width");
                Equal(new Vector2(55,-55),f.Position("file000.txt"),"manual arrange failed");
                var expected=f.Snapshot();f.Frame(RenderMode.ScreenSpaceOverlay,1777.778f,500);f.Same(expected);
                f.Frame(RenderMode.WorldSpace,240,160);f.Same(expected);
            });
            Run("manual sort order and positions survive mode changes and reload",()=>{
                var f=new Fixture();f.Load(5);f.os.SortDesktopIcons("Size");
                Equal(new Vector2(55,-55),f.Position("file004.txt"),"largest should be first");
                Assert(PlayerPrefs.GetString(f.SortKey)=="Size","sort setting not shared");var expected=f.Snapshot();
                f.Frame(RenderMode.WorldSpace,320,180);f.Same(expected);
                var g=new Fixture(false);g.Load(5);g.Same(expected);Assert(Get<string>(g.os,"currentSortMode")=="Size","sort lost");
            });
            Run("renaming a desktop file preserves its manually chosen coordinates",()=>{
                var f=new Fixture();f.Load(2);f.Drop("file000.txt",new Vector2(955,-865));var pos=f.Position("file000.txt");
                f.files[0].path="renamed.txt";f.os.RefreshDesktopIcon();Equal(pos,f.Position("renamed.txt"),"rename moved icon");
            });
            Run("legacy fullscreen layout is preferred, translated once, with original keys retained",()=>{
                var f=new Fixture();string system="icon_positions_"+f.Prefix+"_system";
                string monitor="icon_positions_"+f.Prefix+"_monitor_00000001";
                float width=1920f/(1080f/500f);var old=new Vector2(55-width/2,195);
                string payload="file000.txt,"+old.x.ToString("R",CultureInfo.InvariantCulture)+",195";
                PlayerPrefs.SetString(system,payload);PlayerPrefs.SetString(monitor,"file000.txt,-185,105");
                PlayerPrefs.SetString("icon_sort_mode_"+f.Prefix+"_system","Type");f.Load(1);
                Equal(new Vector2(55,-55),f.Position("file000.txt"),"wrong legacy source/coordinate origin");
                Assert(PlayerPrefs.GetString(system)==payload,"legacy source was overwritten");
                Assert(PlayerPrefs.GetString(monitor)=="file000.txt,-185,105","monitor backup overwritten");
                Assert(PlayerPrefs.GetString(f.SortKey)=="Type","legacy sort not migrated");
                var g=new Fixture(false);Screen.width=2560;g.Load(1);Equal(new Vector2(55,-55),g.Position("file000.txt"),"migrated twice");
            });
            foreach(string context in new[]{"monitor_00000001","monitor"})
                Run("legacy "+context+" fallback uses monitor coordinates",()=>{
                    var f=new Fixture();PlayerPrefs.SetString("icon_positions_"+f.Prefix+"_"+context,"file000.txt,-365,195");
                    f.Load(1);Equal(new Vector2(55,-55),f.Position("file000.txt"),"monitor migration");
                });
            Run("monitor legacy fallback still works if boot finishes while zoomed in",()=>{
                var f=new Fixture();f.canvas.renderMode=RenderMode.ScreenSpaceOverlay;f.viewport.rect=new Rect(1500,900);
                f.desktop.rect=new Rect(1500,900);PlayerPrefs.SetString("icon_positions_"+f.Prefix+"_monitor_00000001","file000.txt,-365,195");
                f.Load(1);Equal(new Vector2(55,-55),f.Position("file000.txt"),"used zoomed size for monitor history");
            });
            Run("an explicitly empty shared layout does not resurrect old monitor/fullscreen data",()=>{
                var f=new Fixture();PlayerPrefs.SetString(f.PositionsKey,"");
                PlayerPrefs.SetString("icon_positions_"+f.Prefix+"_system","file000.txt,300,150");f.Load(1);
                Equal(new Vector2(55,-55),f.Position("file000.txt"),"old layout resurrected");
            });
            Run("old unsuffixed layouts are migrated when newer legacy profiles are absent",()=>{
                var f=new Fixture();float oldX=55-1920f/(1080f/500f)/2;
                PlayerPrefs.SetString("icon_positions_"+f.Prefix,"file000.txt,"+oldX.ToString("R",CultureInfo.InvariantCulture)+",195");
                f.Load(1);Equal(new Vector2(55,-55),f.Position("file000.txt"),"unsuffixed migration");
            });
            Run("an invalid fullscreen legacy profile does not block a valid monitor fallback",()=>{
                var f=new Fixture();PlayerPrefs.SetString("icon_positions_"+f.Prefix+"_system","file000.txt,NaN,0");
                PlayerPrefs.SetString("icon_positions_"+f.Prefix+"_monitor_00000001","file000.txt,-365,195");
                f.Load(1);Equal(new Vector2(55,-55),f.Position("file000.txt"),"fallback blocked");
            });
            Run("saved wide-screen columns are not recalculated on a narrower-screen reload",()=>{
                var f=new Fixture();Screen.width=3840;f.Load(30);int columns=Get<int>(f.os,"iconGridColumns");var saved=f.Snapshot();
                Assert(columns>9,"wide setup failed");var g=new Fixture(false);g.Load(30);g.Same(saved);
                Assert(Get<int>(g.os,"iconGridColumns")==columns,"reload refitted grid");
            });
            Run("a click without dragging does not snap or persist an existing non-grid position",()=>{
                var f=new Fixture();PlayerPrefs.SetString(f.PositionsKey,"file000.txt,123.5,-222.75");f.Load(1);
                int writes=PlayerPrefs.Writes;var drag=f.Icons["file000.txt"].GetComponent<DesktopIconDragger>();
                drag.OnPointerDown(new PointerEventData{position=new Vector2(123.5f,-222.75f)});Call(drag,"Update");
                Equal(new Vector2(123.5f,-222.75f),f.Position("file000.txt"),"click snapped icon");
                Assert(PlayerPrefs.Writes==writes,"click changed saved layout");
            });
            Run("disabling a dragged icon clears the shared drag lock",()=>{
                var f=new Fixture();f.Load(1);var drag=f.Icons["file000.txt"].GetComponent<DesktopIconDragger>();
                drag.OnPointerDown(new PointerEventData{position=new Vector2(55,-55)});Input.held=true;
                Input.mousePosition=new Vector3(100,-100);Call(drag,"Update");Assert(DesktopIconDragger.IsDragging,"drag did not begin");
                Call(drag,"OnDisable");Assert(!DesktopIconDragger.IsDragging,"drag lock survived disable");Input.held=false;
            });
            Run("coordinate persistence is culture-independent and handles names containing commas",()=>{
                var oldCulture=CultureInfo.CurrentCulture;
                try{
                    CultureInfo.CurrentCulture=new CultureInfo("ru-RU");var f=new Fixture();f.files.Add(new File("рисунок, версия 2.pic"));f.Load();
                    f.Drop("рисунок, версия 2.pic",new Vector2(145,-865));var position=f.Position("рисунок, версия 2.pic");
                    var g=new Fixture(false);g.files.Add(new File("рисунок, версия 2.pic"));g.Load();Equal(position,g.Position("рисунок, версия 2.pic"),"round trip");
                }finally{CultureInfo.CurrentCulture=oldCulture;}
            });
            Run("invalid legacy/persisted coordinates are ignored instead of poisoning the grid",()=>{
                var f=new Fixture();PlayerPrefs.SetString(f.PositionsKey,"broken;bad.txt,NaN,12;bad2.txt,Infinity,0;file000.txt,955,-685");
                f.Load(1);Equal(new Vector2(955,-685),f.Position("file000.txt"),"valid entry lost");
                Assert(Get<Dictionary<string,Vector2>>(f.os,"iconPositions").Count==1,"non-finite values accepted");
            });
            Run("manual drag collision avoidance uses the same unbounded grid as allocation",()=>{
                var f=new Fixture();f.Load(2);f.Drop("file000.txt",new Vector2(955,-955));f.Drop("file001.txt",new Vector2(955,-955));
                Assert(f.Position("file000.txt")!=f.Position("file001.txt"),"collision not avoided");
                Assert(f.Position("file001.txt").x>840 && f.Position("file001.txt").y < -500,"collision search clamped to viewport");
            });
            Run("mode change mid-drag finishes at the last logical position, not new pointer coordinates",()=>{
                var f=new Fixture();f.Load(1);var icon=f.Icons["file000.txt"];var drag=icon.GetComponent<DesktopIconDragger>();
                drag.OnPointerDown(new PointerEventData{position=new Vector2(55,-55)});Input.held=true;
                Input.mousePosition=new Vector3(75,-75);Call(drag,"Update");
                Input.mousePosition=new Vector3(250,-210);Call(drag,"Update");var expected=DesktopIconGrid.Default.Snap(icon.GetPosition());
                f.canvas.renderMode=RenderMode.ScreenSpaceOverlay;f.viewport.rect=new Rect(1700,900);
                Input.mousePosition=new Vector3(9000,9000);Call(drag,"Update");
                Equal(expected,icon.GetPosition(),"icon jumped on focus change");Assert(!DesktopIconDragger.IsDragging,"drag state stuck");Input.held=false;
            });
            Run("dense grids always find a free cell instead of returning an occupied origin",()=>{
                var grid=DesktopIconGrid.Default;var occupied=new HashSet<Vector2Int>();
                for(int y=0;y<20;y++)for(int x=0;x<20;x++)occupied.Add(new Vector2Int(x,y));
                Assert(!occupied.Contains(grid.GetCell(grid.FirstFreePosition(9,occupied))),"allocation failed");
                Assert(!occupied.Contains(grid.GetCell(grid.Snap(new Vector2(55,-55),occupied))),"snap failed");
            });
            Run("fullscreen canvas sizing follows scaler modes rather than monitor viewport dimensions",()=>{
                var f=new Fixture();f.Load();
                Equal(new Vector2(888.8889f,500),(Vector2)Call(f.os,"GetFullscreenDesktopSize"),"Expand");
                f.scaler.screenMatchMode=CanvasScaler.ScreenMatchMode.Shrink;
                Equal(new Vector2(800,450),(Vector2)Call(f.os,"GetFullscreenDesktopSize"),"Shrink");
                f.scaler.uiScaleMode=CanvasScaler.ScaleMode.ConstantPixelSize;f.scaler.scaleFactor=2;
                Equal(new Vector2(960,540),(Vector2)Call(f.os,"GetFullscreenDesktopSize"),"pixel scaling");
            });
            Run("fullscreen overflow moves to the closest free cell and leaves visible icons exact",()=>{
                var grid=DesktopIconGrid.Default;
                var positions=new Dictionary<string,Vector2>{{"fixed",new Vector2(775,-55)},{"off",new Vector2(865,-55)}};
                grid.FitOverflow(positions,new Vector2(840,500),new Vector4(0,40,0,0));
                Equal(new Vector2(775,-55),positions["fixed"],"visible icon moved");
                Equal(new Vector2(775,-145),positions["off"],"not the nearest free cell");
            });
            Run("fullscreen fitting respects the full icon rectangle and the taskbar",()=>{
                var grid=DesktopIconGrid.Default;var pad=new Vector4(0,40,0,0);
                var positions=new Dictionary<string,Vector2>{{"near-edge",new Vector2(805,-425)},{"under-taskbar",new Vector2(55,-445)}};
                grid.FitOverflow(positions,new Vector2(840,500),pad);
                Equal(new Vector2(805,-425),positions["near-edge"],"fully visible non-grid icon moved");
                Equal(new Vector2(55,-415),positions["under-taskbar"],"icon still under taskbar");
                foreach(var p in positions.Values)Assert(grid.FitsViewport(p,new Vector2(840,500),pad),"clipped rectangle");
            });
            Run("non-grid legacy icons reserve every cell their rectangle overlaps",()=>{
                var grid=DesktopIconGrid.Default;var positions=new Dictionary<string,Vector2>{
                    {"fixed",new Vector2(100,-55)},{"outside",new Vector2(-55,-55)}};
                grid.FitOverflow(positions,new Vector2(300,300),new Vector4(0,40,0,0));
                Equal(new Vector2(100,-55),positions["fixed"],"legacy icon resnapped");
                Equal(new Vector2(55,-145),positions["outside"],"fitted icon collided with legacy rectangle");
            });
            Run("if the view is full, overflow is left alone without shrinking, stacking or reordering",()=>{
                var grid=DesktopIconGrid.Default;var positions=new Dictionary<string,Vector2>{
                    {"a",new Vector2(55,-55)},{"b",new Vector2(145,-55)},
                    {"c",new Vector2(55,-145)},{"d",new Vector2(145,-145)},{"overflow",new Vector2(775,-865)}};
                var before=new Dictionary<string,Vector2>(positions);
                grid.FitOverflow(positions,new Vector2(180,230),new Vector4(0,40,0,0));
                foreach(var p in before)Equal(p.Value,positions[p.Key],"full view was altered");
                grid.FitOverflow(positions,new Vector2(20,20),new Vector4(0,40,0,0));
                foreach(var p in before)Equal(p.Value,positions[p.Key],"tiny view was altered");
            });
            Run("viewport fitting works for left/top/right/bottom overflow without changing on-screen positions",()=>{
                var grid=DesktopIconGrid.Default;var pad=new Vector4(0,40,0,0);var size=new Vector2(840,500);
                var positions=new Dictionary<string,Vector2>{{"left",new Vector2(-100,-145)},
                    {"top",new Vector2(235,100)},{"right",new Vector2(1100,-235)},{"bottom",new Vector2(505,-800)},
                    {"visible",new Vector2(400,-200)}};
                grid.FitOverflow(positions,size,pad);
                Equal(new Vector2(400,-200),positions["visible"],"visible icon changed");
                foreach(var p in positions.Values)Assert(grid.FitsViewport(p,size,pad),"overflow was not fitted");
            });
            Run("real fullscreen resize uses a temporary projection, not monitor coordinates or preferences",()=>{
                var f=new Fixture();f.Load(2);f.Drop("file001.txt",new Vector2(955,-55));var before=f.Snapshot();
                var prefs=PlayerPrefs.GetString(f.PositionsKey);int writes=PlayerPrefs.Writes;
                f.Frame(RenderMode.ScreenSpaceOverlay,840,500);
                Equal(new Vector2(775,-55),f.Position("file001.txt"),"fullscreen did not fit");
                Equal(new Vector2(55,-55),f.Position("file000.txt"),"visible icon moved");
                Assert(PlayerPrefs.Writes==writes && PlayerPrefs.GetString(f.PositionsKey)==prefs,"auto-fit leaked to saved layout");
                f.Frame(RenderMode.WorldSpace,240,160);f.Same(before);
                f.Frame(RenderMode.ScreenSpaceCamera,300,200);f.Same(before);
            });
            Run("growing a fullscreen window does not reshuffle icons that already fit",()=>{
                var f=new Fixture();f.Load(2);f.Drop("file001.txt",new Vector2(955,-55));
                f.Frame(RenderMode.ScreenSpaceOverlay,840,500);var fitted=f.Snapshot();
                f.Frame(RenderMode.ScreenSpaceOverlay,1600,900);f.Same(fitted);
            });
            Run("shrinking fullscreen moves only the icons that newly cross its bounds",()=>{
                var f=new Fixture();f.Load(3);f.Drop("file001.txt",new Vector2(955,-55));f.Drop("file002.txt",new Vector2(505,-55));
                f.Frame(RenderMode.ScreenSpaceOverlay,1280,720);var visible=f.Position("file002.txt");
                f.Frame(RenderMode.ScreenSpaceOverlay,840,500);
                Equal(new Vector2(775,-55),f.Position("file001.txt"),"right overflow did not fit");
                Equal(visible,f.Position("file002.txt"),"already visible icon moved");
                Equal(new Vector2(55,-55),f.Position("file000.txt"),"first icon moved");
            });
            Run("manual fullscreen moves still update the monitor, while other automatic fits do not",()=>{
                var f=new Fixture();f.Load(3);f.Drop("file001.txt",new Vector2(955,-55));f.Drop("file002.txt",new Vector2(1135,-235));
                f.Frame(RenderMode.ScreenSpaceOverlay,840,500);f.Drop("file001.txt",new Vector2(325,-325));
                f.Frame(RenderMode.WorldSpace,840,500);
                Equal(new Vector2(325,-325),f.Position("file001.txt"),"manual move not shared");
                Equal(new Vector2(1135,-235),f.Position("file002.txt"),"another auto-fit was saved");
            });
            Run("manual monitor edits take precedence over a cached fullscreen projection",()=>{
                var f=new Fixture();f.Load(2);f.Drop("file001.txt",new Vector2(955,-55));
                f.Frame(RenderMode.ScreenSpaceOverlay,840,500);f.Frame(RenderMode.WorldSpace,840,500);
                f.Drop("file000.txt",new Vector2(775,-55));f.Frame(RenderMode.ScreenSpaceOverlay,840,500);
                Equal(new Vector2(775,-55),f.Position("file000.txt"),"manual monitor position lost");
                Assert(!DesktopIconGrid.Default.Overlaps(f.Position("file000.txt"),f.Position("file001.txt")),"stale full projection collided");
            });
            Run("manual fullscreen dragging cannot steal another icon's saved monitor cell",()=>{
                var f=new Fixture();f.Load(2);f.Drop("file001.txt",new Vector2(955,-55));
                f.Frame(RenderMode.ScreenSpaceOverlay,840,500);f.Frame(RenderMode.ScreenSpaceOverlay,1280,720);
                f.Drop("file000.txt",new Vector2(955,-55));f.Frame(RenderMode.WorldSpace,840,500);
                Equal(new Vector2(955,-55),f.Position("file001.txt"),"saved cell was overwritten");
                Assert(!DesktopIconGrid.Default.Overlaps(f.Position("file000.txt"),f.Position("file001.txt")),"canonical collision");
            });
            Run("refresh, rename and file creation cannot persist a fullscreen-only fitted position",()=>{
                var f=new Fixture();f.Load(2);f.Drop("file001.txt",new Vector2(955,-55));f.Frame(RenderMode.ScreenSpaceOverlay,840,500);
                f.files[1].path="renamed.pic";f.os.RefreshDesktopIcon();
                f.os.FileManager.Create(0,new File("new.txt","content"));Call(f.os,"LateUpdate");
                Equal(new Vector2(775,-55),f.Position("renamed.pic"),"rename moved the full projection");
                f.Frame(RenderMode.WorldSpace,840,500);Equal(new Vector2(955,-55),f.Position("renamed.pic"),"rename leaked projection into save");
                Assert(f.Icons.ContainsKey("new.txt"),"new file not shown");
                var g=new Fixture(false);g.files.Add(new File("renamed.pic"));g.Load();
                Equal(new Vector2(955,-55),g.Position("renamed.pic"),"saved projection survived reboot");
            });
            Run("unhiding an icon does not stack it on a new file that reused its previous cell",()=>{
                var f=new Fixture();f.Load(1);f.files[0].hidden=true;f.Tick();
                f.os.FileManager.Create(0,new File("new.txt"));Call(f.os,"LateUpdate");
                f.files[0].hidden=false;f.Tick();
                Assert(!DesktopIconGrid.Default.Overlaps(f.Position("file000.txt"),f.Position("new.txt")),"unhide stacked icons");
            });
            Run("Paint's real SaveDialog path creates a desktop icon in the next frame",()=>{
                var f=new Fixture();f.Load();var go=new GameObject();var dialog=go.AddComponent<PC.Component.Software.OS.SaveDialog>();
                Set(dialog,"system",f.os);Set(dialog,"fileNameInput",new GameObject().AddComponent<InputField>());
                Set(dialog,"extensionText",new GameObject().AddComponent<Text>());
                dialog.ShowDialog("Picture","base64-image",new[]{".pic"});dialog.Save();
                Assert(!go.activeSelf,"successful save did not close dialog");Assert(f.Icons.Count==0,"refresh should be batched");
                Call(f.os,"LateUpdate");Assert(f.Icons.ContainsKey("Picture.pic"),"saved picture absent from desktop");
                Assert(f.Icons["Picture.pic"].File.content=="base64-image","picture bound to wrong file");
                Assert(f.os.FileViewRefreshCount==1,"open file views were not refreshed");
            });
            Run("saving another Paint picture with the same name adds its actual numbered filename",()=>{
                var f=new Fixture();f.Load();f.os.FileManager.Create(0,new File("Picture.pic","first"));Call(f.os,"LateUpdate");
                var dialog=new GameObject().AddComponent<PC.Component.Software.OS.SaveDialog>();Set(dialog,"system",f.os);
                Set(dialog,"fileNameInput",new GameObject().AddComponent<InputField>());
                dialog.ShowDialog("Picture","second",new[]{".pic"});dialog.Save();Call(f.os,"LateUpdate");
                Assert(f.Icons.ContainsKey("Picture.pic") && f.Icons.ContainsKey("Picture (1).pic"),"numbered save missing");
                Assert(f.Icons["Picture (1).pic"].File.content=="second","wrong saved contents");
            });
            Run("a failed SaveDialog save stays open and does not invent a desktop icon",()=>{
                var f=new Fixture();f.Load(2);f.os.AllStorage[0].Capacity=0;
                var dialog=new GameObject().AddComponent<PC.Component.Software.OS.SaveDialog>();Set(dialog,"system",f.os);
                Set(dialog,"fileNameInput",new GameObject().AddComponent<InputField>());
                dialog.ShowDialog("NotSaved","data",new[]{".pic"});dialog.Save();Call(f.os,"LateUpdate");
                Assert(dialog.gameObject.activeSelf && f.os.ErrorMessageCount==1,"save failure hidden from user");
                Assert(!f.Icons.ContainsKey("NotSaved.pic"),"failed save produced an icon");
            });
            Run("file-manager create/write/delete notifications refresh without waiting for the fallback scan",()=>{
                var f=new Fixture();f.Load();f.os.FileManager.Write(0,"note.txt","first");Call(f.os,"LateUpdate");
                Assert(f.Icons.ContainsKey("note.txt"),"write-created file absent");var icon=f.Icons["note.txt"];
                f.os.FileManager.Write(0,"note.txt","updated");Call(f.os,"LateUpdate");
                Assert(ReferenceEquals(icon,f.Icons["note.txt"]) && icon.File.content=="updated","overwrite not refreshed/reused");
                f.os.FileManager.Delete(0,"note.txt");Call(f.os,"LateUpdate");Assert(f.Icons.Count==0,"delete left an icon");
            });
            Run("a batch of file writes causes one refresh, without reordering existing icons",()=>{
                var f=new Fixture();f.Load(2);f.Drop("file001.txt",new Vector2(955,-685));var before=f.Snapshot();
                int refreshes=f.os.FileViewRefreshCount;
                for(int i=0;i<20;i++)f.os.FileManager.Create(0,new File("new"+i+".txt","data"));
                Call(f.os,"LateUpdate");Assert(f.os.FileViewRefreshCount==refreshes+1,"writes were not coalesced");
                foreach(var p in before)Equal(p.Value,f.Position(p.Key),"file batch moved existing icon");
            });
            Run("fallback detects direct list/metadata edits including rename, hide, move and delete",()=>{
                var f=new Fixture();f.Load(1);f.Drop("file000.txt",new Vector2(325,-235));
                f.files[0].path="renamed.txt";f.Tick();Assert(!f.Icons.ContainsKey("file000.txt") && f.Icons.ContainsKey("renamed.txt"),"rename not detected");
                Equal(new Vector2(325,-235),f.Position("renamed.txt"),"direct rename lost position");
                f.files[0].hidden=true;f.Tick();Assert(f.Icons.Count==0,"hide not detected");
                f.files[0].hidden=false;f.Tick();Assert(f.Icons.Count==1,"unhide not detected");
                f.files[0].path="Folder/renamed.txt";f.Tick();Assert(f.Icons.Count==0,"move into folder not detected");
                f.files[0].path="renamed.txt";f.Tick();Assert(f.Icons.Count==1,"move onto desktop not detected");
                f.files.RemoveAt(0);f.Tick();Assert(f.Icons.Count==0,"direct delete not detected");
            });
            Run("fallback detects direct storage writes, copying, contents and folder-flag changes",()=>{
                var f=new Fixture();f.Load();f.os.AllStorage[0].Write("direct.txt","initial");f.Tick();
                Assert(f.Icons.ContainsKey("direct.txt"),"direct storage write not detected");
                int count=f.os.FileViewRefreshCount;f.files[0].content="changed directly";f.Tick();
                Assert(f.os.FileViewRefreshCount==count+1,"content change not detected");
                count=f.os.FileViewRefreshCount;f.files[0].isFolder=true;f.Tick();Assert(f.os.FileViewRefreshCount==count+1,"folder metadata not detected");
                f.files.Add(new File("copy.txt",f.files[0].content));f.Tick();Assert(f.Icons.ContainsKey("copy.txt"),"copy not detected");
            });
            Run("disk cloning rebinds existing icons to new File instances; formatting clears them",()=>{
                var f=new Fixture();f.Load(1);var icon=f.Icons["file000.txt"];var replacement=new File("file000.txt","cloned");
                f.os.AllStorage[0].files=new List<File>{replacement};f.Tick();
                Assert(ReferenceEquals(icon,f.Icons["file000.txt"]) && ReferenceEquals(icon.File,replacement),"icon retained old disk file");
                f.os.AllStorage[0].files=new List<File>();f.Tick();Assert(f.Icons.Count==0,"format left desktop icons");
            });
            Run("filesystem refresh respects hidden files, subfolders and normal root folders",()=>{
                var f=new Fixture();f.Load();f.os.FileManager.Create(0,new File("hidden.txt","",true));
                f.os.FileManager.Create(0,new File("Folder/inside.pic","data"));f.os.FileManager.Create(0,File.MakeFolder("Folder"));
                Call(f.os,"LateUpdate");Assert(f.Icons.Count==1 && f.Icons.ContainsKey("Folder"),"desktop visibility filter broken");
            });
            Run("idle file scans never rebuild the desktop or keep rewriting preferences",()=>{
                var f=new Fixture();f.Load(30);int writes=PlayerPrefs.Writes;int views=f.os.FileViewRefreshCount;
                var icons=new Dictionary<string,FileIcon>(f.Icons);
                for(int i=0;i<100;i++)f.Tick();
                Assert(PlayerPrefs.Writes==writes && f.os.FileViewRefreshCount==views,"idle scan refreshed unchanged files");
                foreach(var pair in icons)Assert(ReferenceEquals(pair.Value,f.Icons[pair.Key]),"idle scan recreated icon");
            });
            Run("file refresh is deferred during dragging and resumes on release",()=>{
                var f=new Fixture();f.Load(1);var drag=f.Icons["file000.txt"].GetComponent<DesktopIconDragger>();
                drag.OnPointerDown(new PointerEventData{position=new Vector2(55,-55)});Input.held=true;
                Input.mousePosition=new Vector3(100,-100);Call(drag,"Update");
                f.os.FileManager.Create(0,new File("saved-during-drag.txt"));Call(f.os,"LateUpdate");
                Assert(!f.Icons.ContainsKey("saved-during-drag.txt"),"refresh interrupted drag");
                Input.held=false;Call(drag,"Update");Call(f.os,"LateUpdate");Assert(f.Icons.ContainsKey("saved-during-drag.txt"),"deferred refresh lost");
            });
            Run("file snapshots detect replacement without copying or hashing image strings",()=>{
                var snapshot=new DesktopFileSnapshot();var files=new List<File>{new File("image.pic",new string('x',100000))};
                snapshot.Capture(files);Assert(!snapshot.HasChanged(files),"stable snapshot changed");
                files[0].size=99;Assert(snapshot.HasChanged(files),"size not tracked");snapshot.Capture(files);
                files[0]=new File("image.pic",files[0].content,false,99);Assert(snapshot.HasChanged(files),"replacement identity not tracked");
                snapshot.Capture(null);Assert(!snapshot.HasChanged(null),"null disk snapshot unstable");
            });
            Run("manual sort in fullscreen replaces temporary fitting and remains shared with the monitor",()=>{
                var f=new Fixture();f.Load(10);f.Drop("file009.txt",new Vector2(1405,-865));
                f.Frame(RenderMode.ScreenSpaceOverlay,840,500);f.os.SortDesktopIcons("Size");var sorted=f.Snapshot();
                Equal(new Vector2(55,-55),f.Position("file009.txt"),"manual full sort failed");
                f.Frame(RenderMode.WorldSpace,320,180);f.Same(sorted);
            });
            Run("viewport padding changes refit fullscreen overflow without modifying monitor positions",()=>{
                var f=new Fixture();f.Load(1);f.Drop("file000.txt",new Vector2(55,-415));
                f.Frame(RenderMode.ScreenSpaceOverlay,840,500);f.viewport.GetComponent<RectMask2D>().padding=new Vector4(0,200,0,0);
                f.Tick();Equal(new Vector2(55,-235),f.Position("file000.txt"),"new bottom exclusion ignored");
                f.Frame(RenderMode.WorldSpace,840,500);Equal(new Vector2(55,-415),f.Position("file000.txt"),"padding fit persisted");
            });
            Run("file mutations during Explorer refresh are not swallowed by the snapshot",()=>{
                var f=new Fixture();f.Load();f.os.OnRefreshFileViews=()=>{
                    if(f.files.Count==1)f.files.Add(File.MakeFolder("AddedByExplorer"));
                };
                f.os.FileManager.Create(0,new File("trigger.txt"));Call(f.os,"LateUpdate");
                Call(f.os,"LateUpdate");Assert(f.Icons.ContainsKey("AddedByExplorer"),"follow-up mutation was marked already seen");
                int refreshed=f.os.FileViewRefreshCount;f.Tick();Assert(f.os.FileViewRefreshCount==refreshed,"refresh feedback loop");
            });
            Run("250 randomized grid layouts retain visible icons and never overlap fitted ones",()=>{
                var random=new Random(420);var grid=DesktopIconGrid.Default;var pad=new Vector4(0,40,0,0);
                for(int round=0;round<250;round++){
                    var size=new Vector2(random.Next(120,1300),random.Next(140,850));var positions=new Dictionary<string,Vector2>();var used=new HashSet<Vector2Int>();
                    for(int i=0;i<25;i++){
                        var cell=new Vector2Int(random.Next(0,20),random.Next(0,15));if(!used.Add(cell)){i--;continue;}
                        positions[i.ToString("D2")]=grid.GetPosition(cell);
                    }
                    var before=new Dictionary<string,Vector2>(positions);grid.FitOverflow(positions,size,pad);
                    foreach(var p in before){
                        if(grid.FitsViewport(p.Value,size,pad))Equal(p.Value,positions[p.Key],"visible icon changed in randomized fit");
                        if(p.Value!=positions[p.Key]){
                            Assert(grid.FitsViewport(positions[p.Key],size,pad),"fitted icon still clipped");
                            foreach(var other in positions)if(other.Key!=p.Key)Assert(!grid.Overlaps(positions[p.Key],other.Value),"fitted rectangles overlap");
                        }
                    }
                    var once=new Dictionary<string,Vector2>(positions);grid.FitOverflow(positions,size,pad);
                    foreach(var p in once)Equal(p.Value,positions[p.Key],"stable fit was not idempotent");
                }
            });
            Console.WriteLine(cases+" headless desktop regression cases passed.");return 0;
        }
        catch(Exception e){Console.Error.WriteLine(e);if(e.InnerException!=null)Console.Error.WriteLine(e.InnerException);return 1;}
    }
}
