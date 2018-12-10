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
        string thisVersion= Application.ResourceAssembly.GetName().Version.ToString();
        public UpdateWindow()
        {
            InitializeComponent();
            VersionNow.Text += thisVersion;
        }
        private void GetLatestVersion(object obj)
        {
            
        }
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            Thread t = new Thread(new ParameterizedThreadStart(GetLatestVersion));
            t.Start();
        }
        //已是最新版本
    }
}
