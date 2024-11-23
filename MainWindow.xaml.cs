using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Text;
using System.Text.Json;
using Windows.Graphics;
using Windows.Storage;
using Microsoft.UI.Windowing;

namespace DrcomLoginApp
{
    public sealed partial class MainWindow : Window
    {
        private readonly string loginUrlTemplate = "http://172.16.253.3:801/eportal/?c=Portal&a=login&callback=dr1003&login_method=1&user_account={0}&user_password={1}&wlan_user_ip={2}&wlan_user_ipv6=&wlan_user_mac=000000000000&wlan_ac_ip=172.16.253.1&wlan_ac_name=&jsVersion=3.3.2&v=4946";
        private readonly string logoutUrlTemplate = "http://172.16.253.3:801/eportal/?c=Portal&a=logout&callback=dr1004&login_method=1&user_account=drcom&user_password=123&ac_logout=0&register_mode=1&wlan_user_ip={0}&wlan_user_ipv6=&wlan_vlan_id=0&wlan_user_mac=000000000000&wlan_ac_ip=172.16.253.1&wlan_ac_name=&jsVersion=3.3.2&v=3484";

        public MainWindow()
        {
            this.InitializeComponent();
            if(LoadSavedCredentials())
            {
                login();
            }
            // 获取 AppWindow 对象
            var appWindow = GetAppWindowForCurrentWindow();
            // 设置窗口的宽度和高度
            appWindow.Resize(new SizeInt32(300, 400));
            string ipAddress = GetLocalIPAddress();
            IpAddressTextBlock.Text = $"当前 IP 地址: {ipAddress}";
            //显示网卡地址
            
        }
        private AppWindow GetAppWindowForCurrentWindow()
        {
            var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hWnd);
            return AppWindow.GetFromWindowId(windowId);
        }
        private async void LoginButton_Click(object sender, RoutedEventArgs e)
        {
            login();
        }
        private async void login()
        {
            string account = AccountTextBox.Text.Trim();
            string password = PasswordBox.Password.Trim();
            string ip = GetLocalIPAddress();

            if (string.IsNullOrEmpty(account) || string.IsNullOrEmpty(password))
            {
                StatusTextBlock.Text = "请输入账号和密码！";
                return;
            }

            string url = string.Format(loginUrlTemplate, Uri.EscapeDataString(account), Uri.EscapeDataString(password), ip);

            try
            {
                using HttpClient client = new();
                string response = await client.GetStringAsync(url);

                if (response.Contains("\"result\":\"1\""))
                {
                    StatusTextBlock.Text = "登录成功！";
                    if (RememberMeCheckBox.IsChecked == true)
                    {
                        SaveCredentials(account, password);
                    }
                }
                else if (response.Contains("\"ret_code\":2"))
                {
                    StatusTextBlock.Text = "已在线！";
                }
                else
                {
                    // 解析错误内容
                    string errorMessage = response;
                    StatusTextBlock.Text = $"登录失败！请求地址：{url} 错误信息: {errorMessage}";
                }
            }
            catch (Exception ex)
            {
                StatusTextBlock.Text = $"请求失败: {ex.Message}";
            }
        }
        private async void LogoutButton_Click(object sender, RoutedEventArgs e)
        {
            string ip = GetLocalIPAddress();
            string url = string.Format(logoutUrlTemplate, ip);

            try
            {
                using HttpClient client = new();
                string response = await client.GetStringAsync(url);

                // 提取 JSON 数据部分，去掉 callback 包装
                string json = response.Substring(response.IndexOf('(') + 1);
                json = json.Substring(0, json.LastIndexOf(')'));

                // 解析 JSON
                var logoutResult = System.Text.Json.JsonDocument.Parse(json).RootElement;

                string result = logoutResult.GetProperty("result").GetString();
                string message = logoutResult.GetProperty("msg").GetString();

                // 转换 Unicode 消息为正常文本
                message = System.Text.RegularExpressions.Regex.Unescape(message);

                if (result == "1")
                {
                    // 登出成功
                    StatusTextBlock.Text = "登出成功！";
                }
                else
                {
                    // 登出失败，显示具体错误信息
                    StatusTextBlock.Text = $"登出失败！错误信息: {message}";
                }
            }
            catch (Exception ex)
            {
                // 捕获并处理异常
                StatusTextBlock.Text = $"请求失败！错误信息: {ex.Message}";
            }
        }



        private string GetLocalIPAddress()
        {
            var networkInterfaces = NetworkInterface.GetAllNetworkInterfaces()
                .Where(n => n.OperationalStatus == OperationalStatus.Up
                            && n.NetworkInterfaceType == NetworkInterfaceType.Ethernet  // 只选择以太网网卡
                            && !n.Description.ToLower().Contains("virtual")  // 排除虚拟网卡
                            && !n.Description.ToLower().Contains("vmware")  // 排除 VMware 网卡
                            && !n.Description.ToLower().Contains("hyper-v"));  // 排除 Hyper-V 网卡

            foreach (var netInterface in networkInterfaces)
            {
                var ipProps = netInterface.GetIPProperties();
                var ipv4Address = ipProps.UnicastAddresses
                    .FirstOrDefault(ip => ip.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork);

                if (ipv4Address != null)
                {
                    return ipv4Address.Address.ToString();
                }
            }

            // 如果没有找到有效的以太网 IP 地址，返回 "0.0.0.0"
            return "0.0.0.0";
        }




        private void SaveCredentials(string account, string password)
        {
            ApplicationDataContainer localSettings = ApplicationData.Current.LocalSettings;
            localSettings.Values["Account"] = account;
            localSettings.Values["Password"] = password;
        }

        private bool LoadSavedCredentials()
        {
            ApplicationDataContainer localSettings = ApplicationData.Current.LocalSettings;

            if (localSettings.Values.TryGetValue("Account", out object accountObj) &&
                localSettings.Values.TryGetValue("Password", out object passwordObj))
            {
                AccountTextBox.Text = accountObj as string ?? "";
                PasswordBox.Password = passwordObj as string ?? "";
                RememberMeCheckBox.IsChecked = true;
                return true;
            }
            return false;
        }
    }
}
