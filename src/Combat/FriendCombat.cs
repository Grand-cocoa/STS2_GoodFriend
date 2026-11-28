using System.Runtime.CompilerServices;
using MegaCrit.Sts2.Core.Models.Powers;

namespace GoodFriend;

// One listener per combat. All clients process the same ordered engine events and choice results.
public sealed partial class FriendCombat
{
    private static readonly ConditionalWeakTable<ICombatState, FriendCombat> Battles = new();
    private readonly ICombatState _combat;
    internal GfCombatHooks Listener { get; }
    private FriendCombat(ICombatState state)
    {
        _combat = state;
        Listener = GfCombatHooks.Create(this);
    }
    public static FriendCombat For(ICombatState state) => Battles.GetValue(state, s => new FriendCombat(s));

    internal record Followup(Player Owner, Creature Target, int Amount, bool Damage);
    internal sealed record Applause(Player Owner, bool Upgraded)
    {
        internal int LastRound = -1;
    }
    internal readonly List<(Player Owner, Creature Target)> Assists = [];
    internal readonly HashSet<Player> PaidWorkers = [];
    internal readonly List<Followup> Followups = [];
    internal readonly List<(Player Owner, int Block)> Lights = [];
    internal readonly List<(Player Owner, Player Friend, CardModel Copy, bool Upgraded)> Keyboards = [];
    internal readonly List<(Player Owner, Player Friend)> Potions = [];
    internal readonly List<Applause> ApplauseCards = [];
    internal readonly Dictionary<Player, int> FinalEnergy = [];
    private readonly Dictionary<Creature, HashSet<Player>> _hits = [];
    private readonly Dictionary<Creature, (Player Assistant, Player Killer)> _possibleKills = [];
    private readonly List<(CardPlay Play, HashSet<Creature> Targets)> _playing = [];
    private readonly Dictionary<Player, int> _nextEnergy = [], _nextDraw = [], _thisDraw = [];
    private bool _assistPaid;

    internal void NextEnergy(Player player, int delta) => _nextEnergy[player] = _nextEnergy.GetValueOrDefault(player) + delta;
    internal void NextDraw(Player player, int delta) => _nextDraw[player] = _nextDraw.GetValueOrDefault(player) + delta;
    internal bool HitByAnother(Player player, Creature target)
        => _hits.TryGetValue(target, out var hitters) && hitters.Any(p => p != player);
    private static Task Shield(Player p, int n) => CreatureCmd.GainBlock(p.Creature, n, ValueProp.Unpowered, null);

    public Task BeforeCardPlayed(CardPlay play)
    {
        _playing.Add((play, []));
        return Task.CompletedTask;
    }

    internal void RecordHit(Creature? dealer, DamageResult result, ValueProp props, Creature target, CardModel? source)
    {
        Player? attacker = dealer?.Player ?? dealer?.PetOwner;
        if (attacker == null || target.Side != CombatSide.Enemy) return;
        if (source?.Type == CardType.Attack && props.HasFlag(ValueProp.Move))
        {
            if (!_hits.TryGetValue(target, out var hitters)) _hits[target] = hitters = [];
            hitters.Add(attacker);
            _playing.LastOrDefault(item => item.Play.Card == source).Targets?.Add(target);
        }
        if (_assistPaid || !result.WasTargetKilled) return;
        var tag = Assists.FirstOrDefault(a => a.Target == target && a.Owner != attacker);
        if (tag.Owner != null) _possibleKills[target] = (tag.Owner, attacker);
    }

    private async Task SettleAssist(Creature target, bool prevented)
    {
        if (!_possibleKills.Remove(target, out var share) || prevented || _assistPaid) return;
        _assistPaid = true;
        await PlayerCmd.GainGold(8, share.Assistant);
        await PlayerCmd.GainGold(2, share.Killer);
    }

