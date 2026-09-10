using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using PC.Component.Software;
using OS = PC.Component.Software.OS.OperatingSystem;

public static class AccountInputTests
{
    static int cases;
    static readonly BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public;
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
    static void Call(object target,string method,params object[] args){target.GetType().GetMethod(method,Flags).Invoke(target,args);}
    static void Set(object target,string field,object value){target.GetType().GetField(field,Flags).SetValue(target,value);}
    static void Run(string name,Action test){test();cases++;Console.WriteLine("PASS: "+name);}
    static PointerEventData Event(int id=-1){return new PointerEventData{pointerId=id,position=new Vector2(50,50)};}
    static Text Label(){return new GameObject("Label").AddComponent<Text>();}
    static void Reset()
    {
        ServerAccounts.Clear();WorkshopClient.Instance=new WorkshopClient();Input.held=false;Input.leftDown=false;Input.rightDown=false;Input.escape=false;
        Input.mousePosition=new Vector3(50,50,0);Input.touches=new Touch[0];Time.unscaledTime=0;DesktopIconDragger.IsDragging=false;
        EventSystem.current=new EventSystem();
    }
    sealed class AccountFixture:IDisposable
    {
        public AccountPage page;public Text name,email,state,chip;public GameObject content;
        public AccountFixture()
        {
            page=new GameObject().AddComponent<AccountPage>();name=Label();email=Label();state=Label();chip=Label();content=new GameObject("Content");
            Set(page,"nameText",name);Set(page,"emailText",email);Set(page,"savesStateText",state);Set(page,"chipText",chip);Set(page,"savesList",content.transform);
        }
        public void Dispose(){page.DisposeTest();}
    }
    sealed class DesktopFixture
    {
        public OS os;public DesktopContextMenu menu;public Canvas canvas;public GameObject desktop;
        public DesktopFixture()
        {
            Reset();var root=new GameObject("Computer");os=root.AddComponent<OS>();canvas=root.AddComponent<Canvas>();canvas.renderMode=RenderMode.WorldSpace;
            desktop=new GameObject("Desktop");desktop.transform.parent=root.transform;os.desktop=desktop;menu=desktop.AddComponent<DesktopContextMenu>();
            Set(menu,"operatingSystem",os);Set(menu,"canvas",canvas);DesktopContextMenu.Current=menu;Hits(desktop);
        }
        public void Hits(params GameObject[] objects){EventSystem.current.hits.Clear();foreach(var go in objects)EventSystem.current.hits.Add(new RaycastResult{gameObject=go});}
        public void Down(int id=-1){Input.held=id<0;menu.OnPointerDown(Event(id));}
        public void Tick(float time){Time.unscaledTime=time;Call(menu,"Update");}
    }
    public static int Main()
    {
        try
        {
            Run("cached account name is shown before a network request",()=>{
                Reset();ServerAccounts.SetSession("token-a","CachedName","sample@example.com");var go=new GameObject();var text=go.AddComponent<Text>();text.text="User";
                var label=go.AddComponent<AccountProfileLabel>();Call(label,"Awake");Call(label,"OnEnable");
                Check(text.text=="CachedName"&&WorkshopClient.Instance.callbacks.Count==0,"placeholder/network wait");Localization.Change();Check(text.text=="CachedName","name translated");Call(label,"OnDisable");
            });
            Run("email masking hides at least half of the local part",()=>{
                Check(AccountProfileLabel.MaskEmail("johnsmith@example.com")=="john*****@example.com","nine-character local part");
                Check(AccountProfileLabel.MaskEmail("abcd@example.com")=="ab**@example.com","even local part");
                Check(AccountProfileLabel.MaskEmail("a@example.com")=="*@example.com","single character exposed");
                Check(AccountProfileLabel.MaskEmail("ab@example.com")=="a*@example.com","two characters");
                Check(AccountProfileLabel.MaskEmail(null)==""&&AccountProfileLabel.MaskEmail("   ")=="","empty email");
                Check(AccountProfileLabel.MaskEmail("😀a@example.com")=="😀*@example.com","split surrogate pair");
            });
            Run("masked display does not alter the cached real email and clears on logout",()=>{
                Reset();ServerAccounts.SetSession("token-a","Name","address@example.com");var go=new GameObject();var text=go.AddComponent<Text>();var label=go.AddComponent<AccountProfileLabel>();Set(label,"showEmail",true);
                Call(label,"Awake");Call(label,"OnEnable");Check(text.text!="address@example.com"&&ServerAccounts.Email=="address@example.com","email changed/exposed");
                ServerAccounts.Clear();Check(text.text=="","cached email remained after logout");Call(label,"OnDisable");
            });
            Run("switching accounts drops another user's saves, email and bonus",()=>{
                Reset();ServerAccounts.SetSession("a","Alice","alice@example.com");ServerAccounts.SetSaves(new List<AccountSaveItem>{new AccountSaveItem{id=7}});ServerAccounts.SetBonusClaimed();
                ServerAccounts.SetSession("b","Bob",null);Check(ServerAccounts.MySaves.Count==0&&!ServerAccounts.SavesLoaded&&!ServerAccounts.BonusClaimed&&ServerAccounts.Email=="","cross-account cache");
            });
            Run("a stale profile response cannot restore an old session",()=>{
                Reset();ServerAccounts.SetSession("a","Alice","alice@example.com");ServerAccounts.Clear();ServerAccounts.SetSession("b","Bob","bob@example.com");
                Check(!ServerAccounts.TryApplyProfile("a",new AccountMeResponse{ok=true,name="Alice",email="alice@example.com"}),"old response applied");Check(ServerAccounts.Name=="Bob","name changed");
            });
            Run("profile updates apply identity and saves in one notification",()=>{
                Reset();ServerAccounts.SetSession("a","Before","before@example.com");int events=0;Action cb=()=>{events++;Check(ServerAccounts.Name=="After"&&ServerAccounts.SavesLoaded&&ServerAccounts.MySaves.Count==1,"partial profile notified");};
                ServerAccounts.StateChanged+=cb;Check(ServerAccounts.TryApplyProfile("a",new AccountMeResponse{ok=true,name="After",email="after@example.com",saves=new[]{new AccountSaveItem{id=2}}}),"profile not applied");ServerAccounts.StateChanged-=cb;Check(events==1,"not atomic");
            });
            Run("profile loading is visible and duplicate requests are coalesced",()=>{
                Reset();ServerAccounts.SetSession("a","Cached","cached@example.com");using(var f=new AccountFixture()){
                    f.page.BeginProfile();f.page.BeginProfile();Check(WorkshopClient.Instance.callbacks.Count==1,"duplicate request");
                    Check(f.name.text=="Cached"&&f.state.gameObject.activeSelf&&f.state.text=="Loading...","loading/identity missing");
                }
            });
            Run("successful empty account response shows the authored empty state",()=>{
                Reset();ServerAccounts.SetSession("a","UserName","name@example.com");using(var f=new AccountFixture()){
                    f.page.BeginProfile();WorkshopClient.Instance.callbacks[0](new AccountMeResponse{ok=true,name="UserName",email="name@example.com",saves=new AccountSaveItem[0]},null);
                    Check(f.state.gameObject.activeSelf&&f.state.text=="You haven't published any saves yet.","empty state invisible");
                }
            });
            Run("nonempty save list hides the empty label and renders rows",()=>{
                Reset();ServerAccounts.SetSession("a","Name","name@example.com");using(var f=new AccountFixture()){
                    f.page.BeginProfile();WorkshopClient.Instance.callbacks[0](new AccountMeResponse{ok=true,name="Name",saves=new[]{new AccountSaveItem{id=1},new AccountSaveItem{id=2}}},null);
                    Check(!f.state.gameObject.activeSelf&&f.content.transform.childCount==2,"nonempty state wrong");
                }
            });
            Run("network failure keeps cached identity but is not reported as no saves",()=>{
                Reset();ServerAccounts.SetSession("a","Cached","cached@example.com");using(var f=new AccountFixture()){
                    f.page.BeginProfile();WorkshopClient.Instance.callbacks[0](null,"offline");
                    Check(f.name.text=="Cached"&&f.state.gameObject.activeSelf&&f.state.text=="Could not load saves. Try again.","network failure erased cache or masqueraded as empty");
                }
            });
            Run("AccountPage ignores an in-flight response after logout",()=>{
                Reset();ServerAccounts.SetSession("a","Cached","cached@example.com");using(var f=new AccountFixture()){
                    f.page.BeginProfile();Call(f.page,"DoLogout");WorkshopClient.Instance.callbacks[0](new AccountMeResponse{ok=true,name="Old",email="old@example.com"},null);
                    Check(!ServerAccounts.LoggedIn&&ServerAccounts.Name==""&&f.chip.text=="Not loggined","late response restored profile");
                }
            });
            Run("admin badge comes from the server flag, never from a chosen nickname",()=>{
                Reset();ServerAccounts.SetSession("admin-test","Goose","g@example.test");
                var go=new GameObject();var text=go.AddComponent<Text>();text.color=new Color(1,0.6f,0,1);var label=go.AddComponent<AccountProfileLabel>();Call(label,"Awake");Call(label,"OnEnable");
                Check(!ServerAccounts.IsAdmin && text.color.g==0.6f,"name alone granted badge");
                ServerAccounts.TryApplyProfile("admin-test",new AccountMeResponse{ok=true,name="Goose",is_admin=true});
                Check(ServerAccounts.IsAdmin && text.color.g==AccountProfileLabel.AdminColor.g,"server role not shown");
                ServerAccounts.TryApplyProfile("admin-test",new AccountMeResponse{ok=true,name="Goose",is_admin=false});
                Check(!ServerAccounts.IsAdmin && text.color.g==0.6f,"revoked badge stayed red");Call(label,"OnDisable");
            });
            Run("cached admin role survives display initialization but not logout/account switch",()=>{
                Reset();ServerAccounts.SetSession("a","Semyalol","s@example.test",true);Check(ServerAccounts.IsAdmin,"auth role not cached");
                ServerAccounts.SetSession("b","Player","p@example.test");Check(!ServerAccounts.IsAdmin,"old admin role leaked");
                ServerAccounts.SetSession("a","Semyalol","s@example.test",true);ServerAccounts.Clear();Check(!ServerAccounts.IsAdmin,"logout kept admin");
            });
            Run("touch lookup follows the original finger rather than touch zero",()=>{
                Reset();Input.touches=new[]{new Touch{fingerId=1,position=new Vector2(999,999),phase=TouchPhase.Moved},new Touch{fingerId=7,position=new Vector2(50,50),phase=TouchPhase.Stationary}};
                Vector2 p;Check(PointerInput.TryGetHeldPosition(7,out p)&&p.x==50,"wrong finger");Check(!PointerInput.TryGetHeldPosition(9,out p),"missing finger treated as held");
            });
            Run("ended and cancelled touches never count as held",()=>{
                Reset();Vector2 p;Input.touches=new[]{new Touch{fingerId=7,phase=TouchPhase.Ended}};Check(!PointerInput.TryGetHeldPosition(7,out p),"ended");Input.touches[0].phase=TouchPhase.Canceled;Check(!PointerInput.TryGetHeldPosition(7,out p),"cancelled");
                Check(!PointerInput.TryGetHeldPosition(-1,out p),"released mouse");Input.held=true;Check(PointerInput.TryGetHeldPosition(-1,out p),"held mouse");
            });
            Run("a joystick blocks a TV desktop even if the world canvas sorts first",()=>{
                var f=new DesktopFixture();var hud=new GameObject("Walk");hud.AddComponent<Joystick>();f.Hits(f.desktop,hud);f.Down();f.Tick(1);Check(f.menu.Opened==0&&!f.menu.CanReceivePointer(new Vector2(50,50)),"joystick click-through");
            });
            Run("HUD movement buttons block right-click and touch context menus",()=>{
                var f=new DesktopFixture();var hud=new GameObject("Walk");hud.AddComponent<ButtonHandler>();f.Hits(hud,f.desktop);Input.rightDown=true;f.Tick(1);Check(f.menu.Opened==0,"button click-through");
                Input.rightDown=false;f.Down();f.Tick(2);Check(f.menu.Opened==0,"button hold click-through");
            });
            Run("ordinary buttons inside the same OS are not mistaken for gameplay controls",()=>{
                var f=new DesktopFixture();var button=new GameObject();button.transform.parent=f.desktop.transform;button.AddComponent<Button>();f.Hits(button);Check(f.menu.CanReceivePointer(new Vector2(50,50)),"OS control blocked");
            });
            Run("right-click on an unobstructed desktop still opens its menu",()=>{
                var f=new DesktopFixture();Input.rightDown=true;f.Tick(1);Check(f.menu.Opened==1,"right-click regressed");
            });
            Run("a held desktop press opens once, while a released tap cannot open later",()=>{
                var f=new DesktopFixture();f.Down();Input.held=false;f.Tick(1);Check(f.menu.Opened==0,"released tap opened later");
                f.Down();f.Tick(2);Check(f.menu.Opened==1,"hold failed");f.Tick(3);Check(f.menu.Opened==1,"repeated hold menu");
            });
            Run("moving beyond the hold threshold cancels even after returning",()=>{
                var f=new DesktopFixture();f.Down();Input.mousePosition=new Vector3(100,100,0);f.Tick(0.1f);Input.mousePosition=new Vector3(50,50,0);f.Tick(1);Check(f.menu.Opened==0,"moved press revived");
            });
            Run("unrelated finger release does not cancel the original held gesture",()=>{
                var f=new DesktopFixture();Input.touches=new[]{new Touch{fingerId=7,position=new Vector2(50,50),phase=TouchPhase.Stationary}};f.Down(7);f.menu.OnPointerUp(Event(3));f.Tick(1);Check(f.menu.Opened==1,"another finger controlled the gesture");
            });
            Run("mode changes and disable cancel pending desktop holds",()=>{
                var f=new DesktopFixture();f.Down();f.canvas.renderMode=RenderMode.ScreenSpaceOverlay;f.Tick(1);Check(f.menu.Opened==0,"mode switch hold");
                f.Down();Call(f.menu,"OnDisable");f.Tick(2);Check(f.menu.Opened==0,"disabled hold survived");
            });
            Run("file-icon long press requires the same pointer to remain held",()=>{
                var f=new DesktopFixture();var go=new GameObject("Icon");go.transform.parent=f.desktop.transform;var icon=go.AddComponent<FileIcon>();icon.Init(new PC.Component.Software.File("note.txt"),x=>{});f.Hits(go);
                Input.held=true;icon.OnPointerDown(Event());Input.held=false;Time.unscaledTime=1;Call(icon,"Update");Check(f.menu.Opened==0,"icon tap opened after release");
                Input.held=true;icon.OnPointerDown(Event());Time.unscaledTime=2;Call(icon,"Update");Check(f.menu.Opened==1,"icon long press failed");
            });
            Run("Explorer background also cancels a released press",()=>{
                var f=new DesktopFixture();var go=new GameObject("Explorer");go.transform.parent=f.desktop.transform;var explorer=go.AddComponent<FileManager>();var pane=go.AddComponent<ExplorerPane>();pane.Init(explorer);f.Hits(go);
                Input.held=true;pane.OnPointerDown(Event());Input.held=false;Time.unscaledTime=1;Call(pane,"Update");Check(f.menu.Opened==0,"explorer stale hold");
            });
            Console.WriteLine(cases+" account/input regression cases passed.");return 0;
        }
        catch(Exception e){Console.Error.WriteLine(e);return 1;}
    }
}
