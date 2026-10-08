using System;
using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace DExportExcels.Editor
{
    /// <summary>
    /// 快捷键/菜单打开导表工具(exe 或 .py), 路径记忆在 EditorPrefs
    /// </summary>
    public static class TableToolLauncher
    {
        private const string PrefKey = "DExportExcels.ToolPath";
        private const string MenuRoot = "读表工具/";

        [MenuItem(MenuRoot + "打开导表工具 %#T", false, 0)]   // Ctrl+Shift+T
        public static void Launch()
        {
            string path = EditorPrefs.GetString(PrefKey, "");
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                path = PickToolPath();
                if (string.IsNullOrEmpty(path)) return;
                EditorPrefs.SetString(PrefKey, path);
            }
            LaunchProcess(path);
        }

        [MenuItem(MenuRoot + "配置导表工具路径", false, 1)]
        public static void ConfigPath()
        {
            string path = PickToolPath();
            if (string.IsNullOrEmpty(path)) return;
            EditorPrefs.SetString(PrefKey, path);
            UnityEngine.Debug.Log("[Table] 导表工具路径已配置: " + path);
        }

        private static string PickToolPath()
        {
            string path = EditorUtility.OpenFilePanel(
                "选择导表工具(打包 exe 或 Main.py)", "", "exe,py");
            return string.IsNullOrEmpty(path) ? "" : path;
        }

        private static void LaunchProcess(string path)
        {
            EditorPrefs.SetString(PrefKey, path);
            if (path.EndsWith(".py", StringComparison.OrdinalIgnoreCase))
            {
                // .py: 用系统 python 启动, 工作目录设为其所在目录(保证 projectConfig.json 读取正确)
                Process.Start(new ProcessStartInfo
                {
                    FileName = "python",
                    Arguments = '"' + path + '"',
                    WorkingDirectory = Path.GetDirectoryName(path),
                    UseShellExecute = true
                });
            }
            else
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = path,
                    WorkingDirectory = Path.GetDirectoryName(path),
                    UseShellExecute = true
                });
            }
        }
    }
}
