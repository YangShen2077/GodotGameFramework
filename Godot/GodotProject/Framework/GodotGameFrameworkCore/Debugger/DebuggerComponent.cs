using GameFramework;
using GameFramework.Debugger;
using Godot;
using System;
using System.Text;

namespace GodotGameFramework.Debugger;

/// <summary>
/// 调试器激活窗口类型。
/// </summary>
public enum DebuggerActiveWindowType : byte
{
    /// <summary>
    /// 总是打开。
    /// </summary>
    AlwaysOpen = 0,

    /// <summary>
    /// 仅在开发（调试）构建中打开（OS.IsDebugBuild）。
    /// </summary>
    OnlyOpenWhenDevelopment,

    /// <summary>
    /// 仅在编辑器中运行时打开（OS.HasFeature("editor")）。
    /// </summary>
    OnlyOpenInEditor,

    /// <summary>
    /// 总是关闭。
    /// </summary>
    AlwaysClose,
}

/// <summary>
/// 调试器组件。
/// 由本组件渲染到 RichTextLabel，并把链接点击路由回窗口回调。
/// </summary>
public sealed partial class DebuggerComponent : GameFrameworkComponent
{
    /// <summary>
    /// 默认图标位置。
    /// </summary>
    internal static readonly Vector2 DefaultIconPosition = new Vector2(10f, 10f);

    /// <summary>
    /// 默认窗口位置。
    /// </summary>
    internal static readonly Vector2 DefaultWindowPosition = new Vector2(10f, 10f);

    /// <summary>
    /// 默认窗口尺寸。
    /// </summary>
    internal static readonly Vector2 DefaultWindowSize = new Vector2(760f, 520f);

    /// <summary>
    /// 默认窗口缩放。
    /// </summary>
    internal const float DefaultWindowScale = 1f;

    /// <summary>
    /// 默认图标尺寸（随缩放倍率等比放大）。
    /// </summary>
    internal static readonly Vector2 DefaultIconSize = new Vector2(96f, 36f);

    /// <summary>
    /// 基准正文字号（随缩放倍率等比放大）。
    /// </summary>
    private const float BaseFontSize = 14f;

    /// <summary>
    /// 基准标题 / 图标字号（随缩放倍率等比放大）。
    /// </summary>
    private const float BaseTitleFontSize = 16f;

    // ---- 主题色（样式随缩放重建时复用）----
    private static readonly Color IconPanelBackground = new Color(0.05f, 0.07f, 0.1f, 0.85f);
    private static readonly Color IconPanelBorder = new Color(0.3f, 0.5f, 0.7f, 0.9f);
    private static readonly Color WindowPanelBackground = new Color(0.04f, 0.05f, 0.08f, 0.94f);
    private static readonly Color WindowPanelBorder = new Color(0.35f, 0.55f, 0.75f, 1f);
    private static readonly Color TitleBarBackground = new Color(0.09f, 0.13f, 0.2f, 1f);
    private static readonly Color TitleBarBorder = new Color(0f, 0f, 0f, 0f);

    private const string SettingIconX = "Debugger.Icon.X";
    private const string SettingIconY = "Debugger.Icon.Y";
    private const string SettingWindowX = "Debugger.Window.X";
    private const string SettingWindowY = "Debugger.Window.Y";
    private const string SettingWindowScale = "Debugger.Window.Scale";
    private const string SettingWindowAutoScale = "Debugger.Window.AutoScale";

    /// <summary>
    /// 调试器 UI 的参考分辨率（自适应缩放基准：对应 760×520 窗口 / 14 号字）。
    /// </summary>
    private static readonly Vector2 ReferenceResolution = new Vector2(1920f, 1080f);

    /// <summary>
    /// 调试器激活窗口类型。
    /// </summary>
    [Export]
    private DebuggerActiveWindowType _ActiveWindowType = DebuggerActiveWindowType.OnlyOpenInEditor;

    /// <summary>
    /// 启动时是否直接展开全窗口（否则先显示 FPS 图标）。
    /// </summary>
    [Export]
    private bool _ShowFullWindow = false;

    private IDebuggerManager m_DebuggerManager;
    private readonly DebuggerDraw m_Draw = new DebuggerDraw();
    private Theme m_UiTheme;
    private FpsCounter m_FpsCounter;
    private ConsoleWindow m_ConsoleWindow;

