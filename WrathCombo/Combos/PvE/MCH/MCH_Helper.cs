using Dalamud.Game.ClientState.JobGauge.Types;
using FFXIVClientStructs.FFXIV.Client.Game;
using System;
using System.Collections.Generic;
using WrathCombo.CustomComboNS;
using WrathCombo.CustomComboNS.Functions;
using static WrathCombo.Combos.PvE.MCH.Config;
using static WrathCombo.CustomComboNS.Functions.CustomComboFunctions;
namespace WrathCombo.Combos.PvE;

internal partial class MCH
{
    #region Queen

    // Queen potency scales linearly with Battery, so Queens belong at the
    // Wildfire burst window at as high a Battery as possible. Summon on
    // cooldown (Battery >= 90) only when Wildfire can't be used as the
    // anchor; otherwise wait for Wildfire, and only break early to dump a
    // sub-cap Queen when riding to Wildfire would overcap Battery anyway.
    private static bool ShouldUseQueenST()
    {
        if (!LevelChecked(Wildfire) || CurrentTarget is null ||
            !CanApplyStatus(CurrentTarget, Debuffs.Wildfire))
            return Battery >= 90;

        if (GetCooldownRemainingTime(Wildfire) <= GCDTotal * 2)
            return Battery >= 50;

        var forecast = BatteryForecastToWildfire();
        return Battery >= 50 && forecast >= 50 && Battery + forecast > 100;
    }

    // Forecasts Battery generated between now and Wildfire coming off
    // cooldown, so ShouldUseQueenST can tell whether holding the current
    // Battery for the Wildfire window still leaves enough being generated
    // for a second, well-timed Queen — versus needing to spend early.
    private static int BatteryForecastToWildfire()
    {
        var t = GetCooldownRemainingTime(Wildfire);

        // Air Anchor is spent before the burst (the Hypercharge tool hold
        // requires it on cooldown), so every cast fitting in t counts.
        var airAnchorCasts = 0;
        if (LevelChecked(OriginalHook(AirAnchor)))
            for (var next = GetCooldownRemainingTime(OriginalHook(AirAnchor)); next <= t; next += 40f)
                airAnchorCasts++;

        // Chain Saw (and the Excavator it grants) coming up just before
        // Wildfire is held into the burst window instead, generating no
        // Battery beforehand — only count casts landing clear of the hold.
        var chainSawCasts = 0;
        if (LevelChecked(Chainsaw))
            for (var next = GetCooldownRemainingTime(Chainsaw); next <= t - 10f; next += 60f)
                chainSawCasts++;

        var excavatorCasts = 0;
        if (LevelChecked(Excavator))
        {
            excavatorCasts = chainSawCasts;
            if (HasStatusEffect(Buffs.ExcavatorReady))
                excavatorCasts++;
        }

        var toolCasts = airAnchorCasts + chainSawCasts + excavatorCasts;
        // Heated Clean Shot lands roughly once per 12s after tool, Drill,
        // and overheat GCDs take their slots (measured from reference logs).
        var cleanShotCasts = (int)(t / 12f);

        return toolCasts * 20 + cleanShotCasts * 10;
    }

    private static bool CanQueen(
        bool onAoE = false,
        int batteryThreshold = 100,
        int hpThreshold = 0,
        bool batteryOnly = false,
        int wildfireBossOnlyOption = 1,
        int turretUsage = 100)
    {
        if (onAoE)
        {
            if (!ActionReady(OriginalHook(RookAutoturret)))
                return false;

            return batteryOnly
                ? Battery is 100
                : Battery >= batteryThreshold &&
                  GetTargetHPPercent() > hpThreshold;
        }

        if (!HasStatusEffect(Buffs.Wildfire) &&
            ActionReady(OriginalHook(RookAutoturret)) &&
            !RobotActive &&
            GetTargetHPPercent() > hpThreshold)
        {
            if (LevelChecked(Wildfire))
            {
                if (wildfireBossOnlyOption == 0 || TargetIsBoss())
                {
                    if (ShouldUseQueenST())
                        return true;
                }

                if (wildfireBossOnlyOption == 1 && !TargetIsBoss() && Battery >= turretUsage)
                    return true;
            }

            if (!LevelChecked(Wildfire) && Battery >= turretUsage)
                return true;
        }

        return false;
    }

    #endregion

    #region Hypercharge

    private static bool CanHypercharge(
        bool onAoE,
        bool useAirAnchor = true,
        float toolHoldThreshold = 8f,
        int hpThreshold = 25,
        bool skipExcavatorHold = false,
        bool skipHyperchargeHold = false,
        float wildfireHyperchargeCutoff = 9f,
        int wildfireBossOnlyOption = 1) =>
        onAoE
            ? CanHyperchargeAoE(useAirAnchor, toolHoldThreshold, hpThreshold)
            : CanHyperchargeST(hpThreshold, skipExcavatorHold, skipHyperchargeHold, wildfireHyperchargeCutoff,
                wildfireBossOnlyOption);

