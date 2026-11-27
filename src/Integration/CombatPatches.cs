using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using MegaCrit.Sts2.Core.Runs;

namespace GoodFriend;

[HarmonyPatch(typeof(CombatManager), nameof(CombatManager.SetReadyToEndTurn))]
internal static class EndEnergySnapshot
{
    private static void Prefix(CombatManager __instance, Player player)
    {
        if (player.Creature.CombatState is { } state && state.Players.Count > 1 && !__instance.IsPlayerReadyToEndTurn(player))
            FriendCombat.For(state).FinalEnergy[player] = player.PlayerCombatState?.Energy ?? 0;
    }
}

[HarmonyPatch(typeof(CombatManager), nameof(CombatManager.TryUndoReadyToEndTurn))]
internal static class CancelEndEnergySnapshot
{
    private static void Postfix(CombatManager __instance, Player player)
    {
        if (!__instance.IsPlayerReadyToEndTurn(player) && player.Creature.CombatState is { } state && state.Players.Count > 1)
            FriendCombat.For(state).FinalEnergy.Remove(player);
    }
}

[HarmonyPatch(typeof(CombatManager), nameof(CombatManager.RemoveDeadPlayerCardsFromCombat))]
internal static class KeepInsuredDeck
{
    private static bool Prefix(Player player, ref Task __result)
    {
        if (player.Creature.CombatState is not { } state || state.Players.Count < 2 || !FriendCombat.For(state).HasClaim(player))
            return true;
        __result = Task.CompletedTask;
        return false;
    }
}

[HarmonyPatch(typeof(CreatureCmd), nameof(CreatureCmd.Kill), typeof(IReadOnlyCollection<Creature>), typeof(CardModel), typeof(bool))]
[HarmonyPatch(MethodType.Async)]
internal static class InsuranceWipeCheck
{
    // Only change the loss predicate in the death command. Ordinary deaths and death-prevention relics still resolve.
    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> codes)
    {
        foreach (var code in codes)
        {
            if (code.operand is MethodInfo method && method.DeclaringType == typeof(Enumerable)
                && method.Name == nameof(Enumerable.All) && method.IsGenericMethod && method.GetGenericArguments()[0] == typeof(Player))
            {
                code.opcode = OpCodes.Call;
                code.operand = AccessTools.Method(typeof(InsuranceWipeCheck), nameof(AllForLoss));
            }
            yield return code;
        }
    }

    private static bool AllForLoss(IEnumerable<Player> players, Func<Player, bool> test)
    {
        if (!players.All(test)) return false;
        return !Covered(players.FirstOrDefault()?.Creature.CombatState);
    }

    internal static bool Covered(ICombatState? state)
        => state != null && state.Players.Count > 1 && CombatManager.Instance.IsInProgress && FriendCombat.For(state).RescuingWipe;
}

[HarmonyPatch(typeof(RunState), nameof(RunState.IsGameOver), MethodType.Getter)]
internal static class PendingClaimRunResult
{
    private static void Postfix(RunState __instance, ref bool __result)
    {
        if (__result && InsuranceWipeCheck.Covered(__instance.Players.FirstOrDefault()?.Creature.CombatState)) __result = false;
    }
}

[HarmonyPatch(typeof(MonsterModel), nameof(MonsterModel.PerformMove))]
internal static class SkipMovesOnCoveredWipe
{
    private static bool Prefix(MonsterModel __instance, ref Task __result)
    {
        if (!InsuranceWipeCheck.Covered(__instance.Creature.CombatState)) return true;
        // Allow the normal synchronized turn transition to run once; do not start a second combat loop.
        __result = Task.CompletedTask;
        return false;
    }
}

[HarmonyPatch]
internal static class FriendPowerIcons
{
    private static IEnumerable<MethodBase> TargetMethods()
        => new[] { nameof(PowerModel.IconPath), nameof(PowerModel.PackedIconPath), nameof(PowerModel.ResolvedBigIconPath) }
            .Select(n => AccessTools.PropertyGetter(typeof(PowerModel), n));

    private static bool Prefix(PowerModel __instance, ref string __result)
    {
        if (__instance is not FriendPower p) return true;
        __result = $"res://GoodFriend/icons/{p.Illustration}.png";
        return false;
    }
}
