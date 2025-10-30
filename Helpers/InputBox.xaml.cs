using System.Windows;

namespace TreeMethod.Helpers
{
    public partial class InputBox : Window
    {
        public string Value => ValueBox.Text;
        public InputBox(string prompt, string defaultValue = "")
        {
            InitializeComponent();
            PromptText.Text = prompt;
            ValueBox.Text = defaultValue;
            ValueBox.Focus();
        }
        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }
        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
