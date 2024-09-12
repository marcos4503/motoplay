#!/bin/sh

echo "Starting Installation/Update of Motoplay Installer..."
cd ~
sudo apt-get install curl -y
sudo apt-get install unzip -y
rm motoplay-installer.zip
rm -r Motoplay/Installer
curl -o motoplay-installer.zip "https://marcos4503.github.io/motoplay/Repository-Pages/current-installer-motoplay-release.zip"
mkdir Motoplay
mkdir Motoplay/Installer
unzip motoplay-installer.zip -d Motoplay/Installer
chmod +x "Motoplay/Installer/InstallerMotoplay.Desktop"
rm motoplay-installer.zip
echo "Starting Motoplay Installer..."
"Motoplay/Installer/InstallerMotoplay.Desktop" online
echo "Motoplay Installation Finished!"