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
            Screen.width=1920;Screen.height=1080;Screen.dpi=96;Input.held=false;
            var root=new GameObject();
            canvas=root.AddComponent<Canvas>();canvas.renderMode=RenderMode.WorldSpace;
            scaler=root.AddComponent<CanvasScaler>();
            os=root.AddComponent<OS>();
            os.Board=new Motherboard{Id=boardId,monitor=new PC.Component.Display{Id=monitorId}};
            os.AllStorage=new List<Storage>{new Storage{files=files}};
            var desk=new GameObject();desk.transform.parent=root.transform;desktop=desk.transform;
            var group=desk.AddComponent<CanvasGroup>();
            var icons=new GameObject();icons.transform.parent=desk.transform;viewport=icons.transform;
            var prefab=new GameObject();var icon=prefab.AddComponent<FileIcon>();prefab.AddComponent<DesktopIconDragger>();
            Set(os,"desktop",group);Set(os,"iconParent",viewport);Set(os,"fileIconPrefab",icon);
        }
        public void Add(int count){for(int i=0;i<count;i++)files.Add(new File("file"+i.ToString("D3")+".txt",i));}
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
            foreach(var icon in Icons.Values)Call(icon.GetComponent<DesktopIconDragger>(),"Update");
        }
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
            Run("100 world/overlay/texture-camera switches preserve all positions and do not write prefs",()=>{
                var f=new Fixture();f.Load(100);var before=f.Snapshot();int writes=PlayerPrefs.Writes;
                for(int i=0;i<100;i++){
                    f.Frame(RenderMode.WorldSpace,320,180);f.Frame(RenderMode.ScreenSpaceOverlay,1600,900);
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
            Console.WriteLine(cases+" headless desktop regression cases passed.");return 0;
        }
        catch(Exception e){Console.Error.WriteLine(e);if(e.InnerException!=null)Console.Error.WriteLine(e.InnerException);return 1;}
    }
}
