using MeowSci.KsaAbstractions;
namespace MeowSci.PartsNowLib;
public sealed partial class ModFolderPanel
{
    internal void Inspect(string id, bool canLoad)
    {
        RescanWhenJobFinished();
        if (!_scanned) Rescan();
        if (_selectedModId != id) { _selectedModId = id; RefreshSelectionGate(); }
        RenderSelection(canLoad);
        RenderMessage();
        if (_openConfirm) { Brutal.ImGuiApi.ImGui.OpenPopup(ConfirmPopupId); _openConfirm = false; }
        RenderConfirmModal();
    }
    internal void BindDraft(DraftBindings state)
    {
        state.Text("ModFolderPanel.filter", _filter);
        state.Value("ModFolderId", () => _selectedModId, v => _selectedModId = v);
    }
}
