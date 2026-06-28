using System;
using System.Collections.Generic;
using Brutal.ImGuiApi;
using Brutal.Numerics;
using KSA;

namespace MeowSci.SpaceTapeLib;

/// <summary>
/// Static ImGui rendering for expanded GameData sub-sections.
/// Extracted from PartEditorUi to keep file sizes manageable.
/// </summary>
public static class GameDataEditorUi
{
    // Connector editor state
    private static int _selectedConnectorIndex = -1;
    private static readonly ImInputString _connectorIdInput = new ImInputString(64);
    private static string _lastConnectorId = "";

    // Tank editor state
    private static int _selectedTankIndex = -1;
    private static readonly ImInputString _tankMaterialInput = new ImInputString(128);
    private static string _lastTankMaterial = "";

    public static int SelectedConnectorIndex => _selectedConnectorIndex;

    /// <summary>Renders the Tank section supporting multiple tanks.</summary>
    public static void RenderTankSection(PartGameDataState gd)
    {
        ImGui.SeparatorText($"Tanks ({gd.Tanks.Count})");

        // Tank list
        ImGui.BeginChild("##st_tank_list", new float2(0, 100),
            ImGuiChildFlags.Borders | ImGuiChildFlags.ResizeY);
        for (int i = 0; i < gd.Tanks.Count; i++)
        {
            var t = gd.Tanks[i];
            string label = t.Shape == TankShape.Cylindrical
                ? $"[{i + 1}] Cylindrical  r={t.OuterRadiusM:G4} m  L={t.LengthM:G4} m"
                : $"[{i + 1}] Spherical    r={t.OuterRadiusM:G4} m";
            bool sel = _selectedTankIndex == i;
            if (ImGui.Selectable($"{label}##st_tk{i}", sel))
                _selectedTankIndex = sel ? -1 : i;
        }
        if (gd.Tanks.Count == 0)
            ImGui.TextDisabled("No tanks. Click + to add.");
        ImGui.EndChild();

        if (ImGui.SmallButton(" + Tank ##st_tk_add"))
        {
            gd.Tanks.Add(new TankState());
            _selectedTankIndex = gd.Tanks.Count - 1;
        }
        ImGui.SameLine();
        bool canRemoveTank = _selectedTankIndex >= 0 && _selectedTankIndex < gd.Tanks.Count;
        if (!canRemoveTank) ImGui.BeginDisabled();
        if (ImGui.SmallButton(" - Remove ##st_tk_rm") && canRemoveTank)
        {
            gd.Tanks.RemoveAt(_selectedTankIndex);
            if (_selectedTankIndex >= gd.Tanks.Count)
                _selectedTankIndex = gd.Tanks.Count - 1;
            _lastTankMaterial = "";
        }
        if (!canRemoveTank) ImGui.EndDisabled();

        // Detail editor for selected tank
        if (_selectedTankIndex >= 0 && _selectedTankIndex < gd.Tanks.Count)
        {
            var tank = gd.Tanks[_selectedTankIndex];

            ImGui.Spacing();
            ImGui.SeparatorText($"Tank {_selectedTankIndex + 1}");

            ImGui.PushStyleVar(ImGuiStyleVar.CellPadding, new float2(6f, 6f));
            if (ImGui.BeginTable("##st_tank_tbl", 2,
                ImGuiTableFlags.SizingStretchProp | ImGuiTableFlags.NoPadOuterX))
            {
                ImGui.TableSetupColumn("##lbl", ImGuiTableColumnFlags.WidthFixed, 330f);
                ImGui.TableSetupColumn("##val", ImGuiTableColumnFlags.WidthStretch, 1f);

                // Shape row
                ImGui.TableNextRow();
                ImGui.TableNextColumn(); ImGui.AlignTextToFramePadding(); ImGui.Text("Shape");
                ImGui.TableNextColumn(); ImGui.SetNextItemWidth(-1f);
                int shapeIdx = (int)tank.Shape;
                if (ImGui.Combo("##st_tank_shape", ref shapeIdx, "Cylindrical\0Spherical\0"))
                {
                    tank.Shape = (TankShape)shapeIdx;
                    _lastTankMaterial = "";  // force buffer resync if tank identity changes
                }

                // Material ID row
                if (tank.WallMaterialId != _lastTankMaterial)
                {
                    _tankMaterialInput.SetValue(tank.WallMaterialId.AsSpan());
                    _lastTankMaterial = tank.WallMaterialId;
                }
                ImGui.TableNextRow();
                ImGui.TableNextColumn(); ImGui.AlignTextToFramePadding(); ImGui.Text("Material");
                ImGui.TableNextColumn(); ImGui.SetNextItemWidth(-1f);
                if (ImGui.InputText("##st_tank_mat", _tankMaterialInput))
                {
                    tank.WallMaterialId = _tankMaterialInput.ToString();
                    _lastTankMaterial = tank.WallMaterialId;
                }

                if (tank.Shape == TankShape.Cylindrical)
                {
                    double length = tank.LengthM;
                    TableInputDouble("Length (m)", "##st_tank_len", ref length, 0.1);
                    tank.LengthM = length;
                }

                double outerR = tank.OuterRadiusM;
                TableInputDouble("Outer Radius (m)", "##st_tank_or", ref outerR, 0.01);
                tank.OuterRadiusM = outerR;

                double wallT = tank.WallThicknessMm;
                TableInputDouble("Wall Thick (mm)", "##st_tank_wt", ref wallT, 0.1);
                tank.WallThicknessMm = wallT;

                ImGui.EndTable();
            }
            ImGui.PopStyleVar();
        }
    }

