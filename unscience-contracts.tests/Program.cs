using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using MeowSci.Unscience.Contracts;

FeatureAlgorithmChecks.Run();
RuntimeOwnershipChecks.Run();

var directory = Path.Combine(Path.GetTempPath(), "unscience-tests-" + Guid.NewGuid().ToString("N"));
try
{
    var store = new WorkspaceStore(directory);
    var original = new WorkspaceDocument { SelectedFeature = "paint", MainWindowVisible = false, LiveWindowVisible = true, SelectedLiveItem = "paint/instance", FeatureFilter = "color", LoadFilter = "laser" };
    original.Features["paint"] = new() { Visible = false, Draft = State(42) };
    original.Features["future-feature"] = new() { Draft = State(9) };
    var saved = store.Save(original, "  Laser Eyes  ", false);
    Check(saved.Name == "Laser Eyes" && store.List().Count == 1, "name normalization");
    Check(original.Name == "Untitled", "save does not mutate authoring input");
    Throws<IOException>(() => store.Save(original, "laser eyes", false));
    var replacement = store.Save(original, "LASER EYES", true);
    Check(replacement.Id == saved.Id && store.List().Count == 1, "overwrite keeps identity");
    Check(File.Exists(Path.Combine(directory, saved.Id + ".json.bak")), "overwrite backup");
    var read = store.Read(Path.Combine(directory, saved.Id + ".json"));
    Check(!read.MainWindowVisible && read.LiveWindowVisible && read.SelectedLiveItem == "paint/instance" && read.FeatureFilter == "color" && read.LoadFilter == "laser", "window visibility and navigation round trip");
    Check(!read.Features["paint"].Visible && read.Features.ContainsKey("future-feature"), "visibility and unknown features round trip");
    var unicode = store.Save(original, "Cafe\u0301", false);
    Throws<IOException>(() => store.Save(original, "Café", false));
    var a = new Fake("paint", 1); var b = new Fake("absent", 2);
    var defaults = new Dictionary<string, DraftState> { ["paint"] = State(0), ["absent"] = State(3) };
    var prepared = WorkspaceRestore.Prepare(read, new[] { a, b }, defaults);
    Check(a.DraftValue == 1 && b.DraftValue == 2, "prepare is read only");
    prepared();
    Check(a.DraftValue == 42 && b.DraftValue == 3, "whole restore resets absent feature to defaults");
    Check(a.LiveValue == 789 && b.LiveValue == 789, "live state unaffected");
    read.Features["absent"] = new() { Draft = State(-1) };
    Throws<InvalidDataException>(() => WorkspaceRestore.Prepare(read, new[] { a, b }, defaults));
    Check(a.DraftValue == 42 && b.DraftValue == 3, "malformed later feature cannot partially restore");
    read.Features["paint"].Draft = State(5); read.Features["absent"].Draft = State(13);
    b.FailOnThirteen = true;
    Throws<InvalidOperationException>(() => WorkspaceRestore.Prepare(read, new[] { a, b }, defaults)());
    Check(a.DraftValue == 42 && b.DraftValue == 3, "setter failure rolls back whole workspace");
    string malformed = Path.Combine(directory, "broken.json"); File.WriteAllText(malformed, "{");
    Check(store.List().Any(s => s.Document == null && s.Error != null), "malformed saves remain visible as errors");
    original.Version = 99; Throws<InvalidDataException>(() => WorkspaceStore.Write(Path.Combine(directory, "future.json"), original));
    original.Version = 1; original.Windows["main"] = new() { Width = float.NaN };
    Throws<InvalidDataException>(() => WorkspaceStore.Write(Path.Combine(directory, "invalid-window.json"), original));
    Check(!Directory.EnumerateFiles(directory, "*.tmp").Any(), "atomic writes leave no temporary files");
    Console.WriteLine("PASS: storage, overwrite, unknown features, visibility, complete restore, transaction rollback, live-state isolation, malformed data.");
}
finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }

static DraftState State(int value) => new() { Fields = new() { ["value"] = JsonSerializer.SerializeToElement(value) } };
static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
static void Throws<T>(Action action) where T : Exception
{ try { action(); } catch (T) { return; } throw new Exception("Expected " + typeof(T).Name); }
sealed class Fake(string id, int value) : IWorkspaceParticipant
{
    public string FeatureId => id;
    public int DraftValue = value;
    public int LiveValue = 789;
    public bool FailOnThirteen;
    public DraftState CaptureDraft() => new() { Fields = new() { ["value"] = JsonSerializer.SerializeToElement(DraftValue) } };
    public Action PrepareRestore(DraftState state)
    {
        int decoded = state.Fields["value"].GetInt32();
        if (decoded < 0) throw new InvalidDataException();
        return () => { DraftValue = decoded; if (decoded == 13 && FailOnThirteen) throw new InvalidOperationException(); };
    }
}
