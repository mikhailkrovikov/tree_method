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
        private TreeModel CurrentTree => ProjectData.CurrentTree;
 
        private Dictionary<int, Border> _nodeBorders = new();
        private Dictionary<int, TextBlock> _nodeLabels = new();
        private Dictionary<string, Polyline> _edges = new();
        private Dictionary<int, Point> _nodePositions = new();
        private HashSet<int> _manuallyMovedNodes = new();
        
        private bool _isDragging = false;
        private int? _draggedNodeId = null;
        private Point _dragStartPoint;
        private Point _nodeStartPosition;
        private bool _isConnecting = false;
        private int? _connectSourceId = null;
        private int? _selectedNodeId = null;
        private bool _nodeWasMoved = false;
        private ContextMenu _nodeContextMenu;
        
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
            if (GraphCanvas == null) return;
            
            GraphCanvas.Children.Clear();
            _nodeBorders.Clear();
            _nodeLabels.Clear();
            _edges.Clear();
            
            var nodesWithoutPositions = CurrentTree.Nodes.Where(n => !_nodePositions.ContainsKey(n.Id)).ToList();
            if (_nodePositions.Count == 0 || nodesWithoutPositions.Any())
            {
                var savedManualPositions = new Dictionary<int, Point>();
                foreach (var nodeId in _manuallyMovedNodes)
                {
                    if (_nodePositions.ContainsKey(nodeId))
                    {
                        savedManualPositions[nodeId] = _nodePositions[nodeId];
                    }
                }
                
                CalculateNodePositions();
                
                foreach (var kvp in savedManualPositions)
                {
                    _nodePositions[kvp.Key] = kvp.Value;
                }
            }
            
            DrawEdges();
            DrawNodes();
            UpdateCanvasSize();
        }
        
        private void UpdateCanvasSize()
        {
            if (!_nodePositions.Any())
            {
                GraphCanvas.Width = 2000;
                GraphCanvas.Height = 1000;
                return;
            }
            
            double minX = _nodePositions.Values.Min(p => p.X);
            double minY = _nodePositions.Values.Min(p => p.Y);
            double maxX = _nodePositions.Values.Max(p => p.X) + NODE_WIDTH;
            double maxY = _nodePositions.Values.Max(p => p.Y) + NODE_HEIGHT;
            
            const double padding = 100;
            const double topPadding = 270;
            
            double requiredWidth = maxX + padding;
            double requiredHeight = maxY + topPadding;
            
            if (minX < 0)
                requiredWidth += Math.Abs(minX) + padding;
            if (minY < 0)
                requiredHeight += Math.Abs(minY) + padding;
            
            const double minWidth = 800;
            const double minHeight = 1000;
            
            GraphCanvas.Width = Math.Max(minWidth, requiredWidth);
            GraphCanvas.Height = Math.Max(minHeight, requiredHeight);
        }
        
        private void CalculateNodePositions()
        {
            _nodePositions.Clear();
            
            if (!CurrentTree.Nodes.Any()) return;
            
            var rootNode = CurrentTree.Nodes.FirstOrDefault(n => 
                !CurrentTree.Nodes.Any(p => p.Children.Contains(n.Id)));
            
            if (rootNode == null)
                rootNode = CurrentTree.Nodes[0];
            
            var startX = 1000.0;
            var startY = 310.0;
            
            var subtreeWidths = new Dictionary<int, double>();
            CalculateSubtreeWidths(rootNode.Id, subtreeWidths);
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
                    totalWidth += NODE_SPACING_X;
                }
                totalWidth += childWidth;
                isFirst = false;
            }
            
            widths[nodeId] = Math.Max(NODE_WIDTH, totalWidth);
            return widths[nodeId];
        }
        
        private double CalculatePositionRecursive(int nodeId, double leftX, double y, int depth, Dictionary<int, double> subtreeWidths)
        {
            var node = CurrentTree.Nodes.FirstOrDefault(n => n.Id == nodeId);
            if (node == null) return leftX;
            
            if (_manuallyMovedNodes.Contains(nodeId) && _nodePositions.ContainsKey(nodeId))
            {
                var currentPos = _nodePositions[nodeId];
                
                if (node.Children.Any())
                {
                    double childX = currentPos.X;
                    
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
                            
                            if (_nodePositions.ContainsKey(childId))
                            {
                                var childPos = _nodePositions[childId];
                                childX = childPos.X + NODE_WIDTH + NODE_SPACING_X;
                            }
                        }
                        else
                        {
                            if (_nodePositions.ContainsKey(childId))
                            {
                                var childPos = _nodePositions[childId];
                                childX = Math.Max(childX, childPos.X + NODE_WIDTH + NODE_SPACING_X);
                            }
                        }
                    }
                }
                
                return currentPos.X + NODE_WIDTH;
            }
            
            if (!node.Children.Any())
            {
                _nodePositions[nodeId] = new Point(leftX, y);
                return leftX + NODE_WIDTH;
            }
            
            double currentX = leftX;
            var childPositions = new List<double>();
            
            foreach (var childId in node.Children)
            {
                if (_manuallyMovedNodes.Contains(childId) && _nodePositions.ContainsKey(childId))
                {
                    var childPos = _nodePositions[childId];
                    childPositions.Add(childPos.X + NODE_WIDTH / 2);
                    currentX = Math.Max(currentX, childPos.X + NODE_WIDTH + NODE_SPACING_X);
                }
                else
                {
                    var childWidth = subtreeWidths.ContainsKey(childId) ? subtreeWidths[childId] : NODE_WIDTH;
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
            
            double nodeCenterX = childPositions.Any() 
                ? (childPositions.First() + childPositions.Last() + NODE_WIDTH) / 2
                : leftX + NODE_WIDTH / 2;
            
            _nodePositions[nodeId] = new Point(nodeCenterX - NODE_WIDTH / 2, y);
            
            return currentX - NODE_SPACING_X;
        }
        
        private void DrawNodes()
        {
            foreach (var node in CurrentTree.Nodes)
            {
                if (!_nodePositions.ContainsKey(node.Id)) continue;
                
                var pos = _nodePositions[node.Id];
                
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
                    IsHitTestVisible = false
                };
                
                border.Child = textBlock;
                
                Canvas.SetLeft(border, pos.X);
                Canvas.SetTop(border, pos.Y);
                
                GraphCanvas.Children.Add(border);
                
                _nodeBorders[node.Id] = border;
                _nodeLabels[node.Id] = textBlock;
                
                if (_selectedNodeId == node.Id)
                {
                    border.BorderBrush = new SolidColorBrush(Colors.Orange);
                    border.BorderThickness = new Thickness(3);
                }
                
                border.MouseEnter += (s, e) => 
                {
                    if (_selectedNodeId != node.Id)
                        border.BorderBrush = new SolidColorBrush(Colors.Blue);
                };
                border.MouseLeave += (s, e) => 
                {
                    if (_selectedNodeId != node.Id)
                        border.BorderBrush = new SolidColorBrush(Colors.DarkGray);
                    else
                        border.BorderBrush = new SolidColorBrush(Colors.Orange);
                };
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
                    
                    var parentCenterX = parentPos.X + NODE_WIDTH / 2;
                    var parentBottom = parentPos.Y + NODE_HEIGHT;
                    var childCenterX = childPos.X + NODE_WIDTH / 2;
                    var childTop = childPos.Y;
                    
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
                    
                    DrawArrow(parentCenterX, parentBottom, childCenterX, childTop);
                    
                    var edgeKey = $"{parent.Id}-{childId}";
                    _edges[edgeKey] = null;
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
            
            var arrowTipX = x2 - unitX * (NODE_HEIGHT / 2 + 5);
            var arrowTipY = y2 - unitY * (NODE_HEIGHT / 2 + 5);
            
            var perpX = -unitY;
            var perpY = unitX;
            
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
                NodeType.And => new SolidColorBrush(Color.FromRgb(144, 238, 144)),
                NodeType.Or => new SolidColorBrush(Color.FromRgb(173, 216, 230)),
                _ => new SolidColorBrush(Color.FromRgb(211, 211, 211))
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
                    if (Keyboard.Modifiers == ModifierKeys.Shift)
                    {
                        _isConnecting = true;
                        _connectSourceId = nodeId;
                        border.BorderBrush = new SolidColorBrush(Colors.Green);
                        _selectedNodeId = nodeId;
                        UpdateSelectedNodeVisual();
                    }
                    else
                    {
                        _isDragging = true;
                        _draggedNodeId = nodeId;
                        _dragStartPoint = e.GetPosition(GraphCanvas);
                        _nodeStartPosition = _nodePositions[nodeId];
                        _manuallyMovedNodes.Add(nodeId);
                        _nodeWasMoved = false;
                        border.CaptureMouse();
                        e.Handled = true;
                    }
                }
            }
            else
            {
                if (_selectedNodeId.HasValue)
                {
                    _selectedNodeId = null;
                    UpdateSelectedNodeVisual();
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
                
                if (Math.Abs(deltaX) > 5 || Math.Abs(deltaY) > 5)
                {
                    _nodeWasMoved = true;
                }
                
                var newPos = new Point(
                    _nodeStartPosition.X + deltaX,
                    _nodeStartPosition.Y + deltaY);
                
                var minX = 0;
                var minY = 0;
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
                
                if (!_nodeWasMoved)
                {
                    _selectedNodeId = _draggedNodeId.Value;
                    UpdateSelectedNodeVisual();
                    Keyboard.Focus(this);
                }
                
                _isDragging = false;
                _draggedNodeId = null;
                _nodeWasMoved = false;
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
                Keyboard.Focus(this);
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
            var typeAndItem = new MenuItem { Header = "И" };
            typeAndItem.Click += (s, e) =>
            {
                if (_selectedNodeId.HasValue) ChangeNodeType(_selectedNodeId.Value, NodeType.And);
            };
            var typeOrItem = new MenuItem { Header = "ИЛИ" };
            typeOrItem.Click += (s, e) =>
            {
                if (_selectedNodeId.HasValue) ChangeNodeType(_selectedNodeId.Value, NodeType.Or);
            };
            var typeLeafItem = new MenuItem { Header = "Висячий", Name = "TypeLeafMenuItem" };
            typeLeafItem.Click += (s, e) =>
            {
                if (_selectedNodeId.HasValue) ChangeNodeType(_selectedNodeId.Value, NodeType.Leaf);
            };
            changeTypeMenu.Items.Add(typeAndItem);
            changeTypeMenu.Items.Add(typeOrItem);
            changeTypeMenu.Items.Add(typeLeafItem);
            
            var deleteItem = new MenuItem { Header = "Удалить", Name = "DeleteMenuItem" };
            deleteItem.Click += (s, e) =>
            {
                if (_selectedNodeId.HasValue) DeleteNode(_selectedNodeId.Value);
            };
            
            var clearGraphItem = new MenuItem { Header = "Очистить граф" };
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
            var currentTree = ProjectData.CurrentTree;
            
            _nodePositions.Clear();
            _manuallyMovedNodes.Clear();
            
            if (currentTree.Nodes.Count == 0)
                currentTree.Nodes.Add(new ModelNode { Id = 0, Name = "Система", Type = NodeType.And });
            
            BuildGraph();
            NormalizeNodePositions();
            BuildGraph();
            ScrollToGraphStart();
        }
        
        private void NormalizeNodePositions()
        {
            if (!_nodePositions.Any())
                return;
            
            double minX = _nodePositions.Values.Min(p => p.X);
            double minY = _nodePositions.Values.Min(p => p.Y);
            
            if (minX > 50 || minY > 50)
            {
                double offsetX = Math.Max(0, minX - 50);
                double offsetY = Math.Max(0, minY - 50);
                
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
            
            ScrollViewer.ScrollToHorizontalOffset(0);
            ScrollViewer.ScrollToVerticalOffset(0);
            
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
            
            var allChildren = CurrentTree.Nodes.SelectMany(n => n.Children).ToHashSet();
            var root = CurrentTree.Nodes.FirstOrDefault(n => !allChildren.Contains(n.Id)) 
                       ?? CurrentTree.Nodes.FirstOrDefault();
            
            if (root == null)
            {
                root = new ModelNode { Id = 0, Name = "Система", Type = NodeType.And };
                CurrentTree.Nodes.Clear();
                CurrentTree.Nodes.Add(root);
            }
            else
            {
                var nodesToDelete = CurrentTree.Nodes.Where(n => n.Id != root.Id).Select(n => n.Id).ToList();
                
                foreach (var nodeId in nodesToDelete)
                {
                    CurrentTree.Nodes.RemoveAll(n => n.Id == nodeId);
                    _nodePositions.Remove(nodeId);
                    _manuallyMovedNodes.Remove(nodeId);
                }
                
                root.Children.Clear();
            }
            
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
                UpdateSelectedNodeVisual();
                if (_nodeContextMenu != null)
                {
                    bool canAddChild = CanAddChild(nodeId);
                    var addChildItem = _nodeContextMenu.Items.OfType<MenuItem>()
                        .FirstOrDefault(mi => mi.Name == "AddChildMenuItem");
                    if (addChildItem != null)
                    {
                        addChildItem.IsEnabled = canAddChild;
                    }
                    
                    var deleteItem = _nodeContextMenu.Items.OfType<MenuItem>()
                        .FirstOrDefault(mi => mi.Name == "DeleteMenuItem");
                    if (deleteItem != null)
                    {
                        deleteItem.IsEnabled = nodeId != 0;
                    }
                    
                    var changeTypeMenu = _nodeContextMenu.Items.OfType<MenuItem>()
                        .FirstOrDefault(mi => mi.Header?.ToString() == "Изменить тип");
                    if (changeTypeMenu != null)
                    {
                        var typeLeafItem = changeTypeMenu.Items.OfType<MenuItem>()
                            .FirstOrDefault(mi => mi.Name == "TypeLeafMenuItem");
                        if (typeLeafItem != null)
                        {
                            var node = CurrentTree.Nodes.FirstOrDefault(n => n.Id == nodeId);
                            typeLeafItem.IsEnabled = node != null && !node.Children.Any();
                        }
                    }
                    
                    _nodeContextMenu.IsOpen = true;
                }
                e.Handled = true;
            }
        }
        
        private void UpdateSelectedNodeVisual()
        {
            foreach (var kvp in _nodeBorders)
            {
                var border = kvp.Value;
                if (border != null)
                {
                    if (kvp.Key == _selectedNodeId)
                    {
                        border.BorderBrush = new SolidColorBrush(Colors.Orange);
                        border.BorderThickness = new Thickness(3);
                    }
                    else
                    {
                        border.BorderBrush = new SolidColorBrush(Colors.DarkGray);
                        border.BorderThickness = new Thickness(2);
                    }
                }
            }
        }
        
        private bool CanAddChild(int parentId)
        {
            var parentNode = CurrentTree.Nodes.FirstOrDefault(n => n.Id == parentId);
            if (parentNode == null || parentNode.Type == NodeType.Leaf)
                return false;
            
            if (!_nodePositions.ContainsKey(parentId))
                return true;
            
            var parentPos = _nodePositions[parentId];
            var canvasHeight = GraphCanvas.Height > 0 ? GraphCanvas.Height : 2000;
            var newChildY = parentPos.Y + NODE_HEIGHT + NODE_SPACING_Y;
            
            return newChildY + NODE_HEIGHT <= canvasHeight;
        }
        
        private void RebuildGraph()
        {
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
            var hasOtherParent = CurrentTree.Nodes.Any(n => n.Children.Contains(childId) && n.Id != parentId);
            if (hasOtherParent)
            {
                return;
            }
            
            var descendantsOfChild = GetNodeWithDescendants(childId);
            if (descendantsOfChild.Contains(parentId))
            {
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

        private void AddChild(int parentId)
        {
            var parentNode = CurrentTree.Nodes.First(n => n.Id == parentId);
            
            if (parentNode.Type == NodeType.Leaf)
            {
                return;
            }
            
            var dialog = new AddNodeDialog(parentNode);
            if (dialog.ShowDialog() == true)
            {
                var data = dialog.NewNodeData;
                int newId = CurrentTree.Nodes.Any() ? CurrentTree.Nodes.Max(n => n.Id) + 1 : 0;
                var newNode = new ModelNode { Id = newId, Name = data.Name, Type = data.Type };
                if (!parentNode.Children.Contains(newId)) 
                    parentNode.Children.Add(newId);
                CurrentTree.Nodes.Add(newNode);
                
                var parentPos = _nodePositions.ContainsKey(parentId) 
                    ? _nodePositions[parentId] 
                    : new Point(1000, 100);
                
                var newY = parentPos.Y + NODE_SPACING_Y;
                
                var siblings = parentNode.Children.Where(cid => cid != newId && _nodePositions.ContainsKey(cid)).ToList();
                
                double newX;
                if (siblings.Any())
                {
                    var rightmostSibling = siblings
                        .Select(sid => _nodePositions[sid].X)
                        .Max();
                    newX = rightmostSibling + NODE_SPACING_X;
            }
            else
            {
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
                _manuallyMovedNodes.Remove(id);
            }
            
            CurrentTree.Nodes.RemoveAll(n => nodesToDelete.Contains(n.Id));
            
            if (nodesToDelete.Contains(_selectedNodeId.GetValueOrDefault(-1)))
            {
                _selectedNodeId = null;
            }
            
            BuildGraph();
            ProjectData.RaiseTreeChanged();
        }
        
        private void OnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Delete && _selectedNodeId.HasValue)
            {
                var nodeIdToDelete = _selectedNodeId.Value;
                _selectedNodeId = null;
                DeleteNode(nodeIdToDelete);
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                if (_selectedNodeId.HasValue)
                {
                    _selectedNodeId = null;
                    BuildGraph();
                }
            }
        }
    }
}
