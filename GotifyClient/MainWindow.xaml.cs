using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Threading;
using Newtonsoft.Json.Linq;
using Forms = System.Windows.Forms;
using Drawing = System.Drawing;

namespace GotifyClient
{
    public partial class MainWindow : Window
    {
        private string serverUrl;
        private string serverAlias;
        private string apiToken;
        private const string MessageFolder = "message";
        private DispatcherTimer timer;
        private bool isPaused = false;
        private Forms.NotifyIcon notifyIcon;
        private int requestInterval = 3; // 默认请求间隔为3秒
        private bool isDescendingOrder = true; // 默认降序
        private int notificationPriority = 3; // 默认通知优先级

        public MainWindow()
        {
            InitializeComponent();
            ShowLoginWindow();
            InitializeTimer();
            InitializeNotifyIcon();
            ChangeRequestIntervalMenuItem.Header = $"请求间隔: {requestInterval}s"; // 初始化按钮文本
            NotificationPriorityMenuItem.Header = $"通知优先级: {notificationPriority}"; // 初始化通知优先级按钮文本
            PopulateMessageComboBox(); // 在初始化时读取message文件夹
        }

        private void InitializeNotifyIcon()
        {
            notifyIcon = new Forms.NotifyIcon
            {
                Visible = true,
                Icon = Drawing.SystemIcons.Information,
                Text = "GotifyClient"
            };
            notifyIcon.DoubleClick += (s, e) => ShowMainWindow();
        }

        private void ShowNotification(string title, string message)
        {
            notifyIcon.BalloonTipTitle = title;
            notifyIcon.BalloonTipText = message;
            notifyIcon.ShowBalloonTip(3000); // 显示3秒
        }

        private void InitializeTimer()
        {
            timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(requestInterval)
            };
            timer.Tick += (s, e) => RequestData();
            timer.Start();

