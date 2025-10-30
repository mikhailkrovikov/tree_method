using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Controls.Primitives;
 
using Microsoft.Msagl.Drawing;
using Microsoft.Msagl.GraphViewerGdi;
using System.Windows.Forms.Integration;
using System.Windows.Forms;
using TreeMethod.Models;
using ModelNode = TreeMethod.Models.Node;
using Color = Microsoft.Msagl.Drawing.Color;

namespace TreeMethod.Views
{
    public partial class TreePage : Page
    {
        private readonly TreeModel _tree = ProjectData.CurrentTree;
 
        private GViewer _viewer;
        private System.Windows.Forms.ContextMenuStrip _contextMenuStrip;
        private bool _isConnecting = false;
        private int? _connectSourceId = null;
        private int? _nodeUnderCursorId = null;
        
        public TreePage()
        {
            InitializeComponent();

            if (_tree.Nodes.Count == 0)
                _tree.Nodes.Add(new ModelNode { Id = 0, Name = "Система", Type = NodeType.And });

            Focusable = true;
            Loaded += (_, __) => Keyboard.Focus(this);
            PreviewKeyDown += OnPreviewKeyDown;
            InitializeViewer();
            BuildAndDrawGraphMsagl();
        }
        
        private void InitializeViewer()
        {
            _viewer = new GViewer
            {
                EdgeInsertButtonVisible = true,
                NavigationVisible = true,
                LayoutEditingEnabled = true,
                BackColor = System.Drawing.Color.White,
                InsertingEdge = false,
                // Удалены несуществующие свойства
                LayoutAlgorithmSettingsButtonVisible = true,
                UndoRedoButtonsVisible = true,
                PanButtonPressed = true
            };
            
            // Создаем контекстное меню WinForms для самого GViewer
            _contextMenuStrip = new System.Windows.Forms.ContextMenuStrip();

            var addChildItem = new System.Windows.Forms.ToolStripMenuItem("Добавить потомка");
            addChildItem.Click += (s, e) =>
            {
                var id = GetTargetNodeId();
                if (id.HasValue) AddChildMsagl(id.Value);
            };

            var renameItem = new System.Windows.Forms.ToolStripMenuItem("Переименовать");
            renameItem.Click += (s, e) =>
            {
                var id = GetTargetNodeId();
                if (id.HasValue) RenameNodeMsagl(id.Value);
            };

            var changeTypeMenu = new System.Windows.Forms.ToolStripMenuItem("Изменить тип");
            var typeAndItem = new System.Windows.Forms.ToolStripMenuItem("AND");
            typeAndItem.Click += (s, e) =>
            {
                var id = GetTargetNodeId();
                if (id.HasValue) ChangeNodeType(NodeType.And);
            };
            var typeOrItem = new System.Windows.Forms.ToolStripMenuItem("OR");
            typeOrItem.Click += (s, e) =>
            {
                var id = GetTargetNodeId();
                if (id.HasValue) ChangeNodeType(NodeType.Or);
            };
            var typeLeafItem = new System.Windows.Forms.ToolStripMenuItem("LEAF");
            typeLeafItem.Click += (s, e) =>
            {
                var id = GetTargetNodeId();
                if (id.HasValue) ChangeNodeType(NodeType.Leaf);
            };
            changeTypeMenu.DropDownItems.Add(typeAndItem);
            changeTypeMenu.DropDownItems.Add(typeOrItem);
            changeTypeMenu.DropDownItems.Add(typeLeafItem);

            var deleteItem = new System.Windows.Forms.ToolStripMenuItem("Удалить");
            deleteItem.Click += (s, e) =>
            {
                var id = GetTargetNodeId();
                if (id.HasValue) DeleteNodeMsagl(id.Value);
            };

            _contextMenuStrip.Items.Add(addChildItem);
            _contextMenuStrip.Items.Add(renameItem);
            _contextMenuStrip.Items.Add(changeTypeMenu);
            _contextMenuStrip.Items.Add(deleteItem);

            // Назначаем контекстное меню непосредственно контролу GViewer
            _viewer.ContextMenuStrip = _contextMenuStrip;
            
            // Подписываемся на события
            _viewer.MouseClick += Viewer_MouseClick;
            _viewer.ObjectUnderMouseCursorChanged += Viewer_ObjectUnderMouseCursorChanged;
            _viewer.MouseDown += Viewer_MouseDown;
            _viewer.MouseUp += Viewer_MouseUp;
            // Событие EdgeInserted больше не поддерживается в текущей версии MSAGL
            
            // Перетаскивание узлов включено по умолчанию в текущей версии MSAGL
            
            WinFormsHost.Child = _viewer;
        }
        
