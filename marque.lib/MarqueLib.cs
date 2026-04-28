using System;
using System.Collections.Generic;
using System.Linq;
using Brutal.ImGuiApi;
using KSA;
using MeowSci.KsaAbstractions;

namespace MeowSci.MarqueLib;

/// <summary>
/// Orbit line visibility menu for vehicles and celestial bodies.
/// Injected into the game's View menu bar via Harmony patch.
/// </summary>
public sealed class MarqueLib
{
  private static readonly ImInputString _everythingFilter = new(128);

  public static void DrawMarqueMenus()
  {
    if (Universe.CurrentSystem == null) return;

    if (ImGui.BeginMenu("Marque"))
    {
      ImGui.PushItemFlag(ImGuiItemFlags.AutoClosePopups, false);
      try
      {
        DrawTopLevelBulkItems();
        ImGui.Separator();
        DrawVehiclesMenu();
        DrawSolMenu();
        DrawEverythingMenu();
      }
      catch (Exception ex)
      {
        Console.WriteLine($"marque: Error drawing menus: {ex.Message}");
      }
      ImGui.PopItemFlag();
      ImGui.EndMenu();
    }
    ImGui.Separator();
  }

  private static void DrawTopLevelBulkItems()
  {
    var allOrbiters = CelestialProvider.GetAllOrbiters();
    if (allOrbiters.Count == 0) return;

    if (ImGui.MenuItem("All"))
      foreach (var o in allOrbiters) o.ShowOrbit = true;

    if (ImGui.MenuItem("None"))
      foreach (var o in allOrbiters) o.ShowOrbit = false;

    if (ImGui.MenuItem("Planetoids"))
      foreach (var o in allOrbiters)
        if (o is not Asteroid and not Comet) o.ShowOrbit = true;
  }

  private static void DrawVehiclesMenu()
  {
    if (!ImGui.BeginMenu("Vehicles")) return;

    var vehicles = VehicleProvider.GetAllVehicles();
    if (vehicles.Count == 0)
    {
      ImGui.MenuItem("(no vehicles)", default(ImString), false, false);
      ImGui.EndMenu();
      return;
    }

    var sorted = vehicles.OrderBy(v => v.Id).ToList();

    if (ImGui.MenuItem("All"))
      foreach (var v in sorted) v.ShowOrbit = true;

    if (ImGui.MenuItem("None"))
      foreach (var v in sorted) v.ShowOrbit = false;

    ImGui.Separator();

    foreach (var v in sorted)
    {
      bool show = v.ShowOrbit;
      if (ImGui.MenuItem(v.Id, default(ImString), show))
        v.ShowOrbit = !show;
    }

    ImGui.EndMenu();
  }

  private static void DrawSolMenu()
  {
    var sun = Universe.CurrentSystem?.GetWorldSun();
    if (sun == null) return;

    if (!ImGui.BeginMenu(sun.Id)) return;

    var allCelestials = new List<Celestial>();
    CollectAllCelestials(sun, allCelestials);

    var immediateChildren = sun.Children.OfType<Celestial>().ToList();

    // All — enables this level + all descendants
    if (ImGui.MenuItem("All"))
      foreach (var c in allCelestials) c.ShowOrbit = true;

    // Children — enables only the immediate children
    if (ImGui.MenuItem("Children"))
      foreach (var c in immediateChildren) c.ShowOrbit = true;

    // Planetoids — like Children but excludes asteroids and comets
    if (ImGui.MenuItem("Planetoids"))
      foreach (var c in immediateChildren)
        if (c is not Asteroid and not Comet) c.ShowOrbit = true;

    if (ImGui.MenuItem("None"))
      foreach (var c in allCelestials) c.ShowOrbit = false;

    ImGui.Separator();

    DrawCelestialChildren(sun);

    ImGui.EndMenu();
  }

  private static void DrawCelestialChildren(IParentBody parent)
  {
    var children = parent.Children
      .OfType<Celestial>()
      .OrderBy(c => c.Id)
      .ToList();

    foreach (var celestial in children)
    {
      var subCelestials = celestial.Children.OfType<Celestial>().ToList();

      if (subCelestials.Count > 0)
      {
        if (ImGui.BeginMenu(celestial.Id))
        {
          var descendants = new List<Celestial> { celestial };
          CollectAllCelestials(celestial, descendants);

          var immediateChildren = subCelestials;

          // All — enables self + all descendants
          if (ImGui.MenuItem("All"))
            foreach (var d in descendants) d.ShowOrbit = true;

          // Children — enables only the immediate child celestials
          if (ImGui.MenuItem("Children"))
            foreach (var c in immediateChildren) c.ShowOrbit = true;

          // Planetoids — self (if not asteroid/comet) + immediate children that aren't asteroid/comet
          if (ImGui.MenuItem("Planetoids"))
          {
            if (celestial is not Asteroid and not Comet) celestial.ShowOrbit = true;
            foreach (var c in immediateChildren)
              if (c is not Asteroid and not Comet) c.ShowOrbit = true;
          }

          if (ImGui.MenuItem("None"))
            foreach (var d in descendants) d.ShowOrbit = false;

          ImGui.Separator();

          // Self toggle — shows checkmark for this celestial
          bool selfShow = celestial.ShowOrbit;
          if (ImGui.MenuItem(celestial.Id, default(ImString), selfShow))
            celestial.ShowOrbit = !selfShow;

          ImGui.Separator();

          DrawCelestialChildren(celestial);

          ImGui.EndMenu();
        }
      }
      else
      {
        bool show = celestial.ShowOrbit;
        if (ImGui.MenuItem(celestial.Id, default(ImString), show))
          celestial.ShowOrbit = !show;
      }
    }
  }

  private static void DrawEverythingMenu()
  {
    if (!ImGui.BeginMenu("Everything")) return;

    if (ImGui.IsWindowAppearing())
      _everythingFilter.Clear();

    var allOrbiters = CelestialProvider.GetAllOrbiters()
      .OrderBy(o => (o as Astronomical)?.Id ?? "")
      .ToList();

    if (allOrbiters.Count == 0)
    {
      ImGui.MenuItem("(none)", default(ImString), false, false);
      ImGui.EndMenu();
      return;
    }

    ImGui.SetNextItemWidth(-1);
    ImGui.InputTextWithHint("##filter", "filter..."u8, _everythingFilter);
    string filterText = _everythingFilter.ToString().Trim();

    foreach (var orbiter in allOrbiters)
    {
      var id = (orbiter as Astronomical)?.Id ?? "";
      if (filterText.Length > 0 && !id.Contains(filterText, StringComparison.OrdinalIgnoreCase)) continue;

      bool show = orbiter.ShowOrbit;
      if (ImGui.MenuItem(id, default(ImString), show))
        orbiter.ShowOrbit = !show;
    }

    ImGui.EndMenu();
  }

  private static void CollectAllCelestials(IParentBody parent, List<Celestial> result)
  {
    foreach (var child in parent.Children)
    {
      if (child is Celestial celestial)
      {
        result.Add(celestial);
        CollectAllCelestials(celestial, result);
      }
    }
  }
}
