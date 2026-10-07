#if TOOLS
using Godot;
using System;
namespace GodotGameFramework.Editor
{
	[Tool]
	public partial class ComponentInsoector : EditorPlugin
	{
		ProcedureComponentInspectorPlugin m_ProcedureComponent;
		BaseComponentInspectorPlugin m_BaseComponent;
		SceneComponentInspectorPlugin m_SceneComponent;
		SettingComponentInspectorPlugin m_SettingComponent;
		EntityComponentInspectorPlugin m_EntityComponent;
		UIComponentInspectorPlugin m_UIComponent;
		SoundComponentInspectorPlugin m_SoundComponent;
		LocalizationComponentInspectorPlugin m_LocalizationComponent;
		DownloadComponentInspectorPlugin m_DownloadComponent;
		WebRequestComponentInspectorPlugin m_WebRequestComponent;
		ResourceComponentInspectorPlugin m_ResourceComponent;
		ScriptGenerateInspector m_ScriptGenerateInspector;
		NodePoolInspectorPlugin m_NodePoolInspector;
		ArchiveSettingInspectorPlugin m_ArchiveSettingInspector;
		public override void _EnterTree()
		{
			m_ProcedureComponent = new ProcedureComponentInspectorPlugin();
			m_BaseComponent = new BaseComponentInspectorPlugin();
			m_SceneComponent = new SceneComponentInspectorPlugin();
			m_SettingComponent = new SettingComponentInspectorPlugin();
			m_EntityComponent = new EntityComponentInspectorPlugin();
			m_UIComponent = new UIComponentInspectorPlugin();
			m_SoundComponent = new SoundComponentInspectorPlugin();
			m_LocalizationComponent = new LocalizationComponentInspectorPlugin();
			m_DownloadComponent = new DownloadComponentInspectorPlugin();
			m_WebRequestComponent = new WebRequestComponentInspectorPlugin();
			m_ResourceComponent = new ResourceComponentInspectorPlugin();
			m_ScriptGenerateInspector = new ScriptGenerateInspector();
			m_NodePoolInspector = new NodePoolInspectorPlugin();
			m_ArchiveSettingInspector = new ArchiveSettingInspectorPlugin();
			AddInspectorPlugin(m_BaseComponent);
			AddInspectorPlugin(m_ProcedureComponent);
			AddInspectorPlugin(m_SceneComponent);
			AddInspectorPlugin(m_SettingComponent);
			AddInspectorPlugin(m_EntityComponent);
			AddInspectorPlugin(m_UIComponent);
			AddInspectorPlugin(m_SoundComponent);
			AddInspectorPlugin(m_LocalizationComponent);
			AddInspectorPlugin(m_DownloadComponent);
			AddInspectorPlugin(m_WebRequestComponent);
			AddInspectorPlugin(m_ResourceComponent);
			AddInspectorPlugin(m_ScriptGenerateInspector);
			AddInspectorPlugin(m_NodePoolInspector);
			AddInspectorPlugin(m_ArchiveSettingInspector);

			// headless 命令行生成工具入口（等效 Generate Script 按钮）：
			//   godot --headless --editor --path GodotProject -- --script-generate --scene=res://TheGame/UIs/XxxForm.tscn --node=XxxForm
			TryRunGenerateFromCommandLine();
		}

		/// <summary>
		/// 检测命令行参数 --script-generate：生成 Ge/Logic（复用 <see cref="ScriptGenerateCore"/>）、
		/// 挂脚本绑定 m_ 子节点并保存场景，然后退出编辑器。headless 下 EditorScript 无法用 -s 运行，
		/// 故走插件启动路径。
		/// </summary>
		private void TryRunGenerateFromCommandLine()
		{
			var args = new System.Collections.Generic.Dictionary<string, string>();
			foreach (string a in OS.GetCmdlineUserArgs())
			{
				if (!a.StartsWith("--"))
				{
					continue;
				}
				var kv = a.Split("=", 2);
				args[kv[0]] = kv.Length == 2 ? kv[1] : "";
			}
			if (!args.ContainsKey("--script-generate"))
			{
				return;
			}

			if (!args.TryGetValue("--scene", out string scenePath) || !args.TryGetValue("--node", out string nodePath))
			{
				GD.PushError("[ScriptGenerate] 用法: --script-generate --scene=res://xxx.tscn --node=NodePath");
				GetTree().Quit(1);
				return;
			}

			var packed = GD.Load<PackedScene>(scenePath);
			if (packed == null)
			{
				GD.PushError($"[ScriptGenerate] 场景加载失败: {scenePath}");
				GetTree().Quit(1);
				return;
			}

			var root = packed.Instantiate();
			// --node 支持 "." / 空 / 根节点名（目标即根自身），否则按相对根的子节点路径查找
			var node = (nodePath == "" || nodePath == "." || nodePath == root.Name)
				? root
				: root.GetNodeOrNull<Node>(nodePath);
			if (node == null)
			{
				GD.PushError($"[ScriptGenerate] 找不到节点: {nodePath}");
				root.Free();
				GetTree().Quit(1);
				return;
			}

			string result = ScriptGenerateCore.Generate(node);
			GD.Print(result);

			// 生成成功后保存场景（headless 下无"当前打开场景"，重新 Pack + 落盘）
			if (result.Contains("已生成并赋值脚本"))
			{
				var saved = new PackedScene();
				saved.Pack(root);
				Error err = ResourceSaver.Save(saved, scenePath);
				GD.Print($"[ScriptGenerate] 场景已保存: {scenePath} ({err})");
			}

			root.Free();
			GetTree().Quit(0);
		}

		public override void _ExitTree()
		{
			RemoveInspectorPlugin(m_ProcedureComponent);
			RemoveInspectorPlugin(m_BaseComponent);
			RemoveInspectorPlugin(m_SceneComponent);
			RemoveInspectorPlugin(m_SettingComponent);
			RemoveInspectorPlugin(m_EntityComponent);
			RemoveInspectorPlugin(m_UIComponent);
			RemoveInspectorPlugin(m_SoundComponent);
			RemoveInspectorPlugin(m_LocalizationComponent);
			RemoveInspectorPlugin(m_DownloadComponent);
			RemoveInspectorPlugin(m_WebRequestComponent);
			RemoveInspectorPlugin(m_ResourceComponent);
			RemoveInspectorPlugin(m_ScriptGenerateInspector);
			RemoveInspectorPlugin(m_NodePoolInspector);
			RemoveInspectorPlugin(m_ArchiveSettingInspector);
			// 注意：InspectorPlugin 子类为 RefCounted，交由 Godot 自动释放，不调用 Free()
		}
	}
}
#endif
