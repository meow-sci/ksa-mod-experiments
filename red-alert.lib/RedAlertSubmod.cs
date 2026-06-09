using System;
using System.Collections.Generic;
using Brutal.ImGuiApi;
using Brutal.Numerics;
using KSA;
using MeowSci.KsaAbstractions;

namespace MeowSci.RedAlertLib;

public sealed class RedAlertSubmod : ISubmod
{
    public static RedAlertSubmod? Instance { get; private set; }

    public string Name => "Red Alert - Action Plans";
    public string Tooltip => "Builds reusable plans of light/solar-panel actions and engages them with one click.";

    private readonly List<ActionPlan> _plans = new();
    public IReadOnlyList<ActionPlan> Plans => _plans;

    // ---- Create-plan form state ----
    private readonly ImInputString _newPlanName = new(64);
    private string? _formError;

    // ---- Per-plan add-action form state (one entry per ActionPlan) ----
    private readonly Dictionary<ActionPlan, PlanFormState> _planForms = new();

    public void Initialize() { Instance = this; }
    public void Update(double dt) { }
    public void Dispose() { Instance = null; }

    // ─────────────────────────────────────────────────────────────────────────
    // UI

    public void RenderContent()
    {
        SubmodUI.BeginContentArea("##ra_content");

        RenderCreatePlanSection();

        if (_plans.Count > 0)
        {
            ImGui.Spacing();
            ImGui.SeparatorText($"Action Plans ( {_plans.Count} )");

            ActionPlan? toDelete = null;
            for (int i = 0; i < _plans.Count; i++)
                RenderPlanSection(_plans[i], i, ref toDelete);
            if (toDelete != null)
            {
                _planForms.Remove(toDelete);
                _plans.Remove(toDelete);
            }
        }

        SubmodUI.EndContentArea();
    }

    // ── Create Plan ────────────────────────────────────────────────────────

    private void RenderCreatePlanSection()
    {
        bool open = ImGui.CollapsingHeader("Create Action Plan (?)", ImGuiTreeNodeFlags.DefaultOpen);
        ImGui.SetItemTooltip("An action plan groups one or more part actions\nthat are all executed together when engaged.");
        if (!open) return;

        ImGui.PushStyleVar(ImGuiStyleVar.CellPadding, new float2(6f, 6f));
        var flags = ImGuiTableFlags.SizingStretchProp | ImGuiTableFlags.NoPadOuterX;
        if (ImGui.BeginTable("##ra_create", 2, flags))
        {
            ImGui.TableSetupColumn("##ra_clbl", ImGuiTableColumnFlags.WidthStretch, 1f);
            ImGui.TableSetupColumn("##ra_cwidget", ImGuiTableColumnFlags.WidthStretch, 3f);

            ImGui.TableNextRow();
            ImGui.TableNextColumn(); ImGui.AlignTextToFramePadding(); ImGui.Text("Plan Name");
            ImGui.TableNextColumn(); ImGui.SetNextItemWidth(-1f);
            ImGui.InputTextWithHint("##ra_plan_name", "e.g. battle stations"u8, _newPlanName);

            ImGui.EndTable();
        }
        ImGui.PopStyleVar();

        ImGui.Spacing();
        var name = _newPlanName.ToString().Trim();
        bool canCreate = name.Length > 0;
        if (!canCreate) ImGui.BeginDisabled();
        if (ImGui.Button(" Create Plan ##ra_create_btn"))
        {
            if (PlanExists(name))
            {
                _formError = $"A plan named '{name}' already exists.";
            }
            else
            {
                _plans.Add(new ActionPlan { Name = name });
                _newPlanName.Clear();
                _formError = null;
            }
        }
        if (!canCreate) ImGui.EndDisabled();

        if (!string.IsNullOrEmpty(_formError))
        {
            ImGui.Spacing();
            ImGui.TextColored(new float4(1f, 0.3f, 0.3f, 1f), _formError);
        }
    }

    // ── Plan section ───────────────────────────────────────────────────────

