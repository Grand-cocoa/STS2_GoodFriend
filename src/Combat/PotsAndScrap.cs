using MegaCrit.Sts2.Core.Platform;
using MegaCrit.Sts2.Core.Runs;

namespace GoodFriend;

public sealed partial class FriendCombat
{
    internal sealed class Pot(Player holder, bool upgraded, CardPlay opening)
    {
        internal Player Holder = holder;
        internal readonly CardPlay Opening = opening;
        internal readonly PotDamage Damage = new(upgraded);
    }
    internal readonly List<Pot> Pots = [];
    private readonly ScrapClaims<Player, CardModel> _scrap = new();
    private readonly HashSet<CardModel> _temporaryEthereal = [];

    internal async Task StartPot(PlayerChoiceContext ctx, GfHotPot card, CardPlay opening)
    {
        Pots.Add(new(card.Owner, card.IsUpgraded, opening));
        await RefreshPot(ctx, card.Owner);
    }
    private async Task RefreshPot(PlayerChoiceContext ctx, Player holder)
    {
        int total = Pots.Where(p => p.Holder == holder).Sum(p => p.Damage.Holder);
        var marker = holder.Creature.GetPower<GfPotPower>();
        if (total == 0) { if (marker != null) await PowerCmd.Remove(marker); }
        else if (marker != null) marker.SetAmount(total);
        else await PowerCmd.Apply<GfPotPower>(ctx, holder.Creature, total, holder.Creature, null);
    }

    private async Task OfferPots(PlayerChoiceContext ctx, CardPlay play)
    {
        Player holder = play.Card.Owner;
        foreach (var pot in Pots.Where(p => p.Holder == holder && p.Opening != play).ToArray())
        {
            Player[] others = _combat.Players.Where(p => p != holder && p.Creature.IsAlive).ToArray();
            if (others.Length == 0 || !holder.Creature.IsAlive) continue;
            var labels = others.Select(p => p.Character.Title.GetFormattedText() + " · " +
                PlatformUtil.GetPlayerName(RunManager.Instance.NetService.Platform, p.NetId)).ToArray();
            int choice = await GfChoice.Choose(ctx, holder, "GF_HOT_POT", labels, allowSkip: true);
            if (choice < 0 || !others[choice].Creature.IsAlive) continue;
            pot.Holder = others[choice];
            pot.Damage.Pass();
            await RefreshPot(ctx, holder);
            await RefreshPot(ctx, pot.Holder);
        }
    }

    private async Task ExplodePots(PlayerChoiceContext ctx)
    {
        foreach (var pot in Pots.ToArray())
        {
            Pots.Remove(pot);
            await RefreshPot(ctx, pot.Holder);
            if (pot.Holder.Creature.IsAlive) await FriendCard.Hit(ctx, pot.Holder.Creature, pot.Damage.Holder);
            foreach (var enemy in _combat.Enemies.Where(c => c.IsAlive).ToArray()) await FriendCard.Hit(ctx, enemy, pot.Damage.Enemies);
        }
    }

    internal void MarkScrap(Player collector)
    {
        var cards = _combat.Players.SelectMany(p => p.PlayerCombatState!.Hand.Cards)
            .Where(c => c.Type == CardType.Status).ToHashSet();
        _scrap.Mark(collector, cards);
        foreach (var c in cards)
        {
            if (c.Keywords.Contains(CardKeyword.Ethereal)) continue;
            _temporaryEthereal.Add(c);
            CardCmd.ApplyKeyword(c, CardKeyword.Ethereal);
        }
    }

    public Task AfterCardExhausted(PlayerChoiceContext ctx, CardModel card, bool causedByEthereal)
    {
        _scrap.Exhaust(card, causedByEthereal);
        return Task.CompletedTask;
    }

    public async Task AfterSideTurnEndLate(PlayerChoiceContext ctx, CombatSide side, IEnumerable<Creature> participants)
    {
        if (side != CombatSide.Player) return;
        foreach (var (collector, amount) in _scrap.Settle())
            if (collector.Creature.IsAlive) await Shield(collector, amount);
        foreach (var c in _temporaryEthereal) CardCmd.RemoveKeyword(c, CardKeyword.Ethereal);
        _temporaryEthereal.Clear();
    }
}
