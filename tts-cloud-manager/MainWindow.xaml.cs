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
            if (_parent == null) return CloudManager.GetCloudData().children;
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
            IProgress<double> progress = new Progress<double>(value => progress_bar.SetProgress(value));
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

        private void FileDelete()
        {
            if (TreeCloud.SelectedItems.Count != 1)
            {
                throw new Exception("Please, select a single file.");
            }
            var obj = TreeCloud.SelectedItem as CloudItem;
            if (obj == null)
            {
                throw new Exception("Please, select a file first.");
            }
            if (obj.data == null)
            {
                throw new Exception("You need to select a file, not a folder.");
            }
            CloudManager.DeleteFile(obj.data.Value);
            UpdateTree();
        }

        private async void FileDelete_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                FileDelete();
            }
            catch (Exception ex)
            {
                await this.ShowMessageAsync("Error", ex.Message + "\n\n" + ex.StackTrace);
            }
        }
    }
}