    /// <summary>Renders the Power section (Batteries, Generators, PowerConsumers).</summary>
    public static void RenderPowerSection(PartGameDataState gd)
    {
        const float UnitColW = 52f;
        const float RemoveColW = 58f;
        var tableFlags = ImGuiTableFlags.SizingFixedFit | ImGuiTableFlags.NoPadOuterX;

        // --- Batteries ---
        ImGui.SeparatorText($"Batteries ({gd.Batteries.Count})");
        if (gd.Batteries.Count > 0)
        {
            ImGui.PushStyleVar(ImGuiStyleVar.CellPadding, new float2(6f, 4f));
            if (ImGui.BeginTable("##st_bat_tbl", 3, tableFlags))
            {
                ImGui.TableSetupColumn("##val", ImGuiTableColumnFlags.WidthStretch);
                ImGui.TableSetupColumn("##unit", ImGuiTableColumnFlags.WidthFixed, UnitColW);
                ImGui.TableSetupColumn("##rm", ImGuiTableColumnFlags.WidthFixed, RemoveColW);
                for (int i = 0; i < gd.Batteries.Count; i++)
                {
                    ImGui.TableNextRow();
                    ImGui.TableNextColumn();
                    ImGui.SetNextItemWidth(-1f);
                    double val = gd.Batteries[i].CapacityKWh;
                    if (ImGui.InputDouble($"##st_bat{i}", ref val, 0.001))
                        gd.Batteries[i].CapacityKWh = val;
                    ImGui.TableNextColumn();
                    ImGui.AlignTextToFramePadding();
                    ImGui.Text("kWh");
                    ImGui.TableNextColumn();
                    if (ImGui.Button($" x ##st_bat_rm{i}"))
                    {
                        gd.Batteries.RemoveAt(i);
                        i--;
                    }
                }
                ImGui.EndTable();
            }
            ImGui.PopStyleVar();
        }
        if (ImGui.Button(" + Battery ##st_bat_add"))
            gd.Batteries.Add(new BatteryState());

        // --- Generators ---
        ImGui.SeparatorText($"Generators ({gd.Generators.Count})");
        if (gd.Generators.Count > 0)
        {
            ImGui.PushStyleVar(ImGuiStyleVar.CellPadding, new float2(6f, 4f));
            if (ImGui.BeginTable("##st_gen_tbl", 3, tableFlags))
            {
                ImGui.TableSetupColumn("##val", ImGuiTableColumnFlags.WidthStretch);
                ImGui.TableSetupColumn("##unit", ImGuiTableColumnFlags.WidthFixed, UnitColW);
                ImGui.TableSetupColumn("##rm", ImGuiTableColumnFlags.WidthFixed, RemoveColW);
                for (int i = 0; i < gd.Generators.Count; i++)
                {
                    ImGui.TableNextRow();
                    ImGui.TableNextColumn();
                    ImGui.SetNextItemWidth(-1f);
                    double val = gd.Generators[i].OutputWatts;
                    if (ImGui.InputDouble($"##st_gen{i}", ref val, 0.5))
                        gd.Generators[i].OutputWatts = val;
                    ImGui.TableNextColumn();
                    ImGui.AlignTextToFramePadding();
                    ImGui.Text("W");
                    ImGui.TableNextColumn();
                    if (ImGui.Button($" x ##st_gen_rm{i}"))
                    {
                        gd.Generators.RemoveAt(i);
                        i--;
                    }
                }
                ImGui.EndTable();
            }
            ImGui.PopStyleVar();
        }
        if (ImGui.Button(" + Generator ##st_gen_add"))
            gd.Generators.Add(new GeneratorState());

        // --- Power Consumers ---
        ImGui.SeparatorText($"Power Consumers ({gd.PowerConsumers.Count})");
        if (gd.PowerConsumers.Count > 0)
        {
            ImGui.PushStyleVar(ImGuiStyleVar.CellPadding, new float2(6f, 4f));
            if (ImGui.BeginTable("##st_pc_tbl", 3, tableFlags))
            {
                ImGui.TableSetupColumn("##val", ImGuiTableColumnFlags.WidthStretch);
                ImGui.TableSetupColumn("##unit", ImGuiTableColumnFlags.WidthFixed, UnitColW);
                ImGui.TableSetupColumn("##rm", ImGuiTableColumnFlags.WidthFixed, RemoveColW);
                for (int i = 0; i < gd.PowerConsumers.Count; i++)
                {
                    ImGui.TableNextRow();
                    ImGui.TableNextColumn();
                    ImGui.SetNextItemWidth(-1f);
                    double val = gd.PowerConsumers[i].ConsumedWatts;
                    if (ImGui.InputDouble($"##st_pc{i}", ref val, 0.5))
                        gd.PowerConsumers[i].ConsumedWatts = val;
                    ImGui.TableNextColumn();
                    ImGui.AlignTextToFramePadding();
                    ImGui.Text("W");
                    ImGui.TableNextColumn();
                    if (ImGui.Button($" x ##st_pc_rm{i}"))
                    {
                        gd.PowerConsumers.RemoveAt(i);
                        i--;
                    }
                }
                ImGui.EndTable();
            }
            ImGui.PopStyleVar();
        }
        if (ImGui.Button(" + Consumer ##st_pc_add"))
            gd.PowerConsumers.Add(new PowerConsumerState());
    }

