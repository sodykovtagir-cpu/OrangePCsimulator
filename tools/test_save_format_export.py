#!/usr/bin/env python3
"""Compile the real FileInformation.Export method with an isolated native-dialog substitute."""
from pathlib import Path
import re,subprocess,tempfile
ROOT=Path(__file__).resolve().parents[1]
CS=ROOT/'Assets/Scripts/Assembly-CSharp'

def main():
    source=(CS/'FileInformation.cs').read_text()
    start=source.index('\tpublic void Export()');end=source.index('\n\tpublic void AskDeleteMessage()',start)
    export=source[start:end]
    assert '"sav"' not in export
    imports=(CS/'FileMenu.cs').read_text()
    assert 'string[] exts = new string[] { ".pc", ".opc" };' in imports
    assert 'if (ext == ".pc")' in imports and 'else if (ext == ".opc")' in imports
    harness='''using System;using System.IO;
public static class Debug {public static void LogWarning(string s){}}
class MessageBox {public int Count;public void Show(string text){Count++;}}
class Loader {public string Path;}
class Load {public Loader loader=new Loader();}
namespace OrangePC.NativeDialogs {
 public static class DesktopFilePicker {public static string Result;public static string[] Types;public static int Calls;
 public static string SaveFile(string title,string name,string[] types){Calls++;Types=types;return Result;}}
}
class ExportHost {public Load load=new Load();public MessageBox messageBox=new MessageBox();
'''+export+'''
}
class Tests {
 static void Check(bool ok,string m){if(!ok)throw new Exception(m);}
 static void Main(string[] args){
  string folder=args[0];var host=new ExportHost();host.load.loader.Path=Path.Combine(folder,"исходный мир.opc");File.WriteAllText(host.load.loader.Path,"real-opc-data");
  var target=Path.Combine(folder,"копия мира.opc");OrangePC.NativeDialogs.DesktopFilePicker.Result=target;host.Export();
  Check(File.ReadAllText(target)=="real-opc-data","export did not copy the save");Check(OrangePC.NativeDialogs.DesktopFilePicker.Types.Length==1&&OrangePC.NativeDialogs.DesktopFilePicker.Types[0]==".opc","wrong exporter format");
  int errors=host.messageBox.Count;OrangePC.NativeDialogs.DesktopFilePicker.Result=null;host.Export();Check(host.messageBox.Count==errors,"cancel reported as permission error");
  string wrong=Path.Combine(folder,"не менять.pc");File.WriteAllText(wrong,"keep-this-file");OrangePC.NativeDialogs.DesktopFilePicker.Result=wrong;host.Export();
  Check(File.ReadAllText(wrong)=="keep-this-file"&&host.messageBox.Count==errors+1,"OPC was renamed/overwritten as PC");
  host.load.loader.Path=Path.Combine(folder,"legacy.pc");File.WriteAllText(host.load.loader.Path,"real-pc-data");target=Path.Combine(folder,"legacy copy.PC");OrangePC.NativeDialogs.DesktopFilePicker.Result=target;host.Export();
  Check(File.ReadAllText(target)=="real-pc-data"&&OrangePC.NativeDialogs.DesktopFilePicker.Types[0]==".pc","legacy source extension not preserved");
  int calls=OrangePC.NativeDialogs.DesktopFilePicker.Calls;host.load.loader.Path=Path.Combine(folder,"wrong.txt");host.Export();Check(calls==OrangePC.NativeDialogs.DesktopFilePicker.Calls,"unsupported source reached export");
  Console.WriteLine("PASS: real save export, Unicode paths, cancellation, source-format preservation and rejection of .sav/.txt/extension substitution");
 }
}'''
    with tempfile.TemporaryDirectory(prefix='save-format-tests-') as tmp:
        folder=Path(tmp);script=folder/'test.cs';script.write_text(harness);exe=folder/'test.exe'
        subprocess.run(['mcs','-define:UNITY_STANDALONE_WIN','-out:'+str(exe),str(script)],check=True)
        subprocess.run(['mono',str(exe),str(folder)],check=True)
    print('Save-format tests passed. Native Windows dialog interaction was not run here.')
if __name__=='__main__':main()
