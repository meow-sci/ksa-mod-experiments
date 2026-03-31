using System;
using Brutal.Numerics;
using Brutal.ImGuiApi;
using StarMap.API;
using KSA;
using MeowSci.HumbleArteestLib.Experiments;

namespace MeowSci.HumbleArteest;

[StarMapMod]
public class Mod
{
  public bool ImmediateUnload => false;

  private bool _isInitialized = false;
  private bool _isDisposed = false;
  private bool _windowVisible = false;


  [StarMapImmediateLoad]
  public void OnImmediateLoad() { }

  [StarMapAllModsLoaded]
  public void OnFullyLoaded()
  {
    try
    {
      Patcher.Patch();
      _isInitialized = true;
    }
    catch (Exception ex)
    {
      Console.WriteLine($"humble-arteest: Error during initialization: {ex.Message}");
    }
  }

  [StarMapBeforeGui]
  public void OnBeforeUi(double dt) { }

  [StarMapAfterGui]
  public void OnAfterUi(double dt)
  {
    try
    {
      if (!_isInitialized || _isDisposed) return;

      if (ImGui.IsKeyPressed(ImGuiKey.F11))
        _windowVisible = !_windowVisible;

      if (_windowVisible)
        RenderWindow();
    }
    catch (Exception ex)
    {
      Console.WriteLine($"humble-arteest: Error in OnAfterUi: {ex.Message}");
    }
  }

  [StarMapUnload]
  public void Unload()
  {
    try
    {
      Patcher.Unload();
      _isDisposed = true;
    }
    catch (Exception ex)
    {
      Console.WriteLine($"humble-arteest: Error during unload: {ex.Message}");
    }
  }

  private void RenderWindow()
  {
    ImGui.SetNextWindowSize(new float2(550, 400), ImGuiCond.FirstUseEver);

    if (ImGui.Begin("Humble Arteest — Experiments", ref _windowVisible))
    {
      ImGui.TextColored(new float4(0.0f, 1.0f, 0.0f, 1.0f), "Humble Arteest");
      ImGui.Separator();

      RenderShaderLoadTest();
      ImGui.Spacing();
      RenderPaddingTest();
    }
    ImGui.End();
  }

  private void RenderShaderLoadTest()
  {
    if (ImGui.CollapsingHeader("Experiment 0.1: Shader File Loading Test", ImGuiTreeNodeFlags.DefaultOpen))
    {
      ImGui.Indent();

      ImGui.TextWrapped("Tests whether KSA loads GLSL shaders from disk at runtime. " +
        "Changes the part highlight color from RED to GREEN in MeshIndirect.frag. " +
        "After applying, RESTART the game and hover over a part in the editor.");
      ImGui.Spacing();

      // Show shader path
      var shaderPath = ShaderLoadTest.GetShaderPath();
      if (shaderPath != null)
      {
        ImGui.TextColored(new float4(0.6f, 0.6f, 0.6f, 1.0f), $"Shader: {shaderPath}");
        ImGui.Spacing();
      }

      // Show current state
      var state = ShaderLoadTest.GetState();
      var stateDesc = ShaderLoadTest.GetStateDescription();

      var stateColor = state switch
      {
        ShaderLoadTest.ShaderState.Original => new float4(0.5f, 1.0f, 0.5f, 1.0f),
        ShaderLoadTest.ShaderState.Modified => new float4(1.0f, 1.0f, 0.0f, 1.0f),
        ShaderLoadTest.ShaderState.FileNotFound => new float4(1.0f, 0.3f, 0.3f, 1.0f),
        ShaderLoadTest.ShaderState.Error => new float4(1.0f, 0.3f, 0.3f, 1.0f),
        _ => new float4(0.8f, 0.8f, 0.8f, 1.0f)
      };
      ImGui.TextColored(stateColor, $"Status: {stateDesc}");
      ImGui.Spacing();

      // Action buttons
      if (state == ShaderLoadTest.ShaderState.Original || state == ShaderLoadTest.ShaderState.BackupExists)
      {
        if (ImGui.Button("Apply Shader Modification (RED -> GREEN)"))
        {
          if (ShaderLoadTest.ApplyModification())
            Console.WriteLine("humble-arteest: Shader modification applied successfully.");
        }
      }

      if (state == ShaderLoadTest.ShaderState.Modified || state == ShaderLoadTest.ShaderState.BackupExists)
      {
        if (ImGui.Button("Restore Original Shader"))
        {
          if (ShaderLoadTest.RestoreOriginal())
            Console.WriteLine("humble-arteest: Shader restored successfully.");
        }
      }

      // Show errors
      if (ShaderLoadTest.LastError != null)
      {
        ImGui.Spacing();
        ImGui.TextColored(new float4(1.0f, 0.3f, 0.3f, 1.0f), $"Error: {ShaderLoadTest.LastError}");
      }

      ImGui.Spacing();
      ImGui.Separator();
      ImGui.TextColored(new float4(0.6f, 0.6f, 0.6f, 1.0f), "Expected result after restart:");
      ImGui.BulletText("If highlight turns GREEN: Shaders load from disk (Approach A is viable!)");
      ImGui.BulletText("If highlight stays RED: Shaders are pre-compiled (need alternative approach)");

      ImGui.Unindent();
    }
  }

