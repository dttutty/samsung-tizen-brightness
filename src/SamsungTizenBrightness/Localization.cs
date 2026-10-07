// SPDX-FileCopyrightText: 2026 dttutty
// SPDX-License-Identifier: GPL-3.0-only

using System.Globalization;

namespace SamsungTizenBrightness;

internal enum UiLanguage
{
    Chinese,
    English,
    Korean,
    Spanish
}

internal static class L
{
    private static UiLanguage _current = DetectSystemLanguage();

    public static UiLanguage Current => _current;

    public static void Initialize(string? savedLanguage)
    {
        if (Enum.TryParse(savedLanguage, ignoreCase: true, out UiLanguage parsed))
            _current = parsed;
    }

    public static void Set(UiLanguage language)
    {
        _current = language;
        LocalState.SaveLanguage(language.ToString());
    }

    public static string T(string key, params object[] args)
    {
        string value = GetDictionary(_current).TryGetValue(key, out string? translated)
            ? translated
            : English.TryGetValue(key, out string? english) ? english : key;
        return args.Length == 0 ? value : string.Format(CultureInfo.CurrentCulture, value, args);
    }

    public static string LanguageName(UiLanguage language) => language switch
    {
        UiLanguage.Chinese => "中文",
        UiLanguage.English => "English",
        UiLanguage.Korean => "한국어",
        UiLanguage.Spanish => "Español",
        _ => language.ToString()
    };

    private static UiLanguage DetectSystemLanguage()
    {
        string language = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
        return language switch
        {
            "zh" => UiLanguage.Chinese,
            "ko" => UiLanguage.Korean,
            "es" => UiLanguage.Spanish,
            _ => UiLanguage.English
        };
    }

    private static IReadOnlyDictionary<string, string> GetDictionary(UiLanguage language) => language switch
    {
        UiLanguage.Chinese => Chinese,
        UiLanguage.Korean => Korean,
        UiLanguage.Spanish => Spanish,
        _ => English
    };

