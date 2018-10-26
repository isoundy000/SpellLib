﻿using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEditor.Callbacks;
using System.IO;
using Newtonsoft.Json;
using System.Linq;

public class Tips
{
    EditorWindow ew;
    float lastCheckTime;
    public Tips(EditorWindow ew)
    {
        this.ew = ew;
    }
    List<string> tipList = new List<string>();

    public void AddTips(string tips)
    {
        tips = string.Format("<b><size=12><color=#{0}>{1}</color></size></b>", "eed9a7", tips);
        tipList.Add(tips);
    }

    public void AddError(string tips)
    {
        tips = string.Format("<b><size=12><color=#{0}>{1}</color></size></b>", "ff0000", tips);
        tipList.Add(tips);
    }

    public void OnGui()
    {
        if (tipList.Count == 0)
        {
            lastCheckTime = Time.realtimeSinceStartup;
            return;
        }

        if (Time.realtimeSinceStartup - lastCheckTime > 10)
        {
            lastCheckTime = Time.realtimeSinceStartup;
            tipList.RemoveAt(0);
        }

        for (int i = 0; i < tipList.Count; i++)
        {
            GUI.Label(new Rect(0, ew.position.height - (tipList.Count - i) * 12, 200, 12), tipList[i], Styles.leftLabel);
        }
    }
    public class GraphEditor : EditorWindow
    {
        private static GUILayoutOption[] layoutOptions = new GUILayoutOption[] { GUILayout.MaxWidth(100), GUILayout.ExpandWidth(true), GUILayout.Height(16) };

        Dictionary<Port, Rect> PortPos = new Dictionary<Port, Rect>();

        static Graph graph;

        static string filePath;

        Tips tips;

        Vector2 pan = Vector2.zero;
        float zoom = 1f;
        private const float kZoomMin = 0.4f;
        private const float kZoomMax = 1.5f;
        private const float kSize = 5000f;

        Vector2 Screen2Zoom(Vector2 position)
        {
            return position / zoom + pan;
        }

        Vector2 Zoom2Screen(Vector2 position)
        {
            return (position - pan) * zoom;
        }

        [MenuItem("Tools/SaveEditor %w")] // ctrl + w
        public static void Save()
        {
            Debug.Log("Save");
            if (graph == null)
                return;

            string data = graph.Serialize();
            Util.WriteTextFile(filePath, data);
        }

        void Init()
        {
            this.tips = new Tips(this);
            zoom = 0.4f;
            pan = new Vector2(kSize / 2 - position.width / 2 / zoom, kSize / 2 - position.height / 2 / zoom);
        }

        [OnOpenAssetAttribute(0)]
        public static bool OnOpen(int instanceID, int line)
        {
            string path = AssetDatabase.GetAssetPath(EditorUtility.InstanceIDToObject(instanceID));
            if (path.EndsWith(".ue"))
            {
                filePath = Path.Combine(System.IO.Directory.GetParent(Application.dataPath).FullName, path);
                string data = Util.ReadTextFile(path);
                SerGraph sg = JsonConvert.DeserializeObject<SerGraph>(data);
                graph = new Graph();
                graph.Load(sg);
                var ge = GetWindow<GraphEditor>();
                ge.Init();
                return true;
            }
            return false;
        }
        private static Matrix4x4 _prevGuiMatrix;
        private void OnGUI()
        {
            if (graph == null)
                return;

            DrawGrid();
            HandleZoomAndPan();
            Rect zoomArea = new Rect(0, 0, position.width, position.height);
            EditorZoomArea.Begin(zoom, zoomArea);

            GUILayout.BeginArea(new Rect(-pan.x, -pan.y, kSize, kSize));

            Controls();
            DrawNodes();
            DrawConnections();
            DrawDraggedConnection();
            GUILayout.EndArea();
            EditorZoomArea.End();
            if (PostGUI != null)
            {
                PostGUI();
                PostGUI = null;
            }
            //DrawBlackboard();
            DrawToolbar();
            tips.OnGui();
        }

        #region  postGui
        public static event System.Action PostGUI;
        public static void InvokePostGUI()
        {
            if (PostGUI != null)
            {
                PostGUI();
                PostGUI = null;
            }
        }



        #endregion
        #region Node
        void DrawNodes()
        {
            BeginWindows();
            foreach (var node in graph.Nodes.Values)
            {
                DrawNode(node);
            }
            EndWindows();
        }

        void DrawNode(Node node)
        {
            GUI.color = Color.white;
            var rect = GUILayout.Window(node.ID, new Rect(node.X, node.Y, node.DefaultWidth, node.DefaultHeight),
                (id) => { NodeWindowGUI(id, node); }, string.Empty, CanvasStyles.window);
            node.Rect = rect;

            GUI.Box(rect, string.Empty, CanvasStyles.windowShadow);
            GUI.color = new Color(1, 1, 1, 0.5f);
            GUI.Box(new Rect(rect.x + 6, rect.y + 6, rect.width, rect.height), string.Empty, CanvasStyles.windowShadow);
        }

