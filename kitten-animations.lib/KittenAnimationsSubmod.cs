using System;
using Brutal.ImGuiApi;
using KSA;
using MeowSci.KittenAnimationsLib.Ui;
using MeowSci.KsaAbstractions;

namespace MeowSci.KittenAnimationsLib;

/// <summary>
/// Animation panel for the controlled kitten: plays any clip the game loaded for it, triggers facial
/// expressions, and exposes the blend weights and locomotion tuning that decide how hard each
/// animation lands.
/// </summary>
public sealed partial class KittenAnimationsSubmod : IWorkspaceFeature
{
    public string Name => "Kitten Animations";
    public string Tooltip => "Play any kitten animation, trigger expressions, and tune animation strength.";

    private readonly KittenAnimationDriver _driver = new();
    private readonly KittenExpressionController _expressions = new();
    private readonly Random _random = new();

    private AnimationUiContext? _context;
    private CharacterAvatar? _boundAvatar;
    private string? _liveKittenTarget;
    private KittenLocomotionTuning? _originalTuning;

    private static KittenEva? ResolveKitten(string? target) => target == "$controlled"
        ? KittenAvatarAccessor.GetKitten() : target == null ? null : VehicleProvider.FindVehicle(target) as KittenEva;

    public void Initialize()
    {
        KittenAnimationPatches.Driver = _driver;
    }

    public void Update(double dt)
    {
        try
        {
            _driver.RestoreDisabledProcessors();
            var kitten = ResolveKitten(_liveKittenTarget);
            var renderable = kitten?.Renderable;
            var avatar = KittenAvatarAccessor.GetAvatar(renderable);

            if (kitten == null || renderable == null || avatar == null)
            {
                Unbind();
                return;
            }

            if (!ReferenceEquals(avatar, _boundAvatar))
            {
                Unbind();
                Bind(kitten, renderable, avatar);
            }

            // Refreshed every frame: the model is what the Harmony prefix matches against.
            _driver.TargetModel = avatar.Core.CharacterModel;

            _expressions.Update(dt);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"kitten-animations: Error in Update: {ex.Message}");
            Unbind();
        }
    }

    private void RenderRuntimeControls()
    {
        SubmodUI.BeginContentArea("##ka_content");

        var context = _context;
        if (context == null)
        {
            ImGui.TextDisabled("No kitten under control.");
            ImGui.TextDisabled("Take control of a kitten on EVA to play its animations.");
            SubmodUI.EndContentArea();
            return;
        }

        PlaybackSection.Render(context);
        ImGui.Spacing();
        AnimationLibrarySection.Render(context);
        ImGui.Spacing();
        ExpressionSection.Render(context);
        ImGui.Spacing();
        StrengthSection.Render(context);
        ImGui.Spacing();
        if (_originalTuning.HasValue) TuningSection.Render(context);

        SubmodUI.EndContentArea();
    }

    public void Dispose()
    {
        ReleaseLiveState();
        KittenAnimationPatches.Driver = null;
    }

    private void Bind(KittenEva kitten, KittenRenderable renderable, CharacterAvatar avatar)
    {
        var catalog = KittenAnimationCatalog.Build(avatar, renderable);
        var processors = KittenAnimProcessors.Read(renderable);

        _expressions.Detach();


        _driver.ClearClip();
        _driver.Processors = processors;

        _context = new AnimationUiContext
        {
            Kitten = kitten,
            Avatar = avatar,
            Catalog = catalog,
            Driver = _driver,
            Expressions = _expressions,
            Processors = processors,
            Random = _random,
        };

        _boundAvatar = avatar;

        if (catalog.UnresolvedFields.Count > 0)
            Console.WriteLine($"kitten-animations: unresolved game fields: {string.Join(", ", catalog.UnresolvedFields)}");
    }

    private void Unbind()
    {
        if (_boundAvatar == null) return;

        _expressions.Detach();
        _driver.ClearClip();
        _driver.TargetModel = null;
        _driver.Processors = null;
        _context = null;
        _boundAvatar = null;
    }
}
