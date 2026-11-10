# Standalone QA audit

From `Assets/_Game/Scripts`, with the installed .NET SDK:

```powershell
./Editor/LogicAudit/run.ps1
```

The runner compiles as **C# 9**, without NuGet or Unity execution. All build output
goes to a unique temporary directory. Harness sources use `.cs.txt` so Unity
imports them as text instead of compiling test entry points or graphics stubs.

It compiles the actual `Kitchen`, `LevelGen`, `GameData`, `Chapters`, `Album`,
and `AutoPlayPolicy` sources. Unity graphics, preferences, and the QA-only
progress facade are stubbed; gameplay is not. Checks include:

- Two independently generated layouts for every level, both the solver witness
  and the exact AutoPilot policy at 120, 60, 30, and 10 FPS: **800 replays**.
  The second generation includes periodic 200 ms frames.
- Exactly one Win on the final delivery, every order sent/completed once, all
  guests finished, empty pantry/plates/stations, and no events after ending.
- Exact stock, minimal witness length, full/balanced pantry, and hidden refills.
- VIP gather/delivery/station priority, reservation swaps and aliases, multiple
  VIPs, delivery already in flight, and fallback to normal customers.
- Repeated recipe parts, returns/revival, rapid careless tapping, VIP expiry,
  empty/stuck loss, ten minutes of non-VIP idle time, and stars/assists.
- Wall-time stall and loss diagnostics, chapter boundaries, all requested
  background paths, night-to-day-to-original fallback, completed-chapter photos,
  and nested album QA previews with no save writes.

A second executable compiles the **actual complete `JuicyButton` class and
`AutoPilot.Click` method**, extracted from their source files. It reproduces the
old click-only failure and verifies the fixed pointer-down/up/click sequence,
including eligibility, repeated clicks, disabled buttons, and cancelled drags.
Only graphics/tweens and event dispatch are substituted for this input test.

## Failure and fix

Level 37 reaches a clean Kitchen win. AutoPilot previously tried to dismiss
the following culture card using a click event alone. `JuicyButton` requires an
armed pointer-down and `eligibleForClick`, so the card stayed open and the win
popup wait timed out. All QA button activations now use a valid press sequence.
Kitchen rules and level generation are unchanged.

AutoPilot shares its move policy with this harness. A loss or lack of progress
now stops QA with the level, reason, elapsed time, orders, VIP patience, plate
and station states, stock, and active popup names. Its test watchdog is derived
from level work, rather than a fixed 120-second screenshot loop.

## Unity capture pass

Run AutoPilot normally, including `bj_qa_level = 37` or `-level 37`.
Besides the existing shots, it now requests:

- `20_ch_L6`, `20_ch_L11`, `20_ch_L16`, `20_ch_L21`, `20_ch_L26`,
  `20_ch_L31`, `20_ch_L36`, `20_ch_L41`, `20_ch_L46`, `20_ch_L9`, `20_ch_L37`.
  Each captures active gameplay after a short settle and checks the rendered
  chapter sprite; the manifest records the requested path and resolved art.
- `10b_album_reward_p05`: checks the visible **Điểm đến tiếp theo!** header and
  chapter 2 destination while retaining the earned Hồ Gươm postcard.

The sandbox cannot run Unity. These captures are wired and their C# sources
compile; PNG capture and visual review still require the Unity QA run.