    // ---- UI ----
    private CanvasLayer m_CanvasLayer;
    private PanelContainer m_IconPanel;
    private Label m_IconLabel;
    private PanelContainer m_WindowPanel;
    private VBoxContainer m_WindowRoot;
    private PanelContainer m_TitleBar;
    private Label m_TitleLabel;
    private Button m_TitleCloseButton;
    private VBoxContainer m_ToolbarRoot;
    private RichTextLabel m_ContentLabel;

    private bool m_ShowFullWindowState;
    private string m_ToolbarSignature;
    private string m_LastContentText;
    private readonly StringBuilder m_ToolbarSignatureBuilder = new StringBuilder(128);
    private float m_WindowScale = DefaultWindowScale;

    /// <summary>
    /// 是否按视口自动计算窗口缩放（开启时忽略手动倍率）。
    /// </summary>
    private bool m_AutoWindowScale = true;

    // ---- 拖拽状态 ----
    private Control m_DragTarget;
    private Vector2 m_DragOffset;
    private float m_DragAccum;

    /// <summary>
    /// 获取调试器绘制上下文（供自定义 IDebuggerWindow 使用）。
    /// </summary>
    public DebuggerDraw Draw => m_Draw;

    /// <summary>
    /// 获取当前帧率。
    /// </summary>
    public float CurrentFps => m_FpsCounter?.CurrentFps ?? 0f;

    /// <summary>
    /// 获取或设置调试器窗口是否激活（图标或全窗口是否可见）。
    /// </summary>
    public bool ActiveWindow
    {
        get => m_DebuggerManager != null && m_DebuggerManager.ActiveWindow;
        set
        {
            if (m_DebuggerManager != null)
            {
                m_DebuggerManager.ActiveWindow = value;
            }

            if (m_CanvasLayer != null)
            {
                m_CanvasLayer.Visible = value;
            }
        }
    }

    /// <summary>
    /// 获取或设置是否显示完整调试器窗口（false 时显示 FPS 图标）。
    /// </summary>
    public bool ShowFullWindow
    {
        get => m_ShowFullWindowState;
        set
        {
            if (m_ShowFullWindowState != value)
            {
                // 切换时驱动选中窗口链的 OnEnter / OnLeave
                IDebuggerWindowGroup root = m_DebuggerManager?.DebuggerWindowRoot;
                if (root != null && root.DebuggerWindowCount > 0)
                {
                    if (value)
                    {
                        root.OnEnter();
                    }
                    else
                    {
                        root.OnLeave();
                    }
                }

                m_ShowFullWindowState = value;
                m_ToolbarSignature = null;
                m_LastContentText = null;
            }

            RefreshVisibility();
        }
    }

    /// <summary>
    /// 获取或设置调试器窗口缩放。
    /// 缩放直接作用在窗口尺寸 / 字号 / 内边距等实际布局上（不使用 Control.Scale），保证文字按真实字号光栅化不发虚。
    /// </summary>
    public float WindowScale
    {
        get => m_WindowScale;
        set
        {
            m_WindowScale = Mathf.Clamp(value, 0.5f, 4f);
            ApplyUiMetrics();
        }
    }

    /// <summary>
    /// 获取或设置是否按当前视口自动计算窗口缩放（开启时 <see cref="WindowScale"/> 由 <see cref="ResolveAutoWindowScale"/> 决定）。
    /// </summary>
    public bool AutoWindowScale
    {
        get => m_AutoWindowScale;
        set
        {
            if (m_AutoWindowScale == value)
            {
                return;
            }

            m_AutoWindowScale = value;
            if (value)
            {
                ApplyAutoWindowScale();
            }

            SaveLayoutSettings();
        }
    }

    /// <summary>
    /// 按视口尺寸与参考分辨率（1920×1080）计算自适应缩放值。
    /// 基准分辨率 3840×2160 下返回 2.0，使窗口/字号保持与 1080p 基准一致的屏占比。
    /// </summary>
    public float ResolveAutoWindowScale()
    {
        Vector2 viewportSize = GetViewport()?.GetVisibleRect().Size ?? ReferenceResolution;
        if (viewportSize.X <= 0f || viewportSize.Y <= 0f)
        {
            return DefaultWindowScale;
        }

        float scale = Mathf.Min(viewportSize.X / ReferenceResolution.X, viewportSize.Y / ReferenceResolution.Y);
        return Mathf.Clamp(scale, 0.5f, 4f);
    }

