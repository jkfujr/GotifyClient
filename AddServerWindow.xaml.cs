using System.Windows;

namespace GotifyClient
{
    public partial class AddServerWindow : Window
    {
        public string ServerName { get; private set; }
        public string ServerUrl { get; private set; }
        public string ClientName { get; private set; }
        public string ClientToken { get; private set; }

        public AddServerWindow()
        {
            InitializeComponent();
        }

        private void AddButton_Click(object sender, RoutedEventArgs e)
        {
            ServerName = ServerNameTextBox.Text;
            ServerUrl = ServerUrlTextBox.Text;
            ClientName = ClientNameTextBox.Text;
            ClientToken = ClientTokenTextBox.Text;

            if (string.IsNullOrEmpty(ServerName) || string.IsNullOrEmpty(ServerUrl))
            {
                System.Windows.MessageBox.Show("请填写服务器别名和URL。", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
