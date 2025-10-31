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
        private string _statusText = "Готово";
        private bool _isBusy;
        private string _rtResult = "";
        private string _rationalSolutions = "";
        private string _bestSolution = "";
        private string _currentFilePath = null;

        public MainViewModel()
        {
            // Инициализация страниц
            TreePage = new TreePage();
            MatricesPage = new MatricesPage();

            // Команды
            NewProjectCommand = new RelayCommand(_ => NewProject());
            OpenCommand = new RelayCommand(_ => Open());
            SaveCommand = new RelayCommand(_ => Save());
            SaveAsCommand = new RelayCommand(_ => SaveAs());
            ExitCommand = new RelayCommand(_ => Exit());

            RunCalculationCommand = new RelayCommand(_ => RunCalculation());
        }

        #region Свойства

        public string StatusText
        {
            get => _statusText;
            set { _statusText = value; OnPropertyChanged(); }
        }

        public bool IsBusy
        {
            get => _isBusy;
            set { _isBusy = value; OnPropertyChanged(); }
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
                ProjectData.CurrentTree = newTree;
                _currentFilePath = null;
                ProjectData.RaiseTreeChanged();
                
                // Обновляем интерфейс
                (TreePage as TreePage)?.RefreshGraph();
                (MatricesPage as MatricesPage)?.RefreshMatrices();
                
                StatusText = "Создан новый проект";
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
                    
                    // Обновляем интерфейс
                    (TreePage as TreePage)?.RefreshGraph();
                    (MatricesPage as MatricesPage)?.RefreshMatrices();
                    
                    ProjectData.RaiseTreeChanged();
                    StatusText = $"Проект загружен: {Path.GetFileName(dialog.FileName)}";
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка при загрузке проекта:\n{ex.Message}", 
                        "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    StatusText = "Ошибка загрузки";
                }
            }
        }
        
        private void Save()
        {
            // Всегда показываем диалог сохранения
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
                    // Сохраняем матрицы и FeatureNames перед сохранением проекта
                    var matricesPage = MatricesPage;
                    if (matricesPage != null)
                    {
                        var matricesViewModel = matricesPage.DataContext as MatricesPageViewModel;
                        if (matricesViewModel != null)
                        {
                            // Сохраняем матрицы и FeatureNames
                            matricesViewModel.SaveAllMatricesData();
                        }
                    }
                    
                    ProjectData.CurrentTree.SaveProject(dialog.FileName);
                    _currentFilePath = dialog.FileName;
                    StatusText = $"Проект сохранён: {System.IO.Path.GetFileName(dialog.FileName)}";
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка при сохранении:\n{ex.Message}", 
                        "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    StatusText = "Ошибка сохранения";
                }
            }
        }
        
        private void Exit() => Application.Current.Shutdown();

        private void RunCalculation()
        {
            IsBusy = true;
            StatusText = "Выполняется расчёт...";

            Task.Run(() =>
            {
                var tree = ProjectData.CurrentTree;

                // Проверка наличия дерева
                if (tree == null || tree.Nodes.Count == 0)
                {
                    Application.Current.Dispatcher.Invoke(() =>
                        MessageBox.Show("Структура дерева пуста!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning));
                    IsBusy = false;
                    return;
                }

                // Проверка наличия матриц
                if (tree.EP == null || tree.AP == null)
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        MessageBox.Show("Матрицы E×P и A×P не заданы или не сохранены.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                        IsBusy = false;
                        StatusText = "Ошибка: матрицы не заданы";
                    });
                    return;
                }

                // Валидация размеров матриц
                var epRows = tree.EP.GetLength(0);
                var epCols = tree.EP.GetLength(1);
                var apRows = tree.AP.GetLength(0);
                var apCols = tree.AP.GetLength(1);
                var nonRootNodes = tree.Nodes.Count(n => 
                {
                    var allChildren = tree.Nodes.SelectMany(node => node.Children).ToHashSet();
                    return !allChildren.Contains(n.Id);
                });

                // Проверка соответствия размеров
                if (epCols != apCols)
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        MessageBox.Show($"Несоответствие размеров матриц:\n" +
                                      $"Матрица E×P имеет {epCols} столбцов (признаков),\n" +
                                      $"а матрица A×P имеет {apCols} столбцов (признаков).\n\n" +
                                      "Количество признаков должно совпадать в обеих матрицах.",
                                      "Ошибка валидации", MessageBoxButton.OK, MessageBoxImage.Warning);
                        IsBusy = false;
                        StatusText = "Ошибка: несоответствие размеров матриц";
                    });
                    return;
                }

                if (nonRootNodes > epRows)
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        MessageBox.Show($"Внимание: Количество узлов в дереве ({nonRootNodes}) больше, чем строк в матрице E×P ({epRows}).\n" +
                                      $"Некоторые узлы не будут учтены при расчете.",
                                      "Предупреждение", MessageBoxButton.OK, MessageBoxImage.Warning);
                    });
                }

                // Автоматически подгоняем веса целей, если не заданы
                if (tree.GoalWeights == null || tree.GoalWeights.Length != tree.AP.GetLength(0))
                {
                    tree.GoalWeights = Enumerable.Repeat(1, tree.AP.GetLength(0)).ToArray();
                }

                // Предупреждение о больших расчетах
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
                            StatusText = "Расчёт отменён пользователем";
                            return;
                        }
                    });
                }

                // Выполняем расчёты с обработкой ошибок
                try
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        StatusText = "Выполняется расчёт (шаг 1/2: вычисление теоретического множества)...";
                    });

                    int theoreticalCount = Algorithm1.CalculateRT(tree);

                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        StatusText = "Выполняется расчёт (шаг 2/2: поиск рациональных решений)...";
                    });

                    var rationalSolutions = Algorithm2.FindSolutions(tree);

                    // Обновляем результаты в UI
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        IsBusy = false;
                        UpdateCalculationResults(theoreticalCount, rationalSolutions);
                        StatusText = $"Расчёт завершён. Найдено решений: {rationalSolutions.Count}";
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
                        StatusText = "Ошибка при выполнении расчёта";
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