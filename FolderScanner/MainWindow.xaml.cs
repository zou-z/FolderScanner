﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Permissions;
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
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace FolderScanner
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        private List<FolderTree> Folder_Tree = new List<FolderTree>();
        private List<FolderInfo> Folder_Info = new List<FolderInfo>();
        private Stack<FolderTree> FrontNode = new Stack<FolderTree>();
        private FolderTree SelectedNode;
        private string ListViewSortState = "Null";
        private int SearchPos = 0;

        public MainWindow()
        {
            InitializeComponent();
            InitPanel.Visibility = Visibility.Visible;
            ScanInfoPanel.Visibility = ChildItemsPanel.Visibility = FileDetailPanel.Visibility = Visibility.Hidden;
        }
        private void ScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            var eventArg = new MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta);
            eventArg.RoutedEvent = UIElement.MouseWheelEvent;
            eventArg.Source = sender;
            (sender as ScrollViewer).RaiseEvent(eventArg);
        }
        /// <summary>
        /// 上面的控件(3个Button和2个Popup和1个TextBox)
        /// </summary>
        private void BackwardButton_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedNode == null || SelectedNode.Parent == null)
                return;
            FrontNode.Push(SelectedNode);
            SelectedNode = SelectedNode.Parent;
            DisplayNode();
        }
        private void ForwardButton_Click(object sender, RoutedEventArgs e)
        {
            if (FrontNode.Count == 0)
                return;
            SelectedNode = FrontNode.Pop();
            DisplayNode();
        }
        private void RestartButton_Click(object sender, RoutedEventArgs e)
        {
            AddScanPopup.IsOpen = !AddScanPopup.IsOpen;
        }
        private void ThePathNow_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (thePathNow.Text == "")
                SuggestionPopup.IsOpen = false;
            else if (SelectedNode != null && thePathNow.Text == SelectedNode.FullPath) //屏蔽刚扫描完和点击树节点时
                SuggestionPopup.IsOpen = false;
            else
            {
                List<SuggestItems> SuggestItemsList = new List<SuggestItems>();
                string FrontPath = "";
                string BackName = "";
                for (int p = thePathNow.Text.Length - 1; p > 0; p--)
                {
                    if (thePathNow.Text[p] == '\\')
                    {
                        FrontPath = thePathNow.Text.Substring(0, p);
                        BackName = thePathNow.Text.Substring(p + 1);
                        break;
                    }
                }
                if (FrontPath != "")
                {
                    DirectoryInfo dire = new DirectoryInfo(FrontPath);
                    foreach (DirectoryInfo folder in dire.GetDirectories())
                    {
                        int i;
                        for (i = 0; i < BackName.Length && i < folder.Name.Length; i++) { if (BackName[i] != folder.Name[i]) break; }
                        if (i < BackName.Length) continue;
                        SuggestItems item = new SuggestItems(folder.FullName);
                        SuggestItemsList.Add(item);
                    }
                    SuggestItemsListBox.ItemsSource = SuggestItemsList;
                }
                SuggestionPopup.IsOpen = (SuggestItemsList.Count > 0) ? true : false;
            }
        }
        private void SuggestItemsListBox_PreviewMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (SuggestItemsListBox.SelectedItem is SuggestItems Selected)
            {
                thePathNow.Text = Selected.FullPath;
                SuggestionPopup.IsOpen = false;
            }
        }
        private void SuggestItemsListBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Down)
            {
                SuggestItemsListBox.SelectedIndex = (SuggestItemsListBox.SelectedIndex < SuggestItemsListBox.Items.Count - 1) ? (SuggestItemsListBox.SelectedIndex + 1) : SuggestItemsListBox.SelectedIndex;
                SuggestItemsListBox.ScrollIntoView(SuggestItemsListBox.SelectedItem);
            }
            else if (e.Key == Key.Up)
            {
                SuggestItemsListBox.SelectedIndex = (SuggestItemsListBox.SelectedIndex > -1) ? (SuggestItemsListBox.SelectedIndex - 1) : SuggestItemsListBox.SelectedIndex;
                SuggestItemsListBox.ScrollIntoView(SuggestItemsListBox.SelectedItem);
            }
            else if (e.Key == Key.Escape)
                SuggestItemsListBox.SelectedIndex = -1;
            else if (e.Key == Key.Enter)
            {
                if (SuggestItemsListBox.SelectedItem is SuggestItems Selected)
                {
                    thePathNow.Text = Selected.FullPath;
                    SuggestionPopup.IsOpen = false;
                    thePathNow.SelectionStart = thePathNow.Text.Length;
                    thePathNow.Focus();
                }
            }
            if (SuggestItemsListBox.SelectedIndex == -1)
            {
                thePathNow.Focus();
            }
        }
        private void ThePathNow_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (SuggestionPopup.IsOpen && e.Key == Key.Down)
            {
                SuggestItemsListBox.SelectedIndex = 0;
                SuggestItemsListBox.Focus();
            }
            if (e.Key == Key.Enter)
            {
                SuggestionPopup.IsOpen = false;
                Search();
            }
        }
        private void SearchPath_Click(object sender, RoutedEventArgs e)
        {
            Search();
        }
        private void Search()
        {
            if (Folder_Tree.Count == 0)
                MessageBox.Show("未找到匹配项!\r\n记录为空,请先扫描");
            else
            {
                SearchPos %= Folder_Tree.Count;
                for (; SearchPos < Folder_Tree.Count; SearchPos++)
                {
                    FolderTree TargetTree = Folder_Tree[SearchPos].Find(thePathNow.Text);
                    if (TargetTree != null)
                    {
                        foreach (FolderTree tempRoot in Folder_Tree)
                            tempRoot.IsExpanded = false;
                        if (SelectedNode != null)
                            SelectedNode.IsSelected = false;
                        SelectedNode = TargetTree;
                        SelectedNode.IsSelected = true;
                        do
                        {
                            TargetTree.IsExpanded = true;
                            TargetTree = TargetTree.Parent;
                        } while (TargetTree != null);
                        treeView.Items.Refresh();
                        SearchPos++;
                        return;
                    }
                }
                SearchPos = 0;
            }
        }
        private void RescanPath_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                AddScanStart_Click(sender, e);
            }
        }
        private void ClearSearchTextBox(object sender, RoutedEventArgs e)
        {
            thePathNow.Text = "";
        }
        private void ClearRescanTextBox(object sender, RoutedEventArgs e)
        {
            AddScanPath.Text = "";
        }
        private void AddScanStart_Click(object sender, RoutedEventArgs e)
        {
            if (!Directory.Exists(AddScanPath.Text))
            {
                AddScanTip.Text = "输入的路径不存在! 请重新输入:";
                return;
            }
            AddScanTip.Text = "请输入路径:";
            FolderTree foldertree = SelectedNode = new FolderTree(AddScanPath.Text, AddScanPath.Text);
            Folder_Tree.Add(foldertree);
            Folder_Info.Clear();
            Kernel kernel = new Kernel(Folder_Tree, Folder_Info, treeView, listView, progressBar, TipText, ItemDetail, IgnoredFolder);

            ChoosePanel(ScanInfoPanel);
            FrontNode.Clear();
            thePathNow.Text = AddScanPath.Text;
            AddScanPopup.IsOpen = false;

            Thread t = new Thread(new ParameterizedThreadStart(kernel.Start));
            t.Start(foldertree);
        }
        private void AddScanCancel_Click(object sender, RoutedEventArgs e)
        {
            AddScanPopup.IsOpen = false;
        }

        /// <summary>
        /// 中间左边的TreeView
        /// </summary>
        private void TreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            SelectedNode = (FolderTree)treeView.SelectedItem;
            if (SelectedNode == null)
            {
                listView.ItemsSource = null;
                listView.Items.Clear();
                thePathNow.Text = "";
                ItemDetail.Text = "0 个项目";
                ChoosePanel(InitPanel);
                return;
            }
            else if (!(Directory.Exists(SelectedNode.FullPath)))
            {
                MessageBox.Show("该文件夹不存在:\r\n" + SelectedNode.FullPath);
                return;
            }
            DisplayNode();
        }
        private void DisplayNode()
        {
            Folder_Info.Clear();
            int direNum = 0, fileNum = 0;
            for (int i = 0; i < SelectedNode.Children.Count; i++)
            {
                DirectoryInfo thisFolder = new DirectoryInfo(SelectedNode.FullPath);
                FolderInfo newFolder = new FolderInfo(SelectedNode.Children[i].Name, SelectedNode.Children[i].Size, thisFolder.CreationTime.ToString(), "/Resource/folder.png");
                newFolder.CalcRatio(SelectedNode.Size);
                Folder_Info.Add(newFolder);
                direNum++;
            }
            DirectoryInfo Dire = new DirectoryInfo(SelectedNode.FullPath);
            foreach (FileInfo file in Dire.GetFiles())
            {
                FolderInfo newFile = new FolderInfo(file.Name, file.Length, file.CreationTime.ToString(), "/Resource/file.png");
                newFile.CalcRatio(SelectedNode.Size);
                Folder_Info.Add(newFile);
                fileNum++;
            }
            thePathNow.Text = SelectedNode.FullPath;
            thePathNow.SelectionStart = thePathNow.Text.Length;
            ItemDetail.Text = (direNum + fileNum).ToString() + " 个项目  " + direNum + " 个文件夹  " + fileNum + " 个文件";
            ListViewSortBy(ListViewSortState);
        }
        private void RefreshThisTree_Click(object sender, RoutedEventArgs e)
        {
            if (!(treeView.SelectedItem is FolderTree thisTree))
            {
                MessageBox.Show("请先选择一个需要刷新的树形结构");
                return;
            }
            while (thisTree.Parent != null)
                thisTree = thisTree.Parent;
            thisTree.Children.Clear();
            SelectedNode = thisTree;
            Folder_Info.Clear();

            Kernel kernel = new Kernel(Folder_Tree, Folder_Info, treeView, listView, progressBar, TipText, ItemDetail, IgnoredFolder);

            ChoosePanel(ScanInfoPanel);
            FrontNode.Clear();
            thePathNow.Text = thisTree.FullPath;

            Thread t = new Thread(new ParameterizedThreadStart(kernel.Start));
            t.Start(thisTree);
        }
        private void DelThisTree_Click(object sender, RoutedEventArgs e)
        {
            if (!(treeView.SelectedItem is FolderTree thisTree))
            {
                MessageBox.Show("请先选择一个需要删除的树形结构!");
                return;
            }
            while (thisTree.Parent != null)
                thisTree = thisTree.Parent;
            MessageBoxResult result = MessageBox.Show("是否确认删除根节点为:\r\n" + thisTree.Name + "\r\n的树形结构?", "删除树形结构", MessageBoxButton.OKCancel, MessageBoxImage.Asterisk);
            if (result == MessageBoxResult.OK)
            {
                Folder_Tree.Remove(thisTree);
                treeView.Items.Refresh();
                FrontNode.Clear();
            }
        }
        private void OpenNodePath_Click(object sender, RoutedEventArgs e)
        {
            if (!(treeView.SelectedItem is FolderTree thisTree))
            {
                MessageBox.Show("请先选择一个项目");
                return;
            }
            else if (!Directory.Exists(thisTree.FullPath))
            {
                MessageBox.Show("不存在该路径:\r\n" + thisTree.FullPath);
                return;
            }
            Process process = new Process();
            process.StartInfo.FileName = "explorer.exe";
            process.StartInfo.Arguments = thisTree.FullPath;
            process.Start();
        }

        /// <summary>
        /// 中间的ListView
        /// </summary>
        private void ListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!(listView.SelectedItem is FolderInfo thisItem))
            {
                SelectedItemNum.Text = "已选择 0 个项目";
                return;
            }
            SelectedItemNum.Text = "已选择 " + listView.SelectedItems.Count.ToString() + " 个项目";
            if (Directory.Exists(SelectedNode.FullPath + "\\" + thisItem.Name))
            {
                List<ChildItems> ChildItemsList = new List<ChildItems>();
                List<FolderTree> childFolder = new List<FolderTree>();
                for (int i = 0; i < SelectedNode.Children.Count; i++)
                {
                    if (SelectedNode.Children[i].Name == thisItem.Name)
                    {
                        childFolder = SelectedNode.Children[i].Children;
                        break;
                    }
                }
                for (int i = 0; i < childFolder.Count; i++)
                {
                    ChildItems newItem = new ChildItems("/Resource/folder.png", childFolder[i].Name, childFolder[i].Size);
                    ChildItemsList.Add(newItem);
                }
                DirectoryInfo Dire = new DirectoryInfo(SelectedNode.FullPath + "\\" + thisItem.Name);
                foreach (FileInfo file in Dire.GetFiles())
                {
                    ChildItems newItem = new ChildItems("/Resource/file.png", file.Name, file.Length);
                    ChildItemsList.Add(newItem);
                }
                listBox.ItemsSource = ChildItemsList;
                listBox.Items.Refresh();
                ChoosePanel(ChildItemsPanel);
            }
            else
            {
                string[] PicExtension = new string[] { "jpg", "jpeg", "png", "bmp", "gif", "ico" };
                int p = thisItem.Name.Length - 1;
                for (; p > 0; p--) { if (thisItem.Name[p] == '.') break; }
                if (p > 0)
                {
                    if (PicExtension.Contains(thisItem.Name.Substring(p + 1)))
                    {
                        BitmapImage image = new BitmapImage();
                        using (FileStream fs = File.OpenRead(SelectedNode.FullPath + "\\" + thisItem.Name))
                        {
                            image.BeginInit();
                            image.CacheOption = BitmapCacheOption.OnLoad; //图像缓存到内存中，不会占用文件，没有被引用时会被自动回收。
                            image.StreamSource = fs;
                            image.EndInit();
                        }
                        FileDetailImg.Source = image;
                    }
                    else
                        FileDetailImg.Source = new BitmapImage(new Uri("pack://application:,,,/Resource/filetype_file.png"));
                }
                else
                    FileDetailImg.Source = new BitmapImage(new Uri("pack://application:,,,/Resource/filetype_file.png"));
                FileDetailName.Text = "名称: " + thisItem.Name;
                FileDetailSize.Text = "大小: " + thisItem.DisplaySize + " (" + thisItem.Size + " B)";
                FileDetailPath.Text = "位置: " + SelectedNode.FullPath + "\\" + thisItem.Name;
                FileDetailCreationTime.Text = "创建时间: " + thisItem.CreationTime;
                ChoosePanel(FileDetailPanel);
            }
        }
        private void ListView_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (!(listView.SelectedItem is FolderInfo thisItem))
            {
                SelectedItemNum.Text = "已选择 0 个项目";
                return;
            }
            SelectedItemNum.Text = "已选择 " + listView.SelectedItems.Count.ToString() + " 个项目";
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                if (Directory.Exists(SelectedNode.FullPath + "\\" + thisItem.Name))
                {
                    for (int i = 0; i < SelectedNode.Children.Count; i++)
                    {
                        if (SelectedNode.Children[i].Name == thisItem.Name)
                        {
                            SelectedNode = SelectedNode.Children[i];
                            break;
                        }
                    }
                    DisplayNode();
                }
                else if (File.Exists(SelectedNode.FullPath + "\\" + thisItem.Name))
                    Open_File(SelectedNode.FullPath + "\\" + thisItem.Name);
                else
                    MessageBox.Show("不存在该文件夹:\r\n" + SelectedNode.FullPath + "\\" + thisItem.Name);
            }
        }
        private void ListViewColumnHeader_Click(object sender, RoutedEventArgs e)
        {
            GridViewColumn clickedColumn = (e.OriginalSource as GridViewColumnHeader).Column;
            Grid clickedGrid = clickedColumn.Header as Grid;
            if (clickedGrid.Name == "ColumnNameGrid")
                ListViewSortState = (ListViewSortState == "NameUp") ? "NameDown" : "NameUp";
            else if (clickedGrid.Name == "ColumnSizeGrid")
                ListViewSortState = (ListViewSortState == "SizeDown") ? "SizeUp" : "SizeDown";
            else if (clickedGrid.Name == "ColumnRatioGrid")
                ListViewSortState = (ListViewSortState == "RatioDown") ? "RatioUp" : "RatioDown";
            else if (clickedGrid.Name == "ColumnCreationTimeGrid")
                ListViewSortState = (ListViewSortState == "CreationTimeUp") ? "CreationTimeDown" : "CreationTimeUp";
            ListViewSortBy(ListViewSortState);
        }
        private void ListViewSortBy(string way)
        {
            ColumnName.Text = ColumnSize.Text = ColumnRatio.Text = ColumnCreationTime.Text = "";
            switch (way)
            {
                case "NameDown":
                    Folder_Info.Sort((a, b) => b.Name.CompareTo(a.Name));
                    ColumnName.Text = "\xE011";
                    break;
                case "NameUp":
                    Folder_Info.Sort((a, b) => a.Name.CompareTo(b.Name));
                    ColumnName.Text = "\xE010";
                    break;
                case "SizeDown":
                    Folder_Info.Sort((a, b) => b.Size.CompareTo(a.Size));
                    ColumnSize.Text = "\xE011";
                    break;
                case "SizeUp":
                    Folder_Info.Sort((a, b) => a.Size.CompareTo(b.Size));
                    ColumnSize.Text = "\xE010";
                    break;
                case "RatioDown":
                    Folder_Info.Sort((a, b) => b.Size.CompareTo(a.Size));
                    ColumnRatio.Text = "\xE011";
                    break;
                case "RatioUp":
                    Folder_Info.Sort((a, b) => a.Size.CompareTo(b.Size));
                    ColumnRatio.Text = "\xE010";
                    break;
                case "CreationTimeDown":
                    Folder_Info.Sort((a, b) => b.CreationTime.CompareTo(a.CreationTime));
                    ColumnCreationTime.Text = "\xE011";
                    break;
                case "CreationTimeUp":
                    Folder_Info.Sort((a, b) => a.CreationTime.CompareTo(b.CreationTime));
                    ColumnCreationTime.Text = "\xE010";
                    break;
            }
            listView.Items.Refresh();
        }
        private void Open_File(string path)
        {
            Process process = new Process();
            process.StartInfo.FileName = "cmd.exe";
            process.StartInfo.CreateNoWindow = true;
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.RedirectStandardInput = true;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.RedirectStandardError = true;
            process.StartInfo.Arguments = "/c " + path;
            process.Start();
            process.Close();
        }
        private void Backward_Click(object sender, RoutedEventArgs e)
        {
            BackwardButton_Click(sender, e);
        }
        private void Forward_Click(object sender, RoutedEventArgs e)
        {
            ForwardButton_Click(sender, e);
        }
        private void OpenListViewItem_Click(object sender, RoutedEventArgs e)
        {
            if (listView.SelectedItem is FolderInfo thisItem)
            {
                if (thisItem.ImgPath == "/Resource/folder.png")
                {
                    for (int i = 0; i < SelectedNode.Children.Count; i++)
                    {
                        if (thisItem.Name == SelectedNode.Children[i].Name)
                        {
                            SelectedNode = SelectedNode.Children[i];
                            DisplayNode();
                            return;
                        }
                    }
                }
                else if (thisItem.ImgPath == "/Resource/file.png")
                    Open_File(SelectedNode.FullPath + "\\" + thisItem.Name);
            }
        }
        private void OpenItemPath_Click(object sender, RoutedEventArgs e)
        {
            Process process = new Process();
            process.StartInfo.FileName = "explorer.exe";
            process.StartInfo.Arguments = SelectedNode.FullPath;
            process.Start();
        }
        private void ClearOrderInfo_Click(object sender, RoutedEventArgs e)
        {
            ListViewSortState = "Null";
            ColumnName.Text = ColumnSize.Text = ColumnRatio.Text = ColumnCreationTime.Text = "";
            DisplayNode();
        }
        private void DelAllSelectItems_Click(object sender, RoutedEventArgs e)
        {
            int direNum = 0, fileNum = 0;
            foreach (FolderInfo item in listView.SelectedItems)
            {
                if (item.ImgPath == "/Resource/folder.png")
                    direNum++;
                else if (item.ImgPath == "/Resource/file.png")
                    fileNum++;
            }
            MessageBoxResult result = MessageBox.Show("是否确认删除选中的所有项目:\r\n共 " + (direNum + fileNum).ToString() + " 个项目 (" + direNum.ToString() + " 个文件夹, " + fileNum.ToString() + " 个文件)",
                "删除所有所选项", MessageBoxButton.OKCancel, MessageBoxImage.Asterisk);
            if (result == MessageBoxResult.OK)
            {
                InitPanel.Visibility = ScanInfoPanel.Visibility = ChildItemsPanel.Visibility = FileDetailPanel.Visibility = Visibility.Hidden;
                FileDetailImg.Source = null;
                foreach (FolderInfo item in listView.SelectedItems)
                {
                    if (File.Exists(SelectedNode.FullPath + "\\" + item.Name))
                        File.Delete(SelectedNode.FullPath + "\\" + item.Name);
                    else if (Directory.Exists(SelectedNode.FullPath + "\\" + item.Name))
                        Directory.Delete(SelectedNode.FullPath + "\\" + item.Name, true);
                    Folder_Info.Remove(item);
                }
                listView.Items.Refresh();
            }
        }

        /// <summary>
        /// //中间右边的Canvas
        /// </summary>
        private void ChoosePanel(StackPanel DisplayPanel)
        {
            if (DisplayPanel.Visibility == Visibility.Visible)
                return;
            if (DisplayPanel == InitPanel) //只有一开始和treeView被清空时有效
            {
                InitPanel.Visibility = Visibility.Visible;
                ScanInfoPanel.Visibility = ChildItemsPanel.Visibility = FileDetailPanel.Visibility = Visibility.Hidden;
            }
            else if (DisplayPanel == ScanInfoPanel) //点击扫描的确定和按下扫描的Enter键时有效,即发生扫描事件时
            {
                ScanInfoPanel.Visibility = Visibility.Visible;
                InitPanel.Visibility = ChildItemsPanel.Visibility = FileDetailPanel.Visibility = Visibility.Hidden;
            }
            else if (DisplayPanel == ChildItemsPanel) //只有鼠标左键单击ListViewItem时
            {
                ChildItemsPanel.Visibility = Visibility.Visible;
                InitPanel.Visibility = ScanInfoPanel.Visibility = FileDetailPanel.Visibility = Visibility.Hidden;
            }
            else if (DisplayPanel == FileDetailPanel) //只有鼠标左键单击ListViewItem时
            {
                FileDetailPanel.Visibility = Visibility.Visible;
                InitPanel.Visibility = ScanInfoPanel.Visibility = ChildItemsPanel.Visibility = Visibility.Hidden;
            }
        }

        /// <summary>
        /// 下边左边的Setting
        /// </summary>
        private void Setting_Click(object sender, RoutedEventArgs e)
        {
            if (!SettingPopup.IsOpen)
            {
                SettingPopup.IsOpen = true;
                Setting.IsEnabled = false;
                Setting.Content = "\xE011";
            }
            else //这句虽然没什么用，但可能出现意外情况
                SettingPopup.IsOpen = false;
        }
        private void SettingPopup_Closed(object sender, EventArgs e)
        {
            Setting.Content = "\xE010";
            Setting.IsEnabled = true;
        }
        private void VisitHomeWebsite_Click(object sender, RoutedEventArgs e)
        {
            Process.Start("http://www.xunqe.com");
        }
        private void SettingUpdate_Click(object sender, RoutedEventArgs e)
        {
            SettingPopup.IsOpen = false;
            UpdateWindow updateWindow = new UpdateWindow { Owner = this };
            updateWindow.ShowDialog();
        }
        private void SettingHelp_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("1.就是点击右上角+按键进行扫描，然后就能看到各文件夹的大小情况了，左边是树状图，中间是列表图，右边是详细信息\r\n" +
                "2.扫描路径中已忽略的目录为没有权限读取的目录\r\n"+
                "3.检查软件更新时如果遇到长时间没反应，请1分钟左右后再次检查软件更新");
        }
        private void SettingAbout_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("版本: " + Application.ResourceAssembly.GetName().Version.ToString() + 
                "\r\n邮箱: 1575375168@qq.com\r\n网址: www.xunqe.com\r\n" +
                "GitHub: github.com/zou-z/FolderScanner");
        }
    }
    class Kernel
    {
        private readonly List<FolderTree> Folder_Tree;
        private List<FolderInfo> Folder_Info;
        private TreeView treeView;
        private ListView listView;
        private TextBlock tipText;
        private ProgressBar progressBar;
        private TextBlock ItemDetail;
        private TextBox IgnoredFolder;

        private int direNum;
        private int fileNum;
        private int ItemNum;
        private int FinishedItem;

        public Kernel(List<FolderTree> Folder_Tree, List<FolderInfo> Folder_Info,
                      TreeView treeView, ListView listView,
                      ProgressBar progressBar,
                      TextBlock tipText,
                      TextBlock ItemDetail,
                      TextBox IgnoredFolder)
        {
            this.Folder_Tree = Folder_Tree;
            this.Folder_Info = Folder_Info;
            this.treeView = treeView;
            this.listView = listView;
            this.tipText = tipText;
            this.progressBar = progressBar;
            this.ItemDetail = ItemDetail;
            this.IgnoredFolder = IgnoredFolder;

            IgnoredFolder.Text = "无";
            direNum = fileNum = ItemNum = FinishedItem = 0;
            listView.ItemsSource = null;
            treeView.ItemsSource = Folder_Tree;
        }
        public void Start(object folder_Tree)
        {
            FolderTree folderTree = folder_Tree as FolderTree;
            DirectoryInfo Dire = new DirectoryInfo(folderTree.FullPath);
            direNum = Dire.GetDirectories().Length;
            fileNum = Dire.GetFiles().Length;
            ItemNum = direNum + fileNum;
            progressBar.Dispatcher.BeginInvoke(new Action(delegate
            {
                progressBar.Value = 0.0;
                ItemDetail.Text = ItemNum.ToString() + " 个项目  " + direNum + " 个文件夹  " + fileNum + " 个文件";
            }));
            foreach (DirectoryInfo dire in Dire.GetDirectories())
            {
                if ((dire.Attributes & FileAttributes.Hidden) == FileAttributes.Hidden)
                    continue;
                //计算文件夹大小和TreeView相关
                FolderTree childFolder = new FolderTree(dire.Name, dire.FullName) { Parent = folderTree };
                folderTree.Children.Add(childFolder);
                tipText.Dispatcher.BeginInvoke(new Action(delegate { tipText.Text = "扫描中...(" + progressBar.Value.ToString() + "%)" + "\r\n" + childFolder.FullPath; }));
                GetFolderSize(childFolder);
                folderTree.Size += childFolder.Size;
                //ListView相关
                FolderInfo folderInfo = new FolderInfo(dire.Name, childFolder.Size, dire.CreationTime.ToString(), "/Resource/folder.png");
                Folder_Info.Add(folderInfo);
                FinishedAnItem();
            }
            foreach (FileInfo file in Dire.GetFiles())
            {
                if ((file.Attributes & FileAttributes.Hidden) == FileAttributes.Hidden)
                    continue;
                folderTree.Size += file.Length;
                FolderInfo folderInfo = new FolderInfo(file.Name, file.Length, file.CreationTime.ToString(), "/Resource/file.png");
                Folder_Info.Add(folderInfo);
                FinishedAnItem();
            }
            UpdateListView(folderTree.Size);
            folderTree.IsSelected = true;
            treeView.Dispatcher.BeginInvoke(new Action(delegate { treeView.Items.Refresh(); }));
            progressBar.Dispatcher.BeginInvoke(new Action(delegate { progressBar.Value = 100; }));
            tipText.Dispatcher.BeginInvoke(new Action(delegate { tipText.Text = "完成!"; }));
        }
        private void GetFolderSize(FolderTree folderTree)
        {
            DirectoryInfo Dire = new DirectoryInfo(folderTree.FullPath);
            try
            {
                foreach (DirectoryInfo dire in Dire.GetDirectories())
                {
                    FolderTree childFolder = new FolderTree(dire.Name, dire.FullName) { Parent = folderTree };
                    folderTree.Children.Add(childFolder);
                    GetFolderSize(childFolder);
                    folderTree.Size += childFolder.Size;
                }
                foreach (FileInfo file in Dire.GetFiles())
                {
                    folderTree.Size += file.Length;
                }
            }
            catch (System.UnauthorizedAccessException)
            {
                IgnoredFolder.Dispatcher.BeginInvoke(new Action(delegate 
                {
                    if (IgnoredFolder.Text == "无")
                        IgnoredFolder.Text = folderTree.FullPath + "\r\n";
                    else
                        IgnoredFolder.Text += folderTree.FullPath + "\r\n";
                }));
            }
        }
        private void FinishedAnItem()
        {
            FinishedItem++;
            progressBar.Dispatcher.BeginInvoke(new Action(delegate
            {
                progressBar.Value = Math.Round((FinishedItem * 100.0 / ItemNum), 2);
                tipText.Text = "扫描中...(" + progressBar.Value.ToString() + "%)";
            }));
        }
        private void UpdateListView(long TotalSize)
        {
            for (int i = 0; i < Folder_Info.Count(); i++)
            {
                Folder_Info[i].CalcRatio(TotalSize);
            }
            treeView.Dispatcher.BeginInvoke(new Action(delegate
            {
                listView.Items.Clear();
                listView.ItemsSource = Folder_Info;
            }));
        }
    }
    public class SuggestItems
    {
        public string FullPath { get; set; }
        public SuggestItems(string FullPath)
        {
            this.FullPath = FullPath;
        }
    }
    public class ChildItems
    {
        public string ListBoxImgPath { get; set; }
        public string ListBoxItemName { get; set; }
        public string ListBoxItemSize { get; set; }
        public ChildItems(string ListBoxImgPath, string ListBoxItemName, long ListBoxItemSize)
        {
            this.ListBoxImgPath = ListBoxImgPath;
            this.ListBoxItemName = ListBoxItemName;
            this.ListBoxItemSize = CalcDisplaySize(ListBoxItemSize);
        }
        private string CalcDisplaySize(long Size)
        {
            string[] units = new string[] { "B", "K", "M", "G", "T" };
            int unit_order = 0;
            double size = Size;
            while (size >= 1024)
            {
                size /= 1024;
                unit_order++;
            }
            return Math.Round(size, 2).ToString() + " " + units[unit_order];
        }
    }
    public class FolderInfo
    {
        public string Name { get; set; }
        public string ImgPath { get; set; }
        public long Size { get; set; }
        public string DisplaySize { get; set; }
        public double Ratio { get; set; }
        public string DisplayRatio { get; set; }
        public string CreationTime { get; set; }
        public FolderInfo(string Name, long Size, string CreationTime, string ImgPath)
        {
            this.Name = Name;
            this.Size = Size;
            this.DisplaySize = CalcDisplaySize();
            this.Ratio = 0;
            this.CreationTime = CreationTime;
            this.ImgPath = ImgPath;
            DisplayRatio = null;
        }
        private string CalcDisplaySize()
        {
            string[] units = new string[] { "B", "K", "M", "G", "T" };
            int unit_order = 0;
            double size = Size;
            while (size >= 1024)
            {
                size /= 1024;
                unit_order++;
            }
            return Math.Round(size, 2).ToString() + " " + units[unit_order];
        }
        public void CalcRatio(long TotalSize)
        {
            if (TotalSize == 0)
                Ratio = 0;
            else
                Ratio = Math.Round((100.0 * Size / TotalSize), 2);
            DisplayRatio = Ratio.ToString() + "%";
        }
    }
    class FolderTree
    {
        public string Name { get; set; }
        public long Size { get; set; }
        public string FullPath { get; set; }
        public bool IsSelected { get; set; }
        public bool IsExpanded { get; set; }
        public List<FolderTree> Children { get; set; }
        public FolderTree Parent { get; set; }
        public FolderTree(string Name, string FullPath)
        {
            Children = new List<FolderTree>();
            this.Name = Name;
            this.Size = 0;
            this.FullPath = FullPath;
            this.IsSelected = false;
            this.IsExpanded = false;
            Parent = null;
        }
        public FolderTree Find(string path)
        {
            if (FullPath == path)
                return this;
            return _Find(this, path);
        }
        private FolderTree _Find(FolderTree Tree, string path)
        {
            FolderTree TargetTree = null;
            foreach (FolderTree child in Tree.Children)
            {
                if (TargetTree == null)
                {
                    if (child.FullPath == path)
                        TargetTree = child;
                    else
                        TargetTree = _Find(child, path);
                }
                else
                    return TargetTree;
            }
            return TargetTree;
        }
    }
    public static class MyTreeViewHelper
    {
        //
        // The TreeViewItem that the mouse is currently directly over (or null).
        //
        private static TreeViewItem _currentItem = null;
        //
        // IsMouseDirectlyOverItem:  A DependencyProperty that will be true only on the
        // TreeViewItem that the mouse is directly over.  I.e., this won't be set on that
        // parent item.
        //
        // This is the only public member, and is read-only.
        //
        // The property key (since this is a read-only DP)
        private static readonly DependencyPropertyKey IsMouseDirectlyOverItemKey = DependencyProperty.RegisterAttachedReadOnly("IsMouseDirectlyOverItem",typeof(bool),typeof(MyTreeViewHelper),new FrameworkPropertyMetadata(null,new CoerceValueCallback(CalculateIsMouseDirectlyOverItem)));
        // The DP itself
        public static readonly DependencyProperty IsMouseDirectlyOverItemProperty = IsMouseDirectlyOverItemKey.DependencyProperty;
        // A strongly-typed getter for the property.
        public static bool GetIsMouseDirectlyOverItem(DependencyObject obj)
        {
            return (bool)obj.GetValue(IsMouseDirectlyOverItemProperty);
        }
        // A coercion method for the property
        private static object CalculateIsMouseDirectlyOverItem(DependencyObject item, object value)
        {
            // This method is called when the IsMouseDirectlyOver property is being calculated
            // for a TreeViewItem. 
            if (item == _currentItem)
                return true;
            else
                return false;
        }
        //
        // UpdateOverItem:  A private RoutedEvent used to find the nearest encapsulating
        // TreeViewItem to the mouse's current position.
        //
        private static readonly RoutedEvent UpdateOverItemEvent = EventManager.RegisterRoutedEvent("UpdateOverItem", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(MyTreeViewHelper));
        //
        // Class constructor
        //
        static MyTreeViewHelper()
        {
            // Get all Mouse enter/leave events for TreeViewItem.
            EventManager.RegisterClassHandler(typeof(TreeViewItem),TreeViewItem.MouseEnterEvent,new MouseEventHandler(OnMouseTransition), true);
            EventManager.RegisterClassHandler(typeof(TreeViewItem),TreeViewItem.MouseLeaveEvent,new MouseEventHandler(OnMouseTransition), true);
            // Listen for the UpdateOverItemEvent on all TreeViewItem's.
            EventManager.RegisterClassHandler(typeof(TreeViewItem),UpdateOverItemEvent,new RoutedEventHandler(OnUpdateOverItem));
        }
        //
        // OnUpdateOverItem:  This method is a listener for the UpdateOverItemEvent.  When it is received,
        // it means that the sender is the closest TreeViewItem to the mouse (closest in the sense of the
        // tree, not geographically).
        static void OnUpdateOverItem(object sender, RoutedEventArgs args)
        {
            // Mark this object as the tree view item over which the mouse
            // is currently positioned.
            _currentItem = sender as TreeViewItem;
            // Tell that item to re-calculate the IsMouseDirectlyOverItem property
            _currentItem.InvalidateProperty(IsMouseDirectlyOverItemProperty);
            // Prevent this event from notifying other tree view items higher in the tree.
            args.Handled = true;
        }
        //
        // OnMouseTransition:  This method is a listener for both the MouseEnter event and
        // the MouseLeave event on TreeViewItems.  It updates the _currentItem, and updates
        // the IsMouseDirectlyOverItem property on the previous TreeViewItem and the new
        // TreeViewItem.
        static void OnMouseTransition(object sender, MouseEventArgs args)
        {
            lock (IsMouseDirectlyOverItemProperty)
            {
                if (_currentItem != null)
                {
                    // Tell the item that previously had the mouse that it no longer does.
                    DependencyObject oldItem = _currentItem;
                    _currentItem = null;
                    oldItem.InvalidateProperty(IsMouseDirectlyOverItemProperty);
                }
                // Get the element that is currently under the mouse.
                IInputElement currentPosition = Mouse.DirectlyOver;
                // See if the mouse is still over something (any element, not just a tree view item).
                if (currentPosition != null)
                {
                    // Yes, the mouse is over something.
                    // Raise an event from that point.  If a TreeViewItem is anywhere above this point
                    // in the tree, it will receive this event and update _currentItem.
                    RoutedEventArgs newItemArgs = new RoutedEventArgs(UpdateOverItemEvent);
                    currentPosition.RaiseEvent(newItemArgs);
                }
            }
        }
    }
}
