# Mate-Engine-Linux-Port
**Unofficial** Linux port of shinyflvre's MateEngine - A free Desktop Mate alternative with a lightweight interface and custom VRM support.

#### Requirements
- A common X11 desktop environment (KDE, Xfce, GNOME, etc.)
- `libpulse-dev` and `pipewire-pulse` (if you are using Pipewire)
- `libgtk-3-dev libglib2.0-dev libappindicator3-dev`
- `libx11-dev libxext-dev libxrender-dev libxdamage-dev`

#### Successfully Ported Features
- Model visuals, alarm, screensaver (they always work, any external libraries are not required for them)
- Transparent background with cutoff (X11 only)
- Dancing (PulseAudio or Pipewire-Pulse for audio program detection)
- AI Chat (require `Meta-Llama-3.1-8B-Instruct-Q4_K_M.gguf`)

#### Known Issues
- Window snapping and dock sitting are still kind of buggy
- Crashes at low system performance (`pa_mainloop_iterate`)
- Large RAM usage (terrible 500 MiB) for Zome model
- Lack of further testing and updates - original MateEngine is in **super** active development
- Unremovable window border (Xfce)
- Limited window moving in KWin (KDE)
- **Window cutoff is not supported by XWayland**

#### Unconfirmed
- Discord RPC
- Custom VRM importing
