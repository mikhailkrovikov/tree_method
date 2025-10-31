using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using TreeMethod.Models;
using ModelNode = TreeMethod.Models.Node;

namespace TreeMethod.Views
{
    public partial class TreePage : Page
    {
        // Используем свойство для доступа к актуальному дереву
        private TreeModel CurrentTree => ProjectData.CurrentTree;
 
        // Словари для хранения UI элементов
        private Dictionary<int, Border> _nodeBorders = new(); // Border - контейнер узла
        private Dictionary<int, TextBlock> _nodeLabels = new(); // TextBlock - текст узла
        private Dictionary<string, Polyline> _edges = new(); // Рёбра между узлами
        
        // Позиции узлов на Canvas
        private Dictionary<int, Point> _nodePositions = new();
        
        // Узлы, которые были перемещены вручную (не должны пересчитываться автоматически)
        private HashSet<int> _manuallyMovedNodes = new();
        
        // Состояние для перетаскивания
        private bool _isDragging = false;
        private int? _draggedNodeId = null;
        private Point _dragStartPoint;
        private Point _nodeStartPosition;
        
        // Состояние для соединения узлов
        private bool _isConnecting = false;
        private int? _connectSourceId = null;
        
        // Текущий выбранный узел для контекстного меню
        private int? _selectedNodeId = null;
        
        // Контекстное меню для узлов
        private ContextMenu _nodeContextMenu;
        
        // Константы для отрисовки
        private const double NODE_WIDTH = 120;
        private const double NODE_HEIGHT = 60;
        private const double NODE_SPACING_X = 200;
        private const double NODE_SPACING_Y = 150;
        private const double ARROW_SIZE = 10;
        
        public TreePage()
        {
            InitializeComponent();

            if (CurrentTree.Nodes.Count == 0)
                CurrentTree.Nodes.Add(new ModelNode { Id = 0, Name = "Система", Type = NodeType.And });

            Focusable = true;
            InitializeContextMenu();
            
            Loaded += (_, __) => 
            {
                Keyboard.Focus(this);
                
                // Подписываемся на события Canvas после загрузки
                if (GraphCanvas != null)
                {
                    GraphCanvas.MouseLeftButtonDown += Canvas_MouseLeftButtonDown;
                    GraphCanvas.MouseLeftButtonUp += Canvas_MouseLeftButtonUp;
                    GraphCanvas.MouseMove += Canvas_MouseMove;
                    GraphCanvas.MouseRightButtonDown += Canvas_MouseRightButtonDown;
                    
                    if (_nodeContextMenu != null)
                    {
                        GraphCanvas.ContextMenu = _nodeContextMenu;
                    }
                }
                
                BuildGraph();
            };
            PreviewKeyDown += OnPreviewKeyDown;
        }
        
        private void BuildGraph()
        {
            // Проверяем, что Canvas инициализирован
            if (GraphCanvas == null) return;
            
            // Очищаем Canvas
            GraphCanvas.Children.Clear();
            _nodeBorders.Clear();
            _nodeLabels.Clear();
            _edges.Clear();
            
            // Если нет позиций узлов, вычисляем их автоматически (сверху вниз)
            // Но НЕ пересчитываем позиции узлов, которые были перемещены вручную
            var nodesWithoutPositions = CurrentTree.Nodes.Where(n => !_nodePositions.ContainsKey(n.Id)).ToList();
            if (_nodePositions.Count == 0 || nodesWithoutPositions.Any())
            {
                // Сохраняем позиции вручную перемещённых узлов
                var savedManualPositions = new Dictionary<int, Point>();
                foreach (var nodeId in _manuallyMovedNodes)
                {
                    if (_nodePositions.ContainsKey(nodeId))
                    {
                        savedManualPositions[nodeId] = _nodePositions[nodeId];
                    }
                }
                
                // Пересчитываем позиции
                CalculateNodePositions();
                
                // Восстанавливаем позиции вручную перемещённых узлов
                foreach (var kvp in savedManualPositions)
                {
                    _nodePositions[kvp.Key] = kvp.Value;
                }
            }
            
            // Рисуем рёбра
            DrawEdges();
            
            // Рисуем узлы
            DrawNodes();
            
            // Обновляем размер Canvas в зависимости от размера графа
            UpdateCanvasSize();
        }
        
