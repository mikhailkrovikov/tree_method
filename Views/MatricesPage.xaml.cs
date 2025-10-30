using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using TreeMethod.Models;

namespace TreeMethod.Views
{
    public partial class MatricesPage : Page
    {
        private List<MatrixRow> _epRows = new();
        private List<MatrixRow> _apRows = new();

        private string[] _features = { "P1", "P2", "P3" };
        private string[] _goals = { "A1", "A2" };

        public MatricesPage()
        {
            InitializeComponent();

            Loaded += (_, __) => LoadMatrices();
            ProjectData.TreeChanged += OnTreeChanged;
        }

        private void OnTreeChanged()
        {
            Application.Current.Dispatcher.Invoke(LoadMatrices);
        }
        private void ApplySizes_Click(object sender, RoutedEventArgs e)
        {
            if (!int.TryParse(PCountBox.Text, out var p) || p <= 0)
            {
                MessageBox.Show("Введите количество признаков (P > 0)");
                return;
            }

            if (!int.TryParse(ACountBox.Text, out var a) || a <= 0)
            {
                MessageBox.Show("Введите количество целей (A > 0)");
                return;
            }

            _features = Enumerable.Range(1, p).Select(i => $"P{i}").ToArray();
            _goals = Enumerable.Range(1, a).Select(i => $"A{i}").ToArray();

            LoadMatrices(); // заново перестраиваем таблицы
        }

        private void LoadMatrices()
        {
            var tree = ProjectData.CurrentTree;
            if (tree.Nodes.Count == 0) return;

            int pCount = _features.Length;
            int aCount = _goals.Length;

            // --- E×P ---
            _epRows = new List<MatrixRow>();

            // все узлы кроме корня
            var allChildren = tree.Nodes.SelectMany(n => n.Children).ToHashSet();
            var root = tree.Nodes.FirstOrDefault(n => !allChildren.Contains(n.Id)) ?? tree.Nodes[0];
            var rows = tree.Nodes.Where(n => n.Id != root.Id).ToList();

            for (int i = 0; i < rows.Count; i++)
            {
                var row = new MatrixRow { Name = rows[i].Name };

                // если матрица EP уже существует — читаем из неё
                if (tree.EP != null && tree.EP.GetLength(0) == rows.Count && tree.EP.GetLength(1) == pCount)
                {
                    for (int j = 0; j < pCount; j++)
                        row.SetValue(_features[j], tree.EP[i, j]);
                }
                else
                {
                    foreach (var f in _features)
                        row.SetValue(f, 0);
                }

                _epRows.Add(row);
            }

            EPGrid.Columns.Clear();
            EPGrid.Columns.Add(new DataGridTextColumn
            {
                Header = "Элемент/подсистема",
                Binding = new System.Windows.Data.Binding("Name"),
                IsReadOnly = true,
                Width = 260
            });
            foreach (var f in _features)
                EPGrid.Columns.Add(new DataGridTextColumn
                {
                    Header = f,
                    Binding = new System.Windows.Data.Binding($"Values[{f}]"),
                    Width = 60
                });
            EPGrid.ItemsSource = _epRows;

            // --- A×P ---
            _apRows = new List<MatrixRow>();
            for (int i = 0; i < aCount; i++)
            {
                var row = new MatrixRow { Name = _goals[i] };

                if (tree.AP != null && tree.AP.GetLength(0) == aCount && tree.AP.GetLength(1) == pCount)
                {
                    for (int j = 0; j < pCount; j++)
                        row.SetValue(_features[j], tree.AP[i, j]);
                }
                else
                {
                    foreach (var f in _features)
                        row.SetValue(f, 0);
                }

                _apRows.Add(row);
            }

            APGrid.Columns.Clear();
            APGrid.Columns.Add(new DataGridTextColumn
            {
                Header = "Цель",
                Binding = new System.Windows.Data.Binding("Name"),
                IsReadOnly = true,
                Width = 260
            });
            foreach (var f in _features)
                APGrid.Columns.Add(new DataGridTextColumn
                {
                    Header = f,
                    Binding = new System.Windows.Data.Binding($"Values[{f}]"),
                    Width = 60
                });
            APGrid.ItemsSource = _apRows;
        }

        private void EPGrid_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            if (e.EditingElement is TextBox tb)
            {
                if (!IsValidMatrixValue(tb.Text))
                {
                    MessageBox.Show("Допустимы только значения: -1, 0 или 1.", "Ошибка ввода");
                    tb.Text = "0";
                }
            }
        }

        private void APGrid_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            if (e.EditingElement is TextBox tb)
            {
                if (!IsValidMatrixValue(tb.Text))
                {
                    MessageBox.Show("Допустимы только значения: -1, 0 или 1.", "Ошибка ввода");
                    tb.Text = "0";
                }
            }
        }

        private bool IsValidMatrixValue(string input)
        {
            return input == "-1" || input == "0" || input == "1";
        }


        private void SaveMatrices_Click(object sender, RoutedEventArgs e)
        {
            int rowsE = _epRows.Count;
            int colsE = _features.Length;
            int rowsA = _apRows.Count;
            int colsA = _features.Length;

            int[,] ep = new int[rowsE, colsE];
            int[,] ap = new int[rowsA, colsA];

            for (int i = 0; i < rowsE; i++)
                for (int j = 0; j < colsE; j++)
                    ep[i, j] = _epRows[i].GetValue(_features[j]);

            for (int i = 0; i < rowsA; i++)
                for (int j = 0; j < colsA; j++)
                    ap[i, j] = _apRows[i].GetValue(_features[j]);

            ProjectData.UpdateMatrices(ep, ap);
            MessageBox.Show("Матрицы сохранены и применены.", "Сохранение");
        }
    }
}