    private static readonly Dictionary<string, string> English = new(StringComparer.Ordinal)
    {
        ["FailureUnavailable"] = "Display currently unavailable",
        ["FailureAuthorization"] = "Authorization required · right-click tray to authorize",
        ["FailureNetwork"] = "Cannot reach display · check IP and network",
        ["FailureUnsupported"] = "This display does not support brightness control",
        ["FailureRemote"] = "IP Remote rejected the request",
        ["FailureInvalidReply"] = "Invalid response from display",
        ["FailureNetworkHelp"] = "Check that the display is on, IP Remote is enabled, and both devices are on the same LAN. Check the saved IP in Connection settings.",
        ["FailureUnsupportedHelp"] = "This display's IP Remote does not support backlightControl. Compatibility varies by model and firmware.",
        ["IpControlPairRequired"] = "Enable IP Remote on the display, then right-click the tray icon and choose Authorize IP Remote. Normal clicks never request permission.",
        ["IpControlRpcFailed"] = "IP Remote rejected the request (code {0}).",
        ["IpControlInvalidReply"] = "The display returned an invalid IP Remote response.",
        ["IpControlValueMismatch"] = "Requested backlight {0}, but the display returned {1}.",
        ["AppTitle"] = "Samsung Tizen Brightness",
        ["PopupTitle"] = "Samsung Display Brightness",
        ["DetectingConnection"] = "Detecting connection…",
        ["DetectingDisplay"] = "Detecting display…",
        ["ConnectingDisplay"] = "Connecting to display…",
        ["ReadingBrightness"] = "Reading brightness…",
        ["HdmiConnected"] = "Display control connected",
        ["ControlDisconnected"] = "Display control disconnected",
        ["DisplayUnavailable"] = "Display currently unavailable",
        ["BrightnessUnavailable"] = "Brightness is currently unavailable.",
        ["Brightness"] = "Brightness",
        ["ThemeLight"] = "☀ Light",
        ["ThemeDark"] = "☾ Dark",
        ["ThemeLightAccessible"] = "Windows is using light mode. Click to switch to dark mode",
        ["ThemeDarkAccessible"] = "Windows is using dark mode. Click to switch to light mode",
        ["SwitchToDark"] = "Switch Windows to dark mode",
        ["SwitchToLight"] = "Switch Windows to light mode",
        ["ThemeSwitchFailed"] = "Could not change Windows color mode:\n{0}",
        ["MinimumBrightness"] = "Minimum brightness",
        ["MaximumBrightness"] = "Maximum brightness",
        ["SetMinimumBrightness"] = "Set minimum brightness",
        ["SetMaximumBrightness"] = "Set maximum brightness",
        ["ClickAgainConfirm"] = "Click again to confirm",
        ["Mute"] = "Mute",
        ["Unmute"] = "Unmute",
        ["ClosingControl"] = "Closing control…",
        ["Language"] = "Language",
        ["OpenBrightness"] = "Open brightness control",
        ["ConnectionSettings"] = "Connection settings…",
        ["StartWithWindows"] = "Start when I sign in to Windows",
        ["ReconnectDisplay"] = "Reconnect display",
        ["PairRemote"] = "Authorize IP Remote…",
        ["Exit"] = "Exit",
        ["TrayConnected"] = "Samsung Tizen Brightness · Connected",
        ["TrayHdmiDisconnected"] = "Samsung Tizen Brightness · Display disconnected",
        ["TrayControlDisconnected"] = "Samsung Tizen Brightness · Control disconnected",
        ["StartedTitle"] = "Samsung Tizen Brightness started",
        ["StartedBody"] = "Left-click the display icon to open brightness control.",
        ["PairingTitle"] = "Authorizing IP Remote",
        ["PairingBody"] = "Approve the one permission prompt shown on the display.",
        ["PairingComplete"] = "Pairing complete",
        ["PairingCompleteBody"] = "The permission token was saved. Normal clicks will not ask again.",
        ["ReconnectFailed"] = "Could not reconnect the display:\n{0}",
        ["PairFailed"] = "Could not authorize IP Remote:\n{0}",
        ["HostSavedRestart"] = "The new display IP was saved. Exit and restart the app to apply it.",
        ["StartupFailed"] = "Could not change Windows startup settings:\n{0}",
        ["SetupTitle"] = "Samsung Tizen Brightness · Connection settings",
        ["SetupHeading"] = "Connect a Samsung display",
        ["SetupIntro"] = "Control hardware backlight directly over IP Remote. No Developer Mode or TV app is needed.",
        ["SetupIpLabel"] = "1. Display IP: Settings → All Settings → Connection → Network → Network Status → IP Settings",
        ["SetupIpExample"] = "For example, 192.168.1.100",
        ["SetupIpRemoteHeading"] = "2. Enable IP Remote on the display",
        ["SetupIpRemoteBody"] = "Settings → All Settings → Connection → Network → Expert Settings → IP Remote → Enable.\nOlder TVs: Settings → General → Network → Expert Settings. If unavailable, check Power On with Mobile there.\nThen right-click the tray icon → Authorize IP Remote, and allow the request on the display once.",
        ["SetupStartup"] = "Start with Windows sign-in (tray background only)",
        ["SaveContinue"] = "Save and continue",
        ["Cancel"] = "Cancel",
        ["InvalidHost"] = "Enter a valid IP address or host name.",
        ["StartupSaveWarning"] = "The display address was saved, but Windows startup could not be changed:\n{0}",
        ["StartupFatal"] = "Samsung Tizen Brightness could not start:\n{0}"
    };

