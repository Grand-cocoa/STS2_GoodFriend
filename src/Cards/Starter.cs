using HarmonyLib;

namespace GoodFriend;

public static class MultiplayerPool
{
    internal static IEnumerable<CardModel> Unlocked(Player player, IEnumerable<CardPoolModel> pools) =>
        pools.SelectMany(pool => pool.GetUnlockedCards(player.UnlockState, player.RunState.CardMultiplayerConstraint))
            .Where(card => card.MultiplayerConstraint == CardMultiplayerConstraint.MultiplayerOnly && card is not GfCrowd)
            .DistinctBy(card => card.Id)
            .OrderBy(card => card.Id.Entry, StringComparer.Ordinal);
}

public sealed class GfCrowd() : FriendCard(0, CardType.Skill, CardRarity.Basic, TargetType.Self)
{
    public override bool CanBeGeneratedInCombat => false;
    public override bool CanBeGeneratedByModifiers => false;
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Innate];

    public override async Task AfterCardDrawn(PlayerChoiceContext ctx, CardModel card, bool fromHandDraw)
    {
        if (card != this) return;
        await CardPileCmd.Draw(ctx, Owner);
        if (!IsInCombat || CombatState is not { } combat) return;

        var options = MultiplayerPool.Unlocked(Owner, [Owner.Character.CardPool, Pool])
            .Where(candidate => candidate.CanBeGeneratedInCombat && candidate.IsAllowedInRun(Owner.RunState));
        var chosen = Owner.RunState.Rng.CombatCardGeneration.NextItem(options);
        if (chosen == null) return;

        var replacement = combat.CreateCard(chosen, Owner);
        if (IsUpgraded) CardCmd.Upgrade(replacement);
        CardCmd.ApplyKeyword(replacement, CardKeyword.Retain);
        await CardCmd.Transform(this, replacement);
    }
}

[HarmonyPatch(typeof(CardModel), nameof(CardModel.IsValidTarget))]
internal static class FriendTargetPatch
{
    private static void Postfix(CardModel __instance, Creature? target, ref bool __result)
    {
        if (__instance is not FriendCard || target == null) return;
        if (__instance is GfInsurance)
        {
            __result = target.Player != null && target.IsAlive && target.CombatState == __instance.CombatState;
            return;
        }
        if (__instance.TargetType == TargetType.AnyAlly)
        {
            bool anotherPlayer = target.Player != null && target.Player != __instance.Owner && target.CombatState == __instance.CombatState;
            if (__instance is GfQuiz) __result = anotherPlayer;
            else __result &= anotherPlayer;
        }
        if (__result && __instance is GfBorrow) __result = target.Player?.PlayerCombatState?.Energy >= 1;
    }
}
