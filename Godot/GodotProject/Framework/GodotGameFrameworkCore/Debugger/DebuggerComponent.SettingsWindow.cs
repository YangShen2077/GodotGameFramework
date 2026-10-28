using Godot;

namespace GodotGameFramework.Debugger;

public sealed partial class DebuggerComponent
{
    /// <summary>
    /// 调试器设置窗口。
    /// </summary>
    private sealed class SettingsWindow : ScrollableDebuggerWindowBase
    {
        protected override void OnDrawScrollableWindow()
        {
            Draw.Title("Window Settings");
            Draw.BeginTable();
            Draw.DrawItem("Icon Position", Component.IconPosition.ToString());
            Draw.DrawItem("Window Position", Component.WindowPosition.ToString());
            Draw.DrawItem("Window Scale", $"{Component.WindowScale:F2}{(Component.AutoWindowScale ? " (auto)" : string.Empty)}");
            Draw.DrawItem("Auto Scale Base", Component.ResolveAutoWindowScale().ToString("F2"));
            Draw.EndTable();
            Draw.Space();

            Draw.Label("Window Scale:");
            Draw.Button("-0.25", () => SetScale(Component.WindowScale - 0.25f));
            Draw.Button("+0.25", () => SetScale(Component.WindowScale + 0.25f));
            Draw.Button("0.5x", () => SetScale(0.5f));
            Draw.Button("1.0x", () => SetScale(1f));
            Draw.Button("1.5x", () => SetScale(1.5f));
            Draw.Button("2.0x", () => SetScale(2f));
            Draw.Button("Auto", () => Component.AutoWindowScale = true);
            Draw.NewLine();
            Draw.Space();

            Draw.Button("Reset Layout", Component.ResetLayout);
            Draw.NewLine();
            Draw.Space();

            Draw.Title("Console Settings");
            Draw.Label($"Max Line: {GetConsoleMaxLine()}");
            Draw.Button("-50", () => AddConsoleMaxLine(-50));
            Draw.Button("+50", () => AddConsoleMaxLine(50));
            Draw.NewLine();
        }

        private void SetScale(float scale)
        {
            // 手动调倍率即退出自动缩放，避免下次窗口尺寸变化时被覆盖
            Component.AutoWindowScale = false;
            Component.WindowScale = scale;
            Component.SaveLayoutSettings();
        }

        private int GetConsoleMaxLine()
        {
            return Component.m_ConsoleWindow?.MaxLine ?? 0;
        }

        private void AddConsoleMaxLine(int delta)
        {
            if (Component.m_ConsoleWindow != null)
            {
                Component.m_ConsoleWindow.MaxLine = Mathf.Clamp(Component.m_ConsoleWindow.MaxLine + delta, 50, 1000);
            }
        }
    }
}