    private static readonly Dictionary<string, string> Chinese = new(English, StringComparer.Ordinal)
    {
        ["FailureUnavailable"] = "显示器当前不可用",
        ["FailureAuthorization"] = "需要授权 · 右键托盘选择授权 IP Remote",
        ["FailureNetwork"] = "无法连接显示器 · 请检查 IP 和网络",
        ["FailureUnsupported"] = "此显示器不支持 IP Remote 亮度控制",
        ["FailureRemote"] = "IP Remote 拒绝了请求",
        ["FailureInvalidReply"] = "显示器返回了无效响应",
        ["FailureNetworkHelp"] = "请确认显示器已开机、IP Remote 已开启，电脑与显示器在同一局域网，并在连接设置中检查保存的 IP。",
        ["FailureUnsupportedHelp"] = "此显示器的 IP Remote 不支持 backlightControl；兼容性取决于型号和固件。",
        ["IpControlPairRequired"] = "请开启显示器的 IP Remote，然后右键托盘图标选择“授权 IP Remote…”。普通点击不会申请权限。",
        ["IpControlRpcFailed"] = "IP Remote 拒绝请求（错误码 {0}）。",
        ["IpControlInvalidReply"] = "显示器返回了无效的 IP Remote 响应。",
        ["IpControlValueMismatch"] = "目标背光为 {0}，但显示器返回 {1}。",
        ["AppTitle"] = "Samsung Tizen 亮度", ["PopupTitle"] = "Samsung 显示器亮度",
        ["DetectingConnection"] = "正在检测连接…", ["DetectingDisplay"] = "正在检测显示器…",
        ["ConnectingDisplay"] = "正在连接显示器…", ["ReadingBrightness"] = "正在读取亮度…",
        ["HdmiConnected"] = "显示器控制已连接",
        ["ControlDisconnected"] = "显示器控制已断开", ["DisplayUnavailable"] = "显示器当前不可用",
        ["BrightnessUnavailable"] = "当前无法读取亮度。", ["Brightness"] = "亮度",
        ["ThemeLight"] = "☀ 浅色", ["ThemeDark"] = "☾ 深色",
        ["ThemeLightAccessible"] = "当前为 Windows 浅色模式，点击切换到深色模式",
        ["ThemeDarkAccessible"] = "当前为 Windows 深色模式，点击切换到浅色模式",
        ["SwitchToDark"] = "切换 Windows 至深色模式", ["SwitchToLight"] = "切换 Windows 至浅色模式",
        ["ThemeSwitchFailed"] = "无法切换 Windows 深色／浅色模式：\n{0}",
        ["MinimumBrightness"] = "最小亮度", ["MaximumBrightness"] = "最大亮度",
        ["SetMinimumBrightness"] = "设为最小亮度", ["SetMaximumBrightness"] = "设为最大亮度",
        ["ClickAgainConfirm"] = "再次点击确认", ["Mute"] = "静音", ["Unmute"] = "取消静音",
        ["ClosingControl"] = "正在收起控制窗口…", ["Language"] = "Language",
        ["OpenBrightness"] = "打开亮度调节", ["ConnectionSettings"] = "连接设置…",
        ["StartWithWindows"] = "随 Windows 登录启动", ["ReconnectDisplay"] = "重新连接显示器",
        ["PairRemote"] = "授权 IP Remote…", ["Exit"] = "退出",
        ["TrayConnected"] = "Samsung Tizen 亮度 · 已连接",
        ["TrayHdmiDisconnected"] = "Samsung Tizen 亮度 · 显示器未连接",
        ["TrayControlDisconnected"] = "Samsung Tizen 亮度 · 控制未连接",
        ["StartedTitle"] = "Samsung Tizen 亮度已启动", ["StartedBody"] = "左键单击电视图标即可打开控制面板。",
        ["PairingTitle"] = "正在授权 IP Remote", ["PairingBody"] = "请只在电视这一次弹出的授权提示中选择“允许”。",
        ["PairingComplete"] = "配对完成", ["PairingCompleteBody"] = "授权令牌已保存，以后普通点击不会再次申请权限。",
        ["ReconnectFailed"] = "无法重新连接显示器：\n{0}", ["PairFailed"] = "无法授权 IP Remote：\n{0}",
        ["HostSavedRestart"] = "新的显示器 IP 已保存。请退出并重新启动程序后生效。",
        ["StartupFailed"] = "无法修改 Windows 启动项：\n{0}",
        ["SetupTitle"] = "Samsung Tizen 亮度 · 连接设置", ["SetupHeading"] = "首次连接 Samsung 显示器",
        ["SetupIntro"] = "通过 IP Remote 直接调节硬件背光，不需要开发者模式或电视端应用。",
        ["SetupIpLabel"] = "1. 显示器 IP：设置 → 所有设置 → 连接 → 网络 → 网络状态 → IP 设置",
        ["SetupIpExample"] = "例如 192.168.1.100", ["SetupIpRemoteHeading"] = "2. 在显示器上开启 IP Remote",
        ["SetupIpRemoteBody"] = "设置 → 所有设置 → 连接 → 网络 → 专家设置 → IP Remote → 启用（Enable）。\n旧款电视：设置 → 常规 → 网络 → 专家设置；若不可用，请检查同页的“通过移动设备开机”。\n保存后右键托盘图标 → 授权 IP Remote，在显示器上允许一次即可。",
        ["SetupStartup"] = "随 Windows 登录自动启动（仅进入托盘后台）", ["SaveContinue"] = "保存并继续",
        ["Cancel"] = "取消", ["InvalidHost"] = "请输入有效的 IP 地址或主机名。",
        ["StartupSaveWarning"] = "显示器地址会正常保存，但无法修改 Windows 启动项：\n{0}",
        ["StartupFatal"] = "无法启动 Samsung Tizen 亮度：\n{0}"
    };

