// Decompiled with JetBrains decompiler
// Type: KSA.OrbitController
// Assembly: KSA, Version=2026.1.10.3353, Culture=neutral, PublicKeyToken=null
// MVID: A52EEB80-D3DB-4A39-B5C4-06AEBED05F0D
// Assembly location: C:\Program Files\Kitten Space Agency\KSA.dll

using Brutal.GlfwApi;
using Brutal.ImGuiApi;
using Brutal.Numerics;
using KSA.Rendering.Sun;
using RenderCore.Input;
using System;

#nullable enable
namespace KSA;

public class OrbitController : Controller
{
  private static string[] _modeAlertTitle;
  public bool SprintFlag = false;
  public const double PAN_SENSITIVITY = 0.003;
  public float2 CursorPositionScreen = new float2(0.0f, 0.0f);
  public float2 CursorPositionScreenStartDrag = new float2(0.0f, 0.0f);
  private const double DISTANCE_POWER_SENSITIVITY = 1.1;
  public const double MIN_DISTANCE_POWER = 0.5;
  public bool IsDragging;
  public bool IsPotentiallyDragging;
  private Astronomical? _animStartFocused = (Astronomical) null;
  private doubleQuat _animStartRotationEcl = doubleQuat.Identity;
  private double _animStartDistance = 0.0;
  private double _animProgress = 1.0;
  private Astronomical? _lastFocused = (Astronomical) null;
  private CameraReferenceFrame? _lastReference = new CameraReferenceFrame?();
  private bool _lastIsEditing = false;
  private doubleQuat _lastFrame2Ecl = doubleQuat.Identity;
  private double _lastDistance = 0.0;
  private double3 _lastOffsetEcl = double3.Zero;
  private double3 _lastOffsetEditor = double3.Zero;
  private double3 _lastOffsetEditorFinal = double3.Zero;
  private double _lastFocusedRadius = 0.0;
  public bool AnimateFocusChange = false;
  public double Azimuth = 0.0;
  public double Elevation = 0.0;
  public double DistancePower = 3.0;

  public OrbitController(Camera camera, string name = "Orbit")
    : base(camera, name)
  {
    OrbitController._modeAlertTitle = new string[EnumCollections.CameraReferenceFrames.Length];
    for (int index = 0; index < EnumCollections.CameraReferenceFrames.Values.Length; ++index)
      OrbitController._modeAlertTitle[index] = "Camera " + EnumCollections.CameraReferenceFrames.GetNameFromIndex(index);
  }

  public override bool OnKey(GlfwKeyEvent keyEvent)
  {
    (GlfwWindow _, GlfwKeyAction action, GlfwKey glfwKey, GlfwModifier glfwModifier) = keyEvent;
    if (KSA.Input.MatchPressed(keyEvent, InputAction.ToggleMap))
    {
      Program.HoveredViewport.SetCameraMode(CameraMode.Map);
      return true;
    }
    if (KSA.Input.MatchPressed(keyEvent, InputAction.CameraReferenceFrame))
    {
      OrbitView orbitView = this.Camera.Following?.OrbitView;
      Astronomical following = this.Camera.Following;
      if (following == null || orbitView == null)
        return false;
      orbitView.ReferenceFrame = OrbitController.NextCameraReferenceFrame(following, orbitView.ReferenceFrame);
      return true;
    }
    if (KSA.Input.Contains(glfwKey, glfwModifier, InputAction.Sprint))
      this.SprintFlag = action != 0;
    return false;
  }

  public override bool OnMouseButton(
    GlfwWindow window,
    GlfwMouseButton button,
    GlfwButtonAction action,
    GlfwModifier mods)
  {
    switch (action)
    {
      case GlfwButtonAction.Release:
        if (button == GlfwMouseButton.Number2)
        {
          this.IsDragging = false;
          this.IsPotentiallyDragging = false;
          return true;
        }
        break;
      case GlfwButtonAction.Press:
        if (button == GlfwMouseButton.Number2 && (mods == (GlfwModifier) 0 || (mods & GlfwModifier.Shift) != 0))
        {
          this.IsPotentiallyDragging = true;
          this.CursorPositionScreenStartDrag = this.CursorPositionScreen;
          return true;
        }
        break;
      default:
        throw new ArgumentOutOfRangeException(nameof (action), (object) action, (string) null);
    }
    return false;
  }

