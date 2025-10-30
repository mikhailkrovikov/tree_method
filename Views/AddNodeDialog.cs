using System.Windows;
using System.Windows.Controls;
using TreeMethod.Models;

namespace TreeMethod.Views
{
    // ---------- ДОПОЛНИТЕЛЬНОЕ ОКНО ----------
    public partial class AddNodeDialog : Window
    {
        public (string Name, NodeType Type) NewNodeData { get; private set; }

        public AddNodeDialog(Node parent, string existingName = "", NodeType existingType = NodeType.Leaf)
        {
            Title = "Добавление / Изменение узла";
            Width = 400;
            Height = 250;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            ResizeMode = ResizeMode.NoResize;

            var panel = new StackPanel { Margin = new Thickness(20) };
            panel.Children.Add(new TextBlock
            {
                Text = $"Родитель: {parent.Name}",
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 0, 10)
            });

            panel.Children.Add(new TextBlock { Text = "Имя узла:" });
            var nameBox = new TextBox { Text = existingName, Margin = new Thickness(0, 0, 0, 10) };
            panel.Children.Add(nameBox);

            panel.Children.Add(new TextBlock { Text = "Тип узла:" });
            var typeBox = new ComboBox { Margin = new Thickness(0, 0, 0, 15), Height = 28 };
            typeBox.ItemsSource = Enum.GetValues(typeof(NodeType));
            typeBox.SelectedItem = existingType;
            panel.Children.Add(typeBox);

            var okButton = new Button { Content = "✅ OK", Width = 100, Height = 32, HorizontalAlignment = HorizontalAlignment.Center };
            okButton.Click += (s, e) =>
            {
                if (string.IsNullOrWhiteSpace(nameBox.Text))
                {
                    MessageBox.Show("Введите имя узла.");
                    return;
                }

                NewNodeData = (nameBox.Text, (NodeType)typeBox.SelectedItem);
                DialogResult = true;
            };

            panel.Children.Add(okButton);
            Content = panel;
        }
    }
}
