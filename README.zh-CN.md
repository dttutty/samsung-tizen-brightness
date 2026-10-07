# Samsung Tizen Brightness

<p align="center"><strong>简体中文</strong> · <a href="README.md">English</a></p>

通过 **IP Remote 直接调节硬件背光**的 Windows 托盘程序。不需要电视端应用、开发者模式、Tizen Studio 或开发证书。

<p align="center"><img src="docs/screenshots/brightness-flyout.png" width="478" alt="亮度调节弹窗"></p>

## 设置

1. 从 [Releases](https://github.com/dttutty/samsung-tizen-brightness/releases/latest) 下载 Windows x64 程序。
2. 电脑与显示器连接同一局域网。在显示器的 **网络 → 专家设置**中开启 **IP Remote**。
3. 启动程序并填写显示器 IP。右键托盘图标 → **授权 IP Remote…**，在显示器上允许一次。
4. 左键托盘图标即可调亮度。支持 Windows 深浅色模式，以及中／英／韩／西班牙语界面。

需要 Windows 11 和 .NET 10 Desktop Runtime。已验证 Samsung M7 / M70B（`LS43BM702UNXZA`）。其他机型是否支持 IP Remote 背光命令需实测；不支持时不会用遥控器打开画面菜单。参见 [Samsung IP Control 指南](https://image-us.samsung.com/SamsungUS/samsungbusiness/tv-ci-resources/Samsung-IP-Control.pdf)。

## 行为

- 仅调节亮度，不自动开关机，不发送 Wake-on-LAN，不显示倒计时。
- 插拔线、Windows 唤醒只刷新连接状态，不改变显示器自身的无信号待机行为。
- 可选随 Windows 登录启动；只启动托盘程序，不开启显示器。

授权在本机通过 Windows DPAPI 加密保存，并绑定显示器地址及 TLS 证书。普通点击与自动重连不会申请权限，也不开放桥接器入站端口。

## 构建

```powershell
dotnet publish .\src\SamsungTizenBrightness\SamsungTizenBrightness.csproj -c Release -r win-x64 --self-contained false
dotnet run --project .\tests\SamsungTizenBrightness.Tests -c Release
```

本项目为非官方社区项目，与 Samsung 无关。不保证所有固件兼容；不包含恢复出厂或 Service Menu 操作。

[MIT](LICENSE)
