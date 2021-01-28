using MahApps.Metro.Controls;
using MahApps.Metro.Controls.Dialogs;
using Microsoft.Win32;
using Newtonsoft.Json;
using Newtonsoft.Json.Bson;
using Newtonsoft.Json.Linq;
using Steamworks;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
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
using tts_cloud_manager.tree;

namespace tts_cloud_manager
{

    public class CloudItem : ITreeModel
    {
        // Name of the object itself, the name of the file or folder
        public string name { get; set; }
        // Full path of the folder containing the file, or the path 
        // to the folder including itself
        public string fullpath { get; set; }
        public string cloud_url { get; set; }
        public CloudData? data { get; set; }
        // Size as in "5MB", "700KB"
        public string size { get; set; }
        public IList<CloudItem> children { get; set; }

        public CloudItem(string path, string parentname)
        {
            name = parentname;
            fullpath = path;
            children = new List<CloudItem>();
            cloud_url = "";
            size = "";
        }

        public void AddChildren(CloudItem child)
        {
            children.Add(child);
        }

        public IEnumerable GetChildren(object parent)
        {
            CloudItem _parent = parent as CloudItem;
            if (_parent == null) return new List<CloudItem> { CloudManager.GetCloudData() };
            return _parent.children;
        }

        public bool HasChildren(object parent)
        {
            CloudItem _parent = parent as CloudItem;
            if (_parent == null) return false;
            return _parent.children.Count > 0;
        }
    }

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : MetroWindow, INotifyPropertyChanged
    {

        private List<CloudItem> m_folders;
        public List<CloudItem> Folders
        {
            get { return m_folders; }
            set
            {
                m_folders = value;
                NotifiyPropertyChanged("Folders");
            }
        }

        public MainWindow()
        {
            InitializeComponent();
            this.DataContext = this;
        }

        void NotifiyPropertyChanged(string property)
        {
            if (PropertyChanged != null)
                PropertyChanged(this, new PropertyChangedEventArgs(property));
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void GetData()
        {
            CloudManager.ConnectToSteam();
            UpdateTree();
            UpdateQuota();
        }

        private void UpdateQuota()
        {
            ulong consumed_bytes, all_bytes;
            CloudManager.GetQuota(out consumed_bytes, out all_bytes);
            lbl_Quota.Content = CloudManager.byte_size_to_str(consumed_bytes) + " of " + CloudManager.byte_size_to_str(all_bytes);
        }

        private void UpdateTree()
        {
            TreeCloud.Model = new CloudItem("", "root");
            TreeCloud.SetIsExpanded(TreeCloud.Nodes.First(), true);
        }

        private async void GetData_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                GetData();
            }
            catch (Exception ex)
            {
                await this.ShowMessageAsync("Error", ex.Message + "\n\n" + ex.StackTrace);
            }
        }

        private async Task UploadData()
        {
            if (TreeCloud.SelectedItems.Count != 1)
            {
                throw new Exception("Please, select a single folder.");
            }
            var selected = TreeCloud.SelectedItem as TreeNode;
            if (selected == null)
            {
                throw new Exception("Please, select a folder first.");
            }
            var obj = selected.Tag as CloudItem;
            if (obj == null)
            {
                throw new Exception("Please, select a folder first.");
            }
            if (obj.data != null)
            {
                throw new Exception("You need to select a folder, not a file.");
            }
            var upload_folder = obj.fullpath;

            OpenFileDialog dlg = new OpenFileDialog();
            dlg.Multiselect = true;
            dlg.FileName = "Asset"; // Default file name

            bool? result = dlg.ShowDialog();

            if (result == null || result == false)
            {
                return;
            }

            var progress_bar = await this.ShowProgressAsync("Uploading", "Please wait...");
            IProgress<double> progress = new Progress<double>(value =>
            {
                if (value > 1)
                    progress_bar.SetProgress(1);
                else
                    progress_bar.SetProgress(value);
            });
            await CloudManager.UploadFiles(upload_folder, dlg.FileNames, progress);
            UpdateTree();
            await progress_bar.CloseAsync();
        }

        private async void UploadData_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                await UploadData();
            }
            catch (Exception ex)
            {
                if (ex is AggregateException)
                {
                    ex = ((AggregateException)ex).InnerException;
                }
                await this.ShowMessageAsync("Error", ex.Message + "\n\n" + ex.StackTrace);
            }
        }

        private async Task FileDelete()
        {
            var items = TreeCloud.SelectedItems;
            IList<CloudItem> cloud_objs = new List<CloudItem>();
            // First validate all
            foreach (var item in items)
            {
                var node = item as TreeNode;
                if (node == null)
                {
                    throw new Exception("Please, select only files.");
                }
                var obj = node.Tag as CloudItem;
                if (obj.data == null)
                {
                    throw new Exception("You need to select files, not folders.");
                }
            }
            var progress_bar = await this.ShowProgressAsync("Deleting", "Please wait...");
            IProgress<double> progress = new Progress<double>(value => progress_bar.SetProgress(value));
            progress.Report(0.1);
            int nfiles = items.Count;
            int files_processed = 0;
            foreach (var item in items)
            {
                var node = item as TreeNode;
                var obj = node.Tag as CloudItem;
                CloudManager.DeleteFile(obj.data.Value);
                files_processed++;
                progress.Report(0.1 + 0.9 * files_processed / nfiles);
            }
            UpdateTree();
            await progress_bar.CloseAsync();
        }

        private async void FileDelete_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                await FileDelete();
            }
            catch (Exception ex)
            {
                await this.ShowMessageAsync("Error", ex.Message + "\n\n" + ex.StackTrace);
            }
        }

        private void CopyLUA()
        {
            if (TreeCloud.SelectedItems.Count < 1)
            {
                throw new Exception("Please, select at least one file.");
            }
            var items = TreeCloud.SelectedItems;
            IList<CloudItem> cloud_objs = new List<CloudItem>();
            foreach (var item in items)
            {
                var node = item as TreeNode;
                if (node == null)
                {
                    throw new Exception("Please, select only files.");
                }
                var obj = node.Tag as CloudItem;
                if (obj == null)
                {
                    throw new Exception("Please, select only files.");
                }
                if (obj.data == null)
                {
                    throw new Exception("You need to select a file, not a folder.");
                }
                cloud_objs.Add(obj);
            }

            string[] lua_obj_strs = new string[cloud_objs.Count];
            int i = 1;
            foreach (var cloud_obj in cloud_objs)
            {
                lua_obj_strs[i-1] = $"\t[{i}] = {{ name = '{cloud_obj.name}', url = '{cloud_obj.cloud_url}' }}";
                i++;
            }
            string final_str = "{\n" + string.Join(",\n", lua_obj_strs) + "\n}";
            Clipboard.SetText(final_str);
        }

        private async void CopyLUA_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                CopyLUA();
            }
            catch (Exception ex)
            {
                await this.ShowMessageAsync("Error", ex.Message + "\n\n" + ex.StackTrace);
            }
        }

        private async void TreeCloud_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                foreach (var item in e.AddedItems)
                {
                    if (item is TreeNode node)
                    {
                        if (node.Tag != null && node.Tag is CloudItem clouditem)
                        {
                            if (clouditem.children != null && clouditem.children.Count > 0) continue;
                            Clipboard.SetText(clouditem.data?.URL);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                await this.ShowMessageAsync("Error", ex.Message + "\n\n" + ex.StackTrace);
            }
        }
    }
}