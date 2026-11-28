using HarmonyLib;
using MegaCrit.Sts2.Core.Runs;

namespace GoodFriend;

[HarmonyPatch(typeof(RunManager), nameof(RunManager.SetUpNewMultiplayer))]
internal static class StartingDeckPatch
{
    // Starting-deck modifiers have finished here. Resuming a run uses a separate entry point.
    private static void Postfix(RunState state)
    {
        if (state.Players.Count < 2) return;
        foreach (var player in state.Players)
        {
            if (player.Deck.Cards.Any(card => card is GfCrowd)) continue;
            var starter = state.CreateCard<GfCrowd>(player);
            starter.FloorAddedToDeck = 1;
            player.Deck.AddInternal(starter, silent: true);
        }
    }
}