    private static bool IsHyperchargeReady() =>
        (ActionReady(Hypercharge) || HasStatusEffect(Buffs.Hypercharged)) && !IsOverheated;

    // One tool is always cast inside the overheat window — on the GCD after the
    // last Blazing Shot, or ahead of them outside Wildfire — so a single pending
    // tool no longer has to be spent before Hypercharge. Only a second one does.
    private static int ToolsPendingWithin(float horizon, bool skipExcavatorHold)
    {
        var pending = 0;

        if (!IsDrillCD(horizon))
            pending++;

        if (!IsAirAnchorCD(horizon))
            pending++;

        if (!IsChainSawCD(horizon))
            pending++;

        if (HasStatusEffect(Buffs.ExcavatorReady) && !skipExcavatorHold)
            pending++;

        return pending;
    }

    private static bool AreHyperchargeToolsReady(
        float toolCutoff,
        bool skipHyperchargeHold,
        bool skipExcavatorHold) =>
        skipHyperchargeHold ||
        ToolsPendingWithin(toolCutoff, skipExcavatorHold) <= 1;

    private static bool IsWildfireBound(int wildfireBossOnlyOption) =>
        LevelChecked(Wildfire) &&
        (wildfireBossOnlyOption == 0 || TargetIsBoss()) &&
        CanApplyStatus(CurrentTarget, Debuffs.Wildfire);

    // Wildfire is the burst anchor and must never drift, and every Wildfire is
    // bound to one Hypercharge. Wildfire takes the first weave slot and
    // Hypercharge the second (Barrel Stabilizer's Hypercharged buff guarantees
    // Hypercharge is usable there even below 50 Heat). This helper is the
    // fallback for the reverse order, where Hypercharge has to lead because
    // Wildfire's own weave was gated on a check Hypercharge does not share.
    private static bool IsWildfireReadyNow(int wildfireBossOnlyOption) =>
        IsWildfireBound(wildfireBossOnlyOption) && ActionReady(Wildfire);

    // ...and for an overheat window's length before that, Hypercharge waits. An
    // earlier one would still be running when Wildfire came up, spending the
    // window's Blazing Shots outside it and leaving no room for the tool. The
    // extra GCD keeps a Hypercharge fired just outside this horizon from
    // finishing fractionally late.
    private static bool IsWildfireWithinOverheat(int wildfireBossOnlyOption) =>
        IsWildfireBound(wildfireBossOnlyOption) &&
        !ActionReady(Wildfire) &&
        GetCooldownRemainingTime(Wildfire) <= OverheatDuration + GCDTotal;

    // Chain Saw and Excavator are the strongest tools and belong inside the
    // Wildfire window, so they are not spent on the GCD that precedes it. Only
    // reserve them once Hypercharge can actually follow Wildfire: otherwise
    // Wildfire never weaves, its cooldown sits at zero, and the reservation
    // would hold both tools indefinitely.
    private static bool ShouldReserveSawForWildfire(int wildfireBossOnlyOption) =>
        IsWildfireBound(wildfireBossOnlyOption) &&
        GetCooldownRemainingTime(Wildfire) <= GCDTotal &&
        IsHyperchargeReady();

    private static bool ShouldUseHyperchargeST(int wildfireBossOnlyOption) =>
        ActionReady(Wildfire) ||
        JustUsed(FullMetalField, GCDTotal / 2) ||
        wildfireBossOnlyOption == 1 && !TargetIsBoss() ||
        GetCooldownRemainingTime(Wildfire) > GCDTotal * 15 ||
        Heat is 100 && GetCooldownRemainingTime(Wildfire) > 10 ||
        !LevelChecked(Wildfire);

    private static bool CanHyperchargeST(
        int hpThreshold = 25,
        bool skipExcavatorHold = false,
        bool skipHyperchargeHold = false,
        float wildfireHyperchargeCutoff = 9f,
        int wildfireBossOnlyOption = 1)
    {
        if (GetTargetHPPercent() <= hpThreshold)
            return false;

        // Full Metal Machinist must always clear first (Full Metal Field
        // precedes Hypercharge), even when Wildfire is imminent.
        if (!IsHyperchargeReady() ||
            HasStatusEffect(Buffs.FullMetalMachinist))
            return false;

        // Wildfire has just been woven and is bound to this Hypercharge: it goes
        // out on the very next weave slot, skipping every tool and combo hold.
        if (JustUsed(Wildfire, GCDTotal + 0.9f))
            return true;

        // Wildfire is up but has not woven yet (it is gated on a boss check
        // Hypercharge does not share). Lead anyway rather than stall the burst.
        if (IsWildfireReadyNow(wildfireBossOnlyOption))
            return true;

        if (IsWildfireWithinOverheat(wildfireBossOnlyOption))
            return false;

        return (!IsComboExpiring(6) || skipHyperchargeHold) &&
               AreHyperchargeToolsReady(wildfireHyperchargeCutoff, skipHyperchargeHold, skipExcavatorHold) &&
               ShouldUseHyperchargeST(wildfireBossOnlyOption);
    }