        private void Viewer_EdgeInserted(object sender, EventArgs e)
        {
            if (_viewer.InsertingEdge)
            {
                // В текущей версии MSAGL нет прямого доступа к узлам вставки ребра
                // Используем первые два узла из графа, так как свойство Selected отсутствует
                var selectedNodes = _viewer.Graph.Nodes.Take(2).ToList();
                
                if (selectedNodes.Count >= 2)
                {
                    var sourceNode = selectedNodes[0];
                    var targetNode = selectedNodes[1];
                    
                    // Преобразуем ID узлов в числа
                    if (int.TryParse(sourceNode.Id, out var sourceId) && 
                        int.TryParse(targetNode.Id, out var targetId))
                    {
                        // Находим узел-родитель в модели
                        var parentNode = _tree.Nodes.FirstOrDefault(n => n.Id == sourceId);
                        
                        if (parentNode != null && !parentNode.Children.Contains(targetId))
                        {
                            // Добавляем связь в модели
                            parentNode.Children.Add(targetId);
                            ProjectData.RaiseTreeChanged();
                            
                            // Перерисовываем граф
                            BuildAndDrawGraphMsagl();
                        }
                    }
                }
            }
        }


 
        private void BuildAndDrawGraphMsagl()
        {
            if (_viewer == null)
            {
                _viewer = new GViewer();
                WinFormsHost.Child = _viewer;
            }

            var g = new Graph();
            foreach (var n in _tree.Nodes)
            {
                var node = g.AddNode(n.Id.ToString());
                node.LabelText = $"{n.Name} ({n.Type})";
                node.Attr.FillColor = n.Type switch
                {
                    NodeType.And => Color.LightGreen,
                    NodeType.Or  => Color.LightBlue,
                    _            => Color.LightGray
                };
            }
            foreach (var parent in _tree.Nodes)
                foreach (var childId in parent.Children.Distinct())
                    g.AddEdge(parent.Id.ToString(), childId.ToString());

            g.Attr.LayerDirection = LayerDirection.TB;
            _viewer.Graph = g;
        }

        private void RenameNodeMsagl(int vertexId)
        {
            var node = _tree.Nodes.First(n => n.Id == vertexId);
            var dialog = new AddNodeDialog(node, node.Name, node.Type);
            if (dialog.ShowDialog() == true)
            {
                node.Name = dialog.NewNodeData.Name;
                BuildAndDrawGraphMsagl();
                ProjectData.RaiseTreeChanged();
            }
        }

        private void AddChildMsagl(int parentId)
        {
            var parentNode = _tree.Nodes.First(n => n.Id == parentId);
            var dialog = new AddNodeDialog(parentNode);
            if (dialog.ShowDialog() == true)
            {
                var data = dialog.NewNodeData;
                int newId = _tree.Nodes.Any() ? _tree.Nodes.Max(n => n.Id) + 1 : 0;
                var newNode = new Models.Node { Id = newId, Name = data.Name, Type = data.Type };
                if (!parentNode.Children.Contains(newId)) parentNode.Children.Add(newId);
                _tree.Nodes.Add(newNode);
                BuildAndDrawGraphMsagl();
                ProjectData.RaiseTreeChanged();
            }
        }

        private void ChangeTypeMsagl(int vertexId)
        {
            var node = _tree.Nodes.First(n => n.Id == vertexId);
            var dialog = new AddNodeDialog(node, node.Name, node.Type);
            if (dialog.ShowDialog() == true)
            {
                node.Type = dialog.NewNodeData.Type;
                BuildAndDrawGraphMsagl();
                ProjectData.RaiseTreeChanged();
            }
        }

        private void DeleteNodeMsagl(int vertexId)
        {
            if (vertexId == 0)
            {
                System.Windows.MessageBox.Show("Нельзя удалить корень!");
                return;
            }
            
            // Получаем список всех узлов, которые нужно удалить (включая потомков)
            var nodesToDelete = GetNodeWithDescendants(vertexId);
            
            // Удаляем ссылки на удаляемые узлы из списков Children всех узлов
            foreach (var nodeId in nodesToDelete)
            {
                foreach (var node in _tree.Nodes)
                {
                    node.Children.RemoveAll(id => id == nodeId);
                }
            }
            
            // Удаляем сами узлы
            _tree.Nodes.RemoveAll(n => nodesToDelete.Contains(n.Id));
            
            BuildAndDrawGraphMsagl();
            ProjectData.RaiseTreeChanged();
        }
        
