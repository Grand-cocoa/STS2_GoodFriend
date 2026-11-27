# 好朋友 · Good Friend

《杀戮尖塔 2》的 2 至 4 人合作卡牌 Mod。19 张多人无色牌可以在爬塔中获得。每位玩家另有一张开局专属的《好多人啊》，每场战斗开始时自动变成一张带有保留的随机多人牌。多人商店在原有两个无色槽位旁增设第三个槽位，出售已解锁的原版和本 Mod 多人牌。

插画参考玩家自制卡《查你 DPS+》的短圆造型和奶白色外轮廓。《俺也一样》里，故障机器人学铁甲战士抱起双臂。《这锅谁背》用了热锅一样的红褐底，静默猎手一边后仰一边举手求饶。卡框、费用和牌面文字由游戏绘制。项目附简体中文和英文文本。

[查看 20 张卡面](docs/卡面一览.jpg) · [查看各牌费用、升级效果、结算与例子](docs/卡牌设计.md)

## 安装

1. 游戏 v0.114.0 对应《好朋友》0.1.2。每位玩家下载同一版 `GoodFriend-0.1.2.zip`。
2. 在 Steam 库中右击《杀戮尖塔 2》，选择「管理」→「浏览本地文件」。将压缩包里的整个 `GoodFriend` 文件夹解压到游戏目录下的 `mods` 文件夹里。
3. 确认 `mods/GoodFriend/` 内有 `GoodFriend.json`、`GoodFriend.dll` 和 `GoodFriend.pck` 三个文件。启动游戏，在模组列表中启用「好朋友」，重新启动后创建多人新局。

## 一个人开多人测试局（Windows）

**房主已经用新版启动器进入选角大厅**时，双击游戏目录里的 `Local-Coop-Player.cmd` 即可单独打开玩家 2（ID 为 1000）。在新窗口点「多人游戏」→「加入游戏」。

**从零开始双开或四开**时，按以下步骤操作。

1. 退出当前房主和队友的游戏窗口。将 `GoodFriend-0.1.2-Windows.zip` 解压到 `SlayTheSpire2.exe` 所在目录。已安装《好朋友》0.1.2 的玩家只需将 `GoodFriend-0.1.2-Windows-Launchers.zip` 中的两个 `.cmd` 和一个 `.ps1` 全部解压到这里，覆盖旧脚本。
2. 双击 `Local-Coop-Test.cmd`，按 **2** 或 **4**。启动器会一次打开所选数量的游戏窗口。各玩家独立写日志，开窗后即可在游戏里操作。
3. 房主从「多人游戏」创建标准房间。等房主进入选角大厅后，其他窗口依次点「多人游戏」→「加入游戏」。每个窗口选角色并准备，用 Alt+Tab 切换操作。如果某个窗口首次提示启用模组，按游戏提示确认。需要单独重开某位队友时，在游戏目录运行 `Local-Coop-Player.cmd 1000`、`Local-Coop-Player.cmd 2000` 或 `Local-Coop-Player.cmd 3000`，分别对应玩家 2、3、4。