  private static CameraReferenceFrame NextCameraReferenceFrame(
    Astronomical focused,
    CameraReferenceFrame current)
  {
    CameraReferenceFrame frame = current;
    do
    {
      if (true)
        ;
      CameraReferenceFrame cameraReferenceFrame;
      switch (frame)
      {
        case CameraReferenceFrame.Surface:
          cameraReferenceFrame = CameraReferenceFrame.Orbit;
          break;
        case CameraReferenceFrame.Orbit:
          cameraReferenceFrame = CameraReferenceFrame.Parent;
          break;
        case CameraReferenceFrame.Parent:
          cameraReferenceFrame = CameraReferenceFrame.Poles;
          break;
        case CameraReferenceFrame.Poles:
          cameraReferenceFrame = CameraReferenceFrame.Stars;
          break;
        case CameraReferenceFrame.Stars:
          cameraReferenceFrame = CameraReferenceFrame.Chase;
          break;
        case CameraReferenceFrame.Chase:
          cameraReferenceFrame = CameraReferenceFrame.Surface;
          break;
        default:
          cameraReferenceFrame = CameraReferenceFrame.Surface;
          break;
      }
      if (true)
        ;
      frame = cameraReferenceFrame;
    }
    while (!frame.IsValidFor(focused));
    return frame;
  }

  private void AlertCameraReference(CameraReferenceFrame frame)
  {
    Alert.Create(OrbitController._modeAlertTitle[(int) frame], (byte4) ref Color.Cyan, 2.0);
  }

  private doubleQuat GetFrame2Ecl(Astronomical focused, CameraReferenceFrame referenceFrame)
  {
    doubleQuat frame2Ecl = this._lastFrame2Ecl;
    switch (focused)
    {
      case Vehicle celestial1:
        switch (referenceFrame)
        {
          case CameraReferenceFrame.Surface:
            doubleQuat? enu2Cce = celestial1.GetEnu2Cce();
            if (enu2Cce.HasValue)
            {
              frame2Ecl = enu2Cce.Value;
              break;
            }
            break;
          case CameraReferenceFrame.Orbit:
            doubleQuat? lvlh2Cce = celestial1.GetLvlh2Cce();
            if (lvlh2Cce.HasValue)
            {
              frame2Ecl = doubleQuat.Concatenate(doubleQuat.CreateFromAxisAngle(double3.UnitX, Math.PI), lvlh2Cce.Value);
              break;
            }
            break;
          case CameraReferenceFrame.Parent:
            frame2Ecl = this.GetCarousel2Cce((IOrbiting) celestial1);
            break;
          case CameraReferenceFrame.Stars:
            frame2Ecl = doubleQuat.Identity;
            break;
          case CameraReferenceFrame.Chase:
            frame2Ecl = doubleQuat.Concatenate(doubleQuat.CreateFromAxisAngle(double3.UnitX, Math.PI), celestial1.Body2Cce);
            break;
        }
        break;
      case Celestial celestial2:
        switch (referenceFrame)
        {
          case CameraReferenceFrame.Surface:
            frame2Ecl = celestial2.GetCcf2Cce();
            break;
          case CameraReferenceFrame.Parent:
            frame2Ecl = this.GetCarousel2Cce((IOrbiting) celestial2);
            break;
          case CameraReferenceFrame.Poles:
            frame2Ecl = celestial2.GetCci2Cce();
            break;
          case CameraReferenceFrame.Stars:
            frame2Ecl = doubleQuat.Identity;
            break;
        }
        break;
      default:
        frame2Ecl = doubleQuat.Identity;
        break;
    }
    return frame2Ecl;
  }

  private doubleQuat GetCarousel2Cce(IOrbiting celestial)
  {
    doubleQuat cci2Cce = celestial.Orbit.Parent.GetCci2Cce();
    double3 vector1_1 = celestial.Orbit.StateVectors.PositionCci.Transform(cci2Cce);
    double3 vector2_1 = celestial.Orbit.StateVectors.VelocityCci.Transform(cci2Cce);
    double3 double3_1 = double3.Cross(vector1_1, vector2_1);
    double val1 = vector1_1.Length();
    double val2 = double3_1.Length();
    if (val1.IsNearlyZero() || val2.IsNearlyZero())
      return doubleQuat.Identity;
    double3 vector2_2 = -vector1_1 / val1;
    double3 vector1_2 = double3_1 / val2;
    double3 double3_2 = double3.Cross(vector1_2, vector2_2).Normalized();
    return doubleQuat.CreateFromRotationMatrix(new double4x4(vector2_2.X, vector2_2.Y, vector2_2.Z, 0.0, double3_2.X, double3_2.Y, double3_2.Z, 0.0, vector1_2.X, vector1_2.Y, vector1_2.Z, 0.0, 0.0, 0.0, 0.0, 1.0));
  }

