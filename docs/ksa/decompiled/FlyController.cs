// Decompiled with JetBrains decompiler
// Type: KSA.FlyController
// Assembly: KSA, Version=2026.1.10.3353, Culture=neutral, PublicKeyToken=null
// MVID: A52EEB80-D3DB-4A39-B5C4-06AEBED05F0D
// Assembly location: C:\Program Files\Kitten Space Agency\KSA.dll

using Brutal.GlfwApi;
using Brutal.ImGuiApi;
using Brutal.Numerics;
using KSA.Rendering.Sun;
using RenderCore.Input;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

#nullable enable
namespace KSA;

public class FlyController : Controller
{
  private FlyController.KeyInputFlags _flags;
  private const double DEFAULT_MULTIPLIER = 1.0;
  private const double DEFAULT_SPEED = 50.0;
  private const float ROLL_SCALE = 0.01f;
  public float3 lookTgt = new float3(0.0f, 0.0f, 0.0f);
  public float lookSharpness = 8f;
  private double _deltaTime;
  private const double SCROLL_SPEED_BASE = 2.0;
  private double _scrollPower = -5.0;
  private static readonly EnumLookup<FlyController.KeyInputFlags, InputAction> Lookup;
  private float2? crsLast = new float2?();
  private const float MIN_LERP = 1E-45f;
  private const int SCROLL_POWER_MAX = 50;
  private doubleQuat _lastFrame2Ecl = doubleQuat.Identity;
  public const double MINIMUM_ALTITUDE_METERS = 2.0;
  private doubleQuat _frame2Ecl = doubleQuat.Identity;
  private doubleQuat _offsetEcl = doubleQuat.Identity;
  private Celestial? _trackedCelestial;

  public FlyController(Camera camera, string name = "Fly")
    : base(camera, name)
  {
    this._flags = FlyController.KeyInputFlags.None;
  }

  public double SpeedMultiplier { get; set; } = 1.0;

  public double Speed { get; set; } = 50.0;

  public double RollSpeed { get; set; } = 25.0;

  public double FastSpeed { get; set; } = 100.0;

  public double GetCurrentSpeed()
  {
    return this.SpeedMultiplier * ((this._flags & FlyController.KeyInputFlags.Sprint) != FlyController.KeyInputFlags.None ? this.FastSpeed : this.Speed) * Math.Pow(2.0, this._scrollPower);
  }

  public void SetSpeed(double inSpeed, DistanceUnit unit)
  {
    switch (unit)
    {
      case DistanceUnit.Meters:
        this._scrollPower = Math.Log(inSpeed / (this.SpeedMultiplier * this.Speed), 2.0);
        break;
      case DistanceUnit.Kilometers:
        inSpeed *= 1000.0;
        goto case DistanceUnit.Meters;
      case DistanceUnit.AstronomicalUnits:
        inSpeed *= 149597870700.0;
        goto case DistanceUnit.Meters;
      case DistanceUnit.LightYears:
        inSpeed *= 9461000000000000.0;
        goto case DistanceUnit.Meters;
      default:
        throw new ArgumentOutOfRangeException(nameof (unit), (object) unit, (string) null);
    }
  }

  private bool IsAltDown => ImGui.GetIO().KeyAlt;

  private bool AddFlag(GlfwKeyEvent keyEvent, FlyController.KeyInputFlags flag)
  {
    InputAction mods = FlyController.Lookup.Get(flag);
    if (!KSA.Input.Contains(keyEvent, mods))
      return false;
    this._flags |= flag;
    return true;
  }

  private bool ClearFlag(GlfwKeyEvent keyEvent, FlyController.KeyInputFlags flag)
  {
    InputAction mods = FlyController.Lookup.Get(flag);
    if (!KSA.Input.Contains(keyEvent, mods))
      return false;
    this._flags &= ~flag;
    return true;
  }

