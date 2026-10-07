# Samsung Tizen Brightness

<p align="center"><a href="README.zh-CN.md">简体中文</a> · <strong>English</strong></p>

A Windows tray controller for Samsung display hardware backlight, using **IP Remote directly**. No TV companion app, Developer Mode, Tizen Studio or certificate setup.

<p align="center"><img src="docs/screenshots/brightness-flyout.png" width="478" alt="Brightness flyout"></p>

## Setup

1. Download the Windows x64 app from [Releases](https://github.com/dttutty/samsung-tizen-brightness/releases/latest).
2. Connect the PC and display to the same LAN. In the display's **Network → Expert Settings**, enable **IP Remote**.
3. Start the app and enter the display's IP. Right-click its tray icon → **Authorize IP Remote…**, then allow the request on the display once.
4. Left-click the tray icon to adjust brightness. Windows light/dark mode and English, Chinese, Korean and Spanish UI are supported.

Requires Windows 11 and .NET 10 Desktop Runtime. Verified on Samsung M7 / M70B (`LS43BM702UNXZA`). IP Remote backlight support varies by model; unsupported devices have no picture-menu fallback. See [Samsung's IP Control guide](https://image-us.samsung.com/SamsungUS/samsungbusiness/tv-ci-resources/Samsung-IP-Control.pdf).

## Behavior

- Brightness control only: no automatic power-on/off, Wake-on-LAN or countdowns.
- Cable insertion/removal and Windows resume only refresh the connection status. The display's own no-signal standby remains unchanged.
- Windows login startup is optional; it starts the tray app, not the display.

Authorization is saved locally with Windows DPAPI, bound to the display address and a pinned TLS certificate. Ordinary clicks and automatic reconnects never request permission. No inbound bridge port is opened.

## Build

```powershell
dotnet publish .\src\SamsungTizenBrightness\SamsungTizenBrightness.csproj -c Release -r win-x64 --self-contained false
dotnet run --project .\tests\SamsungTizenBrightness.Tests -c Release
```

Unofficial community project, not affiliated with Samsung. Interface compatibility is not guaranteed. No factory-reset or service-menu operations.

[MIT](LICENSE)