        private void UpdateCanvasSize()
        {
            if (!_nodePositions.Any())
            {
                // Минимальный размер, если нет узлов
                GraphCanvas.Width = 2000;
                GraphCanvas.Height = 1000;
                return;
            }
            
            // Находим границы всех узлов
            double minX = _nodePositions.Values.Min(p => p.X);
            double minY = _nodePositions.Values.Min(p => p.Y);
            double maxX = _nodePositions.Values.Max(p => p.X) + NODE_WIDTH;
            double maxY = _nodePositions.Values.Max(p => p.Y) + NODE_HEIGHT;
            
            // Добавляем отступы по краям
            const double padding = 100;
            
            // Canvas должен быть достаточно большим, чтобы вместить все узлы
            double requiredWidth = maxX + padding;
            double requiredHeight = maxY + padding;
            
            // Если есть узлы с отрицательными координатами, добавляем место слева/сверху
            if (minX < 0)
                requiredWidth += Math.Abs(minX) + padding;
            if (minY < 0)
                requiredHeight += Math.Abs(minY) + padding;
            
            // Минимальный размер
            const double minWidth = 800;
            const double minHeight = 600;
            
            GraphCanvas.Width = Math.Max(minWidth, requiredWidth);
            GraphCanvas.Height = Math.Max(minHeight, requiredHeight);
        }
        
        private void CalculateNodePositions()
        {
            _nodePositions.Clear();
            
            if (!CurrentTree.Nodes.Any()) return;
            
            // Находим корень (узел без родителей)
            var rootNode = CurrentTree.Nodes.FirstOrDefault(n => 
                !CurrentTree.Nodes.Any(p => p.Children.Contains(n.Id)));
            
            if (rootNode == null)
                rootNode = CurrentTree.Nodes[0]; // Если не нашли, берём первый
            
            var startX = 1000.0; // Центр Canvas
            var startY = 100.0;
            
            // Сначала вычисляем ширину каждого поддерева
            var subtreeWidths = new Dictionary<int, double>();
            CalculateSubtreeWidths(rootNode.Id, subtreeWidths);
            
            // Рекурсивно размещаем узлы с учётом ширины поддеревьев
            CalculatePositionRecursive(rootNode.Id, startX, startY, 0, subtreeWidths);
        }
        
        private double CalculateSubtreeWidths(int nodeId, Dictionary<int, double> widths)
        {
            var node = CurrentTree.Nodes.FirstOrDefault(n => n.Id == nodeId);
            if (node == null) return NODE_WIDTH;
            
            if (!node.Children.Any())
            {
                widths[nodeId] = NODE_WIDTH;
                return NODE_WIDTH;
            }
            
            double totalWidth = 0;
            bool isFirst = true;
            
            foreach (var childId in node.Children)
            {
                var childWidth = CalculateSubtreeWidths(childId, widths);
                if (!isFirst)
                {
                    totalWidth += NODE_SPACING_X; // Промежуток между детьми
                }
                totalWidth += childWidth;
                isFirst = false;
            }
            
            // Ширина поддерева = максимум из ширины узла и ширины всех детей
            widths[nodeId] = Math.Max(NODE_WIDTH, totalWidth);
            return widths[nodeId];
        }
        
