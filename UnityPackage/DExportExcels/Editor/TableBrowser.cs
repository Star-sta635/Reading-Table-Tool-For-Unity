using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace DExportExcels.Editor
{
    /// <summary>
    /// 表数据浏览器: 左侧表列表(带行数), 右侧数据表格, 关键字即时过滤
    /// 数据来源: 数据目录的 .bytes 经生成的 TableManager.LoadAll 解析(复用 schema 校验),
    ///           反射读取全部表管理器的行数据 —— 任何表无需为浏览器写配合代码
    /// </summary>
    public class TableBrowser : EditorWindow
    {
        private class TableData
        {
            public string Name;
            public string[] Columns = new string[0];
            public readonly List<string[]> Rows = new List<string[]>();
            public string Error;                       // 非空 = 该表加载失败原因
        }

        private const string DirPrefKey = "DExportExcels.Browser.DataDir";
        private const int MaxVisibleRows = 500;        // OnGUI 限流, 超出提示用搜索过滤

        private string _dataDir = "";
        private string _search = "";
        private string _selected = "";
        private Vector2 _leftScroll;
        private Vector2 _rightScroll;
        private readonly List<TableData> _tables = new List<TableData>();
        private bool _loaded;

        [MenuItem("读表工具/表数据浏览器", false, 10)]
        public static void Open()
        {
            GetWindow<TableBrowser>("表浏览器");
        }

        private void OnEnable()
        {
            _dataDir = EditorPrefs.GetString(DirPrefKey, "");
            if (string.IsNullOrEmpty(_dataDir))
            {
                _dataDir = AutoScanDir();
                if (!string.IsNullOrEmpty(_dataDir)) Refresh();
            }
        }

        // 自动扫描 Assets 下带 TBL\0 头的 .bytes 最集中的目录作为默认
        private static string AutoScanDir()
        {
            var counter = new Dictionary<string, int>();
            foreach (string guid in AssetDatabase.FindAssets("t:TextAsset", new[] { "Assets" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.EndsWith(".bytes", StringComparison.OrdinalIgnoreCase)) continue;
                try
                {
                    using (FileStream fs = File.OpenRead(path))
                    {
                        var head = new byte[4];
                        if (fs.Read(head, 0, 4) != 4) continue;
                        if (head[0] != (byte)'T' || head[1] != (byte)'B'
                            || head[2] != (byte)'L' || head[3] != 0) continue;
                    }
                }
                catch (IOException)
                {
                    continue;
                }
                string dir = Path.GetDirectoryName(path).Replace('\\', '/');
                counter[dir] = counter.ContainsKey(dir) ? counter[dir] + 1 : 1;
            }
            int best = -1;
            string bestDir = "";
            foreach (var kv in counter)
            {
                if (kv.Value > best) { best = kv.Value; bestDir = kv.Key; }
            }
            return bestDir;
        }

        private void Refresh()
        {
            _tables.Clear();
            _loaded = false;
            _selected = "";
            if (string.IsNullOrEmpty(_dataDir) || !Directory.Exists(_dataDir))
            {
                Repaint();
                return;
            }

            var datas = new Dictionary<string, byte[]>();
            foreach (string file in Directory.GetFiles(_dataDir, "*.bytes"))
            {
                datas[Path.GetFileNameWithoutExtension(file)] = File.ReadAllBytes(file);
            }
            TableManager.LoadAll(datas);          // 复用生成的解析与 schema 校验

            object inst = TableManager.Instance;
            foreach (FieldInfo field in inst.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                object mgr = field.GetValue(inst);
                if (mgr == null) continue;
                var td = new TableData { Name = field.Name };
                BuildTable(mgr, td);
                _tables.Add(td);
            }
            _tables.Sort((a, b) => string.CompareOrdinal(a.Name, b.Name));
            _loaded = true;
            if (_selected == "" && _tables.Count > 0) _selected = _tables[0].Name;
            EditorPrefs.SetString(DirPrefKey, _dataDir);
            Repaint();
        }

        private static void BuildTable(object mgr, TableData td)
        {
            try
            {
                IEnumerable rows = null;
                PropertyInfo listProp = mgr.GetType().GetProperty("List");
                PropertyInfo dictProp = mgr.GetType().GetProperty("Dict");
                if (listProp != null)
                {
                    rows = listProp.GetValue(mgr, null) as IEnumerable;
                }
                else if (dictProp != null)
                {
                    object dict = dictProp.GetValue(mgr, null);
                    PropertyInfo valuesProp = dict.GetType().GetProperty("Values");
                    rows = valuesProp.GetValue(dict, null) as IEnumerable;
                }
                if (rows == null) { td.Error = "无行集合(List/Dict)"; return; }

                FieldInfo[] cols = null;
                foreach (object row in rows)
                {
                    if (cols == null)
                    {
                        cols = row.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance);
                        td.Columns = cols.Select(c => c.Name).ToArray();
                    }
                    var cells = new string[cols.Length];
                    for (int i = 0; i < cols.Length; i++)
                    {
                        cells[i] = FormatValue(cols[i].GetValue(row));
                    }
                    td.Rows.Add(cells);
                }
                if (td.Columns == null) { td.Columns = new string[0]; td.Error = "空表"; }
            }
            catch (Exception e)
            {
                td.Error = e.Message;
            }
        }

        private static string FormatValue(object v)
        {
            if (v == null) return "null";
            if (v is string s) return s;
            if (v is DateTime dt) return dt.ToString("yyyy-MM-dd HH:mm:ss");
            if (v is IEnumerable en)
            {
                var sb = new StringBuilder();
                foreach (object item in en)
                {
                    if (sb.Length > 0) sb.Append("; ");
                    sb.Append(item);
                }
                return sb.ToString();
            }
            return v.ToString();
        }

        // ==================== UI ====================
        private void OnGUI()
        {
            DrawToolbar();
            using (new EditorGUILayout.HorizontalScope())
            {
                DrawLeftPanel();
                DrawRightPanel();
            }
            DrawStatusBar();
        }

        private void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                GUILayout.Label("数据目录:", EditorStyles.miniLabel);
                _dataDir = EditorGUILayout.TextField(
                    _dataDir, EditorStyles.toolbarTextField, GUILayout.MinWidth(220));
                if (GUILayout.Button("选择", EditorStyles.toolbarButton, GUILayout.Width(44)))
                {
                    string pick = EditorUtility.OpenFolderPanel("选择数据目录", _dataDir, "");
                    if (!string.IsNullOrEmpty(pick))
                    {
                        if (pick.StartsWith(Application.dataPath))
                        {
                            pick = "Assets" + pick.Substring(Application.dataPath.Length);
                        }
                        _dataDir = pick.Replace('\\', '/');
                        Refresh();
                    }
                }
                if (GUILayout.Button("刷新", EditorStyles.toolbarButton, GUILayout.Width(40)))
                {
                    Refresh();
                }
                GUILayout.FlexibleSpace();
                _search = EditorGUILayout.TextField(
                    _search, EditorStyles.toolbarSearchField, GUILayout.Width(200));
            }
        }

        private void DrawLeftPanel()
        {
            using (new EditorGUILayout.VerticalScope(GUILayout.Width(150), GUILayout.ExpandHeight(true)))
            {
                _leftScroll = EditorGUILayout.BeginScrollView(_leftScroll);
                foreach (TableData td in _tables)
                {
                    string label = td.Name + "  (" + td.Rows.Count + ")"
                                   + (td.Error != null ? "  !" : "");
                    bool selected = _selected == td.Name;
                    Color old = GUI.color;
                    GUI.color = selected ? Color.white : new Color(1f, 1f, 1f, 0.65f);
                    if (GUILayout.Button(label, EditorStyles.miniButton))
                    {
                        _selected = td.Name;
                    }
                    GUI.color = old;
                }
                EditorGUILayout.EndScrollView();
            }
        }

        private void DrawRightPanel()
        {
            TableData td = _tables.FirstOrDefault(t => t.Name == _selected);
            using (new EditorGUILayout.VerticalScope(
                GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true)))
            {
                if (td == null)
                {
                    EditorGUILayout.HelpBox("左侧选择一张表", MessageType.Info);
                    return;
                }
                if (td.Error != null)
                {
                    EditorGUILayout.HelpBox("加载失败: " + td.Error, MessageType.Warning);
                    return;
                }
                if (td.Columns.Length == 0)
                {
                    EditorGUILayout.HelpBox("空表(无字段)", MessageType.Info);
                    return;
                }

                List<string[]> visible = FilterRows(td);
                _rightScroll = EditorGUILayout.BeginScrollView(_rightScroll);

                using (new EditorGUILayout.HorizontalScope(GUI.skin.box))
                {
                    foreach (string col in td.Columns)
                    {
                        GUILayout.Box(col, GUILayout.MinWidth(90), GUILayout.MaxWidth(260));
                    }
                }

                int shown = 0;
                foreach (string[] row in visible)
                {
                    if (shown >= MaxVisibleRows)
                    {
                        EditorGUILayout.HelpBox(
                            "已截断显示前 " + MaxVisibleRows + " 行(共 " + visible.Count
                            + " 行), 请用搜索过滤", MessageType.Info);
                        break;
                    }
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        foreach (string cell in row)
                        {
                            EditorGUILayout.LabelField(
                                cell ?? "", GUILayout.MinWidth(90), GUILayout.MaxWidth(260));
                        }
                    }
                    shown++;
                }
                EditorGUILayout.EndScrollView();

                GUILayout.Label(
                    "显示 " + shown + " / " + visible.Count + " 行(总 " + td.Rows.Count + " 行)",
                    EditorStyles.miniLabel);
            }
        }

        private List<string[]> FilterRows(TableData td)
        {
            if (string.IsNullOrEmpty(_search)) return td.Rows;
            var result = new List<string[]>();
            foreach (string[] row in td.Rows)
            {
                foreach (string cell in row)
                {
                    if (cell != null && cell.IndexOf(_search, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        result.Add(row);
                        break;
                    }
                }
            }
            return result;
        }

        private void DrawStatusBar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                GUILayout.Label(
                    "目录: " + (string.IsNullOrEmpty(_dataDir) ? "(未配置)" : _dataDir),
                    EditorStyles.miniLabel);
                GUILayout.FlexibleSpace();
                int errCount = _tables.Count(t => t.Error != null);
                GUILayout.Label(
                    "表 " + _tables.Count + "  错误表 " + errCount,
                    EditorStyles.miniLabel);
            }
        }
    }
}
