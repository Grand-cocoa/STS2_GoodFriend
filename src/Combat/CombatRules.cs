namespace GoodFriend;

internal class HotPot<TPlayer>(TPlayer holder, bool upgraded) where TPlayer : class
{
    internal TPlayer Holder { get; private set; } = holder;
    internal int HolderDamage { get; private set; } = 5;
    internal int EnemyDamage { get; private set; } = 4;

    internal bool TryPass(IEnumerable<TPlayer> livingPlayers, Func<TPlayer, ulong> playerId, Func<int, int> roll)
    {
        // Screen order may differ between peers. Network IDs give every roll the same recipient.
        var holderId = playerId(Holder);
        var candidates = livingPlayers.Where(p => playerId(p) != holderId).OrderBy(playerId).ToArray();
        if (candidates.Length == 0) return false;
        Holder = candidates.Length == 1 ? candidates[0] : candidates[roll(candidates.Length)];
        HolderDamage += upgraded ? 1 : 2;
        EnemyDamage += upgraded ? 3 : 4;
        return true;
    }
}

internal struct EchoTurn<TCardType> where TCardType : struct, Enum
{
    private TCardType? _opening;
    private bool _awaitingReply;

    internal void Begin()
    {
        _opening = null;
        _awaitingReply = true;
    }

    internal void OpenWith(TCardType type)
    {
        if (_awaitingReply) _opening ??= type;
    }

    internal bool TryReply(TCardType type)
    {
        if (!_awaitingReply || _opening is not { } opening) return false;
        _awaitingReply = false;
        return EqualityComparer<TCardType>.Default.Equals(opening, type);
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
