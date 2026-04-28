using System;
using System.Linq;
using Brutal.Numerics;
using Brutal.ImGuiApi;
using KSA;
using MeowSci.KsaAbstractions;

namespace MeowSci.EternalFlameLib;

public sealed class EternalFlameSubmod : ISubmod
{
    public string Name => "Eternal Flame - Infinite Fuel";
    public string Tooltip => "Automatically refills fuel tanks on the selected vehicle at regular intervals.";

    private FuelManager _fuelManager = null!;
    private readonly ImInputString _vehicleFilter = new ImInputString(128);
    private int _selectedVehicleIndex = -1;
    private int _refillIntervalMs = 100;

    public void Initialize()
    {
        _fuelManager = new FuelManager();
        _refillIntervalMs = _fuelManager.RefillIntervalMs;
    }

    public void Update(double dt)
    {
        _fuelManager.Update(dt);
    }

    public void RenderContent()
    {
        SubmodUI.BeginContentArea("##ef_content");

        RenderVehicleSelector();
        RenderAddButton();
        RenderMonitoredSection();

        SubmodUI.EndContentArea();
    }

    public void Dispose() { }

    private void RenderVehicleSelector()
    {
        var vehicles = VehicleProvider.GetAllVehicles();
        if (vehicles.Count == 0)
        {
            ImGui.TextDisabled("No vehicles available");
            return;
        }

        var vehicleNames = vehicles.Select(v => v.Id).ToArray();

        if (_selectedVehicleIndex >= vehicleNames.Length)
            _selectedVehicleIndex = -1;

        string preview = _selectedVehicleIndex >= 0 ? vehicleNames[_selectedVehicleIndex] : "Select...";

        ImGui.AlignTextToFramePadding();
        ImGui.Text("Vehicle");
        ImGui.SameLine();
        ImGui.TextDisabled("(?)");
        if (ImGui.IsItemHovered())
        {
            ImGui.BeginTooltip();
            ImGui.Text("Once monitored, a background thread will every N milliseconds issue");
            ImGui.Text("the equivalent of a console \"refill\" command and top up that");
            ImGui.Text("vehicle's fuel.");
            ImGui.EndTooltip();
        }
        ImGui.SameLine();
        ImGui.SetNextItemWidth(-1);
        if (ImGui.BeginCombo("##ef_selector", preview))
        {
            if (ImGui.IsWindowAppearing())
            {
                ImGui.SetKeyboardFocusHere();
                _vehicleFilter.Clear();
            }
            ImGui.SetNextItemWidth(-1);
            ImGui.InputTextWithHint("##ef_VehicleFilter", "filter..."u8, _vehicleFilter);
            string vehicleFilterText = _vehicleFilter.ToString().Trim();

            for (int i = 0; i < vehicleNames.Length; i++)
            {
                if (vehicleFilterText.Length > 0
                    && !vehicleNames[i].Contains(vehicleFilterText, StringComparison.OrdinalIgnoreCase))
                    continue;

                bool isSelected = _selectedVehicleIndex == i;
                if (ImGui.Selectable(vehicleNames[i] + "##ef", isSelected))
                    _selectedVehicleIndex = i;
            }
            ImGui.EndCombo();
        }
    }

    private void RenderAddButton()
    {
        var vehicles = VehicleProvider.GetAllVehicles();
        bool canAdd = _selectedVehicleIndex >= 0
            && _selectedVehicleIndex < vehicles.Count;

        if (!canAdd) ImGui.BeginDisabled();
        if (ImGui.Button(" Add ##ef"))
        {
            var v = vehicles![_selectedVehicleIndex];
            _fuelManager.AddVehicle(v.Id, v.Id);
            _selectedVehicleIndex = -1;
            _vehicleFilter.Clear();
        }
        if (!canAdd) ImGui.EndDisabled();
    }

    private void RenderMonitoredSection()
    {
        var monitored = _fuelManager.MonitoredVehicles;

        ImGui.Spacing();
        ImGui.SeparatorText($"Gassing up ( {monitored.Count} ) Vehicles");

        if (monitored.Count == 0)
        {
            ImGui.Spacing();
            ImGui.TextDisabled("Nothing to see here.  See above to start filling!");
            return;
        }

        // ImGui.Spacing();
        // ImGui.Spacing();
        ImGui.NewLine();
        ImGui.AlignTextToFramePadding();
        ImGui.Text("Refill Interval (ms)");
        ImGui.SameLine();
        ImGui.SetNextItemWidth(-1);
        if (ImGui.DragInt("##ef_interval", ref _refillIntervalMs, 1, 0, 5000))
        {
            _fuelManager.RefillIntervalMs = _refillIntervalMs;
        }
        // ImGui.NewLine();
        ImGui.Spacing();
        // ImGui.NewLine();
        // ImGui.Spacing();

        RenderMonitoredTable();
    }

    private void RenderMonitoredTable()
    {
        var monitored = _fuelManager.MonitoredVehicles;

        if (ImGui.BeginTable("##ef_MonitoredVehicles", 3,
            ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingStretchProp))
        {
            ImGui.TableSetupColumn("##ef_Active", ImGuiTableColumnFlags.WidthFixed | ImGuiTableColumnFlags.NoHeaderLabel, 38);
            ImGui.TableSetupColumn("Vehicle", ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableSetupColumn("##ef_Remove", ImGuiTableColumnFlags.WidthFixed, 40);
            ImGui.TableHeadersRow();

            string? toRemove = null;
            for (int i = 0; i < monitored.Count; i++)
            {
                var entry = monitored[i];
                ImGui.TableNextRow();

                ImGui.TableSetColumnIndex(0);
                bool active = entry.Active;
                float checkboxSize = ImGui.GetFrameHeight();
                float colWidth = ImGui.GetColumnWidth();
                ImGui.SetCursorPosX(ImGui.GetCursorPosX() + (colWidth - checkboxSize) / 2f);
                if (ImGui.Checkbox($"##ef_active_{i}", ref active))
                    entry.Active = active;

                ImGui.TableSetColumnIndex(1);
                ImGui.Text(entry.DisplayName);

                ImGui.TableSetColumnIndex(2);
                float btnWidth = ImGui.CalcTextSize(" X ").X + ImGui.GetStyle().FramePadding.X * 2f;
                float removeColWidth = ImGui.GetColumnWidth();
                ImGui.SetCursorPosX(ImGui.GetCursorPosX() + (removeColWidth - btnWidth) / 2f);
                if (ImGui.SmallButton($" X ##ef_{i}"))
                    toRemove = entry.VehicleId;
            }

            ImGui.EndTable();

            if (toRemove != null)
                _fuelManager.RemoveVehicle(toRemove);
        }
    }
}
