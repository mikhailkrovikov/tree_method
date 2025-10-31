using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using TreeMethod.Helpers;
using TreeMethod.Models;

namespace TreeMethod.ViewModels
{
    public class MatricesPageViewModel : INotifyPropertyChanged
    {
        private const int ValidValuesMin = -1;
        private const int ValidValuesMax = 1;

        private int _featuresCount = 3;
        private int _goalsCount = 2;

        public MatricesPageViewModel()
        {
            EPRows = new ObservableCollection<MatrixRow>();
            APRows = new ObservableCollection<MatrixRow>();
            Features = new ObservableCollection<string>(Enumerable.Range(1, _featuresCount).Select(i => $"P{i}"));
            Goals = new ObservableCollection<string>(Enumerable.Range(1, _goalsCount).Select(i => $"A{i}"));

            ApplySizesCommand = new RelayCommand(_ => ApplySizes());
            SaveMatricesCommand = new RelayCommand(_ => SaveMatrices());

            ProjectData.TreeChanged += OnTreeChanged;
            LoadMatrices();
        }

        public ObservableCollection<MatrixRow> EPRows { get; }
        public ObservableCollection<MatrixRow> APRows { get; }
        public ObservableCollection<string> Features { get; }
        public ObservableCollection<string> Goals { get; }

        public int FeaturesCount
        {
            get => _featuresCount;
            set
            {
                if (_featuresCount != value && value > 0)
                {
                    _featuresCount = value;
                    OnPropertyChanged();
                }
            }
        }

        public int GoalsCount
        {
            get => _goalsCount;
            set
            {
                if (_goalsCount != value && value > 0)
                {
                    _goalsCount = value;
                    OnPropertyChanged();
                }
            }
        }

        public ICommand ApplySizesCommand { get; }
        public ICommand SaveMatricesCommand { get; }

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private void OnTreeChanged()
        {
            System.Windows.Application.Current.Dispatcher.Invoke(LoadMatrices);
        }

        public void LoadMatrices()
        {
            var tree = ProjectData.CurrentTree;
            if (tree.Nodes.Count == 0) return;

            // Определяем размеры матриц
            var (pCount, aCount) = DetermineMatrixSizes(tree);
            
            // Обновляем свойства
            FeaturesCount = pCount;
            GoalsCount = aCount;
            
            // Обновляем коллекции
            UpdateFeatures(pCount);
            UpdateGoals(aCount);

            // Загружаем матрицы
            LoadEPMatrix(tree, pCount);
            LoadAPMatrix(tree, pCount, aCount);
        }

        private (int featuresCount, int goalsCount) DetermineMatrixSizes(TreeModel tree)
        {
            int pCount = _featuresCount;
            int aCount = _goalsCount;
            
            // Определяем количество признаков из матриц
            if (tree.EP != null && tree.EP.GetLength(1) > 0)
            {
                pCount = tree.EP.GetLength(1);
            }
            else if (tree.AP != null && tree.AP.GetLength(1) > 0)
            {
                pCount = tree.AP.GetLength(1);
            }
            
            // Определяем количество целей из матрицы AP
            if (tree.AP != null && tree.AP.GetLength(0) > 0)
            {
                aCount = tree.AP.GetLength(0);
            }
            
            return (pCount, aCount);
        }

        private void UpdateFeatures(int count)
        {
            var tree = ProjectData.CurrentTree;
            
            // Если в дереве есть сохраненные названия признаков и их количество совпадает, используем их
            if (tree.FeatureNames != null && tree.FeatureNames.Count == count)
            {
                Features.Clear();
                foreach (var featureName in tree.FeatureNames)
                {
                    Features.Add(featureName);
                }
            }
            else
            {
                // Иначе генерируем стандартные названия
                Features.Clear();
                foreach (var feature in Enumerable.Range(1, count).Select(i => $"P{i}"))
                {
                    Features.Add(feature);
                }
            }
            OnPropertyChanged(nameof(Features));
        }

        private void UpdateGoals(int count)
        {
            Goals.Clear();
            foreach (var goal in Enumerable.Range(1, count).Select(i => $"A{i}"))
            {
                Goals.Add(goal);
            }
            OnPropertyChanged(nameof(Goals));
        }

        private void LoadEPMatrix(TreeModel tree, int pCount)
        {
            EPRows.Clear();

            // Получаем все узлы кроме корня
            var allChildren = tree.Nodes.SelectMany(n => n.Children).ToHashSet();
            var root = tree.Nodes.FirstOrDefault(n => !allChildren.Contains(n.Id)) ?? tree.Nodes[0];
            var rows = tree.Nodes.Where(n => n.Id != root.Id).ToList();

            foreach (var node in rows)
            {
                var row = new MatrixRow { Name = node.Name };
                LoadMatrixRowValues(row, tree.EP, rows.IndexOf(node), pCount, Features.ToArray());
                EPRows.Add(row);
            }
        }

