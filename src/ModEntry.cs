using HarmonyLib;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Modding;

namespace GoodFriend;

[ModInitializer(nameof(Initialize))]
public static class ModEntry
{
    public const string Id = "GoodFriend";

    public static void Initialize()
    {
        var assembly = typeof(ModEntry).Assembly;
        var harmony = new Harmony("goodfriend.coop.cards");
        try
        {
            harmony.PatchAll(assembly);
        }
        catch
        {
            harmony.UnpatchAll(harmony.Id);
            throw;
        }

        foreach (Type type in assembly.GetTypes()
                     .Where(t => !t.IsAbstract && t.IsSubclassOf(typeof(FriendCard)))
                     .OrderBy(t => t.FullName, StringComparer.Ordinal))
        {
            if (type != typeof(GfCrowd) && !type.IsSubclassOf(typeof(TransientCard)))
                ModHelper.AddModelToPool(typeof(ColorlessCardPool), type);
        }
        ModHelper.SubscribeForCombatStateHooks(Id, state =>
            state.Players.Count > 1 ? [FriendCombat.For(state).Listener] : []);
        ModHelper.SubscribeForRunStateHooks(Id, _ => [ModelDb.GetById<StarterGuard>(ModelDb.GetId<StarterGuard>())]);
        Log.Info($"[GoodFriend] {assembly.GetName().Version!.ToString(3)} initialized");
    }
}
