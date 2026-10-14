using System;
using System.Linq;
using System.Runtime.CompilerServices;
using Aion2DPSPro.Schedules;

// Runs as part of the existing net8 regression executable, without a test framework.
internal static class EventScheduleRegressionTests
{
    [ModuleInitializer]
    internal static void Run()
    {
        var profile = new ScheduleProfile("fixture", "UTC fixture", "UTC", "fixture", "2026-10-14",
            new[] { new ScheduleEvent("rift", "Rift", new[] { 0, 3, 6, 9, 12, 15, 18, 21 }, 0, 10) });
        var before = EventSchedule.Evaluate(profile, new DateTimeOffset(2026, 10, 14, 2, 59, 59, TimeSpan.Zero)).Single();
        Check(!before.InEntryWindow && before.Start == new DateTimeOffset(2026, 10, 14, 3, 0, 0, TimeSpan.Zero), "upcoming event");
        var active = EventSchedule.Evaluate(profile, new DateTimeOffset(2026, 10, 14, 3, 5, 0, TimeSpan.Zero)).Single();
        Check(active.InEntryWindow && active.End == new DateTimeOffset(2026, 10, 14, 3, 10, 0, TimeSpan.Zero), "entry countdown");
        var closed = EventSchedule.Evaluate(profile, new DateTimeOffset(2026, 10, 14, 3, 10, 0, TimeSpan.Zero)).Single();
        Check(!closed.InEntryWindow && closed.Start.Hour == 6, "entry end is exclusive");
        var midnight = EventSchedule.Evaluate(profile, new DateTimeOffset(2026, 10, 14, 23, 59, 0, TimeSpan.Zero)).Single();
        Check(midnight.Start == new DateTimeOffset(2026, 10, 15, 0, 0, 0, TimeSpan.Zero), "next-day rollover");
        var late = EventSchedule.Evaluate(profile, new DateTimeOffset(2026, 10, 14, 0, 5, 0, TimeSpan.Zero)).Single();
        Check(late.InEntryWindow && late.Start.Day == 14, "midnight active window");
    }

    private static void Check(bool valid, string label)
    {
        if (!valid) throw new InvalidOperationException("Event schedule regression failed: " + label);
    }
}
