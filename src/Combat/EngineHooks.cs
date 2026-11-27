namespace GoodFriend;

// The game creates canonical AbstractModels only once. Mutable listeners delegate to state owned by this battle.
public sealed class GfCombatHooks : AbstractModel
{
    private FriendCombat _rules = null!;
    public override bool ShouldReceiveCombatHooks => true;

    public static GfCombatHooks Create(FriendCombat rules)
    {
        var canonical = ModelDb.GetById<GfCombatHooks>(ModelDb.GetId<GfCombatHooks>());
        var listener = (GfCombatHooks)canonical.MutableClone();
        listener._rules = rules;
        return listener;
    }

    public override Task BeforeCardPlayed(CardPlay play) => _rules.BeforeCardPlayed(play);
    public override Task AfterCardPlayed(PlayerChoiceContext ctx, CardPlay play) => _rules.AfterCardPlayed(ctx, play);
    public override Task AfterDamageGiven(PlayerChoiceContext ctx, Creature? dealer, DamageResult result,
        ValueProp props, Creature target, CardModel? source)
    {
        _rules.RecordHit(dealer, result, props, target, source);
        return Task.CompletedTask;
    }
    public override Task AfterDamageReceived(PlayerChoiceContext ctx, Creature target, DamageResult result,
        ValueProp props, Creature? dealer, CardModel? source) => _rules.AfterDamageReceived(ctx, target, result, props, dealer, source);
    public override Task AfterDeath(PlayerChoiceContext ctx, Creature creature, CardModel? source, bool prevented, float anim)
        => _rules.AfterDeath(creature, prevented);
    public override Task AfterPotionUsed(PotionModel potion, Creature? target) => _rules.AfterPotionUsed(potion, target);
    public override Task BeforeSideTurnStart(PlayerChoiceContext ctx, CombatSide side, IReadOnlyList<Creature> participants, ICombatState state)
        => _rules.BeforeSideTurnStart(ctx, side, participants, state);
    public override Task AfterEnergyReset(Player player) => _rules.AfterEnergyReset(player);
    public override decimal ModifyHandDraw(Player player, decimal count) => _rules.ModifyHandDraw(player, count);
    public override Task BeforeSideTurnEndVeryEarly(PlayerChoiceContext ctx, CombatSide side, IEnumerable<Creature> participants)
        => _rules.BeforeSideTurnEndVeryEarly(ctx, side, participants);
    public override Task AfterCardExhausted(PlayerChoiceContext ctx, CardModel card, bool causedByEthereal)
        => _rules.AfterCardExhausted(ctx, card, causedByEthereal);
    public override Task AfterSideTurnEndLate(PlayerChoiceContext ctx, CombatSide side, IEnumerable<Creature> participants)
        => _rules.AfterSideTurnEndLate(ctx, side, participants);
}