  public override bool OnKey(GlfwKeyEvent keyEvent)
  {
    (GlfwWindow _, GlfwKeyAction glfwKeyAction, GlfwKey _, GlfwModifier _) = keyEvent;
    switch (glfwKeyAction)
    {
      case GlfwKeyAction.Release:
        if (this.ClearFlag(keyEvent, FlyController.KeyInputFlags.Forward) || this.ClearFlag(keyEvent, FlyController.KeyInputFlags.Backward) || this.ClearFlag(keyEvent, FlyController.KeyInputFlags.Left) || this.ClearFlag(keyEvent, FlyController.KeyInputFlags.Right) || this.ClearFlag(keyEvent, FlyController.KeyInputFlags.RollLeft) || this.ClearFlag(keyEvent, FlyController.KeyInputFlags.RollRight) || this.ClearFlag(keyEvent, FlyController.KeyInputFlags.Up) || this.ClearFlag(keyEvent, FlyController.KeyInputFlags.Down) || this.ClearFlag(keyEvent, FlyController.KeyInputFlags.Sprint))
          return true;
        goto case GlfwKeyAction.Repeat;
      case GlfwKeyAction.Press:
        return !Program.ConsoleWindow.IsOpen && (this.AddFlag(keyEvent, FlyController.KeyInputFlags.Forward) || this.AddFlag(keyEvent, FlyController.KeyInputFlags.Backward) || this.AddFlag(keyEvent, FlyController.KeyInputFlags.Left) || this.AddFlag(keyEvent, FlyController.KeyInputFlags.Right) || this.AddFlag(keyEvent, FlyController.KeyInputFlags.RollLeft) || this.AddFlag(keyEvent, FlyController.KeyInputFlags.RollRight) || this.AddFlag(keyEvent, FlyController.KeyInputFlags.Up) || this.AddFlag(keyEvent, FlyController.KeyInputFlags.Down) || this.AddFlag(keyEvent, FlyController.KeyInputFlags.Sprint));
      case GlfwKeyAction.Repeat:
        return false;
      default:
        throw new ArgumentOutOfRangeException("action", (object) glfwKeyAction, (string) null);
    }
  }

  public bool MouseMove { get; private set; }

  public override bool OnMouseButton(
    GlfwWindow window,
    GlfwMouseButton button,
    GlfwButtonAction action,
    GlfwModifier mods)
  {
    if (Universe.CurrentSystem != null)
    {
      foreach (Vehicle vehicle in Universe.CurrentSystem.Vehicles.GetList())
      {
        if (vehicle.OnMouseButton(window, button, action, mods))
          return true;
      }
    }
    switch (action)
    {
      case GlfwButtonAction.Release:
        if (button == GlfwMouseButton.Number1 && !this.IsAltDown)
        {
          this.MouseMove = false;
          return true;
        }
        break;
      case GlfwButtonAction.Press:
        if (button == GlfwMouseButton.Number1 && !this.IsAltDown)
        {
          this.MouseMove = true;
          return true;
        }
        break;
      default:
        throw new ArgumentOutOfRangeException(nameof (action), (object) action, (string) null);
    }
    return false;
  }

  public override bool OnCursorPos(GlfwWindow window, double2 pos)
  {
    float2 float2_1 = float2.Pack(in pos);
    this.crsLast.GetValueOrDefault();
    if (!this.crsLast.HasValue)
      this.crsLast = new float2?(float2_1);
    float2 float2_2 = float2_1 - this.crsLast.Value;
    this.crsLast = new float2?(float2_1);
    if (this.IsAltDown || !this.MouseMove || Program.IsWindowOpen)
      return false;
    double num = this._deltaTime * (double) GameSettings.GetLookSensitivity();
    this.lookTgt.X += float2_2.Y * (float) num;
    this.lookTgt.Y += float2_2.X * (float) num;
    return true;
  }

  public override bool OnScroll(GlfwWindow window, double2 offset)
  {
    this._scrollPower += offset.Y / 50.0;
    this._scrollPower = Math.Max(this._scrollPower, -50.0);
    this._scrollPower = Math.Min(this._scrollPower, 50.0);
    return true;
  }

  public override bool OnGamepadConnected(int inGamepad) => false;

  public override bool OnGamepadDisconnected(int inGamepad) => false;

  public override bool OnCursorEnter(Guid inGuid, bool entered) => false;

