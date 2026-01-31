#region Assembly KSA, Version=2026.1.10.3353, Culture=neutral, PublicKeyToken=null
// C:\Program Files\Kitten Space Agency\KSA.dll
// Decompiled with ICSharpCode.Decompiler 9.1.0.7988
#endregion

using System;
using Brutal.Collections;
using Brutal.ImGuiApi;
using Brutal.Numerics;
using Brutal.VulkanApi;
using Core;

namespace KSA;

public class Viewport : IDisposable
{
  private Renderer _renderer;

  public CameraMode Mode;

  public bool Visible = false;

  public bool Hovered = false;

  public bool MenuBarInUse = false;

  public uint ImGuiID;

  public float2 Position;

  public int2 Size;

  public int2 NewSize;

  public int Index;

  public Camera BaseCamera;

  public Camera MapCamera;

  public FlyController FlyController;

  public OrbitController OrbitController;

  public MapController MapController;

  public RenderTarget? OffscreenTarget;

  public RenderPassState OffscreenPass;

  public RenderTarget? MainTarget;

  public RenderPassState MainPass;

  private bool _ownsRenderTargets = false;

  private ImTextureRef _imguiViewportTextureId;

  private readonly VkSampler _sampler;

  private readonly string _viewportName;

  public int Width => Size.X;

  public int Height => Size.Y;

  public string ViewportName => _viewportName;

  public Viewport(Renderer renderer, Camera camera, Camera mapCamera, int2 size, VkSampler viewportSampler, int index)
  {
    Index = index;
    _renderer = renderer;
    _sampler = viewportSampler;
    _viewportName = $"Camera {Index}";
    BaseCamera = camera;
    MapCamera = mapCamera;
    FlyController = new FlyController(camera);
    OrbitController = new OrbitController(camera);
    MapController = new MapController(mapCamera, "Map");
    RenderPassState val = new RenderPassState();
    val3.Depth = 0f;
    reference4 = VkClearValue.op_Implicit(val3);
    val4.ClearValues = new NativeList<VkClearValue>(global::_003CPrivateImplementationDetails_003E.InlineArrayAsReadOnlySpan<_003C_003Ey__InlineArray2<VkClearValue>, VkClearValue>(in buffer2, 2));
    OffscreenPass = val4;
    Resize(size);
  }

  public void Dispose() { /* omitted */}

  public void OnFrame(double dt)
  {
    GetActiveController().OnFrame(this, dt);
    GetCamera().OnFrame(dt);
  }

  public void BuildRenderTarget() { /* omitted */}

  public void DrawImGui()
  {
    if (!Visible)
    {
      Hovered = false;
      return;
    }

    if (Hovered && !ImGui.IsMouseDragging(ImGuiMouseButton.Left) && !ImGui.IsMouseDragging(ImGuiMouseButton.Right))
    {
      Hovered = false;
    }

    float num = 50f;
    float2 @float = new float2(ImGui.GetMainViewport().Size.X - (float)Size.X - num, num);
    ImGui.SetNextWindowPos(ImGui.GetMainViewport().Pos + @float, ImGuiCond.Once, (float2?)null);
    if (ImGui.Begin(_viewportName, ref Visible, ImGuiWindowFlags.NoSavedSettings))
    {
      ImGuiID = ImGui.GetWindowViewport().ID;
      Position = ImGui.GetCursorScreenPos();
      float2 imageSize = new float2(Size.X, Size.Y);
      float2 contentRegionAvail = ImGui.GetContentRegionAvail();
      if (ImGui.IsWindowHovered() && ImGui.IsMouseReleased(ImGuiMouseButton.Left))
      {
        NewSize = new int2((int)contentRegionAvail.X, (int)contentRegionAvail.Y);
      }

      ImGui.SetNextItemAllowOverlap();
      ImGui.ImageWithBg(_imguiViewportTextureId, in imageSize, (float2?)null, (float2?)null, (float4?)null, (float4?)null);
      ImGui.SetCursorScreenPos(in Position);
      ImGui.SetNextItemAllowOverlap();
      ImGui.InvisibleButton("InputBlocker"u8, in imageSize);
      bool flag = ImGui.IsItemHovered(ImGuiHoveredFlags.RectOnly);
      if (!Hovered)
      {
        Hovered = MenuBarInUse || flag;
      }

      if (Hovered && !MenuBarInUse)
      {
        ImGui.SetNextFrameWantCaptureKeyboard(wantCaptureKeyboard: false);
        ImGui.SetNextFrameWantCaptureMouse(wantCaptureMouse: false);
        ImGuiBackend.InputFallthrough = true;
      }

      Universe.OnDrawUi(this);
      GetCamera().NearbyCelestial?.DrawUiNearby(this, GetCamera().CurrentAltitudeKm);
      if (Hovered)
      {
        ImGui.GetForegroundDrawList().AddRect(in Position, Position + contentRegionAvail, KSAColor.Xkcd.Grey_Blue);
        ImGui.SetCursorScreenPos(in Position);
        Program.Instance.DrawMenuBar(this, Size.X);
      }
    }

    ImGui.End();
  }

  public void Resize(int2 newSize) { /* omitted */}

  public Controller GetActiveController()
  {
    return Mode switch
    {
      CameraMode.Orbit => OrbitController,
      CameraMode.Free => FlyController,
      CameraMode.Map => MapController,
      _ => throw new ArgumentOutOfRangeException(),
    };
  }

  public Camera GetCamera()
  {
    if (Mode == CameraMode.Map)
    {
      return MapCamera;
    }

    return BaseCamera;
  }

  public void SetCameraMode(CameraMode mode)
  {
    if (Mode != mode)
    {
      GetActiveController().OnSwitchOff(mode);
      CameraMode mode2 = Mode;
      Mode = mode;
      GetActiveController().OnSwitchOn(mode2);
    }
  }

  public bool NextCameraMode()
  {
    switch (Mode)
    {
      case CameraMode.Orbit:
        SetCameraMode(CameraMode.Free);
        return true;
      case CameraMode.Free:
        SetCameraMode(CameraMode.Orbit);
        return true;
      default:
        return false;
    }
  }
}