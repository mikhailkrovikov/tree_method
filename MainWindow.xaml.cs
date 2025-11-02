using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Win32;
using TreeMethod.Helpers;
using TreeMethod.Models;
using TreeMethod.Views;
using TreeMethod.ViewModels;

namespace TreeMethod
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            DataContext = new MainViewModel();
        }
    }

    

    public class MainViewModel : INotifyPropertyChanged
    {
        private bool _isBusy;
        private string _rtResult = "";
        private string _rationalSolutions = "";
        private string _bestSolution = "";
        private string _currentFilePath = null;
        private bool _canRunCalculation = false;
        private string _calculationDisabledReason = "";

        public MainViewModel()
        {
            TreePage = new TreePage();
            MatricesPage = new MatricesPage();

            NewProjectCommand = new RelayCommand(_ => NewProject());
            OpenCommand = new RelayCommand(_ => Open());
            SaveCommand = new RelayCommand(_ => Save());
            SaveAsCommand = new RelayCommand(_ => SaveAs());
            ExitCommand = new RelayCommand(_ => Exit());

            RunCalculationCommand = new RelayCommand(_ => RunCalculation(), _ => CanRunCalculation && !IsBusy);
            
            ProjectData.TreeChanged += OnTreeChanged;
            ProjectData.MatricesChanged += OnTreeChanged;
            UpdateCalculationButtonState();
        }

        private void OnTreeChanged()
        {
            UpdateCalculationButtonState();
        }

        private void UpdateCalculationButtonState()
        {
            var tree = ProjectData.CurrentTree;
            
            if (tree == null || tree.Nodes.Count == 0)
            {
                CanRunCalculation = false;
                CalculationDisabledReason = "Структура дерева пуста. Добавьте узлы в дерево.";
                return;
            }

            if (tree.EP == null || tree.AP == null)
            {
                CanRunCalculation = false;
                CalculationDisabledReason = "Матрицы E×P и A×P не заданы. Заполните матрицы на вкладке 'Матрицы'.";
                return;
            }

            var epCols = tree.EP.GetLength(1);
            var apCols = tree.AP.GetLength(1);

            if (epCols != apCols)
            {
                CanRunCalculation = false;
                CalculationDisabledReason = $"Несоответствие размеров матриц: E×P имеет {epCols} признаков, а A×P имеет {apCols} признаков. Количество признаков должно совпадать.";
                return;
            }

            CanRunCalculation = true;
            CalculationDisabledReason = "";
        }

        #region Свойства

        public bool IsBusy
        {
            get => _isBusy;
            set { _isBusy = value; OnPropertyChanged(); CommandManager.InvalidateRequerySuggested(); }
        }

        public string RTResult
        {
            get => _rtResult;
            set { _rtResult = value; OnPropertyChanged(); }
        }

        public string RationalSolutions
        {
            get => _rationalSolutions;
            set { _rationalSolutions = value; OnPropertyChanged(); }
        }

        public string BestSolution
        {
            get => _bestSolution;
            set { _bestSolution = value; OnPropertyChanged(); }
        }

        public bool CanRunCalculation
        {
            get => _canRunCalculation;
            set { _canRunCalculation = value; OnPropertyChanged(); CommandManager.InvalidateRequerySuggested(); }
        }

        public string CalculationDisabledReason
        {
            get => _calculationDisabledReason;
            set { _calculationDisabledReason = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasCalculationDisabledReason)); }
        }

        public bool HasCalculationDisabledReason => !string.IsNullOrWhiteSpace(_calculationDisabledReason);

        #endregion

        #region Страницы

        public Page TreePage { get; }
        public Page MatricesPage { get; }

        #endregion

        #region Команды

        public ICommand NewProjectCommand { get; }
        public ICommand OpenCommand { get; }
        public ICommand SaveCommand { get; }
        public ICommand SaveAsCommand { get; }
        public ICommand ExitCommand { get; }

        public ICommand RunCalculationCommand { get; }

        #endregion

        #region Методы команд

        private void NewProject()
        {
            var result = MessageBox.Show(
                "Создать новый проект? Все несохранённые изменения будут потеряны.",
                "Новый проект",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);
            
            if (result == MessageBoxResult.Yes)
            {
                var newTree = new TreeModel();
                if (newTree.Nodes.Count == 0)
                {
                    newTree.Nodes.Add(new Node 
                    { 
                        Id = 0, 
                        Name = "Система", 
                        Type = NodeType.And 
                    });
                }
                // Назначаем уровни новому дереву
                newTree.AssignLevels();
                ProjectData.CurrentTree = newTree;
                _currentFilePath = null;
                
                (TreePage as TreePage)?.RefreshGraph();
                (MatricesPage as MatricesPage)?.RefreshMatrices();
                
                ProjectData.RaiseTreeChanged();
            }
        }
        
        private void Open()
        {
            var dialog = new OpenFileDialog
            {
                Filter = "JSON файлы (*.json)|*.json|Все файлы (*.*)|*.*",
                Title = "Открыть проект"
            };
            
            if (dialog.ShowDialog() == true)
            {
                try
                {
                    var loaded = TreeModel.LoadProject(dialog.FileName);
                    if (loaded == null)
                    {
                        MessageBox.Show("Ошибка загрузки: не удалось прочитать файл.", 
                            "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }
                    
                    ProjectData.CurrentTree = loaded;
                    _currentFilePath = dialog.FileName;
                    
                    (TreePage as TreePage)?.RefreshGraph();
                    (MatricesPage as MatricesPage)?.RefreshMatrices();
                    
                    ProjectData.RaiseTreeChanged();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка при загрузке проекта:\n{ex.Message}", 
                        "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
        
        private void Save()
        {
            SaveAs();
        }
        
        private void SaveAs()
        {
            var dialog = new SaveFileDialog
            {
                Filter = "JSON файлы (*.json)|*.json|Все файлы (*.*)|*.*",
                Title = "Сохранить проект как",
                FileName = "project.json"
            };
            
            if (dialog.ShowDialog() == true)
            {
                try
                {
                    var matricesPage = MatricesPage;
                    if (matricesPage != null)
                    {
                        var matricesViewModel = matricesPage.DataContext as MatricesPageViewModel;
                        if (matricesViewModel != null)
                        {
                            matricesViewModel.SaveAllMatricesData();
                        }
                    }
                    
                    ProjectData.CurrentTree.SaveProject(dialog.FileName);
                    _currentFilePath = dialog.FileName;
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка при сохранении:\n{ex.Message}", 
                        "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
        
        private void Exit() => Application.Current.Shutdown();

        private void RunCalculation()
        {
            var matricesPage = MatricesPage as MatricesPage;
            if (matricesPage != null)
            {
                var matricesViewModel = matricesPage.DataContext as MatricesPageViewModel;
                if (matricesViewModel != null)
                {
                    matricesViewModel.SaveAllMatricesData();
                    UpdateCalculationButtonState();
                }
            }

            IsBusy = true;

            Task.Run(() =>
            {
                var tree = ProjectData.CurrentTree;

                var epRows = tree.EP.GetLength(0);
                var epCols = tree.EP.GetLength(1);
                var apRows = tree.AP.GetLength(0);
                var apCols = tree.AP.GetLength(1);
                var nonRootNodes = tree.Nodes.Count(n => 
                {
                    var allChildren = tree.Nodes.SelectMany(node => node.Children).ToHashSet();
                    return !allChildren.Contains(n.Id);
                });


                if (tree.GoalWeights == null || tree.GoalWeights.Length != tree.AP.GetLength(0))
                {
                    tree.GoalWeights = Enumerable.Repeat(1, tree.AP.GetLength(0)).ToArray();
                }

                var leafCount = tree.Nodes.Count(n => n.Children.Count == 0);
                if (leafCount > 15)
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        var result = MessageBox.Show($"Дерево содержит {leafCount} листьев.\n" +
                                                    $"Расчет может занять продолжительное время.\n\n" +
                                                    $"Продолжить?",
                                                    "Предупреждение", 
                                                    MessageBoxButton.YesNo, 
                                                    MessageBoxImage.Question);
                        if (result == MessageBoxResult.No)
                        {
                            IsBusy = false;
                            return;
                        }
                    });
                }

                try
                {
                    int theoreticalCount = Algorithm1.CalculateRT(tree);
                    var rationalSolutions = Algorithm2.FindSolutions(tree);

                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        IsBusy = false;
                        UpdateCalculationResults(theoreticalCount, rationalSolutions);
                    });
                }
                catch (Exception ex)
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        IsBusy = false;
                        MessageBox.Show($"Ошибка при выполнении расчёта:\n{ex.Message}\n\n" +
                                      $"Тип ошибки: {ex.GetType().Name}",
                                      "Ошибка расчёта", MessageBoxButton.OK, MessageBoxImage.Error);
                    });
                }
            });
        }
        
        private void UpdateCalculationResults(int theoreticalCount, List<RationalSolution> rationalSolutions)
        {
            RTResult = theoreticalCount.ToString();
            
            if (rationalSolutions.Any())
            {
                var solutionsText = string.Join("\n", rationalSolutions.Select((r, idx) => 
                    $"{idx + 1}. {string.Join(", ", r.Elements)} (оценка: {r.Score})"));
                RationalSolutions = solutionsText;
                BestSolution = rationalSolutions.First().ToString();
            }
            else
            {
                RationalSolutions = "Решения не найдены";
                BestSolution = "";
            }
        }


        #endregion

        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string prop = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));
    }
}