    /// <summary>
    /// 获取或设置 FPS 图标位置。
    /// </summary>
    public Vector2 IconPosition
    {
        get => m_IconPanel?.Position ?? DefaultIconPosition;
        set
        {
            if (m_IconPanel != null)
            {
                m_IconPanel.Position = value;
            }
        }
    }

    /// <summary>
    /// 获取或设置调试器窗口位置。
    /// </summary>
    public Vector2 WindowPosition
    {
        get => m_WindowPanel?.Position ?? DefaultWindowPosition;
        set
        {
            if (m_WindowPanel != null)
            {
                m_WindowPanel.Position = value;
            }
        }
    }

    public override void OnInit()
    {
        base.OnInit();
        m_DebuggerManager = GameFrameworkEntry.GetModule<IDebuggerManager>();
        if (m_DebuggerManager == null)
        {
            Log.Fatal("Debugger manager is invalid.");
            return;
        }

        m_FpsCounter = new FpsCounter(0.5f);
    }

    public override void OnEnter()
    {
        base.OnEnter();
        if (m_DebuggerManager == null)
        {
            return;
        }

        BuildUi();
        RegisterDefaultDebuggerWindows();
        LoadLayoutSettings();

        // 视口 / 窗口尺寸变化时（换分辨率、拖窗口、切显示器）重新计算自适应缩放
        Window window = GetWindow();
        if (window != null)
        {
            window.SizeChanged += OnWindowSizeChanged;
        }

        ActiveWindow = _ActiveWindowType switch
        {
            DebuggerActiveWindowType.AlwaysOpen => true,
            DebuggerActiveWindowType.OnlyOpenWhenDevelopment => OS.IsDebugBuild(),
            DebuggerActiveWindowType.OnlyOpenInEditor => OS.HasFeature("editor"),
            _ => false,
        };

        ShowFullWindow = _ShowFullWindow;
    }

    public override void OnUpdate(double delta)
    {
        if (m_DebuggerManager == null || m_CanvasLayer == null)
        {
            return;
        }

        float deltaSeconds = (float)delta;
        m_FpsCounter.Update(deltaSeconds, deltaSeconds);

        if (!ActiveWindow)
        {
            return;
        }

        if (m_ShowFullWindowState)
        {
            RefreshToolbar();
            RefreshContent();
        }
        else
        {
            RefreshIcon();
        }
    }

    public override void OnExitTree()
    {
        base.OnExitTree();

        Window window = GetWindow();
        if (window != null)
        {
            window.SizeChanged -= OnWindowSizeChanged;
        }
    }

    #region 注册 API

    /// <summary>
    /// 注册调试器窗口。
    /// 注意：组件会把自身作为首个初始化参数注入（args[0]），自定义窗口可借此访问绘制上下文。
    /// </summary>
    /// <param name="path">调试器窗口路径（'/' 分级）。</param>
    /// <param name="debuggerWindow">要注册的调试器窗口。</param>
    /// <param name="args">初始化调试器窗口参数。</param>
    public void RegisterDebuggerWindow(string path, IDebuggerWindow debuggerWindow, params object[] args)
    {
        object[] merged = new object[(args?.Length ?? 0) + 1];
        merged[0] = this;
        if (args != null && args.Length > 0)
        {
            Array.Copy(args, 0, merged, 1, args.Length);
        }

        m_DebuggerManager.RegisterDebuggerWindow(path, debuggerWindow, merged);
        m_ToolbarSignature = null;
    }

    /// <summary>
    /// 解除注册调试器窗口。
    /// </summary>
    public bool UnregisterDebuggerWindow(string path)
    {
        bool result = m_DebuggerManager.UnregisterDebuggerWindow(path);
        m_ToolbarSignature = null;
        return result;
    }

    /// <summary>
    /// 获取调试器窗口。
    /// </summary>
    public IDebuggerWindow GetDebuggerWindow(string path)
    {
        return m_DebuggerManager.GetDebuggerWindow(path);
    }

    /// <summary>
    /// 选中调试器窗口。
    /// </summary>
    public bool SelectDebuggerWindow(string path)
    {
        bool result = m_DebuggerManager.SelectDebuggerWindow(path);
        m_ToolbarSignature = null;
        m_LastContentText = null;
        return result;
    }

    #endregion

    #region 布局设置

    /// <summary>
    /// 重置图标 / 窗口布局并保存。
    /// </summary>
    public void ResetLayout()
    {
        IconPosition = DefaultIconPosition;
        WindowPosition = DefaultWindowPosition;
        m_AutoWindowScale = true;
        ApplyAutoWindowScale();
        SaveLayoutSettings();
    }

