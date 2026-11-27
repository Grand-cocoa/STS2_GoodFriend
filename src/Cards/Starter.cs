using HarmonyLib;
using MegaCrit.Sts2.Core.Runs;

namespace GoodFriend;

public static class MultiplayerPool
{
    internal static List<CardModel> For(Player player, bool generating)
    {
        // Unlocks and the run's actual player count still apply to character-specific co-op cards.
        return ModelDb.AllCardPools.SelectMany(pool => pool.GetUnlockedCards(player.UnlockState, player.RunState.CardMultiplayerConstraint))
            .Where(c => c.MultiplayerConstraint == CardMultiplayerConstraint.MultiplayerOnly
                && c.Rarity is CardRarity.Common or CardRarity.Uncommon or CardRarity.Rare
                && (!generating || c.CanBeGeneratedInCombat))
            .DistinctBy(c => c.Id).OrderBy(c => c.Id.Entry, StringComparer.Ordinal).ToList();
    }
}

public sealed class GfCrowd() : FriendCard(0, CardType.Skill, CardRarity.Basic, TargetType.Self)
{
    private bool _transformed;
    public override bool CanBeGeneratedInCombat => false;
    public override bool CanBeGeneratedByModifiers => false;
    protected override CardLocation GetResultLocationForCardPlay() => new(Owner, PileType.None, CardPilePosition.Bottom);
    public override async Task BeforeCombatStartLate()
    {
        if (Pile?.Type == PileType.Draw && DeckVersion != null && CombatState!.Players.Count > 1)
            await CardCmd.AutoPlay(new BlockingPlayerChoiceContext(), this, Owner.Creature);
    }
    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        if (_transformed) return;
        _transformed = true;
        var pool = MultiplayerPool.For(Owner, true);
        var result = CombatState!.CreateCard(pool[Owner.RunState.Rng.CombatCardGeneration.NextInt(pool.Count)], Owner);
        if (IsUpgraded) CardCmd.Upgrade(result);
        CardCmd.ApplyKeyword(result, CardKeyword.Retain);
        await CardPileCmd.AddGeneratedCardToCombat(result, PileType.Hand, Owner);
    }
}

public sealed class StarterGuard : AbstractModel
{
    public override bool ShouldReceiveCombatHooks => false;
    public override bool ShouldAddToDeck(CardModel card) => card is not GfCrowd;
}

[HarmonyPatch(typeof(RunState), nameof(RunState.CreateForNewRun))]
internal static class StartingDeckPatch
{
    private static void Postfix(RunState __result)
    {
        if (__result.Players.Count < 2) return;
        foreach (var player in __result.Players)
        {
            if (player.Deck.Cards.Any(c => c is GfCrowd)) continue;
            var starter = __result.CreateCard<GfCrowd>(player);
            starter.FloorAddedToDeck = 1;
            player.Deck.AddInternal(starter, silent: true);
        }
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
