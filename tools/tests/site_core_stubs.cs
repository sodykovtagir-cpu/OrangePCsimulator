using System;
using System.Web.Script.Serialization;
namespace UnityEngine {
 public static class JsonUtility {
  static JavaScriptSerializer Serializer(){return new JavaScriptSerializer{MaxJsonLength=9*1024*1024,RecursionLimit=64};}
  public static string ToJson(object value){return Serializer().Serialize(value);}
  public static T FromJson<T>(string value){return Serializer().Deserialize<T>(value);}
 }
 public static class Random {public static float value{get{return .5f;}}public static int Range(int a,int b){return a;}}
}
