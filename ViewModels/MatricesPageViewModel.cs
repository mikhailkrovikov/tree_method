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

            var (pCount, aCount) = DetermineMatrixSizes(tree);
            
            FeaturesCount = pCount;
            GoalsCount = aCount;
            
            UpdateFeatures(pCount);
            UpdateGoals(aCount);

            LoadEPMatrix(tree, pCount);
            LoadAPMatrix(tree, pCount, aCount);
        }

        private (int featuresCount, int goalsCount) DetermineMatrixSizes(TreeModel tree)
        {
            int pCount = _featuresCount;
            int aCount = _goalsCount;
            
            if (tree.EP != null && tree.EP.GetLength(1) > 0)
            {
                pCount = tree.EP.GetLength(1);
            }
            else if (tree.AP != null && tree.AP.GetLength(1) > 0)
            {
                pCount = tree.AP.GetLength(1);
            }
            
            if (tree.AP != null && tree.AP.GetLength(0) > 0)
            {
                aCount = tree.AP.GetLength(0);
            }
            
            return (pCount, aCount);
        }

        private void UpdateFeatures(int count)
        {
            var tree = ProjectData.CurrentTree;
            
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
            var tree = ProjectData.CurrentTree;
            
            if (tree.GoalNames != null && tree.GoalNames.Count == count)
            {
                Goals.Clear();
                foreach (var goalName in tree.GoalNames)
                {
                    Goals.Add(goalName);
                }
            }
            else
            {
                Goals.Clear();
                foreach (var goal in Enumerable.Range(1, count).Select(i => $"A{i}"))
                {
                    Goals.Add(goal);
                }
            }
            OnPropertyChanged(nameof(Goals));
        }

        private void LoadEPMatrix(TreeModel tree, int pCount)
        {
            EPRows.Clear();

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
                var goalName = (i < Goals.Count) ? Goals[i] : $"A{i + 1}";
                var row = new MatrixRow { Name = goalName };
                LoadMatrixRowValues(row, tree.AP, i, pCount, Features.ToArray());
                APRows.Add(row);
            }
        }

        private void LoadMatrixRowValues(MatrixRow row, int[,] matrix, int rowIndex, int columnCount, string[] featureNames)
        {
            if (matrix != null && matrix.GetLength(0) > rowIndex)
            {
                int matrixCols = matrix.GetLength(1);
                for (int j = 0; j < Math.Min(columnCount, matrixCols); j++)
                {
                    row.SetValue(featureNames[j], matrix[rowIndex, j]);
                }
                for (int j = matrixCols; j < columnCount; j++)
                {
                    row.SetValue(featureNames[j], 0);
                }
            }
            else
            {
                foreach (var feature in featureNames)
                {
                    row.SetValue(feature, 0);
                }
            }
        }

        private void ApplySizes()
        {
            if (FeaturesCount <= 0)
            {
                FeaturesCount = 3;
            }

            if (GoalsCount <= 0)
            {
                GoalsCount = 2;
            }

            if (FeaturesCount > 0 && GoalsCount > 0)
            {
                var currentFeatureNames = Features.ToArray();
                var currentGoalNames = Goals.ToArray();
                
                ResizeMatrices(FeaturesCount, GoalsCount, currentFeatureNames, currentGoalNames);
            }
        }

        private void ResizeMatrices(int newFeaturesCount, int newGoalsCount, string[] currentFeatureNames, string[] currentGoalNames)
        {
            var newFeatureNames = new List<string>();
            for (int i = 0; i < newFeaturesCount; i++)
            {
                if (i < currentFeatureNames.Length)
                {
                    newFeatureNames.Add(currentFeatureNames[i]);
                }
                else
                {
                    newFeatureNames.Add($"P{i + 1}");
                }
            }
            
            var newGoalNames = new List<string>();
            for (int i = 0; i < newGoalsCount; i++)
            {
                if (i < currentGoalNames.Length)
                {
                    newGoalNames.Add(currentGoalNames[i]);
                }
                else
                {
                    newGoalNames.Add($"A{i + 1}");
                }
            }
            
            // Обновляем коллекции Features и Goals
            Features.Clear();
            foreach (var feature in newFeatureNames)
            {
                Features.Add(feature);
            }
            OnPropertyChanged(nameof(Features));
            
            Goals.Clear();
            foreach (var goal in newGoalNames)
            {
                Goals.Add(goal);
            }
            OnPropertyChanged(nameof(Goals));

            foreach (var row in EPRows)
            {
                var currentValues = new Dictionary<string, int>();
                foreach (var feature in currentFeatureNames)
                {
                    if (row.Values.ContainsKey(feature))
                    {
                        currentValues[feature] = row.GetValue(feature);
                    }
                }
                
                row.Values.Clear();
                foreach (var feature in newFeatureNames)
                {
                    row.SetValue(feature, currentValues.ContainsKey(feature) ? currentValues[feature] : 0);
                }
                row.NotifyValuesChanged();
            }

            var oldGoalsCount = APRows.Count;
            
            if (newGoalsCount > oldGoalsCount)
            {
                for (int i = oldGoalsCount; i < newGoalsCount; i++)
                {
                    var row = new MatrixRow { Name = newGoalNames[i] };
                    foreach (var feature in newFeatureNames)
                    {
                        row.SetValue(feature, 0);
                    }
                    APRows.Add(row);
                }
            }
            else if (newGoalsCount < oldGoalsCount)
            {
                while (APRows.Count > newGoalsCount)
                {
                    APRows.RemoveAt(APRows.Count - 1);
                }
            }
            
            for (int i = 0; i < APRows.Count && i < newGoalNames.Count; i++)
            {
                var row = APRows[i];
                row.Name = newGoalNames[i];
                
                var currentValues = new Dictionary<string, int>();
                foreach (var feature in currentFeatureNames)
                {
                    if (row.Values.ContainsKey(feature))
                    {
                        currentValues[feature] = row.GetValue(feature);
                    }
                }
                
                row.Values.Clear();
                foreach (var feature in newFeatureNames)
                {
                    row.SetValue(feature, currentValues.ContainsKey(feature) ? currentValues[feature] : 0);
                }
                row.NotifyValuesChanged();
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
            
            var tree = ProjectData.CurrentTree;
            tree.FeatureNames = new List<string>(Features);
        }
        
        public void RenameFeature(int index, string newName)
        {
            if (index < 0 || index >= Features.Count || string.IsNullOrWhiteSpace(newName))
                return;
                
            var oldName = Features[index];
            if (oldName == newName)
                return;
            
            Features[index] = newName;
            
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
        
        public void RenameGoal(int index, string newName)
        {
            if (index < 0 || index >= Goals.Count || string.IsNullOrWhiteSpace(newName))
                return;
                
            var oldName = Goals[index];
            if (oldName == newName)
                return;
            
            Goals[index] = newName;
            
            if (index < APRows.Count)
            {
                APRows[index].Name = newName;
            }
            
            OnPropertyChanged(nameof(Goals));
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
        
        public void SaveAllMatricesData()
        {
            var ep = ConvertRowsToMatrix(EPRows, Features.ToArray());
            var ap = ConvertRowsToMatrix(APRows, Features.ToArray());

            ProjectData.UpdateMatrices(ep, ap);
            
            var tree = ProjectData.CurrentTree;
            tree.FeatureNames = new List<string>(Features);
            tree.GoalNames = new List<string>(Goals);
        }
    }
}

