using MegaCrit.Sts2.Core.Entities.Powers;

namespace GoodFriend;

public sealed class GfEchoPower : FriendPower
{
    internal override string Illustration => "GF_SAME_HERE";
    // Each cast pairs its own issuer with the selected teammate.
    public override PowerInstanceType InstanceType => PowerInstanceType.Instanced;

    private EchoTurn<CardType> _turn;

    public override Task BeforeSideTurnStart(PlayerChoiceContext ctx, CombatSide side,
        IReadOnlyList<Creature> participants, ICombatState state)
    {
        if (side == CombatSide.Player) _turn.Begin();
        return Task.CompletedTask;
    }

    public override async Task AfterCardPlayed(PlayerChoiceContext ctx, CardPlay play)
    {
        if (play.Player.Creature == Owner)
        {
            _turn.OpenWith(play.Card.Type);
            return;
        }
        if (play.Player.Creature != Applier || !_turn.TryReply(play.Card.Type)) return;
        if (!play.Player.Creature.IsAlive || !Owner.IsAlive) return;

        await CreatureCmd.GainBlock(play.Player.Creature, Amount, ValueProp.Unpowered, null);
        if (Owner.IsAlive) await CreatureCmd.GainBlock(Owner, Amount, ValueProp.Unpowered, null);
    }

    public override Task AfterDeath(PlayerChoiceContext ctx, Creature creature, CardModel? source, bool prevented, float anim)
        => !prevented && creature == Applier ? PowerCmd.Remove(this) : Task.CompletedTask;
}