    private static bool UsedBioBlaster(float time = 9f) =>
        !LevelChecked(BioBlaster) ||
        IsBioBlasterCD(time) ||
        HasStatusEffect(Debuffs.Bioblaster, CurrentTarget, true);

    private static bool UsedDrill(float time = 9f) =>
        !LevelChecked(Drill) || IsDrillCD(time);

    private static bool CanHyperchargeAoE(bool useAirAnchor = true, float toolHoldThreshold = 8f, int hpThreshold = 25)
    {
        if (GetTargetHPPercent() <= hpThreshold)
            return false;

        if (!IsHyperchargeReady())
            return false;

        // Pre-Bio Blaster: spend Drill. After: spend Bio Blaster (DoT counts as spent).
        if (LevelChecked(BioBlaster))
        {
            if (!UsedBioBlaster(toolHoldThreshold))
                return false;
        }
        else if (!UsedDrill(toolHoldThreshold))
            return false;

        if (!IsChainSawCD(toolHoldThreshold) || HasStatusEffect(Buffs.ExcavatorReady))
            return false;

        return !useAirAnchor || IsAirAnchorCD(toolHoldThreshold);
    }

    #endregion

    #region Overheat window

    private const float OverheatDuration = 10f;

    private static float OverheatRemaining => Gauge.OverheatTimeRemaining / 1000f;

    private static float OverheatElapsed => OverheatDuration - OverheatRemaining;

    // Blazing Shot recasts in 1.5s against the standard 2.5s GCD, both scaled
    // by the same Skill Speed.
    private static float HeatedGCD => GCDTotal * 0.6f;

    // The Overheated status carries one stack per Blazing Shot left. Assume a
    // full window when it cannot be read, so an unreadable gauge never lets a
    // tool squeeze a Blazing Shot out of the window.
    private static int RemainingBlazingShots =>
        GetStatusEffectStacks(Buffs.Overheated) is var stacks && stacks > 0 ? stacks : 5;

    // Exactly one tool GCD fits inside a Heated Window. Pressing it costs a
    // full GCD, after which the remaining Blazing Shots are one recast apart —
    // so only the last of them has to still land inside the window, not a
    // recast beyond it. At a 2.45s GCD that is 8.34s of a 10s window; a second
    // tool would need 2.45s more than the window has.
    private static bool ToolFitsInOverheat() =>
        OverheatRemaining >= GCDTotal + (RemainingBlazingShots - 1) * HeatedGCD + 0.4f;

    // Fits one tool into the overheat window, at whichever heated GCD it first
    // becomes safe to. Chain Saw leads: it hits hardest and readies Excavator.
    //
    // Never inside Wildfire, though. Overheat ends the instant its fifth stack
    // is spent, so a tool already falls on the GCD straight after the last
    // Blazing Shot — still inside Wildfire, which counts six weaponskills.
    // Moving it to the front of the window instead delays that sixth
    // weaponskill past Wildfire's expiry and forfeits 240 potency of it, buying
    // nothing: the tool is cast either way.
    private static bool TryGetOverheatTool(out uint action)
    {
        action = 0;

        if (!IsOverheated ||
            HasStatusEffect(Buffs.Wildfire) ||
            !LevelChecked(Drill) ||
            JustUsedTool(OverheatElapsed) ||
            !ToolFitsInOverheat())
            return false;

        if (ActionReady(Chainsaw) && !HasStatusEffect(Buffs.ExcavatorReady))
            action = Chainsaw;
        else if (LevelChecked(Excavator) && HasStatusEffect(Buffs.ExcavatorReady))
            action = Excavator;
        else if (ActionReady(AirAnchor))
            action = AirAnchor;
        else if (ActionReady(Drill))
            action = Drill;

        return action != 0;
    }

    #endregion

    #region Wildfire

    private static bool IsWildfireAboutToBeUsed(int wildfireHpThreshold, int wildfireBossOnlyOption) =>
        (wildfireBossOnlyOption == 0 && GetTargetHPPercent() > wildfireHpThreshold || TargetIsBoss()) &&
        CanApplyStatus(CurrentTarget, Debuffs.Wildfire) &&
        ActionReady(Wildfire);

    #endregion

    #region Misc

    private static bool CanUseFullMetalField =>
        HasStatusEffect(Buffs.FullMetalMachinist) &&
        !IsOverheated &&
        (ActionReady(Wildfire) ||
         GetCooldownRemainingTime(Wildfire) > 90 ||
         // Two GCDs early: the buff blocks Hypercharge, so Full Metal Field
         // must be done before Wildfire comes up or Wildfire drifts.
         GetCooldownRemainingTime(Wildfire) <= GCDTotal * 2 ||
         GetStatusEffectRemainingTime(Buffs.FullMetalMachinist) <= 6);

    private static bool JustUsedOverheatGCD(float window, bool onAoE) =>
        onAoE
            ? JustUsed(OriginalHook(AutoCrossbow), window) ||
              JustUsed(OriginalHook(Heatblast), window)
            : JustUsed(OriginalHook(Heatblast), window);

