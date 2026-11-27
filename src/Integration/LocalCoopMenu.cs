using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Nodes.Screens.MainMenu;

namespace GoodFriend;

[HarmonyPatch(typeof(NMainMenu), nameof(NMainMenu._Ready))]
internal static class LocalCoopMenu
{
    private static void Postfix(NMainMenu __instance)
    {
        if (!CommandLineHelper.HasArg("goodfriend-local-coop")) return;
        // The Workshop mod can leave its Godot autoload active in an offline process without its DLL.
        if (ModManager.Mods.Any(m => m.state == ModLoadState.Loaded && m.manifest?.id == "ItsLoading")) return;
        var splash = __instance.GetNodeOrNull<Node>("/root/LoadingBarBoot");
        if (splash?.HasMethod("takeover") != true) return;
        splash.CallDeferred("takeover");
        Log.Info("[GoodFriend] Local co-op main menu ready, dismissing the offline ItsLoading splash");
    }
}
