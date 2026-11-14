using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine.Networking;
public partial class WorkshopClient
{
 // Encode UTF-8 bytes directly: empty fields are legal and this avoids the
 // legacy Uri.EscapeDataString length limit for multi-megabyte packages.
 static void SiteEscape(StringBuilder b,string value)
 {
  const string hex="0123456789ABCDEF";foreach(byte c in Encoding.UTF8.GetBytes(value??""))
   if(c>=65&&c<=90||c>=97&&c<=122||c>=48&&c<=57||c==45||c==95||c==46||c==126)b.Append((char)c);
   else {b.Append('%');b.Append(hex[c>>4]);b.Append(hex[c&15]);}
 }
 static UnityWebRequest SiteForm(string action,Dictionary<string,string> values)
 {
  var body=new StringBuilder("action=");SiteEscape(body,action);
  if(values!=null)foreach(var pair in values){body.Append('&');SiteEscape(body,pair.Key);body.Append('=');SiteEscape(body,pair.Value);}
  var req=new UnityWebRequest("https://orangepcsimu.byethost4.com/sites/api.php?i=1",UnityWebRequest.kHttpVerbPOST);
  req.uploadHandler=new UploadHandlerRaw(Encoding.UTF8.GetBytes(body.ToString()));req.uploadHandler.contentType="application/x-www-form-urlencoded";
  req.downloadHandler=new DownloadHandlerBuffer();req.chunkedTransfer=false;return req;
 }
 public void SiteRequest(string action,Dictionary<string,string> values,Action<string,string> done)
 {StartCoroutine(SiteRequestRoutine(action,values,ServerAccounts.Token,done));}
 IEnumerator SiteRequestRoutine(string action,Dictionary<string,string> values,string token,Action<string,string> done)
 {
  for(int attempt=0;attempt<2;attempt++)
  {
   yield return BypassByethost();if(token!=ServerAccounts.Token){done(null,"Аккаунт изменился");yield break;}
   using(var req=SiteForm(action,values))
   {
    // Keep normal platform certificate validation for the new API.
    ApplyCookie(req);if(!string.IsNullOrEmpty(token))req.SetRequestHeader("X-Auth-Token",token);req.timeout=25;var op=SafeSend(req);if(op==null){done(null,"Сервис сайтов недоступен");yield break;}
    while(!op.isDone){if(req.downloadedBytes>12582912){req.Abort();break;}yield return null;}
    string body=req.downloadHandler!=null?req.downloadHandler.text:null;
    bool challenge=body!=null&&body.IndexOf("toNumbers",StringComparison.Ordinal)>=0;
    if(challenge&&attempt==0){byetCookie=null;continue;}
    if(string.IsNullOrEmpty(body)||body.Length>12582912||challenge||!body.TrimStart().StartsWith("{",StringComparison.Ordinal)){done(null,"Сервис сайтов недоступен");yield break;}
    done(body,null);yield break;
   }
  }
 }
}
