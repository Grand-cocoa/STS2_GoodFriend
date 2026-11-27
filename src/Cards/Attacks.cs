using MegaCrit.Sts2.Core.Models.Cards;

namespace GoodFriend;

public sealed class GfAssist() : FriendCard(0, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy)
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DamageVar(1, ValueProp.Move)];
    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        Creature target = play.Target!;
        await Strike(ctx, play, target, DynamicVars.Damage.BaseValue);
        if (target.IsAlive) Rules.Assists.Add((Owner, target));
    }
    protected override void OnUpgrade() => AddKeyword(CardKeyword.Retain);
}

public sealed class GfTrustMe() : FriendCard(1, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy)
{
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DamageVar(6, ValueProp.Move)];
    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        Creature target = play.Target!;
        await Strike(ctx, play, target, DynamicVars.Damage.BaseValue);
        if (target.IsAlive) Rules.Followups.Add(new(Owner, target, Level(9, 12), true));
    }
}

public sealed class GfFocusFire() : FriendCard(1, CardType.Attack, CardRarity.Uncommon, TargetType.AllEnemies)
{
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DamageVar(5, ValueProp.Move)];
    protected override Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
        => DamageCmd.Attack(DynamicVars.Damage.BaseValue).FromCard(this, play).TargetingAllOpponents(CombatState!)
            .WithHitFx("vfx/vfx_attack_slash").Execute(ctx);

    public override decimal ModifyDamageAdditive(Creature? target, decimal amount, ValueProp props, Creature? dealer,
        CardModel? cardSource, CardPlay? cardPlay)
        => cardSource == this && target != null && CombatState != null && Rules.HitByAnother(Owner, target) ? Level(5, 8) : 0;
}

public sealed class GfSaveShield() : FriendCard(1, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy)
{
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DamageVar(8, ValueProp.Move)];
    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        Creature target = play.Target!;
        var strike = await Strike(ctx, play, target, DynamicVars.Damage.BaseValue);
        if (target.IsAlive && strike.Results.SelectMany(r => r).Any(r => r.Receiver == target && r.WasBlockBroken))
            Rules.Followups.Add(new(Owner, target, 1, false));
    }
    protected override void OnUpgrade() => DynamicVars.Damage.UpgradeValueBy(4);
}

public sealed class GfOneTap() : TransientCard(0, CardType.Attack, TargetType.AnyEnemy)
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Ethereal, CardKeyword.Exhaust];
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DamageVar(6, ValueProp.Move)];
    protected override bool IsPlayable => Owner.Creature.Block >= 4;
    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        if (Owner.Creature.Block < 4) return;
        await CreatureCmd.LoseBlock(ctx, Owner.Creature, 4, Owner.Creature);
        await Strike(ctx, play, play.Target!, DynamicVars.Damage.BaseValue);
    }
    protected override void OnUpgrade() => DynamicVars.Damage.UpgradeValueBy(2);
}
