using System;
namespace PC.Component
{
 public static class CpuThermals
 {
  // Bare silicon/heat spreader is not a heatsink. Powered passive loss must
  // not cancel the generated heat at only a few degrees above room temperature.
  public static float PassiveStep(float temperature,float ambient,bool powered,float dt)
  {return ambient+(temperature-ambient)*(float)Math.Exp(-(powered?.012f:.12f)*Math.Max(0,dt));}
 }
}
