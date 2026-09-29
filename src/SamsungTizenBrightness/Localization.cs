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
        ["AppTitle"] = "Samsung Tizen Brightness",
        ["PopupTitle"] = "Samsung Display Brightness",
        ["DetectingConnection"] = "Detecting connection…",
        ["DetectingDisplay"] = "Detecting display…",
        ["ConnectingDisplay"] = "Connecting to display…",
        ["ReadingBrightness"] = "Reading brightness…",
        ["FallbackConnected"] = "Remote compatibility mode connected",
        ["HdmiConnected"] = "HDMI control connected",
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
        ["ConnectionSettings"] = "Connection and Developer Mode settings…",
        ["StartWithWindows"] = "Start when I sign in to Windows",
        ["RecoverHdmi"] = "Restore HDMI picture",
        ["PairRemote"] = "Pair TV remote permission again…",
        ["Exit"] = "Exit",
        ["TrayConnected"] = "Samsung Tizen Brightness · Connected",
        ["TrayHdmiDisconnected"] = "Samsung Tizen Brightness · HDMI disconnected",
        ["TrayControlDisconnected"] = "Samsung Tizen Brightness · Control disconnected",
        ["StartedTitle"] = "Samsung Tizen Brightness started",
        ["StartedBody"] = "Left-click the display icon to open brightness control.",
        ["PairingTitle"] = "Pairing the TV remote",
        ["PairingBody"] = "Approve the one permission prompt shown on the display.",
        ["PairingComplete"] = "Pairing complete",
        ["PairingCompleteBody"] = "The permission token was saved. Normal clicks will not ask again.",
        ["RecoverFailed"] = "Could not restore the HDMI picture:\n{0}",
        ["PairFailed"] = "Could not pair the TV remote:\n{0}",
        ["HostSavedRestart"] = "The new display IP was saved. Exit and restart the app to apply it.",
        ["StartupFailed"] = "Could not change Windows startup settings:\n{0}",
        ["SetupTitle"] = "Samsung Tizen Brightness · Connection settings",
        ["SetupHeading"] = "Connect a Samsung display",
        ["SetupIntro"] = "Remote compatibility mode does not require Developer Mode. Developer Mode enables direct brightness control without covering HDMI.",
        ["SetupIpLabel"] = "1. Enter the display IP (Display: Settings → General → Network → Network Status → IP Settings)",
        ["SetupIpExample"] = "For example, 192.168.1.100",
        ["SetupDeveloperHeading"] = "2. Optional: enable Developer Mode and install the display bridge",
        ["SetupDeveloperBody"] = "On the display, open Apps → App Settings and press the remote's “123/Number pad” button.\nEnter 12345 using the on-screen keypad. Enable Developer Mode, enter the PC IP below, then restart the display.\nInstall Tizen Studio, TV Extensions and Samsung Certificate Extension on the PC, then create a Partner certificate.",
        ["SetupPcIp"] = "PC IP to enter on the display:",
        ["ComputerIpNotDetected"] = "Not detected; check Windows network settings",
        ["CopyIp"] = "Copy IP",
        ["Copied"] = "Copied",
        ["OpenSamsungGuide"] = "Open Samsung's Developer Mode, Tizen Studio and Device Manager guide",
        ["OpenWebFailed"] = "Could not open the webpage: {0}",
        ["SetupFallbackNote"] = "You can continue without Developer Mode. The app will automatically use simulated remote control; approve the first permission prompt on the display.",
        ["SetupStartup"] = "Start with Windows sign-in (tray background only)",
        ["SaveContinue"] = "Save and continue",
        ["Cancel"] = "Cancel",
        ["InvalidHost"] = "Enter a valid IP address or host name.",
        ["StartupSaveWarning"] = "The display address was saved, but Windows startup could not be changed:\n{0}",
        ["StartupFatal"] = "Samsung Tizen Brightness could not start:\n{0}"
    };

    private static readonly Dictionary<string, string> Chinese = new(English, StringComparer.Ordinal)
    {
        ["AppTitle"] = "Samsung Tizen 亮度", ["PopupTitle"] = "Samsung 显示器亮度",
        ["DetectingConnection"] = "正在检测连接…", ["DetectingDisplay"] = "正在检测显示器…",
        ["ConnectingDisplay"] = "正在连接显示器…", ["ReadingBrightness"] = "正在读取亮度…",
        ["FallbackConnected"] = "遥控兼容模式已连接", ["HdmiConnected"] = "HDMI 控制已连接",
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
        ["ClosingControl"] = "正在收起控制窗口…", ["Language"] = "语言",
        ["OpenBrightness"] = "打开亮度调节", ["ConnectionSettings"] = "连接与开发者模式设置…",
        ["StartWithWindows"] = "随 Windows 登录启动", ["RecoverHdmi"] = "恢复 HDMI 画面",
        ["PairRemote"] = "重新配对电视遥控权限…", ["Exit"] = "退出",
        ["TrayConnected"] = "Samsung Tizen 亮度 · 已连接",
        ["TrayHdmiDisconnected"] = "Samsung Tizen 亮度 · HDMI 未连接",
        ["TrayControlDisconnected"] = "Samsung Tizen 亮度 · 控制未连接",
        ["StartedTitle"] = "Samsung Tizen 亮度已启动", ["StartedBody"] = "左键单击电视图标即可打开控制面板。",
        ["PairingTitle"] = "正在重新配对电视", ["PairingBody"] = "请只在电视这一次弹出的授权提示中选择“允许”。",
        ["PairingComplete"] = "配对完成", ["PairingCompleteBody"] = "授权令牌已保存，以后普通点击不会再次申请权限。",
        ["RecoverFailed"] = "无法恢复 HDMI 画面：\n{0}", ["PairFailed"] = "无法完成电视遥控配对：\n{0}",
        ["HostSavedRestart"] = "新的显示器 IP 已保存。请退出并重新启动程序后生效。",
        ["StartupFailed"] = "无法修改 Windows 启动项：\n{0}",
        ["SetupTitle"] = "Samsung Tizen 亮度 · 连接设置", ["SetupHeading"] = "首次连接 Samsung 显示器",
        ["SetupIntro"] = "普通遥控兼容模式不需要开发者模式；开发者模式仅用于不遮挡 HDMI 的直接亮度控制。",
        ["SetupIpLabel"] = "1. 输入显示器 IP（电视：设置 → 常规 → 网络 → 网络状态 → IP 设置）",
        ["SetupIpExample"] = "例如 192.168.1.100", ["SetupDeveloperHeading"] = "2. 可选：开启开发者模式并安装电视端桥接器",
        ["SetupDeveloperBody"] = "在电视上打开 Apps → App Settings，然后按遥控器的“123/数字键盘”按钮，\n用屏幕数字键盘输入 12345。开启 Developer Mode 后，输入下面的电脑 IP 并重启电视。\n电脑还需安装 Tizen Studio、TV Extensions 和 Samsung Certificate Extension，并创建 Partner 证书。",
        ["SetupPcIp"] = "需要填入电视的电脑 IP：", ["CopyIp"] = "复制 IP", ["Copied"] = "已复制",
        ["ComputerIpNotDetected"] = "未检测到，请在 Windows 网络设置中查看",
        ["OpenSamsungGuide"] = "打开 Samsung 官方开发者模式、Tizen Studio 与 Device Manager 安装说明",
        ["OpenWebFailed"] = "无法打开网页：{0}",
        ["SetupFallbackNote"] = "不配置开发者模式也可以继续：程序会自动改用模拟遥控器方式。首次遥控配对时，请在电视上选择“允许”。",
        ["SetupStartup"] = "随 Windows 登录自动启动（仅进入托盘后台）", ["SaveContinue"] = "保存并继续",
        ["Cancel"] = "取消", ["InvalidHost"] = "请输入有效的 IP 地址或主机名。",
        ["StartupSaveWarning"] = "显示器地址会正常保存，但无法修改 Windows 启动项：\n{0}",
        ["StartupFatal"] = "无法启动 Samsung Tizen 亮度：\n{0}"
    };

    private static readonly Dictionary<string, string> Korean = new(English, StringComparer.Ordinal)
    {
        ["AppTitle"] = "Samsung Tizen 밝기", ["PopupTitle"] = "Samsung 디스플레이 밝기",
        ["DetectingConnection"] = "연결 확인 중…", ["DetectingDisplay"] = "디스플레이 확인 중…",
        ["ConnectingDisplay"] = "디스플레이 연결 중…", ["ReadingBrightness"] = "밝기 읽는 중…",
        ["FallbackConnected"] = "리모컨 호환 모드 연결됨", ["HdmiConnected"] = "HDMI 제어 연결됨",
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
        ["ClosingControl"] = "제어 창 닫는 중…", ["Language"] = "언어",
        ["OpenBrightness"] = "밝기 조절 열기", ["ConnectionSettings"] = "연결 및 개발자 모드 설정…",
        ["StartWithWindows"] = "Windows 로그인 시 시작", ["RecoverHdmi"] = "HDMI 화면 복원",
        ["PairRemote"] = "TV 리모컨 권한 다시 페어링…", ["Exit"] = "종료",
        ["TrayConnected"] = "Samsung Tizen 밝기 · 연결됨",
        ["TrayHdmiDisconnected"] = "Samsung Tizen 밝기 · HDMI 연결 안 됨",
        ["TrayControlDisconnected"] = "Samsung Tizen 밝기 · 제어 연결 안 됨",
        ["StartedTitle"] = "Samsung Tizen 밝기 시작됨", ["StartedBody"] = "디스플레이 아이콘을 왼쪽 클릭하여 밝기를 조절하세요.",
        ["PairingTitle"] = "TV 리모컨 페어링 중", ["PairingBody"] = "디스플레이에 표시되는 권한 요청을 승인하세요.",
        ["PairingComplete"] = "페어링 완료", ["PairingCompleteBody"] = "권한 토큰이 저장되었습니다. 일반 클릭 시 다시 요청하지 않습니다.",
        ["RecoverFailed"] = "HDMI 화면을 복원할 수 없습니다:\n{0}", ["PairFailed"] = "TV 리모컨을 페어링할 수 없습니다:\n{0}",
        ["HostSavedRestart"] = "새 디스플레이 IP가 저장되었습니다. 적용하려면 앱을 다시 시작하세요.",
        ["StartupFailed"] = "Windows 시작 설정을 변경할 수 없습니다:\n{0}",
        ["SetupTitle"] = "Samsung Tizen 밝기 · 연결 설정", ["SetupHeading"] = "Samsung 디스플레이 연결",
        ["SetupIntro"] = "리모컨 호환 모드는 개발자 모드가 필요하지 않습니다. 개발자 모드는 HDMI 화면을 가리지 않는 직접 밝기 제어에 사용됩니다.",
        ["SetupIpLabel"] = "1. 디스플레이 IP 입력 (설정 → 일반 → 네트워크 → 네트워크 상태 → IP 설정)",
        ["SetupIpExample"] = "예: 192.168.1.100", ["SetupDeveloperHeading"] = "2. 선택 사항: 개발자 모드 및 디스플레이 브리지 설치",
        ["SetupDeveloperBody"] = "디스플레이에서 Apps → App Settings를 열고 리모컨의 ‘123/숫자 키패드’ 버튼을 누르세요.\n화면 키패드로 12345를 입력하고 Developer Mode를 켠 뒤 아래 PC IP를 입력하고 디스플레이를 다시 시작하세요.\nPC에 Tizen Studio, TV Extensions, Samsung Certificate Extension을 설치하고 Partner 인증서를 만드세요.",
        ["SetupPcIp"] = "디스플레이에 입력할 PC IP:", ["CopyIp"] = "IP 복사", ["Copied"] = "복사됨",
        ["ComputerIpNotDetected"] = "감지되지 않음. Windows 네트워크 설정을 확인하세요",
        ["OpenSamsungGuide"] = "Samsung 개발자 모드, Tizen Studio 및 Device Manager 안내 열기",
        ["OpenWebFailed"] = "웹페이지를 열 수 없습니다: {0}",
        ["SetupFallbackNote"] = "개발자 모드 없이도 계속할 수 있습니다. 앱이 리모컨 시뮬레이션을 사용하며 최초 권한 요청을 승인해야 합니다.",
        ["SetupStartup"] = "Windows 로그인 시 시작(트레이 백그라운드만)", ["SaveContinue"] = "저장하고 계속",
        ["Cancel"] = "취소", ["InvalidHost"] = "올바른 IP 주소 또는 호스트 이름을 입력하세요.",
        ["StartupSaveWarning"] = "디스플레이 주소는 저장되었지만 Windows 시작 설정을 변경할 수 없습니다:\n{0}",
        ["StartupFatal"] = "Samsung Tizen 밝기를 시작할 수 없습니다:\n{0}"
    };

    private static readonly Dictionary<string, string> Spanish = new(English, StringComparer.Ordinal)
    {
        ["AppTitle"] = "Brillo Samsung Tizen", ["PopupTitle"] = "Brillo de pantalla Samsung",
        ["DetectingConnection"] = "Comprobando conexión…", ["DetectingDisplay"] = "Buscando pantalla…",
        ["ConnectingDisplay"] = "Conectando con la pantalla…", ["ReadingBrightness"] = "Leyendo el brillo…",
        ["FallbackConnected"] = "Modo remoto compatible conectado", ["HdmiConnected"] = "Control HDMI conectado",
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
        ["ClosingControl"] = "Cerrando el control…", ["Language"] = "Idioma",
        ["OpenBrightness"] = "Abrir control de brillo", ["ConnectionSettings"] = "Conexión y modo desarrollador…",
        ["StartWithWindows"] = "Iniciar al entrar en Windows", ["RecoverHdmi"] = "Restaurar imagen HDMI",
        ["PairRemote"] = "Volver a emparejar el permiso remoto…", ["Exit"] = "Salir",
        ["TrayConnected"] = "Brillo Samsung Tizen · Conectado",
        ["TrayHdmiDisconnected"] = "Brillo Samsung Tizen · HDMI desconectado",
        ["TrayControlDisconnected"] = "Brillo Samsung Tizen · Control desconectado",
        ["StartedTitle"] = "Brillo Samsung Tizen iniciado", ["StartedBody"] = "Haz clic izquierdo en el icono para ajustar el brillo.",
        ["PairingTitle"] = "Emparejando el control del TV", ["PairingBody"] = "Aprueba la solicitud que aparecerá en la pantalla.",
        ["PairingComplete"] = "Emparejamiento completado", ["PairingCompleteBody"] = "El permiso se guardó y no se volverá a solicitar normalmente.",
        ["RecoverFailed"] = "No se pudo restaurar la imagen HDMI:\n{0}", ["PairFailed"] = "No se pudo emparejar el control del TV:\n{0}",
        ["HostSavedRestart"] = "La nueva IP se guardó. Reinicia la aplicación para aplicarla.",
        ["StartupFailed"] = "No se pudo cambiar el inicio de Windows:\n{0}",
        ["SetupTitle"] = "Brillo Samsung Tizen · Conexión", ["SetupHeading"] = "Conectar una pantalla Samsung",
        ["SetupIntro"] = "El modo remoto compatible no requiere el modo desarrollador. El modo desarrollador permite controlar el brillo sin cubrir HDMI.",
        ["SetupIpLabel"] = "1. Introduce la IP de la pantalla (Ajustes → General → Red → Estado de red → Configuración IP)",
        ["SetupIpExample"] = "Por ejemplo, 192.168.1.100", ["SetupDeveloperHeading"] = "2. Opcional: activar modo desarrollador e instalar el puente",
        ["SetupDeveloperBody"] = "En la pantalla, abre Apps → App Settings y pulsa ‘123/Teclado numérico’ en el mando.\nIntroduce 12345, activa Developer Mode, escribe la IP del PC indicada abajo y reinicia la pantalla.\nInstala Tizen Studio, TV Extensions y Samsung Certificate Extension, y crea un certificado Partner.",
        ["SetupPcIp"] = "IP del PC que se debe introducir:", ["CopyIp"] = "Copiar IP", ["Copied"] = "Copiada",
        ["ComputerIpNotDetected"] = "No detectada; revisa la configuración de red de Windows",
        ["OpenSamsungGuide"] = "Abrir la guía de Samsung para Developer Mode, Tizen Studio y Device Manager",
        ["OpenWebFailed"] = "No se pudo abrir la página web: {0}",
        ["SetupFallbackNote"] = "Puedes continuar sin el modo desarrollador. La aplicación usará un mando simulado; aprueba la primera solicitud en la pantalla.",
        ["SetupStartup"] = "Iniciar con Windows (solo en segundo plano)", ["SaveContinue"] = "Guardar y continuar",
        ["Cancel"] = "Cancelar", ["InvalidHost"] = "Introduce una dirección IP o un nombre de host válido.",
        ["StartupSaveWarning"] = "La dirección se guardó, pero no se pudo cambiar el inicio de Windows:\n{0}",
        ["StartupFatal"] = "No se pudo iniciar Brillo Samsung Tizen:\n{0}"
    };
}