    private static readonly Dictionary<string, string> Korean = new(English, StringComparer.Ordinal)
    {
        ["FailureUnavailable"] = "디스플레이를 사용할 수 없음",
        ["FailureAuthorization"] = "승인 필요 · 트레이를 오른쪽 클릭하세요",
        ["FailureNetwork"] = "연결 실패 · IP와 네트워크를 확인하세요",
        ["FailureUnsupported"] = "이 디스플레이는 밝기 제어를 지원하지 않음",
        ["FailureRemote"] = "IP Remote가 요청을 거부함",
        ["FailureInvalidReply"] = "디스플레이 응답이 올바르지 않음",
        ["FailureNetworkHelp"] = "디스플레이 전원과 IP Remote를 켜고 두 기기가 같은 LAN에 있는지 확인하세요. 연결 설정에서 저장된 IP도 확인하세요.",
        ["FailureUnsupportedHelp"] = "이 디스플레이의 IP Remote는 backlightControl을 지원하지 않습니다. 호환성은 모델과 펌웨어에 따라 다릅니다.",
        ["IpControlPairRequired"] = "디스플레이에서 IP Remote를 켠 후 트레이를 오른쪽 클릭하여 IP Remote 승인을 선택하세요. 일반 클릭은 권한을 요청하지 않습니다。",
        ["IpControlRpcFailed"] = "IP Remote가 요청을 거부했습니다(코드 {0}).",
        ["IpControlInvalidReply"] = "디스플레이의 IP Remote 응답이 올바르지 않습니다.",
        ["IpControlValueMismatch"] = "요청한 백라이트는 {0}이지만 디스플레이는 {1}을 반환했습니다.",
        ["AppTitle"] = "Samsung Tizen 밝기", ["PopupTitle"] = "Samsung 디스플레이 밝기",
        ["DetectingConnection"] = "연결 확인 중…", ["DetectingDisplay"] = "디스플레이 확인 중…",
        ["ConnectingDisplay"] = "디스플레이 연결 중…", ["ReadingBrightness"] = "밝기 읽는 중…",
        ["HdmiConnected"] = "디스플레이 제어 연결됨",
        ["ControlDisconnected"] = "디스플레이 제어 연결 끊김", ["DisplayUnavailable"] = "디스플레이를 사용할 수 없음",
        ["BrightnessUnavailable"] = "현재 밝기를 읽을 수 없습니다.", ["Brightness"] = "밝기",
        ["ThemeLight"] = "☀ 라이트", ["ThemeDark"] = "☾ 다크",
        ["ThemeLightAccessible"] = "Windows 라이트 모드입니다. 다크 모드로 전환하려면 클릭하세요",
        ["ThemeDarkAccessible"] = "Windows 다크 모드입니다. 라이트 모드로 전환하려면 클릭하세요",
        ["SwitchToDark"] = "Windows를 다크 모드로 전환", ["SwitchToLight"] = "Windows를 라이트 모드로 전환",
        ["ThemeSwitchFailed"] = "Windows 색상 모드를 변경할 수 없습니다:\n{0}",
        ["MinimumBrightness"] = "최소 밝기", ["MaximumBrightness"] = "최대 밝기",
        ["SetMinimumBrightness"] = "최소 밝기로 설정", ["SetMaximumBrightness"] = "최대 밝기로 설정",
        ["ClickAgainConfirm"] = "확인하려면 다시 클릭", ["Mute"] = "음소거", ["Unmute"] = "음소거 해제",
        ["ClosingControl"] = "제어 창 닫는 중…", ["Language"] = "Language",
        ["OpenBrightness"] = "밝기 조절 열기", ["ConnectionSettings"] = "연결 설정…",
        ["StartWithWindows"] = "Windows 로그인 시 시작", ["ReconnectDisplay"] = "디스플레이 다시 연결",
        ["PairRemote"] = "IP Remote 승인…", ["Exit"] = "종료",
        ["TrayConnected"] = "Samsung Tizen 밝기 · 연결됨",
        ["TrayHdmiDisconnected"] = "Samsung Tizen 밝기 · 디스플레이 연결 안 됨",
        ["TrayControlDisconnected"] = "Samsung Tizen 밝기 · 제어 연결 안 됨",
        ["StartedTitle"] = "Samsung Tizen 밝기 시작됨", ["StartedBody"] = "디스플레이 아이콘을 왼쪽 클릭하여 밝기를 조절하세요.",
        ["PairingTitle"] = "IP Remote 승인 중", ["PairingBody"] = "디스플레이에 표시되는 권한 요청을 승인하세요.",
        ["PairingComplete"] = "페어링 완료", ["PairingCompleteBody"] = "권한 토큰이 저장되었습니다. 일반 클릭 시 다시 요청하지 않습니다.",
        ["ReconnectFailed"] = "디스플레이에 다시 연결할 수 없습니다:\n{0}", ["PairFailed"] = "IP Remote를 승인할 수 없습니다:\n{0}",
        ["HostSavedRestart"] = "새 디스플레이 IP가 저장되었습니다. 적용하려면 앱을 다시 시작하세요.",
        ["StartupFailed"] = "Windows 시작 설정을 변경할 수 없습니다:\n{0}",
        ["SetupTitle"] = "Samsung Tizen 밝기 · 연결 설정", ["SetupHeading"] = "Samsung 디스플레이 연결",
        ["SetupIntro"] = "IP Remote로 백라이트를 직접 조절합니다. 개발자 모드나 TV 앱이 필요하지 않습니다.",
        ["SetupIpLabel"] = "1. 디스플레이 IP: 설정 → 전체 설정 → 연결 → 네트워크 → 네트워크 상태 → IP 설정",
        ["SetupIpExample"] = "예: 192.168.1.100", ["SetupIpRemoteHeading"] = "2. 디스플레이에서 IP Remote 활성화",
        ["SetupIpRemoteBody"] = "설정 → 전체 설정 → 연결 → 네트워크 → 전문가 설정 → IP Remote → 사용(Enable).\n이전 TV: 설정 → 일반 → 네트워크 → 전문가 설정. 사용할 수 없으면 모바일로 전원 켜기도 확인하세요.\n저장 후 트레이를 오른쪽 클릭 → IP Remote 승인, 디스플레이에서 한 번 허용하세요.",
        ["SetupStartup"] = "Windows 로그인 시 시작(트레이 백그라운드만)", ["SaveContinue"] = "저장하고 계속",
        ["Cancel"] = "취소", ["InvalidHost"] = "올바른 IP 주소 또는 호스트 이름을 입력하세요.",
        ["StartupSaveWarning"] = "디스플레이 주소는 저장되었지만 Windows 시작 설정을 변경할 수 없습니다:\n{0}",
        ["StartupFatal"] = "Samsung Tizen 밝기를 시작할 수 없습니다:\n{0}"
    };

