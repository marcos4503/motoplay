#!/bin/sh

echo "Starting Motoplay Uninstaller..."
sudo pkill -f "App/Motoplay.Desktop"
sudo pkill -f "Installer/Motoplay.Desktop"
sleep 10
cd ~
rm -r Motoplay
rm .local/share/applications/Motoplay.desktop
rm ".local/share/applications/Motoplay Installer.desktop"
sudo update-desktop-database
rm .config/wayfire-autobackup.ini
cp .config/wayfire.ini .config/wayfire-autobackup.ini
sed -z -i 's/\[autostart\]\nmotoplay = \/home\/rpi\/Motoplay\/App\/Motoplay.Desktop/ /g' .config/wayfire.ini
echo "Motoplay Uninstallation Finished!"
echo "Rebooting in 10 Seconds..."
sleep 10
sudo reboot