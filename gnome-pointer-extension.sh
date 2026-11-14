#!/bin/bash
# Installs the "MateEngine Pointer" GNOME Shell extension.
#
# On GNOME Wayland an X11 application only learns where the cursor is while it is over its own window.
# This extension lets MateEngine ask GNOME Shell for the cursor position over D-Bus, so the avatar can
# follow the cursor across the whole desktop. It does nothing else.
#
#   ./gnome-pointer-extension.sh              install and enable (takes effect after logging in again)
#   ./gnome-pointer-extension.sh --uninstall  remove it

UUID="mateengine-pointer@mateengine.linux"
DEST="${XDG_DATA_HOME:-$HOME/.local/share}/gnome-shell/extensions/$UUID"

if [[ "$1" == "--uninstall" ]]; then
    gnome-extensions disable "$UUID" 2>/dev/null
    rm -rf "$DEST" && echo "Removed $DEST"
    exit 0
fi

mkdir -p "$DEST" || exit 1

cat > "$DEST/metadata.json" <<'JSON'
{
  "uuid": "mateengine-pointer@mateengine.linux",
  "name": "MateEngine Pointer",
  "description": "Lets MateEngine read the cursor position, so the avatar can follow the cursor on Wayland.",
  "shell-version": ["45", "46", "47", "48", "49", "50", "51", "52"],
  "url": "https://github.com/Marksonthegamer/Mate-Engine-Linux-Port"
}
JSON

cat > "$DEST/extension.js" <<'JS'
import Gio from 'gi://Gio';
import {Extension} from 'resource:///org/gnome/shell/extensions/extension.js';

const INTERFACE = `
<node>
  <interface name="org.mateengine.Pointer">
    <method name="Get">
      <arg type="i" direction="out" name="x"/>
      <arg type="i" direction="out" name="y"/>
      <arg type="i" direction="out" name="width"/>
      <arg type="i" direction="out" name="height"/>
    </method>
  </interface>
</node>`;

export default class MateEnginePointerExtension extends Extension {
    enable() {
        this._dbus = Gio.DBusExportedObject.wrapJSObject(INTERFACE, this);
        this._dbus.export(Gio.DBus.session, '/org/mateengine/Pointer');
    }

    disable() {
        this._dbus?.unexport();
        this._dbus = null;
    }

    // Pointer position and the size of the whole desktop, in stage coordinates.
    Get() {
        const [x, y] = global.get_pointer();
        return [Math.round(x), Math.round(y), Math.round(global.stage.width), Math.round(global.stage.height)];
    }
}
JS

echo "Installed to $DEST"

# A running shell only discovers new extensions at startup, so enabling can fail until the next login;
# in that case it is put on the list of enabled extensions directly.
if gnome-extensions enable "$UUID" 2>/dev/null; then
    echo "Enabled."
else
    current="$(gsettings get org.gnome.shell enabled-extensions)"
    if [[ "$current" != *"'$UUID'"* ]]; then
        if [[ "$current" == "@as []" || "$current" == "[]" ]]; then
            updated="['$UUID']"
        else
            updated="${current%]}, '$UUID']"
        fi
        gsettings set org.gnome.shell enabled-extensions "$updated" || exit 1
    fi
    echo "Enabled for the next session: log out and back in to activate it."
fi
