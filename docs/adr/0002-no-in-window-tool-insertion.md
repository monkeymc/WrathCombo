# No tool is ever inserted inside a Heated Window

Commit 2a08cac76 shipped `TryGetOverheatTool`, an arithmetic guard fitting one tool GCD mid-Overheat. Log verification proved it a regression: the guard read `GCDTotal` (the recast of the *last pressed* GCD, ~1.5s once a Blazing Shot goes out) instead of the 2.5s base, passed when it should fail, and wasted 4 Overheat stacks in 6 minutes. We chose deletion over repairing the guard: at the player's 2.496s GCD a mid-window tool can never legitimately fit (insertion needs the first heated GCD ≤1.1s after Hypercharge; the weave layout gives ~1.9s), and at ~120ms ping no fractional-second arithmetic over live timers is trustworthy anyway (the Ping Rule). Tools instead go at the Head (ready-now tool fires before Hypercharge) or the Tail (Overheat ends the instant the fifth stack is spent, so the next GCD is free and still inside Wildfire).

Considered and rejected: repairing the guard via `ActionManager.GetAdjustedRecastTime` base recasts — correct arithmetic, but the feature it guards can never fire at this GCD speed, and it violates the Ping Rule.

Beware: `GCDTotal` is recast group 57's *Total* — the last GCD actually pressed, not a stable base. Any combo logic comparing it against a window while a short-recast GCD (Blazing Shot, Heat Blast) is active inherits this bug.
