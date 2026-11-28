using GoodFriend;

int checks = 0;
void Expect(bool actual, string scenario)
{
    if (!actual) throw new InvalidOperationException(scenario);
    checks++;
}

var host = new Seat(7);
var guest = new Seat(9);
Seat[] duo = [host, guest];
int NoRoll(int _) => throw new InvalidOperationException("A forced pass should not consume combat RNG");
foreach (bool upgraded in new[] { false, true })
{
    var pot = new HotPot<Seat>(host, upgraded);
    Expect(!pot.TryPass([host], p => p.Id, NoRoll), "A lone survivor keeps the pot");
    Expect((pot.HolderDamage, pot.EnemyDamage) == (5, 4), "No transfer leaves both damage totals unchanged");
    pot.TryPass(duo, p => p.Id, NoRoll);
    Expect(pot.Holder == guest && (pot.HolderDamage, pot.EnemyDamage) == (upgraded ? (6, 7) : (7, 8)), "First forced pass");
    for (int i = 1; i < 4; i++) pot.TryPass(duo, p => p.Id, NoRoll);
    Expect(pot.Holder == host && (pot.HolderDamage, pot.EnemyDamage) == (upgraded ? (9, 16) : (13, 20)), "Four passes, including return passes");
    for (int i = 4; i < 100; i++) pot.TryPass(duo, p => p.Id, NoRoll);
    Expect((pot.HolderDamage, pot.EnemyDamage) == (upgraded ? (105, 304) : (205, 404)), "A long chain has no transfer limit");
}

Seat[] partyOrder = [guest, new(1), host, new(3)];
Seat[] expectedRecipients = [partyOrder[1], partyOrder[3], guest];
for (int roll = 0; roll < expectedRecipients.Length; roll++)
{
    int Pick(int count)
    {
        Expect(count == 3, "In a party of four, only three players can receive the pot");
        return roll;
    }
    var firstClient = new HotPot<Seat>(host, false);
    var secondClient = new HotPot<Seat>(host, false);
    firstClient.TryPass(partyOrder, p => p.Id, Pick);
    secondClient.TryPass(partyOrder.Reverse(), p => p.Id, Pick);
    Expect(firstClient.Holder == expectedRecipients[roll] && firstClient.Holder == secondClient.Holder,
        "Both clients resolve each roll to the same teammate");
}

var echo = new EchoTurn<PlayedType>();
echo.OpenWith(PlayedType.Attack);
Expect(!echo.TryReply(PlayedType.Attack), "A partnership starts next turn");
echo.Begin();
Expect(!echo.TryReply(PlayedType.Skill), "Playing before the teammate opens does not spend the reply");
echo.OpenWith(PlayedType.Attack);
echo.OpenWith(PlayedType.Skill);
Expect(echo.TryReply(PlayedType.Attack), "A different attack matches the teammate's opening type");
Expect(!echo.TryReply(PlayedType.Attack), "Each partnership pays once per turn");
echo.Begin();
echo.OpenWith(PlayedType.Power);
Expect(!echo.TryReply(PlayedType.Skill) && !echo.TryReply(PlayedType.Power), "A mismatching first reply spends this turn's chance");

var claims = new InsuranceClaims<string>();
string[] party = ["Ironclad", "Silent", "Regent", "Defect"];
Expect(!claims.CoversWipe(party, _ => true), "An uninsured wipe ends the run");
Expect(!claims.File("Ironclad", true, true), "A prevented death does not cash a policy");
Expect(!claims.File("Defect", false, false), "An uninsured player cannot file");
Expect(claims.File("Silent", true, false), "Covered death before the last member falls");
Expect(!claims.File("Silent", true, false), "A stacked policy pays only one claim");
Expect(!claims.CoversWipe(party, p => p != "Regent"), "One living player keeps the usual turn order");
Expect(claims.CoversWipe(party, _ => true), "An earlier pending claim rescues an uninsured last death");
Expect(claims.File("Regent", true, false), "A second insured player can die in the same attack");
Expect(claims.Redeem("Silent") && claims.Contains("Regent"), "Pay each covered player independently");
Expect(claims.Redeem("Regent") && !claims.Redeem("Silent"), "A claim pays at most once");
Expect(!claims.CoversWipe(party, _ => true), "Spent policies cannot rescue a second wipe");

var scrap = new ScrapClaims<string, string>();
scrap.Mark("Silent", ["wound-1", "wound-2", "wound-3", "wound-4", "dazed"]);
scrap.Mark("Defect", ["wound-1", "dazed"]);
scrap.Exhaust("wound-2", false);
foreach (string card in new[] { "wound-1", "wound-3", "wound-4", "dazed" }) scrap.Exhaust(card, true);
scrap.Exhaust("dazed", true);
scrap.Exhaust("newly-drawn-wound", true);
var payout = scrap.Settle().ToDictionary();
Expect(payout["Silent"] == 12, "Only the four marked cards exhausted by Ethereal pay out");
Expect(payout["Defect"] == 6, "Another collector is paid for their own snapshot");
Expect(scrap.Settle().Length == 0, "The next turn cannot pay claims twice");
scrap.Exhaust("wound-2", true);
Expect(scrap.Settle().Length == 0, "Unspent marks expire at the end of that turn");

Console.WriteLine($"{checks} battle rule checks passed.");

sealed record Seat(ulong Id);
enum PlayedType { Attack, Skill, Power }
