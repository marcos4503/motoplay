#!/bin/sh

echo "Starting Motoplay Uninstaller..."
cd ~
rm -r Motoplay
rm .local/share/applications/Motoplay.desktop
rm .local/share/applications/Motoplay Installer.desktop
cp .config/wayfire.ini .config/wayfire-autobackup.ini
sed -i 's/motoplay = \/home\/rpi\/Motoplay\/App\/Motoplay.Desktop/ /g' .config/wayfire.ini
sed -i 's/\[autostart\]/ /g' .config/wayfire.ini
echo "Motoplay Uninstallation Finished!"
echo "Rebooting in 10 Seconds..."
sleep 10