        void NodeWindowGUI(int id, Node node)
        {
            var text = string.Format("<b><size=12><color=#{0}>{1}</color></size></b>", "eed9a7", node.GetType().ToString());
            var content = new GUIContent(text, "node");
            GUILayout.Label(content, CanvasStyles.nodeTitle, GUILayout.MaxHeight(23));

            int flowCount = node.FlowCount;
            GUILayout.BeginHorizontal();
            {
                GUILayout.BeginVertical();
                {
                    // flowIn
                    var flowIns = node.FlowInDict.Values.ToArray();
                    for (int i = 0; i < flowCount; i++)
                    {
                        if (i < flowIns.Count())
                            GUILayout.Label(string.Format("<b>► {0}</b>", flowIns[i].name), Styles.leftLabel);
                        else
                            GUILayout.Label("");
                    }

                    // valueIn
                    foreach (var valueIn in node.PortValueInDict.Values)
                    {
                        GUILayout.Label(valueIn.name, Styles.leftLabel);
                    }
                }
                GUILayout.EndVertical();
            }

            GUILayout.BeginVertical();
            {
                // flowOut
                var flowOuts = node.FlowOutDict.Values.ToArray();
                for (int i = 0; i < flowCount; i++)
                {
                    if (i < flowOuts.Count())
                        GUILayout.Label(string.Format("<b>{0} ►</b>", flowOuts[i].name), Styles.rightLabel);
                    else
                        GUILayout.Label("");
                }

                // valueOut
                foreach (var valueOut in node.PortValueOutDict.Values)
                {
                    GUILayout.Label(valueOut.name, Styles.rightLabel);
                }

                GUILayout.EndVertical();
            }
            GUILayout.EndHorizontal();

            GUI.DragWindow();
        }
        #endregion

        #region Connection
        void DrawConnections()
        {
            const float startOffset = 35;
            const float portHeight = 18;

            PortPos.Clear();

            foreach (var node in graph.Nodes.Values)
            {
                int flowCount = node.FlowCount;

                var flowIns = node.FlowInDict.Values.ToArray();
                for (int i = 0; i < flowIns.Count(); i++)
                {
                    var port = flowIns[i];
                    Rect rect = new Rect(0, 0, 10, 10);
                    rect.position = new Vector2(node.X - 15, node.Y + i * portHeight + startOffset);
                    PortPos.Add(flowIns[i], rect);
                }

                var flowOuts = node.FlowOutDict.Values.ToArray();
                for (int i = 0; i < flowOuts.Count(); i++)
                {
                    var port = flowOuts[i];
                    Rect rect = new Rect(0, 0, 10, 10);
                    rect.position = new Vector2(node.X + node.Width + 5, node.Y + i * portHeight + startOffset);
                    PortPos.Add(flowOuts[i], rect);
                }

                var valueIns = node.PortValueInDict.Values.ToArray();
                for (int i = 0; i < valueIns.Count(); i++)
                {
                    var port = valueIns[i];
                    Rect rect = new Rect(0, 0, 10, 10);
                    rect.position = new Vector2(node.X - 15, node.Y + (i + flowCount) * portHeight + startOffset);
                    PortPos.Add(valueIns[i], rect);
                }

                var valueOuts = node.PortValueOutDict.Values.ToArray();
                for (int i = 0; i < valueOuts.Count(); i++)
                {
                    var port = valueOuts[i];
                    Rect rect = new Rect(0, 0, 10, 10);
                    rect.position = new Vector2(node.X + node.Width + 5, node.Y + (i + flowCount) * portHeight + startOffset);
                    PortPos.Add(valueOuts[i], rect);
                }
            }

            foreach (var connect in graph.Connections)
            {
                if (PortPos.ContainsKey(connect.sourcePort) && PortPos.ContainsKey(connect.targetPort))
                {
                    var startPos = PortPos[connect.sourcePort].center;
                    var endPos = PortPos[connect.targetPort].center;
                    Color color = connect.connectType == ConnectType.Flow ? new Color(0.5f, 0.5f, 0.8f, 0.8f) : new Color(0.3f, 0.3f, 0.3f, 1f);
                    DrawConnection(startPos, endPos, color);
                }
            }

            foreach (var itr in PortPos)
            {
                GUI.Box(itr.Value, string.Empty, itr.Key.IsConnected ? CanvasStyles.nodePortConnected : CanvasStyles.nodePortEmpty);
            }
        }