    private void LoadLayoutSettings()
    {
        try
        {
            var setting = GF.Setting;
            if (setting == null)
            {
                return;
            }

            IconPosition = new Vector2(
                setting.GetFloat(SettingIconX, DefaultIconPosition.X),
                setting.GetFloat(SettingIconY, DefaultIconPosition.Y));
            WindowPosition = new Vector2(
                setting.GetFloat(SettingWindowX, DefaultWindowPosition.X),
                setting.GetFloat(SettingWindowY, DefaultWindowPosition.Y));

            // 默认开启自动缩放：旧存档只有 Scale 键、没有 AutoScale 键时也按自动处理，
            // 否则历史拖动窗口保存的倍率会让自适应永远不生效；手动调倍率会写入 AutoScale=false。
            m_AutoWindowScale = setting.GetBool(SettingWindowAutoScale, true);

            WindowScale = setting.GetFloat(SettingWindowScale, DefaultWindowScale);
            if (m_AutoWindowScale)
            {
                ApplyAutoWindowScale();
            }
            else
            {
                ClampWindowPosition();
            }
        }
        catch (Exception exception)
        {
            Log.Warning("Load debugger layout settings failure: {0}", exception.Message);
        }
    }

    /// <summary>
    /// 保存图标 / 窗口布局到设置组件。
    /// </summary>
    public void SaveLayoutSettings()
    {
        try
        {
            var setting = GF.Setting;
            if (setting == null)
            {
                return;
            }

            setting.SetFloat(SettingIconX, IconPosition.X);
            setting.SetFloat(SettingIconY, IconPosition.Y);
            setting.SetFloat(SettingWindowX, WindowPosition.X);
            setting.SetFloat(SettingWindowY, WindowPosition.Y);
            setting.SetFloat(SettingWindowScale, m_WindowScale);
            setting.SetBool(SettingWindowAutoScale, m_AutoWindowScale);
            setting.Save();
        }
        catch (Exception exception)
        {
            Log.Warning("Save debugger layout settings failure: {0}", exception.Message);
        }
    }

    /// <summary>
    /// 按当前视口重新计算并应用自适应缩放。
    /// </summary>
    private void ApplyAutoWindowScale()
    {
        WindowScale = ResolveAutoWindowScale();
        ClampWindowPosition();
    }

    /// <summary>
    /// 把窗口位置钳制在视口内（缩放变大后避免窗口跑到屏幕外）。
    /// </summary>
    private void ClampWindowPosition()
    {
        if (m_WindowPanel == null)
        {
            return;
        }

        Vector2 viewportSize = GetViewport()?.GetVisibleRect().Size ?? Vector2.Zero;
        if (viewportSize.X <= 0f || viewportSize.Y <= 0f)
        {
            return;
        }

        Vector2 panelSize = m_WindowPanel.Size.Max(m_WindowPanel.CustomMinimumSize);
        Vector2 position = m_WindowPanel.Position;
        position.X = Mathf.Clamp(position.X, 0f, Mathf.Max(0f, viewportSize.X - panelSize.X));
        position.Y = Mathf.Clamp(position.Y, 0f, Mathf.Max(0f, viewportSize.Y - panelSize.Y));
        m_WindowPanel.Position = position;
    }

    /// <summary>
    /// 把 FPS 图标位置钳制在视口内（缩放变化后避免图标跑到屏幕外）。
    /// </summary>
    private void ClampIconPosition()
    {
        if (m_IconPanel == null)
        {
            return;
        }

        Vector2 viewportSize = GetViewport()?.GetVisibleRect().Size ?? Vector2.Zero;
        if (viewportSize.X <= 0f || viewportSize.Y <= 0f)
        {
            return;
        }

        Vector2 iconSize = m_IconPanel.Size.Max(m_IconPanel.CustomMinimumSize);
        Vector2 position = m_IconPanel.Position;
        position.X = Mathf.Clamp(position.X, 0f, Mathf.Max(0f, viewportSize.X - iconSize.X));
        position.Y = Mathf.Clamp(position.Y, 0f, Mathf.Max(0f, viewportSize.Y - iconSize.Y));
        m_IconPanel.Position = position;
    }