    private static readonly Dictionary<string, string> Spanish = new(English, StringComparer.Ordinal)
    {
        ["FailureUnavailable"] = "Pantalla no disponible",
        ["FailureAuthorization"] = "Autoriza IP Remote desde la bandeja",
        ["FailureNetwork"] = "Sin conexión · comprueba IP y red",
        ["FailureUnsupported"] = "Esta pantalla no admite control de brillo",
        ["FailureRemote"] = "IP Remote rechazó la solicitud",
        ["FailureInvalidReply"] = "Respuesta de pantalla no válida",
        ["FailureNetworkHelp"] = "Enciende la pantalla y activa IP Remote. Conecta ambos dispositivos a la misma LAN y comprueba la IP guardada en Configuración de conexión.",
        ["FailureUnsupportedHelp"] = "IP Remote de esta pantalla no admite backlightControl. La compatibilidad depende del modelo y el firmware.",
        ["IpControlPairRequired"] = "Activa IP Remote y autorízalo desde el menú de la bandeja. Los clics normales no solicitan permiso.",
        ["IpControlRpcFailed"] = "IP Remote rechazó la solicitud (código {0}).",
        ["IpControlInvalidReply"] = "La respuesta de IP Remote no es válida.",
        ["IpControlValueMismatch"] = "Se solicitó {0}, pero la pantalla devolvió {1}.",
        ["AppTitle"] = "Brillo Samsung Tizen", ["PopupTitle"] = "Brillo de pantalla Samsung",
        ["DetectingConnection"] = "Comprobando conexión…", ["DetectingDisplay"] = "Buscando pantalla…",
        ["ConnectingDisplay"] = "Conectando con la pantalla…", ["ReadingBrightness"] = "Leyendo el brillo…",
        ["HdmiConnected"] = "Control de pantalla conectado",
        ["ControlDisconnected"] = "Control de pantalla desconectado", ["DisplayUnavailable"] = "Pantalla no disponible",
        ["BrightnessUnavailable"] = "No se puede leer el brillo.", ["Brightness"] = "Brillo",
        ["ThemeLight"] = "☀ Claro", ["ThemeDark"] = "☾ Oscuro",
        ["ThemeLightAccessible"] = "Windows usa el modo claro. Haz clic para cambiar al modo oscuro",
        ["ThemeDarkAccessible"] = "Windows usa el modo oscuro. Haz clic para cambiar al modo claro",
        ["SwitchToDark"] = "Cambiar Windows al modo oscuro", ["SwitchToLight"] = "Cambiar Windows al modo claro",
        ["ThemeSwitchFailed"] = "No se pudo cambiar el modo de color de Windows:\n{0}",
        ["MinimumBrightness"] = "Brillo mínimo", ["MaximumBrightness"] = "Brillo máximo",
        ["SetMinimumBrightness"] = "Establecer brillo mínimo", ["SetMaximumBrightness"] = "Establecer brillo máximo",
        ["ClickAgainConfirm"] = "Haz clic de nuevo para confirmar", ["Mute"] = "Silenciar", ["Unmute"] = "Activar sonido",
        ["ClosingControl"] = "Cerrando el control…", ["Language"] = "Language",
        ["OpenBrightness"] = "Abrir control de brillo", ["ConnectionSettings"] = "Configuración de conexión…",
        ["StartWithWindows"] = "Iniciar al entrar en Windows", ["ReconnectDisplay"] = "Reconectar pantalla",
        ["PairRemote"] = "Autorizar IP Remote…", ["Exit"] = "Salir",
        ["TrayConnected"] = "Brillo Samsung Tizen · Conectado",
        ["TrayHdmiDisconnected"] = "Brillo Samsung Tizen · Pantalla desconectada",
        ["TrayControlDisconnected"] = "Brillo Samsung Tizen · Control desconectado",
        ["StartedTitle"] = "Brillo Samsung Tizen iniciado", ["StartedBody"] = "Haz clic izquierdo en el icono para ajustar el brillo.",
        ["PairingTitle"] = "Autorizando IP Remote", ["PairingBody"] = "Aprueba la solicitud que aparecerá en la pantalla.",
        ["PairingComplete"] = "Emparejamiento completado", ["PairingCompleteBody"] = "El permiso se guardó y no se volverá a solicitar normalmente.",
        ["ReconnectFailed"] = "No se pudo reconectar la pantalla:\n{0}", ["PairFailed"] = "No se pudo autorizar IP Remote:\n{0}",
        ["HostSavedRestart"] = "La nueva IP se guardó. Reinicia la aplicación para aplicarla.",
        ["StartupFailed"] = "No se pudo cambiar el inicio de Windows:\n{0}",
        ["SetupTitle"] = "Brillo Samsung Tizen · Conexión", ["SetupHeading"] = "Conectar una pantalla Samsung",
        ["SetupIntro"] = "Controla la retroiluminación mediante IP Remote. No requiere modo desarrollador ni una app de TV.",
        ["SetupIpLabel"] = "1. IP: Ajustes → Todos los ajustes → Conexión → Red → Estado de red → Configuración IP",
        ["SetupIpExample"] = "Por ejemplo, 192.168.1.100", ["SetupIpRemoteHeading"] = "2. Activa IP Remote en la pantalla",
        ["SetupIpRemoteBody"] = "Ajustes → Todos los ajustes → Conexión → Red → Configuración avanzada → IP Remote → Activar (Enable).\nTV antiguos: Ajustes → General → Red → Configuración avanzada. Si no está disponible, revisa Encender con móvil.\nDespués, autoriza IP Remote desde la bandeja y permite la solicitud una vez en la pantalla.",
        ["SetupStartup"] = "Iniciar con Windows (solo en segundo plano)", ["SaveContinue"] = "Guardar y continuar",
        ["Cancel"] = "Cancelar", ["InvalidHost"] = "Introduce una dirección IP o un nombre de host válido.",
        ["StartupSaveWarning"] = "La dirección se guardó, pero no se pudo cambiar el inicio de Windows:\n{0}",
        ["StartupFatal"] = "No se pudo iniciar Brillo Samsung Tizen:\n{0}"
    };
}
