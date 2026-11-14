#!/bin/bash

# --- Configuration ---
DEST="/opt/MateEngineX"
DESKTOP_FILE="/usr/share/applications/mateengine.desktop"
ICON_FILE="/usr/share/pixmaps/mateengine.png"

# --- Uninstall Logic ---
if [[ "$1" == "--uninstall" ]]; then
    echo "Uninstalling MateEngine..."

    [ -d "$DEST" ] && sudo rm -rf "$DEST" && echo "Removed $DEST"
    [ -f "$DESKTOP_FILE" ] && sudo rm "$DESKTOP_FILE" && echo "Removed $DESKTOP_FILE"
    [ -f "$ICON_FILE" ] && sudo rm "$ICON_FILE" && echo "Removed $ICON_FILE"
    sudo rm "/usr/bin/mateengine" && echo "Removed /usr/bin/mateengine"

    echo "Uninstallation complete."
    exit 0
fi

# --- Installation Logic ---

if grep -qE "arch|artix" /etc/os-release || { [ -f /etc/os-release ] && . /etc/os-release && [[ "$ID_LIKE" == *"arch"* ]]; }; then
    echo "This system is Arch-based.";
    echo "Please use the following command to install MateEngine:";
    echo "yay -S mateengine";
    exit;
fi

DEBIAN_DEPS=("libpulse0" "libgtk-3-0t64" "libglib2.0-0t64" "libayatana-appindicator3-1" "libx11-6" "libxext6" "libxrender1" "libxdamage1" "libxcursor1" "libxrandr2" "libxcomposite1")
missing=()

if grep -qE "debian" /etc/os-release || { [ -f /etc/os-release ] && . /etc/os-release && [[ "$ID_LIKE" == *"debian"* ]]; }; then
    echo "This system is Debian-based.";
    echo "Installing requirements...";
    for pkg in "${DEBIAN_DEPS[@]}"; do
        if dpkg -s "$pkg" &> /dev/null; then
            echo "$pkg is already installed"
        else
            missing+=("$pkg")
        fi
    done
    if [ ${#missing[@]} -gt 0 ]; then
        echo "Installing packages: ${missing[*]}"
        sudo apt install -y "${missing[@]}"
    fi
fi

if grep -qE "fedora" /etc/os-release || { [ -f /etc/os-release ] && . /etc/os-release && [[ "$ID_LIKE" == *"fedora"* ]]; }; then
    echo "This system is Fedora-based.";
    echo "Installing requirements...";
    sudo dnf install -y pulseaudio-libs gtk3 glib2 libX11 libXext libXrender libXrandr libXdamage libXcursor libXcomposite libayatana-appindicator-gtk3
fi

SOURCE="$(dirname "$(realpath "${BASH_SOURCE[0]}")")/Payload"

if [ -d "$DEST" ]; then
    echo "Directory $DEST exists. Removing old version..."
    sudo rm -rf "$DEST"
fi

echo "Installing to $DEST"

sudo mkdir -p "$DEST"
sudo cp -R "$SOURCE/." "$DEST"

sudo ln -s "$DEST/launch.sh" "/usr/bin/mateengine"

# Create Desktop Entry
cat <<EOF | sudo tee "$DESKTOP_FILE" > /dev/null
[Desktop Entry]
Name=Mate Engine
Comment=A free Desktop Mate alternative with custom VRM support
Exec=mateengine
Icon=mateengine
Terminal=false
Type=Application
Categories=Game;
Keywords=desktop;pet;anime;vrm;
EOF

# Install Icon
sudo cp "$DEST/MateEngineX_Data/Resources/UnityPlayer.png" "$ICON_FILE"

# Set permissions
sudo chmod +x "$DESKTOP_FILE"

echo "OK - Installation Complete."
echo "To uninstall, simply add --uninstall parameter before running this script."