  public override unsafe void OnDrawStatisticsUi()
  {
    StringBuilder sb = new StringBuilder();
    KSA.Input.AppendString(sb, InputAction.Forward);
    sb.Append(' ');
    KSA.Input.AppendString(sb, InputAction.Backward);
    sb.Append(' ');
    KSA.Input.AppendString(sb, InputAction.Left);
    sb.Append(' ');
    KSA.Input.AppendString(sb, InputAction.Right);
    // ISSUE: reference to a compiler-generated field
    ImGuiHelper.DrawTextWidget((ImString) new ReadOnlySpan<byte>((void*) &\u003CPrivateImplementationDetails\u003E.\u0037B7E0AD695726FBFF582E332D86D7BF5F22E3F5DA1EC0541AE5820FFA88EE53A, 8), sb.ToString());
    sb.Clear();
    KSA.Input.AppendString(sb, InputAction.RollLeft);
    sb.Append(' ');
    KSA.Input.AppendString(sb, InputAction.RollRight);
    // ISSUE: reference to a compiler-generated field
    ImGuiHelper.DrawTextWidget((ImString) new ReadOnlySpan<byte>((void*) &\u003CPrivateImplementationDetails\u003E.A2F0E98CB0A8D3021F1D6C53C8AB590D5D04E129974A2FFA55C2598AFB6EA649, 4), sb.ToString());
    sb.Clear();
    KSA.Input.AppendString(sb, InputAction.CameraUp);
    sb.Append(' ');
    KSA.Input.AppendString(sb, InputAction.CameraDown);
    // ISSUE: reference to a compiler-generated field
    ImGuiHelper.DrawTextWidget((ImString) new ReadOnlySpan<byte>((void*) &\u003CPrivateImplementationDetails\u003E.\u0031D1135625500910D060FE43661CDEFB54207C06A7D45C5C9F0BE18C67765AE0C, 8), sb.ToString());
    sb.Clear();
    // ISSUE: reference to a compiler-generated field
    ImGuiHelper.DrawTextWidget((ImString) new ReadOnlySpan<byte>((void*) &\u003CPrivateImplementationDetails\u003E.BE526F3B3B48FFA4FDFBA0D89DEBF3353A3C8B9AC5CD00ED0DB16B1098D409D6, 6), KSA.Input.GetAssignmentString(InputAction.Sprint));
    // ISSUE: reference to a compiler-generated field
    // ISSUE: reference to a compiler-generated field
    ImGuiHelper.DrawTextWidget((ImString) new ReadOnlySpan<byte>((void*) &\u003CPrivateImplementationDetails\u003E.\u0036B83A78348B8C8826274EB79BB7A81FA3DB1FB5755FE103B99039418D74AB64C, 9), (ImString) new ReadOnlySpan<byte>((void*) &\u003CPrivateImplementationDetails\u003E.E75887F7BE5C86235E1AC8D40BB54F1BAAC53384E5D6F06DFE541C84279557EB, 6));
    // ISSUE: reference to a compiler-generated field
    // ISSUE: reference to a compiler-generated field
    ImGuiHelper.DrawTextWidget((ImString) new ReadOnlySpan<byte>((void*) &\u003CPrivateImplementationDetails\u003E.EC2FC56BAFBD233B37467CB458E637FB78B25541D11E5C311D213257EC56700B, 13), (ImString) new ReadOnlySpan<byte>((void*) &\u003CPrivateImplementationDetails\u003E.E5315C05F113F868287E34FB9B63EF6E9E6B403B85EC80AE83329B29BBF6643D, 4));
    // ISSUE: reference to a compiler-generated field
    // ISSUE: reference to a compiler-generated field
    ImGuiHelper.DrawTextWidget((ImString) new ReadOnlySpan<byte>((void*) &\u003CPrivateImplementationDetails\u003E.E12162310A0770BF51A5F11CC3647F03D50289E221F39BB0BAA0E8A47CB4426B, 13), (ImString) new ReadOnlySpan<byte>((void*) &\u003CPrivateImplementationDetails\u003E.\u00328C2E2E2DD0EE19DB6AA020BF1E6D9F9597C6E7A4AC66DAF22BBE12F87B42759, 12));
  }

