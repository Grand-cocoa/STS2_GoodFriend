using System.Runtime.CompilerServices;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Merchant;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Nodes.Screens.Shops;

namespace GoodFriend;

[HarmonyPatch(typeof(MerchantInventory), "GetDefaultColorlessCardEntries", typeof(Player))]
internal static class CoopShop
{
    internal static readonly ConditionalWeakTable<MerchantCardEntry, List<CardModel>> Pools = new();

    private static void Postfix(Player player, List<MerchantCardEntry> __result)
    {
        if (player.RunState.Players.Count < 2) return;
        var pool = MultiplayerPool.For(player, false);
        if (pool.Count == 0) return;
        var extra = new MerchantCardEntry(player, pool, CardRarity.Uncommon);
        Pools.Add(extra, pool);
        if (__result.Any(entry => entry.IsStocked)) extra.Populate(null);
        __result.Add(extra);
    }
}

[HarmonyPatch(typeof(MerchantCardEntry), nameof(MerchantCardEntry.Populate))]
internal static class CoopStockPatch
{
    private static readonly Action<MerchantCardEntry, CardCreationResult?> SetStock =
        AccessTools.PropertySetter(typeof(MerchantCardEntry), nameof(MerchantCardEntry.CreationResult))
            .CreateDelegate<Action<MerchantCardEntry, CardCreationResult?>>();

    private static bool Prefix(MerchantCardEntry __instance, MerchantInventory? inventory)
    {
        if (!CoopShop.Pools.TryGetValue(__instance, out var pool)) return true;
        var player = __instance.Player;
        var displayed = inventory?.CardEntries.Select(e => e.CreationResult?.Card.Id).ToHashSet();
        var available = displayed == null ? pool : pool.Where(c => !displayed.Contains(c.Id)).ToList();
        if (available.Count == 0) available = pool;
        var chosen = available[player.PlayerRng.Shops.NextInt(available.Count)];
        var original = player.RunState.CreateCard(chosen, player);
        var results = new List<CardCreationResult> { new(original) };
        Hook.ModifyMerchantCardCreationResults(player.RunState, player, results);
        var modified = results.FirstOrDefault();
        SetStock(__instance, modified != null && pool.Any(c => c.Id == modified.Card.Id) ? modified : new(original));
        __instance.CalcCost();
        return false;
    }
}

[HarmonyPatch(typeof(NMerchantInventory), nameof(NMerchantInventory.Initialize))]
internal static class CoopShelfPatch
{
    private static void Prefix(NMerchantInventory __instance, MerchantInventory inventory)
    {
        if (!inventory.ColorlessCardEntries.Any(e => CoopShop.Pools.TryGetValue(e, out _))) return;
        var row = __instance.GetNode<Control>("%ColorlessCards");
        if (row.GetChildCount() >= inventory.ColorlessCardEntries.Count) return;
        var first = row.GetChild<NMerchantCard>(0);
        var last = row.GetChild<NMerchantCard>(row.GetChildCount() - 1);
        var step = row.GetChild<NMerchantCard>(1).Position - first.Position;
        var right = last.Position;
        // Extend the row leftwards. Relics and potions already occupy the space to the right.
        foreach (var slot in row.GetChildren().OfType<NMerchantCard>()) slot.Position -= step;
        var extra = GD.Load<PackedScene>(last.SceneFilePath).Instantiate<NMerchantCard>();
        row.AddChild(extra);
        extra.Position = right;
    }
}
