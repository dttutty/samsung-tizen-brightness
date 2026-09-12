# Samsung Tizen Brightness

<p align="center">
  <strong>简体中文</strong> · <a href="README.md">English</a>
</p>

一个用于兼容 Samsung Tizen 显示设备的 Windows 托盘亮度控制器。电视端配套应用会在调节硬件背光时保持 HDMI 画面显示。

<p align="center">
  <img src="docs/screenshots/brightness-flyout.png" width="478" alt="亮度调节弹窗">
</p>

## 下载

从 [GitHub Releases](https://github.com/dttutty/samsung-tizen-brightness/releases/latest) 下载 Windows x64 版本。

## 使用条件

- Windows 11
- 电脑与兼容的 Samsung Tizen 显示设备位于同一局域网
- 可选：开启开发者模式并安装配套 Tizen 应用，以实现不遮挡 HDMI 的直接控制

已在 Samsung Smart Monitor M7 / M70B（`LS43BM702UNXZA`，2022 Tizen）上验证。其他型号可能兼容，但不作保证。

## 设置

1. 启动 Windows 程序，输入显示设备的局域网 IP。
2. 如需授权，右键托盘图标选择“重新配对电视遥控权限…”，并在显示设备上允许。
3. 左键单击托盘图标即可调节亮度。

首次设置可勾选“随 Windows 登录自动启动”，之后也可在托盘图标右键菜单中开关；登录启动时仅静默进入后台。

安装配套 Tizen 应用后，程序会先唤醒电视网络与桥接服务，再直接调节亮度且不遮挡 HDMI。没有开发者模式时会自动退回模拟遥控器方式，调节过程中电视设置菜单会短暂出现。

<details>
<summary><strong>可选：安装 Tizen 桥接器（开发者模式）</strong></summary>

1. 安装 [Tizen Studio](https://developer.samsung.com/smarttv/develop/tools/tizen-studio.html)，然后在 Package Manager → **Extension SDK** 中安装 **TV Extensions** 和 **Samsung Certificate Extension**。
2. 在显示设备上进入 **Apps → App Settings**，输入 `12345`，开启 **Developer Mode**，填写电脑的局域网 IP，然后重启显示设备。
3. 在 Tizen Studio 中打开 **Tools → Device Manager → Remote Device Manager**，添加显示设备 IP 并开启连接。
4. 打开 **Tools → Certificate Manager**，依次选择 **Samsung → TV**，创建 Author 证书和 **Partner** 级 Distributor 证书；加入显示设备 DUID，并备份 Author 证书。
5. 通过 **File → Import → Tizen → Tizen Project** 导入 `tizen/HDMIBrightnessBridge`，在 `bridge.js` 中填写电脑的局域网 IPv4 地址，然后右键项目并选择 **Run As → Tizen Web Application**。

Windows 防火墙询问时，请允许程序访问专用网络；桥接器使用 TCP 端口 `8765`。如果安装被拒绝，请在 Device Manager 中右键已连接设备，选择 **Permit to install applications**。详细步骤参见 Samsung 官方的[电视连接](https://developer.samsung.com/smarttv/develop/getting-started/using-sdk/tv-device.html)、[SDK 安装](https://developer.samsung.com/smarttv/develop/getting-started/setting-up-sdk/installing-tv-sdk.html)和[证书创建](https://developer.samsung.com/smarttv/develop/getting-started/setting-up-sdk/creating-certificates.html)指南。

</details>

配对令牌使用 Windows DPAPI 加密并仅保存在本机；仓库不包含用户地址或令牌。

## 构建

```powershell
dotnet publish .\src\SamsungTizenBrightness\SamsungTizenBrightness.csproj -c Release -r win-x64 --self-contained false
```

Public 证书无法获得所需的 AVInfo 权限。

> 本项目是非官方社区项目，与 Samsung 无关。使用的 Samsung 接口未公开保证兼容性。程序不包含恢复出厂或 Service Menu 操作。

## 许可证

[MIT](LICENSE)
