using System;
using System.Collections.Generic;
using Brutal.Numerics;
using Brutal.ImGuiApi;
using KSA;
using MeowSci.KsaAbstractions;

namespace MeowSci.ConManLib;

public sealed class ConManSubmod : ISubmod
{
  public string Name => "Con-Man - Console Layouts";
  public string Tooltip => "Saves and restores UI gauge layout configurations for different scenarios.";

  private LayoutManager _layoutManager = null!;
  private bool _startupApplied;

  // Layout selector state
  private int _selectedLayoutIndex = -1;
  private readonly ImInputString _layoutFilter = new ImInputString(128);

  // Save input state
  private readonly ImInputString _saveNameInput = new ImInputString(128);
  private string _saveStatus = string.Empty;

  // Startup default state
  private int _selectedDefaultIndex;  // 0 = "(None)", 1+ = layout names
  private readonly ImInputString _defaultFilter = new ImInputString(128);

  // Delete confirmation
  private bool _pendingOpenDelete;

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

    SubmodUI.BeginContentArea("##cm_content");

    var style = ImGui.GetStyle();
    float labelW = ImGui.CalcTextSize("Startup default").X + style.ItemSpacing.X;
    float applyW = ImGui.CalcTextSize(" Apply ").X + style.FramePadding.X * 2f;
    float deleteW = ImGui.CalcTextSize(" Delete ").X + style.FramePadding.X * 2f;
    float btnColW = applyW + deleteW + style.ItemSpacing.X;
    float halfBtnW = (btnColW - style.ItemSpacing.X) / 2f;

