#!/usr/bin/env python3
"""Compile desktop backend/actual plugin routing, test filters, Unicode, cancel and cursor restore.
Requires mcs/mono. Native Windows/macOS GUI is not launched in this Linux sandbox.
"""
from pathlib import Path
import json,re,subprocess,tempfile
ROOT=Path(__file__).resolve().parents[1]
HELPER=ROOT/'Assets/Plugins/OrangePCNativeDialogs/DesktopFilePicker.cs'
FP=ROOT/'Assets/Plugins/NativeFilePicker/NativeFilePicker.cs'
NG=ROOT/'Assets/Plugins/NativeGallery/NativeGallery.cs'

def method(path,name):
    match=re.search(r'\t(?:public|private) static [^\n]+\b'+name+r'\(.*?\n\t\}',path.read_text(),re.S)
    if not match:
        match=re.search(r'    private static string\[\] '+name+r'\(.*?\n    \}',path.read_text(),re.S)
    assert match,name
    return match[0]

def compile_run(folder,code,define,actual=False):
    test=folder/'test.cs';test.write_text(code);exe=folder/'tests.exe'
    sources=[str(HELPER)] if actual else []
    subprocess.run(['mcs','-langversion:latest','-nowarn:0169,0414,0219','-define:'+define,'-out:'+str(exe)]+sources+[str(test)],check=True)
    subprocess.run(['mono',str(exe)],check=True)