各窗口通过游戏自带的本机联机模式，使用独立玩家 ID，读取同一份本地 Mod。处理完首次启动提示后，可从游戏目录运行 `Local-Coop-Test.cmd /fast`。房主开启本机联机端口后，队友会自动加入。等待 30 秒仍未检测到端口时，队友会先进主菜单，方便你手动点「加入游戏」。[本地联机启动参数](https://tutorials.sts2modding.com/docs/08-launch-arguments/)

### 使用已下载的创意工坊模组

等 Steam 下载完所需模组和前置依赖，再用启动器开游戏。启动器会查找游戏所在 Steam 库及 Steam 登记的其他库，读取各库的 `steamapps\workshop\content\2868840`。每个窗口同时看到游戏 `mods` 里的模组和已下载的创意工坊模组。同一模组 ID 优先使用手动安装的版本。能识别当前 Steam 账号配置时，启动器会跳过该账号在游戏内已禁用的创意工坊模组。各测试玩家也可在主菜单「模组」中查看自己的启用状态。订阅或开关有变动时，重开所有测试窗口。

在资源管理器地址栏粘贴 `%LOCALAPPDATA%\GoodFriend\LocalCoopLogs`。打开 `host.workshop.log` 或 `client-1000.workshop.log` 查看对应窗口的扫描结果。`Include` 表示该模组已提供给游戏，`Skip` 后面会写明跳过原因。若游戏拒绝加载某个模组，可在同目录的 `host.log` 或 `client-1000.log` 中搜索它的 ID。

临时测试目录使用 Windows 目录联接和硬链接，建议将游戏安装在 NTFS 盘上。普通 Steam 启动仍然使用原有模组目录。若 Steam 库位置无法自动识别，可在游戏目录的命令提示符中指定实际的工坊缓存目录后启动。

```bat
set "GOODFRIEND_WORKSHOP_DIR=D:\SteamLibrary\steamapps\workshop\content\2868840"
Local-Coop-Test.cmd
```

如果画面停在「获取创意工坊 Mod」，在资源管理器地址栏粘贴 `%LOCALAPPDATA%\GoodFriend\LocalCoopLogs`。房主日志是 `host.log`，第一位队友是 `client-1000.log`。搜索 `[GoodFriend] 0.1.2 initialized` 可确认新版已成功初始化。脚本启动的窗口会在主菜单就绪时通过《不再干等》自身的接口收起离线残留的启动画面。若首次启用《好朋友》前该画面就挡住了确认提示，可从 Steam 正常启动一次，在模组菜单暂时关闭《不再干等》，重启后再运行测试脚本。[《不再干等》的启动画面说明](https://github.com/Somiona/sts2-its-loading#readme)

战斗中按 Esc 下方的反引号键打开控制台。输入 `card GF_HOT_POT hand` 可把《这锅谁背》放进当前玩家手中，`card GF_INSURANCE hand` 可拿到《意外死亡》。每条命令只需在想拿牌的那个窗口执行一次。[控制台指令](https://tutorials.sts2modding.com/docs/09-console-commands/)

## 合作局里的几个例子

- 《这锅谁背》可以在打出任意牌后传回上一位玩家。一口锅传了 4 次，基础版对持锅者造成 13 点伤害、对每个敌人造成 20 点，升级版分别是 9 点和 16 点。持锅者可以提前叠格挡。
- 《意外死亡》在死亡时清空能量。受保玩家在下一个玩家回合以 1 点生命复活，清除强化，在本回合正常能量上再加 1 点。默认起手为 4 点。团灭时若仍有待理赔的保单，受保者在下一个玩家回合一起复活。
- 《收破烂喽》打出时给你 5 点格挡。四张《伤口》和一张本来就有虚无的《晕眩》都在回合末因虚无而消耗，你会再得 15 点格挡。

## 从源码构建

使用 .NET 9 SDK、Python 3 和 Godot 4.5.1。将 `dotnet` 和 `godot` 放入 PATH，或通过 `--dotnet` 和 `--godot` 指定可执行文件。

```sh
dotnet run --project tests/Rules.Tests.csproj -c Release
python tools/package.py
```

打包脚本默认通过 NuGet 获取游戏 0.114.0 的编译参考程序集。若要按本机安装的版本编译，将含有 `sts2.dll`、`GodotSharp.dll`、`0Harmony.dll` 的目录传给 `--game-data`。

```sh
python tools/package.py --game-data "/path/to/game/data_sts2_windows_x86_64"
```

升级游戏版本时，用相应的程序集检查 Harmony 需要的私有方法、重载和注入参数。第二个参数也可以指向同版本编译参考包里的 `sts2.dll`。

```sh
dotnet run --project tests/Patches/PatchTargets.Tests.csproj -c Release -- bin/Release/net9.0/GoodFriend.dll "/path/to/game/data_sts2_windows_x86_64/sts2.dll"
```

安装包和含启动脚本的 Windows 测试包都位于 `dist/`。脚本会在一个空 Godot 项目里挂载生成的 PCK，逐项加载卡面、状态图标和本地化文件。规则测试覆盖传锅、团灭保单和虚无回收。Windows 游戏内的双人、四人联机仍待实机验证。

在 Windows PowerShell 5.1 中运行以下命令可单独验证工坊模组启动流程。测试会创建临时 Steam 库和两个测试进程，检查两个玩家看到的模组、前置文件、重复 ID 及退出后的目录清理。

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File tests\LocalCoopWorkshop.Tests.ps1
```