    private static uint OverheatGCD(bool onAoE, bool gaussRicoEnabled = true, bool alwaysAutoCrossbow = false)
    {
        if (!onAoE)
            return OriginalHook(Heatblast);

        if (alwaysAutoCrossbow ||
            !LevelChecked(CheckMate) && ActionReady(AutoCrossbow) ||
            LevelChecked(CheckMate) && LevelChecked(BlazingShot) &&
            NumberOfEnemiesInRange(AutoCrossbow, CurrentTarget) >= 5 ||
            !gaussRicoEnabled && ActionReady(AutoCrossbow))
            return AutoCrossbow;

        return OriginalHook(Heatblast);
    }

    private static bool CanBarrelStabilizer(
        bool onAoE = false,
        int hpThreshold = 0,
        int bossOnlyOption = 1,
        bool requireBoss = false) =>
        ActionReady(BarrelStabilizer) && !HasStatusEffect(Buffs.FullMetalMachinist) &&
        (onAoE
            ? GetTargetHPPercent() > hpThreshold
            : (requireBoss
                  ? TargetIsBoss()
                  : bossOnlyOption == 0 &&
                  GetTargetHPPercent() > hpThreshold || TargetIsBoss()) &&
              GetCooldownRemainingTime(Wildfire) <= 20);

    private static bool CanWildfireWeave(
        int hpThreshold = 0,
        int bossOnlyOption = 1,
        bool requireBoss = false,
        float? hyperchargeWindow = null) =>
        CanApplyStatus(CurrentTarget, Debuffs.Wildfire) &&
        ActionReady(Wildfire) &&
        // Wildfire leads its bound Hypercharge rather than following it: taking
        // the first weave slot means Wildfire can never be held hostage, and it
        // pushes Hypercharge to the second slot, where the overheat window
        // starts late enough into the GCD for a tool to still fit inside it.
        // Hypercharge merely has to be able to follow — or already have gone
        // out, which is the never-drift backstop for any path that fires it
        // early.
        (IsHyperchargeReady() && !HasStatusEffect(Buffs.FullMetalMachinist) ||
         IsOverheated ||
         JustUsed(Hypercharge, hyperchargeWindow ?? GCDTotal + 0.9f)) &&
        !HasStatusEffect(Buffs.Wildfire) &&
        (requireBoss
            ? TargetIsBoss()
            : bossOnlyOption == 0 &&
            GetTargetHPPercent() > hpThreshold || TargetIsBoss());

    #endregion

    #region Reassembled

    private static uint CurrentReassembleCharges = uint.MaxValue;
    private static bool UseBothCharges;

    private static bool TwoChargesUnlocked => GetMaxCharges(Reassemble) >= 2;

    private static bool IsWildfireActive => HasStatusEffect(Buffs.Wildfire);

    private static void UpdateReassembleChargeTracking()
    {
        uint charges = GetRemainingCharges(Reassemble);
        if (charges == CurrentReassembleCharges)
            return;

        switch (TwoChargesUnlocked)
        {
            case true:
                switch (charges)
                {
                    case 2 when CurrentReassembleCharges != 2:
                        UseBothCharges = true;
                        break;

                    case 0:

                    case 1 when CurrentReassembleCharges == 0:
                        UseBothCharges = false;
                        break;
                }
                break;

            default:
                UseBothCharges = false;
                break;
        }

        CurrentReassembleCharges = charges;
    }

    private static bool ShouldReassemble() =>
        !TwoChargesUnlocked || UseBothCharges;

    private static int ReadyTools()
    {
        int numberOfReadyTools = 0;

        if (ToolsReady(Drill))
            numberOfReadyTools += (int)GetRemainingCharges(Drill);

        if (ToolsReady(Chainsaw))
        {
            numberOfReadyTools++;
            if (LevelChecked(Excavator))
                numberOfReadyTools++;
        }

        else if (HasStatusEffect(Buffs.ExcavatorReady))
        {
            numberOfReadyTools++;
        }

        if (ToolsReady(OriginalHook(AirAnchor)))
            numberOfReadyTools++;

        if (!LevelChecked(Drill) && ComboTimer > 0 && ComboAction is SlugShot &&
            LevelChecked(CleanShot))
            numberOfReadyTools++;

        return numberOfReadyTools;
    }

    private static bool CanReassembleAoE(int chargePool = 0, int hpThreshold = 25)
    {
        uint remainingCharges = GetRemainingCharges(Reassemble);

        if (!ActionReady(Reassemble) || HasStatusEffect(Buffs.Reassembled) ||
            !HasBattleTarget() || GetTargetHPPercent() <= hpThreshold ||
            !InReassembleRange() || JustUsed(Reassemble, 2f))
            return false;

        if (remainingCharges == 0 || remainingCharges <= chargePool)
            return false;

        if (ToolsReady(Excavator) && HasStatusEffect(Buffs.ExcavatorReady))
            return true;

        if (ToolsReady(Chainsaw) && !HasStatusEffect(Buffs.ExcavatorReady))
            return true;

        if (ToolsReady(AirAnchor) &&
            (!LevelChecked(Chainsaw) || GetCooldownRemainingTime(Chainsaw) > GCDTotal * 2))
            return true;

        if (CanUseDrill(true) && ToolsReady(Drill))
            return true;

        if (LevelChecked(Scattergun) && ActionReady(Scattergun))
            return true;

        return ActionReady(OriginalHook(SpreadShot));
    }

