using System;
using System.Linq;

namespace BepJam
{
    /// <summary>Shared by the screenshot driver and the standalone real-Kitchen audit.</summary>
    public sealed class AutoPlayPolicy
    {
        public const float TapInterval = 0.55f;
        readonly Kitchen kitchen;
        readonly float deadline, stallSeconds;
        string progress;
        float lastProgress;

        public AutoPlayPolicy(Kitchen kitchen, float now)
        {
            this.kitchen = kitchen;
            deadline = now + LevelGen.SimulationWatchdogSeconds(kitchen.level);
            stallSeconds = Math.Max(12f, kitchen.level.cookTime + kitchen.level.cutTime + 5f);
            lastProgress = now;
        }

        public bool Step()
        {
            if (kitchen.ended || kitchen.paused || !kitchen.started) return false;
            int id = kitchen.Hint(false);
            if (id >= 0) return kitchen.Tap(id);
            return kitchen.returnsLeft > 0 && kitchen.slots.All(s => s.state == SlotState.Ingredient) && kitchen.ReturnPlate();
        }

        // now is real time in the UI, simulated wall time in the harness. Pauses cannot hide a stall.
        public string Failure(float now)
        {
            if (kitchen.ended) return kitchen.won ? null : "lost (" + kitchen.loseKind + "): " + kitchen.loseReason;
            string current = kitchen.moves + "/" + kitchen.merges + "/" + kitchen.served + "/" +
                string.Join(",", kitchen.customers.Select(c => c.arrived + ":" + c.orders.Count(o => o.done))) + "/" +
                string.Join(",", kitchen.slots.Select(s => s.state + ":" + s.key)) + "/" +
                string.Join(",", kitchen.stations.Values.Select(s => s.state + ":" + s.dish));
            if (current != progress) { progress = current; lastProgress = now; }
            if (now >= deadline) return "playback watchdog exceeded";
            if (now - lastProgress >= stallSeconds) return "stalled: no action, arrival, delivery or station/plate transition for " + stallSeconds.ToString("F1") + "s";
            return null;
        }

        public static string Describe(Kitchen k)
        {
            if (k == null) return "no active Kitchen";
            return "L" + k.level.number + " time=" + k.time.ToString("F2") + " started=" + k.started + " paused=" + k.paused +
                " ended=" + k.ended + " won=" + k.won + " loss=" + k.loseKind + ":" + k.loseReason +
                " served=" + k.served + "/" + k.TotalServedTarget + " moves=" + k.moves + " returns=" + k.returnsLeft +
                " hint=" + k.Hint(false) + " pantry=" + k.columns.Sum(c => c.visible.Sum(cr => cr.count)) + "+" + k.columns.Sum(c => c.hidden.Sum(cr => cr.count)) +
                " plates=[" + string.Join(",", k.slots.Select(s => s.index + ":" + s.state + ":" + s.key + (s.uncut ? "(uncut)" : ""))) +
                "] stations=[" + string.Join(",", k.stations.Values.Select(s => s.type + ":" + s.state + ":" + s.dish + ":" + s.timer.ToString("F2"))) +
                "] seats=[" + string.Join(",", k.seats.Select(c => c == null ? "-" : c.id + (c.vip ? " VIP " + c.patience.ToString("F1") + "s" : "") +
                    ":" + string.Join("/", c.orders.Select(o => o.dish + (o.done ? "(done)" : o.reserved ? "(reserved)" : "(open)"))))) + "]";
        }
    }
}
