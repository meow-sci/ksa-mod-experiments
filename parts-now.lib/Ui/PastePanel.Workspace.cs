using MeowSci.KsaAbstractions;
namespace MeowSci.PartsNowLib;
public sealed partial class PastePanel
{
    internal void InvalidateDraftValidation() { _validated = false; _validationClean = false; _modIdChecked = false; }
    internal void BindDraft(DraftBindings state)
    {
        state.Text("PastePanel.modId", _modId);
        state.Text("PastePanel.displayName", _displayName);
        state.Text("PastePanel.author", _author);
        state.Text("PastePanel.version", _version);
        _xml.BindDraft(state);
        state.Value("DisplayNameEdited", () => _displayNameEdited, v => _displayNameEdited = v);
    }
}