    private static bool InReassembleRange() =>
        LevelChecked(Drill) && InActionRange(Drill) ||
        LevelChecked(AirAnchor) && InActionRange(AirAnchor) ||
        LevelChecked(Chainsaw) && InActionRange(Chainsaw) ||
        LevelChecked(Scattergun) && InActionRange(OriginalHook(SpreadShot)) ||
        !LevelChecked(Drill) && InActionRange(OriginalHook(SpreadShot));

    private static bool CanReassemble(bool onAoE, int reassembleChoice = 1, int chargePool = 0, int hpThreshold = 25) =>
        onAoE ? CanReassembleAoE(chargePool, hpThreshold) : CanReassembleST(reassembleChoice, chargePool, hpThreshold);

    private static bool CanReassembleST(int reassembleChoice = 1, int chargePool = 0, int hpThreshold = 25)
    {
        UpdateReassembleChargeTracking();

        uint remainingCharges = GetRemainingCharges(Reassemble);

        if (!ActionReady(Reassemble) || HasStatusEffect(Buffs.Reassembled) || IsWildfireActive || !HasBattleTarget() ||
            GetTargetHPPercent() <= hpThreshold ||
            !InReassembleRange() || JustUsed(Reassemble, 2f))
            return false;

        if (remainingCharges == 0 || remainingCharges <= chargePool)
            return false;

        if (reassembleChoice == 0 && !ShouldReassemble())
            return false;

        switch (reassembleChoice)
        {
            case 0:
            {
                int numberOfReadyTools = ReadyTools();
                return numberOfReadyTools >= remainingCharges;
            }

            case 1 when ToolsReady(Excavator) && HasStatusEffect(Buffs.ExcavatorReady):
            case 1 when ToolsReady(Chainsaw) && !HasStatusEffect(Buffs.ExcavatorReady):
            case 1 when ToolsReady(AirAnchor) && (!LevelChecked(Chainsaw) || GetCooldownRemainingTime(Chainsaw) > GCDTotal * 2):
            case 1 when ToolsReady(Drill) && (!LevelChecked(AirAnchor) || GetCooldownRemainingTime(AirAnchor) > GCDTotal * 2):
            case 1 when !LevelChecked(Drill) && ComboTimer > 0 && ComboAction is SlugShot && LevelChecked(CleanShot):
            case 1 when !LevelChecked(CleanShot) && ToolsReady(HotShot):
                return true;

            default:
                return false;
        }
    }

    #endregion

    #region Gauss and Rico

    private static bool IsOvercapping(uint action) =>
        ActionReady(action) &&
        (!LevelChecked(Traits.ChargedActionMastery) && GetRemainingCharges(action) is 1 ||
         LevelChecked(Traits.ChargedActionMastery) && GetRemainingCharges(action) is 2) &&
        GetCooldownChargeRemainingTime(action) < 25;

    private static bool OvercapGaussRound =>
        IsOvercapping(OriginalHook(GaussRound)) ||
        ActionReady(OriginalHook(GaussRound)) &&
        !LevelChecked(Hypercharge) &&
        GetRemainingCharges(OriginalHook(GaussRound)) is 2;

    private static bool OvercapRicochet =>
        IsOvercapping(OriginalHook(Ricochet));

    private static bool CanGaussRound =>
        ActionReady(OriginalHook(GaussRound)) &&
        GetRemainingCharges(OriginalHook(GaussRound)) >= GetRemainingCharges(OriginalHook(Ricochet));

    private static bool CanRicochet =>
        ActionReady(OriginalHook(Ricochet)) &&
        GetRemainingCharges(OriginalHook(Ricochet)) > GetRemainingCharges(OriginalHook(GaussRound));

    private static bool OvercapGaussRicochetProtection(out uint action, bool allowRicochet = true)
    {
        action = 0;

        if (OvercapGaussRound)
        {
            action = OriginalHook(GaussRound);
            return true;
        }

        if (allowRicochet && OvercapRicochet)
        {
            action = OriginalHook(Ricochet);
            return true;
        }

        return false;
    }

