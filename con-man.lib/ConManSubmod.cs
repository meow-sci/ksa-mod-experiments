using System;
using System.Collections.Generic;
using Brutal.Numerics;
using Brutal.ImGuiApi;
using KSA;
using MeowSci.KsaAbstractions;

namespace MeowSci.ConManLib;

public sealed class ConManSubmod : ISubmod
{
    public string Name => "Con-Man \u2014 Layout Manager";

    private LayoutManager _layoutManager = null!;
    private bool _startupApplied;

    // Layout selector state
    private int _selectedLayoutIndex = -1;
    private readonly ImInputString _layoutFilter = new ImInputString(256);

    // Save input state
    private readonly ImInputString _saveNameInput = new ImInputString(128);
    private string _saveStatus = string.Empty;

    // Startup default state
    private int _selectedDefaultIndex;  // 0 = "(None)", 1+ = layout names
    private readonly ImInputString _defaultFilter = new ImInputString(256);

    // Delete confirmation
    private bool _confirmDelete;

    public void Initialize()
    {
        var accessor = new GaugeStateAccessor();
        _layoutManager = new LayoutManager(accessor);
        _layoutManager.Initialize();

        if (!accessor.IsValid)
            Console.WriteLine("[con-man] WARNING: GaugeStateAccessor failed to resolve fields — mod may not function correctly");
    }

    public void Update(double dt)
    {
        // Apply startup default once gauges become available (they may not exist at Initialize time)
        if (!_startupApplied)
        {
            var canvases = _layoutManager.Accessor.GetCanvases();
            if (canvases != null && canvases.Count > 0)
            {
                _layoutManager.ApplyStartupDefault();
                _startupApplied = true;
            }
        }
    }

    public void RenderContent()
    {
        if (!_layoutManager.Accessor.IsValid)
        {
            ImGui.TextColored(new float4(1f, 0.3f, 0.3f, 1f), "Error: Could not access GaugeCanvas fields via reflection.");
            ImGui.TextColored(new float4(1f, 0.3f, 0.3f, 1f), "The game may have been updated. Check console for details.");
            return;
        }

        RenderLayoutSelector();
        ImGui.Spacing();
        ImGui.Separator();
        RenderSaveSection();
        ImGui.Spacing();
        ImGui.Separator();
        RenderStartupDefaultSection();
        ImGui.Spacing();
        ImGui.Separator();
        RenderDeleteSection();
        ImGui.Spacing();
        RenderGaugeSummary();
    }

    public void Dispose() { }

    // --- Layout Selector ---
    private void RenderLayoutSelector()
    {
        ImGui.TextDisabled("Load Layout");
        ImGui.Spacing();

        var names = _layoutManager.GetLayoutNames();
        string preview = (_selectedLayoutIndex >= 0 && _selectedLayoutIndex < names.Length)
            ? names[_selectedLayoutIndex]
            : "Select a layout...";

        ImGui.SetNextItemWidth(-80);
        if (ImGui.BeginCombo("##cm_layout_select", preview))
        {
            ImGui.SetNextItemWidth(-1);
            ImGui.InputText("##cm_layout_filter", _layoutFilter);
            ImGui.Separator();

            string filterText = _layoutFilter.ToString();
            for (int i = 0; i < names.Length; i++)
            {
                if (!string.IsNullOrEmpty(filterText) &&
                    !names[i].Contains(filterText, StringComparison.OrdinalIgnoreCase))
                    continue;

                bool selected = _selectedLayoutIndex == i;
                if (ImGui.Selectable(names[i], selected))
                    _selectedLayoutIndex = i;
                if (selected) ImGui.SetItemDefaultFocus();
            }
            ImGui.EndCombo();
        }

        ImGui.SameLine();
        bool canApply = _selectedLayoutIndex >= 0 && _selectedLayoutIndex < names.Length;
        if (!canApply) ImGui.BeginDisabled();
        if (ImGui.Button("Apply##cm"))
        {
            if (canApply)
            {
                _layoutManager.ApplyLayout(names[_selectedLayoutIndex]);
            }
        }
        if (!canApply) ImGui.EndDisabled();
    }

    // --- Save Section ---
    private void RenderSaveSection()
    {
        ImGui.TextDisabled("Save Current Layout");
        ImGui.Spacing();

        ImGui.SetNextItemWidth(-80);
        ImGui.InputText("##cm_save_name", _saveNameInput);

        ImGui.SameLine();
        if (ImGui.Button("Save##cm"))
        {
            var name = _saveNameInput.ToString().Trim();
            if (string.IsNullOrEmpty(name))
            {
                _saveStatus = "Enter a name first";
            }
            else if (_layoutManager.SaveLayout(name))
            {
                _saveStatus = $"Saved: {name}";
                // Update selection to the newly saved layout
                var names = _layoutManager.GetLayoutNames();
                _selectedLayoutIndex = Array.IndexOf(names, name);
            }
            else
            {
                _saveStatus = "Save failed — check console";
            }
        }

        if (!string.IsNullOrEmpty(_saveStatus))
        {
            ImGui.SameLine();
            ImGui.TextDisabled(_saveStatus);
        }
    }

