# 操作手册（RUNBOOK）

> 所有“怎么跑”的命令都放这里，避免每次让 AI 猜路径。

---

## 1. 路径与版本

| 项 | 值 |
|---|---|
| 项目根 | `D:\AI_dungen` |
| 游戏 | `D:\steam\steamapps\common\Enter the Gungeon` |
| 游戏版本 | 2.1.9（Steam build 3829397） |
| Unity | 2017.4.27f1，Mono x64 |
| BepInEx | 5.4.21.0 x64 |
| 插件 | `game/EtgGym/`（net35） |
| 部署目标 | `<游戏>\BepInEx\plugins\EtgGym\EtgGym.dll` |
| 日志 | `<游戏>\BepInEx\LogOutput.log` |
| Python | `.venv\Scripts\python.exe`（3.12.16） |
| PyTorch | 2.11.0+cu128 |
| SB3 / Gymnasium | 2.9.2 / 1.4.0 |
| 存档备份 | `backup/saves_20261108_101346.zip` |

---

## 2. 构建并部署插件

先关闭游戏（DLL 被占用会失败）。

```powershell
cd D:\AI_dungen
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\build_deploy.ps1
```

只构建、不部署：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\build_deploy.ps1 -NoDeploy
```

指定其他游戏目录：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\build_deploy.ps1 -GameDir "D:\path\to\game"
```

---

## 3. 启动游戏

- **直接从 Steam 启动**，不要走 Thunderstore Mod Manager（避免 profile 与根目录双份 BepInEx）。
- 不要同时开 BepInEx 控制台（会抢焦点导致暂停）。
- 训练时保持游戏前台。

---

## 4. 看日志

```powershell
$log = "D:\steam\steamapps\common\Enter the Gungeon\BepInEx\LogOutput.log"
Get-Content $log -Tail 50
```

只看关键行（排除刷屏）：

```powershell
Get-Content $log | Select-String -CaseSensitive -Pattern '\[E[0-9]|\[S1|\[S0\]|\[reset\]|Exception|\[Error' | ForEach-Object { $_.Line } | Select-Object -Last 60
```

确认插件加载：

```powershell
Select-String -Path $log -Pattern 'EtgGym 0\.1\.0 loaded|Harmony lifecycle'
```

---

## 5. Python 环境

系统默认 Python 仍是 3.14；**所有命令都显式用 venv**。

```powershell
cd D:\AI_dungen
.\.venv\Scripts\python.exe --version
.\.venv\Scripts\python.exe .\scripts\check_env.py
```

激活（可选）：

```powershell
.\.venv\Scripts\Activate.ps1
```

被执行策略拦截时（只影响当前窗口）：

```powershell
Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass
.\.venv\Scripts\Activate.ps1
```

`uv` 已写入用户 PATH，但当前终端可能看不到；新开 PowerShell 即可，或刷新：

```powershell
$env:Path = [System.Environment]::GetEnvironmentVariable("Path","Machine") + ";" + [System.Environment]::GetEnvironmentVariable("Path","User")
```

---

## 6. 学习样例

CartPole：

```powershell
.\.venv\Scripts\python.exe -c "import gymnasium as gym; from stable_baselines3 import PPO; m=PPO('MlpPolicy', gym.make('CartPole-v1'), verbose=1, device='cpu'); m.learn(50000)"
```

LunarLander：

```powershell
.\.venv\Scripts\python.exe -c "import gymnasium as gym; from stable_baselines3 import PPO; m=PPO('MlpPolicy', gym.make('LunarLander-v3'), verbose=1, device='cpu'); m.learn(300000)"
```

TensorBoard：

```powershell
.\.venv\Scripts\tensorboard.exe --logdir tb
```

> 纯 MLP 用 CPU；CNN/栅格图训练再用 GPU。

---

## 7. 存档

- 备份：`backup/saves_20261108_101346.zip`
- 存档目录：`%USERPROFILE%\AppData\LocalLow\Dodge Roll\Enter the Gungeon`
- 恢复前先关闭游戏，再解压覆盖。
- **训练禁用 `QuickRestart`**（可能写坏存档）。

---

## 8. Git

```powershell
cd D:\AI_dungen
git status
git add -A
git commit -m "docs: add project memory files"
```

不要提交：游戏 DLL、`.venv/`、`decompiled/`、`backup/`（`.gitignore` 已覆盖）。

---

## 9. 排障

| 现象 | 排查 |
|---|---|
| 游戏启动报错/闪退 | 检查 `winhttp.dll`、`doorstop_config.ini`、`BepInEx\core\BepInEx.Preloader.dll` 是否齐全；是否同时存在 `version.dll` |
| 日志没有 `EtgGym loaded` | DLL 是否在 `BepInEx\plugins\EtgGym\`；游戏是否用 TMM 启动 |
| 角色不动 | 是否失焦暂停（`Time.timeScale=0`）；是否在 `Preload`/加载场景 |
| 按键没反应 | 先点一下游戏窗口；F12 被 Steam 截图占用 |
| 训练中突然停住 | 看门狗未启用；`PreventPausing` 未设置 |
| 存档异常 | 恢复 `backup/saves_20261108_101346.zip`；不要再调用 `QuickRestart` |
| `XInputInterface32.dll` 报错 | ETG 自带 x86 插件，**无害** |
| 插件编译报缺 DLL | 用 `-p:GameDir=...` 指向正确游戏目录 |

---

## 10. 一键自检

```powershell
cd D:\AI_dungen
.\.venv\Scripts\python.exe .\scripts\check_env.py
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\build_deploy.ps1 -NoDeploy
Test-Path "D:\steam\steamapps\common\Enter the Gungeon\BepInEx\plugins\EtgGym\EtgGym.dll"
```
