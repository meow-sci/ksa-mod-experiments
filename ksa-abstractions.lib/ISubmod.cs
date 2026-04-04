namespace MeowSci.KsaAbstractions;

/// <summary>
/// Generic submod interface for a UI panel that can be embedded in any host window.
/// Implementations render ImGui content (without Begin/End) and manage their own state.
/// </summary>
public interface ISubmod
{
    /// <summary>Display name shown in headers and menus.</summary>
    string Name { get; }

    /// <summary>Short description shown as a tooltip on the header in the host window.</summary>
    string Tooltip { get; }

    /// <summary>Called once during initialization to set up state and lib instances.</summary>
    void Initialize();

    /// <summary>Called every frame for pre-UI computation (sampling, ticking, etc.).</summary>
    void Update(double dt);

    /// <summary>
    /// Renders ImGui content. Caller is responsible for Begin/End window framing.
    /// Do NOT call ImGui.Begin/End for the main content area. Additional popup/child windows are fine.
    /// </summary>
    void RenderContent();

    /// <summary>
    /// Renders any floating/secondary ImGui windows (e.g. stand-alone editor windows).
    /// Called every frame regardless of whether the submod's section is collapsed, so
    /// windows opened by this submod remain consistent with their own visibility state.
    /// Default implementation is a no-op.
    /// </summary>
    void RenderFloatingWindows() { }

    /// <summary>Called during unload to clean up resources.</summary>
    void Dispose();
}
