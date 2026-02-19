#!/bin/bash

export GDK_BACKEND=x11

if [[ $XDG_SESSION_DESKTOP == "Hyprland"* ]]; then
  echo "Hyprland detected"
  # hyprland seems to need these variables as well to create a transparent xwayland window
  export XDG_BACKEND=x11
  export SDL_VIDEODRIVER=x11 
else
  echo "Unknown windowmanager"
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

SDL_VIDEO_X11_VISUALID=$visual_id "$(dirname "$(realpath "${BASH_SOURCE[0]}")")/MateEngineX.$(uname -m)" "$@" | grep -v "[Vulkan init] extensions: "

echo "\`\`\`"