  private bool _paddingTestEnabled = false;
  private float3 _paintColor = new float3(1.0f, 0.0f, 0.0f);

  private void RenderPaddingTest()
  {
    if (ImGui.CollapsingHeader("Experiment 0.2: Padding Passthrough Test", ImGuiTreeNodeFlags.DefaultOpen))
    {
      ImGui.Indent();

      ImGui.TextWrapped("Tests whether C# PerInstanceData padding bytes reach the GPU shader. " +
        "Step 1: Apply modified shaders (adds paint fields to vertex/fragment shaders). " +
        "Step 2: Restart game. " +
        "Step 3: Enable paint test toggle — all static parts should turn RED.");
      ImGui.Spacing();

      // Shader state
      var shaderState = PaddingTest.GetShaderState();
      var shaderColor = shaderState switch
      {
        PaddingTest.ShaderState.Original => new float4(0.5f, 1.0f, 0.5f, 1.0f),
        PaddingTest.ShaderState.Modified => new float4(1.0f, 1.0f, 0.0f, 1.0f),
        _ => new float4(1.0f, 0.3f, 0.3f, 1.0f)
      };
      var shaderLabel = shaderState switch
      {
        PaddingTest.ShaderState.Original => "Shaders: ORIGINAL (need modification)",
        PaddingTest.ShaderState.Modified => "Shaders: MODIFIED (paint fields added)",
        _ => $"Shaders: ERROR — {PaddingTest.LastError}"
      };
      ImGui.TextColored(shaderColor, shaderLabel);
      ImGui.Spacing();

      // Shader action buttons
      if (shaderState == PaddingTest.ShaderState.Original)
      {
        if (ImGui.Button("Apply Modified Shaders (vert + frag)"))
        {
          if (PaddingTest.ApplyShaderModifications())
            Console.WriteLine("humble-arteest: Padding test shaders applied.");
        }
      }
      if (shaderState == PaddingTest.ShaderState.Modified)
      {
        if (ImGui.Button("Restore Original Shaders"))
        {
          if (PaddingTest.RestoreOriginalShaders())
            Console.WriteLine("humble-arteest: Shaders restored.");
        }
      }

      ImGui.Spacing();
      ImGui.Separator();
      ImGui.Spacing();

      // Paint test toggle + color picker
      if (ImGui.Checkbox("Enable Paint Test", ref _paddingTestEnabled))
      {
        PaddingTest.Enabled = _paddingTestEnabled;
      }
      ImGui.SameLine(0, 10);
      if (ImGui.ColorEdit3("##paintColor", ref _paintColor, ImGuiColorEditFlags.NoInputs | ImGuiColorEditFlags.NoLabel))
      {
        PaddingTest.PaintR = _paintColor.X;
        PaddingTest.PaintG = _paintColor.Y;
        PaddingTest.PaintB = _paintColor.Z;
      }

      if (_paddingTestEnabled)
      {
        ImGui.TextColored(new float4(1.0f, 0.5f, 0.0f, 1.0f),
          "ACTIVE — parts should be tinted with the selected color");
      }

      // Errors
      if (PaddingTest.LastError != null)
      {
        ImGui.Spacing();
        ImGui.TextColored(new float4(1.0f, 0.3f, 0.3f, 1.0f), $"Error: {PaddingTest.LastError}");
      }

      ImGui.Spacing();
      ImGui.Separator();
      ImGui.TextColored(new float4(0.6f, 0.6f, 0.6f, 1.0f), "Expected result:");
      ImGui.BulletText("If parts turn RED with toggle ON: Padding passthrough works!");
      ImGui.BulletText("If parts look normal: Struct alignment mismatch — need debugging.");

      ImGui.Unindent();
    }
  }
}