    /// <summary>Renders the Connectors section with list and selected connector detail editor.</summary>
    public static void RenderConnectorsSection(PartGameDataState gd)
    {
        // List
        ImGui.BeginChild("##st_conn_list", new float2(0, 120),
            ImGuiChildFlags.Borders | ImGuiChildFlags.ResizeY);
        for (int i = 0; i < gd.Connectors.Count; i++)
        {
            var c = gd.Connectors[i];
            string flagStr = FormatConnectorFlags(c);
            bool sel = _selectedConnectorIndex == i;
            if (ImGui.Selectable($"{c.Id}  {flagStr}##st_cn{i}", sel))
                _selectedConnectorIndex = sel ? -1 : i;
        }
        if (gd.Connectors.Count == 0)
            ImGui.TextDisabled("No connectors. Click + to add.");
        ImGui.EndChild();

        if (ImGui.SmallButton(" + Connector ##st_cn_add"))
        {
            int nextIdx = gd.Connectors.Count + 1;
            gd.Connectors.Add(new ConnectorState { Id = $"conn_{nextIdx}" });
            _selectedConnectorIndex = gd.Connectors.Count - 1;
        }
        ImGui.SameLine();
        bool canRemove = _selectedConnectorIndex >= 0 && _selectedConnectorIndex < gd.Connectors.Count;
        if (!canRemove) ImGui.BeginDisabled();
        if (ImGui.SmallButton(" - Remove ##st_cn_rm") && canRemove)
        {
            gd.Connectors.RemoveAt(_selectedConnectorIndex);
            if (_selectedConnectorIndex >= gd.Connectors.Count)
                _selectedConnectorIndex = gd.Connectors.Count - 1;
            _lastConnectorId = "";
        }
        if (!canRemove) ImGui.EndDisabled();

        // Detail editor for selected connector
        if (_selectedConnectorIndex >= 0 && _selectedConnectorIndex < gd.Connectors.Count)
        {
            var c = gd.Connectors[_selectedConnectorIndex];

            ImGui.Spacing();
            ImGui.SeparatorText($"Connector: {c.Id}");

            // ID input
            if (c.Id != _lastConnectorId)
            {
                _connectorIdInput.SetValue(c.Id.AsSpan());
                _lastConnectorId = c.Id;
            }
            ImGui.AlignTextToFramePadding();
            ImGui.Text("Id:");
            ImGui.SameLine();
            ImGui.SetNextItemWidth(-1);
            if (ImGui.InputText("##st_cn_id", _connectorIdInput))
            {
                c.Id = _connectorIdInput.ToString();
                _lastConnectorId = c.Id;
            }

            // Flags
            bool fi = c.FlagInternal, ft = c.FlagToSurface, ff = c.FlagFromSurface;
            ImGui.Checkbox("Internal##st_cn_fi", ref fi); c.FlagInternal = fi;
            ImGui.SameLine();
            ImGui.Checkbox("ToSurface##st_cn_ft", ref ft); c.FlagToSurface = ft;
            ImGui.SameLine();
            ImGui.Checkbox("FromSurface##st_cn_ff", ref ff); c.FlagFromSurface = ff;

            // Position
            ImGui.TextDisabled("Position (m) - [X, Y, Z]");
            {
                float x = (float)c.Position.X, y = (float)c.Position.Y, z = (float)c.Position.Z;
                bool cx, cy, cz;
                Drag3("st_cn_pos", ref x, out cx, ref y, out cy, ref z, out cz, 0.001f);
                if (cx || cy || cz) c.Position = new double3(x, y, z);
            }

            // Rotation (Euler degrees)
            ImGui.TextDisabled("Rotation (\u00b0) - [X, Y, Z]");
            {
                double3 eulerRad = c.Rotation.NormalizedOrIdentity().ToXyzRadians();
                float rx = (float)(eulerRad.X * (180.0 / Math.PI));
                float ry = (float)(eulerRad.Y * (180.0 / Math.PI));
                float rz = (float)(eulerRad.Z * (180.0 / Math.PI));
                bool cx, cy, cz;
                Drag3("st_cn_rot", ref rx, out cx, ref ry, out cy, ref rz, out cz, 0.1f);
                if (cx || cy || cz)
                    c.Rotation = QuaternionEx.CreateFromXyzRadians(new double3(
                        rx * (Math.PI / 180.0), ry * (Math.PI / 180.0), rz * (Math.PI / 180.0)));
            }

        }
    }

