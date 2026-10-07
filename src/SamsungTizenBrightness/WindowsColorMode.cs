// SPDX-FileCopyrightText: 2026 dttutty
// SPDX-License-Identifier: GPL-3.0-only

using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace SamsungTizenBrightness;

internal static class WindowsColorMode
{
    private const string PersonalizeKey =
        @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
    private const uint WmSettingChange = 0x001A;
    private const uint SmtoAbortIfHung = 0x0002;

    public static bool IsLight
    {
        get
        {
            try
            {
                using RegistryKey? key = Registry.CurrentUser.OpenSubKey(PersonalizeKey);
                return key?.GetValue("AppsUseLightTheme") is not int value || value != 0;
            }
            catch
            {
                // A blocked theme lookup must not prevent the tray from opening.
                return true;
            }
        }
    }

    public static void SetLight(bool light)
    {
        using RegistryKey key = Registry.CurrentUser.CreateSubKey(PersonalizeKey, writable: true)
            ?? throw new InvalidOperationException("无法打开 Windows 个性化设置。");
        int value = light ? 1 : 0;
        key.SetValue("AppsUseLightTheme", value, RegistryValueKind.DWord);
        key.SetValue("SystemUsesLightTheme", value, RegistryValueKind.DWord);

        // Ask Explorer and running apps to refresh their theme. Applications
        // that do not handle this notification may update on their next launch.
        _ = Task.Run(() => SendMessageTimeout(
            new IntPtr(0xFFFF),
            WmSettingChange,
            IntPtr.Zero,
            "ImmersiveColorSet",
            SmtoAbortIfHung,
            100,
            out _));
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr SendMessageTimeout(
        IntPtr window,
        uint message,
        IntPtr wParam,
        string lParam,
        uint flags,
        uint timeoutMilliseconds,
        out IntPtr result);
}
