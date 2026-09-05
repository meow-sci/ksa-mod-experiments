using System;
using System.Text.Json;
using MeowSci.GraffitiLib;
using MeowSci.ZippoLib;

internal static class FeatureAlgorithmChecks
{
    public static void Run()
    {
        var spray = new SprayCadence();
        Check(!spray.Tick(0, true, true, true, 100), "UI press cannot spray");
        Check(!spray.Tick(.1, false, true, false, 100), "UI drag into world cannot start a stroke");
        Check(spray.Tick(1, true, true, false, 100), "world press sprays immediately");
        Check(!spray.Tick(1.05, false, true, false, 100), "hold respects interval");
        Check(spray.Tick(1.11, false, true, false, 100), "hold sprays at next tick");
        Check(spray.Tick(5, false, true, false, 100), "long frame emits once");
        Check(!spray.Tick(5, false, true, false, 100), "no catch-up burst");
        Check(!spray.Tick(6, false, false, false, 100), "release stops spray");
        Check(spray.Tick(6.01, true, true, false, 100), "new stroke sprays immediately");
        Check(!spray.Tick(7, false, true, true, 100), "UI capture cancels stroke");
        Check(!spray.Tick(8, false, true, false, 100), "capture cannot resume without a world press");
        spray.Tick(9, true, true, false, 100); spray.Reset();
        Check(!spray.Tick(10, false, true, false, 100), "workspace gesture cancellation prevents continued hold");

        var timing = new DiscoTiming { Hold = 1, Transition = 2, Easing = 0 };
        timing.Validate();
        Check(timing.Sample(.9) == (0, 0), "color holds at first endpoint");
        Check(timing.Sample(2) == (0, .5f), "transition interpolates after hold");
        Check(timing.Sample(3) == (1, 0), "next endpoint begins its hold");
        Check(timing.Sample(300002) == (100000, .5f), "long frames jump directly to correct phase");
        var independent = new DiscoTiming { Hold = 0, Transition = 4, Easing = 0 };
        Check(independent.Sample(3) == (0, .75f), "channels have independent clocks");
        foreach (int easing in new[] { 0, 1, 2, 3 })
        {
            timing.Easing = easing;
            float previous = 0;
            for (int i = 0; i <= 100; i++)
            {
                float value = timing.Sample(1 + i / 50.0).Mix;
                if (i == 100) break; // exact cycle boundary starts the next endpoint
                Check(value >= previous && value <= 1, "easing is monotonic and bounded"); previous = value;
            }
        }
        var options = new JsonSerializerOptions { IncludeFields = true };
        var saved = JsonSerializer.Serialize(timing, options);
        var restored = JsonSerializer.Deserialize<DiscoTiming>(saved, options)!;
        timing.Hold = 12;
        Check(restored.Hold == 1 && restored.Transition == 2 && restored.Easing == 3, "detached timing round trip");
        restored.Transition = 0;
        bool rejected = false;
        try { restored.Validate(); } catch (InvalidOperationException) { rejected = true; }
        Check(rejected, "zero-duration recipe rejected before apply");
        Console.WriteLine("PASS: spray strokes/cadence/cancellation and Disco independent timing/easing/serialization.");
    }
    private static void Check(bool condition, string message)
    { if (!condition) throw new Exception(message); }
}