  public override bool OnCursorPos(GlfwWindow window, double2 pos)
  {
    float2 float2_1 = float2.Pack(in pos);
    float2 float2_2 = float2_1 - this.CursorPositionScreen;
    this.CursorPositionScreen = float2_1;
    if (this.IsPotentiallyDragging && (double) (this.CursorPositionScreen - this.CursorPositionScreenStartDrag).LengthSquared() > 2.0)
    {
      this.IsDragging = true;
      this.IsPotentiallyDragging = false;
    }
    OrbitView orbitView;
    int num;
    if (this.IsDragging)
    {
      orbitView = this.Camera.Following?.OrbitView;
      num = orbitView != null ? 1 : 0;
    }
    else
      num = 0;
    if (num == 0)
      return false;
    orbitView.Azimuth -= (double) float2_2.X * 0.003;
    orbitView.Elevation -= (double) float2_2.Y * 0.003;
    this.Elevation = Math.Clamp(this.Elevation, -1.0 * Math.PI / 2.0, Math.PI / 2.0);
    orbitView.Elevation = Math.Clamp(orbitView.Elevation, -1.0 * Math.PI / 2.0, Math.PI / 2.0);
    if (Math.Abs(this.Azimuth) > 2.0 * Math.PI && Math.Abs(orbitView.Azimuth) > 2.0 * Math.PI)
    {
      this.Azimuth %= 2.0 * Math.PI;
      orbitView.Azimuth %= 2.0 * Math.PI;
    }
    return true;
  }

  public override bool OnScroll(GlfwWindow window, double2 offset)
  {
    OrbitView orbitView = this.Camera.Following?.OrbitView;
    if (orbitView != null)
    {
      if (offset.Y > 0.0)
      {
        if (this.SprintFlag)
          orbitView.DistancePower /= 2.2;
        else
          orbitView.DistancePower /= 1.1;
      }
      else if (this.SprintFlag)
        orbitView.DistancePower *= 2.2;
      else
        orbitView.DistancePower *= 1.1;
      double val2 = !this.Camera.Following.Id.Equals("Sol") ? 0.5 : SunRenderer.OrbitCamDistPow;
      orbitView.DistancePower = Math.Max(orbitView.DistancePower, val2);
    }
    return true;
  }