        void DrawConnection(Vector2 startPos, Vector2 endPos, Color color)
        {
            var xDiff = (startPos.x - endPos.x) * 0.8f;
            xDiff = startPos.x > endPos.x ? xDiff : -xDiff;
            var tangA = new Vector2(xDiff, 0);
            var tangB = tangA * -1;
            Handles.DrawBezier(startPos, endPos, startPos + tangA, endPos + tangB, color, null, 3);

        }

        void DrawDraggedConnection()
        {
            if (IsDraggingPort)
            {
                var from = PortPos[draggedPort].center;
                var to = Event.current.mousePosition;
                Color color = (draggedPort is FlowIn || draggedPort is FlowOut) ? new Color(0.5f, 0.5f, 0.8f, 0.8f) : new Color(0.3f, 0.3f, 0.3f, 1f);
                DrawConnection(from, to, color);
                Repaint();
            }
        }
        #endregion

        #region Blackboard
        void DrawBlackboard()
        {
            Blackboard blackboard = graph.Blackboard;
            Rect rect = new Rect(position.width - 300, 30, 280, 300);
            GUI.Box(rect, string.Empty, CanvasStyles.windowShadow);
            GUI.color = Color.white;
            GUILayout.BeginArea(rect, "Variable", CanvasStyles.editorPanel);
            GUILayout.Space(5);

            GUI.skin.label.richText = true;
            var e = Event.current;
            GUI.backgroundColor = new Color(0.8f, 0.8f, 1);
            if (GUILayout.Button("Add Variable"))
            {
                blackboard.AddShowData();
                Event.current.Use();
            }
            GUI.backgroundColor = Color.white;

            if (blackboard.DataSource.Count == 0)
            {
                EditorGUILayout.HelpBox("Blackboard has no variables", MessageType.Info);
            }
            else
            {
                GUILayout.BeginHorizontal();
                GUI.color = Color.yellow;
                GUILayout.Label("Name", layoutOptions);
                GUILayout.Label("Value", layoutOptions);
                GUI.color = Color.white;
                GUILayout.EndHorizontal();

                GUILayout.BeginVertical();

                for (int i = blackboard.ShowDataList.Count - 1; i >= 0; i--)
                {
                    var data = blackboard.ShowDataList[i];
                    GUILayout.BeginHorizontal();

                    data.Name = EditorGUILayout.DelayedTextField(data.Name, layoutOptions);

                    data.StrValue = EditorGUILayout.DelayedTextField(data.StrValue, layoutOptions);

                    if (GUILayout.Button("-"))
                    {
                        blackboard.ShowDataList.RemoveAt(i);
                    }

                    GUILayout.EndHorizontal();
                }
                GUILayout.EndVertical();

            }

            GUILayout.EndArea();
        }
        #endregion

        #region Background
        void DrawGrid()
        {
            if (Event.current.type != EventType.Repaint)
            {
                return;
            }

            Handles.color = new Color(0, 0, 0, 0.15f);

            var drawGridSize = 35;
            var step = drawGridSize * zoom;

            float xStart = 0;
            float xEnd = position.width - 0;
            for (var i = xStart; i < xEnd; i += step)
            {
                Handles.DrawLine(new Vector3(i, 0, 0), new Vector3(i, position.height, 0));
            }

            float yStart = 0f;
            float yEnd = position.height;
            for (var i = yStart; i < yEnd; i += step)
            {
                Handles.DrawLine(new Vector3(0, i, 0), new Vector3(position.width, i, 0));
            }
        }
        #endregion

        #region Toolbar
        void DrawToolbar()
        {
            if (GUILayout.Button("Save", EditorStyles.toolbarButton, GUILayout.Width(50)))
            {
                Save();
            }
        }
        #endregion

        #region Controls
        private bool IsDraggingNode { get { return draggedNode != null; } }
        private bool IsDraggingPort { get { return draggedPort != null; } }

        private Node draggedNode = null;
        private Port draggedPort = null;

        /*
         s = 屏幕坐标
         z = 缩放
         p = 平移
         x = zoom坐标
        (x - p) * z = s
         */
        public void SetPan(Vector2 newPan)
        {
            pan.x = Mathf.Clamp(newPan.x, 0, kSize - position.width / zoom);
            pan.y = Mathf.Clamp(newPan.y, 0, kSize - position.height / zoom);
        }

