# Run with Windows PowerShell 5.1. Uses a temporary Steam library, no game installation.
$ErrorActionPreference = 'Stop'
$root = [IO.Directory]::CreateDirectory([IO.Path]::Combine([IO.Path]::GetTempPath(), 'gf [coop] & ' + [guid]::NewGuid()))
$apps = [IO.Path]::Combine($root.FullName, 'steamapps')
$game = [IO.Directory]::CreateDirectory([IO.Path]::Combine($apps, 'common', 'Slay the Spire 2'))
$cache = [IO.Path]::Combine($apps, 'workshop', 'content', '2868840')
$helper = [IO.Path]::Combine($game.FullName, 'Local-Coop-Workshop.ps1')
$children = @()

function New-TestMod([string]$Directory, [string]$Id) {
    $null = [IO.Directory]::CreateDirectory($Directory)
    [IO.File]::WriteAllText([IO.Path]::Combine($Directory, "$Id.json"), ('{"id":"' + $Id + '","has_dll":true}'))
    [IO.File]::WriteAllText([IO.Path]::Combine($Directory, "$Id.dll"), $Id)
}

try {
    [IO.File]::Copy([IO.Path]::Combine($PSScriptRoot, '..', 'tools', 'Local-Coop-Workshop.ps1'), $helper)
    New-TestMod ([IO.Path]::Combine($game.FullName, 'mods', 'GoodFriend')) 'GoodFriend'
    New-TestMod ([IO.Path]::Combine($cache, '11', 'nested')) 'BaseLib'
    New-TestMod ([IO.Path]::Combine($cache, '12', 'one')) 'BundleMod'
    New-TestMod ([IO.Path]::Combine($cache, '12', 'two')) 'GoodFriend'
    New-TestMod ([IO.Path]::Combine($cache, '13')) 'Disabled'
    $account = 76561197960265729L
    $active = (Get-ItemProperty -LiteralPath 'HKCU:\Software\Valve\Steam\ActiveProcess' -Name ActiveUser -ErrorAction SilentlyContinue).ActiveUser
    if ($active) { $account = 76561197960265728L + ([int64]$active -band 4294967295L) }
    $prefs = [IO.Directory]::CreateDirectory([IO.Path]::Combine($root.FullName, 'SlayTheSpire2', 'steam', [string]$account))
    [IO.File]::WriteAllText([IO.Path]::Combine($prefs.FullName, 'settings.save'), '{"mod_settings":{"mod_list":[{"id":"Disabled","source":"steam_workshop","is_enabled":false}]}}')

    $probe = @'
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
class Probe {
    static int Main(string[] args) {
        string role = args.Single(a => a.StartsWith("--clientId=")).Substring(11);
        string exe = Process.GetCurrentProcess().MainModule.FileName;
        string mods = Path.Combine(Path.GetDirectoryName(exe), "mods");
        var manifests = Directory.GetFiles(mods, "*.json", SearchOption.AllDirectories);
        var names = manifests.Select(Path.GetFileNameWithoutExtension).OrderBy(x => x).ToArray();
        if (!names.SequenceEqual(new[]{"BaseLib", "BundleMod", "GoodFriend"}) ||
            manifests.Any(p => !File.Exists(Path.ChangeExtension(p, "dll"))) ||
            !args.Contains("--force-steam=off")) return 10;
        string result = Environment.GetEnvironmentVariable("GF_TEST_RESULT");
        File.WriteAllText(Path.Combine(result, role + ".ready"), exe);
        for (int i = 0; i < 400 && !File.Exists(Path.Combine(result, role + ".stop")); i++) Thread.Sleep(100);
        return 0;
    }
}
'@
    Add-Type -TypeDefinition $probe -OutputAssembly ([IO.Path]::Combine($game.FullName, 'SlayTheSpire2.exe')) -OutputType ConsoleApplication
    foreach ($role in '1','1000') {
        $info = New-Object Diagnostics.ProcessStartInfo
        $info.FileName = [IO.Path]::Combine($PSHOME, 'powershell.exe')
        $info.Arguments = '-NoProfile -NonInteractive -ExecutionPolicy Bypass -File "' + $helper + '"'
        $info.UseShellExecute = $false
        $info.EnvironmentVariables['APPDATA'] = $root.FullName
        $info.EnvironmentVariables['GOODFRIEND_WORKSHOP_DIR'] = $cache
        $info.EnvironmentVariables['GF_TEST_RESULT'] = $root.FullName
        $info.EnvironmentVariables['gfPlayerId'] = $role
        $info.EnvironmentVariables['gfLogFile'] = [IO.Path]::Combine($root.FullName, "$role.log")
        $info.EnvironmentVariables['gfGameArgs'] = "--force-steam=off --clientId=$role --goodfriend-local-coop"
        $children += [Diagnostics.Process]::Start($info)
    }
    foreach ($role in '1','1000') {
        $ready = [IO.Path]::Combine($root.FullName, "$role.ready")
        for ($i = 0; $i -lt 300 -and ![IO.File]::Exists($ready); $i++) { Start-Sleep -Milliseconds 100 }
        if (![IO.File]::Exists($ready)) {
            $report = [IO.Path]::Combine($root.FullName, "$role.workshop.log")
            throw "Player $role failed. $([IO.File]::ReadAllText($report))"
        }
        $view = [IO.Path]::Combine($game.FullName, '.GoodFriendLocalCoop', "player-$role")
        if ([IO.File]::ReadAllText($ready) -ine [IO.Path]::Combine($view,'SlayTheSpire2.exe')) { throw 'Wrong game directory' }
    }
    foreach ($role in '1000','1') {
        [IO.File]::WriteAllText([IO.Path]::Combine($root.FullName, "$role.stop"), '')
        $child = if ($role -eq '1') { $children[0] } else { $children[1] }
        if (!$child.WaitForExit(10000) -or $child.ExitCode -ne 0) { throw "Player $role failed to exit" }
    }
    if (([IO.DirectoryInfo][IO.Path]::Combine($game.FullName, '.GoodFriendLocalCoop')).GetDirectories().Length) { throw 'Test directory left behind' }
    if (![IO.File]::Exists([IO.Path]::Combine($cache,'11','nested','BaseLib.dll')) -or
        ![IO.File]::Exists([IO.Path]::Combine($game.FullName,'mods','GoodFriend','GoodFriend.dll')) -or
        ([IO.DirectoryInfo][IO.Path]::Combine($game.FullName,'mods')).GetDirectories().Length -ne 1) { throw 'Original mods changed' }
    Write-Output 'Workshop launch test passed for host and guest.'
} finally {
    foreach ($role in '1','1000') { [IO.File]::WriteAllText([IO.Path]::Combine($root.FullName, "$role.stop"), '') }
    foreach ($child in $children) { $null = $child.WaitForExit(10000); $child.Dispose() }
    # A failed test can leave junctions. Keep its fixture for inspection in that case.
    $views = [IO.Path]::Combine($game.FullName, '.GoodFriendLocalCoop')
    if (![IO.Directory]::Exists($views) -or ([IO.DirectoryInfo]$views).GetDirectories().Length -eq 0) {
        $root.Delete($true)
    }
}