            // 添加创建消息文件夹的逻辑
            string folderPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, MessageFolder);
            if (!Directory.Exists(folderPath))
            {
                Directory.CreateDirectory(folderPath);
            }
        }

        private void ShowLoginWindow()
        {
            var loginWindow = new LoginWindow();
            if (loginWindow.ShowDialog() == true)
            {
                serverUrl = loginWindow.ServerUrl;
                serverAlias = loginWindow.ServerAlias;
                apiToken = loginWindow.ApiToken;
                UpdateCurrentServerMenuItem();
            }
            else
            {
                // 如果用户没有成功登录，关闭主窗口
                System.Windows.Application.Current.Shutdown();
            }
        }

        private void UpdateCurrentServerMenuItem()
        {
            CurrentServerMenuItem.Header = $"当前服务器: {serverAlias}";
        }

        // 请求数据
        private async void RequestData()
        {
            if (string.IsNullOrEmpty(serverUrl) || string.IsNullOrEmpty(apiToken))
            {
                Dispatcher.Invoke(() => {
                    OutputTextBox.Text = "服务器 URL 或 Token 为空，请重新登录。";
                });
                return;
            }

            string url = $"{serverUrl}/message?limit=100";
            string apiKey = apiToken;

            using (HttpClient client = new HttpClient())
            {
                client.DefaultRequestHeaders.Add("accept", "application/json");
                client.DefaultRequestHeaders.Add("X-Gotify-Key", apiKey);

                try
                {
                    HttpResponseMessage response = await client.GetAsync(url);
                    response.EnsureSuccessStatusCode();
                    string responseBody = await response.Content.ReadAsStringAsync();
                    Dispatcher.Invoke(() => {
                        UpdateLastRequestTime();
                    });

                    // 先读取本地文件
                    var localFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, MessageFolder, $"{DateTime.Now:yyyyMMdd}.txt");
                    JArray localMessages = new JArray();

                    if (File.Exists(localFilePath))
                    {
                        var localContent = File.ReadAllText(localFilePath);
                        if (!string.IsNullOrEmpty(localContent))
                        {
                            try
                            {
                                localMessages = JArray.Parse(localContent);
                            }
                            catch (Exception ex)
                            {
                                OutputTextBox.Text = $"解析本地文件错误: {ex.Message}";
                            }
                        }
                    }

                    // 处理从服务器获取的数据
                    bool hasNewMessages = SaveMessagesToFile(responseBody, localMessages);
                    if (hasNewMessages)
                    {
                        Dispatcher.Invoke(() => {
                            PopulateMessageComboBox();
                        });
                    }
                }
                catch (HttpRequestException ex)
                {
                    Dispatcher.Invoke(() => {
                        OutputTextBox.Text = $"请求错误: {ex.Message}";
                    });
                }
                catch (Exception ex)
                {
                    Dispatcher.Invoke(() => {
                        OutputTextBox.Text = $"发生异常: {ex.Message}";
                    });
                }
            }
        }


        private void UpdateLastRequestTime()
        {
            LastRequestTimeTextBlock.Text = $"上次请求时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss}";
        }

        private string FormatJsonResponse(string jsonResponse)
        {
            try
            {
                var jsonArray = JArray.Parse(jsonResponse);
                var formattedText = new System.Text.StringBuilder();

                foreach (var message in jsonArray)
                {
                    formattedText.AppendLine($"标题: {message["消息标题"]}");
                    formattedText.AppendLine($"消息: {message["消息"]}");
                    formattedText.AppendLine($"优先级: {message["优先级"]}");
                    formattedText.AppendLine($"日期: {message["时间"]}");
                    formattedText.AppendLine(new string('-', 50));
                }

                return formattedText.ToString();
            }
            catch (Exception ex)
            {
                return $"格式化JSON响应时出错: {ex.Message}";
            }
        }

        /// <summary>
        /// 保存从服务器获取的消息到本地文件
        /// </summary>
        /// <param name="jsonResponse">服务器响应的 JSON 字符串</param>
        /// <param name="localMessages">本地消息数组</param>
        /// <returns>如果有新消息，返回 true；否则返回 false</returns>
        private bool SaveMessagesToFile(string jsonResponse, JArray localMessages)
        {
            var jsonObject = JObject.Parse(jsonResponse);
            var messages = jsonObject["messages"];
            var today = DateTime.Now.ToString("yyyyMMdd"); // 使用本地时间
            var folderPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, MessageFolder);

            // 添加创建消息文件夹的逻辑
            if (!Directory.Exists(folderPath))
            {
                Directory.CreateDirectory(folderPath);
            }

            var filePath = Path.Combine(folderPath, $"{today}.txt");

            var existingIds = new HashSet<int>(localMessages.Select(m => m["消息ID"].Value<int>()));
            bool hasNewMessages = false;

            foreach (var message in messages)
            {
                var messageId = message["id"].Value<int>();
                if (existingIds.Contains(messageId))
                {
                    continue;
                }

                hasNewMessages = true;
                var formattedMessage = new JObject
                {
                    ["消息ID"] = message["id"],
                    ["客户端ID"] = message["appid"],
                    ["消息"] = message["message"],
                    ["消息标题"] = message["title"],
                    ["优先级"] = message["priority"],
                    ["时间"] = DateTime.Parse(message["date"].ToString()).ToString("yyyy-MM-dd HH:mm:ss")
                };

                localMessages.Add(formattedMessage);

                // 调用系统通知推送
                if ((int)formattedMessage["优先级"] >= notificationPriority)
                {
                    Dispatcher.Invoke(() => ShowNotification(formattedMessage["消息标题"].ToString(), formattedMessage["消息"].ToString()));
                }
            }

            // 按消息ID排序
            var sortedMessages = isDescendingOrder
                ? new JArray(localMessages.OrderByDescending(m => (int)m["消息ID"]))
                : new JArray(localMessages.OrderBy(m => (int)m["消息ID"]));

            File.WriteAllText(filePath, sortedMessages.ToString());

            return hasNewMessages;
        }

        private void PopulateMessageComboBox()
        {
            try
            {
                MessageComboBox.Items.Clear();
                var folderPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, MessageFolder);
                var files = Directory.GetFiles(folderPath, "*.txt");

                Regex regex = new Regex(@"^\d{8}\.txt$");
                foreach (var file in files)
                {
                    var fileName = Path.GetFileName(file);
                    if (regex.IsMatch(fileName) && IsValidDate(fileName.Substring(0, 8)))
                    {
                        MessageComboBox.Items.Add(fileName);
                    }
                }

                if (MessageComboBox.Items.Count > 0)
                {
                    MessageComboBox.SelectedIndex = 0;
                    LoadSelectedMessageFile();
                }
            }
            catch (Exception ex)
            {
                OutputTextBox.Text = $"读取消息文件时出错: {ex.Message}";
            }
        }

        private bool IsValidDate(string date)
        {
            if (DateTime.TryParseExact(date, "yyyyMMdd", null, System.Globalization.DateTimeStyles.None, out DateTime _))
            {
                return true;
            }
            return false;
        }

        private void LoadSelectedMessageFile()
        {
            if (MessageComboBox.SelectedItem != null)
            {
                var selectedFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, MessageFolder, MessageComboBox.SelectedItem.ToString());
                if (File.Exists(selectedFile))
                {
                    var fileContent = File.ReadAllText(selectedFile);
                    if (!string.IsNullOrEmpty(fileContent))
                    {
                        var formattedContent = FormatJsonResponse(fileContent);
                        OutputTextBox.Text = formattedContent;
                    }
                }
            }
        }

        private void CurrentServerMenuItem_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!string.IsNullOrEmpty(serverUrl))
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = serverUrl,
                        UseShellExecute = true
                    });
                }
                else
                {
                    Forms.MessageBox.Show("服务器 URL 为空，请重新登录。", "错误", Forms.MessageBoxButtons.OK, Forms.MessageBoxIcon.Error);
                }
            }
            catch (Exception ex)
            {
                OutputTextBox.Text = $"打开服务器 URL 时出错: {ex.Message}";
            }
        }

        private void OpenMessageFolder_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var folderPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, MessageFolder);

                // 添加创建消息文件夹的逻辑
                if (!Directory.Exists(folderPath))
                {
                    Directory.CreateDirectory(folderPath);
                }

                if (Directory.Exists(folderPath))
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = folderPath,
                        UseShellExecute = true
                    });
                }
                else
                {
                    Forms.MessageBox.Show("消息文件夹不存在或创建失败。", "错误", Forms.MessageBoxButtons.OK, Forms.MessageBoxIcon.Error);
                }
            }
            catch (Exception ex)
            {
                OutputTextBox.Text = $"打开消息文件夹时出错: {ex.Message}";
            }
        }

        private void ChangeRequestInterval_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string input = InputBox.Show("请输入新的请求间隔（秒）", "修改请求间隔", requestInterval.ToString());
                if (int.TryParse(input, out int newInterval) && newInterval > 0)
                {
                    requestInterval = newInterval;
                    timer.Interval = TimeSpan.FromSeconds(requestInterval);
                    ChangeRequestIntervalMenuItem.Header = $"请求间隔: {requestInterval}s";
                    Forms.MessageBox.Show($"请求间隔已更新为 {requestInterval} 秒。", "信息", Forms.MessageBoxButtons.OK, Forms.MessageBoxIcon.Information);
                }
                else
                {
                    Forms.MessageBox.Show("无效的输入，请输入一个大于0的整数。", "错误", Forms.MessageBoxButtons.OK, Forms.MessageBoxIcon.Error);
                }
            }
            catch (Exception ex)
            {
                OutputTextBox.Text = $"修改请求间隔时出错: {ex.Message}";
            }
        }

        private void ChangeNotificationPriority_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string input = InputBox.Show("请输入新的通知优先级（1-7）", "修改通知优先级", notificationPriority.ToString());
                if (int.TryParse(input, out int newPriority) && newPriority >= 1 && newPriority <= 7)
                {
                    notificationPriority = newPriority;
                    NotificationPriorityMenuItem.Header = $"通知优先级: {notificationPriority}";
                    Forms.MessageBox.Show($"通知优先级已更新为 {notificationPriority}。", "信息", Forms.MessageBoxButtons.OK, Forms.MessageBoxIcon.Information);
                }
                else
                {
                    Forms.MessageBox.Show("无效的输入，请输入1到7之间的整数。", "错误", Forms.MessageBoxButtons.OK, Forms.MessageBoxIcon.Error);
                }
            }
            catch (Exception ex)
            {
                OutputTextBox.Text = $"修改通知优先级时出错: {ex.Message}";
            }
        }

        private void Logout_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                this.Hide();
                ShowLoginWindow();
                this.Show();
            }
            catch (Exception ex)
            {
                OutputTextBox.Text = $"注销时出错: {ex.Message}";
            }
        }

        private void RequestButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                RequestData();
                PopulateMessageComboBox(); // 点击刷新按钮时重新读取txt文件
            }
            catch (Exception ex)
            {
                OutputTextBox.Text = $"点击刷新按钮时出错: {ex.Message}";
            }
        }

        private void PauseButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (isPaused)
                {
                    timer.Start();
                    PauseButtonMenuItem.Header = "暂停请求";
                    isPaused = false;
                }
                else
                {
                    timer.Stop();
                    PauseButtonMenuItem.Header = "恢复请求";
                    isPaused = true;
                }
            }
            catch (Exception ex)
            {
                OutputTextBox.Text = $"暂停/恢复按钮时出错: {ex.Message}";
            }
        }

        private void MessageComboBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            try
            {
                LoadSelectedMessageFile();
            }
            catch (Exception ex)
            {
                OutputTextBox.Text = $"选择消息文件时出错: {ex.Message}";
            }
        }

        private void ToggleSortOrder_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                isDescendingOrder = !isDescendingOrder;
                ToggleSortOrderButton.Header = $"消息排序: {(isDescendingOrder ? "降序" : "升序")}";
                PopulateMessageComboBox(); // 切换排序方式后重新读取消息文件
            }
            catch (Exception ex)
            {
                OutputTextBox.Text = $"切换排序方式时出错: {ex.Message}";
            }
        }

        private void Window_StateChanged(object sender, EventArgs e)
        {
            try
            {
                if (this.WindowState == WindowState.Minimized)
                {
                    this.Hide();
                    notifyIcon.Visible = true;
                }
            }
            catch (Exception ex)
            {
                OutputTextBox.Text = $"窗口状态改变时出错: {ex.Message}";
            }
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            notifyIcon.Dispose();
        }

        private void ShowMainWindow()
        {
            try
            {
                this.Show();
                this.WindowState = WindowState.Normal;
                this.Activate();
                notifyIcon.Visible = false;
            }
            catch (Exception ex)
            {
                OutputTextBox.Text = $"恢复窗口时出错: {ex.Message}";
            }
        }
    }
}
