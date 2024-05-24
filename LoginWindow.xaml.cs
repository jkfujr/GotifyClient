using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace GotifyClient
{
    public partial class LoginWindow : Window
    {
        private const string ConfigFilePath = "config.yaml";
        private Config config;

        public string ServerUrl { get; private set; }
        public string ServerAlias { get; private set; }
        public string ApiToken { get; private set; }

        public LoginWindow()
        {
            InitializeComponent();
            LoadConfig();
            PopulateServerComboBox();
        }

        private void LoadConfig()
        {
            if (File.Exists(ConfigFilePath))
            {
                var deserializer = new DeserializerBuilder()
                    .WithNamingConvention(CamelCaseNamingConvention.Instance)
                    .Build();

                using (var reader = new StreamReader(ConfigFilePath))
                {
                    config = deserializer.Deserialize<Config>(reader);
                }
            }
            else
            {
                config = new Config
                {
                    Server = new Dictionary<string, ServerConfig>(),
                    Client = new Dictionary<string, ClientConfig>()
                };
            }
        }

        private void SaveConfig()
        {
            var serializer = new SerializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance)
                .Build();

            using (var writer = new StreamWriter(ConfigFilePath))
            {
                serializer.Serialize(writer, config);
            }
        }

        private void PopulateServerComboBox()
        {
            ServerComboBox.Items.Clear();
            if (config.Client.Any())
            {
                foreach (var client in config.Client)
                {
                    var serverAlias = client.Value.Server;
                    ServerComboBox.Items.Add($"[{serverAlias}] {client.Key}");
                }
            }
            else if (config.Server.Any())
            {
                foreach (var server in config.Server)
                {
                    ServerComboBox.Items.Add($"[{server.Key}]");
                }
            }

            if (ServerComboBox.Items.Count > 0)
            {
                ServerComboBox.SelectedIndex = 0;
            }
        }

        private void AddServerMenuItem_Click(object sender, RoutedEventArgs e)
        {
            var addServerWindow = new AddServerWindow();
            if (addServerWindow.ShowDialog() == true)
            {
                config.Server[addServerWindow.ServerName] = new ServerConfig { Host = addServerWindow.ServerUrl };
                if (!string.IsNullOrEmpty(addServerWindow.ClientName) && !string.IsNullOrEmpty(addServerWindow.ClientToken))
                {
                    config.Client[addServerWindow.ClientName] = new ClientConfig
                    {
                        Server = addServerWindow.ServerName,
                        Token = addServerWindow.ClientToken
                    };
                }
                SaveConfig();
                PopulateServerComboBox();
                System.Windows.MessageBox.Show("服务器已添加。", "信息", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void LoginButton_Click(object sender, RoutedEventArgs e)
        {
            if (ServerComboBox.SelectedItem == null)
            {
                System.Windows.MessageBox.Show("请先选择服务器。", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var selectedItem = ServerComboBox.SelectedItem.ToString();
            var selectedServer = selectedItem.Split(']')[0].TrimStart('[');
            ServerAlias = selectedServer;
            var clientName = selectedItem.Contains("] ") ? selectedItem.Split(new string[] { "] " }, StringSplitOptions.None)[1] : null;

            ServerUrl = config.Server[selectedServer].Host;
            if (clientName != null && config.Client.ContainsKey(clientName))
            {
                var existingToken = config.Client[clientName].Token;
                ApiToken = string.IsNullOrEmpty(TokenTextBox.Text) ? existingToken : TokenTextBox.Text;
            }
            else
            {
                ApiToken = TokenTextBox.Text;
            }

            if (string.IsNullOrEmpty(ApiToken))
            {
                System.Windows.MessageBox.Show("请填写Token。", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
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

    public class Config
    {
        public Dictionary<string, ServerConfig> Server { get; set; }
        public Dictionary<string, ClientConfig> Client { get; set; }
    }

    public class ServerConfig
    {
        public string Host { get; set; }
    }

    public class ClientConfig
    {
        public string Server { get; set; }
        public string Token { get; set; }
    }
}
