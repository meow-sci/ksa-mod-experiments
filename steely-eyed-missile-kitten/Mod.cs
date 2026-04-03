using System;
using System.Collections.Generic;
using Brutal.Numerics;
using Brutal.ImGuiApi;
using KSA;
using StarMap.API;
using MeowSci.KsaAbstractions;
using MeowSci.SteelyEyedMissileKittenLib.Events;
using MeowSci.SteelyEyedMissileKittenLib.Missions;
using MeowSci.SteelyEyedMissileKittenLib.Monitoring;
using MeowSci.SteelyEyedMissileKittenLib.Persistence;
using MeowSci.SteelyEyedMissileKittenLib.UI;

namespace MeowSci.SteelyEyedMissileKitten;

[StarMapMod]
public class Mod
{
    public bool ImmediateUnload => false;

    private bool _isInitialized;
    private bool _isDisposed;
    private bool _windowVisible;

    // Core systems
    private MonitoringConfig _config = null!;
    private EventBus _eventBus = null!;
    private EventDetector _detector = null!;
    private MonitoringLoop _monitoringLoop = null!;

    // Persistence
    private EventDatabase _database = null!;
    private EventWriter _eventWriter = null!;

    // Missions
    private MissionManager _missionManager = null!;

    // UI state
    private readonly List<FlightEvent> _uiEventFeed = new();
    private double _flushTimer;
    private const double FlushIntervalSec = 5.0;

    [StarMapImmediateLoad]
    public void OnImmediateLoad() { }

    [StarMapAllModsLoaded]
    public void OnFullyLoaded()
    {
        try
        {
            Patcher.Patch();

            _config = new MonitoringConfig();
            _eventBus = new EventBus();
            _detector = new EventDetector();
            _monitoringLoop = new MonitoringLoop(_config, _detector, _eventBus);

            string dbDir = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "My Games", "Kitten Space Agency", ".steely-eyed-missile-kitten");
            System.IO.Directory.CreateDirectory(dbDir);
            string dbPath = System.IO.Path.Combine(dbDir, "events.db");

            _database = new EventDatabase(dbPath);
            _database.Initialize();

            _eventWriter = new EventWriter(_database, _eventBus);

            _eventBus.OnEvent += evt => _uiEventFeed.Add(evt);

            string assemblyDir = System.IO.Path.GetDirectoryName(typeof(Mod).Assembly.Location) ?? ".";
            string bundledMissionsDir = System.IO.Path.Combine(assemblyDir, "missions");
            string userMissionsDir = System.IO.Path.Combine(dbDir, "missions");

            _missionManager = new MissionManager(MissionLoader.LoadAllMissions(bundledMissionsDir, userMissionsDir), _database);
            _eventBus.OnEvent += evt => _missionManager.OnEvent(evt);

            _isInitialized = true;
            Console.WriteLine("steely-eyed-missile-kitten: loaded");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"steely-eyed-missile-kitten: Error during initialization: {ex.Message}");
        }
    }

    [StarMapBeforeGui]
    public void OnBeforeUi(double dt)
    {
        try
        {
            if (!_isInitialized || _isDisposed) return;

            _monitoringLoop.Update(dt);
            _missionManager.EvaluateAll(_monitoringLoop.CurrentSnapshots);

            _flushTimer += dt;
            if (_flushTimer >= FlushIntervalSec)
            {
                _flushTimer = 0;
                _eventWriter.Flush();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"steely-eyed-missile-kitten: Error in OnBeforeUi: {ex.Message}");
        }
    }

    [StarMapAfterGui]
    public void OnAfterUi(double dt)
    {
        if (!_isInitialized || _isDisposed) return;
        if (ImGui.IsKeyPressed(ImGuiKey.F11))
            _windowVisible = !_windowVisible;
        if (_windowVisible)
            RenderWindow();
    }

    [StarMapUnload]
    public void Unload()
    {
        try
        {
            _eventWriter?.Dispose();
            _database?.Dispose();
            Patcher.Unload();
            _isDisposed = true;
            Console.WriteLine("steely-eyed-missile-kitten: unloaded");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"steely-eyed-missile-kitten: Error during unload: {ex.Message}");
        }
    }

    private void RenderWindow()
    {
        ImGui.SetNextWindowSize(new float2(950, 700), ImGuiCond.FirstUseEver);
        if (ImGui.Begin("Steely-Eyed Missile Kitten", ref _windowVisible))
        {
            if (ImGui.BeginTabBar("##semk_tabs"))
            {
                if (ImGui.BeginTabItem(" Telemetry "))
                {
                    MonitorUI.Render(_monitoringLoop, _config);
                    ImGui.EndTabItem();
                }
                if (ImGui.BeginTabItem(" Events "))
                {
                    bool clearRequested = EventFeedUI.Render(_uiEventFeed);
                    if (clearRequested)
                        _uiEventFeed.Clear();
                    ImGui.EndTabItem();
                }
                if (ImGui.BeginTabItem(" Missions "))
                {
                    double simTime = SimTimeProvider.GetElapsedTime().Seconds();
                    MissionUI.Render(_missionManager, _monitoringLoop.CurrentSnapshots, simTime);
                    ImGui.EndTabItem();
                }
                ImGui.EndTabBar();
            }
        }
        ImGui.End();
    }

}