        private double CalculatePositionRecursive(int nodeId, double leftX, double y, int depth, Dictionary<int, double> subtreeWidths)
        {
            var node = CurrentTree.Nodes.FirstOrDefault(n => n.Id == nodeId);
            if (node == null) return leftX;
            
            // Если узел был перемещён вручную, не пересчитываем его позицию, но размещаем его потомков
            if (_manuallyMovedNodes.Contains(nodeId) && _nodePositions.ContainsKey(nodeId))
            {
                // Используем текущую позицию узла
                var currentPos = _nodePositions[nodeId];
                
                // Но всё равно размещаем детей относительно текущей позиции
                if (node.Children.Any())
                {
                    double childX = currentPos.X; // Начинаем с позиции родителя
                    
                    // Размещаем всех детей, которые не были перемещены вручную
                    foreach (var childId in node.Children)
                    {
                        if (!_manuallyMovedNodes.Contains(childId) || !_nodePositions.ContainsKey(childId))
                        {
                            var childWidth = subtreeWidths.ContainsKey(childId) ? subtreeWidths[childId] : NODE_WIDTH;
                            
                            CalculatePositionRecursive(
                                childId, 
                                childX,
                                currentPos.Y + NODE_SPACING_Y,
                                depth + 1,
                                subtreeWidths);
                            
                            // Если у ребёнка есть позиция, используем её для расчёта следующего X
                            if (_nodePositions.ContainsKey(childId))
                            {
                                var childPos = _nodePositions[childId];
                                childX = childPos.X + NODE_WIDTH + NODE_SPACING_X;
                            }
                        }
                        else
                        {
                            // Если ребёнок перемещён вручную, просто пропускаем его в расчёте, но учитываем его позицию
                            if (_nodePositions.ContainsKey(childId))
                            {
                                var childPos = _nodePositions[childId];
                                childX = Math.Max(childX, childPos.X + NODE_WIDTH + NODE_SPACING_X);
                            }
                        }
                    }
                }
                
                // Возвращаем правую границу поддерева на основе текущей позиции узла
                return currentPos.X + NODE_WIDTH;
            }
            
            if (!node.Children.Any())
            {
                // Листовой узел - размещаем по левому краю
                _nodePositions[nodeId] = new Point(leftX, y);
                return leftX + NODE_WIDTH;
            }
            
            // Размещаем детей с учётом ширины их поддеревьев
            double currentX = leftX;
            var childPositions = new List<double>();
            
            foreach (var childId in node.Children)
            {
                // Если ребёнок был перемещён вручную, используем его текущую позицию
                if (_manuallyMovedNodes.Contains(childId) && _nodePositions.ContainsKey(childId))
                {
                    var childPos = _nodePositions[childId];
                    childPositions.Add(childPos.X + NODE_WIDTH / 2);
                    currentX = Math.Max(currentX, childPos.X + NODE_WIDTH + NODE_SPACING_X);
                }
                else
                {
                    var childWidth = subtreeWidths.ContainsKey(childId) ? subtreeWidths[childId] : NODE_WIDTH;
                    
                    // Центрируем ребёнка относительно его поддерева
                    var childCenterX = currentX + childWidth / 2;
                    
                    var childRightX = CalculatePositionRecursive(
                        childId, 
                        currentX,
                        y + NODE_SPACING_Y,
                        depth + 1,
                        subtreeWidths);
                    
                    childPositions.Add(childCenterX - NODE_WIDTH / 2);
                    currentX = childRightX + NODE_SPACING_X;
                }
            }
            
            // Центрируем текущий узел относительно всех детей
            double nodeCenterX = childPositions.Any() 
                ? (childPositions.First() + childPositions.Last() + NODE_WIDTH) / 2
                : leftX + NODE_WIDTH / 2;
            
            _nodePositions[nodeId] = new Point(nodeCenterX - NODE_WIDTH / 2, y);
            
            return currentX - NODE_SPACING_X; // Возвращаем правую границу поддерева
        }
        
        private void DrawNodes()
        {
            foreach (var node in CurrentTree.Nodes)
            {
                if (!_nodePositions.ContainsKey(node.Id)) continue;
                
                var pos = _nodePositions[node.Id];
                
                // Создаём Border для узла
                var border = new Border
                {
                    Width = NODE_WIDTH,
                    Height = NODE_HEIGHT,
                    CornerRadius = new CornerRadius(5),
                    BorderBrush = new SolidColorBrush(Colors.DarkGray),
                    BorderThickness = new Thickness(2),
                    Background = GetNodeColor(node.Type),
                    Cursor = Cursors.Hand,
                    Tag = node.Id
                };
                
                // Создаём TextBlock для текста
                var textBlock = new TextBlock
                {
                    Text = node.Name,
                    FontSize = 12,
                    FontFamily = new FontFamily("Arial"),
                    Foreground = new SolidColorBrush(Colors.Black),
                    TextAlignment = TextAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    TextWrapping = TextWrapping.Wrap,
                    Padding = new Thickness(5),
                    IsHitTestVisible = false // Позволяем событиям мыши проходить через TextBlock к Border
                };
                
                border.Child = textBlock;
                
                // Размещаем на Canvas
                Canvas.SetLeft(border, pos.X);
                Canvas.SetTop(border, pos.Y);
                
                GraphCanvas.Children.Add(border);
                
                _nodeBorders[node.Id] = border;
                _nodeLabels[node.Id] = textBlock;
                
                // Подписываемся на события
                border.MouseEnter += (s, e) => border.BorderBrush = new SolidColorBrush(Colors.Blue);
                border.MouseLeave += (s, e) => border.BorderBrush = new SolidColorBrush(Colors.DarkGray);
            }
        }
        
