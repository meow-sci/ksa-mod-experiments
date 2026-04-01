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
      ImGui.Spacing();
      RenderMaterialColorTest();
      ImGui.Spacing();
      RenderTemperatureTest();
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

  private (string Name, int Handle)[] _materialList = Array.Empty<(string, int)>();
  private float4 _materialTestColor = new float4(1.0f, 0.0f, 0.0f, 1.0f);
  private int _selectedMaterialIdx = -1;

  private void RenderMaterialColorTest()
  {
    if (ImGui.CollapsingHeader("Experiment 0.3: Material AlbedoColor Test", ImGuiTreeNodeFlags.DefaultOpen))
    {
      ImGui.Indent();

      ImGui.TextWrapped("Tests whether modifying MaterialData.AlbedoColor in the GPU buffer " +
        "affects the indirect rendering path. Expected: NO visible change (confirming " +
        "the indirect path ignores AlbedoColor).");
      ImGui.Spacing();

      // Initialize button
      if (!MaterialColorTest.IsInitialized)
      {
        if (ImGui.Button("Initialize Material System"))
        {
          if (MaterialColorTest.Initialize())
            _materialList = MaterialColorTest.GetMaterialList();
        }
      }
      else
      {
        ImGui.TextColored(new float4(0.5f, 1.0f, 0.5f, 1.0f),
          $"Material system initialized: {_materialList.Length} materials found");
        ImGui.Spacing();

        // Color picker — auto-apply to all materials on change
        if (ImGui.ColorEdit4("Test Color", ref _materialTestColor, ImGuiColorEditFlags.NoInputs | ImGuiColorEditFlags.AlphaBar))
        {
          ApplyColorToAllMaterials();
        }
        ImGui.Spacing();

        // Material list with apply buttons
        if (_materialList.Length > 0)
        {
          ImGui.Text("Materials (click to apply AlbedoColor):");
          int displayCount = Math.Min(_materialList.Length, 50);

          if (ImGui.BeginChild("MaterialList", new float2(0, 200), ImGuiChildFlags.Borders))
          {
            for (int i = 0; i < displayCount; i++)
            {
              var (name, handle) = _materialList[i];
              bool isSelected = i == _selectedMaterialIdx;

              if (ImGui.Selectable($"[{handle}] {name}", isSelected))
              {
                _selectedMaterialIdx = i;
                var color = _materialTestColor;
                MaterialColorTest.ModifyAlbedoColor(handle, color);
              }
            }

            if (_materialList.Length > displayCount)
              ImGui.Text($"... and {_materialList.Length - displayCount} more");
          }
          ImGui.EndChild();

          ImGui.Spacing();
          if (ImGui.Button("Apply to ALL materials"))
          {
            ApplyColorToAllMaterials();
          }
        }
      }

      // Status
      if (MaterialColorTest.StatusMessage != null)
      {
        ImGui.Spacing();
        ImGui.TextColored(new float4(0.7f, 0.7f, 1.0f, 1.0f), MaterialColorTest.StatusMessage);
      }

      // Errors
      if (MaterialColorTest.LastError != null)
      {
        ImGui.Spacing();
        ImGui.TextColored(new float4(1.0f, 0.3f, 0.3f, 1.0f), $"Error: {MaterialColorTest.LastError}");
      }

      ImGui.Spacing();
      ImGui.Separator();
      ImGui.TextColored(new float4(0.6f, 0.6f, 0.6f, 1.0f), "Expected result:");
      ImGui.BulletText("If parts change color: Approach B (material cloning) is viable!");
      ImGui.BulletText("If no change: Indirect path ignores AlbedoColor (expected).");

      ImGui.Unindent();
    }
  }

  private bool _temperatureTestEnabled = false;
  private float _temperatureValue = 1.0f;
  private float _tfiThicknessValue = 0.0f;

  private void RenderTemperatureTest()
  {
    if (ImGui.CollapsingHeader("Experiment 0.4: Temperature Visual Test", ImGuiTreeNodeFlags.DefaultOpen))
    {
      ImGui.Indent();

      ImGui.TextWrapped("Tests per-instance visual modification via the Temperature field. " +
        "No shader modifications needed — Temperature is already wired from C# through " +
        "to the fragment shader. Toggle the override and adjust sliders.");
      ImGui.Spacing();

      // Enable toggle
      if (ImGui.Checkbox("Enable Temperature Override", ref _temperatureTestEnabled))
      {
        TemperatureTest.Enabled = _temperatureTestEnabled;
      }
      ImGui.Spacing();

      // Temperature slider
      if (ImGui.SliderFloat("Temperature", ref _temperatureValue, 0.0f, 1.0f))
      {
        TemperatureTest.Temperature = _temperatureValue;
      }

      // TFI Thickness slider
      if (ImGui.SliderFloat("TFI Thickness", ref _tfiThicknessValue, 0.0f, 1.0f))
      {
        TemperatureTest.TfiThickness = _tfiThicknessValue;
      }

      if (_temperatureTestEnabled)
      {
        ImGui.Spacing();
        ImGui.TextColored(new float4(1.0f, 0.5f, 0.0f, 1.0f),
          $"ACTIVE — Temperature={_temperatureValue:F2}, TFI={_tfiThicknessValue:F2}");
      }

      // Errors
      if (TemperatureTest.LastError != null)
      {
        ImGui.Spacing();
        ImGui.TextColored(new float4(1.0f, 0.3f, 0.3f, 1.0f), $"Error: {TemperatureTest.LastError}");
      }

      ImGui.Spacing();
      ImGui.Separator();
      ImGui.TextColored(new float4(0.6f, 0.6f, 0.6f, 1.0f), "Expected result:");
      ImGui.BulletText("Dynamic parts should glow orange/red at high Temperature values");

      ImGui.Unindent();
    }
  }

  private void ApplyColorToAllMaterials()
  {
    var color = _materialTestColor;
    int success = 0;
    foreach (var (_, handle) in _materialList)
    {
      if (MaterialColorTest.ModifyAlbedoColor(handle, color))
        success++;
    }
  }
}