        // Метод для получения узла и всех его потомков рекурсивно
        private HashSet<int> GetNodeWithDescendants(int nodeId)
        {
            var result = new HashSet<int> { nodeId };
            var node = _tree.Nodes.FirstOrDefault(n => n.Id == nodeId);
            
            if (node != null)
            {
                foreach (var childId in node.Children)
                {
                    // Рекурсивно добавляем всех потомков
                    foreach (var descendantId in GetNodeWithDescendants(childId))
                    {
                        result.Add(descendantId);
                    }
                }
            }
            
            return result;
        }

        // удалены методы ручного Canvas CRUD и рендера

        private void ClearTree_Click(object sender, RoutedEventArgs e)
        {
            if (System.Windows.MessageBox.Show("Удалить всё дерево?", "Подтверждение", MessageBoxButton.YesNo) != MessageBoxResult.Yes)
                return;

            _tree.Nodes.Clear();

            var root = new ModelNode
            {
                Id = 0,
                Name = "Система",
                Type = NodeType.And,
                Children = new List<int>()
            };

            _tree.Nodes.Add(root);
            BuildAndDrawGraphMsagl();
            ProjectData.RaiseTreeChanged();

            System.Windows.MessageBox.Show("Дерево очищено. Создан новый корень 'Система'.", "Готово", MessageBoxButton.OK, MessageBoxImage.Information);
        }


