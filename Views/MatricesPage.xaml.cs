using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using TreeMethod.Models;
using TreeMethod.ViewModels;

namespace TreeMethod.Views
{
    public partial class MatricesPage : Page
    {
        private MatricesPageViewModel ViewModel => (MatricesPageViewModel)DataContext;

        public MatricesPage()
        {
            InitializeComponent();
            DataContext = new MatricesPageViewModel();

            Loaded += (_, __) => SetupDataGrids();
        }

        private void SetupDataGrids()
        {
            if (ViewModel == null) return;

            SetupDataGrid(EPGrid, "Элемент/подсистема", ViewModel.Features.ToArray(), false);
            SetupAPGrid(APGrid, ViewModel.Features.ToArray());
            
            EPGrid.ItemsSource = ViewModel.EPRows;
            APGrid.ItemsSource = ViewModel.APRows;
            
            EPGrid.PreparingCellForEdit += DataGrid_PreparingCellForEdit;
            APGrid.PreparingCellForEdit += DataGrid_PreparingCellForEdit;
            
            ViewModel.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(MatricesPageViewModel.Features))
                {
                    SetupDataGrid(EPGrid, "Элемент/подсистема", ViewModel.Features.ToArray(), false);
                    SetupAPGrid(APGrid, ViewModel.Features.ToArray());
                }
                else if (e.PropertyName == nameof(MatricesPageViewModel.Goals))
                {
                    RefreshAPRowHeaders();
                }
            };
        }


        private void SetupDataGrid(DataGrid grid, string nameColumnHeader, string[] featureNames, bool isNameEditable = false)
        {
            grid.Columns.Clear();
            
            grid.Columns.Add(new DataGridTextColumn
            {
                Header = nameColumnHeader,
                Binding = new Binding("Name"),
                IsReadOnly = !isNameEditable,
                Width = 300
            });
            
            for (int i = 0; i < featureNames.Length; i++)
            {
                var featureIndex = i;
                var featureName = featureNames[i];
                
                var column = new DataGridTemplateColumn
                {
                    Header = CreateEditableHeader(featureName, featureIndex, grid),
                    Width = 80
                };
                
                var cellTemplate = new DataTemplate();
                var textBlockFactory = new FrameworkElementFactory(typeof(TextBlock));
                textBlockFactory.SetBinding(TextBlock.TextProperty, new Binding($"Values[{featureName}]"));
                textBlockFactory.SetValue(TextBlock.FontSizeProperty, 14.0);
                textBlockFactory.SetValue(TextBlock.HorizontalAlignmentProperty, HorizontalAlignment.Center);
                textBlockFactory.SetValue(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center);
                cellTemplate.VisualTree = textBlockFactory;
                column.CellTemplate = cellTemplate;
                
                var editingTemplate = new DataTemplate();
                var textBoxFactory = new FrameworkElementFactory(typeof(TextBox));
                textBoxFactory.SetBinding(TextBox.TextProperty, new Binding($"Values[{featureName}]") { UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged });
                textBoxFactory.SetValue(TextBox.FontSizeProperty, 14.0);
                textBoxFactory.SetValue(TextBox.HorizontalAlignmentProperty, HorizontalAlignment.Center);
                textBoxFactory.SetValue(TextBox.VerticalAlignmentProperty, VerticalAlignment.Center);
                editingTemplate.VisualTree = textBoxFactory;
                column.CellEditingTemplate = editingTemplate;
                
                grid.Columns.Add(column);
            }
        }
        
        private FrameworkElement CreateEditableHeader(string currentName, int featureIndex, DataGrid grid)
        {
            var stackPanel = new StackPanel 
            { 
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            
            var textBlock = new TextBlock 
            { 
                Text = currentName,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                Cursor = Cursors.IBeam,
                Tag = featureIndex,
                FontSize = 13
            };
            
            stackPanel.Children.Add(textBlock);
            
            textBlock.PreviewMouseLeftButtonDown += (s, e) =>
            {
                e.Handled = true;
                var index = (int)textBlock.Tag;
                var oldName = textBlock.Text;
                
                var headerTextBox = new TextBox
                {
                    Text = oldName,
                    VerticalAlignment = VerticalAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    BorderThickness = new Thickness(1),
                    Padding = new Thickness(2),
                    MinWidth = 50,
                    Tag = index
                };
                
                stackPanel.Children.Clear();
                stackPanel.Children.Add(headerTextBox);
                
                headerTextBox.Focus();
                headerTextBox.SelectAll();
                
                void SaveHeader(object sender, RoutedEventArgs args)
                {
                    var newName = headerTextBox.Text.Trim();
                    var savedIndex = (int)headerTextBox.Tag;
                    
                    if (!string.IsNullOrEmpty(newName) && newName != oldName)
                    {
                        ViewModel?.RenameFeature(savedIndex, newName);
                        SetupDataGrids();
                }
                else
                {
                        stackPanel.Children.Clear();
                        var restoreTextBlock = CreateEditableHeader(oldName, savedIndex, grid);
                        stackPanel.Children.Add(restoreTextBlock);
                    }
                    
                    headerTextBox.LostFocus -= SaveHeader;
                    headerTextBox.KeyDown -= SaveOnEnter;
                }
                
                void SaveOnEnter(object sender, KeyEventArgs args)
                {
                    if (args.Key == Key.Enter)
                    {
                        SaveHeader(sender, null);
                    }
                    else if (args.Key == Key.Escape)
                    {
                        var savedIndex = (int)headerTextBox.Tag;
                        stackPanel.Children.Clear();
                        var restoreTextBlock = CreateEditableHeader(oldName, savedIndex, grid);
                        stackPanel.Children.Add(restoreTextBlock);
                        headerTextBox.LostFocus -= SaveHeader;
                        headerTextBox.KeyDown -= SaveOnEnter;
                    }
                }
                
                headerTextBox.LostFocus += SaveHeader;
                headerTextBox.KeyDown += SaveOnEnter;
            };
            
            return stackPanel;
        }
        
        private void SetupAPGrid(DataGrid grid, string[] featureNames)
        {
            grid.Columns.Clear();
            
            for (int i = 0; i < featureNames.Length; i++)
            {
                var featureIndex = i;
                var featureName = featureNames[i];
                
                var column = new DataGridTemplateColumn
                {
                    Header = CreateEditableHeader(featureName, featureIndex, grid),
                    Width = 80
                };
                
                var cellTemplate = new DataTemplate();
                var textBlockFactory = new FrameworkElementFactory(typeof(TextBlock));
                textBlockFactory.SetBinding(TextBlock.TextProperty, new Binding($"Values[{featureName}]"));
                textBlockFactory.SetValue(TextBlock.FontSizeProperty, 14.0);
                textBlockFactory.SetValue(TextBlock.HorizontalAlignmentProperty, HorizontalAlignment.Center);
                textBlockFactory.SetValue(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center);
                cellTemplate.VisualTree = textBlockFactory;
                column.CellTemplate = cellTemplate;
                
                var editingTemplate = new DataTemplate();
                var textBoxFactory = new FrameworkElementFactory(typeof(TextBox));
                textBoxFactory.SetBinding(TextBox.TextProperty, new Binding($"Values[{featureName}]") { UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged });
                textBoxFactory.SetValue(TextBox.FontSizeProperty, 14.0);
                textBoxFactory.SetValue(TextBox.HorizontalAlignmentProperty, HorizontalAlignment.Center);
                textBoxFactory.SetValue(TextBox.VerticalAlignmentProperty, VerticalAlignment.Center);
                textBoxFactory.AddHandler(TextBox.PreviewTextInputEvent, new TextCompositionEventHandler(TextBox_PreviewTextInput));
                textBoxFactory.AddHandler(TextBox.KeyDownEvent, new KeyEventHandler(TextBox_KeyDown));
                editingTemplate.VisualTree = textBoxFactory;
                column.CellEditingTemplate = editingTemplate;
                
                grid.Columns.Add(column);
            }
        }
        
        private void APGrid_LoadingRow(object sender, DataGridRowEventArgs e)
        {
            if (e.Row.Item is MatrixRow row && ViewModel != null)
            {
                var rowIndex = e.Row.GetIndex();
                if (rowIndex >= 0 && rowIndex < ViewModel.Goals.Count)
                {
                    e.Row.Header = CreateEditableRowHeader(ViewModel.Goals[rowIndex], rowIndex);
                }
            }
        }
        
        private FrameworkElement CreateEditableRowHeader(string currentName, int goalIndex)
        {
            var stackPanel = new StackPanel 
            { 
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                MinWidth = 80
            };
            
            var textBlock = new TextBlock 
            { 
                Text = currentName,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                Cursor = Cursors.IBeam,
                Tag = goalIndex,
                FontSize = 13
            };
            
            stackPanel.Children.Add(textBlock);
            
            textBlock.PreviewMouseLeftButtonDown += (s, e) =>
            {
                e.Handled = true;
                var index = (int)textBlock.Tag;
                var oldName = textBlock.Text;
                
                var headerTextBox = new TextBox
                {
                    Text = oldName,
                    VerticalAlignment = VerticalAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    BorderThickness = new Thickness(1),
                    Padding = new Thickness(2),
                    MinWidth = 50,
                    Tag = index
                };
                
                stackPanel.Children.Clear();
                stackPanel.Children.Add(headerTextBox);
                
                headerTextBox.Focus();
                headerTextBox.SelectAll();
                
                void SaveHeader(object sender, RoutedEventArgs args)
                {
                    var newName = headerTextBox.Text.Trim();
                    var savedIndex = (int)headerTextBox.Tag;
                    
                    if (!string.IsNullOrEmpty(newName) && newName != oldName)
                    {
                        ViewModel?.RenameGoal(savedIndex, newName);
                        RefreshAPRowHeaders();
                }
                else
                {
                        stackPanel.Children.Clear();
                        var restoreTextBlock = CreateEditableRowHeader(oldName, savedIndex);
                        stackPanel.Children.Add(restoreTextBlock);
                    }
                    
                    headerTextBox.LostFocus -= SaveHeader;
                    headerTextBox.KeyDown -= SaveOnEnter;
                }
                
                void SaveOnEnter(object sender, KeyEventArgs args)
                {
                    if (args.Key == Key.Enter)
                    {
                        SaveHeader(sender, null);
                    }
                    else if (args.Key == Key.Escape)
                    {
                        var savedIndex = (int)headerTextBox.Tag;
                        stackPanel.Children.Clear();
                        var restoreTextBlock = CreateEditableRowHeader(oldName, savedIndex);
                        stackPanel.Children.Add(restoreTextBlock);
                        headerTextBox.LostFocus -= SaveHeader;
                        headerTextBox.KeyDown -= SaveOnEnter;
                    }
                }
                
                headerTextBox.LostFocus += SaveHeader;
                headerTextBox.KeyDown += SaveOnEnter;
            };
            
            return stackPanel;
        }
        
        private void RefreshAPRowHeaders()
        {
            if (APGrid != null && ViewModel != null)
            {
                foreach (var item in APGrid.Items)
                {
                    if (item is MatrixRow row)
                    {
                        var rowIndex = APGrid.Items.IndexOf(row);
                        if (rowIndex >= 0 && rowIndex < ViewModel.Goals.Count)
                        {
                            var dataGridRow = (DataGridRow)APGrid.ItemContainerGenerator.ContainerFromItem(item);
                            if (dataGridRow != null)
                            {
                                dataGridRow.Header = CreateEditableRowHeader(ViewModel.Goals[rowIndex], rowIndex);
                            }
                        }
                    }
                }
            }
        }

        private void EPGrid_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            ValidateCellValue(e);
        }

        private void APGrid_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            ValidateCellValue(e);
        }

        private void ValidateCellValue(DataGridCellEditEndingEventArgs e)
        {
            if (e.EditingElement is TextBox textBox && ViewModel != null && !ViewModel.ValidateMatrixValue(textBox.Text))
            {
                textBox.Text = "0";
                e.Cancel = false;
            }
        }

        public void RefreshMatrices()
        {
            ViewModel?.LoadMatrices();
            SetupDataGrids();
        }

        private void DataGrid_PreparingCellForEdit(object sender, DataGridPreparingCellForEditEventArgs e)
        {
            if (e.EditingElement is TextBox textBox)
            {
                textBox.PreviewTextInput -= TextBox_PreviewTextInput;
                textBox.KeyDown -= TextBox_KeyDown;
                textBox.PreviewKeyDown -= TextBox_PreviewKeyDown;
                
                textBox.PreviewTextInput += TextBox_PreviewTextInput;
                textBox.KeyDown += TextBox_KeyDown;
                textBox.PreviewKeyDown += TextBox_PreviewKeyDown;
            }
        }

        private void TextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            var textBox = sender as TextBox;
            if (textBox == null) return;
            
            string currentText = textBox.Text ?? "";
            int selectionStart = textBox.SelectionStart;
            int selectionLength = textBox.SelectionLength;
            
            string newText = currentText.Substring(0, selectionStart) + 
                           e.Text + 
                           currentText.Substring(selectionStart + selectionLength);
            
            foreach (char c in e.Text)
            {
                if (c != '-' && c != '0' && c != '1')
                {
                    e.Handled = true;
                    return;
                }
            }
            
            newText = newText.Trim();
            
            if (newText.Length > 2)
            {
                e.Handled = true;
                return;
            }
            
            if (newText.Contains('-') && (newText.IndexOf('-') != 0 || newText.Length == 1))
            {
                if (newText != "-" && newText.Length > 1)
                {
                    e.Handled = true;
                    return;
                }
            }
            
            if (newText.StartsWith("-") && newText.Length == 2 && newText[1] != '1')
            {
                e.Handled = true;
                return;
            }
        }

        private void TextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Delete || e.Key == Key.Back || 
                e.Key == Key.Tab || e.Key == Key.Enter || 
                e.Key == Key.Left || e.Key == Key.Right || 
                e.Key == Key.Up || e.Key == Key.Down ||
                (Keyboard.Modifiers == ModifierKeys.Control && (e.Key == Key.A || e.Key == Key.C || e.Key == Key.V || e.Key == Key.X)))
            {
                return;
            }
            
            if (!((e.Key >= Key.D0 && e.Key <= Key.D1) ||
                  (e.Key >= Key.NumPad0 && e.Key <= Key.NumPad1) ||
                  e.Key == Key.Subtract || e.Key == Key.OemMinus))
            {
                e.Handled = true;
            }
        }

        private void TextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            var textBox = sender as TextBox;
            if (textBox == null) return;
            
            if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.V)
            {
                var clipboardText = Clipboard.GetText();
                string newText = textBox.Text.Substring(0, textBox.SelectionStart) + 
                               clipboardText + 
                               textBox.Text.Substring(textBox.SelectionStart + textBox.SelectionLength);
                
                if (!IsValidMatrixValue(newText))
                {
                    e.Handled = true;
                }
            }
        }

        private bool IsValidMatrixValue(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return true;
            
            text = text.Trim();
            
            return text == "-1" || text == "0" || text == "1" || text == "-" || text == "";
        }

        private void DataGrid_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (sender is DataGrid && MainScrollViewer != null)
            {
                if (Keyboard.Modifiers != ModifierKeys.Shift)
                {
                    var scrollAmount = e.Delta / 3.0;
                    var newOffset = MainScrollViewer.VerticalOffset - scrollAmount;
                    
                    newOffset = Math.Max(0, Math.Min(newOffset, MainScrollViewer.ScrollableHeight));
                    MainScrollViewer.ScrollToVerticalOffset(newOffset);
                    e.Handled = true;
                }
            }
        }
    }
}
