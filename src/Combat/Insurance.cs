using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Localization;

namespace GoodFriend;

public abstract class FriendPower : PowerModel
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;
    internal abstract string Illustration { get; }
}
public sealed class GfDeathPolicy : FriendPower
{
    internal override string Illustration => "GF_INSURANCE";
}
public sealed class GfWorkPolicy : FriendPower
{
    internal override string Illustration => "GF_WORK_COVER";
}
public sealed class GfApplausePower : FriendPower
{
    internal override string Illustration => "GF_BIG_SPENDER";
    public override Task AfterRemoved(Creature oldOwner)
    {
        if (oldOwner.CombatState is { } state) FriendCombat.For(state).ApplauseCards.RemoveAll(p => p.Owner == oldOwner.Player);
        return Task.CompletedTask;
    }
}
public sealed class GfPotPower : FriendPower
{
    internal override string Illustration => "GF_HOT_POT";
    public override LocString Description
    {
        get
        {
            var text = base.Description;
            var pots = IsMutable && Owner?.CombatState is { } state
                ? FriendCombat.For(state).Pots.Where(p => p.Holder == Owner.Player).ToArray() : [];
            text.Add("Self", pots.Length == 0 ? 5 : pots.Sum(p => p.HolderDamage));
            text.Add("Area", pots.Length == 0 ? 4 : pots.Sum(p => p.EnemyDamage));
            return text;
        }
    }
}

public sealed partial class FriendCombat
{
    private readonly InsuranceClaims<Player> _pendingClaims = new();
    internal bool HasClaim(Player player) => _pendingClaims.Contains(player);
    internal bool RescuingWipe => _pendingClaims.CoversWipe(_combat.Players, p => p.Creature.IsDead);

    public async Task AfterDamageReceived(PlayerChoiceContext ctx, Creature target, DamageResult result, ValueProp props,
        Creature? dealer, CardModel? source)
    {
        if (target.Player is not { } player || !target.IsAlive || dealer?.Side != CombatSide.Enemy
            || !props.HasFlag(ValueProp.Move) || result.UnblockedDamage <= 0) return;
        if (target.GetPower<GfWorkPolicy>() is not { } cover || !PaidWorkers.Add(player)) return;
        var issuer = cover.Applier?.Player;
        await PowerCmd.Remove(cover);
        await CreatureCmd.Heal(target, 5);
        if (issuer != null) NextDraw(issuer, 1);
    }

    public async Task AfterDeath(Creature creature, bool wasRemovalPrevented)
    {
        if (creature.Side == CombatSide.Enemy)
            await SettleAssist(creature, wasRemovalPrevented);
        else if (creature.Player is { } player && _pendingClaims.File(player, creature.HasPower<GfDeathPolicy>(), wasRemovalPrevented))
            await PlayerCmd.SetEnergy(0, player);
    }

    private async Task ResolveInsurance(PlayerChoiceContext ctx)
    {
        // The pending claim survives removal of powers by the normal death procedure.
        foreach (var p in _combat.Players)
        {
            if (_pendingClaims.Redeem(p))
            {
                if (p.Creature.IsAlive) continue;
                await CreatureCmd.SetCurrentHp(p.Creature, 1);
                foreach (var buff in p.Creature.Powers.Where(x => x.Type == PowerType.Buff).ToArray())
                    await PowerCmd.Remove(buff);
                p.ActivateHooks();
                NextEnergy(p, 1);
                continue;
            }
            if (!p.Creature.IsAlive) continue;
            if (p.Creature.GetPower<GfDeathPolicy>() is { } policy)
                await PowerCmd.ModifyAmount(ctx, policy, -1, null, null);
            if (p.Creature.GetPower<GfWorkPolicy>() is { Amount: 1 } worker)
                await PowerCmd.Remove(worker);
        }
    }
}
