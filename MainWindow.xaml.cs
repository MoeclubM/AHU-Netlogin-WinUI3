using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Linq;
using System.Net.Http;
using System.Net.NetworkInformation;
using Windows.Graphics;
using Windows.Storage;
using Microsoft.UI.Windowing;

namespace DrcomLoginApp
{

    public sealed partial class MainWindow : Window
    {
        private readonly string loginUrlTemplate = "http://172.16.253.3:801/eportal/?c=Portal&a=login&callback=dr1003&login_method=1&user_account={0}&user_password={1}&wlan_user_ip={2}&wlan_user_ipv6=&wlan_user_mac={3}&wlan_ac_ip=172.16.253.1&wlan_ac_name={4}&jsVersion=3.3.2&v=4946";
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
            // 自定义标题栏
            ExtendsContentIntoTitleBar = true;
            SetTitleBar(null); // 隐藏默认的标题栏
            // 设置窗口的宽度和高度
            appWindow.Resize(new SizeInt32(360, 550));
            //设置icon
            appWindow.SetIcon("Assets/logo.ico");
            //显示网卡地址类型
            IpAddressTextBlock.Text = $"IP 地址: {GetNetworkDetails().IpAddress}";
            InterfaceTypeTextBlock.Text = $"网卡类型: {GetNetworkDetails().InterfaceType}";
            // 加载校区信息
            string savedCampus = LoadCampus();
            if (!string.IsNullOrEmpty(savedCampus))
            {
                foreach (ComboBoxItem item in campus.Items)
                {
                    if (item.Tag as string == savedCampus)
                    {
                        campus.SelectedItem = item;
                        break;
                    }
                }
            }
        }
        private AppWindow GetAppWindowForCurrentWindow()
        {
            var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hWnd);
            return AppWindow.GetFromWindowId(windowId);
        }

        private void PasswordBox_KeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
        {
            // 检查按下的是否是 Enter 键
            if (e.Key == Windows.System.VirtualKey.Enter)
            {
                // 调用登录方法
                login();
            }
        }

        private async void LoginButton_Click(object sender, RoutedEventArgs e)
        {
            login();
        }
        private async void login()
        {
            string account = AccountTextBox.Text.Trim();
            string password = PasswordBox.Password.Trim();
            string ip = GetNetworkDetails().IpAddress;
            string mac = GetNetworkDetails().MacAddress;
            string InterfaceType = GetNetworkDetails().InterfaceType;
            string url = "";
            string acName = (campus.SelectedItem as ComboBoxItem)?.Tag as string ?? "";
            if (string.IsNullOrEmpty(account) || string.IsNullOrEmpty(password))
            {
                StatusTextBlock.Text = "请输入账号和密码！";
                return;
            }
            if (InterfaceType != "none")
            {
                if (InterfaceType == "Ethernet")
                {
                    url = string.Format(loginUrlTemplate, Uri.EscapeDataString(account), Uri.EscapeDataString(password), ip, mac, "");
                }
                if (InterfaceType == "Wireless")
                {
                    url = string.Format(loginUrlTemplate, Uri.EscapeDataString(account), Uri.EscapeDataString(password), ip, mac, acName);
                }
            }
            else
            {
                StatusTextBlock.Text = "未正确识别您的网络设备，请尝试重新启动本应用";
                return;
            }

            try
            {
                using HttpClient client = new();
                string response = await client.GetStringAsync(url);

                if (response.Contains("\"result\":\"1\""))
                {
                    StatusTextBlock.Text = "登录成功！";
                    if (RememberMeCheckBox.IsChecked == true)
                    {
                        SaveCampus(acName);
                        SaveCredentials(account, password);
                    }
                }
                else if (response.Contains("\"ret_code\":2"))
                {
                    if (RememberMeCheckBox.IsChecked == true)
                    {
                        SaveCampus(acName);
                        SaveCredentials(account, password);
                    }
                    StatusTextBlock.Text = "当前设备已在线！无需重复登录";
                }
                else
                {
                    // 解析错误内容
                    string errorMessage = response;
                    StatusTextBlock.Text = $"登录失败！请求地址：{url} 错误信息: {errorMessage}";
                    if (RememberMeCheckBox.IsChecked == true)
                    {
                        SaveCampus(acName);
                        SaveCredentials(account, password);
                    }
                }
            }
            catch (Exception ex)
            {
                if (RememberMeCheckBox.IsChecked == true)
                {
                    SaveCampus(acName);
                    SaveCredentials(account, password);
                }
                StatusTextBlock.Text = $"请求失败: {ex.Message}";
            }
        }
        private async void LogoutButton_Click(object sender, RoutedEventArgs e)
        {
            string ip = GetNetworkDetails().IpAddress;
            string mac = GetNetworkDetails().MacAddress;
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
        private (string IpAddress, string MacAddress, string InterfaceType) GetNetworkDetails()
        {
            var networkInterfaces = NetworkInterface.GetAllNetworkInterfaces()
                .Where(n => n.OperationalStatus == OperationalStatus.Up
                            && (n.NetworkInterfaceType == NetworkInterfaceType.Ethernet || n.NetworkInterfaceType == NetworkInterfaceType.Wireless80211) // 包括以太网和无线网卡
                            && !n.Description.ToLower().Contains("virtual") // 排除虚拟网卡
                            && !n.Description.ToLower().Contains("vmware") // 排除 VMware 网卡
                            && !n.Description.ToLower().Contains("hyper-v")); // 排除 Hyper-V 网卡

            foreach (var netInterface in networkInterfaces)
            {
                var ipProps = netInterface.GetIPProperties();
                var ipv4Address = ipProps.UnicastAddresses
                    .FirstOrDefault(ip => ip.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork);

                if (ipv4Address != null)
                {
                    // 获取实际的 MAC 地址并去掉冒号
                    string macAddress = string.Join("", netInterface.GetPhysicalAddress()
                        .GetAddressBytes()
                        .Select(b => b.ToString("X2")));

                    // 获取网卡类型（有线或无线）
                    string interfaceType = netInterface.NetworkInterfaceType switch
                    {
                        NetworkInterfaceType.Ethernet => "Ethernet",
                        NetworkInterfaceType.Wireless80211 => "Wireless",
                        _ => "Other"
                    };

                    return (ipv4Address.Address.ToString(), macAddress, interfaceType);
                }
            }

            // 如果没有找到有效的 IP 地址和 MAC 地址，返回默认值
            return ("0.0.0.0", "000000000000", "none");
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

        private void SaveCampus(string campusTag)
        {
            ApplicationDataContainer localSettings = ApplicationData.Current.LocalSettings;
            localSettings.Values["Campus"] = campusTag;
        }

        private string LoadCampus()
        {
            ApplicationDataContainer localSettings = ApplicationData.Current.LocalSettings;

            if (localSettings.Values.TryGetValue("Campus", out object campusObj))
            {
                return campusObj as string ?? "";
            }
            return "";
        }


    }
}
