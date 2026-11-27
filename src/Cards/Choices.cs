using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models.Cards;

namespace GoodFriend;

/// <summary>Display-only options. The built-in selector synchronizes the chosen index.</summary>
public sealed class GfChoice() : TransientCard(0, CardType.Skill, TargetType.Self)
{
    private string? _caption;
    private string? _art;
    public override string Title => _caption ?? base.Title;
    public override string Art => _art ?? "GF_OFFER_BOTTLE";
    protected override bool IsPlayable => false;

    internal static string Text(string key) => new LocString("card_selection", "GOOD_FRIEND." + key).GetFormattedText();

    internal static async Task<int> Choose(PlayerChoiceContext ctx, Player chooser, string art,
        IReadOnlyList<string> captions, bool allowSkip = false, IReadOnlyList<string>? optionArt = null)
    {
        var options = captions.Select((label, index) =>
        {
            var card = chooser.Creature.CombatState!.CreateCard<GfChoice>(chooser);
            card._caption = label;
            card._art = optionArt?[index] ?? art;
            return (CardModel)card;
        }).ToList();
        try
        {
            CardModel? picked = await CardSelectCmd.FromChooseACardScreen(ctx, options, chooser, allowSkip);
            return picked == null ? -1 : options.IndexOf(picked);
        }
        finally
        {
            foreach (CardModel preview in options) chooser.Creature.CombatState?.RemoveCard(preview);
        }
    }
}

public sealed class GfOfferBottle() : FriendCard(1, CardType.Skill, CardRarity.Uncommon, TargetType.AnyAlly)
{
    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        Player friend = Ally(play);
        string[] labels = [GfChoice.Text("take"), GfChoice.Text("yield")];
        // Resolve nothing until both private choices are committed.
        string[] illustrations = ["GF_TAKE", "GF_YIELD"];
        bool mine = await GfChoice.Choose(ctx, Owner, Art, labels, optionArt: illustrations) == 0;
        bool theirs = await GfChoice.Choose(ctx, friend, Art, labels, optionArt: illustrations) == 0;
        if (mine != theirs) await PlayerCmd.GainEnergy(2, mine ? Owner : friend);
        else if (!mine)
        {
            Rules.NextEnergy(Owner, 1);
            Rules.NextEnergy(friend, 1);
        }
        else
        {
            await Hit(ctx, Owner.Creature, 2);
            await Hit(ctx, friend.Creature, 2);
            foreach (var enemy in CombatState!.Enemies.Where(c => c.IsAlive).ToArray()) await Hit(ctx, enemy, 8);
        }
    }
    protected override void OnUpgrade() => EnergyCost.UpgradeBy(-1);
}

public sealed class GfBorrow() : FriendCard(0, CardType.Skill, CardRarity.Uncommon, TargetType.AnyAlly)
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];
    protected override bool IsPlayable => CombatState?.Players.Any(p =>
        p != Owner && p.Creature.IsAlive && p.PlayerCombatState?.Energy >= 1) == true;
    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        Player friend = Ally(play);
        if (friend.PlayerCombatState?.Energy >= 1 && await GfChoice.Choose(ctx, friend, Art,
                [GfChoice.Text("lend"), GfChoice.Text("decline")], optionArt: ["GF_LEND", "GF_YIELD"]) == 0 && friend.PlayerCombatState.Energy >= 1)
        {
            await PlayerCmd.LoseEnergy(1, friend);
            await PlayerCmd.GainEnergy(2, Owner);
            Rules.NextEnergy(Owner, -1);
            Rules.NextEnergy(friend, 1);
            if (IsUpgraded) await CardPileCmd.Draw(ctx, friend);
        }
        else await Block(Owner, 4, play);
    }
}

public sealed class GfSplitRewards() : FriendCard(2, CardType.Skill, CardRarity.Rare, TargetType.AnyAlly)
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];
    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        var friend = Ally(play);
        int solo = Owner.RunState.Rng.CombatCardSelection.NextInt(3);
        int[] pair = Enumerable.Range(0, 3).Where(i => i != solo).ToArray();
        string[] labels = [GfChoice.Text("energy"), GfChoice.Text("draw"), GfChoice.Text("block")];
        int choice = await GfChoice.Choose(ctx, friend, Art, [labels[solo], string.Join(" + ", pair.Select(i => labels[i]))]);
        Player single = choice == 0 ? friend : Owner;
        Player both = choice == 0 ? Owner : friend;
        for (int reward = 0; reward < 3; reward++)
        {
            var recipient = reward == solo ? single : both;
            switch (reward)
            {
                case 0: await PlayerCmd.GainEnergy(2, recipient); break;
                case 1: await CardPileCmd.Draw(ctx, 2, recipient); break;
                case 2: await Block(recipient, 6); break;
            }
        }
        Rules.NextDraw(both, -1);
    }
    protected override void OnUpgrade() => EnergyCost.UpgradeBy(-1);
}

public sealed class GfQuiz() : FriendCard(0, CardType.Skill, CardRarity.Uncommon, TargetType.AnyAlly)
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];
    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        CardType[] types = [CardType.Attack, CardType.Skill, CardType.Power];
        int guess = await GfChoice.Choose(ctx, Owner, Art,
            [GfChoice.Text("attack"), GfChoice.Text("skill"), GfChoice.Text("power")]);
        Player friend = Ally(play);
        CardModel? drawn = friend.Creature.IsAlive ? await CardPileCmd.Draw(ctx, friend) : null;
        if (guess >= 0 && drawn?.Type == types[guess])
        {
            if (!drawn.EnergyCost.CostsX) drawn.EnergyCost.AddThisTurn(-2);
            if (IsUpgraded) await CardPileCmd.Draw(ctx, friend);
        }
        else
        {
            var dazed = CombatState!.CreateCard<Dazed>(Owner);
            await CardPileCmd.AddGeneratedCardToCombat(dazed, PileType.Draw, Owner, CardPilePosition.Top);
        }
    }
}
