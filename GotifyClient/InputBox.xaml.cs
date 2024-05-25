using System.Windows;

namespace GotifyClient
{
    public partial class InputBox : Window
    {
        public string InputText { get; private set; }

        public InputBox(string prompt, string title, string defaultValue = "")
        {
            InitializeComponent();
            this.Title = title;
            PromptTextBlock.Text = prompt;
            InputTextBox.Text = defaultValue;
            InputTextBox.Focus();
            InputTextBox.SelectAll();
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            this.InputText = InputTextBox.Text;
            this.DialogResult = true;
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
        }

        public static string Show(string prompt, string title, string defaultValue = "")
        {
            InputBox inputBox = new InputBox(prompt, title, defaultValue);
            if (inputBox.ShowDialog() == true)
            {
                return inputBox.InputText;
            }
            return null;
        }
    }
}
