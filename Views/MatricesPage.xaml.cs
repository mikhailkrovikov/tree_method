using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
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

            SetupDataGrid(EPGrid, "Элемент/подсистема", ViewModel.Features.ToArray());
            SetupDataGrid(APGrid, "Цель", ViewModel.Features.ToArray());
            
            EPGrid.ItemsSource = ViewModel.EPRows;
            APGrid.ItemsSource = ViewModel.APRows;
            
            // Обновляем при изменении Features
            ViewModel.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(MatricesPageViewModel.Features))
                {
                    SetupDataGrid(EPGrid, "Элемент/подсистема", ViewModel.Features.ToArray());
                    SetupDataGrid(APGrid, "Цель", ViewModel.Features.ToArray());
                }
            };
        }


        private void SetupDataGrid(DataGrid grid, string nameColumnHeader, string[] featureNames)
        {
            grid.Columns.Clear();
            
            // Колонка с именем
            grid.Columns.Add(new DataGridTextColumn
            {
                Header = nameColumnHeader,
                Binding = new Binding("Name"),
                IsReadOnly = true,
                Width = 260
            });
            
            // Колонки с признаками
            foreach (var feature in featureNames)
            {
                grid.Columns.Add(new DataGridTextColumn
                {
                    Header = feature,
                    Binding = new Binding($"Values[{feature}]"),
                    Width = 60
                });
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
                MessageBox.Show("Допустимы только значения: -1, 0 или 1.", "Ошибка ввода");
                textBox.Text = "0";
            }
        }

        public void RefreshMatrices()
        {
            ViewModel?.LoadMatrices();
            SetupDataGrids();
        }
    }
}
