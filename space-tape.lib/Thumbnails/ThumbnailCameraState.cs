using Brutal.Numerics;
using KSA;
using KSA.Rendering;

namespace MeowSci.SpaceTapeLib;

internal sealed class ThumbnailCameraState
{
    private const double FrameDeltaSeconds = 1.0 / 60.0;

    private readonly Viewport _viewport;
    private readonly Camera _camera;
    private readonly int2 _framebufferSize;
    private readonly int2 _viewportSize;
    private readonly IFollowable? _following;
    private readonly bool _tidalLocking;
    private readonly double3 _positionEcl;
    private readonly double3 _localPosition;
    private readonly doubleQuat _localRotation;
    private readonly double3 _localScale;
    private readonly Vehicle? _controlledVehicle;
    private readonly bool _postMemoryWrite;
    private readonly bool _postDescriptorSet;

    public ThumbnailCameraState(Viewport viewport, Camera camera)
    {
        _viewport = viewport;
        _camera = camera;
        _framebufferSize = camera.FramebufferSize;
        _viewportSize = viewport.Size;
        _following = camera.Following;
        _tidalLocking = camera.TidalLocking;
        _positionEcl = camera.PositionEcl;
        _localPosition = camera.LocalPosition;
        _localRotation = camera.LocalRotation;
        _localScale = camera.LocalScale;
        _controlledVehicle = Program.ControlledVehicle;
        _postMemoryWrite = Program.DeviceHostSharedMemoryDebug.PostMemoryWrite;
        _postDescriptorSet = Program.DeviceHostSharedMemoryDebug.PostDescriptorSet;
    }

    public void ConfigureForThumbnailRender(int2 thumbnailSize)
    {
        _camera.Unfollow(changeControl: false);
        _camera.Resize(thumbnailSize);
        _viewport.Size = thumbnailSize;
        _camera.LocalPosition = double3.Zero;
        _camera.LocalRotation = doubleQuat.Identity;
        _camera.LocalScale = double3.One;
        _camera.OnFrame(FrameDeltaSeconds);
        Program.Instance.UpdateShaderData(FrameDeltaSeconds, _viewport);
        Program.Instance.UpdateRenderingResources(0);
        Program.DeviceHostSharedMemoryDebug.PostMemoryWrite = false;
        Program.DeviceHostSharedMemoryDebug.PostDescriptorSet = false;
    }

    public void Restore()
    {
        _camera.Resize(_framebufferSize);
        _viewport.Size = _viewportSize;

        if (_following != null)
        {
            _camera.SetFollow(_following, _tidalLocking, changeControl: false, alert: false);
        }
        else
        {
            _camera.Unfollow(changeControl: false);
            _camera.PositionEcl = _positionEcl;
        }

        _camera.LocalPosition = _localPosition;
        _camera.LocalRotation = _localRotation;
        _camera.LocalScale = _localScale;
        Program.ControlledVehicle = _controlledVehicle;
        Program.DeviceHostSharedMemoryDebug.PostMemoryWrite = _postMemoryWrite;
        Program.DeviceHostSharedMemoryDebug.PostDescriptorSet = _postDescriptorSet;
        _camera.OnFrame(FrameDeltaSeconds);
    }
}