COMMON='''using System;using System.Collections.Generic;using System.IO;using System.Reflection;using System.Runtime.InteropServices;
namespace UnityEngine {
 public enum CursorLockMode{None,Locked,Confined}
 public static class Cursor{public static CursorLockMode lockState;public static bool visible;}
 public static class Debug{public static int Errors;public static void LogError(object message){Errors++;}}
}
'''
HELPER_TEST=COMMON+'''
class Test {
 static void Check(bool ok,string reason){if(!ok)throw new Exception(reason);}
 static void Main(){
  var all=OrangePC.NativeDialogs.DesktopFilePicker.NormalizePatterns(null);Check(all.Length==1&&all[0]=="*.*","all files");
  var p=OrangePC.NativeDialogs.DesktopFilePicker.NormalizePatterns(new[]{".pc","opc"});Check(string.Join(";",p)=="*.pc;*.opc","save extensions");
  p=OrangePC.NativeDialogs.DesktopFilePicker.NormalizePatterns(new[]{"image/*",".PNG","image/jpeg"});Check(string.Join(";",p)=="*.png;*.jpg;*.jpeg","image MIME/dedup");
  p=OrangePC.NativeDialogs.DesktopFilePicker.NormalizePatterns(new[]{"public.image","public.jpeg"});Check(p.Length==3,"iOS image UTI");
  p=OrangePC.NativeDialogs.DesktopFilePicker.NormalizePatterns(new[]{"video/*","audio/*"});Check(Array.IndexOf(p,"*.mp4")>=0&&Array.IndexOf(p,"*.wav")>=0,"media filters");
  p=OrangePC.NativeDialogs.DesktopFilePicker.NormalizePatterns(new[]{"public.item","public.content"});Check(p[0]=="*.*","default mobile types");
  p=OrangePC.NativeDialogs.DesktopFilePicker.NormalizePatterns(new[]{"weird/path;$(bad)"});Check(p.Length==0,"unsafe filter input");
  string f=OrangePC.NativeDialogs.DesktopFilePicker.WindowsFilter(new[]{"*.pc","*.opc"});Check(f.EndsWith("\\0\\0")&&f.Contains("*.pc;*.opc\\0"),"Win32 filter termination");
  Check(!f.Contains("All files")&&!f.Contains("*.*"),"restricted filter still exposes All files");
  var editor=OrangePC.NativeDialogs.DesktopFilePicker.EditorFilters(new[]{"image/*"});Check(editor.Length==2&&editor[1]=="png,jpg,jpeg","editor image filter");
  editor=OrangePC.NativeDialogs.DesktopFilePicker.EditorFilters(new[]{".pc",".opc"});Check(editor[1]=="pc,opc","editor save filter");

  p=OrangePC.NativeDialogs.DesktopFilePicker.NormalizePatterns(new[]{"*/*",".pc",".opc"});Check(p.Length==2,"wildcard widened explicit save formats");
  Check(OrangePC.NativeDialogs.DesktopFilePicker.IsAllowedPath("PHOTO.PNG",new[]{"*.png","*.jpg"}),"uppercase image extension");
  Check(!OrangePC.NativeDialogs.DesktopFilePicker.IsAllowedPath("video.mp4",new[]{"*.png","*.jpg"}),"video bypassed image filter");
  Check(!OrangePC.NativeDialogs.DesktopFilePicker.IsAllowedPath("image.png.exe",new[]{"*.png"}),"double extension bypass");
  Check(!OrangePC.NativeDialogs.DesktopFilePicker.IsAllowedPath("a.sav",new[]{"*.pc","*.opc"}),"unsupported save format");
  Check(OrangePC.NativeDialogs.DesktopFilePicker.IsAllowedPath("мир.OPC",new[]{"*.pc","*.opc"}),"valid OPC format");
  string unicode="C:\\\\Игры\\\\Пример мира.opc";
  var result=OrangePC.NativeDialogs.DesktopFilePicker.ParseWindowsSelection(unicode+"\\0\\0");Check(result.Length==1&&result[0]==unicode,"Unicode path");
  result=OrangePC.NativeDialogs.DesktopFilePicker.ParseWindowsSelection("C:\\\\Save\\0one.pc\\0два.opc\\0\\0");Check(result.Length==2&&result[1]==Path.Combine("C:\\\\Save","два.opc"),"multi-select");
  Check(OrangePC.NativeDialogs.DesktopFilePicker.ParseWindowsSelection("\\0\\0")==null,"cancel parse");
#if UNITY_STANDALONE_WIN
  var type=typeof(OrangePC.NativeDialogs.DesktopFilePicker).GetNestedType("OpenFileName",BindingFlags.NonPublic);
  Check(Marshal.SizeOf(type)==(IntPtr.Size==8?152:88),"Win32 ABI size");
  Check(Marshal.OffsetOf(type,"file").ToInt32()==(IntPtr.Size==8?48:28),"Win32 file pointer offset");
  if(Environment.OSVersion.Platform!=PlatformID.Win32NT){
   UnityEngine.Cursor.lockState=UnityEngine.CursorLockMode.Locked;UnityEngine.Cursor.visible=false;
   result=OrangePC.NativeDialogs.DesktopFilePicker.PickFiles("Test",new[]{".opc"},false);
   Check(result==null&&!OrangePC.NativeDialogs.DesktopFilePicker.IsBusy,"error cancellation / busy reset");
   Check(UnityEngine.Cursor.lockState==UnityEngine.CursorLockMode.Locked&&!UnityEngine.Cursor.visible,"cursor state not restored");
  }
#endif
  Console.WriteLine("PASS: desktop backend compilation, MIME/UTI filters, Unicode/multiple selection, cancellation and platform-safe structure");
 }
}
'''

