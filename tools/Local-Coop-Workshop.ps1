# Windows PowerShell 5.1. The launcher supplies the role and the normal game arguments.
$ErrorActionPreference = 'Stop'
$game = [IO.DirectoryInfo]$PSScriptRoot
$role = $env:gfPlayerId
if ($role -notin '1','1000','2000','3000' -or !$env:gfLogFile) { throw 'Start this helper through Local-Coop-Test.cmd or Local-Coop-Player.cmd.' }
$report = [IO.Path]::ChangeExtension($env:gfLogFile, 'workshop.log')

function Write-Report([string]$Message) {
    [IO.File]::AppendAllText($report, $Message + [Environment]::NewLine)
}

function Get-RoleLockPath([string]$Id) {
    [IO.Path]::Combine([IO.Path]::GetDirectoryName($env:gfLogFile), "player-$Id.lock")
}

function Get-WorkshopRoots {
    if ($env:GOODFRIEND_WORKSHOP_DIR) {
        $override = Get-Item -LiteralPath $env:GOODFRIEND_WORKSHOP_DIR
        if (!$override.PSIsContainer) { throw 'GOODFRIEND_WORKSHOP_DIR must point to the Workshop content directory for 2868840.' }
        $override.FullName
        return
    }
    $libraries = @()
    if ($game.Parent -and $game.Parent.Parent) { $libraries += $game.Parent.Parent.FullName }
    $steam = (Get-ItemProperty -LiteralPath 'HKCU:\Software\Valve\Steam' -Name SteamPath -ErrorAction SilentlyContinue).SteamPath
    if (!$steam -and ${env:ProgramFiles(x86)}) { $steam = [IO.Path]::Combine(${env:ProgramFiles(x86)}, 'Steam') }
    if ($steam) {
        $apps = [IO.Path]::Combine($steam, 'steamapps')
        $libraries += $apps
        $vdf = [IO.Path]::Combine($apps, 'libraryfolders.vdf')
        if ([IO.File]::Exists($vdf)) {
            foreach ($line in [IO.File]::ReadLines($vdf)) {
                if ($line -match '^\s*"path"\s+"([^"]+)"') {
                    $libraries += [IO.Path]::Combine($Matches[1].Replace('\\','\'), 'steamapps')
                }
            }
        }
    }
    $seen = @{}
    foreach ($library in $libraries) {
        $path = [IO.Path]::GetFullPath([IO.Path]::Combine($library, 'workshop', 'content', '2868840'))
        if (!$seen.ContainsKey($path) -and [IO.Directory]::Exists($path)) { $seen[$path] = $true; $path }
    }
}

function Get-DisabledWorkshopMods {
    $disabled = @{}
    $accounts = [IO.Path]::Combine($env:APPDATA, 'SlayTheSpire2', 'steam')
    if (![IO.Directory]::Exists($accounts)) { return $disabled }
    $active = (Get-ItemProperty -LiteralPath 'HKCU:\Software\Valve\Steam\ActiveProcess' -Name ActiveUser -ErrorAction SilentlyContinue).ActiveUser
    $settings = $null
    if ($active) {
        $steamId = 76561197960265728L + ([int64]$active -band 4294967295L)
        $settings = [IO.Path]::Combine($accounts, [string]$steamId, 'settings.save')
    } else {
        $files = @(foreach ($dir in ([IO.DirectoryInfo]$accounts).GetDirectories()) {
            $path = [IO.Path]::Combine($dir.FullName, 'settings.save')
            if ($dir.Name -match '^\d+$' -and [IO.File]::Exists($path)) { $path }
        })
        if ($files.Count -eq 1) { $settings = $files[0] }
    }
    if ($settings -and [IO.File]::Exists($settings)) {
        try {
            $prefs = ConvertFrom-Json -InputObject ([IO.File]::ReadAllText($settings))
            foreach ($entry in $prefs.mod_settings.mod_list) {
                $source = ([string]$entry.source).Replace('_','')
                if ($entry.id -and $entry.is_enabled -is [bool] -and !$entry.is_enabled -and $source -in '','2','SteamWorkshop') {
                    $disabled[[string]$entry.id] = $true
                }
            }
        } catch { Write-Report "Could not read Steam mod switches: $($_.Exception.Message)" }
    }
    return $disabled
}

function Get-ModManifests([IO.DirectoryInfo]$Folder, [switch]$LocalRoot) {
    foreach ($file in $Folder.GetFiles('*.json')) {
        try {
            $m = ConvertFrom-Json -InputObject ([IO.File]::ReadAllText($file.FullName))
            if ($m.id -isnot [string] -or !$m.id -or $m.id -in '.','..' -or $m.id.IndexOfAny([IO.Path]::GetInvalidFileNameChars()) -ge 0) { continue }
            $dll = [IO.File]::Exists([IO.Path]::Combine($Folder.FullName, $m.id + '.dll'))
            $pck = [IO.File]::Exists([IO.Path]::Combine($Folder.FullName, $m.id + '.pck'))
            if ($null -eq $m.has_dll -and $null -eq $m.has_pck -and !$dll -and !$pck) { continue }
            [pscustomobject]@{ Id = $m.id; File = $file; Complete = ((!$m.has_dll -or $dll) -and (!$m.has_pck -or $pck)) }
        } catch { continue }
    }
    foreach ($dir in $Folder.GetDirectories()) {
        if ($LocalRoot -or !($dir.Attributes -band [IO.FileAttributes]::ReparsePoint)) { Get-ModManifests $dir }
    }
}

function New-ViewLink([string]$Path, [string]$Target, [switch]$Directory) {
    # Use literal filesystem paths, including square brackets in Steam library names.
    $flag = if ($Directory) { '/J' } else { '/H' }
    $info = New-Object Diagnostics.ProcessStartInfo
    $info.FileName = $env:ComSpec
    $info.UseShellExecute = $false
    $info.CreateNoWindow = $true
    $info.EnvironmentVariables['GF_VIEW_LINK'] = $Path
    $info.EnvironmentVariables['GF_VIEW_SOURCE'] = $Target
    $info.Arguments = '/d /v:off /c mklink ' + $flag + ' "%GF_VIEW_LINK%" "%GF_VIEW_SOURCE%" >nul'
    $info.RedirectStandardError = $true
    $link = [Diagnostics.Process]::Start($info)
    try {
        $detail = $link.StandardError.ReadToEnd().Trim()
        $link.WaitForExit()
        if ($link.ExitCode -ne 0) { throw "Could not link $Path to $Target. $detail" }
    } finally { $link.Dispose() }
}

function Add-File([IO.FileInfo]$File, [string]$Path) {
    # Settings written beside the executable must remain private to the test run.
    $privateCopy = $File.IsReadOnly -or $File.Extension -in '.json','.cfg','.ini','.txt'
    if (!$privateCopy -and [IO.Path]::GetPathRoot($File.FullName) -ieq [IO.Path]::GetPathRoot($Path)) {
        New-ViewLink $Path $File.FullName
    } else {
        [IO.File]::Copy($File.FullName, $Path)
        if ($File.IsReadOnly) { ([IO.FileInfo]$Path).IsReadOnly = $false }
    }
}

$useJunctions = $true
function Add-ViewDirectory([IO.DirectoryInfo]$Folder, [string]$Path, [hashtable]$Omit) {
    if ($script:useJunctions -and !$Omit.ContainsKey($Folder.FullName)) {
        try { New-ViewLink $Path $Folder.FullName -Directory; return }
        catch {
            $script:useJunctions = $false
            Write-Report "Directory junction unavailable. Linking individual files: $($_.Exception.Message)"
        }
    }
    if ($Folder.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw "Cannot mirror a directory link without a junction: $($Folder.FullName)" }
    $null = [IO.Directory]::CreateDirectory($Path)
    foreach ($file in $Folder.GetFiles()) {
        if (!$Omit.ContainsKey($file.FullName)) { Add-File $file ([IO.Path]::Combine($Path,$file.Name)) }
    }
    foreach ($dir in $Folder.GetDirectories()) { Add-ViewDirectory $dir ([IO.Path]::Combine($Path,$dir.Name)) $Omit }
}

function Remove-TestView([IO.DirectoryInfo]$Folder, [switch]$MarkedRoot) {
    # Never recurse across a junction into the real installation or Workshop.
    if (!($Folder.Attributes -band [IO.FileAttributes]::ReparsePoint)) {
        foreach ($entry in $Folder.GetFileSystemInfos()) {
            if ($MarkedRoot -and $entry.Name -eq '.goodfriend-view-v1') { continue }
            if ($entry -is [IO.DirectoryInfo]) { Remove-TestView $entry } else { [IO.File]::Delete($entry.FullName) }
        }
    }
    if ($MarkedRoot) { [IO.File]::Delete([IO.Path]::Combine($Folder.FullName, '.goodfriend-view-v1')) }
    [IO.Directory]::Delete($Folder.FullName)
}

function Clear-IdleViews([IO.DirectoryInfo]$Base, [string]$HeldRole) {
    # Windows can keep an executable's hard links busy while a different player is running it.
    if (![IO.Directory]::Exists($Base.FullName) -or ($Base.Attributes -band [IO.FileAttributes]::ReparsePoint)) { return }
    foreach ($dir in $Base.GetDirectories('player-*')) {
        if ($dir.Name -notmatch '^player-(1|1000|2000|3000)(-[0-9a-f]{32})?$') { continue }
        $id = $Matches[1]
        $markerPath = [IO.Path]::Combine($dir.FullName, '.goodfriend-view-v1')
        if (($dir.Attributes -band [IO.FileAttributes]::ReparsePoint) -or ![IO.File]::Exists($markerPath)) { continue }
        $guard = $null
        try {
            if ($id -ne $HeldRole) {
                $path = Get-RoleLockPath $id
                try { $guard = [IO.File]::Open($path,'OpenOrCreate','ReadWrite','None') } catch [IO.IOException] { continue }
            }
            if (![IO.File]::Exists($markerPath)) { continue }
            $last = 0
            if (![int]::TryParse([IO.File]::ReadAllText($markerPath), [ref]$last)) { continue }
            $alive = if ($last -gt 0) { Get-Process -Id $last -ErrorAction SilentlyContinue }
            if ($alive -and $alive.Path -ieq [IO.Path]::Combine($dir.FullName,'SlayTheSpire2.exe')) {
                if ($id -eq $HeldRole) { throw "Player $id is already running." }
                continue
            }
            try { Remove-TestView $dir -MarkedRoot }
            catch { Write-Report "Cleanup deferred for $($dir.Name): $($_.Exception.Message)" }
        } finally { if ($guard) { $guard.Dispose() } }
    }
}

$view = $null
$base = [IO.DirectoryInfo][IO.Path]::Combine($game.FullName, '.GoodFriendLocalCoop')
$lease = $null
$ownsView = $false
$process = $null
$exitCode = 1
try {
    $lock = Get-RoleLockPath $role
    for ($attempt = 0; !$lease; $attempt++) {
        try { $lease = [IO.File]::Open($lock, 'OpenOrCreate', 'ReadWrite', 'None') }
        catch [IO.IOException] {
            if ($attempt -ge 15) { throw }
            [Threading.Thread]::Sleep(100)
        }
    }
    [IO.File]::WriteAllText($report, '')
    Clear-IdleViews $base $role
    $roots = @(Get-WorkshopRoots)
    $localMods = [IO.DirectoryInfo][IO.Path]::Combine($game.FullName,'mods')
    $seen = @{}
    $disabled = Get-DisabledWorkshopMods
    if ($roots.Count -and $localMods.Exists) {
        foreach ($m in (Get-ModManifests $localMods -LocalRoot)) { $seen[$m.Id] = $true }
    }
    $plans = @(foreach ($root in $roots) {
        Write-Report "Workshop cache: $root"
        foreach ($item in (([IO.DirectoryInfo]$root).GetDirectories() | Sort-Object Name)) {
            if ($item.Name -notmatch '^\d+$') { continue }
            $manifests = @(Get-ModManifests $item | Sort-Object { $_.File.FullName })
            $keep = @()
            $omit = @{}
            foreach ($m in $manifests) {
                $reason = $null
                if (!$m.Complete) { $reason = 'incomplete download' }
                elseif ($seen.ContainsKey($m.Id)) { $reason = 'another copy already present' }
                elseif ($disabled.ContainsKey($m.Id)) { $reason = 'disabled in Steam profile' }
                if ($reason) {
                    Write-Report "Skip $($m.Id) ($reason): $($m.File.FullName)"
                    $omit[$m.File.FullName] = $true
                    for ($d = $m.File.Directory; $null -ne $d; $d = $d.Parent) {
                        $omit[$d.FullName] = $true
                        if ($d.FullName -ieq $item.FullName) { break }
                    }
                } else {
                    $seen[$m.Id] = $true
                    $keep += $m
                    Write-Report "Include $($m.Id): $($m.File.FullName)"
                }
            }
            if (!$keep.Count) { continue }
            $folder = $item
            if ($manifests.Count -eq 1) {
                # Workshop uploads may wrap the actual mod in one or more otherwise empty directories.
                while ($folder.FullName -ine $keep[0].File.DirectoryName) {
                    $children = @($folder.GetFileSystemInfos())
                    if ($children.Count -ne 1 -or $children[0] -isnot [IO.DirectoryInfo]) { break }
                    $folder = $children[0]
                }
            }
            $name = 'Workshop-' + $item.Name
            if ($keep.Count -eq 1 -and $keep[0].Id -match '^[\w.-]+$' -and $keep[0].Id -notmatch '^(CON|PRN|AUX|NUL|COM[0-9]|LPT[0-9])(\.|$)' -and !$keep[0].Id.EndsWith('.')) { $name = $keep[0].Id }
            [pscustomobject]@{ Folder = $folder; Name = $name; Omit = $omit }
        }
    })

    $runDir = $game.FullName
    if ($plans.Count) {
        $base = [IO.Directory]::CreateDirectory($base.FullName)
        if ($base.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw "Unexpected link at $($base.FullName)" }
        $view = [IO.DirectoryInfo][IO.Path]::Combine($base.FullName, "player-$role")
        if ($view.Exists) {
            $view = [IO.DirectoryInfo]($view.FullName + '-' + [guid]::NewGuid().ToString('N'))
        }
        $marker = [IO.Path]::Combine($view.FullName, '.goodfriend-view-v1')
        $null = [IO.Directory]::CreateDirectory($view.FullName)
        [IO.File]::WriteAllText($marker,'0')
        $ownsView = $true
        $mods = [IO.Directory]::CreateDirectory([IO.Path]::Combine($view.FullName, 'mods'))
        foreach ($dir in $game.GetDirectories()) {
            if ($dir.Name -notin 'mods',$base.Name) { Add-ViewDirectory $dir ([IO.Path]::Combine($view.FullName,$dir.Name)) @{} }
        }
        foreach ($file in $game.GetFiles()) {
            if ($file.Extension -in '.exe','.dll','.pck','.cfg','.ini','.txt','.json','.gdextension','.dat','.bin') {
                Add-File $file ([IO.Path]::Combine($view.FullName,$file.Name))
            }
        }
        if ($localMods.Exists) {
            foreach ($entry in $localMods.GetFileSystemInfos()) {
                $dest = [IO.Path]::Combine($mods.FullName,$entry.Name)
                if ($entry -is [IO.DirectoryInfo]) { Add-ViewDirectory $entry $dest @{} } else { Add-File $entry $dest }
            }
        }
        foreach ($plan in $plans) {
            $dest = [IO.Path]::Combine($mods.FullName, $plan.Name)
            while ([IO.Directory]::Exists($dest) -or [IO.File]::Exists($dest)) { $dest += '-workshop' }
            Add-ViewDirectory $plan.Folder $dest $plan.Omit
        }
        $runDir = $view.FullName
    } else {
        Write-Report 'No additional cached mods selected. Using the installed game directly.'
    }

    Write-Report "Game directory: $runDir"
    $info = New-Object Diagnostics.ProcessStartInfo
    $info.FileName = [IO.Path]::Combine($runDir, 'SlayTheSpire2.exe')
    $info.WorkingDirectory = $runDir
    $info.Arguments = '--log-file "' + $env:gfLogFile + '" ' + $env:gfGameArgs
    $info.UseShellExecute = $false
    $process = [Diagnostics.Process]::Start($info)
    if ($ownsView) { [IO.File]::WriteAllText($marker, [string]$process.Id) }
    $process.WaitForExit()
    $exitCode = $process.ExitCode
} catch {
    Write-Report "ERROR: $($_.Exception.Message)"
} finally {
    if ($process) { $process.Dispose() }
    if ($lease) { $lease.Dispose() }
    if ($ownsView) {
        try { Clear-IdleViews $base '' } catch { Write-Report "Cleanup: $($_.Exception.Message)" }
    }
}
exit $exitCode
