namespace MeowSci.Grant;

/// <summary>
/// Interface for a submod panel rendered inside the grant supermod window.
/// </summary>
internal interface IGrantSubmod
{
    /// <summary>Display name shown in the collapsible header and context menu.</summary>
    string Name { get; }

    /// <summary>
    /// Called once during OnFullyLoaded. Initialize state, create instances of .lib classes, etc.
    /// </summary>
    void Initialize();

    /// <summary>
    /// Called every frame in OnBeforeUi for submods that need pre-UI computation
    /// (e.g., TWR sampling, fuel manager ticking, game thread draining).
    /// </summary>
    void Update(double dt);

    /// <summary>
    /// Renders this submod's ImGui content. Called between Begin/End of the
    /// main grant window — do NOT call ImGui.Begin/ImGui.End for the main content.
    /// Additional popup/child windows (like Skittles editor) are fine.
    /// </summary>
    void RenderContent();

    /// <summary>Called during Unload to clean up resources.</summary>
    void Dispose();
}
