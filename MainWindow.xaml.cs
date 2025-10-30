using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using TreeMethod.Helpers;
using TreeMethod.Models;
using TreeMethod.Models.TreeMethod.Models;
using TreeMethod.Views;

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
        private Page _currentPage;
        private string _statusText = "Готово";
        private bool _isBusy;

        

        public MainViewModel()
        {
            // Инициализация страниц
            TreePage = new TreePage();
            MatricesPage = new MatricesPage();
            CalculationPage = new CalculationPage();
            ResultsPage = new ResultsPage();

            // Начальная страница
            CurrentPage = TreePage;

            // Команды
            NewProjectCommand = new RelayCommand(_ => NewProject());
            OpenCommand = new RelayCommand(_ => Open());
            SaveCommand = new RelayCommand(_ => Save());
            SaveAsCommand = new RelayCommand(_ => SaveAs());
            ExitCommand = new RelayCommand(_ => Exit());

            ShowTreePageCommand = new RelayCommand(_ => CurrentPage = TreePage);
            ShowMatricesPageCommand = new RelayCommand(_ => CurrentPage = MatricesPage);
            ShowCalculationPageCommand = new RelayCommand(_ => CurrentPage = CalculationPage);
            ShowResultsPageCommand = new RelayCommand(_ => CurrentPage = ResultsPage);

            RunCalculationCommand = new RelayCommand(_ => RunCalculation());
            ExportTreeCommand = new RelayCommand(_ => ExportTree());
            AboutCommand = new RelayCommand(_ => ShowAbout());
        }

        #region Свойства

        public Page CurrentPage
        {
            get => _currentPage;
            set { _currentPage = value; OnPropertyChanged(); }
        }

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

        #endregion

        #region Страницы

        public Page TreePage { get; }
        public Page MatricesPage { get; }
        public Page CalculationPage { get; }
        public Page ResultsPage { get; }

        #endregion

        #region Команды

        public ICommand NewProjectCommand { get; }
        public ICommand OpenCommand { get; }
        public ICommand SaveCommand { get; }
        public ICommand SaveAsCommand { get; }
        public ICommand ExitCommand { get; }

        public ICommand ShowTreePageCommand { get; }
        public ICommand ShowMatricesPageCommand { get; }
        public ICommand ShowCalculationPageCommand { get; }
        public ICommand ShowResultsPageCommand { get; }

        public ICommand RunCalculationCommand { get; }
        public ICommand ExportTreeCommand { get; }
        public ICommand AboutCommand { get; }

        #endregion

        #region Методы команд

        private void NewProject() => StatusText = "Создан новый проект";
        private void Open() => StatusText = "Открытие проекта...";
        private void Save() => StatusText = "Сохранение проекта...";
        private void SaveAs() => StatusText = "Сохранение как...";
        private void Exit() => System.Windows.Application.Current.Shutdown();

        private void RunCalculation()
        {
            IsBusy = true;
            StatusText = "Выполняется расчёт...";

            System.Threading.Tasks.Task.Run(() =>
            {
                var tree = ProjectData.CurrentTree;

                // 🧩 Проверка
                if (tree == null || tree.Nodes.Count == 0)
                {
                    Application.Current.Dispatcher.Invoke(() =>
                        MessageBox.Show("Структура дерева пуста!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning));
                    IsBusy = false;
                    return;
                }

                if (tree.EP == null || tree.AP == null)
                {
                    Application.Current.Dispatcher.Invoke(() =>
                        MessageBox.Show("Матрицы E×P и A×P не заданы или не сохранены.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning));
                    IsBusy = false;
                    return;
                }

                // 🧩 Автоматически подгоняем веса целей
                if (tree.GoalWeights == null || tree.GoalWeights.Length != tree.AP.GetLength(0))
                {
                    tree.GoalWeights = Enumerable.Repeat(1, tree.AP.GetLength(0)).ToArray();
                }

                int theoreticalCount = Algorithm1.CalculateRT(tree);
                var rationalSolutions = Algorithm2.FindSolutions(tree);

                System.Threading.Thread.Sleep(800); // имитация задержки

                Application.Current.Dispatcher.Invoke(() =>
                {
                    IsBusy = false;
                    string msg = $"|RT| = {theoreticalCount}\nРациональные решения:\n" +
                                 $"{string.Join("\n", rationalSolutions.Select(r => r.ToString()))}";
                    StatusText = "Расчёт завершён";
                    MessageBox.Show(msg, "Результаты расчёта", MessageBoxButton.OK, MessageBoxImage.Information);
                });
            });
        }


        private void ExportTree() => StatusText = "Экспорт дерева...";
        private void ShowAbout() => System.Windows.MessageBox.Show("И-ИЛИ Дерево\nВерсия 0.1", "О программе");

        #endregion

        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string prop = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));
    }
}