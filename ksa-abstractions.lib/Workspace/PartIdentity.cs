using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Brutal.ImGuiApi;
using KSA;

namespace MeowSci.KsaAbstractions;

/// <summary>
/// KSA regenerates Part.InstanceId on load. Persist a verified vehicle topology + root/subpart path.
/// A changed topology is unresolved; editor-only parts retain a session identity.
/// </summary>
public static class PartIdentity
{
    private static readonly string Session = Guid.NewGuid().ToString("N");
    private static readonly Dictionary<Part, string> Ids = new(ReferenceEqualityComparer.Instance);
    private static int _frame = -1;
    public static string Get(Part part)
    {
        int frame = ImGui.GetFrameCount();
        if (_frame != frame)
        {
            _frame = frame; Ids.Clear();
            foreach (var vehicle in VehicleProvider.GetAllVehicles())
            {
                var nodes = new List<(Part Part, string Path)>();
                for (int i = 0; i < vehicle.Parts.Parts.Length; i++) Walk(vehicle.Parts.Parts[i], i.ToString(), nodes);
                string shape = string.Join("|", nodes.Select(n => n.Path + ":" + n.Part.Template.Id + ":" + n.Part.Id));
                string fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(shape)));
                foreach (var node in nodes)
                    Ids[node.Part] = "vehicle/" + Uri.EscapeDataString(vehicle.Id) + "/" + fingerprint + "/" + node.Path;
            }
        }
        return Ids.TryGetValue(part, out var id) ? id : "session/" + Session + "/" + part.InstanceId + "/" + part.Template.Id;
    }
    private static void Walk(Part part, string path, List<(Part Part, string Path)> nodes)
    {
        nodes.Add((part, path));
        int index = 0;
        foreach (var child in part.SubParts) Walk(child, path + "/" + index++, nodes);
    }
}