    private void RenderPlanSection(ActionPlan plan, int index, ref ActionPlan? toDelete)
    {
        if (!ImGui.CollapsingHeader($"Plan: {plan.Name}  ( {plan.Actions.Count} actions )##ra_plan_{index}",
            ImGuiTreeNodeFlags.DefaultOpen))
            return;

        var wpadX = ImGui.GetStyle().WindowPadding.X;
        float childW = ImGui.GetContentRegionAvail().X + wpadX * 2;
        ImGui.SetCursorPosX(ImGui.GetCursorPosX() - wpadX);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new float2(20f, 10f));
        ImGui.BeginChild($"ra_plan_child_{index}", new float2(childW, 0),
            ImGuiChildFlags.Borders | ImGuiChildFlags.AutoResizeY | ImGuiChildFlags.AlwaysUseWindowPadding,
            ImGuiWindowFlags.NoScrollbar);
        ImGui.PopStyleVar();

        // Engage + Delete row
        bool canEngage = plan.Actions.Count > 0;
        if (!canEngage) ImGui.BeginDisabled();
        ImGui.PushStyleColor(ImGuiCol.Button, ImGui.GetColorU32(KSAColor.Xkcd.Scarlet));
        ImGui.PushStyleColor(ImGuiCol.Text, ImGui.GetColorU32(KSAColor.Xkcd.PaleGrey));
        if (ImGui.Button($" Engage ##ra_engage_{index}"))
        {
            try { ActionExecutor.Execute(plan); }
            catch (Exception ex) { Console.WriteLine($"red-alert: engage error: {ex.Message}"); }
        }
        ImGui.PopStyleColor();
        ImGui.PopStyleColor();
        if (!canEngage) ImGui.EndDisabled();

        ImGui.SameLine(0, 8);
        if (ImGui.Button($" Delete Plan ##ra_delplan_{index}"))
            toDelete = plan;

        // Existing actions
        if (plan.Actions.Count > 0)
        {
            ImGui.Spacing();
            ImGui.SeparatorText("Actions");
            RenderActionList(plan, index);
        }

        // Add-action sub-form
        ImGui.Spacing();
        ImGui.SeparatorText("Add Action");
        RenderAddActionForm(plan, index);

