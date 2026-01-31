// Decompiled with JetBrains decompiler
// Type: KSA.Controller
// Assembly: KSA, Version=2026.1.10.3353, Culture=neutral, PublicKeyToken=null
// MVID: A52EEB80-D3DB-4A39-B5C4-06AEBED05F0D
// Assembly location: C:\Program Files\Kitten Space Agency\KSA.dll

using Brutal.GlfwApi;
using Brutal.Numerics;
using RenderCore.Input;
using System;

#nullable enable
namespace KSA;

public abstract class Controller : 
  IReceivesFrameUpdate,
  IKeyListener,
  IMouseButtonListener,
  IScrollListener,
  ICursorPosListener,
  ICursorEnterListener
{
  protected readonly Transform3D Transform;
  protected readonly string Name;
  public Camera Camera;

  public Controller(Camera camera, string name)
  {
    this.Camera = camera;
    this.Transform = (Transform3D) this.Camera;
    this.Name = name;
  }

  public virtual bool OnKey(GlfwKeyEvent keyEvent) => false;

  public virtual bool OnMouseButton(
    GlfwWindow window,
    GlfwMouseButton button,
    GlfwButtonAction action,
    GlfwModifier mods)
  {
    return false;
  }

  public virtual bool OnCursorPos(GlfwWindow window, double2 pos) => false;

  public virtual bool OnCursorEnter(GlfwWindow window, bool entered) => false;

  public virtual bool OnScroll(GlfwWindow window, double2 offset) => false;

  public virtual bool OnGamepadConnected(int inGamepad) => false;

  public virtual bool OnGamepadDisconnected(int inGamepad) => false;

  public virtual bool OnCursorEnter(Guid inGuid, bool inEntered) => false;

  public virtual void OnFrame(Viewport inViewport, double inDeltaTime)
  {
  }

  public virtual void OnDrawUi(Viewport inViewport)
  {
  }

  public virtual void OnDrawStatisticsUi()
  {
  }

  public virtual GlfwCursorMode GetCursorMode() => GlfwCursorMode.Normal;

  public virtual void OnSwitchOn(CameraMode lastMode)
  {
  }

  public virtual void OnSwitchOff(CameraMode nextMode)
  {
  }

  public virtual bool IsMouseDrag() => false;

  public virtual void CancelMouseDrag()
  {
  }
}
