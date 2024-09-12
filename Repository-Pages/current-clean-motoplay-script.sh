#!/bin/sh

echo "Starting Motoplay Uninstaller..."
cd ~
rm -r Motoplay
rm .local/share/applications/Motoplay.desktop
rm .local/share/applications/Motoplay Installer.desktop
echo "Motoplay Uninstallation Finished!"