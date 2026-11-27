@echo off
setlocal EnableExtensions DisableDelayedExpansion
if /i "%~1"=="/run-game" goto :game
chcp 65001 >nul
pushd "%~dp0" || exit /b 1
if not exist "SlayTheSpire2.exe" (
    echo 请把本脚本放在 SlayTheSpire2.exe 旁边后再启动。
    goto :failed
)
for %%F in (GoodFriend.json GoodFriend.dll GoodFriend.pck) do if not exist "mods\GoodFriend\%%F" (
    echo 缺少 "mods\GoodFriend\%%F"。请将完整测试包解压到游戏目录。
    goto :failed
)

set "gfPlayerId=1000"
if not "%~1"=="" if /i not "%~1"=="/fast" (
    set "gfPlayerId="
    for %%I in (1 1000 2000 3000) do if "%%I"=="%~1" set "gfPlayerId=%%I"
)
if not defined gfPlayerId (
    echo 可用的玩家 ID 为 1、1000、2000、3000。双击此文件可单独启动玩家 2。
    goto :failed
)
set "gfLogDir=%LOCALAPPDATA%\GoodFriend\LocalCoopLogs"
if not exist "%gfLogDir%\" mkdir "%gfLogDir%"
if not exist "%gfLogDir%\" (
    echo 无法创建日志目录 "%gfLogDir%"。
    goto :failed
)
set "gfLogFile=%gfLogDir%\client-%gfPlayerId%.log"
if "%gfPlayerId%"=="1" set "gfLogFile=%gfLogDir%\host.log"
set "gfAction="
if /i "%~1"=="/fast" set "gfAction=--fastmp=join"
if /i "%~2"=="/fast" set "gfAction=--fastmp=join"
if defined gfAction if "%gfPlayerId%"=="1" set "gfAction=--fastmp=host_standard"

set "gfGameArgs=--windowed --force-steam=off --clientId=%gfPlayerId% --goodfriend-local-coop %gfAction%"

rem Launch the game from its own console. Godot can attach to its parent's console on Windows.
ver >nul
start "GoodFriend %gfPlayerId%" /min "%ComSpec%" /d /c Local-Coop-Player.cmd /run-game || goto :failed
popd
exit /b 0

:game
if exist "Local-Coop-Workshop.ps1" goto :workshop
".\SlayTheSpire2.exe" --log-file "%gfLogFile%" %gfGameArgs% <nul >nul 2>&1
exit /b %errorlevel%

:workshop
"%SystemRoot%\System32\WindowsPowerShell\v1.0\powershell.exe" -NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass -File ".\Local-Coop-Workshop.ps1" <nul >nul 2>&1
if not errorlevel 1 exit /b 0
chcp 65001 >nul
echo 玩家 %gfPlayerId% 的游戏未能正常运行。可在以下目录查看游戏和创意工坊日志：
echo "%gfLogDir%"
pause
exit /b 1

:failed
if "%~1"=="" pause
popd
exit /b 1
