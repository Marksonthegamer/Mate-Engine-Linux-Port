#!/bin/bash
if ! which "glxinfo" > /dev/null 2>&1; then
    echo "Error: glxinfo is not installed." >&2
    exit 1
fi
output=$(glxinfo | grep -i "32 tc  0  32  0 r  y .   8  8  8  8 .  s  0 24  8  0  0  0  0  0 0 .  None" 2>/dev/null)
if [ -z "$output" ]; then
    echo "Error: Cannot find the specific 32-bit visual. Make sure your graphic drivers are installed correctly. If no luck, check the following link for details about how to find an ARGB visual: " >&2
    echo "https://discussions.unity.com/t/transparent-background-on-linux-xwayland-kwin-gameobject-visible-but-alpha-ignored/1667473/4" >&2
    exit 2
fi
visual_id=$(echo "$output" | head -n 1 | awk '{print $1}')
SDL_VIDEO_X11_VISUALID=$visual_id $(dirname "$(realpath "${BASH_SOURCE[0]}")")/MateEngineX.$(uname -m) "$@"
