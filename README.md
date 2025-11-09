# 🌐 Language / 语言选择
- [English](#English)
- [中文](#中文)

---

## English

# Mate-Engine-Linux-Port
This is an **unofficial** Linux port of shinyflvre's MateEngine - A free Desktop Mate alternative with a lightweight interface and custom VRM support.
Tested on Ubuntu 24.04 LTS.

![](https://raw.githubusercontent.com/Marksonthegamer/Mate-Engine-Linux-Port/refs/heads/main/Screenshot.png)

### Usage
Open the project in Unity 6000.2.6f2 and build the player with executable name "MateEngineX.x86_64", or simply grab a prebuilt one in [Releases](https://github.com/Marksonthegamer/Mate-Engine-Linux-Port/releases/) page. Then, run the `launch.sh` script in the output directory (This script is necessary for window transparency. For KDE, you also need to **disable "Allow applications to block compositing"** in `systemsettings`).

### Requirements
- A common GNU/Linux distro
- A common X11 desktop environment which supports compositing (such as KDE, Xfce, GNOME, etc.)
- `libpulse` and `pipewire-pulse` (if you are using Pipewire as audio server)
- `libgtk-3-dev libglib2.0-dev libappindicator3-dev`
- `libx11-6 libxext6 libxrender1 libxdamage2`

On Ubuntu and other Debian-based Linux:
```bash
sudo apt install libpulse-dev libgtk-3-dev libglib2.0-dev libappindicator3-dev libx11-6 libxext6 libxrender1 libxdamage2
```
On Fedora:
```bash
sudo dnf install pulseaudio-libs gtk3 glib2 libX11 libXext libXrender-devel libXdamage libappindicator-gtk3
```
On Arch Linux:
```bash
sudo pacman -S libpulse gtk3 glib2 libx11 libxext libxrender libxdamage libappindicator-gtk3
```

Note that if you use GNOME, you will need [AppIndicator and KStatusNotifierItem Support extension](https://extensions.gnome.org/extension/615/appindicator-support/) to show tray icon.

### Ported Features & Highlights
- Model visuals, alarm, screensaver, Chibi mode (they always work, any external libraries are not required for them)
- Transparent background with cutoff
- Set window always on top
- Dancing (PulseAudio or Pipewire-Pulse for audio program detection)
- AI Chat (require `Meta-Llama-3.1-8B-Instruct-Q4_K_M.gguf`)
- Mouse tracking (hand holding and eyes tracking)
- Discord RPC
- Custom VRM importing
- Simplified Chinese localization
- Lower RAM usage than Windows version (Release 2.2.5)

![](https://raw.githubusercontent.com/Marksonthegamer/Mate-Engine-Linux-Port/refs/heads/main/RAMComparition.png)

### Known Issues
- Window snapping and dock sitting are still kind of buggy, and they don't work on XWayland Interface
- Crashes at low system performance (`pa_mainloop_iterate`)
- Limited window moving in KWin (KDE, GNOME)
- PulseAudio sometimes returns an empty audio program name

### Removed
- Steam API (no workshop support)
- NAudio
- UniWindowController

This project lacks further testing and updates. Feel free to make PRs to contribute!

---

## 中文

# Mate-Engine-Linux-Port
这是一个非官方的MateEngine Linux移植版 - 一个免费的Desktop Mate替代品（桌宠软件），具有轻量级界面和自定义VRM支持。
已在Ubuntu 24.04 LTS上测试。

![](https://raw.githubusercontent.com/Marksonthegamer/Mate-Engine-Linux-Port/refs/heads/main/Screenshot.png)

### 用法
使用 Unity 6000.2.6f2 打开此项目然后构建Player，可执行文件名为"MateEngineX.x86_64"，或者在[Releases](https://github.com/Marksonthegamer/Mate-Engine-Linux-Port/releases/)页面获取预构建版本。必须运行输出目录中的`launch.sh`，否则 MateEngne 将缺少透明窗口背景（对于 KDE Plasma 桌面环境，你还需要在 KDE 系统设置中禁用“允许应用程序阻止显示特效合成”）。

### 系统要求
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

如果你使用 GNOME 桌面环境，你还需要安装 [AppIndicator and KStatusNotifierItem Support extension](https://extensions.gnome.org/extension/615/appindicator-support/) 以显示托盘图标。

### 移植的功能与亮点
- 模型视觉效果、闹钟、屏保、Q版模式（它们不需要任何外部库，因此始终工作）
- 带 Cutoff 的透明背景
- 窗口置顶
- 跳舞（PulseAudio或Pipewire-Pulse用于音频程序检测）
- AI聊天（需要`Meta-Llama-3.1-8B-Instruct-Q4_K_M.gguf`）
- 鼠标跟踪（手持和眼睛跟踪）
- Discord RPC
- 自定义 VRM 模型导入
- 简体中文版汉化
- 与 Windows 版相比，使用更少内存（Release 2.2.5）

![](https://raw.githubusercontent.com/Marksonthegamer/Mate-Engine-Linux-Port/refs/heads/main/RAMComparition.png)

### 已知问题
- 坐在窗口和程序坞上仍然有点bug
- 系统性能较低时崩溃（`pa_mainloop_iterate`）
- KWin（KDE）和GNOME中窗口的移动范围有限
- PulseAudio有时会返回空的音频程序名称

### 已删除
- Steam API (无创意工坊支持)
- NAudio
- UniWindowController

该项目缺乏进一步的测试和更新。请随时通过Pull Requests来贡献！
