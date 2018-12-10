using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
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
        int[] thisVersion = new int[] { 1, 0, 0, 0 };
        int[] latestVersion = new int[] { 1, 0, 0, 0 };
        HttpWebRequest request;
        HttpWebResponse response;
        Stream responseStream;
        Stream stream;
        StreamReader reader;

        public UpdateWindow()
        {
            InitializeComponent();
            VersionNow.Text += Application.ResourceAssembly.GetName().Version.ToString();
            thisVersion[0] = Application.ResourceAssembly.GetName().Version.Major;
            thisVersion[1] = Application.ResourceAssembly.GetName().Version.Minor;
            thisVersion[2] = Application.ResourceAssembly.GetName().Version.Build;
            thisVersion[3] = Application.ResourceAssembly.GetName().Version.Revision;
        }
        private void GetLatestVersion(object obj)
        {
            request = WebRequest.Create("https://github.com/zou-z/FolderScanner/releases/download/1.0/Version.txt") as HttpWebRequest;
            try
            {
                response = request.GetResponse() as HttpWebResponse;
                responseStream = response.GetResponseStream();
                reader = new StreamReader(responseStream, Encoding.Default);
                for (int i = 0; i < 4; i++)
                    latestVersion[i] = int.Parse(reader.ReadLine());
                for (int i = 0; i < 4; i++)
                {
                    if (latestVersion[i] > thisVersion[i])
                    {
                        this.Dispatcher.BeginInvoke(new Action(delegate
                        {
                            NewVersionDetail.Text = reader.ReadToEnd();
                            VersionState.Text = "发现新版本: " + latestVersion[0].ToString() + "." + latestVersion[1].ToString() + "." + latestVersion[2].ToString() + "." + latestVersion[3].ToString();
                            NewVersionDetail.Visibility = NewVersionTitle.Visibility = Visibility.Visible;
                            UpdateBtn.Visibility = CancelBtn.Visibility = Visibility.Visible;
                        }));
                        return;
                    }
                }
                this.Dispatcher.BeginInvoke(new Action(delegate
                {
                    VersionState.Text = "已是最新版本";
                    ConfirmBtn.Visibility = Visibility.Visible;
                }));
            }
            catch (System.Net.WebException)
            {
                this.Dispatcher.BeginInvoke(new Action(delegate
                {
                    VersionState.Text = "连接服务器出错！建议\r\n1.重试\r\n2.在浏览器中打开: https://github.com/zou-z/FolderScanner/releases进行下载";
                }));
            }
        }
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            Thread t = new Thread(new ParameterizedThreadStart(GetLatestVersion));
            t.Start();
        }
        private void Download(object obj)
        {
            string v = latestVersion[0].ToString() + "." + latestVersion[1].ToString() + "." + latestVersion[2].ToString() + "." + latestVersion[3].ToString();
            request = WebRequest.Create("https://github.com/zou-z/FolderScanner/releases/download/" + v + "/FolderScanner.exe") as HttpWebRequest;
            response = request.GetResponse() as HttpWebResponse;
            responseStream = response.GetResponseStream();
            stream = new FileStream(Directory.GetCurrentDirectory() + "\\FolderScanner_v" + v + ".exe", FileMode.Create);
            byte[] bArr = new byte[1024];
            int size = responseStream.Read(bArr, 0, (int)bArr.Length);
            long sizeNow = size;
            long sizeTotal = response.ContentLength;
            while (size > 0)
            {
                this.Dispatcher.BeginInvoke(new Action(delegate
                {
                    progressBar.Value = 100.0 * sizeNow / sizeTotal;
                    ratioTextBlock.Text = Math.Round(100.0 * sizeNow / sizeTotal, 2).ToString()+"%";
                }));
                stream.Write(bArr, 0, size);
                size = responseStream.Read(bArr, 0, (int)bArr.Length);
                sizeNow += size;
            }
            this.Dispatcher.BeginInvoke(new Action(delegate
            {
                progressBar.Value = 100;
                ratioTextBlock.Text = "100%";
                DownloadState.Text = "完成";
            }));
            stream.Close();
            responseStream.Close();
        }
        private void UpdateBtn_Click(object sender, RoutedEventArgs e)
        {
            MainGrid.RowDefinitions[4].Height = new GridLength(30);
            UpdateBtn.Visibility = CancelBtn.Visibility = Visibility.Hidden;
            Thread t = new Thread(new ParameterizedThreadStart(Download));
            t.Start();
        }
        private void ConfirmBtn_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
        private void CancelBtn_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
        private void Window_Closed(object sender, EventArgs e)
        {
            if (response != null)
                response.Close();
            if (responseStream != null)
                responseStream.Close();
            if (stream != null)
                stream.Close();
            if (reader != null)
                reader.Close();
        }
    }
}
