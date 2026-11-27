namespace GoodFriend;

internal sealed class PotDamage(bool upgraded)
{
    internal int Holder { get; private set; } = 5;
    internal int Enemies { get; private set; } = 4;

    internal void Pass()
    {
        Holder += upgraded ? 1 : 2;
        Enemies += upgraded ? 3 : 4;
    }
}

// Powers are removed on death. A filed claim belongs to the battle until the next player turn.
internal sealed class InsuranceClaims<TPlayer> where TPlayer : notnull
{
    private readonly HashSet<TPlayer> _pending = [];
    internal bool File(TPlayer player, bool insured, bool prevented) => insured && !prevented && _pending.Add(player);
    internal bool Contains(TPlayer player) => _pending.Contains(player);
    internal bool Redeem(TPlayer player) => _pending.Remove(player);
    internal bool CoversWipe(IEnumerable<TPlayer> party, Func<TPlayer, bool> dead) => _pending.Count > 0 && party.All(dead);
}

internal sealed class ScrapClaims<TPlayer, TCard> where TPlayer : notnull where TCard : notnull
{
    private readonly List<(TPlayer Owner, HashSet<TCard> Cards)> _claims = [];
    private readonly Dictionary<TPlayer, int> _payouts = [];

    internal void Mark(TPlayer owner, HashSet<TCard> cards) => _claims.Add((owner, cards));

    internal void Exhaust(TCard card, bool ethereal)
    {
        if (!ethereal) return;
        foreach (var (owner, cards) in _claims)
            if (cards.Remove(card)) _payouts[owner] = _payouts.GetValueOrDefault(owner) + 3;
    }

    internal KeyValuePair<TPlayer, int>[] Settle()
    {
        var earned = _payouts.ToArray();
        _claims.Clear();
        _payouts.Clear();
        return earned;
    }
}