  private doubleQuat GetFrame2Ecl(Astronomical focused, CameraReferenceFrame referenceFrame)
  {
    doubleQuat frame2Ecl = this._lastFrame2Ecl;
    switch (focused)
    {
      case Vehicle vehicle:
        switch (referenceFrame)
        {
          case CameraReferenceFrame.Surface:
            doubleQuat? enu2Cce = vehicle.GetEnu2Cce();
            if (enu2Cce.HasValue)
            {
              frame2Ecl = enu2Cce.Value;
              break;
            }
            break;
          case CameraReferenceFrame.Orbit:
            doubleQuat? lvlh2Cce = vehicle.GetLvlh2Cce();
            if (lvlh2Cce.HasValue)
            {
              frame2Ecl = doubleQuat.Concatenate(doubleQuat.CreateFromAxisAngle(double3.UnitX, Math.PI), lvlh2Cce.Value);
              break;
            }
            break;
          case CameraReferenceFrame.Parent:
            throw new NotImplementedException("Parent reference frame is not implemented for Fly Controller.");
          case CameraReferenceFrame.Poles:
            throw new InvalidOperationException("Poles reference frame is not valid for Vehicles.");
          case CameraReferenceFrame.Stars:
            frame2Ecl = doubleQuat.Identity;
            break;
          case CameraReferenceFrame.Chase:
            frame2Ecl = doubleQuat.Concatenate(doubleQuat.CreateFromAxisAngle(double3.UnitX, Math.PI), vehicle.Body2Cce);
            break;
        }
        break;
      case Celestial celestial:
        if (true)
          ;
        doubleQuat doubleQuat;
        switch (referenceFrame)
        {
          case CameraReferenceFrame.Surface:
            doubleQuat = celestial.GetCcf2Cce();
            break;
          case CameraReferenceFrame.Orbit:
          case CameraReferenceFrame.Chase:
            throw new InvalidOperationException("Orbit and Chase reference frames are not valid for Celestial bodies.");
          case CameraReferenceFrame.Poles:
            doubleQuat = celestial.GetCci2Cce();
            break;
          case CameraReferenceFrame.Stars:
            doubleQuat = doubleQuat.Identity;
            break;
          default:
            doubleQuat = frame2Ecl;
            break;
        }
        if (true)
          ;
        frame2Ecl = doubleQuat;
        break;
      default:
        frame2Ecl = doubleQuat.Identity;
        break;
    }
    return frame2Ecl;
  }

  public override void OnFrame(Viewport inViewport, double inDeltaTime)
  {
    this._deltaTime = inDeltaTime;
    if (this._flags != 0)
    {
      double3 vector = new double3(0.0);
      double3 forward = this.Camera.GetForward();
      double3 right = this.Camera.GetRight();
      double3 up = this.Camera.GetUp();
      if ((this._flags & FlyController.KeyInputFlags.Forward) != 0)
        vector += forward;
      if ((this._flags & FlyController.KeyInputFlags.Backward) != 0)
        vector -= forward;
      if ((this._flags & FlyController.KeyInputFlags.Left) != 0)
        vector -= right;
      if ((this._flags & FlyController.KeyInputFlags.Right) != 0)
        vector += right;
      if ((this._flags & FlyController.KeyInputFlags.Up) != 0)
        vector += up;
      if ((this._flags & FlyController.KeyInputFlags.Down) != 0)
        vector -= up;
      if (vector.LengthSquared() > 0.0)
      {
        double currentSpeed = this.GetCurrentSpeed();
        this.Transform.Translate(double3.Normalize(vector) * inDeltaTime * currentSpeed);
        this.ClampCamera();
      }
    }
    bool flag1 = (this._flags & FlyController.KeyInputFlags.RollLeft) != 0;
    bool flag2 = (this._flags & FlyController.KeyInputFlags.RollRight) != 0;
    if (flag1 ^ flag2)
    {
      float rollSensitivity = GameSettings.GetRollSensitivity();
      this.lookTgt.Z += (float) (this.RollSpeed * inDeltaTime * (flag2 ? (double) rollSensitivity : -(double) rollSensitivity));
    }
    if (this.Camera.Following is Celestial following)
    {
      this._frame2Ecl = this.GetFrame2Ecl((Astronomical) following, CameraReferenceFrame.Surface);
      this._trackedCelestial = following;
    }
    else
    {
      this._frame2Ecl = doubleQuat.Identity;
      this._trackedCelestial = (Celestial) null;
    }
    this.lookTgt = float3.Lerp(this.lookTgt, float3.Zero, this.lookSharpness * (float) inDeltaTime);
    this._offsetEcl *= QuaternionEx.AngleAxis((double) this.lookTgt.Z * 0.009999999776482582, Double3Ex.Forward) * QuaternionEx.AngleAxis(-(double) this.lookTgt.Y * 0.009999999776482582, Double3Ex.Up) * QuaternionEx.AngleAxis(-(double) this.lookTgt.X * 0.009999999776482582, Double3Ex.Right);
    this.Transform.LocalRotation = this._frame2Ecl * this._offsetEcl;
  }

