# Samsung Tizen Brightness

<p align="center">
  <a href="README.zh-CN.md">简体中文</a> · <strong>English</strong>
</p>

A Windows tray controller for the hardware backlight of compatible Samsung Tizen displays. Its companion TV app keeps the HDMI picture visible while brightness is adjusted.

<p align="center">
  <img src="docs/screenshots/brightness-flyout.png" width="478" alt="Brightness flyout">
</p>

## Download

Download the Windows x64 build from [GitHub Releases](https://github.com/dttutty/samsung-tizen-brightness/releases/latest).

## Requirements

- Windows 11
- A compatible Samsung Tizen display on the same local network
- Optional: Developer Mode and the companion Tizen app for direct, menu-free control

Verified on Samsung Smart Monitor M7 / M70B (`LS43BM702UNXZA`, 2022 Tizen). Other models may work but are not guaranteed.

## Setup

1. Start the Windows app and enter the display's local IP address.
2. If required, right-click the tray icon, select **Pair TV remote permission…**, and approve the request on the display.
3. Left-click the tray icon to adjust brightness.

Start-at-login is enabled from the first-run guide and can be changed from the tray icon's right-click menu. It starts silently in the background.

With the companion Tizen app installed, brightness is changed directly without covering HDMI. Without Developer Mode, the app automatically falls back to simulated remote-control keys; the TV settings menu appears briefly while adjusting.

<details>
<summary><strong>Optional: install the Tizen bridge (Developer Mode)</strong></summary>

1. Install [Tizen Studio](https://developer.samsung.com/smarttv/develop/tools/tizen-studio.html) and, in Package Manager → **Extension SDK**, install **TV Extensions** and **Samsung Certificate Extension**.
2. On the display, open **Apps → App Settings**, enter `12345`, enable **Developer Mode**, enter the PC's LAN IP address, and reboot the display.
3. In Tizen Studio, open **Tools → Device Manager → Remote Device Manager**, add the display's IP address, and switch the connection on.
4. Open **Tools → Certificate Manager** and create **Samsung → TV** author and **Partner** distributor certificates. Include the display's DUID and back up the author certificate.
5. Use **File → Import → Tizen → Tizen Project** to import `tizen/HDMIBrightnessBridge`, set the PC's LAN IPv4 address in `bridge.js`, then right-click the project and select **Run As → Tizen Web Application**.

Allow the Windows app on private networks when Windows Firewall asks; the bridge connects to TCP port `8765`. If installation is denied, right-click the connected device in Device Manager and select **Permit to install applications**. See Samsung's official [TV device](https://developer.samsung.com/smarttv/develop/getting-started/using-sdk/tv-device.html), [SDK installation](https://developer.samsung.com/smarttv/develop/getting-started/setting-up-sdk/installing-tv-sdk.html), and [certificate](https://developer.samsung.com/smarttv/develop/getting-started/setting-up-sdk/creating-certificates.html) guides.

</details>

The pairing token is encrypted with Windows DPAPI and stored locally. No user address or token is included in this repository.

## Build

```powershell
dotnet publish .\src\SamsungTizenBrightness\SamsungTizenBrightness.csproj -c Release -r win-x64 --self-contained false
```

The required AVInfo privilege is unavailable to Public certificates.

> Unofficial community project, not affiliated with Samsung. It uses Samsung interfaces whose compatibility is not publicly guaranteed. Factory reset and service-menu operations are not implemented.

## License

[MIT](LICENSE)
