using System;
using System.Linq;
using Brutal.Numerics;
using Brutal.ImGuiApi;
using KSA;
using MeowSci.KsaAbstractions;

namespace MeowSci.EternalFlameLib;

public sealed class EternalFlameSubmod : ISubmod
{
    public string Name => "Eternal Flame \u2014 Infinite Fuel";

    private FuelManager _fuelManager = null!;
    private ImGuiTextFilter _vehicleFilter = new ImGuiTextFilter();
    private int _selectedVehicleIndex = -1;
    private int _refillIntervalMs = 500;

    public void Initialize()
    {
        _fuelManager = new FuelManager();
    }

    public void Update(double dt)
    {
        _fuelManager.Update(dt);
    }

    public void RenderContent()
    {
        ImGui.TextColored(new float4(1.0f, 0.6f, 0.0f, 1.0f), "Eternal Flame");
        ImGui.SameLine(0, 10);
        ImGui.TextDisabled($"(refill every {_fuelManager.RefillIntervalMs}ms)");
        ImGui.Separator();

        RenderVehicleSelector();
        ImGui.Spacing();
        RenderRefillIntervalSlider();
        ImGui.Spacing();
        ImGui.SeparatorText("Monitored Vehicles");
        RenderMonitoredTable();
    }

    public void Dispose() { }

    private void RenderVehicleSelector()
    {
        var vehicles = Universe.CurrentSystem?.Vehicles.GetList();
        if (vehicles == null || vehicles.Count == 0)
        {
            ImGui.TextDisabled("No vehicles available");
            return;
        }

        var vehicleNames = vehicles.Select(v => v.Id).ToArray();

        if (_selectedVehicleIndex >= vehicleNames.Length)
            _selectedVehicleIndex = -1;

        string preview = _selectedVehicleIndex >= 0 ? vehicleNames[_selectedVehicleIndex] : "Select a vehicle...";

        if (ImGui.BeginCombo("Vehicle##ef_selector", preview))
        {
            if (ImGui.IsWindowAppearing())
            {
                ImGui.SetKeyboardFocusHere();
                _vehicleFilter.Clear();
            }
            _vehicleFilter.Draw("##ef_VehicleFilter", -float.MaxValue);

            for (int i = 0; i < vehicleNames.Length; i++)
            {
                if (!_vehicleFilter.PassFilter(vehicleNames[i]))
                    continue;

                bool isSelected = _selectedVehicleIndex == i;
                if (ImGui.Selectable(vehicleNames[i] + "##ef", isSelected))
                    _selectedVehicleIndex = i;
            }
            ImGui.EndCombo();
        }

        ImGui.SameLine();

        bool canAdd = _selectedVehicleIndex >= 0;
        if (!canAdd) ImGui.BeginDisabled();
        if (ImGui.Button("Add##ef"))
        {
            var v = vehicles[_selectedVehicleIndex];
            _fuelManager.AddVehicle(v.Id, v.Id);
            _selectedVehicleIndex = -1;
        }
        if (!canAdd) ImGui.EndDisabled();
    }

    private void RenderRefillIntervalSlider()
    {
        if (ImGui.DragInt("Refill Interval (ms)##ef", ref _refillIntervalMs, 1, 0, 1000))
        {
            _fuelManager.RefillIntervalMs = _refillIntervalMs;
        }
    }

    private void RenderMonitoredTable()
    {
        var monitored = _fuelManager.MonitoredVehicles;
        if (monitored.Count == 0)
        {
            ImGui.TextDisabled("No vehicles being monitored. Add one above.");
            return;
        }

        if (ImGui.BeginTable("##ef_MonitoredVehicles", 3,
            ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingStretchProp))
        {
            ImGui.TableSetupColumn("Active", ImGuiTableColumnFlags.WidthFixed, 50);
            ImGui.TableSetupColumn("Vehicle", ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableSetupColumn("##ef_Remove", ImGuiTableColumnFlags.WidthFixed, 30);
            ImGui.TableHeadersRow();

            string? toRemove = null;
            for (int i = 0; i < monitored.Count; i++)
            {
                var entry = monitored[i];
                ImGui.TableNextRow();

                ImGui.TableSetColumnIndex(0);
                bool active = entry.Active;
                if (ImGui.Checkbox($"##ef_active_{i}", ref active))
                    entry.Active = active;

                ImGui.TableSetColumnIndex(1);
                ImGui.Text(entry.DisplayName);

                ImGui.TableSetColumnIndex(2);
                if (ImGui.SmallButton($"X##ef_{i}"))
                    toRemove = entry.VehicleId;
            }

            ImGui.EndTable();

            if (toRemove != null)
                _fuelManager.RemoveVehicle(toRemove);
        }
    }
}