    private static bool GaussRicochetWeaves(out uint action, bool onAoE, bool duringHypercharge,
        bool enabled = true, int gaussOnlyOrBoth = 0, int chargePool = 0)
    {
        action = 0;

        if (!enabled)
            return false;

        if (duringHypercharge)
        {
            if (!JustUsedOverheatGCD(1f, onAoE) || HasWeaved())
                return false;
        }
        else if (!onAoE && !JustUsedTool(2f))
            return false;

        const float spacing = 2f;

        if (gaussOnlyOrBoth == 1)
        {
            if (HasCharges(GaussRound) && !LevelChecked(DoubleCheck))
            {
                action = GaussRound;
                return true;
            }

            return false;
        }

        if (GetRemainingCharges(OriginalHook(GaussRound)) > chargePool &&
            (CanGaussRound || !LevelChecked(Ricochet)) &&
            (duringHypercharge || !JustUsed(OriginalHook(GaussRound), spacing) || !LevelChecked(Ricochet)))
        {
            action = OriginalHook(GaussRound);
            return true;
        }

        if (GetRemainingCharges(OriginalHook(Ricochet)) > chargePool &&
            CanRicochet && (duringHypercharge || !JustUsed(OriginalHook(Ricochet), spacing)))
        {
            action = OriginalHook(Ricochet);
            return true;
        }

        return false;
    }

    #endregion

    #region HP Threshold

    private static int BossHpThreshold(int hpBossOption, int hpOption, bool isBoss) =>
        hpBossOption == 1 || !isBoss ? hpOption : 0;

    private static int ReassembleHPThreshold =>
        BossHpThreshold(MCH_ST_ReassembleHPBossOption, MCH_ST_ReassembleHPOption, TargetIsBoss());

    private static int HyperchargeHPThreshold =>
        BossHpThreshold(MCH_ST_HyperchargeHPBossOption, MCH_ST_HyperchargeHPOption, TargetIsBoss());

    private static int QueenHPThreshold =>
        BossHpThreshold(MCH_ST_QueenHPBossOption, MCH_ST_QueenHPOption, InBossEncounter());

    private static int ToolsHPThreshold =>
        BossHpThreshold(MCH_ST_ToolsHPBossOption, MCH_ST_ToolsHPOption, TargetIsBoss());

    private static int BarrelStabilizerHPThreshold =>
        BossHpThreshold(MCH_ST_BarrelStabilizerHPBossOption, MCH_ST_BarrelStabilizerHPOption, TargetIsBoss());

    private static int WildfireHPThreshold =>
        BossHpThreshold(MCH_ST_WildfireHPBossOption, MCH_ST_WildfireHPOption, TargetIsBoss());

    #endregion

    #region Tools

    private static bool ToolsReady(uint actionId) =>
        LevelChecked(actionId) && (HasCharges(actionId) || GetCooldownRemainingTime(actionId) <= GCDTotal);

    private static bool CanUseDrill(bool onAoE) =>
        !onAoE || !LevelChecked(BioBlaster);

    private static bool IsDrillCD(float time = 9f) =>
        !LevelChecked(Drill) ||
        !TraitLevelChecked(Traits.EnhancedMultiWeapon) && GetCooldownRemainingTime(Drill) >= time ||
        TraitLevelChecked(Traits.EnhancedMultiWeapon) && GetRemainingCharges(Drill) < GetMaxCharges(Drill) && GetCooldownChargeRemainingTime(Drill) >= time;

    private static bool IsBioBlasterCD(float time = 9f) =>
        !LevelChecked(BioBlaster) ||
        !TraitLevelChecked(Traits.EnhancedMultiWeapon) && GetCooldownRemainingTime(BioBlaster) >= time ||
        TraitLevelChecked(Traits.EnhancedMultiWeapon) && GetRemainingCharges(BioBlaster) < GetMaxCharges(BioBlaster) && GetCooldownChargeRemainingTime(BioBlaster) >= time;

    private static bool IsAirAnchorCD(float time = 9f) =>
        !LevelChecked(OriginalHook(HotShot)) ||
        GetCooldownRemainingTime(OriginalHook(HotShot)) >= time;

    private static bool IsChainSawCD(float time = 9f) =>
        !LevelChecked(Chainsaw) ||
        GetCooldownRemainingTime(Chainsaw) >= time;

    private static bool JustUsedTool(float window) =>
        JustUsed(OriginalHook(AirAnchor), window) ||
        JustUsed(Chainsaw, window) ||
        JustUsed(Drill, window) ||
        JustUsed(Excavator, window);

