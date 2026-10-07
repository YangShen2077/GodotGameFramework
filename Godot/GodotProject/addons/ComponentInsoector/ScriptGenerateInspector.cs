#if TOOLS
using System;
using Godot;
namespace GodotGameFramework.Editor
{
    [Tool]
    public partial class ScriptGenerateInspector : EditorInspectorPlugin
    {
        const string Resc = "res://TheGame/MainPack/Resources/ScriptGenerateRes.tres";
        public override bool _CanHandle(GodotObject @object)
        {
            // Control（含所有 CanvasItem 中的 UI）→ 生成 UIForm；Node2D / Node3D → 生成 Entity
            return @object is CanvasItem or Node3D;
        }

        public override void _ParseEnd(GodotObject @object)
        {
            base._ParseEnd(@object);

            // 只判断资源是否存在，不做强类型转换：即使 ScriptGenerateRes 尚未在编辑器注册，
            // 也能通过属性名读取配置，避免 InvalidCastException 和类型未注册导致按钮消失
            if (!ResourceLoader.Exists(Resc))
            {
                GD.PushWarning($"[ScriptGenerateInspector] 找不到配置资源: {Resc}");
                return;
            }
            VBoxContainer vbox = new VBoxContainer();
            Button m_GenerateButton = new Button();
            m_GenerateButton.Text = "Generate Script";
            vbox.AddChild(m_GenerateButton);
            Button m_DeleteGeButton = new Button();
            m_DeleteGeButton.Text = "Delete Gen";
            m_DeleteGeButton.Modulate = Colors.Red;
            vbox.AddChild(m_DeleteGeButton);
            Button m_DeleteLogicButton = new Button();
            m_DeleteLogicButton.Text = "Delete Logic";
            m_DeleteLogicButton.Modulate = Colors.Red;
            vbox.AddChild(m_DeleteLogicButton);
            m_GenerateButton.Pressed += () => OnGeneratePressed(@object);
            m_DeleteGeButton.Pressed += () => OnDeleteGenPressed(@object);
            m_DeleteLogicButton.Pressed += () => OnDeleteLogicPressed(@object);
            AddCustomControl(vbox);
        }

        private void OnDeleteGenPressed(GodotObject @object)
        {
            if (@object is not Node node) return;

            string className = Sanitize(node.Name);
            if (string.IsNullOrEmpty(className)) return;

            Godot.Resource config = ResourceLoader.Load(Resc);
            if (!ScriptGenerateCore.ResolvePaths(node, config, out string outputDirGe, out _)) return;
            string gePath = outputDirGe + className + ".cs";

            if (!FileAccess.FileExists(gePath))
            {
                GD.PushWarning($"[ScriptGenerateInspector] 文件不存在: {gePath}");
                return;
            }

            ShowConfirmDialog($"确定删除 Generated 脚本？\n{gePath}", () =>
            {
                DirAccess.RemoveAbsolute(gePath);

                // 如果当前节点挂载了该脚本，一并清除引用
                if (node.GetScript().AsGodotObject() is CSharpScript currentScript && currentScript.ResourcePath == gePath)
                {
                    node.SetScript(default);
                    EditorInterface.Singleton.MarkSceneAsUnsaved();
                    GD.Print($"[ScriptGenerateInspector] 已清除节点上的脚本引用: {node.Name}");
                }

                EditorInterface.Singleton.GetResourceFilesystem().Scan();
                GD.Print($"[ScriptGenerateInspector] 已删除: {gePath}");
            });
        }

        private void OnDeleteLogicPressed(GodotObject @object)
        {
            if (@object is not Node node) return;

            string className = Sanitize(node.Name);
            if (string.IsNullOrEmpty(className)) return;

            Godot.Resource config = ResourceLoader.Load(Resc);
            if (!ScriptGenerateCore.ResolvePaths(node, config, out _, out string outputDirLogic)) return;
            string logicPath = outputDirLogic + className + ".Logic.cs";

            if (!FileAccess.FileExists(logicPath))
            {
                GD.PushWarning($"[ScriptGenerateInspector] 文件不存在: {logicPath}");
                return;
            }

            ShowConfirmDialog($"确定删除 Logic 脚本？\n{logicPath}", () =>
            {
                DirAccess.RemoveAbsolute(logicPath);
                EditorInterface.Singleton.GetResourceFilesystem().Scan();
                GD.Print($"[ScriptGenerateInspector] 已删除: {logicPath}");
            });
        }

        private static void ShowConfirmDialog(string message, Action onConfirm)
        {
            var dialog = new ConfirmationDialog();
            dialog.Title = "确认";
            dialog.DialogText = message;
            dialog.Confirmed += () =>
            {
                onConfirm();
                dialog.QueueFree();
            };
            dialog.Canceled += () => dialog.QueueFree();
            EditorInterface.Singleton.GetBaseControl().AddChild(dialog);
            dialog.PopupCentered();
        }

        private void OnGeneratePressed(GodotObject @object)
        {
            if (@object is not Node node) return;

            ShowConfirmDialog("是否生成脚本？\n注意：Node挂载的脚本若为自定义的脚本请不要确认否则会被替换为流程内的脚本", () =>
            {
                GD.Print(ScriptGenerateCore.Generate(node));
            });
        }

        private static string Sanitize(string name)
        {
            if (string.IsNullOrEmpty(name)) return name;
            var sb = new System.Text.StringBuilder(name.Length);
            foreach (char c in name)
            {
                if (char.IsLetterOrDigit(c) || c == '_') sb.Append(c);
            }
            // C# 标识符不能以数字开头
            if (sb.Length > 0 && char.IsDigit(sb[0])) sb.Insert(0, '_');
            return sb.ToString();
        }
    }
}
#endif
