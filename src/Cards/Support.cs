using MegaCrit.Sts2.Core.Models.Cards;

namespace GoodFriend;

public sealed class GfWorkCover() : FriendCard(1, CardType.Skill, CardRarity.Uncommon, TargetType.AnyAlly)
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];
    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        Player insured = Ally(play);
        await Hit(ctx, Owner.Creature, 1, true);
        if (insured.Creature.IsAlive && !Rules.PaidWorkers.Contains(insured) && !insured.Creature.HasPower<GfWorkPolicy>())
            await PowerCmd.Apply<GfWorkPolicy>(ctx, insured.Creature, IsUpgraded ? 2 : 1, Owner.Creature, this);
    }
}

public sealed class GfHotPot() : FriendCard(0, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];
    protected override Task OnPlay(PlayerChoiceContext ctx, CardPlay play) => Rules.StartPot(ctx, this, play);
}

public sealed class GfDontGreed() : FriendCard(1, CardType.Skill, CardRarity.Uncommon, TargetType.AnyAlly)
{
    protected override IEnumerable<DynamicVar> CanonicalVars => [new BlockVar(8, ValueProp.Move)];
    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        var player = Ally(play);
        await CreatureCmd.GainBlock(player.Creature, DynamicVars.Block, play);
        for (int n = 0; n < 2; n++)
        {
            var stab = CombatState!.CreateCard<GfOneTap>(player);
            if (IsUpgraded) CardCmd.Upgrade(stab);
            await CardPileCmd.AddGeneratedCardToCombat(stab, PileType.Hand, Owner);
        }
    }
}

public sealed class GfLightsOut() : FriendCard(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];
    protected override Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        Rules.Lights.Add((Owner, Level(8, 10)));
        return Task.CompletedTask;
    }
}

public sealed class GfSameHere() : FriendCard(1, CardType.Power, CardRarity.Uncommon, TargetType.AnyAlly)
{
    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        Rules.Partners.Add(new(Owner, Ally(play), Level(5, 6), CombatState!.RoundNumber));
        await PowerCmd.Apply<GfEchoPower>(ctx, Owner.Creature, 1, Owner.Creature, this);
    }
}

public sealed class GfKeyboard() : FriendCard(1, CardType.Skill, CardRarity.Rare, TargetType.AnyAlly)
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];
    protected override bool IsPlayable => Owner.PlayerCombatState?.Hand.Cards.Any(c => c.Type == CardType.Attack) == true;
    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        var chosen = await CardSelectCmd.FromHandForDiscard(ctx, Owner,
            new CardSelectorPrefs(CardSelectorPrefs.DiscardSelectionPrompt, 1), c => c.Type == CardType.Attack, this);
        if (chosen.FirstOrDefault() is not { } original) return;
        Player recipient = Ally(play);
        await CardCmd.Discard(ctx, original);
        var copy = original.CreateCloneForPlayer(recipient);
        CardCmd.ApplyKeyword(copy, CardKeyword.Ethereal, CardKeyword.Exhaust);
        copy.SetToFreeThisTurn();
        await CardPileCmd.AddGeneratedCardToCombat(copy, PileType.Hand, Owner);
        Rules.Keyboards.Add((Owner, recipient, copy, IsUpgraded));
    }
}

public sealed class GfInsurance() : FriendCard(2, CardType.Skill, CardRarity.Rare, TargetType.AnyPlayer)
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];
    protected override Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
        => PowerCmd.Apply<GfDeathPolicy>(ctx, play.Target ?? Owner.Creature, Level(1, 2), Owner.Creature, this);
}

public sealed class GfSavePotion() : FriendCard(1, CardType.Skill, CardRarity.Uncommon, TargetType.AnyAlly)
{
    protected override IEnumerable<DynamicVar> CanonicalVars => [new BlockVar(8, ValueProp.Move)];
    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        var friend = Ally(play);
        await CreatureCmd.GainBlock(friend.Creature, DynamicVars.Block, play);
        Rules.Potions.Add((Owner, friend));
    }
    protected override void OnUpgrade() => DynamicVars.Block.UpgradeValueBy(3);
}

public sealed class GfBigSpender() : FriendCard(1, CardType.Power, CardRarity.Uncommon, TargetType.Self)
{
    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        Rules.ApplauseCards.Add(new(Owner, IsUpgraded));
        await PowerCmd.Apply<GfApplausePower>(ctx, Owner.Creature, 1, Owner.Creature, this);
    }
}

public sealed class GfScrap() : FriendCard(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];
    protected override IEnumerable<DynamicVar> CanonicalVars => [new BlockVar(5, ValueProp.Move)];
    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, play);
        Rules.MarkScrap(Owner);
    }
    protected override void OnUpgrade() => AddKeyword(CardKeyword.Retain);
}

public sealed class GfLogOff() : FriendCard(0, CardType.Skill, CardRarity.Rare, TargetType.AnyAlly)
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];
    protected override bool HasEnergyCostX => true;
    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        var recipient = Ally(play);
        await PlayerCmd.GainEnergy(ResolveEnergyXValue() + 1, recipient);
        await CardPileCmd.Draw(ctx, Level(1, 2), recipient);
        PlayerCmd.EndTurn(Owner, canBackOut: false);
    }
}
