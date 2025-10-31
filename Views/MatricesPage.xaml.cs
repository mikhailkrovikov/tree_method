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
            
            // Обновляем при изменении Features
            ViewModel.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(MatricesPageViewModel.Features))
                {
                    SetupDataGrid(EPGrid, "Элемент/подсистема", ViewModel.Features.ToArray(), false);
                    SetupAPGrid(APGrid, ViewModel.Features.ToArray());
                }
                else if (e.PropertyName == nameof(MatricesPageViewModel.Goals))
                {
                    // При изменении Goals обновляем заголовки строк
                    RefreshAPRowHeaders();
                }
            };
        }


        private void SetupDataGrid(DataGrid grid, string nameColumnHeader, string[] featureNames, bool isNameEditable = false)
        {
            grid.Columns.Clear();
            
            // Колонка с именем
            grid.Columns.Add(new DataGridTextColumn
            {
                Header = nameColumnHeader,
                Binding = new Binding("Name"),
                IsReadOnly = !isNameEditable,
                Width = 300
            });
            
            // Колонки с признаками (с редактируемыми заголовками)
            for (int i = 0; i < featureNames.Length; i++)
            {
                var featureIndex = i; // Захватываем индекс для замыкания
                var featureName = featureNames[i];
                
                // Используем DataGridTemplateColumn для редактируемого заголовка
                var column = new DataGridTemplateColumn
                {
                    Header = CreateEditableHeader(featureName, featureIndex, grid),
                    Width = 80
                };
                
                // Ячейка для отображения значения
                var cellTemplate = new DataTemplate();
                var textBlockFactory = new FrameworkElementFactory(typeof(TextBlock));
                textBlockFactory.SetBinding(TextBlock.TextProperty, new Binding($"Values[{featureName}]"));
                textBlockFactory.SetValue(TextBlock.FontSizeProperty, 14.0);
                textBlockFactory.SetValue(TextBlock.HorizontalAlignmentProperty, HorizontalAlignment.Center);
                textBlockFactory.SetValue(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center);
                cellTemplate.VisualTree = textBlockFactory;
                column.CellTemplate = cellTemplate;
                
                // Ячейка для редактирования значения
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
                Tag = featureIndex, // Сохраняем индекс для восстановления
                FontSize = 13
            };
            
            stackPanel.Children.Add(textBlock);
            
            // Обработчик клика для редактирования
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
                
                // Заменяем TextBlock на TextBox
                stackPanel.Children.Clear();
                stackPanel.Children.Add(headerTextBox);
                
                headerTextBox.Focus();
                headerTextBox.SelectAll();
                
                // Сохраняем при потере фокуса или нажатии Enter
                void SaveHeader(object sender, RoutedEventArgs args)
                {
                    var newName = headerTextBox.Text.Trim();
                    var savedIndex = (int)headerTextBox.Tag;
                    
                    if (!string.IsNullOrEmpty(newName) && newName != oldName)
                    {
                        ViewModel?.RenameFeature(savedIndex, newName);
                        // Перестраиваем все колонки после переименования
                        SetupDataGrids();
                }
                else
                {
                        // Восстанавливаем старое значение
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
                        // Отмена редактирования
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
        
        // Специальный метод для настройки APGrid с редактируемыми заголовками строк
        private void SetupAPGrid(DataGrid grid, string[] featureNames)
        {
            grid.Columns.Clear();
            
            // Колонки с признаками (с редактируемыми заголовками)
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
        
        private void APGrid_LoadingRow(object sender, DataGridRowEventArgs e)
        {
            if (e.Row.Item is MatrixRow row && ViewModel != null)
            {
                var rowIndex = e.Row.GetIndex();
                if (rowIndex >= 0 && rowIndex < ViewModel.Goals.Count)
                {
                    // Создаем редактируемый заголовок строки
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
            
            // Обработчик клика для редактирования
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
                
                // Заменяем TextBlock на TextBox
                stackPanel.Children.Clear();
                stackPanel.Children.Add(headerTextBox);
                
                headerTextBox.Focus();
                headerTextBox.SelectAll();
                
                // Сохраняем при потере фокуса или нажатии Enter
                void SaveHeader(object sender, RoutedEventArgs args)
                {
                    var newName = headerTextBox.Text.Trim();
                    var savedIndex = (int)headerTextBox.Tag;
                    
                    if (!string.IsNullOrEmpty(newName) && newName != oldName)
                    {
                        ViewModel?.RenameGoal(savedIndex, newName);
                        // Перестраиваем заголовки строк после переименования
                        RefreshAPRowHeaders();
                }
                else
                {
                        // Восстанавливаем старое значение
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
                        // Отмена редактирования
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
                // Просто сбрасываем на допустимое значение, без сообщения
                textBox.Text = "0";
                e.Cancel = false; // Разрешаем завершение редактирования с исправленным значением
            }
        }

        public void RefreshMatrices()
        {
            ViewModel?.LoadMatrices();
            SetupDataGrids();
        }

        private void DataGrid_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            // Если прокручиваем DataGrid, передаем событие прокрутки в родительский ScrollViewer
            if (sender is DataGrid && MainScrollViewer != null)
            {
                // Проверяем, не находится ли DataGrid в процессе горизонтальной прокрутки
                // Если Shift не нажат, прокручиваем вертикально
                if (Keyboard.Modifiers != ModifierKeys.Shift)
                {
                    // Нормализуем Delta (обычно кратно 120) и прокручиваем ScrollViewer
                    var scrollAmount = e.Delta / 3.0; // Делаем прокрутку более плавной
                    var newOffset = MainScrollViewer.VerticalOffset - scrollAmount;
                    
                    // Ограничиваем в допустимых пределах
                    newOffset = Math.Max(0, Math.Min(newOffset, MainScrollViewer.ScrollableHeight));
                    MainScrollViewer.ScrollToVerticalOffset(newOffset);
                    e.Handled = true;
                }
            }
        }
    }
}