    // --- Startup Default Section ---
    private void RenderStartupDefaultSection()
    {
        ImGui.TextDisabled("Startup Default");
        ImGui.Spacing();

        string currentDefault = _layoutManager.StartupDefault;
        ImGui.Text($"Current: {(string.IsNullOrEmpty(currentDefault) ? "(None)" : currentDefault)}");

        // Build options: "(None)" + all layout names
        var layoutNames = _layoutManager.GetLayoutNames();

        // Sync _selectedDefaultIndex with actual startup default
        if (string.IsNullOrEmpty(currentDefault))
        {
            _selectedDefaultIndex = 0;
        }
        else
        {
            int idx = Array.IndexOf(layoutNames, currentDefault);
            _selectedDefaultIndex = idx >= 0 ? idx + 1 : 0;
        }

        string defaultPreview = _selectedDefaultIndex == 0
            ? "(None)"
            : (_selectedDefaultIndex - 1 < layoutNames.Length ? layoutNames[_selectedDefaultIndex - 1] : "(None)");

        ImGui.SetNextItemWidth(-1);
        if (ImGui.BeginCombo("##cm_default_select", defaultPreview))
        {
            ImGui.SetNextItemWidth(-1);
            ImGui.InputText("##cm_default_filter", _defaultFilter);
            ImGui.Separator();

            string filterText = _defaultFilter.ToString();

            // "(None)" option
            if (string.IsNullOrEmpty(filterText) || "(None)".Contains(filterText, StringComparison.OrdinalIgnoreCase))
            {
                bool noneSelected = _selectedDefaultIndex == 0;
                if (ImGui.Selectable("(None)", noneSelected))
                {
                    _selectedDefaultIndex = 0;
                    _layoutManager.SetStartupDefault(string.Empty);
                }
                if (noneSelected) ImGui.SetItemDefaultFocus();
            }

            // Layout options
            for (int i = 0; i < layoutNames.Length; i++)
            {
                if (!string.IsNullOrEmpty(filterText) &&
                    !layoutNames[i].Contains(filterText, StringComparison.OrdinalIgnoreCase))
                    continue;

                bool selected = _selectedDefaultIndex == i + 1;
                if (ImGui.Selectable(layoutNames[i] + "##cm_def", selected))
                {
                    _selectedDefaultIndex = i + 1;
                    _layoutManager.SetStartupDefault(layoutNames[i]);
                }
                if (selected) ImGui.SetItemDefaultFocus();
            }
            ImGui.EndCombo();
        }
    }

    // --- Delete Section ---
    private void RenderDeleteSection()
    {
        var names = _layoutManager.GetLayoutNames();
        bool canDelete = _selectedLayoutIndex >= 0 && _selectedLayoutIndex < names.Length;

        if (!canDelete) ImGui.BeginDisabled();
        if (ImGui.Button("Delete Selected##cm"))
        {
            _confirmDelete = true;
            ImGui.OpenPopup("##cm_confirm_delete");
        }
        if (!canDelete) ImGui.EndDisabled();

        // Confirmation popup
        if (ImGui.BeginPopup("##cm_confirm_delete"))
        {
            if (_confirmDelete && canDelete)
            {
                string deleteName = names[_selectedLayoutIndex];
                ImGui.Text($"Delete layout '{deleteName}'?");
                ImGui.Spacing();
                if (ImGui.Button("Yes, Delete##cm"))
                {
                    _layoutManager.DeleteLayout(deleteName);
                    _selectedLayoutIndex = -1;
                    _confirmDelete = false;
                    ImGui.CloseCurrentPopup();
                }
                ImGui.SameLine();
                if (ImGui.Button("Cancel##cm"))
                {
                    _confirmDelete = false;
                    ImGui.CloseCurrentPopup();
                }
            }
            ImGui.EndPopup();
        }
    }

    // --- Live Gauge Summary ---
    private void RenderGaugeSummary()
    {
        if (!ImGui.CollapsingHeader("Gauges##cm"))
            return;

        var canvases = _layoutManager.Accessor.GetCanvases();
        if (canvases == null || canvases.Count == 0)
        {
            ImGui.TextDisabled("No gauge canvases detected");
            return;
        }

        var flags = ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.Resizable
                  | ImGuiTableFlags.SizingStretchProp;

        if (ImGui.BeginTable("##cm_gauges_table", 5, flags))
        {
            ImGui.TableSetupColumn("Name");
            ImGui.TableSetupColumn("Id");
            ImGui.TableSetupColumn("Enabled", ImGuiTableColumnFlags.WidthFixed, 55);
            ImGui.TableSetupColumn("Offset");
            ImGui.TableSetupColumn("Scale");
            ImGui.TableHeadersRow();

            var accessor = _layoutManager.Accessor;
            foreach (var canvas in canvases)
            {
                ImGui.TableNextRow();

                ImGui.TableNextColumn();
                ImGui.Text(canvas.DisplayName?.ToString() ?? "?");

                ImGui.TableNextColumn();
                ImGui.Text(canvas.Id);

                ImGui.TableNextColumn();
                bool enabled = accessor.GetEnabled(canvas);
                ImGui.Text(enabled ? "Yes" : "No");

                ImGui.TableNextColumn();
                var offset = accessor.GetCustomOffset(canvas);
                ImGui.Text($"{offset.X:F1}, {offset.Y:F1}");

                ImGui.TableNextColumn();
                var scale = accessor.GetCustomScale(canvas);
                ImGui.Text($"{scale.X:F2}, {scale.Y:F2}");
            }

            ImGui.EndTable();
        }
    }
}
