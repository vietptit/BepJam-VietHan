Run from the project root with the .NET 10 SDK (the sources compile as C# 9):

```powershell
dotnet restore Tools/DifficultyHarness --configfile Tools/DifficultyHarness/NuGet.Config
dotnet run --project Tools/DifficultyHarness -c Release --no-restore
```

The harness compiles the real GameData, LevelGen, Kitchen and Editor report sources.
Only Unity graphics and Editor APIs are stubbed. It checks all 50 levels' exact
ingredient multisets, six plates, minimum tap counts, measured difficulty
bands and winning witnesses with VIP patience enabled. Every pantry must show
`min(16, stock)` crates across four columns, with no hidden stock behind an empty visible
cell and column total lengths differing by at most two (generation currently guarantees
at most one). Each portion has its own crate. Boundary checks cover stock totals 2-33,
including both sides of the 16-cell capacity, and replay hidden-crate refills. The first
three tutorial layouts retain their original row assignment. The harness also exercises
immediate wins, leftover detection, returns, repeated recipe parts and actual rapid tapping.
Timer-free regressions cover ten minutes of idle play followed by a three-star win,
assist-based stars, the 1.8x VIP patience ramp, VIP expiry, full plates during flight,
stuck loss/revival and empty stock. Actual careless replays must end with a win or one
of the three supported loss kinds; hitting the test watchdog is a failure.

The target bands are unchanged. Days 5-9 may finish up to 10 percentage points below
their band; the report explicitly labels the shortfall. All later levels must meet their
band. The full-grid rules always apply, including the fallback layout. Menus budget stock
using `hiddenRows` as extra depth **after** the visible grid is filled. Depth ramps with
days and bosses, with one fewer hidden row for difficult four-part menus that share only
one ingredient with their companion recipe; whole recipes can round depth by one crate.

For an independent sample that is never used during candidate selection:

```powershell
dotnet run --project Tools/DifficultyHarness -c Release --no-restore -- --audit
```

This additionally checks 2,000 playouts per selected layout against the same bands and
early allowance. Audit time is separate from generation time.

`-- --report` invokes the same report command as **BepJam/Report Difficulty** in Unity.
Both modes write `Tools/difficulty_report.txt`. Generation is bounded by elapsed time;
the chosen seed can differ between machines. Each accepted candidate completes 200
uniform random stable-state simulations, and a deadline returns the best proven layout.
The work deadline is 150ms, with a 12ms reserve before starting another candidate; the
harness allows 10ms of scheduler jitter. Fully visible pantries skip placement calibration
because permuting selectable crates cannot change uniform random difficulty.

Both report paths include `visible`, `hiddenPerColumn max`, the unchanged target band,
explicit early shortfalls and a final summary of band results and maximum generation time.

There is no level time knob or time column in either report path. The random simulator
waits for transitions between taps to measure plate/stuck planning failures; every selected
layout also passes a real Kitchen replay with VIP patience. Replays have a test-only
watchdog of twice the serial action/animation work plus ten seconds (minimum 30 seconds).
That watchdog cannot cause a gameplay loss or change difficulty.
