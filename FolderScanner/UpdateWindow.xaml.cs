using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace FolderScanner
{
    /// <summary>
    /// UpdateWindow.xaml 的交互逻辑
    /// </summary>
    public partial class UpdateWindow : Window
    {
        UpdateVersion update = new UpdateVersion("FolderScanner", "128.1.174.252", 8800);

        public UpdateWindow()
        {
            InitializeComponent();
        }
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // 获取当前版本信息
            VersionNow.Text += Application.ResourceAssembly.GetName().Version.ToString();
            update.GetNowVersion(Application.ResourceAssembly.GetName().Version.Major,
                                 Application.ResourceAssembly.GetName().Version.Minor,
                                 Application.ResourceAssembly.GetName().Version.Build,
                                 Application.ResourceAssembly.GetName().Version.Revision);
            // 获取最新的版本信息
            update.InitControls(VersionState, NewVersionUpdateTime, NewVersionDetail, UpdateBtn, DownloadGrid, progressBar, DownloadedSize);
            Thread t = new Thread(new ThreadStart(update.Start));
            t.Start();
        }
        private void UpdateBtn_Click(object sender, RoutedEventArgs e)
        {
            if (UpdateBtn.Content.ToString() == "完成")
            {
                if (File.Exists(update.full_name + ".exe"))
                {
                    Application.Current.Shutdown();
                    Process.Start(update.full_name + ".exe");
                }
                else
                {
                    MessageBox.Show("额，刚刚下载的软件的新版本貌似不见了!");
                    this.Close();
                }
            }
            else
            {
                UpdateBtn.Visibility = Visibility.Hidden;
                DownloadGrid.Visibility = Visibility.Visible;
                Thread t = new Thread(new ThreadStart(update.DownloadNewVersion));
                t.Start();
            }
        }
    }
    public class UpdateVersion
    {
        private readonly IPEndPoint ipe;
        private readonly string name;
        public string full_name;
        private readonly int[] NowV = new int[4];

        public int[] LatestV = new int[4];
        public string update_time;
        public string content;
        public long file_size;

        private TextBlock VState;
        private TextBlock VTime;
        private TextBlock VDetail;
        private Button VUpdate;
        private ProgressBar VBar;
        private Grid Vgrid;
        private TextBlock VRece;

        public UpdateVersion(string name, string host, int ip)
        {
            ipe = new IPEndPoint(IPAddress.Parse(host), ip);
            this.name = name;
        }
        public void GetNowVersion(int major,int minor,int build,int revision)
        {
            NowV[0] = major; NowV[1] = minor;
            NowV[2] = build; NowV[3] = revision;
        }
        public void InitControls(TextBlock State, TextBlock Time, TextBlock Detail, Button Update, Grid grid, ProgressBar Bar, TextBlock Rece)
        {
            VState = State;
            VTime = Time;
            VDetail = Detail;
            VUpdate = Update;
            VBar = Bar;
            Vgrid = grid;
            VRece = Rece;
        }
        public void Start()
        {
            try
            {
                GetLatestVersionInfo();
            }
            catch (System.Net.Sockets.SocketException)
            {
                VState.Dispatcher.BeginInvoke(new Action(delegate
                {
                    VState.Text = "连接服务器失败!\r\n可进入以下地址更新:\r\n" +
                    "www.xunqe.com\r\n或 github.com/zou-z/FolderScanner/releases";
                }));
                return;
            }
            for (int i = 0; i < 4; i++)
            {
                if (LatestV[i] > NowV[i])
                {
                    VUpdate.Dispatcher.BeginInvoke(new Action(delegate
                    {
                        VState.Text = "发现新版本: " + LatestV[0] + "." + LatestV[1] + "." + LatestV[2] + "." + LatestV[3];
                        VTime.Text = "发布时间: " + update_time;
                        VDetail.Text = "\r\n新版本特性:\r\n" + content;
                        VUpdate.Content = "下载新版本 (" + FormatUnit(file_size) + ")";
                        VUpdate.Visibility = VTime.Visibility = VDetail.Visibility = Visibility.Visible;
                    }));
                    return;
                }
                else if (LatestV[i] < NowV[i])
                {
                    break;
                }
            }
            VState.Dispatcher.BeginInvoke(new Action(delegate
            {
                VState.Text = "已是最新版本!";
            }));
        }
        private void GetLatestVersionInfo()
        {
            Socket clientSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            clientSocket.Connect(ipe);
            clientSocket.Send(Encoding.UTF8.GetBytes("GetLatestVersion " + this.name));
            byte[] result = new byte[1024];
            int bytes = clientSocket.Receive(result);
            string res = Encoding.UTF8.GetString(result, 0, bytes);
            clientSocket.Close();
            ParseParameter(res);
        }
        private void ParseParameter(string res)
        {
            string[] para = res.Split(',');

            for (int i = 0; i < 4; i++)
                LatestV[i] = int.Parse(para[i]);

            DateTime dtStart = TimeZone.CurrentTimeZone.ToLocalTime(new DateTime(1970, 1, 1));
            DateTime targetDt = dtStart.AddSeconds(long.Parse(para[4]));
            update_time = targetDt.ToString();

            content = para[5].Trim('\'').Replace("|||", "\r\n");

            file_size = long.Parse(para[6]);
        }
        private string FormatUnit(long size)
        {
            string[] units = new string[] { "B", "KB", "MB", "GB" };
            int p = 0;
            double temp_size = size;
            while (temp_size >= 1024)
            {
                temp_size /= 1024;
                p++;
            }
            return Math.Round(temp_size, 2).ToString() + " " + units[p];
        }
        public void DownloadNewVersion()
        {
            this.full_name = this.name + "_v" + LatestV[0] + "." + LatestV[1] + "." + LatestV[2] + "." + LatestV[3];
            string path = this.name + "/" + full_name + "/" + full_name + ".exe";
            byte[] result = new byte[1024 * 2];

            Socket clientSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            clientSocket.Connect(ipe);
            clientSocket.Send(Encoding.UTF8.GetBytes("Download " + path));

            FileStream fs = new FileStream(full_name + ".exe", FileMode.Create);
            long rece_size = 0;
            int length;
            while (rece_size < this.file_size)
            {
                length=clientSocket.Receive(result);
                fs.Write(result, 0, length);
                rece_size += length;

                VBar.Dispatcher.BeginInvoke(new Action(delegate
                {
                    VBar.Value = rece_size * 100.0 / file_size;
                    VRece.Text = FormatUnit(rece_size);
                }));
            }
            clientSocket.Close();
            fs.Close();

            Vgrid.Dispatcher.BeginInvoke(new Action(delegate
            {
                Vgrid.Visibility = Visibility.Collapsed;
                VUpdate.Content = "完成";
                VUpdate.Visibility = Visibility.Visible;
            }));
        }
    }
}
