# 🌐 Language / 语言选择
- [English](#English)
- [中文](#中文)

---

## English

# Mate-Engine-Linux-Port
This is an **unofficial** Linux port of shinyflvre's MateEngine - A free Desktop Mate alternative with a lightweight interface and custom VRM support.
Tested on Ubuntu 24.04 LTS.

![](https://i.ibb.co/B5MWsHqx/Screenshot.png)

#### Usage
Open the project in Unity 6000.2.6f2 and build the player, or simply grab a prebuilt one in [Releases](https://github.com/Marksonthegamer/Mate-Engine-Linux-Port/releases/) page. Then, run the `launch.sh` script in the output directory (This script is necessary for window transparency. For KDE, you also need to **disable "Allow applications to block compositing"** in `systemsettings`).

#### Requirements
- A common GNU/Linux distro
- A common X11 desktop environment which supports compositing (such as KDE, Xfce, GNOME, etc.)
- `libpulse-dev` and `pipewire-pulse` (if you are using Pipewire as audio server)
- `libgtk-3-dev libglib2.0-dev libappindicator3-dev`
- `libx11-dev libxext-dev libxrender-dev libxdamage-dev`

On Ubuntu and other Debian-based Linux:
```bash
sudo apt install libpulse-dev libgtk-3-dev libglib2.0-dev libappindicator3-dev libx11-dev libxext-dev libxrender-dev libxdamage-dev
```
On Fedora:
```bash
sudo dnf install pulseaudio-libs-devel gtk3-devel glib2-devel libX11-devel libXext-devel libXrender-devel libXdamage-devel libappindicator-gtk3
```
On Arch Linux:
```bash
sudo pacman -S libpulse gtk3 glib2 libx11 libxext libxrender libxdamage libappindicator-gtk3
```

#### Ported Features
- Model visuals, alarm, screensaver, Chibi mode (they always work, any external libraries are not required for them)
- Transparent background with cutoff (X11 only)
- Set window always on top
- Dancing (PulseAudio or Pipewire-Pulse for audio program detection)
- AI Chat (require `Meta-Llama-3.1-8B-Instruct-Q4_K_M.gguf`)
- Mouse tracking (hand holding and eyes tracking)
- Borderless window

#### Known Issues
- Window snapping and dock sitting are still kind of buggy
- Crashes at low system performance (`pa_mainloop_iterate`)
- Large RAM usage (terrible 500 MiB) for Zome model
- Limited window moving in KWin (KDE)
- **Window cutoff is not supported by XWayland**
- Delayed system tray menu update
- PulseAudio sometimes returns an empty audio program name

#### Unconfirmed
- Discord RPC
- Custom VRM importing

This project lacks further testing and updates. Feel free to make PRs to contribute!

---

## 中文

# Mate-Engine-Linux-Port
这是一个非官方的MateEngine Linux移植版 - 一个免费的Desktop Mate替代品（桌宠软件），具有轻量级界面和自定义VRM支持。
已在Ubuntu 24.04 LTS上测试。

![](https://i.ibb.co/B5MWsHqx/Screenshot.png)

#### 用法
使用 Unity 6000.2.6f2 打开此项目然后构建Player，或者在[Releases](https://github.com/Marksonthegamer/Mate-Engine-Linux-Port/releases/)页面获取预构建版本。必须运行输出目录中的`launch.sh`，否则 MateEngne 将缺少透明窗口背景（对于 KDE Plasma 桌面环境，你还需要在 KDE 系统设置中禁用“允许应用程序阻止显示特效合成”）。

#### Requirements
- 一个常见的 GNU/Linux 发行版
- 一个常见的 X11 桌面环境，支持显示特效合成（compositing） ，比如KDE，Xfce，GNOME等
- `libpulse-dev` 和 `pipewire-pulse` (如果你在用 Pipewire 作为音频服务器)
- `libgtk-3-dev libglib2.0-dev libappindicator3-dev`
- `libx11-dev libxext-dev libxrender-dev libxdamage-dev`

以下命令适用于 Ubuntu 和别的基于 Debian 的 Linux:
```bash
sudo apt install libpulse-dev libgtk-3-dev libglib2.0-dev libappindicator3-dev libx11-dev libxext-dev libxrender-dev libxdamage-dev
```
以下命令适用于 Fedora:
```bash
sudo dnf install pulseaudio-libs-devel gtk3-devel glib2-devel libX11-devel libXext-devel libXrender-devel libXdamage-devel libappindicator-gtk3
```
以下命令适用于 Arch Linux:
```bash
sudo pacman -S libpulse gtk3 glib2 libx11 libxext libxrender libxdamage libappindicator-gtk3
```

#### 移植的功能
- 模型视觉效果、闹钟、屏保、Q版模式（它不需要任何外部库，因此始终工作）
- 带 Cutoff 的透明背景（仅限X11）
- 窗口置顶
- 跳舞（PulseAudio或Pipewire-Pulse用于音频程序检测）
- AI聊天（需要`Meta-Llama-3.1-8B-Instruct-Q4_K_M.gguf`）
- 鼠标跟踪（手持和眼睛跟踪）
- 无边框窗口

#### 已知问题
- 坐在窗口和程序坞上仍然有点bug
- 系统性能较低时崩溃（`pa_mainloop_iterate`）
- Zome模型的RAM使用量大（可怕的500 MiB）
- KWin（KDE）中窗口的移动范围有限
- **XWayland不支持窗口 Cutoff**
- 系统托盘菜单更新延迟
- PulseAudio有时会返回空的音频程序名称

#### 未验证
- Discord RPC
- 自定义 VRM 模型导入

该项目缺乏进一步的测试和更新。请随时通过Pull Requests来贡献！