        private void DrawEdges()
        {
            foreach (var parent in CurrentTree.Nodes)
            {
                foreach (var childId in parent.Children.Distinct())
                {
                    if (!_nodePositions.ContainsKey(parent.Id) || 
                        !_nodePositions.ContainsKey(childId))
                        continue;
                    
                    var parentPos = _nodePositions[parent.Id];
                    var childPos = _nodePositions[childId];
                    
                    // Вычисляем точки соединения
                    var parentCenterX = parentPos.X + NODE_WIDTH / 2;
                    var parentBottom = parentPos.Y + NODE_HEIGHT;
                    var childCenterX = childPos.X + NODE_WIDTH / 2;
                    var childTop = childPos.Y;
                    
                    // Рисуем линию
                    var line = new Line
                    {
                        X1 = parentCenterX,
                        Y1 = parentBottom,
                        X2 = childCenterX,
                        Y2 = childTop,
                        Stroke = new SolidColorBrush(Colors.DarkGray),
                        StrokeThickness = 2
                    };
                    
                    GraphCanvas.Children.Add(line);
                    
                    // Рисуем стрелку
                    DrawArrow(parentCenterX, parentBottom, childCenterX, childTop);
                    
                    var edgeKey = $"{parent.Id}-{childId}";
                    _edges[edgeKey] = null; // Сохраняем факт существования ребра
                }
            }
        }
        
        private void DrawArrow(double x1, double y1, double x2, double y2)
        {
            var dx = x2 - x1;
            var dy = y2 - y1;
            var length = Math.Sqrt(dx * dx + dy * dy);
            
            if (length < 0.1) return;
            
            var unitX = dx / length;
            var unitY = dy / length;
            
            // Точка на конце стрелки (немного не доходя до узла)
            var arrowTipX = x2 - unitX * (NODE_HEIGHT / 2 + 5);
            var arrowTipY = y2 - unitY * (NODE_HEIGHT / 2 + 5);
            
            // Вектор перпендикулярный линии
            var perpX = -unitY;
            var perpY = unitX;
            
            // Точки стрелки
            var p1 = new Point(arrowTipX, arrowTipY);
            var p2 = new Point(arrowTipX - unitX * ARROW_SIZE + perpX * ARROW_SIZE / 2, 
                              arrowTipY - unitY * ARROW_SIZE + perpY * ARROW_SIZE / 2);
            var p3 = new Point(arrowTipX - unitX * ARROW_SIZE - perpX * ARROW_SIZE / 2, 
                              arrowTipY - unitY * ARROW_SIZE - perpY * ARROW_SIZE / 2);
            
            var arrow = new Polygon
            {
                Points = new PointCollection { p1, p2, p3 },
                Fill = new SolidColorBrush(Colors.DarkGray),
                Stroke = new SolidColorBrush(Colors.DarkGray)
            };
            
            GraphCanvas.Children.Add(arrow);
        }
        
        private Brush GetNodeColor(NodeType type)
        {
            return type switch
            {
                NodeType.And => new SolidColorBrush(Color.FromRgb(144, 238, 144)), // LightGreen
                NodeType.Or => new SolidColorBrush(Color.FromRgb(173, 216, 230)),  // LightBlue
                _ => new SolidColorBrush(Color.FromRgb(211, 211, 211))             // LightGray
            };
        }
        
