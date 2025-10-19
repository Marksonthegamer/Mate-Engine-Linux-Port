# Mate-Engine-Linux-Port
**Unofficial** Linux port of shinyflvre's MateEngine - A free Desktop Mate alternative with a lightweight interface and custom VRM support.
Tested on Ubuntu 24.04 LTS.

#### Usage
Run the `launch.sh` script.
For KDE, you need to disable "Allow applications to block compositing" in `systemsettings`

#### Requirements
- A common X11 desktop environment which supports compositing (KDE, Xfce, GNOME, etc.)
- `libpulse-dev` and `pipewire-pulse` (if you are using Pipewire)
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
- Delayed tray icon update
- PulseAudio sometimes returns an empty audio program name

#### Unconfirmed
- Discord RPC
- Custom VRM importing

This project lacks further testing and updates. Feel free to make PRs to contribute!