        private void LoadAPMatrix(TreeModel tree, int pCount, int aCount)
        {
            APRows.Clear();
            
            for (int i = 0; i < aCount; i++)
            {
                var row = new MatrixRow { Name = Goals[i] };
                LoadMatrixRowValues(row, tree.AP, i, pCount, Features.ToArray());
                APRows.Add(row);
            }
        }

        private void LoadMatrixRowValues(MatrixRow row, int[,] matrix, int rowIndex, int columnCount, string[] featureNames)
        {
            if (matrix != null && matrix.GetLength(0) > rowIndex)
            {
                int matrixCols = matrix.GetLength(1);
                // Читаем существующие значения
                for (int j = 0; j < Math.Min(columnCount, matrixCols); j++)
                {
                    row.SetValue(featureNames[j], matrix[rowIndex, j]);
                }
                // Заполняем оставшиеся нулями, если нужно
                for (int j = matrixCols; j < columnCount; j++)
                {
                    row.SetValue(featureNames[j], 0);
                }
            }
            else
            {
                // Если матрицы нет, заполняем нулями
                foreach (var feature in featureNames)
                {
                    row.SetValue(feature, 0);
                }
            }
        }

        private void ApplySizes()
        {
            // Автоматически корректируем неверные значения без показа ошибки
            if (FeaturesCount <= 0)
            {
                FeaturesCount = 3;
                UpdateFeatures(3);
            }

            if (GoalsCount <= 0)
            {
                GoalsCount = 2;
                UpdateGoals(2);
            }

            // Применяем только если значения корректны
            if (FeaturesCount > 0 && GoalsCount > 0)
            {
                UpdateFeatures(FeaturesCount);
                UpdateGoals(GoalsCount);
                LoadMatrices();
            }
        }

        public bool ValidateMatrixValue(string input)
        {
            if (int.TryParse(input, out int value))
            {
                return value >= ValidValuesMin && value <= ValidValuesMax;
            }
            return false;
        }

        private void SaveMatrices()
        {
            var ep = ConvertRowsToMatrix(EPRows, Features.ToArray());
            var ap = ConvertRowsToMatrix(APRows, Features.ToArray());

            ProjectData.UpdateMatrices(ep, ap);
            
            // Сохраняем названия признаков
            var tree = ProjectData.CurrentTree;
            tree.FeatureNames = new List<string>(Features);
            
            // Матрицы сохранены тихо, без уведомления
        }
        
        // Метод для переименования признака
        public void RenameFeature(int index, string newName)
        {
            if (index < 0 || index >= Features.Count || string.IsNullOrWhiteSpace(newName))
                return;
                
            var oldName = Features[index];
            if (oldName == newName)
                return;
            
            // Обновляем название в коллекции
            Features[index] = newName;
            
            // Переименовываем ключи во всех строках матриц
            foreach (var row in EPRows)
            {
                if (row.Values.ContainsKey(oldName))
                {
                    var value = row.Values[oldName];
                    row.Values.Remove(oldName);
                    row.Values[newName] = value;
                    row.NotifyValuesChanged();
                }
            }
            
            foreach (var row in APRows)
            {
                if (row.Values.ContainsKey(oldName))
                {
                    var value = row.Values[oldName];
                    row.Values.Remove(oldName);
                    row.Values[newName] = value;
                    row.NotifyValuesChanged();
                }
            }
            
            OnPropertyChanged(nameof(Features));
        }

        private int[,] ConvertRowsToMatrix(ObservableCollection<MatrixRow> rows, string[] featureNames)
        {
            int rowCount = rows.Count;
            int colCount = featureNames.Length;
            var matrix = new int[rowCount, colCount];

            for (int i = 0; i < rowCount; i++)
            {
                for (int j = 0; j < colCount; j++)
                {
                    matrix[i, j] = rows[i].GetValue(featureNames[j]);
                }
            }

            return matrix;
        }
        
        // Публичный метод для сохранения всех данных матриц
        public void SaveAllMatricesData()
        {
            var ep = ConvertRowsToMatrix(EPRows, Features.ToArray());
            var ap = ConvertRowsToMatrix(APRows, Features.ToArray());

            ProjectData.UpdateMatrices(ep, ap);
            
            // Сохраняем названия признаков
            var tree = ProjectData.CurrentTree;
            tree.FeatureNames = new List<string>(Features);
        }
    }
}