        // ---------------- СЕРИАЛИЗАЦИЯ ----------------
        private void SaveTree_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string path = "project.json";
                ProjectData.CurrentTree.SaveProject(path);
                System.Windows.MessageBox.Show($"Проект сохранён в {path}", "Сохранено");
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Ошибка при сохранении: {ex.Message}");
            }
        }

        private void LoadTree_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string path = "project.json";
                if (!File.Exists(path))
                {
                    System.Windows.MessageBox.Show("Файл project.json не найден.");
                    return;
                }

                var loaded = TreeModel.LoadProject(path);
                if (loaded == null)
                {
                    System.Windows.MessageBox.Show("Ошибка загрузки: пустой объект.");
                    return;
                }

                ProjectData.CurrentTree = loaded;
                _tree.Nodes = loaded.Nodes;
                _tree.EP = loaded.EP;
                _tree.AP = loaded.AP;
                _tree.GoalWeights = loaded.GoalWeights;

                BuildAndDrawGraphMsagl();

                ProjectData.RaiseTreeChanged();

                System.Windows.MessageBox.Show("Проект успешно загружен.", "Готово");
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Ошибка загрузки: {ex.Message}", "Ошибка");
            }
        }



        private void RedrawTree_Click(object sender, RoutedEventArgs e) => BuildAndDrawGraphMsagl();

        private void OnPreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.Delete)
            {
                if (_viewer?.SelectedObject is Microsoft.Msagl.Drawing.Node vnode)
                {
                    if (int.TryParse(vnode.Id, out var vid))
                    {
                        DeleteNodeMsagl(vid);
                        e.Handled = true;
                        return;
                    }
                }
            }
        }
        
        private void Viewer_MouseClick(object sender, System.Windows.Forms.MouseEventArgs e)
        {
            // Обработка правого клика для показа контекстного меню
            // Контекстное меню уже привязано к WinFormsHost, поэтому нам не нужно
            // вручную его показывать - WPF сделает это автоматически
            
            // Сохраняем выбранный узел для использования в командах контекстного меню
            if (_viewer.SelectedObject is Microsoft.Msagl.Drawing.Node)
            {
                // Обновляем состояние UI при необходимости
            }
        }
        
        private void Viewer_ObjectUnderMouseCursorChanged(object sender, ObjectUnderMouseCursorChangedEventArgs e)
        {
            if (e.NewObject is Microsoft.Msagl.Drawing.Node)
            {
                // Используем стандартный метод установки подсказки
                System.Windows.Forms.ToolTip toolTip = new System.Windows.Forms.ToolTip();
                toolTip.SetToolTip(_viewer, "Щелкните правой кнопкой мыши для вызова меню");
                var vnode = (Microsoft.Msagl.Drawing.Node)e.NewObject;
                if (int.TryParse(vnode.Id, out var id))
                    _nodeUnderCursorId = id;
                else
                    _nodeUnderCursorId = null;
            }
            else
            {
                _nodeUnderCursorId = null;
            }
        }
        
        private void AddChildToSelectedNode()
        {
            if (_viewer.SelectedObject is Microsoft.Msagl.Drawing.Node node && int.TryParse(node.Id, out var nodeId))
            {
                AddChildMsagl(nodeId);
            }
        }
        
        private void RenameSelectedNode()
        {
            if (_viewer.SelectedObject is Microsoft.Msagl.Drawing.Node node && int.TryParse(node.Id, out var nodeId))
            {
                RenameNodeMsagl(nodeId);
            }
        }
        
        private void ChangeTypeOfSelectedNode()
        {
            if (_viewer.SelectedObject is Microsoft.Msagl.Drawing.Node node && int.TryParse(node.Id, out var nodeId))
            {
                ChangeTypeMsagl(nodeId);
            }
        }
        
        private void ChangeNodeType(NodeType newType)
        {
            var nodeIdOpt = GetTargetNodeId();
            if (!nodeIdOpt.HasValue) return;
            var nodeId = nodeIdOpt.Value;

            var modelNode = _tree.Nodes.FirstOrDefault(n => n.Id == nodeId);
            if (modelNode != null)
            {
                if (newType == NodeType.Leaf && modelNode.Children.Any())
                {
                    System.Windows.MessageBox.Show("Нельзя установить тип Leaf: у узла есть потомки.", "Запрещено", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                modelNode.Type = newType;
                BuildAndDrawGraphMsagl();
                ProjectData.RaiseTreeChanged();
            }
        }
        
        private void DeleteSelectedNode()
        {
            if (_viewer.SelectedObject is Microsoft.Msagl.Drawing.Node node && int.TryParse(node.Id, out var nodeId))
            {
                DeleteNodeMsagl(nodeId);
            }
        }

        private void Viewer_MouseDown(object sender, System.Windows.Forms.MouseEventArgs e)
        {
            // Начало режима соединения: удерживайте Shift и нажмите на узел
            if (e.Button == System.Windows.Forms.MouseButtons.Left && (System.Windows.Forms.Control.ModifierKeys & System.Windows.Forms.Keys.Shift) == System.Windows.Forms.Keys.Shift)
            {
                if (_nodeUnderCursorId.HasValue)
                {
                    _isConnecting = true;
                    _connectSourceId = _nodeUnderCursorId.Value;
                }
            }
        }

        private int? GetTargetNodeId()
        {
            if (_nodeUnderCursorId.HasValue) return _nodeUnderCursorId.Value;
            if (_viewer.SelectedObject is Microsoft.Msagl.Drawing.Node node && int.TryParse(node.Id, out var nodeId))
                return nodeId;
            return null;
        }

        private void Viewer_MouseUp(object sender, System.Windows.Forms.MouseEventArgs e)
        {
            if (_isConnecting)
            {
                _isConnecting = false;
                if (_connectSourceId.HasValue && _nodeUnderCursorId.HasValue)
                {
                    var src = _connectSourceId.Value;
                    var dst = _nodeUnderCursorId.Value;
                    _connectSourceId = null;
                    if (src != dst)
                    {
                        TryConnectNodes(src, dst);
                    }
                }
            }
        }

        private void TryConnectNodes(int parentId, int childId)
        {
            // Валидация: один родитель на узел
            var hasOtherParent = _tree.Nodes.Any(n => n.Children.Contains(childId) && n.Id != parentId);
            if (hasOtherParent)
            {
                System.Windows.MessageBox.Show("Узел уже имеет другого родителя. Строгое дерево: один родитель.", "Запрещено", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Валидация: отсутствие циклов (child не должен быть предком parent)
            var descendantsOfChild = GetNodeWithDescendants(childId);
            if (descendantsOfChild.Contains(parentId))
            {
                System.Windows.MessageBox.Show("Создание цикла запрещено.", "Запрещено", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var parentNode = _tree.Nodes.FirstOrDefault(n => n.Id == parentId);
            var childNode = _tree.Nodes.FirstOrDefault(n => n.Id == childId);
            if (parentNode == null || childNode == null) return;

            if (!parentNode.Children.Contains(childId))
                parentNode.Children.Add(childId);

            BuildAndDrawGraphMsagl();
            ProjectData.RaiseTreeChanged();
        }
    }
}