        private void Canvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var element = e.OriginalSource as FrameworkElement;
            if (element?.Tag is int nodeId)
            {
                var border = _nodeBorders[nodeId];
                if (border != null)
                {
                    // Проверяем Shift для соединения узлов
                    if (Keyboard.Modifiers == ModifierKeys.Shift)
                    {
                        _isConnecting = true;
                        _connectSourceId = nodeId;
                        border.BorderBrush = new SolidColorBrush(Colors.Green);
                    }
                    else
                    {
                        // Начинаем перетаскивание
                        _isDragging = true;
                        _draggedNodeId = nodeId;
                        _dragStartPoint = e.GetPosition(GraphCanvas);
                        _nodeStartPosition = _nodePositions[nodeId];
                        // Отмечаем узел как перемещённый вручную
                        _manuallyMovedNodes.Add(nodeId);
                        border.CaptureMouse();
                        e.Handled = true;
                    }
                }
            }
        }
        
        private void Canvas_MouseMove(object sender, MouseEventArgs e)
        {
            if (_isDragging && _draggedNodeId.HasValue)
            {
                var currentPos = e.GetPosition(GraphCanvas);
                var deltaX = currentPos.X - _dragStartPoint.X;
                var deltaY = currentPos.Y - _dragStartPoint.Y;
                
                var newPos = new Point(
                    _nodeStartPosition.X + deltaX,
                    _nodeStartPosition.Y + deltaY);
                
                // Ограничиваем перемещение границами Canvas
                var minX = 0;
                var minY = 0;
                // Используем Width/Height, так как они установлены динамически
                var canvasWidth = GraphCanvas.Width > 0 ? GraphCanvas.Width : (GraphCanvas.ActualWidth > 0 ? GraphCanvas.ActualWidth : 2000);
                var canvasHeight = GraphCanvas.Height > 0 ? GraphCanvas.Height : (GraphCanvas.ActualHeight > 0 ? GraphCanvas.ActualHeight : 1000);
                var maxX = canvasWidth - NODE_WIDTH;
                var maxY = canvasHeight - NODE_HEIGHT;
                
                newPos.X = Math.Max(minX, Math.Min(maxX, newPos.X));
                newPos.Y = Math.Max(minY, Math.Min(maxY, newPos.Y));
                
                _nodePositions[_draggedNodeId.Value] = newPos;
                
                var border = _nodeBorders[_draggedNodeId.Value];
                if (border != null)
                {
                    Canvas.SetLeft(border, newPos.X);
                    Canvas.SetTop(border, newPos.Y);
                }
                
                // Перерисовываем рёбра
                RebuildGraph();
            }
        }
        
