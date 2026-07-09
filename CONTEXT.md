# Context — MCH ST Advanced (fixed rotation)

Glossary of domain terms as used in this project's MCH rotation work. Terms, not implementation.

## Glossary

**Heated Window (Overheat)** — The state entered on using Hypercharge, carrying 5 Overheat stacks and a 10-second timer. Each Blazing Shot consumes one stack. It ends on **whichever comes first**: the timer expiring, or the fifth stack being spent. All 5 Blazing Shots must therefore be cast within the 10s.

**Tool** — One of the big-potency charged GCDs: Drill, Air Anchor, Chain Saw, Excavator. Tools do not consume Overheat stacks but do consume time inside a Heated Window.

**Tool Fit** — Inserting exactly one Tool GCD inside a Heated Window while still landing all 5 Blazing Shots before Overheat expires. One tool fits (with ~0.5–1s margin at ~2.45s GCD); two never do.

**Second Form** — Excavator, unlocked by the ExcavatorReady buff that Chain Saw grants. This is why Chain Saw is the preferred Tool Fit: it hits hard *and* readies a second tool.

**Burst Anchor (Wildfire)** — The 120s cooldown that the whole burst aligns to. It counts the next 6 GCDs within 10s of application; any GCD counts equally (Blazing Shot, Tool, Full Metal Field). Must never drift.

**Wildfire Drift** — Wildfire being applied later than the moment it comes off cooldown. Compounds every 2-minute cycle and desyncs the burst from party buffs.

**Tool Hold** — Refusing to Hypercharge until all Tools are on cooldown beyond a threshold, so no Tool comes up (and drifts) behind the Heated Window.

**Imminent-Wildfire Bypass** — Firing Hypercharge early (Wildfire within one GCD of ready) and skipping Tool Holds, on the theory that Wildfire weaves immediately after. Observed failure mode: Hypercharge goes out before Wildfire is castable and Wildfire drifts behind the Heated Window. Superseded by the Wildfire–Hypercharge Bind.

## Rules (agreed)

**Wildfire never drifts** — absolute rule; everything else yields to it.

**Wildfire–Hypercharge Bind** — Every Wildfire window contains exactly one Hypercharge, double-woven under the same GCD. Wildfire takes the first weave slot and Hypercharge the second, so Wildfire is never held hostage by Hypercharge's readiness.

**Single Tool Insertion** — At most one GCD may be inserted into a Heated Window, and it must be a Tool. Full Metal Field and combo GCDs are never inserted.

**Tail Placement** — Inside a Wildfire window the Tool goes *after* the fifth Blazing Shot, not before it. Overheat ends the instant its fifth stack is spent, so the Tool falls on the very next GCD and is still inside Wildfire. Putting it at the head of the window instead delays the sixth weaponskill past Wildfire's expiry, forfeiting 240 potency of Wildfire and buying nothing. Outside a Wildfire window the Tool goes at the head, which reduces tool drift.

**Hypercharge Cost** — Hypercharge is castable at 50+ Heat, or freely via the Hypercharged status from Barrel Stabilizer (which is what guarantees the Bind at the 2-minute burst).

**Tool Fit Priority** — Chain Saw > Excavator > Air Anchor > Drill. Chain Saw first (Second Form + battery), Excavator next (its readiness is an expiring buff), Air Anchor next (40s cooldown that hates drift), Drill last (its two charges absorb drift harmlessly). A tool due to come ready inside the Heated Window no longer blocks Hypercharge — the window eats it.

**Opportunistic Fit** — The Tool Fit may happen at any heated GCD slot, not just the first: whenever an eligible tool is ready and the remaining Overheat time still fits all remaining Blazing Shots plus one tool GCD (with snapshot margin). At most one insertion per window.

**No Reassemble In-Window** — Reassemble never weaves during Overheat; the inserted tool goes out unassembled. Heated weave slots are reserved for Wildfire and heat oGCDs.