    /// <summary>Renders Decoupler/DockingPort/EVADoor toggles and fields.</summary>
    public static void RenderCouplingSection(PartGameDataState gd)
    {
        // Connector ID options for combo
        string[] connectorIds = BuildConnectorIdArray(gd);

        // Decoupler
        bool hasDec = gd.Decoupler != null;
        if (ImGui.Checkbox("Decoupler##st_gd_dec", ref hasDec))
            gd.Decoupler = hasDec ? new DecouplerState() : null;

        if (gd.Decoupler != null)
        {
            string connId = gd.Decoupler.ConnectorId;
            double force = gd.Decoupler.Force;
            RenderCouplingFields("st_dec", connectorIds, ref connId, hasForce: true, ref force);
            gd.Decoupler.ConnectorId = connId;
            gd.Decoupler.Force = force;
        }

        // DockingPort
        bool hasDp = gd.DockingPort != null;
        if (ImGui.Checkbox("Docking Port##st_gd_dp", ref hasDp))
            gd.DockingPort = hasDp ? new DockingPortState() : null;

        if (gd.DockingPort != null)
        {
            string connId = gd.DockingPort.ConnectorId;
            double impulse = gd.DockingPort.PushoffImpulseNs;
            RenderCouplingFields("st_dp", connectorIds, ref connId, hasForce: true, ref impulse, "Pushoff Impulse (Ns)");
            gd.DockingPort.ConnectorId = connId;
            gd.DockingPort.PushoffImpulseNs = impulse;
        }

        // EVADoor
        bool hasEva = gd.EVADoor != null;
        if (ImGui.Checkbox("EVA Door##st_gd_eva", ref hasEva))
            gd.EVADoor = hasEva ? new EVADoorState() : null;

        if (gd.EVADoor != null)
        {
            string connId = gd.EVADoor.ConnectorId;
            double unused = 0;
            RenderCouplingFields("st_eva", connectorIds, ref connId, hasForce: false, ref unused);
            gd.EVADoor.ConnectorId = connId;
        }
    }