        /*
        x = zoom坐标
        z = 缩放前
        z1 = 缩放后
        p = 平移前
        p1 = 平移后
        s = 屏幕坐标
        (x - p) * z = s
        (x - p1) * z1 = s
        x = s / z + p
        p1 = s / z - s / z1 + p
         * */
        public void HandleZoomAndPan()
        {
            Event e = Event.current;
            switch (e.type)
            {
                case EventType.ScrollWheel:
                    float oldZoom = zoom;
                    zoom += 0.1f * (e.delta.y > 0 ? -1 : 1) * zoom;
                    zoom = Mathf.Clamp(zoom, kZoomMin, kZoomMax);
                    pan = (e.mousePosition / oldZoom) - e.mousePosition / zoom + pan;
                    SetPan(pan);
                    Event.current.Use();
                    break;
                case EventType.MouseDrag:
                    if (e.button == 1 || e.button == 2)
                    {
                        pan -= e.delta;
                        SetPan(pan);
                        Repaint();
                    }
                    break;
            }
        }

        public void Controls()
        {
            Event e = Event.current;
            switch (e.type)
            {
                case EventType.MouseDrag:
                    if (e.button == 0)
                    {
                        if (IsDraggingNode)
                        {
                            draggedNode.X += (int)e.delta.x;
                            draggedNode.Y += (int)e.delta.y;
                        }
                    }
                    break;
                case EventType.MouseDown:
                    if (e.button == 0)
                    {
                        foreach (var node in graph.Nodes.Values)
                        {
                            if (node.Rect.Contains(e.mousePosition))
                            {
                                draggedNode = node;
                                return;
                            }
                        }

                        foreach (var itr in PortPos)
                        {
                            if (itr.Value.Contains(e.mousePosition))
                            {
                                draggedPort = itr.Key;
                                return;
                            }
                        }
                    }
                    else if (e.button == 1)
                    {
                        foreach (var itr in PortPos)
                        {
                            if (itr.Value.Contains(e.mousePosition))
                            {
                                foreach (var connect in itr.Key.Connections.ToArray())
                                {
                                    connect.sourcePort.Connections.Remove(connect);
                                    connect.targetPort.Connections.Remove(connect);

                                    graph.Connections.Remove(connect);
                                }
                                Repaint();
                                break;
                            }
                        }
                    }
                    break;
                case EventType.MouseUp:
                    if (e.button == 0)
                    {
                        if (IsDraggingPort)
                        {
                            foreach (var itr in PortPos)
                            {
                                if (itr.Value.Contains(e.mousePosition))
                                {
                                    var port = itr.Key;
                                    if (CheckConnectValid(draggedPort, itr.Key))
                                    {
                                        graph.CreateConnection(draggedPort, itr.Key);
                                        Repaint();
                                    }
                                    break;
                                }
                            }
                        }
                        draggedNode = null;
                        draggedPort = null;
                    }
                    else if (e.button == 1)
                    {
                        foreach (var node in graph.Nodes.Values)
                        {
                            if (node.Rect.Contains(e.mousePosition))
                            {
                                ShowNodeContextMenu(node);
                                break;
                            }
                        }
                    }
                    break;
            }
        }
        #endregion

        #region contextMenu
        void ShowNodeContextMenu(Node node)
        {
            GenericMenu contextMenu = new GenericMenu();
            contextMenu.AddItem(new GUIContent("Duplicate"), false, () =>
            {
                var clone = graph.CopyNode(node);
                clone.X = node.X + 30; clone.Y = node.Y + 30;
            });
            contextMenu.AddItem(new GUIContent("Remove"), false, () =>
            {
                graph.RemoveNode(node);

                node.FlowInDict.Values.ToList().ForEach((x) => PortPos.Remove(x));
                node.FlowOutDict.Values.ToList().ForEach((x) => PortPos.Remove(x));
                node.PortValueInDict.Values.ToList().ForEach((x) => PortPos.Remove(x));
                node.PortValueOutDict.Values.ToList().ForEach((x) => PortPos.Remove(x));
            });
            PostGUI += () => { contextMenu.ShowAsContext(); };
        }
        #endregion

        int GetPortValue(Port port)
        {
            if (port is FlowIn)
                return 1;
            else if (port is FlowOut)
                return 2;
            else if (port is ValueIn)
                return 3;
            else if (port is ValueOut)
                return 4;

            return 0;
        }

        bool CheckConnectValid(Port port1, Port port2)
        {
            if (((port1 is ValueIn) && port1.Connections.Count > 0) || ((port2 is ValueIn) && port2.Connections.Count > 0))
            {
                tips.AddError("输入口不能有多个连接");
                return false;
            }

            if (port1.Connections.Intersect(port2.Connections).Count() > 0)
            {
                tips.AddError("已连接");
                return false;
            }

            if (port1.node == port2.node)
            {
                tips.AddError("同一个节点不能相接");
                return false;
            }


            int result = GetPortValue(port1) + GetPortValue(port2);
            Debug.Log(result);
            if (result != 3 && result != 7)
            {
                tips.AddError("端口类型不一致");
                return false;
            }
            return true;
        }
    }
}