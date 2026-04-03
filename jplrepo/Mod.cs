using System;
using StarMap.API;
using KSA;

namespace MeowSci.JplRepo;

[StarMapMod]
public class Mod
{
  public bool ImmediateUnload => false;

  [StarMapImmediateLoad]
  public void OnImmediateLoad() { }

  [StarMapAllModsLoaded]
  public void OnFullyLoaded()
  {
    try
    {
      Patcher.Patch();
    }
    catch (Exception ex)
    {
      Console.WriteLine($"jplrepo: Error during initialization: {ex.Message}");
    }
  }

  [StarMapBeforeGui]
  public void OnBeforeUi(double dt) { }

  [StarMapAfterGui]
  public void OnAfterUi(double dt) { }

  [StarMapUnload]
  public void Unload()
  {
    try
    {
      Patcher.Unload();
    }
    catch (Exception ex)
    {
      Console.WriteLine($"jplrepo: Error during unload: {ex.Message}");
    }
  }
}

