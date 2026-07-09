# Hypercharge takes the first weave slot; Wildfire the second

**Status:** accepted — with a known, deliberately accepted cost. Read the Consequences before changing anything.

The original bind (commit 2a08cac76) put Wildfire first so it could never be held hostage by Hypercharge's readiness. We reversed it: the player runs at ~120ms ping, and Wildfire in the second weave slot starts its 10s/6-weaponskill count ~0.7s later into the GCD, widening the margin on the sixth weaponskill (the tail tool).

The hostage risk that motivated Wildfire-first is covered instead by decoupling: `CanWildfireWeave` no longer references Hypercharge at all. Order is enforced purely by checking Hypercharge first at the call site, so if Hypercharge cannot fire, Wildfire simply takes the slot alone and Hypercharge chases it via its own `JustUsed(Wildfire)` clause. Two fallbacks back this up (Wildfire fires alone; Wildfire clips the GCD if the weave slot was missed) plus the Hypercharge Fuel Guarantee — see CONTEXT.md.

## Consequences (measured, not modelled)

Verified against back-to-back FFLogs exports of the same striking-dummy rotation, ~6.5 minutes each, at a 2.496s GCD.

**Gained** — sixth-weaponskill margin inside Wildfire, `1.21–1.79s → 2.41–2.50s`. Wildfire now applies at `+2.36/+2.51/+2.40s` into the Full Metal Field GCD that carries the double-weave (that GCD measures 2.46–2.54s), versus `+1.75/+1.17/+1.18s` before.

**Paid** — Wildfire drifts `+0.65s every cycle`, systematically:

| | drift per cycle | over 3 cycles |
|---|---|---|
| Wildfire-first | +0.066, +0.036, +1.090 (one missed weave slot) | +1.19s |
| Hypercharge-first | +0.633, +0.694, +0.642 | +1.97s |

This is **structural, not a tuning bug**, and it compounds linearly because Wildfire's cooldown starts on cast. `IsWildfireWithinOverheat` blocks Hypercharge until Wildfire's cooldown is under ~12.5s, so Hypercharge's earliest legal instant *is* the instant Wildfire becomes ready; Hypercharge takes that weave slot and Wildfire waits one animation lock. You cannot press Wildfire the instant it is ready and also have Hypercharge precede it under the same GCD — one animation lock must land either inside Wildfire's window or inside Wildfire's cooldown.

This is in tension with the "Wildfire never drifts" rule in CONTEXT.md, which the second-weave rule outranks in practice. The owner reviewed the numbers above and chose to keep Hypercharge-first anyway. Expect roughly `+3.3s` of drift by minute 12 and `+5.9s` by minute 20; on long fights with tight party-buff alignment, reconsider.

## Considered and rejected

**Wildfire-first.** Would hold drift near `+0.05s/cycle` and still delivered 6 weaponskills in 4 of 4 windows, with the worst margin (1.21s) at ~10× the player's ping — i.e. the margin the flip buys was not in danger. Rejected in favour of the wider margin.

Do not "fix" the order back to Wildfire-first without raising it first: it was tried, measured, and deliberately kept reversed.
