using Brutal.Numerics;
using System;
using System.Collections.Generic;
using System.Linq;
using Brutal.ImGuiApi;
using MeowSci.KsaAbstractions;

namespace MeowSci.HotPursuitLib;

public sealed partial class HotPursuitSubmod
{
    public IEnumerable<ILiveStateItem> GetLiveItems()
    {
        foreach (var entry in _cameras.ToArray())
            yield return new LiveStateItem<HotPursuitCamera>(entry.Id.ToString(), "Mounted camera " + entry.Id,
                entry.TargetDescription, entry, camera => { if (ImGui.Button("Copy settings to workspace"))
                { _nextFov = camera.FieldOfView; _nextWidth = camera.Width; _nextHeight = camera.Height; _nextTranslation = float3.Pack(camera.Translation); _nextRotation = float3.Pack(camera.RotationDeg); }
                if (RenderCamera(camera, 0)) RemoveCamera(camera); }, entry.Status);
    }
    public void CancelAuthoringGesture() => Disarm(null);
}
