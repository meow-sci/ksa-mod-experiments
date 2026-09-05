using MeowSci.KsaAbstractions;
namespace MeowSci.PartsNowLib;
public sealed partial class XmlTabEditor
{
    internal void BindDraft(DraftBindings state)
    {
        state.Value("XmlTab", () => _selectedTab, v => { _selectedTab = v; _restoreTab = true; }, validate: v => { if (v is not ("assets" or "part" or "gamedata")) throw new System.InvalidOperationException("Invalid XML tab."); });
        state.Text("XmlTabEditor.assets", _assets);
        state.Text("XmlTabEditor.part", _part);
        state.Text("XmlTabEditor.gameData", _gameData);
    }
}
