using System;
using System.Text.Json;
using MeowSci.Unscience.Contracts;

internal static class RuntimeOwnershipChecks
{
    public static void Run()
    {
        int installed = 0, removed = 0;
        bool failActivation = true, failRelease = false;
        var lease = new RuntimeActivation(() => { installed++; if (failActivation) throw new InvalidOperationException(); },
            () => { if (failRelease) throw new InvalidOperationException(); removed++; });
        lease.Sync(false); Check(installed == 0, "startup is inert");
        Throws(() => lease.Sync(true)); Check(removed == 1 && !lease.Active, "partial activation rolls back");
        lease.Sync(true); Check(installed == 1, "failed activation does not retry every frame");
        failActivation = false; lease.Retry(); lease.Sync(true); lease.Sync(true);
        Check(installed == 2 && lease.Active, "single install while demanded");
        failRelease = true; Throws(() => lease.Sync(false)); Check(lease.Active, "failed release retains ownership");
        failRelease = false; lease.Sync(false); lease.Sync(false); Check(removed == 2, "successful release is idempotent");

        int value = 7, captures = 0;
        var shared = new SharedRestoration(); var key = new object();
        IDisposable Acquire() => shared.Acquire(key, () => { captures++; int original = value; return () => value = original; });
        var first = Acquire(); value = 12; var second = Acquire(); value = 19;
        first.Dispose(); Check(value == 19 && captures == 1, "intermediate owner preserves shared policy");
        second.Dispose(); second.Dispose(); Check(value == 7, "last owner restores exact original once");
        bool failRestore = true;
        var retryKey = new object();
        var retry = shared.Acquire(retryKey, () => () => { if (failRestore) throw new InvalidOperationException(); value = 7; });
        value = 91; Throws(retry.Dispose); Check(value == 91, "failed baseline restoration preserves lease");
        failRestore = false; retry.Dispose(); Check(value == 7, "baseline restoration can retry");
        var third = Acquire(); value = 5; third.Dispose(); Check(value == 7 && captures == 2, "new lifetime captures anew");

        var ranges = new ReleasedRanges(); ranges.Add(100, 20); ranges.Add(140, 30);
        Check(ranges.Trim(170, 100) == 140, "tail reclamation stops at live gap");
        ranges.Add(120, 20); Check(ranges.Trim(140, 100) == 100 && ranges.Bytes == 0, "out-of-order removal reclaims contiguous tail");
        ranges.Add(80, 20); Check(ranges.Trim(100, 100) == 100, "stock baseline cannot be reclaimed");
        ranges.Add(100, 20); Check(ranges.Trim(130, 100) == 130, "external allocation prevents unsafe rewind");

        Throws(() => DraftValueValidation.Range(999, -1, 7, "preset"));
        Throws(() => DraftValueValidation.Json(JsonDocument.Parse("{\"nested\":[1e100]}").RootElement));
        Throws(() => DraftValueValidation.RequiredShape(JsonDocument.Parse("{\"recipe\":null}").RootElement,
            JsonDocument.Parse("{\"recipe\":{\"items\":[]}}").RootElement));
        DraftValueValidation.Json(JsonSerializer.SerializeToElement(float.MaxValue));
        DraftValueValidation.Json(JsonDocument.Parse("{\"items\":[1,2],\"optional\":null}").RootElement);
        Console.WriteLine("PASS: activation rollback/retry, retained failed release, shared baselines, safe mesh-tail reclamation, detached value validation.");
    }
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    private static void Throws(Action action) { try { action(); } catch (Exception) { return; } throw new Exception("Expected rejection"); }
}
