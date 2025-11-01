using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using TreeMethod.Models;

namespace TreeMethod.Views
{
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
            
            var nodeTypeItems = new List<KeyValuePair<NodeType, string>>
            {
                new KeyValuePair<NodeType, string>(NodeType.And, "И"),
                new KeyValuePair<NodeType, string>(NodeType.Or, "ИЛИ"),
                new KeyValuePair<NodeType, string>(NodeType.Leaf, "Висячий")
            };
            
            typeBox.ItemsSource = nodeTypeItems;
            typeBox.DisplayMemberPath = "Value";
            typeBox.SelectedValuePath = "Key";
            typeBox.SelectedValue = existingType;
            panel.Children.Add(typeBox);

            var okButton = new Button 
            { 
                Content = "✅ OK", 
                Width = 100, 
                Height = 32, 
                HorizontalAlignment = HorizontalAlignment.Center,
                IsEnabled = !string.IsNullOrWhiteSpace(existingName) && typeBox.SelectedValue != null,
                IsDefault = true
            };
            
            okButton.Click += (s, e) =>
            {
                if (typeBox.SelectedValue is NodeType selectedType)
                {
                    NewNodeData = (nameBox.Text.Trim(), selectedType);
                    DialogResult = true;
                }
            };
            
            void UpdateOkButtonState()
            {
                okButton.IsEnabled = !string.IsNullOrWhiteSpace(nameBox.Text.Trim()) && typeBox.SelectedValue != null;
            }
            
            nameBox.TextChanged += (s, e) => UpdateOkButtonState();
            typeBox.SelectionChanged += (s, e) => UpdateOkButtonState();

            panel.Children.Add(okButton);
            Content = panel;
            
            nameBox.Focus();
            if (!string.IsNullOrWhiteSpace(existingName))
            {
                nameBox.SelectAll();
            }
        }
    }
}
