#!/usr/bin/env bash

export GDK_BACKEND=x11

visual_id=$(glxinfo 2>/dev/null | grep -i "32 tc  0  32  0 r  y .   8  8  8  8 .  .   0 24  8" | head -n1 | awk '{print $1}')

if [ -z "$visual_id" ]; then
    visual_id=$(glxinfo 2>/dev/null | grep -i "32 tc  0  32  0 r  y" | head -n1 | awk '{print $1}')
fi

if [ -z "$visual_id" ]; then
    visual_id=$(glxinfo -v 2>/dev/null | grep "Visual ID" | grep "depth=32" | awk '{print $3}' | head -n1)
fi

echo "Visual ARGB: $visual_id"

export SDL_VIDEO_X11_VISUALID=$visual_id

SDL_VIDEO_X11_VISUALID=$visual_id "$(dirname "$(realpath "${BASH_SOURCE[0]}")")/MateEngineX.$(uname -m)" "$@"