  public override void OnFrame(Viewport inViewport, double inDeltaTime)
  {
    Astronomical following = this.Camera.Following;
    if (following == null)
    {
      this._lastFocused = (Astronomical) null;
    }
    else
    {
      Astronomical focused = following;
      bool flag1 = Program.Editor != null;
      ref OrbitView local = ref following.OrbitView;
      double num1 = (focused is Vehicle vehicle1 ? (double) new float?(vehicle1.BoundingSphereRadius) : (double) new float?()) ?? focused.MeanRadius;
      double3 positionEcl = focused.GetPositionEcl();
      double3 double3_1 = double3.Zero;
      double3 double3_2 = double3.Zero;
      bool flag2 = false;
      Vehicle vehicle2;
      int num2;
      if (flag1)
      {
        vehicle2 = focused as Vehicle;
        num2 = vehicle2 != null ? 1 : 0;
      }
      else
        num2 = 0;
      if (num2 != 0)
      {
        double3_1 = -vehicle2.CenterOfMassAsmb.Transform(vehicle2.Asmb2Cce);
        double3_2 = Program.Editor.CameraOffset;
        if (double3_2 != this._lastOffsetEditorFinal)
          flag2 = true;
        this._lastOffsetEditorFinal = double3_2;
        if (num1 != this._lastFocusedRadius)
        {
          local.DistancePower = this._lastFocusedRadius / num1 * local.DistancePower;
          this.DistancePower = local.DistancePower;
        }
      }
      CameraReferenceFrame cameraReferenceFrame1 = !flag1 ? local.ReferenceFrame : CameraReferenceFrame.Chase;
      CameraReferenceFrame? lastReference = this._lastReference;
      CameraReferenceFrame cameraReferenceFrame2 = cameraReferenceFrame1;
      bool flag3 = !(lastReference.GetValueOrDefault() == cameraReferenceFrame2 & lastReference.HasValue);
      bool flag4 = this._lastIsEditing != flag1;
      bool flag5 = focused != this._lastFocused;
      if (flag3 && !flag4)
        this.AlertCameraReference(cameraReferenceFrame1);
      bool flag6 = true;
      if (flag5 | flag3 | flag4 | flag2 && this._lastFocused != null)
      {
        if (!this.AnimateFocusChange & flag5)
        {
          flag6 = false;
          this._animProgress = 1.0;
        }
        else
          this._animProgress = 0.0;
        if (this.Camera.Following.Id.Equals("Sol"))
          local.DistancePower = Math.Max(local.DistancePower, SunRenderer.OrbitCamDistPow);
        this.Azimuth = local.Azimuth;
        this.Elevation = local.Elevation;
        this.DistancePower = local.DistancePower;
        this._animStartFocused = this._lastFocused;
        this._animStartDistance = this._lastDistance;
        this._animStartRotationEcl = this.Transform.LocalRotation;
      }
      else
      {
        double num3 = 5.0;
        this.Azimuth = double.Lerp(this.Azimuth, local.Azimuth, num3 * inDeltaTime);
        this.Elevation = double.Lerp(this.Elevation, local.Elevation, num3 * inDeltaTime);
        this.DistancePower = double.Lerp(this.DistancePower, local.DistancePower, num3 * inDeltaTime);
      }
      doubleQuat frame2Ecl = this.GetFrame2Ecl(focused, cameraReferenceFrame1);
      double3 double3_3 = double3.UnitX.Transform(frame2Ecl);
      double3 double3_4 = double3.UnitZ.Transform(frame2Ecl);
      double num4 = this.DistancePower * num1;
      double3 vector1 = double3_3.Transform(QuaternionEx.AngleAxis(this.Azimuth, double3_4));
      double3 double3_5 = double3.Cross(vector1, double3_4).Normalized();
      double3 double3_6 = vector1.Transform(QuaternionEx.AngleAxis(this.Elevation, double3_5));
      double3 upEcl = double3.Cross(double3_5, double3_6).Normalized();
      doubleQuat doubleQuat = Camera.LookAtRotation(double3_6, upEcl);
      Astronomical animStartFocused = this._animStartFocused;
      double3 double3_7 = animStartFocused != null ? animStartFocused.GetPositionEcl() : positionEcl;
      if (flag6)
      {
        double num5 = 0.2;
        this._animProgress = Math.Clamp(this._animProgress + inDeltaTime / (double) GameSettings.Current.Interface.CameraJumpTime, 0.0, 1.0 + num5);
        double t = this._animProgress - num5;
        double animProgress = this._animProgress;
        double amount1 = MathEx.Smootherstep(t);
        double amount2 = MathEx.Smootherstep(animProgress);
        doubleQuat rotation = doubleQuat.Lerp(this._animStartRotationEcl, doubleQuat, amount1);
        double3 double3_8 = double3.Lerp(double3_7, positionEcl, amount2);
        double num6 = double.Lerp(this._animStartDistance, num4, amount2);
        double3 double3_9 = double3.Lerp(this._lastOffsetEcl, double3_1, amount2);
        double3 double3_10 = double3.Lerp(this._lastOffsetEditor, double3_2, amount2);
        this.Transform.LocalRotation = rotation;
        this.Transform.PositionEcl = double3_8 + double3_9 + double3_10 - (-double3.UnitZ).Transform(rotation) * num6;
        this._lastDistance = num6;
        this._lastOffsetEcl = double3_9;
        this._lastOffsetEditor = double3_10;
      }
      else
      {
        this.Transform.LocalRotation = doubleQuat;
        this.Transform.PositionEcl = positionEcl + double3_1 + double3_2 - (-double3.UnitZ).Transform(doubleQuat) * num4;
        this._lastDistance = num4;
        this._lastOffsetEcl = double3_1;
        this._lastOffsetEditor = double3_2;
        this._animStartFocused = focused;
        this._animStartDistance = num4;
        this._animStartRotationEcl = doubleQuat;
      }
      this.AnimateFocusChange = false;
      this._lastReference = new CameraReferenceFrame?(cameraReferenceFrame1);
      this._lastFocused = focused;
      this._lastIsEditing = flag1;
      this._lastFrame2Ecl = frame2Ecl;
      this._lastFocusedRadius = num1;
    }
  }

