# Network Monitor

[![Release](https://img.shields.io/github/release/danielchalmers/Network-Monitor?include_prereleases)](https://github.com/danielchalmers/Network-Monitor/releases/latest)
[![License](https://img.shields.io/github/license/danielchalmers/Network-Monitor)](LICENSE)

Network Monitor is a tiny widget for Windows that keeps your live latency, download, and upload speeds on the desktop.

![Network Monitor showing live latency, download, and upload](https://user-images.githubusercontent.com/7112040/33785224-16384542-dc32-11e7-8574-676f0fe52726.gif)

## 📸 Screenshots

**Dark mode**

![Dark mode](https://user-images.githubusercontent.com/7112040/173041072-b55593e1-9849-472e-a1ec-a1c64f7fc810.png)

**Horizontal layout**

![Horizontal layout](https://user-images.githubusercontent.com/7112040/33785456-202dfeb0-dc33-11e7-87e5-af4a09d77058.gif)

**Right-click for all the options**

![Right-click options](https://user-images.githubusercontent.com/7112040/204114602-8d508d10-6a93-43d5-bb8a-af0c4befefd6.png)

## ✨ Features

- **Three live readings:** round-trip latency (ping), download speed, and upload speed, each with its own icon.
- **Bits or bytes:** switch between Mb/s and MB/s; units auto-scale from K to T as traffic changes.
- **Vertical or horizontal:** a slim column or a wide strip, so it fits any edge of your screen.
- **Light, dark, or auto:** pick a theme, or let Auto follow the Windows light/dark mode and accent color.
- **Resize instantly:** drag the size slider, or hold **Ctrl** and scroll over the widget to scale it.
- **Stays where you want:** keep it on top of other windows, drag it anywhere, and it remembers where you left it.
- **Copy on demand:** right-click → **Copy** to grab the current numbers.
- **Starts with Windows:** optionally launch on sign-in, and check for updates in one click from the menu.
- **Light on resources:** updates are synced to the system clock, and it recovers on its own if the network drops.

## 🚀 Install

### Option 1: Installer (recommended)

1. Download **`Install.Network.Monitor.msi`** from the [latest release](https://github.com/danielchalmers/Network-Monitor/releases/latest).
2. Run it, then launch **Network Monitor** from the Start menu.

### Option 2: Portable

1. Download **`Network.Monitor.exe`** from the [latest release](https://github.com/danielchalmers/Network-Monitor/releases/latest).
2. Run it — no installation needed.

## 🕹️ Using it

- **Move it:** drag the widget anywhere on screen.
- **Resize it:** hold **Ctrl** and scroll over it, or use the **Size** slider in the menu.
- **Options:** right-click for bits/bytes, orientation, theme, stay-on-top, show-in-taskbar, start-with-PC, etc.
- Latency is measured by pinging `8.8.8.8` (Google DNS); download and upload reflect traffic across your network interfaces.