    var tableFlags = ImGuiTableFlags.SizingFixedFit | ImGuiTableFlags.NoPadOuterX;
    ImGui.PushStyleVar(ImGuiStyleVar.CellPadding, new float2(6f, 6f));
    if (ImGui.BeginTable("##cm_main", 3, tableFlags))
    {
      ImGui.TableSetupColumn("##cm_lbl", ImGuiTableColumnFlags.WidthFixed, labelW);
      ImGui.TableSetupColumn("##cm_widget", ImGuiTableColumnFlags.WidthStretch);
      ImGui.TableSetupColumn("##cm_btns", ImGuiTableColumnFlags.WidthFixed, btnColW);

      // ---- Layout row ----
      var names = _layoutManager.GetLayoutNames();
      string preview = (_selectedLayoutIndex >= 0 && _selectedLayoutIndex < names.Length)
          ? names[_selectedLayoutIndex]
          : "Select a layout...";
      bool canApply = _selectedLayoutIndex >= 0 && _selectedLayoutIndex < names.Length;

      ImGui.TableNextRow();
      ImGui.TableNextColumn();
      ImGui.AlignTextToFramePadding();
      ImGui.Text("Layout");

      ImGui.TableNextColumn();
      ImGui.SetNextItemWidth(-1);
      if (ImGui.BeginCombo("##cm_layout_select", preview))
      {
        if (ImGui.IsWindowAppearing())
        {
          ImGui.SetKeyboardFocusHere();
          _layoutFilter.Clear();
        }
        ImGui.SetNextItemWidth(-1);
        ImGui.InputTextWithHint("##cm_layout_filter", "filter..."u8, _layoutFilter);
        string layoutFilterText = _layoutFilter.ToString().Trim();
        for (int i = 0; i < names.Length; i++)
        {
          if (layoutFilterText.Length > 0
              && !names[i].Contains(layoutFilterText, StringComparison.OrdinalIgnoreCase))
            continue;
          bool selected = _selectedLayoutIndex == i;
          if (ImGui.Selectable(names[i], selected))
            _selectedLayoutIndex = i;
          if (selected) ImGui.SetItemDefaultFocus();
        }
        ImGui.EndCombo();
      }

      ImGui.TableNextColumn();
      if (!canApply) ImGui.BeginDisabled();
      if (ImGui.Button(" Apply ##cm", new float2(halfBtnW, 0)))
        _layoutManager.ApplyLayout(names[_selectedLayoutIndex]);
      ImGui.SameLine();
      ImGui.PushStyleColor(ImGuiCol.Button, ImGui.GetColorU32(KSAColor.Xkcd.Scarlet));
      ImGui.PushStyleColor(ImGuiCol.Text, ImGui.GetColorU32(KSAColor.Xkcd.PaleGrey));
      if (ImGui.Button(" Delete ##cm", new float2(halfBtnW, 0)))
        _pendingOpenDelete = true;
      ImGui.PopStyleColor();
      ImGui.PopStyleColor();
      if (!canApply) ImGui.EndDisabled();

      // ---- Save current row ----
      ImGui.TableNextRow();
      ImGui.TableNextColumn();
      ImGui.AlignTextToFramePadding();
      ImGui.Text("Save current");

      ImGui.TableNextColumn();
      ImGui.SetNextItemWidth(-1);
      ImGui.InputText("##cm_save_name", _saveNameInput);

      ImGui.TableNextColumn();
      if (ImGui.Button(" Save ##cm", new float2(-1, 0)))
      {
        var name = _saveNameInput.ToString().Trim();
        if (string.IsNullOrEmpty(name))
        {
          _saveStatus = "Enter a name first";
        }
        else if (_layoutManager.SaveLayout(name))
        {
          _saveStatus = $"Saved: {name}";
          var savedNames = _layoutManager.GetLayoutNames();
          _selectedLayoutIndex = Array.IndexOf(savedNames, name);
        }
        else
        {
          _saveStatus = "Save failed — check console";
        }
      }

      // ---- Startup default row ----
      var layoutNames = _layoutManager.GetLayoutNames();
      string currentDefault = _layoutManager.StartupDefault;
      if (string.IsNullOrEmpty(currentDefault))
        _selectedDefaultIndex = 0;
      else
      {
        int idx = Array.IndexOf(layoutNames, currentDefault);
        _selectedDefaultIndex = idx >= 0 ? idx + 1 : 0;
      }
      string defaultPreview = _selectedDefaultIndex == 0 || _selectedDefaultIndex - 1 >= layoutNames.Length
          ? "(None)"
          : layoutNames[_selectedDefaultIndex - 1];

      ImGui.TableNextRow();
      ImGui.TableNextColumn();
      ImGui.AlignTextToFramePadding();
      ImGui.Text("Startup default");

      ImGui.TableNextColumn();
      ImGui.SetNextItemWidth(-1);
      if (ImGui.BeginCombo("##cm_default_select", defaultPreview))
      {
        if (ImGui.IsWindowAppearing())
        {
          ImGui.SetKeyboardFocusHere();
          _defaultFilter.Clear();
        }
        ImGui.SetNextItemWidth(-1);
        ImGui.InputTextWithHint("##cm_default_filter", "filter..."u8, _defaultFilter);
        string defaultFilterText = _defaultFilter.ToString().Trim();
        if (defaultFilterText.Length == 0
            || "(None)".Contains(defaultFilterText, StringComparison.OrdinalIgnoreCase))
        {
          bool noneSelected = _selectedDefaultIndex == 0;
          if (ImGui.Selectable("(None)", noneSelected))
          {
            _selectedDefaultIndex = 0;
            _layoutManager.SetStartupDefault(string.Empty);
          }
          if (noneSelected) ImGui.SetItemDefaultFocus();
        }
        for (int i = 0; i < layoutNames.Length; i++)
        {
          if (defaultFilterText.Length > 0
              && !layoutNames[i].Contains(defaultFilterText, StringComparison.OrdinalIgnoreCase))
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

      ImGui.TableNextColumn();
      bool canReset = !string.IsNullOrEmpty(currentDefault);
      if (!canReset) ImGui.BeginDisabled();
      if (ImGui.Button(" Reset ##cm", new float2(-1, 0)))
      {
        _selectedDefaultIndex = 0;
        _layoutManager.SetStartupDefault(string.Empty);
      }
      if (!canReset) ImGui.EndDisabled();

      ImGui.EndTable();
    }
    ImGui.PopStyleVar(); // CellPadding

    if (!string.IsNullOrEmpty(_saveStatus))
    {
      ImGui.Spacing();
      ImGui.TextDisabled(_saveStatus);
    }

    ImGui.Spacing();
    // Deferred popup open at content area scope (outside table ID stack)
    if (_pendingOpenDelete)
    {
      ImGui.OpenPopup("##cm_confirm_delete");
      _pendingOpenDelete = false;
    }
    RenderDeleteConfirmPopup();
    RenderGaugeSummary();

    SubmodUI.EndContentArea();
  }

  public void Dispose() { }

  // --- Delete confirmation popup ---
  private void RenderDeleteConfirmPopup()
  {
    var names = _layoutManager.GetLayoutNames();
    bool canDelete = _selectedLayoutIndex >= 0 && _selectedLayoutIndex < names.Length;

    ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new float2(20f, 16f));
    bool open = true;
    bool began = ImGui.BeginPopupModal("##cm_confirm_delete", ref open, ImGuiWindowFlags.AlwaysAutoResize);
    ImGui.PopStyleVar();
    if (!began) return;

    if (canDelete)
    {
      string deleteName = names[_selectedLayoutIndex];
      ImGui.Text($"Delete layout '{deleteName}'?");
      ImGui.Spacing();
      if (ImGui.Button(" Yes, Delete ##cm"))
      {
        _layoutManager.DeleteLayout(deleteName);
        _selectedLayoutIndex = -1;
        ImGui.CloseCurrentPopup();
      }
      ImGui.SameLine(0, 8);
      if (ImGui.Button(" Cancel ##cm"))
        ImGui.CloseCurrentPopup();
    }
    ImGui.EndPopup();
  }

  // --- Debug (live gauge data) ---
  private void RenderGaugeSummary()
  {
    if (!ImGui.CollapsingHeader("Gauge Data Debug##cm"))
      return;

    var canvases = _layoutManager.Accessor.GetCanvases();
    if (canvases == null || canvases.Count == 0)
    {
      ImGui.TextDisabled("No gauge canvases detected");
      return;
    }

    var flags = ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.Resizable
              | ImGuiTableFlags.SizingStretchProp;

    if (ImGui.BeginTable("##cm_gauges_table", 4, flags))
    {
      ImGui.TableSetupColumn("Name");
      ImGui.TableSetupColumn("Enabled", ImGuiTableColumnFlags.WidthFixed, 55);
      ImGui.TableSetupColumn("Offset");
      ImGui.TableSetupColumn("Scale");
      ImGui.TableHeadersRow();

      var accessor = _layoutManager.Accessor;
      foreach (var canvas in canvases)
      {
        ImGui.TableNextRow();

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