    /// <summary>
    /// 窗口 / 视口尺寸变化回调。
    /// </summary>
    private void OnWindowSizeChanged()
    {
        if (!m_AutoWindowScale)
        {
            return;
        }

        ApplyAutoWindowScale();
    }

    #endregion

    #region 默认窗口注册

    private void RegisterDefaultDebuggerWindows()
    {
        m_ConsoleWindow = new ConsoleWindow();
        RegisterDebuggerWindow("Console", m_ConsoleWindow);

        RegisterDebuggerWindow("Information/System", new SystemInformationWindow());
        RegisterDebuggerWindow("Information/Environment", new EnvironmentInformationWindow());
        RegisterDebuggerWindow("Information/Screen", new ScreenInformationWindow());
        RegisterDebuggerWindow("Information/Graphics", new GraphicsInformationWindow());
        RegisterDebuggerWindow("Information/Input", new InputInformationWindow());
        RegisterDebuggerWindow("Information/Path", new PathInformationWindow());
        RegisterDebuggerWindow("Information/Scene", new SceneInformationWindow());
        RegisterDebuggerWindow("Information/Time", new TimeInformationWindow());

        RegisterDebuggerWindow("Profiler/Summary", new ProfilerInformationWindow());
        RegisterDebuggerWindow("Profiler/Object Pool", new ObjectPoolInformationWindow());
        RegisterDebuggerWindow("Profiler/Reference Pool", new ReferencePoolInformationWindow());
        RegisterDebuggerWindow("Profiler/Resource", new ResourceInformationWindow());
        RegisterDebuggerWindow("Profiler/WebRequest", new WebRequestInformationWindow());
        RegisterDebuggerWindow("Profiler/Download", new DownloadInformationWindow());

        RegisterDebuggerWindow("Other/Settings", new SettingsWindow());
        RegisterDebuggerWindow("Other/Operations", new OperationsWindow());
    }

    #endregion

    #region UI 构建

    private void BuildUi()
    {
        m_CanvasLayer = new CanvasLayer
        {
            Name = "DebuggerCanvas",
            Layer = 100,
            Visible = false,
        };
        AddChild(m_CanvasLayer);

        m_UiTheme = CreateUiTheme();
        BuildIcon();
        BuildWindow();
        ApplyUiMetrics();
        RefreshVisibility();
    }

    /// <summary>
    /// 构建调试器专用主题：用 SystemFont 选一套支持 CJK 的系统字体（中英统一到同一族），
    /// 粗体用 weight 700 的真实字重 —— 内置默认主题的 bold 是 Open Sans 的 embolden 变体，
    /// 中文只能走系统字体回退、拿不到粗体，标题会和正文一样"虚"。
    /// </summary>
    private static Theme CreateUiTheme()
    {
        Font regular = CreateCjkFont(400);
        Font bold = CreateCjkFont(700);

        var theme = new Theme
        {
            DefaultFont = regular,
            DefaultFontSize = (int)BaseFontSize,
        };

        theme.SetFont("font", "Label", regular);
        theme.SetFont("font", "Button", regular);
        theme.SetFont("normal_font", "RichTextLabel", regular);
        theme.SetFont("bold_font", "RichTextLabel", bold);
        theme.SetFont("mono_font", "RichTextLabel", regular);

        theme.SetFontSize("normal_font_size", "RichTextLabel", (int)BaseFontSize);
        theme.SetFontSize("bold_font_size", "RichTextLabel", (int)BaseFontSize);
        theme.SetFontSize("mono_font_size", "RichTextLabel", (int)BaseFontSize);
        return theme;
    }

    /// <summary>
    /// 创建支持中日韩的系统字体（按优先级回退到各平台常见中文字体）。
    /// </summary>
    private static SystemFont CreateCjkFont(int weight)
    {
        return new SystemFont
        {
            FontNames = new[]
            {
                "Microsoft YaHei",      // Windows
                "PingFang SC",          // macOS
                "Noto Sans CJK SC",     // Linux / Android
                "Source Han Sans SC",
                "WenQuanYi Micro Hei",
                "sans-serif",           // 兜底别名
            },
            FontWeight = weight,
        };
    }

