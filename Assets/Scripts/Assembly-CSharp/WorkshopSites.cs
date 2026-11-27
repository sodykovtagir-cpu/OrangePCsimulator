using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using UnityEngine;

public partial class WorkshopClient
{
 // The hosting service talks to this endpoint. Requests are sent from a worker
 // thread: a background sync must never cost the game a frame.
 const string SiteUrl = "https://orangepcsimu.byethost4.com/sites/api.php?i=1";

 // Encode UTF-8 bytes directly: empty fields are legal and this avoids the
 // legacy Uri.EscapeDataString length limit for multi-megabyte packages.
 static void SiteEscape(StringBuilder b,string value)
 {
  const string hex="0123456789ABCDEF";foreach(byte c in Encoding.UTF8.GetBytes(value??""))
   if(c>=65&&c<=90||c>=97&&c<=122||c>=48&&c<=57||c==45||c==95||c==46||c==126)b.Append((char)c);
   else {b.Append('%');b.Append(hex[c>>4]);b.Append(hex[c&15]);}
 }
 static string SiteBody(string action,Dictionary<string,string> values)
 {
  var body=new StringBuilder("action=");SiteEscape(body,action);
  if(values!=null)foreach(var pair in values){body.Append('&');SiteEscape(body,pair.Key);body.Append('=');SiteEscape(body,pair.Value);}
  return body.ToString();
 }
 /// <summary>
 /// One exchange with the sites API, performed away from the main thread. The
 /// result is published through volatile fields and read back in the coroutine.
 /// </summary>
 sealed class SiteExchange
 {
  public string request,token,cookie,response,error;
  public volatile bool finished;
  public void Run()
  {
   try
   {
    var req=(HttpWebRequest)WebRequest.Create(SiteUrl);
    req.Method="POST";req.ContentType="application/x-www-form-urlencoded";
    req.Timeout=20000;req.ReadWriteTimeout=20000;req.KeepAlive=true;
    req.UserAgent="Mozilla/5.0 OrangePCSimulator";
    var endpoint=new Uri(SiteUrl);req.Proxy=null;
    // The challenge cookie is sent as a single name=value pair.
    int split=cookie!=null?cookie.IndexOf('='):-1;
    if(split>0){var jar=new CookieContainer();jar.Add(endpoint,new Cookie(cookie.Substring(0,split),cookie.Substring(split+1)));req.CookieContainer=jar;}
    if(!string.IsNullOrEmpty(token))req.Headers["X-Auth-Token"]=token;
    byte[] bytes=Encoding.UTF8.GetBytes(request??"");
    req.ContentLength=bytes.Length;
    using(var stream=req.GetRequestStream())stream.Write(bytes,0,bytes.Length);
    using(var answer=req.GetResponse())
    using(var reader=new StreamReader(answer.GetResponseStream(),Encoding.UTF8))response=reader.ReadToEnd();
   }
   catch(WebException e)
   {
    // A 4xx/5xx still carries the JSON body: hand it over, the caller decides.
    var answer=e.Response;
    if(answer!=null)try{using(var reader=new StreamReader(answer.GetResponseStream(),Encoding.UTF8))response=reader.ReadToEnd();}catch{}
    if(string.IsNullOrEmpty(response))error=e.Message;
   }
   catch(Exception e){error=e.Message;}
   finally{finished=true;}
  }
 }
 public void SiteRequest(string action,Dictionary<string,string> values,Action<string,string> done)
 {StartCoroutine(SiteRequestRoutine(action,values,ServerAccounts.Token,done));}
 IEnumerator SiteRequestRoutine(string action,Dictionary<string,string> values,string token,Action<string,string> done)
 {
  for(int attempt=0;attempt<2;attempt++)
  {
   yield return BypassByethost();if(token!=ServerAccounts.Token){done(null,"Аккаунт изменился");yield break;}
   var exchange=new SiteExchange{request=SiteBody(action,values),token=token,cookie=byetCookie};
   var thread=new Thread(exchange.Run);thread.IsBackground=true;thread.Start();
   float deadline=Time.realtimeSinceStartup+26f;
   // Nothing here touches the network: the frame only checks a flag.
   while(!exchange.finished&&Time.realtimeSinceStartup<deadline)yield return null;
   if(!exchange.finished){done(null,"Сервис сайтов недоступен: таймаут");yield break;}
   string body=exchange.response;
   if(string.IsNullOrEmpty(body)&&exchange.error!=null){done(null,"Сервис сайтов недоступен");yield break;}
   bool challenge=body!=null&&body.IndexOf("toNumbers",StringComparison.Ordinal)>=0;
   if(challenge&&attempt==0){byetCookie=null;continue;}
   if(string.IsNullOrEmpty(body)||body.Length>12582912||challenge||!body.TrimStart().StartsWith("{",StringComparison.Ordinal)){done(null,"Сервис сайтов недоступен");yield break;}
   done(body,null);yield break;
  }
 }
}