        ImGui.Spacing();
        ImGui.EndChild();
    }

    // ── Action list ────────────────────────────────────────────────────────

    private void RenderActionList(ActionPlan plan, int planIndex)
    {
        ImGui.PushStyleVar(ImGuiStyleVar.CellPadding, new float2(6f, 6f));
        var flags = ImGuiTableFlags.SizingFixedFit | ImGuiTableFlags.NoPadOuterX
            | ImGuiTableFlags.RowBg | ImGuiTableFlags.Borders;
        if (ImGui.BeginTable($"##ra_actions_{planIndex}", 5, flags))
        {
            ImGui.TableSetupColumn("Vehicle", ImGuiTableColumnFlags.WidthStretch, 1f);
            ImGui.TableSetupColumn("Part", ImGuiTableColumnFlags.WidthStretch, 2f);
            ImGui.TableSetupColumn("Action", ImGuiTableColumnFlags.WidthStretch, 1f);
            ImGui.TableSetupColumn("Detail", ImGuiTableColumnFlags.WidthStretch, 1f);
            ImGui.TableSetupColumn("##rm", ImGuiTableColumnFlags.WidthFixed, 64f);
            ImGui.TableHeadersRow();

            int? toRemoveIdx = null;
            for (int i = 0; i < plan.Actions.Count; i++)
            {
                var a = plan.Actions[i];
                ImGui.TableNextRow();
                ImGui.TableNextColumn(); ImGui.AlignTextToFramePadding(); ImGui.Text(a.VehicleId);
                ImGui.TableNextColumn(); ImGui.AlignTextToFramePadding(); ImGui.Text($"{a.PartDisplayName}  #{a.PartInstanceId}");
                ImGui.TableNextColumn(); ImGui.AlignTextToFramePadding(); ImGui.Text(ActionLabels.Short(a.Type));
                ImGui.TableNextColumn();
                RenderActionDetail(a);
                ImGui.TableNextColumn();
                if (ImGui.Button($" X ##ra_rm_{planIndex}_{i}")) toRemoveIdx = i;
            }
            if (toRemoveIdx.HasValue)
                plan.Actions.RemoveAt(toRemoveIdx.Value);

            ImGui.EndTable();
        }
        ImGui.PopStyleVar();
    }

    private void RenderActionDetail(PlannedAction a)
    {
        switch (a.Type)
        {
            case ActionType.LightColor:
                var c = new float4(a.Color.X, a.Color.Y, a.Color.Z, 1f);
                ImGui.ColorEdit4($"##ra_actdetail_{a.GetHashCode()}", ref c,
                    ImGuiColorEditFlags.NoInputs | ImGuiColorEditFlags.NoLabel);
                a.Color = new float3(c.X, c.Y, c.Z);
                break;
            case ActionType.LightActuate:
            case ActionType.SolarPanelActuate:
                ImGui.SetNextItemWidth(-1);
                ImGui.DragFloat($"##ra_actdetail_{a.GetHashCode()}", ref a.Actuate, 0.005f, 0f, 1f);
                break;
            default:
                ImGui.AlignTextToFramePadding(); ImGui.TextDisabled("—");
                break;
        }
    }

    // ── Add action form ─────────────────────────────────────────────────────

    private void RenderAddActionForm(ActionPlan plan, int planIndex)
    {
        var vehicles = VehicleProvider.GetAllVehicles();
        if (vehicles.Count == 0)
        {
            ImGui.TextDisabled("No vehicles available — load a vehicle to add actions.");
            return;
        }

        if (!_planForms.TryGetValue(plan, out var form))
        {
            form = new PlanFormState();
            _planForms[plan] = form;
        }

        var vehicleIds = new string[vehicles.Count];
        for (int i = 0; i < vehicles.Count; i++) vehicleIds[i] = vehicles[i].Id;

        if (form.VehicleIndex >= vehicles.Count) form.VehicleIndex = -1;

        // Rescan when this plan's selected vehicle changes
        if (form.VehicleIndex != form.PrevVehicleIndex)
        {
            form.PrevVehicleIndex = form.VehicleIndex;
            form.ScannedParts.Clear();
            form.PartIndex = -1;
            form.ActionTypeIndex = -1;
            if (form.VehicleIndex >= 0)
                form.ScannedParts.AddRange(ActionScanner.Scan(vehicles[form.VehicleIndex]));
        }

        // Part labels — include InstanceId since Part.Id can collide between instances
        var partLabels = new string[form.ScannedParts.Count];
        for (int i = 0; i < form.ScannedParts.Count; i++)
        {
            var p = form.ScannedParts[i];
            partLabels[i] = $"{p.DisplayName}  #{p.PartInstanceId}  ({CapabilitiesShort(p.Capabilities)})";
        }
        if (form.PartIndex >= form.ScannedParts.Count) form.PartIndex = -1;

        // Available actions for the selected part
        ActionablePart? selPart = (form.PartIndex >= 0 && form.PartIndex < form.ScannedParts.Count)
            ? form.ScannedParts[form.PartIndex] : null;
        var availableActions = AvailableActions(selPart);
        var actionLabels = new string[availableActions.Count];
        for (int i = 0; i < availableActions.Count; i++) actionLabels[i] = ActionLabels.Long(availableActions[i]);
        if (form.ActionTypeIndex >= availableActions.Count) form.ActionTypeIndex = -1;

        // Form table — Vehicle / Part / Action / Detail
        ImGui.PushStyleVar(ImGuiStyleVar.CellPadding, new float2(6f, 6f));
        var flags = ImGuiTableFlags.SizingStretchProp | ImGuiTableFlags.NoPadOuterX;
        if (ImGui.BeginTable($"##ra_addform_{planIndex}", 2, flags))
        {
            ImGui.TableSetupColumn("##lbl", ImGuiTableColumnFlags.WidthStretch, 1f);
            ImGui.TableSetupColumn("##widget", ImGuiTableColumnFlags.WidthStretch, 3f);

            ImGui.TableNextRow();
            ImGui.TableNextColumn(); ImGui.AlignTextToFramePadding(); ImGui.Text("Vehicle");
            ImGui.TableNextColumn(); ImGui.SetNextItemWidth(-1f);
            RenderFilteredCombo($"##ra_v_{planIndex}", vehicleIds, ref form.VehicleIndex, form.VehicleFilter);

            ImGui.TableNextRow();
            ImGui.TableNextColumn(); ImGui.AlignTextToFramePadding(); ImGui.Text("Part");
            ImGui.TableNextColumn(); ImGui.SetNextItemWidth(-1f);
            if (form.VehicleIndex < 0)
            {
                ImGui.AlignTextToFramePadding();
                ImGui.TextDisabled("(select a vehicle)");
            }
            else
            {
                RenderFilteredCombo($"##ra_p_{planIndex}", partLabels, ref form.PartIndex, form.PartFilter);
            }

            ImGui.TableNextRow();
            ImGui.TableNextColumn(); ImGui.AlignTextToFramePadding(); ImGui.Text("Action");
            ImGui.TableNextColumn(); ImGui.SetNextItemWidth(-1f);
            if (actionLabels.Length == 0)
            {
                ImGui.AlignTextToFramePadding();
                ImGui.TextDisabled("(select a part)");
            }
            else
            {
                ImGui.Combo($"##ra_a_{planIndex}", ref form.ActionTypeIndex, actionLabels, actionLabels.Length);
            }

            // Detail row, varies by action type
            if (form.ActionTypeIndex >= 0 && form.ActionTypeIndex < availableActions.Count)
            {
                var t = availableActions[form.ActionTypeIndex];
                if (t == ActionType.LightColor)
                {
                    ImGui.TableNextRow();
                    ImGui.TableNextColumn(); ImGui.AlignTextToFramePadding(); ImGui.Text("Color");
                    ImGui.TableNextColumn(); ImGui.SetNextItemWidth(-1f);
                    ImGui.ColorEdit4($"##ra_addcolor_{planIndex}", ref form.Color, ImGuiColorEditFlags.NoLabel);
                }
                else if (t == ActionType.LightActuate || t == ActionType.SolarPanelActuate)
                {
                    ImGui.TableNextRow();
                    ImGui.TableNextColumn(); ImGui.AlignTextToFramePadding(); ImGui.Text("Actuate (0..1)");
                    ImGui.TableNextColumn(); ImGui.SetNextItemWidth(-1f);
                    ImGui.DragFloat($"##ra_addactuate_{planIndex}", ref form.Actuate, 0.005f, 0f, 1f);
                }
            }

            ImGui.EndTable();
        }
        ImGui.PopStyleVar();

        ImGui.Spacing();
        bool hasSelection = selPart != null && form.ActionTypeIndex >= 0 && form.ActionTypeIndex < availableActions.Count;
        ActionType? selectedType = hasSelection ? availableActions[form.ActionTypeIndex] : null;
        bool isDuplicate = hasSelection && PlanContains(plan, selPart!.PartInstanceId, selectedType!.Value);
        bool canAdd = hasSelection && !isDuplicate;

        if (!canAdd) ImGui.BeginDisabled();
        if (ImGui.Button($" Add Action ##ra_add_{planIndex}"))
        {
            plan.Actions.Add(new PlannedAction
            {
                VehicleId = selPart!.VehicleId,
                PartInstanceId = selPart.PartInstanceId,
                PartId = selPart.PartId,
                PartDisplayName = selPart.DisplayName,
                Type = selectedType!.Value,
                Color = new float3(form.Color.X, form.Color.Y, form.Color.Z),
                Actuate = form.Actuate,
            });
        }
        if (!canAdd) ImGui.EndDisabled();

        if (isDuplicate)
        {
            ImGui.SameLine(0, 12);
            ImGui.AlignTextToFramePadding();
            ImGui.TextDisabled("(this part instance already has this action in the plan)");
        }
    }

    private static bool PlanContains(ActionPlan plan, uint partInstanceId, ActionType type)
    {
        for (int i = 0; i < plan.Actions.Count; i++)
        {
            var a = plan.Actions[i];
            if (a.PartInstanceId == partInstanceId && a.Type == type) return true;
        }
        return false;
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    private static List<ActionType> AvailableActions(ActionablePart? p)
    {
        var list = new List<ActionType>();
        if (p == null) return list;
        if (p.Capabilities.HasFlag(PartCapability.LightOnOff))
        {
            list.Add(ActionType.LightOff);
            list.Add(ActionType.LightOn);
            list.Add(ActionType.LightToggle);
        }
        if (p.Capabilities.HasFlag(PartCapability.LightColor))
            list.Add(ActionType.LightColor);
        if (p.Capabilities.HasFlag(PartCapability.LightActuate))
            list.Add(ActionType.LightActuate);
        if (p.Capabilities.HasFlag(PartCapability.SolarDeployRetract))
        {
            list.Add(ActionType.SolarPanelDeploy);
            list.Add(ActionType.SolarPanelRetract);
            list.Add(ActionType.SolarPanelToggle);
        }
        if (p.Capabilities.HasFlag(PartCapability.SolarActuate))
            list.Add(ActionType.SolarPanelActuate);
        return list;
    }

    private static string CapabilitiesShort(PartCapability c)
    {
        var parts = new List<string>();
        if (c.HasFlag(PartCapability.LightOnOff)) parts.Add("on/off");
        if (c.HasFlag(PartCapability.LightColor)) parts.Add("color");
        if (c.HasFlag(PartCapability.LightActuate)) parts.Add("light-anim");
        if (c.HasFlag(PartCapability.SolarDeployRetract)) parts.Add("deploy");
        if (c.HasFlag(PartCapability.SolarActuate)) parts.Add("solar-anim");
        return string.Join(", ", parts);
    }

    private bool PlanExists(string name)
    {
        for (int i = 0; i < _plans.Count; i++)
            if (string.Equals(_plans[i].Name, name, StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    private static void RenderFilteredCombo(string id, string[] items, ref int selectedIndex, ImInputString filter)
    {
        string preview = selectedIndex >= 0 && selectedIndex < items.Length
            ? items[selectedIndex] : "Select...";

        if (!ImGui.BeginCombo(id, preview)) return;

        if (ImGui.IsWindowAppearing())
        {
            ImGui.SetKeyboardFocusHere();
            filter.Clear();
        }
        ImGui.SetNextItemWidth(-1f);
        ImGui.InputTextWithHint($"{id}_filter", "filter..."u8, filter);
        var filterText = filter.ToString().Trim();

        for (int i = 0; i < items.Length; i++)
        {
            if (filterText.Length > 0 && !items[i].Contains(filterText, StringComparison.OrdinalIgnoreCase)) continue;
            bool sel = selectedIndex == i;
            ImGui.PushID(i);
            if (ImGui.Selectable(items[i], sel))
                selectedIndex = i;
            ImGui.PopID();
            if (sel) ImGui.SetItemDefaultFocus();
        }
        ImGui.EndCombo();
    }
}

internal static class ActionLabels
{
    public static string Long(ActionType t) => t switch
    {
        ActionType.LightOff => "Light off",
        ActionType.LightOn => "Light on",
        ActionType.LightToggle => "Light toggle",
        ActionType.LightColor => "Light color",
        ActionType.LightActuate => "Light animate (actuate)",
        ActionType.SolarPanelDeploy => "Solar deploy",
        ActionType.SolarPanelRetract => "Solar retract",
        ActionType.SolarPanelToggle => "Solar toggle",
        ActionType.SolarPanelActuate => "Solar animate (actuate)",
        _ => t.ToString(),
    };

    public static string Short(ActionType t) => t switch
    {
        ActionType.LightOff => "off",
        ActionType.LightOn => "on",
        ActionType.LightToggle => "toggle",
        ActionType.LightColor => "color",
        ActionType.LightActuate => "actuate",
        ActionType.SolarPanelDeploy => "deploy",
        ActionType.SolarPanelRetract => "retract",
        ActionType.SolarPanelToggle => "toggle",
        ActionType.SolarPanelActuate => "actuate",
        _ => t.ToString(),
    };
}
