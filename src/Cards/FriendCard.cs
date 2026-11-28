using MegaCrit.Sts2.Core.Commands.Builders;

namespace GoodFriend;

public abstract class FriendCard(int cost, CardType type, CardRarity rarity, TargetType target, bool show = true)
    : CardModel(cost, type, rarity, target, show)
{
    public override CardMultiplayerConstraint MultiplayerConstraint => CardMultiplayerConstraint.MultiplayerOnly;
    public override CardPoolModel Pool => ModelDb.CardPool<ColorlessCardPool>();
    public virtual string Art => Id.Entry;
    public override string PortraitPath => $"res://GoodFriend/art/{Art}.png";
    public override string BetaPortraitPath => PortraitPath;
    protected override string PortraitPngPath => PortraitPath;
    protected FriendCombat Rules => FriendCombat.For(CombatState!);
    protected int Level(int normal, int upgraded) => IsUpgraded ? upgraded : normal;
    protected Task<AttackCommand> Strike(PlayerChoiceContext ctx, CardPlay play, Creature target, decimal damage)
        => DamageCmd.Attack(damage).FromCard(this, play).Targeting(target)
            .WithHitFx("vfx/vfx_attack_slash").Execute(ctx);
    protected static Task Block(Player player, int amount, CardPlay? play = null)
        => CreatureCmd.GainBlock(player.Creature, amount, play == null ? ValueProp.Unpowered : ValueProp.Move, play);
    internal static Task Hit(PlayerChoiceContext ctx, Creature target, int amount, bool bypassBlock = false)
        => CreatureCmd.Damage(ctx, target, amount, ValueProp.Unpowered | (bypassBlock ? ValueProp.Unblockable : 0),
            null, null, null);
}

public abstract class TeammateCard(int cost, CardType type, CardRarity rarity)
    : FriendCard(cost, type, rarity, TargetType.AnyAlly)
{
    internal Player? GetTeammate(Creature? target)
    {
        if (target is not { IsAlive: true, Player: { } teammate, CombatState: { } battle }
            || teammate.NetId == Owner.NetId || battle != Owner.Creature.CombatState) return null;
        return CanTargetTeammate(teammate) ? teammate : null;
    }

    protected virtual bool CanTargetTeammate(Player teammate) => true;

    protected sealed override Task OnPlay(PlayerChoiceContext ctx, CardPlay play) =>
        GetTeammate(play.Target) is { } teammate ? PlayForTeammate(ctx, play, teammate) : Task.CompletedTask;

    protected abstract Task PlayForTeammate(PlayerChoiceContext ctx, CardPlay play, Player teammate);
}

public abstract class TransientCard(int cost, CardType type, TargetType target)
    : FriendCard(cost, type, CardRarity.Token, target, false)
{
    public override bool CanBeGeneratedInCombat => false;
    public override bool CanBeGeneratedByModifiers => false;
}