    /// <summary>Renders the Connector combo (and optionally the Force input) for a coupling sub-type.</summary>
    private static void RenderCouplingFields(string prefix, string[] connectorIds,
        ref string connectorId, bool hasForce, ref double force, string valueLabel = "Force (N)")
    {
        var tableFlags = ImGuiTableFlags.SizingStretchProp | ImGuiTableFlags.NoPadOuterX;
        ImGui.PushStyleVar(ImGuiStyleVar.CellPadding, new float2(6f, 4f));
        if (ImGui.BeginTable($"##{prefix}_tbl", 2, tableFlags))
        {
            ImGui.TableSetupColumn("##lbl", ImGuiTableColumnFlags.WidthStretch, 1f);
            ImGui.TableSetupColumn("##val", ImGuiTableColumnFlags.WidthStretch, 3f);

            ImGui.TableNextRow();
            ImGui.TableNextColumn(); ImGui.AlignTextToFramePadding(); ImGui.Text("Connector");
            ImGui.TableNextColumn(); ImGui.SetNextItemWidth(-1f);
            RenderConnectorCombo($"##{prefix}_cn", connectorIds, ref connectorId);

            if (hasForce)
            {
                ImGui.TableNextRow();
                ImGui.TableNextColumn(); ImGui.AlignTextToFramePadding(); ImGui.Text(valueLabel);
                ImGui.TableNextColumn(); ImGui.SetNextItemWidth(-1f);
                ImGui.InputDouble($"##{prefix}_f", ref force, 10.0);
            }

            ImGui.EndTable();
        }
        ImGui.PopStyleVar();
    }

    // --- Helpers ---

    private static void TableInputDouble(string label, string id, ref double value, double step)
    {
        ImGui.TableNextRow();
        ImGui.TableNextColumn(); ImGui.AlignTextToFramePadding(); ImGui.Text(label);
        ImGui.TableNextColumn(); ImGui.SetNextItemWidth(-1);
        ImGui.InputDouble(id, ref value, step);
    }

    private static void Drag3(string prefix, ref float x, out bool cx, ref float y, out bool cy, ref float z, out bool cz, float speed)
    {
        ImGui.PushStyleVar(ImGuiStyleVar.CellPadding, new float2(4f, 4f));
        if (ImGui.BeginTable($"##{prefix}_tbl", 3,
            ImGuiTableFlags.SizingStretchSame | ImGuiTableFlags.NoPadOuterX))
        {
            ImGui.TableNextRow();
            ImGui.TableNextColumn(); ImGui.SetNextItemWidth(-1);
            cx = ImGui.DragFloat($"##{prefix}_x", ref x, speed, 0f, 0f, "%.4f");
            ImGui.TableNextColumn(); ImGui.SetNextItemWidth(-1);
            cy = ImGui.DragFloat($"##{prefix}_y", ref y, speed, 0f, 0f, "%.4f");
            ImGui.TableNextColumn(); ImGui.SetNextItemWidth(-1);
            cz = ImGui.DragFloat($"##{prefix}_z", ref z, speed, 0f, 0f, "%.4f");
            ImGui.EndTable();
        }
        else { cx = cy = cz = false; }
        ImGui.PopStyleVar();
    }

    private static string FormatConnectorFlags(ConnectorState c)
    {
        var parts = new List<string>();
        if (c.FlagInternal) parts.Add("Int");
        if (c.FlagToSurface) parts.Add("To");
        if (c.FlagFromSurface) parts.Add("From");
        return parts.Count > 0 ? $"[{string.Join("|", parts)}]" : "";
    }

    private static string[] BuildConnectorIdArray(PartGameDataState gd)
    {
        if (gd.Connectors.Count == 0)
            return new[] { "(none)" };
        var ids = new string[gd.Connectors.Count];
        for (int i = 0; i < gd.Connectors.Count; i++)
            ids[i] = gd.Connectors[i].Id;
        return ids;
    }

    private static void RenderConnectorCombo(string label, string[] connectorIds, ref string connectorId)
    {
        int idx = Array.IndexOf(connectorIds, connectorId);
        if (idx < 0) idx = 0;
        if (ImGui.Combo(label, ref idx, connectorIds, connectorIds.Length))
            connectorId = connectorIds[idx];
    }
}
