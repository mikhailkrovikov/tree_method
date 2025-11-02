using System;
using System.Windows;
using System.Windows.Controls;

namespace TreeMethod.Views
{
    public partial class ChangeLevelDialog : Window
    {
        public int NewLevel { get; private set; }
        public bool IsManual { get; private set; }

        public ChangeLevelDialog(string nodeName, int currentLevel, bool isManual)
        {
            Title = "Изменение уровня узла";
            Width = 400;
            Height = 220;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            ResizeMode = ResizeMode.NoResize;

            var panel = new StackPanel { Margin = new Thickness(20) };
            
            panel.Children.Add(new TextBlock
            {
                Text = $"Узел: {nodeName}",
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 0, 15)
            });

            panel.Children.Add(new TextBlock 
            { 
                Text = $"Текущий уровень: {currentLevel}",
                Margin = new Thickness(0, 0, 0, 10)
            });

            panel.Children.Add(new TextBlock { Text = "Новый уровень:", Margin = new Thickness(0, 0, 0, 5) });
            var levelBox = new TextBox 
            { 
                Text = currentLevel.ToString(), 
                Margin = new Thickness(0, 0, 0, 15)
            };
            
            levelBox.PreviewTextInput += (s, e) =>
            {
                // Разрешаем только цифры и минус
                foreach (char c in e.Text)
                {
                    if (!char.IsDigit(c) && c != '-')
                    {
                        e.Handled = true;
                        return;
                    }
                }
            };
            panel.Children.Add(levelBox);

            var manualCheckBox = new CheckBox
            {
                Content = "Ручной уровень (не пересчитывать автоматически)",
                IsChecked = isManual,
                Margin = new Thickness(0, 0, 0, 15)
            };
            panel.Children.Add(manualCheckBox);

            var buttonPanel = new StackPanel 
            { 
                Orientation = Orientation.Horizontal, 
                HorizontalAlignment = HorizontalAlignment.Center 
            };
            
            var okButton = new Button 
            { 
                Content = "✅ OK", 
                Width = 80, 
                Height = 30, 
                Margin = new Thickness(5) 
            };
            okButton.Click += (s, e) =>
            {
                if (int.TryParse(levelBox.Text, out int level))
                {
                    NewLevel = level;
                    IsManual = manualCheckBox.IsChecked ?? false;
                    DialogResult = true;
                }
                else
                {
                    MessageBox.Show("Введите корректное числовое значение для уровня.", 
                        "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            };

            var cancelButton = new Button 
            { 
                Content = "Отмена", 
                Width = 80, 
                Height = 30, 
                Margin = new Thickness(5) 
            };
            cancelButton.Click += (s, e) => DialogResult = false;

            var autoButton = new Button 
            { 
                Content = "Авто", 
                Width = 80, 
                Height = 30, 
                Margin = new Thickness(5),
                ToolTip = "Вернуться к автоматическому вычислению уровня"
            };
            autoButton.Click += (s, e) =>
            {
                IsManual = false;
                // Устанавливаем уровень в текущий, чтобы при следующем AssignLevels он пересчитался автоматически
                // Но для корректной работы нужно будет пересчитать уровни после закрытия диалога
                NewLevel = currentLevel; // Это будет перезаписано при AssignLevels
                DialogResult = true;
            };

            buttonPanel.Children.Add(okButton);
            buttonPanel.Children.Add(cancelButton);
            buttonPanel.Children.Add(autoButton);
            panel.Children.Add(buttonPanel);

            Content = panel;
            
            // Фокус на поле ввода уровня
            Loaded += (s, e) => levelBox.Focus();
        }
    }
}