    private void BuildIcon()
    {
        m_IconPanel = new PanelContainer
        {
            Name = "DebuggerIcon",
            Position = DefaultIconPosition,
            CustomMinimumSize = DefaultIconSize,
            Theme = m_UiTheme,
        };
        m_IconPanel.AddThemeStyleboxOverride("panel", MakeStyle(IconPanelBackground, IconPanelBorder));

        m_IconLabel = new Label
        {
            Text = "FPS: --",
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            CustomMinimumSize = DefaultIconSize,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        m_IconPanel.AddChild(m_IconLabel);
        m_IconPanel.GuiInput += @event => OnDragInput(@event, m_IconPanel, true);
        m_CanvasLayer.AddChild(m_IconPanel);
    }

    private void BuildWindow()
    {
        m_WindowPanel = new PanelContainer
        {
            Name = "DebuggerWindow",
            Position = DefaultWindowPosition,
            CustomMinimumSize = DefaultWindowSize,
            Theme = m_UiTheme,
        };
        m_WindowPanel.AddThemeStyleboxOverride("panel", MakeStyle(WindowPanelBackground, WindowPanelBorder));

        m_WindowRoot = new VBoxContainer();
        m_WindowRoot.AddThemeConstantOverride("separation", 4);
        m_WindowPanel.AddChild(m_WindowRoot);

        // ---- 标题栏（拖拽区）----
        m_TitleBar = new PanelContainer
        {
            MouseFilter = Control.MouseFilterEnum.Stop,
        };
        m_TitleBar.AddThemeStyleboxOverride("panel", MakeStyle(TitleBarBackground, TitleBarBorder));
        m_TitleBar.GuiInput += @event => OnDragInput(@event, m_WindowPanel, false);

        var titleRow = new HBoxContainer();
        m_TitleBar.AddChild(titleRow);

        m_TitleLabel = new Label
        {
            Text = "GODOT GAME FRAMEWORK DEBUGGER",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        m_TitleLabel.AddThemeColorOverride("font_color", new Color(0.85f, 0.92f, 1f));
        titleRow.AddChild(m_TitleLabel);

        m_TitleCloseButton = new Button
        {
            Text = "  ×  ",
            FocusMode = Control.FocusModeEnum.None,
            Flat = true,
        };
        m_TitleCloseButton.Pressed += () => ShowFullWindow = false;
        titleRow.AddChild(m_TitleCloseButton);
        m_WindowRoot.AddChild(m_TitleBar);

        // ---- 页签区 ----
        m_ToolbarRoot = new VBoxContainer();
        m_ToolbarRoot.AddThemeConstantOverride("separation", 2);
        m_WindowRoot.AddChild(m_ToolbarRoot);

        // ---- 内容区 ----
        m_ContentLabel = new RichTextLabel
        {
            BbcodeEnabled = true,
            ScrollActive = true,
            SelectionEnabled = true,
            ContextMenuEnabled = true,
            FitContent = false,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
        };
        m_ContentLabel.AddThemeFontSizeOverride("normal_font_size", (int)BaseFontSize);
        m_ContentLabel.AddThemeFontSizeOverride("bold_font_size", (int)BaseFontSize);
        m_ContentLabel.AddThemeFontSizeOverride("mono_font_size", (int)BaseFontSize);
        m_ContentLabel.MetaClicked += OnMetaClicked;
        m_WindowRoot.AddChild(m_ContentLabel);

        m_CanvasLayer.AddChild(m_WindowPanel);
    }

    private StyleBoxFlat MakeStyle(Color background, Color border)
    {
        int radius = Mathf.Max(1, Mathf.RoundToInt(4f * m_WindowScale));
        var style = new StyleBoxFlat
        {
            BgColor = background,
            BorderColor = border,
            CornerRadiusTopLeft = radius,
            CornerRadiusTopRight = radius,
            CornerRadiusBottomLeft = radius,
            CornerRadiusBottomRight = radius,
        };
        style.SetBorderWidthAll(Mathf.Max(1, Mathf.RoundToInt(m_WindowScale)));
        style.SetContentMarginAll(Mathf.Round(6f * m_WindowScale));
        return style;
    }

    /// <summary>
    /// 按 <see cref="WindowScale"/> 重建窗口 / 图标 / 字号 / 内边距等实际尺寸。
    /// 走真实布局而不是 Control.Scale，文字按目标字号光栅化，放大后不发虚。
    /// </summary>
    private void ApplyUiMetrics()
    {
        int fontSize = ScaledFontSize(BaseFontSize);
        int titleFontSize = ScaledFontSize(BaseTitleFontSize);
        float separation = Mathf.Max(1f, 4f * m_WindowScale);
        Vector2 iconSize = DefaultIconSize * m_WindowScale;
        Vector2 windowSize = DefaultWindowSize * m_WindowScale;

        if (m_IconPanel != null)
        {
            m_IconPanel.Scale = Vector2.One;
            m_IconPanel.CustomMinimumSize = iconSize;
            m_IconPanel.Size = iconSize;
            m_IconPanel.AddThemeStyleboxOverride("panel", MakeStyle(IconPanelBackground, IconPanelBorder));
        }

        if (m_IconLabel != null)
        {
            m_IconLabel.CustomMinimumSize = iconSize;
            m_IconLabel.AddThemeFontSizeOverride("font_size", fontSize);
        }

        if (m_WindowPanel != null)
        {
            m_WindowPanel.Scale = Vector2.One;
            m_WindowPanel.CustomMinimumSize = windowSize;
            m_WindowPanel.AddThemeStyleboxOverride("panel", MakeStyle(WindowPanelBackground, WindowPanelBorder));
        }

        if (m_WindowRoot != null)
        {
            m_WindowRoot.AddThemeConstantOverride("separation", Mathf.RoundToInt(separation));
        }

        if (m_TitleBar != null)
        {
            m_TitleBar.AddThemeStyleboxOverride("panel", MakeStyle(TitleBarBackground, TitleBarBorder));
        }

        if (m_TitleLabel != null)
        {
            m_TitleLabel.AddThemeFontSizeOverride("font_size", titleFontSize);
        }

        if (m_TitleCloseButton != null)
        {
            m_TitleCloseButton.AddThemeFontSizeOverride("font_size", titleFontSize);
        }

        if (m_ToolbarRoot != null)
        {
            m_ToolbarRoot.AddThemeConstantOverride("separation", Mathf.Max(1, Mathf.RoundToInt(separation / 2f)));
        }

        if (m_ContentLabel != null)
        {
            m_ContentLabel.AddThemeFontSizeOverride("normal_font_size", fontSize);
            m_ContentLabel.AddThemeFontSizeOverride("bold_font_size", fontSize);
            m_ContentLabel.AddThemeFontSizeOverride("mono_font_size", fontSize);
        }

        // 小节标题比正文大一号（配合描边 + 琥珀色，不再只靠颜色区分）
        m_Draw.TitleFontSize = titleFontSize;

        // 字号/尺寸变化后强制重建页签与内容
        m_ToolbarSignature = null;
        m_LastContentText = null;

        ClampWindowPosition();
        ClampIconPosition();
    }

    /// <summary>
    /// 按缩放倍率换算字号（最小 8，避免过小不可读）。
    /// </summary>
    private int ScaledFontSize(float baseSize)
    {
        return Mathf.Max(8, Mathf.RoundToInt(baseSize * m_WindowScale));
    }

    #endregion

    #region UI 刷新

    private void RefreshVisibility()
    {
        if (m_IconPanel != null)
        {
            m_IconPanel.Visible = !m_ShowFullWindowState;
        }

        if (m_WindowPanel != null)
        {
            m_WindowPanel.Visible = m_ShowFullWindowState;
        }
    }

    private void RefreshIcon()
    {
        m_ConsoleWindow.GetLogCounts(out _, out _, out int warningCount, out int errorCount, out int fatalCount);
        Color color = errorCount + fatalCount > 0 ? Colors.Red : warningCount > 0 ? Colors.Yellow : Colors.White;
        m_IconLabel.AddThemeColorOverride("font_color", color);
        m_IconLabel.Text = $"FPS: {m_FpsCounter.CurrentFps:F2}";
    }

    private void RefreshToolbar()
    {
        // 以名称 + 选中索引作为签名，仅在结构 / 选中变化时重建按钮
        m_ToolbarSignatureBuilder.Clear();
        IDebuggerWindowGroup group = m_DebuggerManager.DebuggerWindowRoot;
        while (group != null)
        {
            string[] names = group.GetDebuggerWindowNames();
            if (names != null)
            {
                m_ToolbarSignatureBuilder.Append(string.Join('|', names));
            }

            m_ToolbarSignatureBuilder.Append('#').Append(group.SelectedIndex).Append(';');
            group = group.SelectedWindow as IDebuggerWindowGroup;
        }

        string signature = m_ToolbarSignatureBuilder.ToString();
        if (signature == m_ToolbarSignature)
        {
            return;
        }

        m_ToolbarSignature = signature;
        RebuildToolbar();
    }

    private void RebuildToolbar()
    {
        foreach (Node child in m_ToolbarRoot.GetChildren())
        {
            m_ToolbarRoot.RemoveChild(child);
            child.QueueFree();
        }

        IDebuggerWindowGroup group = m_DebuggerManager.DebuggerWindowRoot;
        bool isRoot = true;
        while (group != null)
        {
            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", Mathf.Max(1, Mathf.RoundToInt(4f * m_WindowScale)));

            string[] names = group.GetDebuggerWindowNames() ?? Array.Empty<string>();
            for (int i = 0; i < names.Length; i++)
            {
                var button = new Button
                {
                    Text = names[i],
                    ToggleMode = true,
                    ButtonPressed = i == group.SelectedIndex,
                    FocusMode = Control.FocusModeEnum.None,
                };
                button.AddThemeFontSizeOverride("font_size", ScaledFontSize(BaseFontSize));

                IDebuggerWindowGroup capturedGroup = group;
                int capturedIndex = i;
                button.Pressed += () => OnToolbarButtonPressed(capturedGroup, capturedIndex);
                row.AddChild(button);
            }

            if (isRoot)
            {
                var spacer = new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
                row.AddChild(spacer);

                var closeButton = new Button
                {
                    Text = "Close",
                    FocusMode = Control.FocusModeEnum.None,
                    Modulate = new Color(1f, 0.65f, 0.65f),
                };
                closeButton.AddThemeFontSizeOverride("font_size", ScaledFontSize(BaseFontSize));
                closeButton.Pressed += () => ShowFullWindow = false;
                row.AddChild(closeButton);
                isRoot = false;
            }

            m_ToolbarRoot.AddChild(row);
            group = group.SelectedWindow as IDebuggerWindowGroup;
        }
    }

    private void OnToolbarButtonPressed(IDebuggerWindowGroup group, int index)
    {
        if (group.SelectedIndex != index)
        {
            group.SelectedWindow?.OnLeave();
            group.SelectedIndex = index;
            group.SelectedWindow?.OnEnter();
            m_LastContentText = null;
        }

        // 无论是否切换都强制重建，修正 ToggleMode 按钮的按下态
        m_ToolbarSignature = null;
    }

    private void RefreshContent()
    {
        IDebuggerWindow leafWindow = m_DebuggerManager.DebuggerWindowRoot;
        while (leafWindow is IDebuggerWindowGroup leafGroup)
        {
            leafWindow = leafGroup.SelectedWindow;
        }

        m_Draw.Clear();
        leafWindow?.OnDraw();

        string text = m_Draw.GetText();
        if (!string.Equals(text, m_LastContentText, StringComparison.Ordinal))
        {
            m_LastContentText = text;
            m_ContentLabel.Text = text;
        }

        m_ContentLabel.ScrollFollowing = m_Draw.ScrollFollowing;
    }

    private void OnMetaClicked(Variant meta)
    {
        m_Draw.HandleMeta(meta.AsString());
        m_LastContentText = null;
    }

    #endregion

    #region 拖拽

    private void OnDragInput(InputEvent @event, Control target, bool isIcon)
    {
        if (@event is InputEventMouseButton mouseButton && mouseButton.ButtonIndex == MouseButton.Left)
        {
            if (mouseButton.Pressed)
            {
                m_DragTarget = target;
                m_DragOffset = target.GetGlobalMousePosition() - target.GlobalPosition;
                m_DragAccum = 0f;
            }
            else
            {
                if (m_DragTarget == target)
                {
                    if (m_DragAccum <= 6f && isIcon)
                    {
                        // 视为点击：展开全窗口
                        ShowFullWindow = true;
                    }
                    else if (m_DragAccum > 6f)
                    {
                        SaveLayoutSettings();
                    }
                }

                m_DragTarget = null;
            }
        }
        else if (@event is InputEventMouseMotion mouseMotion && m_DragTarget == target)
        {
            m_DragAccum += mouseMotion.Relative.Length();
            Vector2 position = target.GetGlobalMousePosition() - m_DragOffset;

            // 钳制在视口内
            Vector2 viewportSize = target.GetViewportRect().Size;
            Vector2 targetSize = target.Size * target.Scale;
            position.X = Mathf.Clamp(position.X, 0f, Mathf.Max(0f, viewportSize.X - targetSize.X));
            position.Y = Mathf.Clamp(position.Y, 0f, Mathf.Max(0f, viewportSize.Y - targetSize.Y));
            target.Position = position;
        }
    }

    #endregion
}
