# Context — MCH ST Advanced (fixed rotation)

Glossary of domain terms as used in this project's MCH rotation work. Terms, not implementation.

## Glossary

**Heated Window (Overheat)** — The state entered on using Hypercharge, carrying 5 Overheat stacks and a 10-second timer. Each Blazing Shot consumes one stack. It ends on **whichever comes first**: the timer expiring, or the fifth stack being spent. All 5 Blazing Shots must therefore be cast within the 10s.

**Tool** — One of the big-potency charged GCDs: Drill, Air Anchor, Chain Saw, Excavator. Tools do not consume Overheat stacks but do consume time inside a Heated Window.

**Head** — The GCD slot immediately *before* Hypercharge is woven. A Tool placed at the Head goes out first and delays the Heated Window by one GCD.

**Tail** — The GCD slot immediately *after* the Heated Window ends. Because Overheat ends the instant its fifth stack is spent, the Tail arrives one heated recast after the last Blazing Shot — early enough to still sit inside a Wildfire window.

**Second Form** — Excavator, unlocked by the ExcavatorReady buff that Chain Saw grants.

**Burst Anchor (Wildfire)** — The 120s cooldown that the whole burst aligns to. It counts the next 6 GCDs within 10s of application; any GCD counts equally (Blazing Shot, Tool, Full Metal Field). Must never drift.

**Wildfire Drift** — Wildfire being applied later than the moment it comes off cooldown. Compounds every 2-minute cycle and desyncs the burst from party buffs.

**Tool Hold** — Refusing to Hypercharge while a Tool is ready *right now*, so that Tool goes out at the Head instead of drifting behind the Heated Window. Tools merely *due* during the window do not hold Hypercharge — the Tail catches them.

**Imminent-Wildfire Bypass** — Firing Hypercharge early (Wildfire within one GCD of ready) and skipping Tool Holds, on the theory that Wildfire weaves immediately after. Observed failure mode: Hypercharge goes out before Wildfire is castable and Wildfire drifts behind the Heated Window. Superseded by the Wildfire–Hypercharge Bind.

## Rules (agreed)

**Wildfire never drifts** — Wildfire is pressed the moment it is ready; nothing gates it, and it will clip a GCD rather than wait. One exception, taken knowingly: the Wildfire–Hypercharge Bind costs one animation lock (~0.65s) of drift per cycle, because Hypercharge takes the weave slot Wildfire becomes ready in. See `docs/adr/0001` for the measurements and why that trade was accepted.

**No Timing Arithmetic (Ping Rule)** — The player runs at ~120ms ping; every event lands late by a variable amount. No rule may depend on fractional-second arithmetic over live timers (fitting X seconds of casts into Y seconds of window). Rules must be positional and discrete: counts, slots, ready-or-not.

**Wildfire–Hypercharge Bind** — Every Wildfire window contains exactly one Hypercharge, double-woven under the same GCD. **Hypercharge takes the first weave slot and Wildfire the second**: Wildfire's 10-second, 6-weaponskill count then starts later, giving the sixth weaponskill (the Tail Tool) ping-safe margin. Two fallbacks protect the never-drift rule:
1. *Wildfire fires alone* — if Hypercharge cannot fire when Wildfire is ready, Wildfire goes out on the first available weave slot by itself; Hypercharge follows as soon as it can.
2. *Wildfire clips* — if the weave window was missed entirely and the GCD is already ready, Wildfire fires anyway, clipping the GCD. Wildfire ready → Wildfire wins.

**Hypercharge Fuel Guarantee** — The rotation must *arrive* at Wildfire with Hypercharge usable: no filler Hypercharge may spend the Heat (or displace the Hypercharged buff from Barrel Stabilizer) that the Wildfire-bound Hypercharge needs. The fire-alone fallback is a safety net, not a plan.

**No In-Window Insertion** — No GCD is ever inserted into a Heated Window: five Blazing Shots and nothing else. (An in-window Tool cannot fit at this GCD speed, and per the Ping Rule no arithmetic guard may claim otherwise.)

**Head–Tail Placement** — A Tool ready at the moment Hypercharge would fire goes out first, at the Head. Tools that come ready during the Heated Window land at the Tail, which costs nothing. Inside a Wildfire window only the Tail is used — the Tail Tool is the sixth weaponskill; nothing delays the Wildfire-bound Hypercharge.

**Hypercharge Cost** — Hypercharge is castable at 50+ Heat, or freely via the Hypercharged status from Barrel Stabilizer (which is what guarantees the Bind at the 2-minute burst).

**No Reassemble In-Window** — Reassemble never weaves during Overheat; a Tail Tool goes out unassembled if its Reassemble would have to weave heated. Heated weave slots are reserved for Wildfire and heat oGCDs.
