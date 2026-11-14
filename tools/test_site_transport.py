#!/usr/bin/env python3
"""Compile new transport partial; verify real encoder, no network is contacted."""
from pathlib import Path
import subprocess,tempfile
R=Path(__file__).resolve().parents[1]
stubs=r'''
using System;using System.Collections;using System.Collections.Generic;
public static class ServerAccounts{public static string Token="token";}
public partial class WorkshopClient {
 static string byetCookie;
 public object StartCoroutine(IEnumerator e){return null;}
 IEnumerator BypassByethost(){yield break;}
 static void ApplyCookie(UnityEngine.Networking.UnityWebRequest r){}
 static UnityEngine.Networking.UnityWebRequestAsyncOperation SafeSend(UnityEngine.Networking.UnityWebRequest r){return null;}
}
namespace UnityEngine.Networking {
 public class UnityWebRequestAsyncOperation {public bool isDone=true;}
 public class UploadHandler{public string contentType;}
 public class UploadHandlerRaw:UploadHandler{public byte[] data;public UploadHandlerRaw(byte[] b){data=b;}}
 public class DownloadHandler{public string text;}
 public class DownloadHandlerBuffer:DownloadHandler{}
 public class UnityWebRequest:IDisposable {
  public const string kHttpVerbPOST="POST";public string url,method;public bool chunkedTransfer;public int timeout;public ulong downloadedBytes;public UploadHandler uploadHandler;public DownloadHandler downloadHandler;
  public UnityWebRequest(string u,string m){url=u;method=m;}
  public void SetRequestHeader(string k,string v){}public void Abort(){}public void Dispose(){}
 }
}
class Test {
 static int count;static void Check(bool b){count++;if(!b)throw new Exception("transport "+count);}
 static void Main(){
 var form=typeof(WorkshopClient).GetMethod("SiteForm",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic);
 var doc="Русский & += % / ? 😀 "+new string('A',200000);
 var req=(UnityEngine.Networking.UnityWebRequest)form.Invoke(null,new object[]{"publish",new Dictionary<string,string>{{"url",""},{"document",doc}}});
 var body=System.Text.Encoding.UTF8.GetString(((UnityEngine.Networking.UploadHandlerRaw)req.uploadHandler).data);
 Check(body.StartsWith("action=publish&url=&document="));Check(Uri.UnescapeDataString(body.Substring(body.IndexOf("document=")+9))==doc);Check(!body.Contains("😀")&&!body.Contains("Русский"));Check(req.method=="POST"&&req.url.StartsWith("https://")&&!req.chunkedTransfer);Check(req.uploadHandler.contentType=="application/x-www-form-urlencoded");Check(req.downloadHandler is UnityEngine.Networking.DownloadHandlerBuffer);
 Console.WriteLine("PASS: "+count+" transport encoding checks: 200K+ fields, Unicode/surrogates, empty URL, reserved characters, URL form and HTTPS");
 }
}
'''
with tempfile.TemporaryDirectory()as td:
 p=Path(td);(p/'test.cs').write_text(stubs)
 subprocess.run(['mcs','-out:'+str(p/'test.exe'),str(p/'test.cs'),str(R/'Assets/Scripts/Assembly-CSharp/WorkshopSites.cs')],check=True)
 subprocess.run(['mono',str(p/'test.exe')],check=True,timeout=20)
