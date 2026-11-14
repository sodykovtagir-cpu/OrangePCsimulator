using System;
namespace PC.Component.Software.Sites
{
 public static class SiteTransfer
 {
  public static float Delay(float megabytes,float internetSeconds,float capacity,float load,float hostMBps,int online)
  {
   double cpu=Math.Max(.1,Math.Min(20,capacity/1000d));
   double busy=1+Math.Max(0,Math.Min(1,load))*8+Math.Max(0,online-1)*.25;
   double rate=Math.Max(.02,Math.Min(Math.Max(.02,hostMBps),cpu*8)/busy);
   return (float)(.05+Math.Max(internetSeconds,Math.Max(0,megabytes)/rate));
  }
 }
}