        private void Canvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_isDragging && _draggedNodeId.HasValue)
            {
                var border = _nodeBorders[_draggedNodeId.Value];
                if (border != null)
                {
                    border.ReleaseMouseCapture();
                }
                _isDragging = false;
                _draggedNodeId = null;
            }
            
            if (_isConnecting)
            {
                var element = e.OriginalSource as FrameworkElement;
                if (element?.Tag is int targetNodeId && _connectSourceId.HasValue)
                {
                    if (_connectSourceId.Value != targetNodeId)
                    {
                        TryConnectNodes(_connectSourceId.Value, targetNodeId);
                    }
                }
                
                // Сбрасываем подсветку
                if (_connectSourceId.HasValue)
                {
                    var border = _nodeBorders[_connectSourceId.Value];
                    if (border != null)
                    {
                        border.BorderBrush = new SolidColorBrush(Colors.DarkGray);
                    }
                }
                
                _isConnecting = false;
                _connectSourceId = null;
            }
        }
        
        private void InitializeContextMenu()
        {
            _nodeContextMenu = new ContextMenu();
            
            var addChildItem = new MenuItem { Header = "Добавить потомка", Name = "AddChildMenuItem" };
            addChildItem.Click += (s, e) =>
            {
                if (_selectedNodeId.HasValue) AddChild(_selectedNodeId.Value);
            };

            var renameItem = new MenuItem { Header = "Переименовать" };
            renameItem.Click += (s, e) =>
            {
                if (_selectedNodeId.HasValue) RenameNode(_selectedNodeId.Value);
            };

            var changeTypeMenu = new MenuItem { Header = "Изменить тип" };
            var typeAndItem = new MenuItem { Header = "AND" };
            typeAndItem.Click += (s, e) =>
            {
                if (_selectedNodeId.HasValue) ChangeNodeType(_selectedNodeId.Value, NodeType.And);
            };
            var typeOrItem = new MenuItem { Header = "OR" };
            typeOrItem.Click += (s, e) =>
            {
                if (_selectedNodeId.HasValue) ChangeNodeType(_selectedNodeId.Value, NodeType.Or);
            };
            var typeLeafItem = new MenuItem { Header = "LEAF" };
            typeLeafItem.Click += (s, e) =>
            {
                if (_selectedNodeId.HasValue) ChangeNodeType(_selectedNodeId.Value, NodeType.Leaf);
            };
            changeTypeMenu.Items.Add(typeAndItem);
            changeTypeMenu.Items.Add(typeOrItem);
            changeTypeMenu.Items.Add(typeLeafItem);
            
            var deleteItem = new MenuItem { Header = "Удалить" };
            deleteItem.Click += (s, e) =>
            {
                if (_selectedNodeId.HasValue) DeleteNode(_selectedNodeId.Value);
            };
            
            var clearGraphItem = new MenuItem { Header = "Очистить граф, кроме корня" };
            clearGraphItem.Click += (s, e) => ClearGraphExceptRoot();
            
            _nodeContextMenu.Items.Add(addChildItem);
            _nodeContextMenu.Items.Add(renameItem);
            _nodeContextMenu.Items.Add(changeTypeMenu);
            _nodeContextMenu.Items.Add(new Separator());
            _nodeContextMenu.Items.Add(clearGraphItem);
            _nodeContextMenu.Items.Add(new Separator());
            _nodeContextMenu.Items.Add(deleteItem);
        }
        
        public void RefreshGraph()
        {
            // Обновляем ссылку на актуальное дерево из ProjectData
            // Это важно при загрузке нового проекта
            var currentTree = ProjectData.CurrentTree;
            
            // Сбрасываем позиции и перестраиваем граф
            _nodePositions.Clear();
            _manuallyMovedNodes.Clear();
            
            if (currentTree.Nodes.Count == 0)
                currentTree.Nodes.Add(new ModelNode { Id = 0, Name = "Система", Type = NodeType.And });
            
            // Используем актуальное дерево для построения графа
            BuildGraph();
            
            // После построения смещаем все узлы к началу координат для удобства просмотра
            NormalizeNodePositions();
            
            // Перестраиваем граф с нормализованными позициями
            BuildGraph();
            
            // Автоматически прокручиваем в начало
            ScrollToGraphStart();
        }
        
        private void NormalizeNodePositions()
        {
            if (!_nodePositions.Any())
                return;
            
            // Находим минимальные координаты
            double minX = _nodePositions.Values.Min(p => p.X);
            double minY = _nodePositions.Values.Min(p => p.Y);
            
            // Если узлы начинаются не с (0,0), смещаем их к началу с небольшим отступом
            if (minX > 50 || minY > 50)
            {
                double offsetX = Math.Max(0, minX - 50);
                double offsetY = Math.Max(0, minY - 50);
                
                // Смещаем все позиции
                var newPositions = new Dictionary<int, Point>();
                foreach (var kvp in _nodePositions)
                {
                    newPositions[kvp.Key] = new Point(kvp.Value.X - offsetX, kvp.Value.Y - offsetY);
                }
                _nodePositions = newPositions;
            }
        }
        
        private void ScrollToGraphStart()
        {
            if (ScrollViewer == null)
                return;
            
            // Просто прокручиваем в начало - позиции уже нормализованы
            ScrollViewer.ScrollToHorizontalOffset(0);
            ScrollViewer.ScrollToVerticalOffset(0);
            
            // Дополнительная попытка через задержку для гарантии
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (ScrollViewer != null)
                {
                    ScrollViewer.ScrollToHome();
                }
            }), System.Windows.Threading.DispatcherPriority.Loaded);
        }
        
        private void ClearGraphExceptRoot()
        {
            var result = MessageBox.Show(
                "Удалить все узлы кроме корня?",
                "Очистить граф",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);
            
            if (result != MessageBoxResult.Yes)
                return;
            
            // Находим корень
            var allChildren = CurrentTree.Nodes.SelectMany(n => n.Children).ToHashSet();
            var root = CurrentTree.Nodes.FirstOrDefault(n => !allChildren.Contains(n.Id)) 
                       ?? CurrentTree.Nodes.FirstOrDefault();
            
            if (root == null)
            {
                // Если корня нет, создаём новый
                root = new ModelNode { Id = 0, Name = "Система", Type = NodeType.And };
                CurrentTree.Nodes.Clear();
                CurrentTree.Nodes.Add(root);
            }
            else
            {
                // Удаляем все узлы кроме корня
                var nodesToDelete = CurrentTree.Nodes.Where(n => n.Id != root.Id).Select(n => n.Id).ToList();
                
                foreach (var nodeId in nodesToDelete)
                {
                    CurrentTree.Nodes.RemoveAll(n => n.Id == nodeId);
                    _nodePositions.Remove(nodeId);
                    _manuallyMovedNodes.Remove(nodeId);
                }
                
                // Очищаем всех потомков корня
                root.Children.Clear();
            }
            
            // Сбрасываем позиции и перестраиваем граф
            _nodePositions.Clear();
            _manuallyMovedNodes.Clear();
            BuildGraph();
                ProjectData.RaiseTreeChanged();
            }
        
        private void Canvas_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            var element = e.OriginalSource as FrameworkElement;
            if (element?.Tag is int nodeId)
            {
                _selectedNodeId = nodeId;
                if (_nodeContextMenu != null)
                {
                    // Проверяем, есть ли место для добавления потомка
                    bool canAddChild = CanAddChild(nodeId);
                    var addChildItem = _nodeContextMenu.Items.OfType<MenuItem>()
                        .FirstOrDefault(mi => mi.Name == "AddChildMenuItem");
                    if (addChildItem != null)
                    {
                        addChildItem.IsEnabled = canAddChild;
                    }
                    
                    _nodeContextMenu.IsOpen = true;
                }
                e.Handled = true;
            }
        }
        
        private bool CanAddChild(int parentId)
        {
            // Проверяем, есть ли место под родителем для нового узла
            if (!_nodePositions.ContainsKey(parentId))
                return true; // Если позиции нет, разрешаем (автоматически разместится)
            
            var parentPos = _nodePositions[parentId];
            var canvasHeight = GraphCanvas.Height > 0 ? GraphCanvas.Height : 2000;
            
            // Вычисляем Y-координату нового потомка
            var newChildY = parentPos.Y + NODE_HEIGHT + NODE_SPACING_Y;
            
            // Проверяем, поместится ли новый узел в Canvas
            // Нужно место: newChildY + NODE_HEIGHT должно быть <= canvasHeight
            return newChildY + NODE_HEIGHT <= canvasHeight;
        }
        
        private void RebuildGraph()
        {
            // Удаляем только рёбра, узлы оставляем
            var lines = GraphCanvas.Children.OfType<Line>().ToList();
            var polygons = GraphCanvas.Children.OfType<Polygon>().ToList();
            
            foreach (var edge in lines)
            {
                GraphCanvas.Children.Remove(edge);
            }
            
            foreach (var arrow in polygons)
            {
                GraphCanvas.Children.Remove(arrow);
            }
            
            _edges.Clear();
            DrawEdges();
        }
        
        private void TryConnectNodes(int parentId, int childId)
        {
            // Валидация: один родитель на узел
            var hasOtherParent = CurrentTree.Nodes.Any(n => n.Children.Contains(childId) && n.Id != parentId);
            if (hasOtherParent)
            {
                MessageBox.Show("Узел уже имеет другого родителя. Строгое дерево: один родитель.", 
                    "Запрещено", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            
            // Валидация: отсутствие циклов
            var descendantsOfChild = GetNodeWithDescendants(childId);
            if (descendantsOfChild.Contains(parentId))
            {
                MessageBox.Show("Создание цикла запрещено.", 
                    "Запрещено", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var parentNode = CurrentTree.Nodes.FirstOrDefault(n => n.Id == parentId);
            var childNode = CurrentTree.Nodes.FirstOrDefault(n => n.Id == childId);
            if (parentNode == null || childNode == null) return;

            if (!parentNode.Children.Contains(childId))
                parentNode.Children.Add(childId);

            BuildGraph();
            ProjectData.RaiseTreeChanged();
        }
        
        private HashSet<int> GetNodeWithDescendants(int nodeId)
        {
            var result = new HashSet<int> { nodeId };
            var node = CurrentTree.Nodes.FirstOrDefault(n => n.Id == nodeId);
            
            if (node != null)
            {
                foreach (var childId in node.Children)
                {
                    foreach (var descendantId in GetNodeWithDescendants(childId))
                    {
                        result.Add(descendantId);
                    }
                }
            }
            
            return result;
        }

        // Основные операции
        private void AddChild(int parentId)
        {
            var parentNode = CurrentTree.Nodes.First(n => n.Id == parentId);
            var dialog = new AddNodeDialog(parentNode);
            if (dialog.ShowDialog() == true)
            {
                var data = dialog.NewNodeData;
                int newId = CurrentTree.Nodes.Any() ? CurrentTree.Nodes.Max(n => n.Id) + 1 : 0;
                var newNode = new ModelNode { Id = newId, Name = data.Name, Type = data.Type };
                if (!parentNode.Children.Contains(newId)) 
                    parentNode.Children.Add(newId);
                CurrentTree.Nodes.Add(newNode);
                
                // Размещаем новый узел под родителем
                // Если родитель был перемещён вручную, размещаем новый узел относительно его текущей позиции
                var parentPos = _nodePositions.ContainsKey(parentId) 
                    ? _nodePositions[parentId] 
                    : new Point(1000, 100);
                
                var newY = parentPos.Y + NODE_SPACING_Y;
                
                // Находим свободное место под родителем (смотрим, есть ли уже другие потомки)
                var siblings = parentNode.Children.Where(cid => cid != newId && _nodePositions.ContainsKey(cid)).ToList();
                
                double newX;
                if (siblings.Any())
                {
                    // Если есть другие потомки, размещаем справа от самого правого
                    var rightmostSibling = siblings
                        .Select(sid => _nodePositions[sid].X)
                        .Max();
                    newX = rightmostSibling + NODE_SPACING_X;
            }
            else
            {
                    // Если это первый потомок, центрируем под родителем
                    // Центр родителя: parentPos.X + NODE_WIDTH / 2
                    // Центр нового узла должен быть там же, значит левый край: parentPos.X
                    newX = parentPos.X;
                }
                
                _nodePositions[newId] = new Point(newX, newY);
                
                BuildGraph();
                ProjectData.RaiseTreeChanged();
            }
        }
        
        private void RenameNode(int nodeId)
        {
            var node = CurrentTree.Nodes.First(n => n.Id == nodeId);
            var dialog = new AddNodeDialog(node, node.Name, node.Type);
            if (dialog.ShowDialog() == true)
            {
                node.Name = dialog.NewNodeData.Name;
                if (_nodeLabels.ContainsKey(nodeId))
                {
                    _nodeLabels[nodeId].Text = node.Name;
                }
                ProjectData.RaiseTreeChanged();
            }
        }
        
        private void ChangeNodeType(int nodeId, NodeType newType)
        {
            var modelNode = CurrentTree.Nodes.FirstOrDefault(n => n.Id == nodeId);
            if (modelNode != null)
            {
                if (newType == NodeType.Leaf && modelNode.Children.Any())
                {
                    MessageBox.Show("Нельзя установить тип Leaf: у узла есть потомки.", 
                        "Запрещено", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                modelNode.Type = newType;
                if (_nodeBorders.ContainsKey(nodeId))
                {
                    _nodeBorders[nodeId].Background = GetNodeColor(newType);
                }
                ProjectData.RaiseTreeChanged();
            }
        }
        
        private void DeleteNode(int nodeId)
        {
            if (nodeId == 0)
            {
                MessageBox.Show("Нельзя удалить корень!");
                return;
            }
            
            var nodesToDelete = GetNodeWithDescendants(nodeId);
            
            foreach (var id in nodesToDelete)
            {
                foreach (var node in CurrentTree.Nodes)
                {
                    node.Children.RemoveAll(childId => childId == id);
                }
                _nodePositions.Remove(id);
                _manuallyMovedNodes.Remove(id); // Убираем из списка вручную перемещённых
            }
            
            CurrentTree.Nodes.RemoveAll(n => nodesToDelete.Contains(n.Id));
            
            BuildGraph();
            ProjectData.RaiseTreeChanged();
        }
        
        private void OnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Delete && _selectedNodeId.HasValue)
            {
                DeleteNode(_selectedNodeId.Value);
                e.Handled = true;
            }
        }
    }
}
