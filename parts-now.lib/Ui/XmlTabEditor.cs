// THREADING RULE (repeated in every parts-now file):
// Everything runs on the game thread except RuntimeModLoader's loader step, which runs on a
// Task.Run worker. The worker touches only ILoader.Load(). Completion is polled from Update(dt).
// Do NOT use MeowSci.KsaAbstractions.GameThread — its queue is only drained when
// unladen-swallow.lib is present, and parts-now must work standalone.

using System;
using System.Text;
using Brutal.ImGuiApi;
using Brutal.Numerics;

namespace MeowSci.PartsNowLib;

/// <summary>
/// The three tabbed XML documents of the paste panel — <b>Assets</b>, <b>Part</b> and
/// <b>GameData</b> — with a clipboard button, a clear button and a
/// character counter each.
/// </summary>
/// <remarks>
/// <para>
/// Pasting is the primary input path; typing into the box is the fallback. That is why the buffers
/// are sized at <see cref="Capacity" /> bytes: a real part bundle with a few dozen components is
/// comfortably tens of kilobytes of XML.
/// </para>
/// <para>
/// The buffers are <c>readonly</c> fields and never locals. <c>ImInputString</c> owns the native
/// memory ImGui edits in place, so a per-frame instance would lose the user's typing every frame.
/// The <c>ImString</c> that <c>ImGui.GetClipboardText()</c> returns is the opposite: it points into
/// a shared ring buffer that is reset every frame, so it is converted to a <c>string</c> and copied
/// into the buffer immediately and never cached.
/// </para>
/// </remarks>
public sealed class XmlTabEditor
{
    /// <summary>Byte capacity of each XML buffer, including the null terminator.</summary>
    public const int Capacity = 262144;

    private const float EditorHeight = 260f;

    private readonly ImInputString _assets = new ImInputString(Capacity);
    private readonly ImInputString _part = new ImInputString(Capacity);
    private readonly ImInputString _gameData = new ImInputString(Capacity);

    private string _message = string.Empty;
    private bool _messageIsError;

    /// <summary>Current contents of the <b>Assets</b> tab.</summary>
    public string AssetsXml => _assets.ToString();

    /// <summary>Current contents of the <b>Part</b> tab.</summary>
    public string PartXml => _part.ToString();

    /// <summary>Current contents of the <b>GameData</b> tab.</summary>
    public string GameDataXml => _gameData.ToString();

    /// <summary>True when every tab is blank, i.e. there is nothing to validate or install.</summary>
    public bool IsEmpty => _assets.IsEmpty && _part.IsEmpty && _gameData.IsEmpty;

    /// <summary>Draws the tab bar and the currently selected document.</summary>
    public void Render()
    {
        if (ImGui.BeginTabBar("##pn_xml_tabs"))
        {
            RenderTab("assets", "Assets", _assets,
                "Meshes, textures and PBR materials. Leave this empty when the part reuses only "
                + "assets that already exist.");
            RenderTab("part", "Part", _part,
                "The <Part> and <SubPart> templates themselves.");
            RenderTab("gamedata", "GameData", _gameData,
                "<PartGameData> — masses, connectors, tanks and everything else merged onto the Part.");
            ImGui.EndTabBar();
        }

        if (_message.Length > 0)
        {
            ImGui.Spacing();
            if (_messageIsError)
            {
                ImGui.TextColored(PanelStyle.Error, _message);
            }
            else
            {
                ImGui.TextDisabled(_message);
            }
        }
    }

    /// <summary>Empties all three documents and clears any clipboard message.</summary>
    public void Clear()
    {
        _assets.Clear();
        _part.Clear();
        _gameData.Clear();
        _message = string.Empty;
        _messageIsError = false;
    }

    private void RenderTab(string id, string label, ImInputString buffer, string hint)
    {
        if (!ImGui.BeginTabItem($"{label}##pn_tab_{id}"))
        {
            return;
        }

        ImGui.TextDisabled(hint);

        if (ImGui.Button($" Paste from clipboard ##pn_paste_{id}"))
        {
            PasteInto(label, buffer);
        }

        ImGui.SameLine(0, 8);
        if (ImGui.Button($" Clear ##pn_clear_{id}"))
        {
            buffer.Clear();
            _message = $"Cleared the {label} document.";
            _messageIsError = false;
        }

        ImGui.SameLine(0, 12);
        ImGui.AlignTextToFramePadding();
        ImGui.TextDisabled($"{buffer.Length} / {Capacity - 1} bytes");

        ImGui.InputTextMultiline(
            $"##pn_xml_{id}", buffer, new float2(-1f, EditorHeight), ImGuiInputTextFlags.AllowTabInput);

        ImGui.EndTabItem();
    }

    private void PasteInto(string label, ImInputString buffer)
    {
        string text;
        try
        {
            // ImGui.GetClipboardText() hands back an ImString backed by a per-frame ring buffer, so
            // it is materialised into a real string here and never held on to.
            text = ImGui.GetClipboardText().ToString();
        }
        catch (Exception ex)
        {
            _message = $"The clipboard could not be read: {ex.Message}";
            _messageIsError = true;
            return;
        }

        if (string.IsNullOrEmpty(text))
        {
            _message = "The clipboard is empty.";
            _messageIsError = true;
            return;
        }

        // Checked BEFORE the write, not caught after it. ImInputString.SetValue copies into Buffer
        // first and only then throws on overflow, so it never gets to NullTerminate() — leaving the
        // buffer full and unterminated. ImGui would then strlen past the end of the array on the
        // next frame.
        int required = Encoding.UTF8.GetByteCount(text);
        if (required >= Capacity)
        {
            _message = $"That text does not fit in the {label} document: {required} bytes, "
                + $"{Capacity - 1} maximum. Load it from a mod folder instead of pasting it.";
            _messageIsError = true;
            return;
        }

        try
        {
            buffer.SetValue(text.AsSpan());
        }
        catch (Exception ex)
        {
            // Belt and braces: if SetValue rejects something GetByteCount accepted, the buffer may
            // be half-written and unterminated, so drop it rather than hand it to ImGui.
            buffer.Clear();
            _message = $"That text could not be pasted into the {label} document: {ex.Message}";
            _messageIsError = true;
            return;
        }

        _message = $"Pasted {buffer.Length} bytes into the {label} document.";
        _messageIsError = false;
    }
}
