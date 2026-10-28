using System;
using System.Collections.Generic;
using System.Text;

namespace GodotGameFramework.Debugger;

/// <summary>
/// 调试器绘制上下文。
///
/// 以 BBCode 文本方式模拟 UGF OnGUI（IMGUI）风格的调试器绘制：
/// 调试器窗口每帧通过本类写入内容，DebuggerComponent 将结果渲染到 RichTextLabel，
/// 并把 [url] 链接点击（meta_clicked）路由回本帧注册的回调，实现按钮 / 开关等交互。
/// </summary>
public sealed class DebuggerDraw
{
    private const string ButtonColor = "#7EC8FF";
    private const string ToggleOnColor = "#7EFF9E";
    private const string ToggleOffColor = "#9AA5B1";
    private const string ItemNameColor = "#8FBCE6";

    // 标题色：比旧值 #FFD37E（低饱和浅黄）更饱和的琥珀色，深底上更醒目
    private const string TitleColor = "#FFC53D";
    private const string TitleOutlineColor = "#000000C0";
    private const string SeparatorColor = "#3A4A5A";

    private readonly StringBuilder m_Text = new StringBuilder(4096);
    private readonly Dictionary<string, Action> m_Actions = new Dictionary<string, Action>();
    private int m_ActionId;

    /// <summary>
    /// 是否请求内容区自动滚动到底部（锁定滚动，控制台用）。
    /// </summary>
    public bool ScrollFollowing { get; set; }

    /// <summary>
    /// 标题字号（由 DebuggerComponent 按缩放倍率设置，比正文大一号）。
    /// </summary>
    public int TitleFontSize { get; set; } = 16;

    /// <summary>
    /// 清空本帧绘制内容与交互回调。由 DebuggerComponent 每帧绘制前调用。
    /// </summary>
    public void Clear()
    {
        m_Text.Length = 0;
        m_Actions.Clear();
        m_ActionId = 0;
        ScrollFollowing = false;
    }

    /// <summary>
    /// 转义 BBCode 特殊字符，避免内容中的 '[' 被解析为标签（日志内容尤其需要）。
    /// </summary>
    public static string Esc(string text)
    {
        return string.IsNullOrEmpty(text) ? string.Empty : text.Replace("[", "[lb]");
    }

    /// <summary>
    /// 追加原始 BBCode 文本（不转义）。
    /// </summary>
    public void AppendRaw(string bbcode)
    {
        m_Text.Append(bbcode);
    }

    /// <summary>
    /// 绘制一行普通文本（自动转义 + 换行）。
    /// </summary>
    public void Label(string text)
    {
        m_Text.Append(Esc(text)).Append('\n');
    }

    /// <summary>
    /// 绘制小节标题。
    /// 比正文大一号 + 琥珀色 + 黑色描边，避免在深底上"只有颜色区分"而看不清。
    /// </summary>
    public void Title(string text)
    {
        m_Text.Append("[font_size=").Append(TitleFontSize)
            .Append("][b][color=").Append(TitleColor)
            .Append("][outline_size=2][outline_color=").Append(TitleOutlineColor).Append(']')
            .Append(Esc(text))
            .Append("[/outline_color][/outline_size][/color][/b][/font_size]\n");
    }

    /// <summary>
    /// 绘制一条水平分隔线。
    /// </summary>
    public void Separator()
    {
        m_Text.Append("[color=").Append(SeparatorColor).Append("]────────────────────────────────────────────────[/color]\n");
    }

    /// <summary>
    /// 空行。
    /// </summary>
    public void Space()
    {
        m_Text.Append('\n');
    }

    /// <summary>
    /// 换行（结束当前内联按钮 / 开关行）。
    /// </summary>
    public void NewLine()
    {
        m_Text.Append('\n');
    }

    /// <summary>
    /// 开始两列信息表格。
    /// </summary>
    public void BeginTable()
    {
        m_Text.Append("[table=2]");
    }

    /// <summary>
    /// 结束两列信息表格。
    /// </summary>
    public void EndTable()
    {
        m_Text.Append("[/table]\n");
    }

    /// <summary>
    /// 两列信息条目（须在 BeginTable / EndTable 之间调用）。
    /// </summary>
    public void DrawItem(string name, string value)
    {
        m_Text.Append("[cell][color=").Append(ItemNameColor).Append(']').Append(Esc(name))
            .Append("[/color]      [/cell][cell]").Append(Esc(value)).Append("[/cell]");
    }

    /// <summary>
    /// 内联按钮，点击触发回调。
    /// </summary>
    public void Button(string label, Action onClick)
    {
        string id = RegisterAction(onClick);
        m_Text.Append("[url=").Append(id).Append("][color=").Append(ButtonColor)
            .Append("][lb] ").Append(Esc(label)).Append(" [rb][/color][/url]  ");
    }

    /// <summary>
    /// 内联开关，点击取反并回调新值。
    /// </summary>
    public void Toggle(bool value, string label, Action<bool> onChanged)
    {
        string id = RegisterAction(() => onChanged?.Invoke(!value));
        m_Text.Append("[url=").Append(id).Append("][color=").Append(value ? ToggleOnColor : ToggleOffColor)
            .Append(value ? "][lb]x[rb] " : "][lb]  [rb] ").Append(Esc(label)).Append("[/color][/url]  ");
    }

    /// <summary>
    /// 可点击文本（内容为原始 BBCode，调用方自行转义），如控制台日志行。
    /// </summary>
    public void Link(string bbcodeInner, Action onClick)
    {
        string id = RegisterAction(onClick);
        m_Text.Append("[url=").Append(id).Append(']').Append(bbcodeInner).Append("[/url]");
    }

    private string RegisterAction(Action action)
    {
        string id = "a" + m_ActionId++;
        m_Actions[id] = action;
        return id;
    }

    /// <summary>
    /// 获取本帧绘制的 BBCode 文本。
    /// </summary>
    internal string GetText()
    {
        return m_Text.ToString();
    }

    /// <summary>
    /// 路由 meta_clicked 链接点击到对应回调。
    /// </summary>
    internal bool HandleMeta(string id)
    {
        if (m_Actions.TryGetValue(id, out Action action))
        {
            action?.Invoke();
            return true;
        }

        return false;
    }
}
