# Network Monitor

[![Release](https://img.shields.io/github/release/danielchalmers/Network-Monitor?include_prereleases)](https://github.com/danielchalmers/Network-Monitor/releases/latest) [![License](https://img.shields.io/github/license/danielchalmers/Network-Monitor)](LICENSE)

A small widget for Windows that keeps your live latency, download, and upload speeds on the desktop.

<img width="300" alt="Network Monitor ticking through live latency, download, and upload" src="https://github.com/user-attachments/assets/a2fb99f1-b749-4275-8f4d-1805bf2bfc83" />

## 📸 See more

Hover for a 60-second sparkline and the stats behind it: min/avg/max, jitter, packet loss, and session totals.

<img width="560" alt="Hover tooltip showing a latency sparkline with min/avg/max, jitter, and packet loss" src="https://github.com/user-attachments/assets/1295bb25-ee35-48b1-bc92-4241a75d89ae" />

**Light, dark, or auto**

<img width="280" alt="Network Monitor in dark mode" src="https://github.com/user-attachments/assets/036d321c-6b59-4fd3-984f-1d1ba2fd4c0f" />

**Vertical or horizontal**

<img width="560" alt="Network Monitor as a horizontal strip" src="https://github.com/user-attachments/assets/2e37da0e-1415-4b28-88bb-464dd518632d" />

## ✨ Features

- **Three live readings:** round-trip latency (ping), download speed, and upload speed, updated every second.
- **Hover for history:** a 60-second sparkline with min/avg/max, jitter, packet loss, and session totals.
- **Pick your adapter:** measure every network adapter combined, or single one out from the menu.
- **Bits or bytes:** switch between Mb/s and MB/s; units auto-scale from K to T as traffic changes.
- **Vertical or horizontal:** a slim column or a wide strip, so it fits any edge of your screen.
- **Light, dark, or auto:** pick a theme, or let Auto follow the Windows light/dark mode and accent color.
- **Resize instantly:** drag the size slider, or hold **Ctrl** and scroll over the widget to scale it.
- **Stays where you want:** keep it on top of other windows, drag it anywhere, and it remembers where you left it.
- **Copy on demand:** double-click, or right-click → **Copy**, to grab the current numbers.
- **Starts with Windows:** optionally launch on sign-in, and check for updates in one click from the menu.
- **Honest numbers:** stale readings dim, lost pings show as gaps, and it recovers on its own after drops.
- **Light on resources:** one tiny reading per second, published exactly when the system clock ticks.

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
- **Hover it:** hold the cursor over any reading for a sparkline and detailed stats from the last minute.
- **Options:** right-click for network adapter, bits/bytes, orientation, theme, stay-on-top, etc.
- Latency is measured by pinging `8.8.8.8` (Google DNS); download and upload count all adapters, or one you pick.
