#!/bin/bash

export GDK_BACKEND=x11

# MateEngine is an X11 application. Under any Wayland session (GNOME, KDE, Hyprland, ...)
# it has to run through XWayland to get a transparent ARGB window.
# XDG_SESSION_TYPE is left untouched on purpose: the app reads it to detect Wayland.
if [ -n "$WAYLAND_DISPLAY" ] || [ "$XDG_SESSION_TYPE" = "wayland" ]; then
  echo "Wayland session detected (${XDG_CURRENT_DESKTOP:-unknown desktop}), using XWayland"
  export XDG_BACKEND=x11
  export SDL_VIDEODRIVER=x11
  export DISPLAY="${DISPLAY:-:0}"
else
  echo "X11 session detected (${XDG_CURRENT_DESKTOP:-unknown desktop})"
fi

visual_id=$(glxinfo 2>/dev/null | grep -i "32 tc  0  32  0 r  y .   8  8  8  8 .  .   0 24  8" | head -n1 | awk '{print $1}')

if [ -z "$visual_id" ]; then
    visual_id=$(glxinfo 2>/dev/null | grep -i "32 tc  0  32  0 r  y" | head -n1 | awk '{print $1}')
fi

if [ -z "$visual_id" ]; then
    visual_id=$(xdpyinfo 2>/dev/null | grep -A 2 "visual id" | grep -B 5 "depth:.* .*32 planes" | grep "visual id" | awk '{print $3}' | head -n1)
fi

echo "Visual ARGB: $visual_id"

echo "\`\`\`"

export SDL_VIDEO_X11_VISUALID=$visual_id

SCRIPT_DIR="$(dirname "$(realpath "${BASH_SOURCE[0]}")")"
if [ -f "$SCRIPT_DIR/MateEngineX.$(uname -m)" ]; then
    TARGET_DIR="$SCRIPT_DIR"
elif [ -f "$SCRIPT_DIR/BuildOutput/MateEngineX.$(uname -m)" ]; then
    TARGET_DIR="$SCRIPT_DIR/BuildOutput"
else
    TARGET_DIR="$SCRIPT_DIR"
fi

cd "$TARGET_DIR" || exit 1
SDL_VIDEO_X11_VISUALID=$visual_id "./MateEngineX.$(uname -m)" "$@" | grep -vF --line-buffered "[Vulkan init] extensions: "

echo "\`\`\`"
