@echo off
setlocal EnableExtensions DisableDelayedExpansion
chcp 65001 >nul
title 好朋友 - 本机联机测试
pushd "%~dp0" || exit /b 1
if not exist "Local-Coop-Player.cmd" (
    echo 请把测试包中的启动脚本一起放在 SlayTheSpire2.exe 旁边。
    goto :failed
)
set "quickJoin="
if /i "%~1"=="/fast" set "quickJoin=/fast"
set "peerIds=1000"
choice /c 24 /n /m "选择同时打开的游戏窗口数 [2] 双人  [4] 四人 "
if errorlevel 3 goto :failed
if not errorlevel 1 goto :failed
if errorlevel 2 set "peerIds=1000 2000 3000"

echo 房主在「多人游戏」创建标准房间。等房主进入选角大厅后，其他窗口再点「加入游戏」。
call Local-Coop-Player.cmd 1 %quickJoin% || goto :failed
if defined quickJoin call :waitForHost
for %%I in (%peerIds%) do (
    call Local-Coop-Player.cmd %%I %quickJoin% || goto :failed
)
popd
exit /b 0

:waitForHost
setlocal EnableDelayedExpansion
echo 正在等待本机房主开启联机端口……
for /l %%S in (1,1,30) do (
    for /f "tokens=1,2" %%P in ('netstat -an 2^>nul') do if /i "%%P"=="UDP" (
        set "endpoint=%%Q"
        if "!endpoint:~-6!"==":33771" goto :hostReady
    )
    timeout /t 1 /nobreak >nul
)
endlocal
echo 房主还在启动，将先打开队友的主菜单。房主进入选角大厅后，请在队友窗口点「加入游戏」。
set "quickJoin="
exit /b 0

:hostReady
endlocal
exit /b 0

:failed
pause
popd
exit /b 1