  private void ClampCamera()
  {
    Camera camera = Program.GetCamera();
    if (camera.Following != null && camera.Following.Id.Equals("Sol"))
    {
      double3 positionEgo = camera.GetPositionEgo((IPosition) Universe.WorldSun);
      if (positionEgo.Length() >= SunRenderer.MeshRadius + 2.0)
        return;
      this.Transform.PositionEcl = -positionEgo.Normalized() * (SunRenderer.MeshRadius + 2.0);
    }
    else
    {
      Celestial nearbyCelestial = Program.GetNearbyCelestial();
      double num = Program.GetCurrentAltitudeKm() * 1000.0;
      if (nearbyCelestial == null || camera == null || num > 2.0)
        return;
      double3 positionDirCce = nearbyCelestial.GetPositionCce(camera).Normalized();
      this.Transform.PositionEcl = nearbyCelestial.GetSurfacePositionEclFromDirCce(positionDirCce) + positionDirCce * 2.0;
    }
  }

  public void CacheOffset()
  {
    if (this.Camera.Following is Celestial following)
      this._offsetEcl = this.GetFrame2Ecl((Astronomical) following, CameraReferenceFrame.Surface).Inverse() * this.Transform.LocalRotation;
    else
      this._offsetEcl = this.Transform.LocalRotation;
  }

  public override GlfwCursorMode GetCursorMode()
  {
    return !this.IsAltDown && !Program.IsWindowOpen ? GlfwCursorMode.Disabled : GlfwCursorMode.Normal;
  }

  public void SetDefaultSpeed() => this.SetSpeed(1000.0, DistanceUnit.Meters);

  public override void OnSwitchOn(CameraMode lastMode)
  {
    this.CacheOffset();
    Alert.Create("Free Camera", (byte4) ref Color.Yellow, 3.0);
  }

  static FlyController()
  {
    int num = 9;
    List<EnumMapping<FlyController.KeyInputFlags, InputAction>> enumMappingList = new List<EnumMapping<FlyController.KeyInputFlags, InputAction>>(num);
    CollectionsMarshal.SetCount<EnumMapping<FlyController.KeyInputFlags, InputAction>>(enumMappingList, num);
    Span<EnumMapping<FlyController.KeyInputFlags, InputAction>> span = CollectionsMarshal.AsSpan<EnumMapping<FlyController.KeyInputFlags, InputAction>>(enumMappingList);
    int index1 = 0;
    span[index1] = new EnumMapping<FlyController.KeyInputFlags, InputAction>(FlyController.KeyInputFlags.Forward, InputAction.Forward);
    int index2 = index1 + 1;
    span[index2] = new EnumMapping<FlyController.KeyInputFlags, InputAction>(FlyController.KeyInputFlags.Backward, InputAction.Backward);
    int index3 = index2 + 1;
    span[index3] = new EnumMapping<FlyController.KeyInputFlags, InputAction>(FlyController.KeyInputFlags.Left, InputAction.Left);
    int index4 = index3 + 1;
    span[index4] = new EnumMapping<FlyController.KeyInputFlags, InputAction>(FlyController.KeyInputFlags.Right, InputAction.Right);
    int index5 = index4 + 1;
    span[index5] = new EnumMapping<FlyController.KeyInputFlags, InputAction>(FlyController.KeyInputFlags.Up, InputAction.CameraUp);
    int index6 = index5 + 1;
    span[index6] = new EnumMapping<FlyController.KeyInputFlags, InputAction>(FlyController.KeyInputFlags.Down, InputAction.CameraDown);
    int index7 = index6 + 1;
    span[index7] = new EnumMapping<FlyController.KeyInputFlags, InputAction>(FlyController.KeyInputFlags.RollLeft, InputAction.RollLeft);
    int index8 = index7 + 1;
    span[index8] = new EnumMapping<FlyController.KeyInputFlags, InputAction>(FlyController.KeyInputFlags.RollRight, InputAction.RollRight);
    int index9 = index8 + 1;
    span[index9] = new EnumMapping<FlyController.KeyInputFlags, InputAction>(FlyController.KeyInputFlags.Sprint, InputAction.Sprint);
    FlyController.Lookup = new EnumLookup<FlyController.KeyInputFlags, InputAction>(enumMappingList);
  }

  [Flags]
  public enum KeyInputFlags
  {
    None = 0,
    Forward = 1,
    Backward = 2,
    Left = 4,
    Right = 8,
    Up = 16, // 0x00000010
    Down = 32, // 0x00000020
    Sprint = 64, // 0x00000040
    RollLeft = 128, // 0x00000080
    RollRight = 256, // 0x00000100
  }
}