  public override bool OnGamepadConnected(int inGamepad) => false;

  public override bool OnGamepadDisconnected(int inGamepad) => false;

  public override bool OnCursorEnter(Guid inGuid, bool inEntered) => false;

  public override unsafe void OnDrawStatisticsUi()
  {
    // ISSUE: reference to a compiler-generated field
    ImGuiHelper.DrawTextWidget((ImString) new ReadOnlySpan<byte>((void*) &\u003CPrivateImplementationDetails\u003E.\u0034D69F92F24FC4BCA7526CA7ECED05CEEF44A080BF727C89AF1573B0121027F1B, 16 /*0x10*/), KSA.Input.GetAssignmentString(InputAction.CameraReferenceFrame));
    // ISSUE: reference to a compiler-generated field
    // ISSUE: reference to a compiler-generated field
    ImGuiHelper.DrawTextWidget((ImString) new ReadOnlySpan<byte>((void*) &\u003CPrivateImplementationDetails\u003E.\u00364AD7557F8714FD546F0D0F8F8ECCAB4CC0306F692FCC0E5384E656FD6A93D8F, 13), (ImString) new ReadOnlySpan<byte>((void*) &\u003CPrivateImplementationDetails\u003E.E96DDCCEE47D9BDEED64C1D95B513126BD1386E89ECD1B3E1AB7EA4CCFC95465, 6));
    if (this.Camera?.Following is Vehicle following)
      following.OnDrawStats();
    OrbitView orbitView = this.Camera?.Following?.OrbitView;
    if (orbitView == null)
      return;
    // ISSUE: reference to a compiler-generated field
    ImGuiHelper.DrawTextWidget((ImString) new ReadOnlySpan<byte>((void*) &\u003CPrivateImplementationDetails\u003E.D01DB1C2710AE67F268EB3E9964B6DC936356DD293083B90222A2F1550C13209, 9), orbitView.ReferenceFrame.ToString());
    // ISSUE: reference to a compiler-generated field
    ImGuiHelper.DrawTextWidget((ImString) new ReadOnlySpan<byte>((void*) &\u003CPrivateImplementationDetails\u003E.\u003208064CED26ABF7617C568C927A3E1376AC195815E33BDBEFE08BE20BD087E07, 7), (MathEx.ToOrbitAngle(orbitView.Azimuth) * (180.0 / Math.PI)).ToString("F3"));
    // ISSUE: reference to a compiler-generated field
    ImGuiHelper.DrawTextWidget((ImString) new ReadOnlySpan<byte>((void*) &\u003CPrivateImplementationDetails\u003E.E80D12EE22594750E91DC86C2DD8E3468898476E021E2B8E6367528064D0C897, 9), (MathEx.ToDeviationAngle(orbitView.Elevation) * (180.0 / Math.PI)).ToString("F3"));
    // ISSUE: reference to a compiler-generated field
    ImGuiHelper.DrawTextWidget((ImString) new ReadOnlySpan<byte>((void*) &\u003CPrivateImplementationDetails\u003E.A73A30CF2498F1A2913A81C069E04DA204971A3D38D61AFEC2386E3B637F13F1, 13), orbitView.DistancePower.ToString("F3"));
  }

  public override void OnDrawUi(Viewport inViewport)
  {
  }

  public override GlfwCursorMode GetCursorMode()
  {
    return !this.IsDragging ? GlfwCursorMode.Normal : GlfwCursorMode.Disabled;
  }

  public override void OnSwitchOn(CameraMode lastMode)
  {
    if (this.Camera.Following == null)
      this.Camera.SetFollow((Astronomical) Universe.WorldSun, false);
    if (lastMode != CameraMode.Free)
      return;
    Alert.Create("Orbit Camera", (byte4) ref Color.Yellow, 3.0);
  }

  public override void OnSwitchOff(CameraMode nextMode)
  {
    this.SprintFlag = false;
    this.IsDragging = false;
    this.IsPotentiallyDragging = false;
  }

  public override bool IsMouseDrag() => this.IsDragging;

  public override void CancelMouseDrag()
  {
    base.CancelMouseDrag();
    this.IsDragging = false;
    this.IsPotentiallyDragging = false;
  }
}