    private static bool CanUseTools(ref uint actionID, bool onAoE, bool useAirAnchor = true, bool holdExcavatorForWildfire = false,
        bool holdSawForWildfire = false)
    {
        if (ToolsReady(Chainsaw) && !HasStatusEffect(Buffs.ExcavatorReady) && !holdSawForWildfire)
        {
            actionID = Chainsaw;
            return true;
        }

        if (ToolsReady(Excavator) && HasStatusEffect(Buffs.ExcavatorReady) && !holdSawForWildfire &&
            (onAoE || !holdExcavatorForWildfire ||
             GetStatusEffectRemainingTime(Buffs.ExcavatorReady) <= GCDTotal * 3))
        {
            actionID = Excavator;
            return true;
        }

        if ((!onAoE || useAirAnchor) && ToolsReady(AirAnchor))
        {
            actionID = AirAnchor;
            return true;
        }

        if (onAoE &&
            ToolsReady(BioBlaster) &&
            !HasStatusEffect(Debuffs.Bioblaster, CurrentTarget) &&
            CanApplyStatus(CurrentTarget, Debuffs.Bioblaster))
        {
            actionID = BioBlaster;
            return true;
        }

        if (CanUseDrill(onAoE) && ToolsReady(Drill))
        {
            actionID = Drill;
            return true;
        }

        if (onAoE &&
            HasStatusEffect(Buffs.Reassembled) &&
            ToolsReady(OriginalHook(SpreadShot)))
        {
            actionID = OriginalHook(SpreadShot);
            return true;
        }

        if (!onAoE && !LevelChecked(AirAnchor) && ToolsReady(HotShot) &&
            (!LevelChecked(CleanShot) || !HasStatusEffect(Buffs.Reassembled) && ActionReady(HotShot)))
        {
            actionID = HotShot;
            return true;
        }

        return false;
    }

    #endregion

    #region Combos

    private static unsafe bool IsComboExpiring(float times)
    {
        float gcd = GCDTotal * times;

        return ActionManager.Instance()->Combo.Timer != 0 && ActionManager.Instance()->Combo.Timer < gcd;
    }

    private static uint DoBasicCombo(uint actionId, bool allowReassembleOnClean = false, int reassembleChoice = 1, int chargePool = 0, int hpThreshold = 25)
    {
        if (ComboTimer > 0)
        {
            if (ComboAction is SplitShot && ActionReady(OriginalHook(SlugShot)))
                return OriginalHook(SlugShot);

            if (ComboAction is SlugShot && ActionReady(OriginalHook(CleanShot)))
            {
                if (allowReassembleOnClean && CanReassemble(false, reassembleChoice, chargePool, hpThreshold))
                    return Reassemble;

                return OriginalHook(CleanShot);
            }
        }

        return OriginalHook(SplitShot);
    }

    #endregion

    #region Openers

    internal static WrathOpener Opener()
    {
        if (Lvl100StandardOpener.LevelChecked &&
            MCH_SelectedOpener == 0)
            return Lvl100StandardOpener;

        if (Lvl100EarlyWFOpener.LevelChecked &&
            MCH_SelectedOpener == 1)
            return Lvl100EarlyWFOpener;

        if (Lvl90EarlyTools.LevelChecked)
            return Lvl90EarlyTools;

        return WrathOpener.Dummy;
    }

    internal static MCHLvl90EarlyToolsOpener Lvl90EarlyTools = new();
    internal static MCHLvl100EarlyWFOpener Lvl100EarlyWFOpener = new();
    internal static MCHLvl100StandardOpener Lvl100StandardOpener = new();

    internal class MCHLvl100StandardOpener : WrathOpener
    {
        public override int MinOpenerLevel => 100;

        public override int MaxOpenerLevel => 109;

        public override List<uint> OpenerActions { get; set; } =
        [
            Reassemble,
            AirAnchor,
            CheckMate,
            DoubleCheck,
            Drill,
            BarrelStabilizer,
            Chainsaw,
            Excavator,
            AutomatonQueen,
            Reassemble,
            Drill,
            CheckMate,
            Wildfire,
            FullMetalField,
            Hypercharge,
            DoubleCheck,
            BlazingShot,
            CheckMate,
            BlazingShot,
            DoubleCheck,
            BlazingShot,
            CheckMate,
            BlazingShot,
            DoubleCheck,
            BlazingShot,
            CheckMate,
            Drill,
            DoubleCheck,
            CheckMate,
            HeatedSplitShot,
            DoubleCheck,
            HeatedSlugShot,
            HeatedCleanShot
        ];

        internal override UserData ContentCheckConfig => MCH_Balance_Content;

        public override List<(int[] Steps, Func<int> HoldDelay)> PrepullDelays { get; set; } =
        [
            ([2], () => 4)
        ];

        public override Preset Preset => Preset.MCH_ST_Adv_Opener;

        public override bool HasCooldowns() =>
            GetRemainingCharges(Reassemble) is 2 &&
            GetRemainingCharges(OriginalHook(GaussRound)) is 3 &&
            GetRemainingCharges(OriginalHook(Ricochet)) is 3 &&
            IsOffCooldown(Chainsaw) &&
            IsOffCooldown(Wildfire) &&
            IsOffCooldown(BarrelStabilizer) &&
            IsOffCooldown(Excavator) &&
            IsOffCooldown(FullMetalField);
    }

    internal class MCHLvl100EarlyWFOpener : WrathOpener
    {
        public override int MinOpenerLevel => 100;

        public override int MaxOpenerLevel => 109;

