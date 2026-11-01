#!/bin/bash

export GDK_BACKEND=x11

if [[ $XDG_SESSION_DESKTOP == "Hyprland"* || $XDG_SESSION_TYPE == "wayland" || -n $WAYLAND_DISPLAY ]]; then
  export XDG_BACKEND=x11
  export SDL_VIDEODRIVER=x11 
else
  echo "Unknown windowmanager"
fi

if [[ ($XDG_SESSION_TYPE == "wayland" || -n "${WAYLAND_DISPLAY:-}") && ("$XDG_CURRENT_DESKTOP" == *"GNOME"* || "$XDG_CURRENT_DESKTOP" == *"ubuntu"*) ]]; then
  script_dir="$(dirname "$(realpath "${BASH_SOURCE[0]}")")"
  compat_so="$script_dir/libmate_gnome_compat.so"
  if [ ! -f "$compat_so" ] && [ -f "$script_dir/Plugins/wayland_compat/mate_gnome_compat.c" ] && command -v gcc >/dev/null 2>&1; then
      gcc -shared -fPIC -O2 -o "$compat_so.tmp" "$script_dir/Plugins/wayland_compat/mate_gnome_compat.c" -ldl -lX11 -lXext 2>/dev/null && mv "$compat_so.tmp" "$compat_so" || rm -f "$compat_so.tmp"
  fi
  if [ -f "$compat_so" ]; then
      export LD_PRELOAD="${compat_so}${LD_PRELOAD:+:$LD_PRELOAD}"
  fi
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