def routing_test():
    fp='\n'.join(method(FP,n) for n in ['CanPickMultipleFiles','IsFilePickerBusy','PickFile','PickMultipleFiles'])
    ng='\n'.join(method(NG,n) for n in ['CanSelectMultipleFilesFromGallery','CanSelectMultipleMediaTypesFromGallery','IsMediaPickerBusy','GetImageFromGallery','GetVideoFromGallery','GetMixedMediaFromGallery','GetImagesFromGallery','DesktopMediaTypes','GetMediaFromGallery','GetMultipleMediaFromGallery'])
    return COMMON+'''
namespace OrangePC.NativeDialogs {
 public static class DesktopFilePicker {
  public static bool IsBusy;public static int Calls;public static bool Multiple;public static string Title;public static string[] Types;
  public static string[] Result=new[]{"C:/тест/image.png"};
  public static string[] PickFiles(string title,string[] types,bool multiple){Calls++;Title=title;Types=types;Multiple=multiple;return Result;}
 }
}
static class NativeFilePicker {
 public enum Permission{Denied,Granted,ShouldAsk}
 public delegate void FilePickedCallback(string path);public delegate void MultipleFilesPickedCallback(string[] paths);
 static void RequestPermissionAsync(Action<Permission> done,bool read=true){done(Permission.Granted);}
'''+fp+'''
}
static class NativeGallery {
 public enum Permission{Denied,Granted,ShouldAsk} public enum PermissionType{Read,Write}
 [Flags] public enum MediaType{Image=1,Video=2,Audio=4}
 public delegate void MediaPickCallback(string path);public delegate void MediaPickMultipleCallback(string[] paths);
 static void RequestPermissionAsync(Action<Permission> done,PermissionType type,MediaType media){done(Permission.Granted);}
'''+ng+'''
}
class Test {
 static void Check(bool ok,string why){if(!ok)throw new Exception(why);}
 static void Main(){
  int callbacks=0;string path=null;int thread=System.Threading.Thread.CurrentThread.ManagedThreadId;
  NativeFilePicker.PickFile(p=>{callbacks++;path=p;Check(thread==System.Threading.Thread.CurrentThread.ManagedThreadId,"callback left Unity thread");},".pc",".opc");
  Check(callbacks==1&&path=="C:/тест/image.png"&&OrangePC.NativeDialogs.DesktopFilePicker.Calls==1,"file picker still cancels on desktop");
  NativeGallery.GetImageFromGallery(p=>{callbacks++;path=p;},"Choose image");Check(callbacks==2&&OrangePC.NativeDialogs.DesktopFilePicker.Types[0]=="image/*","image not routed");
  NativeGallery.GetVideoFromGallery(p=>{callbacks++;},"Choose video");Check(callbacks==3&&OrangePC.NativeDialogs.DesktopFilePicker.Types[0]=="video/*","video not routed");
  NativeGallery.GetMixedMediaFromGallery(p=>{callbacks++;},NativeGallery.MediaType.Image|NativeGallery.MediaType.Video,"Choose media");Check(OrangePC.NativeDialogs.DesktopFilePicker.Types.Length==2,"mixed media filter");
  NativeFilePicker.PickMultipleFiles(p=>{callbacks++;Check(p.Length==1,"multi path");},".opc");Check(OrangePC.NativeDialogs.DesktopFilePicker.Multiple,"multiple files not routed");
  NativeGallery.GetImagesFromGallery(p=>{callbacks++;},"Choose images");Check(OrangePC.NativeDialogs.DesktopFilePicker.Multiple,"multiple images not routed");
  OrangePC.NativeDialogs.DesktopFilePicker.Result=null;NativeFilePicker.PickFile(p=>{callbacks++;Check(p==null,"cancel path");});
  int before=OrangePC.NativeDialogs.DesktopFilePicker.Calls;OrangePC.NativeDialogs.DesktopFilePicker.IsBusy=true;
  NativeGallery.GetImageFromGallery(p=>{callbacks++;Check(p==null,"busy result");});Check(before==OrangePC.NativeDialogs.DesktopFilePicker.Calls,"duplicate modal dialog");
  Check(callbacks==8,"callback count");
  Console.WriteLine("PASS: actual plugin methods route save/image/video/mixed/multiple picks; cancel and busy callbacks fire once on the caller thread");
 }
}
'''

def main():
    for name in ['NativeFilePicker','NativeGallery']:
        config=json.loads((ROOT/'Assets/Plugins'/name/(name+'.Runtime.asmdef')).read_text())
        assert 'OrangePC.NativeDialogs' in config['references']
    assert json.loads((HELPER.parent/'OrangePC.NativeDialogs.asmdef').read_text())['name']=='OrangePC.NativeDialogs'
    with tempfile.TemporaryDirectory(prefix='desktop-picker-tests-') as tmp:
        folder=Path(tmp)
        for define in ['UNITY_STANDALONE_WIN','UNITY_STANDALONE_OSX','UNITY_STANDALONE_LINUX']:
            print('Checking',define,flush=True)
            compile_run(folder,HELPER_TEST,define,actual=True)
            compile_run(folder,routing_test(),define)
    print('Desktop picker tests passed. Native GUI selection still requires a check in a real Windows/macOS/Linux player.')
if __name__=='__main__':main()
