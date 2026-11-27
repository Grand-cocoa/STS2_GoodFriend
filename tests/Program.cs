using GoodFriend;

int checks = 0;
void Expect(bool actual, string scenario)
{
    if (!actual) throw new InvalidOperationException(scenario);
    checks++;
}

foreach (bool upgraded in new[] { false, true })
{
    var pot = new PotDamage(upgraded);
    Expect((pot.Holder, pot.Enemies) == (5, 4), "An untouched pot starts at 5 / 4");
    pot.Pass();
    Expect((pot.Holder, pot.Enemies) == (upgraded ? (6, 7) : (7, 8)), "First transfer");
    for (int i = 1; i < 4; i++) pot.Pass();
    Expect((pot.Holder, pot.Enemies) == (upgraded ? (9, 16) : (13, 20)), "Four passes, including return passes");
    for (int i = 4; i < 100; i++) pot.Pass();
    Expect((pot.Holder, pot.Enemies) == (upgraded ? (105, 304) : (205, 404)), "A long chain has no transfer limit");
}

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
