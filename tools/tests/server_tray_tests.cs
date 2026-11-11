using System;
using System.Reflection;
using UnityEngine;
using PC.Component;
public static class ServerTrayTests {
 static void Check(bool ok,string msg){if(!ok)throw new Exception(msg);}
 static void Field(object o,string n,object v){o.GetType().GetField(n,BindingFlags.Instance|BindingFlags.NonPublic).SetValue(o,v);}
 static void Call(object o,string n){o.GetType().GetMethod(n,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(o,null);}
 static T Part<T>(GameObject parent=null) where T:Component,new(){var go=new GameObject();go.parent=parent;return go.Add<T>();}
 static HardwareSlot DriveSlot(DriveTray tray){var s=Part<HardwareSlot>(tray.gameObject);s.onChanged=new UnityEngine.Events.UnityEvent();return s;}
 static DriveTray Tray(out HardwareSlot drive){var t=Part<DriveTray>();drive=DriveSlot(t);Field(t,"driveSlot",drive);Call(t,"Awake");Call(t,"OnEnable");return t;}
 static Case Chassis(out HardwareSlot boardSlot,out Slot bay){var c=Part<Case>();boardSlot=Part<HardwareSlot>(c.gameObject);boardSlot.onChanged=new UnityEngine.Events.UnityEvent();Field(c,"motherboard",boardSlot);Field(c,"external",new Motherboard.External());bay=Part<Slot>(c.gameObject);bay.target="DriveTray";Call(c,"Awake");Call(c,"Start");return c;}
 static int Count(Motherboard b){return b.GetSlots(HardwareType.Drive).FindAll(s=>s.Ready).Count;}
 public static int Main(){
  HardwareSlot boardSlot,drive;Slot bay;var chassis=Chassis(out boardSlot,out bay);var tray=Tray(out drive);var disk=Part<Storage>();var board=Part<Motherboard>();
  // Disk first, tray still loose. There is no path to the board.
  drive.TryAttach(disk);Check(!disk.Power&&!tray.Connected&&Count(board)==0,"loose basket was visible");
  bay.TryAttach(tray);Check(!tray.Connected&&Count(board)==0,"case without motherboard was visible");
  boardSlot.TryAttach(board);Check(tray.Connected&&Count(board)==1&&!disk.Power,"motherboard-last registration");
  board.TestPower(true);Check(disk.Power,"boot must power a docked drive");
  Check(board.System.Disks.Count==1,"boot disk missing");
  tray.RefreshConnection();tray.RefreshConnection();Check(Count(board)==1&&board.System.Disks.Count==1,"duplicate external/OS disk");
  bay.UnPlug();Check(board.System.Faults==1,"removing system disk must fault the OS");Check(!tray.Connected&&Count(board)==0&&!disk.Power&&board.System.Disks.Count==0,"removing basket must remove and power off HDD");
  // Connector survives briefly after UnPlug in the real engine; IsUsing/AttachedItem must gate it.
  tray.RefreshConnection();Check(!tray.Connected&&Count(board)==0,"stale Connector reattached basket");
  bay.TryAttach(tray);Check(tray.Connected&&disk.Power&&board.System.Disks.Count==1,"live reinsertion did not enumerate/power prefilled tray");
  drive.UnPlug();Check(Count(board)==0&&board.System.Disks.Count==0&&!disk.Power,"HDD-only removal");
  drive.TryAttach(disk);Check(Count(board)==1&&disk.Power&&board.System.Disks.Count==1,"tray-first HDD insertion");
  // Removing/replacing motherboard disconnects every tray even when its drive remains in it.
  boardSlot.UnPlug();Check(!tray.Connected&&Count(board)==0&&!disk.Power,"board removal left tray registered");
  var secondBoard=Part<Motherboard>();boardSlot.TryAttach(secondBoard);Check(tray.Connected&&Count(secondBoard)==1&&Count(board)==0,"board replacement");
  secondBoard.TestPower(true);Check(disk.Power,"new board boot");
  Call(tray,"OnDisable");Check(!tray.Connected&&!disk.Power&&Count(secondBoard)==0,"disable cleanup");
  Call(tray,"OnEnable");Check(tray.Connected&&disk.Power&&Count(secondBoard)==1,"enable restoration");
  HardwareSlot drive2;var tray2=Tray(out drive2);var disk2=Part<Storage>();drive2.TryAttach(disk2);var bay2=Part<Slot>(chassis.gameObject);bay2.target="DriveTray";bay2.TryAttach(tray2);
  Check(Count(secondBoard)==2&&secondBoard.System.Disks.Count==2,"second basket");
  // Data disks do not fault; a secondary disk explicitly containing boot.bin does.
  int faults=secondBoard.System.Faults;bay2.UnPlug();Check(secondBoard.System.Faults==faults,"data-disk removal faulted OS");bay2.TryAttach(tray2);
  disk2.BootDisk=true;bay2.UnPlug();Check(secondBoard.System.Faults==faults+1,"secondary boot disk removal did not fault OS");disk2.BootDisk=false;bay2.TryAttach(tray2);
  bay.UnPlug();Check(Count(secondBoard)==1&&disk2.Power&&!disk.Power,"one basket removal disconnected other basket");
  // Wrong chassis slot must not grant drive recognition.
  var wrong=Part<Slot>(chassis.gameObject);wrong.target="Cover";wrong.TryAttach(tray);Check(!tray.Connected&&Count(secondBoard)==1,"non-tray slot granted recognition");
  // Actual motherboard idempotence: repeated AddExternal and RemoveExternal do not duplicate events.
  var ext=new Motherboard.External{drive=new[]{drive}};secondBoard.AddExternal(ext);secondBoard.AddExternal(ext);
  Check(Count(secondBoard)==2,"AddExternal duplicates");secondBoard.RemoveExternal(ext);secondBoard.RemoveExternal(ext);Check(Count(secondBoard)==1,"RemoveExternal duplicates");
  // Factory parts latch through the same Slot events; loading suppresses the
  // embedded defaults because the save independently restores their Items.
  HardwareSlot factoryBoard,fd;Slot factoryBay;var factoryCase=Chassis(out factoryBoard,out factoryBay);
  var factoryTray=Tray(out fd);var lid=Part<Item>();var lidSlot=Part<Slot>(factoryCase.gameObject);lidSlot.target="Cover";
  var factory=factoryCase.gameObject.Add<ServerChassis>();Field(factory,"factoryCover",lid);Field(factory,"coverSlot",lidSlot);
  Field(factory,"factoryTrays",new[]{factoryTray});Field(factory,"trayBays",new[]{factoryBay});
  var sampleDrive=Part<Storage>();Field(factory,"factoryDrive",sampleDrive);Field(factory,"factoryDriveSlot",fd);
  Call(factory,"Awake");Call(factory,"Start");Check(factoryBay.AttachedItem==factoryTray&&lidSlot.AttachedItem==lid&&fd.AttachedItem==sampleDrive,"factory attachment");
  var restoredFactory=Part<ServerChassis>();var loadingTray=Tray(out fd);var loadingLid=Part<Item>();
  Field(restoredFactory,"factoryCover",loadingLid);Field(restoredFactory,"coverSlot",Part<Slot>());
  Field(restoredFactory,"factoryTrays",new[]{loadingTray});Field(restoredFactory,"trayBays",new[]{Part<Slot>()});
  var loadingDrive=Part<Storage>();Field(restoredFactory,"factoryDrive",loadingDrive);Field(restoredFactory,"factoryDriveSlot",fd);
  SaveManager.RestoringSave=true;Call(restoredFactory,"Awake");SaveManager.RestoringSave=false;Call(restoredFactory,"Start");
  Check(!loadingDrive.gameObject.active&&!loadingLid.gameObject.active&&!loadingTray.gameObject.active&&loadingTray.GetComponent<Connector>()==null,"save load respawned factory parts");
  Console.WriteLine("PASS: real tray/case/slot event code; both insertion orders, live power/OS sync, stale connectors, motherboard replacement, multiple trays, idempotent externals");return 0;
 }
}