        public override List<uint> OpenerActions { get; set; } =
        [
            Reassemble,
            AirAnchor,
            CheckMate,
            DoubleCheck,
            Drill,
            BarrelStabilizer,
            Reassemble,
            Chainsaw,
            DoubleCheck,
            Wildfire,
            Excavator,
            Hypercharge,
            AutomatonQueen,
            BlazingShot,
            CheckMate,
            BlazingShot,
            DoubleCheck,
            BlazingShot,
            CheckMate,
            BlazingShot,
            DoubleCheck,
            BlazingShot,
            CheckMate,
            Drill,
            DoubleCheck,
            CheckMate,
            FullMetalField,
            DoubleCheck,
            CheckMate,
            Drill,
            HeatedSplitShot,
            HeatedSlugShot,
            HeatedCleanShot
        ];

        internal override UserData ContentCheckConfig => MCH_Balance_Content;

        public override List<(int[] Steps, Func<int> HoldDelay)> PrepullDelays { get; set; } =
        [
            ([2], () => 4)
        ];
        public override Preset Preset => Preset.MCH_ST_Adv_Opener;
        public override bool HasCooldowns() =>
            GetRemainingCharges(Reassemble) is 2 &&
            GetRemainingCharges(OriginalHook(GaussRound)) is 3 &&
            GetRemainingCharges(OriginalHook(Ricochet)) is 3 &&
            IsOffCooldown(Chainsaw) &&
            IsOffCooldown(Wildfire) &&
            IsOffCooldown(BarrelStabilizer) &&
            IsOffCooldown(Excavator) &&
            IsOffCooldown(FullMetalField);
    }

    internal class MCHLvl90EarlyToolsOpener : WrathOpener
    {
        public override int MinOpenerLevel => 90;

        public override int MaxOpenerLevel => 90;

        public override List<uint> OpenerActions { get; set; } =
        [
            Reassemble,
            AirAnchor,
            GaussRound,
            Ricochet,
            Drill,
            BarrelStabilizer,
            Chainsaw,
            GaussRound,
            Ricochet,
            HeatedSplitShot,
            GaussRound,
            Ricochet,
            HeatedSlugShot,
            Wildfire,
            HeatedCleanShot,
            AutomatonQueen,
            Hypercharge,
            BlazingShot,
            Ricochet,
            BlazingShot,
            GaussRound,
            BlazingShot,
            Ricochet,
            BlazingShot,
            GaussRound,
            BlazingShot,
            Reassemble,
            Drill
        ];

        internal override UserData ContentCheckConfig => MCH_Balance_Content;

        public override List<(int[] Steps, Func<int> HoldDelay)> PrepullDelays { get; set; } =
        [
            ([2], () => 4)
        ];

        public override List<int> DelayedWeaveSteps { get; set; } =
        [
            14
        ];
        public override Preset Preset => Preset.MCH_ST_Adv_Opener;
        public override bool HasCooldowns() =>
            GetRemainingCharges(Reassemble) is 2 &&
            GetRemainingCharges(OriginalHook(GaussRound)) is 3 &&
            GetRemainingCharges(OriginalHook(Ricochet)) is 3 &&
            IsOffCooldown(Chainsaw) &&
            IsOffCooldown(Wildfire) &&
            IsOffCooldown(BarrelStabilizer);
    }

    #endregion

    #region Gauge

    private static MCHGauge Gauge => GetJobGauge<MCHGauge>();

    private static bool IsOverheated => Gauge.IsOverheated;

    private static bool RobotActive => Gauge.IsRobotActive;

    private static byte Heat => Gauge.Heat;

    private static byte Battery => Gauge.Battery;

    #endregion

    #region ID's

    public const uint
        CleanShot = 2873,
        HeatedCleanShot = 7413,
        SplitShot = 2866,
        HeatedSplitShot = 7411,
        SlugShot = 2868,
        HeatedSlugShot = 7412,
        GaussRound = 2874,
        Ricochet = 2890,
        Reassemble = 2876,
        Drill = 16498,
        HotShot = 2872,
        AirAnchor = 16500,
        Hypercharge = 17209,
        Heatblast = 7410,
        SpreadShot = 2870,
        Scattergun = 25786,
        AutoCrossbow = 16497,
        RookAutoturret = 2864,
        RookOverdrive = 7415,
        AutomatonQueen = 16501,
        QueenOverdrive = 16502,
        Tactician = 16889,
        Chainsaw = 25788,
        BioBlaster = 16499,
        BarrelStabilizer = 7414,
        Wildfire = 2878,
        Dismantle = 2887,
        Flamethrower = 7418,
        BlazingShot = 36978,
        DoubleCheck = 36979,
        CheckMate = 36980,
        Excavator = 36981,
        FullMetalField = 36982;

    public static class Buffs
    {
        public const ushort
            Reassembled = 851,
            Tactician = 1951,
            Wildfire = 1946,
            Overheated = 2688,
            Flamethrower = 1205,
            Hypercharged = 3864,
            ExcavatorReady = 3865,
            FullMetalMachinist = 3866;
    }

    public static class Debuffs
    {
        public const ushort
            Dismantled = 860,
            Wildfire = 861,
            Bioblaster = 1866;
    }

    public static class Traits
    {
        public const ushort
            EnhancedMultiWeapon = 605,
            ChargedActionMastery = 292;
    }

    #endregion
}
