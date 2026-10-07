#if TOOLS
using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace GodotGameFramework.Editor
{
	/// <summary>
	/// UIForm / Entity 脚本生成核心（等效 ScriptGenerateInspector 的 Generate Script 按钮）。
	/// Inspector 按钮与命令行工具（ScriptGenerateTool.gd）共用，避免生成逻辑两处漂移。
	/// 生成：读模板 + ScriptGenerateRes 配置 → 写 Ge（覆盖）/Logic（仅首次）→ 挂脚本 → 绑定 m_ 子节点。
	/// </summary>
	public static class ScriptGenerateCore
	{
		const string UI_SCRIPT_TEMPLATE = "res://Framework/GodotGameFrameworkCore/Templet/UIFormTemplet.txt";
		const string UI_LOGIC_TEMPLATE = "res://Framework/GodotGameFrameworkCore/Templet/UIFormLogicTemplet.txt";
		const string ENTITY_SCRIPT_TEMPLATE = "res://Framework/GodotGameFrameworkCore/Templet/EntityTemplet.txt";
		const string ENTITY_LOGIC_TEMPLATE = "res://Framework/GodotGameFrameworkCore/Templet/EntityLogicTemplet.txt";
		const string Resc = "res://TheGame/MainPack/Resources/ScriptGenerateRes.tres";
		const string NameSpaceReplace = "_NAMESPACE_";
		const string ParentClassReplace = "_PARENT_";
		const string ClassNameReplace = "_CLASSNAME_";
		const string DefaultNameSpace = "GameLogic";
		const string DefaultOutputPath = "res://TheGame/";
		const string ChildNodes = "_CHILDNODES_";

		/// <summary>
		/// 为节点生成 Ge/Logic 并挂载绑定（等效 Inspector 的 Generate Script 按钮）。
		/// 返回结果日志文本。
		/// </summary>
		public static string Generate(Node node)
		{
			if (node == null) return "[ScriptGenerate] 节点为空。";

			string parent = node.GetType().Name;
			string className = Sanitize(node.Name);
			if (string.IsNullOrEmpty(className))
			{
				return "[ScriptGenerate] 节点名称无法转换为合法的类名。";
			}
			// 已挂生成脚本的节点重新生成时，父类回退引擎基类，避免 class X : X
			if (parent == className)
			{
				parent = node is Control ? "Control" : node is Node3D ? "Node3D" : "Node2D";
			}

			Godot.Resource config = ResourceLoader.Load(Resc);
			if (config == null)
			{
				return $"[ScriptGenerate] 找不到配置资源: {Resc}";
			}
			if (!ResolvePaths(node, config, out string outputDirGe, out string outputDirLogic))
			{
				return $"[ScriptGenerate] 不支持的节点类型: {parent}";
			}

			string geTemplate;
			string logicTemplate;
			string namespaceStr = ReadProp(config, ScriptGenerateRes.Parameters.NameSpace, DefaultNameSpace);
			if (node is Control)
			{
				geTemplate = ReadText(UI_SCRIPT_TEMPLATE);
				logicTemplate = ReadText(UI_LOGIC_TEMPLATE);
			}
			else // Node2D or Node3D（已由 ResolvePaths 校验类型）
			{
				geTemplate = ReadText(ENTITY_SCRIPT_TEMPLATE);
				logicTemplate = ReadText(ENTITY_LOGIC_TEMPLATE);
			}

			if (geTemplate == null) return "[ScriptGenerate] Ge 模板缺失，无法生成。";

			// 生成部分（Ge）：包含框架样板代码，每次都覆盖重写
			var prs = new Dictionary<string, string>();
			var matchingChildren = new List<Node>();
			string geScript = geTemplate
				.Replace(NameSpaceReplace, namespaceStr)
				.Replace(ParentClassReplace, parent)
				.Replace(ClassNameReplace, className)
				.Replace(ChildNodes, ReadChildNodes(node, config, prs, matchingChildren));
			string gePath = outputDirGe + className + ".cs"; // Godot只有文件名与类名相同才可显示在Inspector上
			if (!WriteText(gePath, geScript)) return $"[ScriptGenerate] 写入失败: {gePath}";

			// 逻辑部分（Logic）：用户业务代码，仅在首次生成时创建，避免覆盖已有逻辑
			string logicPath = outputDirLogic + className + ".Logic.cs";
			if (!FileAccess.FileExists(logicPath))
			{
				if (logicTemplate != null)
				{
					string logicScript = logicTemplate
						.Replace(NameSpaceReplace, namespaceStr)
						.Replace(ClassNameReplace, className);
					WriteText(logicPath, logicScript);
				}
			}

			// 刷新文件系统，让 Godot 识别新生成或更新的脚本文件
			var fs = EditorInterface.Singleton.GetResourceFilesystem();
			fs.UpdateFile(gePath);
			if (FileAccess.FileExists(logicPath)) fs.UpdateFile(logicPath);
			fs.Scan();

			// 加载 CSharpScript 资源并赋值给节点
			var script = GD.Load<CSharpScript>(gePath);
			if (script != null)
			{
				node.SetScript(script);

				// 自动赋值子节点到 [Export] 字段
				foreach (var child in matchingChildren)
				{
					node.Set(child.Name, child);
				}

				// 标记场景为已修改（headless 命令行下由调用方 EditorSceneSaver 保存）
				EditorInterface.Singleton.MarkSceneAsUnsaved();
				return $"[ScriptGenerate] 已生成并赋值脚本: {gePath}";
			}

			return $"[ScriptGenerate] 脚本已生成，但无法加载（请重新构建后重试）: {gePath}";
		}

		/// <summary>
		/// 根据节点类型解析 Ge / Logic 输出目录（Control→UI，Node2D/Node3D→Entity），并保证以 "/" 结尾。
		/// </summary>
		public static bool ResolvePaths(Node node, Godot.Resource config, out string geDir, out string logicDir)
		{
			geDir = null;
			logicDir = null;
			if (node is Control)
			{
				geDir = ReadProp(config, ScriptGenerateRes.Parameters.UIOutPutPathGe, DefaultOutputPath);
				logicDir = ReadProp(config, ScriptGenerateRes.Parameters.UIOutPutPathLogic, DefaultOutputPath);
			}
			else if (node is Node2D or Node3D)
			{
				geDir = ReadProp(config, ScriptGenerateRes.Parameters.EntityOutPutPathGe, DefaultOutputPath);
				logicDir = ReadProp(config, ScriptGenerateRes.Parameters.EntityOutPutPathLogic, DefaultOutputPath);
			}
			else
			{
				return false;
			}
			if (!geDir.EndsWith("/")) geDir += "/";
			if (!logicDir.EndsWith("/")) logicDir += "/";
			return true;
		}

		private static string ReadChildNodes(Node node, Godot.Resource config,
			Dictionary<string, string> prs, List<Node> matchingChildren)
		{
			string prefix = ReadProp(config, ScriptGenerateRes.Parameters.NodePrefix, "m_");
			foreach (var child in node.GetChildren())
			{
				if (child.GetChildCount() > 0)
				{
					ReadChildNodes(child, config, prs, matchingChildren);
				}
				if (child.Name.ToString().StartsWith(prefix))
				{
					if (!prs.ContainsKey(child.Name))
					{
						prs.Add(child.Name, child.GetType().Name);
						matchingChildren.Add(child);
					}
					else
					{
						GD.PushWarning($"[ScriptGenerate] {child.Name}重复");
					}
				}
			}
			return string.Join("\n", prs.Select(x => $"\t\t[Export]\n\t\tprivate {x.Value} {x.Key};"));
		}

		private static string ReadProp(Godot.Resource res, string prop, string fallback)
		{
			if (res == null) return fallback;
			string value = res.Get(prop).AsString();
			return string.IsNullOrEmpty(value) ? fallback : value;
		}

		private static string ReadText(string path)
		{
			using var file = FileAccess.Open(path, FileAccess.ModeFlags.Read);
			if (file == null)
			{
				GD.PushError($"[ScriptGenerate] 无法读取模板: {path} ({FileAccess.GetOpenError()})");
				return null;
			}
			return file.GetAsText();
		}

		private static bool WriteText(string path, string content)
		{
			// 检测目标文件所在文件夹是否存在，不存在创建
			string globalPath = ProjectSettings.GlobalizePath(path);
			string dir = System.IO.Path.GetDirectoryName(globalPath);
			if (!string.IsNullOrEmpty(dir) && !System.IO.Directory.Exists(dir))
			{
				System.IO.Directory.CreateDirectory(dir);
			}
			using var file = FileAccess.Open(path, FileAccess.ModeFlags.Write);
			if (file == null)
			{
				GD.PushError($"[ScriptGenerate] 无法写入文件: {path} ({FileAccess.GetOpenError()})");
				return false;
			}
			file.StoreString(content);
			return true;
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
