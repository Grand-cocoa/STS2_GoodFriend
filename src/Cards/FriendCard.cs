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
    protected static Player Ally(CardPlay play) => play.Target!.Player!;
    protected Task<AttackCommand> Strike(PlayerChoiceContext ctx, CardPlay play, Creature target, decimal damage)
        => DamageCmd.Attack(damage).FromCard(this, play).Targeting(target)
            .WithHitFx("vfx/vfx_attack_slash").Execute(ctx);
    protected static Task Block(Player player, int amount, CardPlay? play = null)
        => CreatureCmd.GainBlock(player.Creature, amount, play == null ? ValueProp.Unpowered : ValueProp.Move, play);
    internal static Task Hit(PlayerChoiceContext ctx, Creature target, int amount, bool bypassBlock = false)
        => CreatureCmd.Damage(ctx, target, amount, ValueProp.Unpowered | (bypassBlock ? ValueProp.Unblockable : 0),
            null, null, null);
}

public abstract class TransientCard(int cost, CardType type, TargetType target)
    : FriendCard(cost, type, CardRarity.Token, target, false)
{
    public override bool CanBeGeneratedInCombat => false;
    public override bool CanBeGeneratedByModifiers => false;
}