    public async Task AfterCardPlayed(PlayerChoiceContext ctx, CardPlay play)
    {
        var card = play.Card;
        Player player = card.Owner;
        var active = _playing.LastOrDefault(item => item.Play == play);
        _playing.Remove(active);
        if (active.Targets != null)
        {
            foreach (var follow in Followups.Where(f => f.Owner != player && active.Targets.Contains(f.Target)).ToArray())
            {
                Followups.Remove(follow);
                if (!follow.Damage) await PlayerCmd.GainEnergy(1, player);
                else if (follow.Target.IsAlive)
                {
                    await FriendCard.Hit(ctx, follow.Target, follow.Amount);
                    if (follow.Target.IsAlive && follow.Owner.Creature.IsAlive)
                        await FriendCard.Hit(ctx, follow.Owner.Creature, 3, true);
                }
            }
        }
        foreach (var key in Keyboards.Where(k => k.Copy == card && k.Friend == player).ToArray())
        {
            Keyboards.Remove(key);
            if (key.Upgraded && key.Owner.Creature.IsAlive) await CardPileCmd.Draw(ctx, key.Owner);
        }
        if (play.IsFirstInSeries && play.Resources.EnergySpent >= 2)
        {
            foreach (var perk in ApplauseCards.Where(a => a.Owner != player && a.Owner.Creature.IsAlive
                         && a.LastRound != _combat.RoundNumber).ToArray())
            {
                perk.LastRound = _combat.RoundNumber;
                await CardPileCmd.Draw(ctx, perk.Owner);
                if (perk.Upgraded) await Shield(player, 4);
            }
        }
        await PassPots(ctx, play);
    }

    public async Task AfterPotionUsed(PotionModel potion, Creature? target)
    {
        var ctx = new BlockingPlayerChoiceContext();
        foreach (var effect in Potions.Where(p => p.Friend == potion.Owner).ToArray())
        {
            Potions.Remove(effect);
            if (effect.Owner.Creature.IsAlive) await CardPileCmd.Draw(ctx, effect.Owner);
            if (effect.Friend.Creature.IsAlive) await CardPileCmd.Draw(ctx, effect.Friend);
        }
    }

    public async Task BeforeSideTurnStart(PlayerChoiceContext ctx, CombatSide side,
        IReadOnlyList<Creature> participants, ICombatState state)
    {
        if (side != CombatSide.Player) return;
        await ResolveInsurance(ctx);
        _hits.Clear();
        FinalEnergy.Clear();
        _thisDraw.Clear();
        foreach (var p in state.Players)
            if (participants.Contains(p.Creature) && _nextDraw.Remove(p, out int n)) _thisDraw[p] = n;
    }

    public decimal ModifyHandDraw(Player player, decimal count) => Math.Max(0, count + _thisDraw.GetValueOrDefault(player));

    public async Task AfterEnergyReset(Player player)
    {
        if (!_nextEnergy.Remove(player, out int amount)) return;
        if (amount > 0) await PlayerCmd.GainEnergy(amount, player);
        if (amount < 0) await PlayerCmd.LoseEnergy(-amount, player);
    }

    public async Task BeforeSideTurnEndVeryEarly(PlayerChoiceContext ctx, CombatSide side, IEnumerable<Creature> participants)
    {
        if (side != CombatSide.Player) return;
        foreach (var key in Keyboards.ToArray())
            if (key.Copy.Pile?.Type == PileType.Hand && key.Copy.Owner == key.Friend && key.Owner.Creature.IsAlive)
                await Shield(key.Owner, 4);
        Keyboards.Clear();
        var living = _combat.Players.Where(p => p.Creature.IsAlive).ToArray();
        var savers = living.Where(p => FinalEnergy.GetValueOrDefault(p, p.PlayerCombatState?.Energy ?? 0) > 0).ToArray();
        foreach (var effect in Lights.ToArray())
        {
            if (savers.Length == 0 && effect.Owner.Creature.IsAlive)
                await PowerCmd.Apply<VulnerablePower>(ctx, effect.Owner.Creature, 1, effect.Owner.Creature, null);
            else foreach (var p in savers.Length == 1 ? living : savers)
                await Shield(p, savers.Length == 1 ? effect.Block : 4);
        }
        Lights.Clear();
        await ExplodePots(ctx);
        Assists.Clear();
        Followups.Clear();
        Potions.Clear();
    }
}
