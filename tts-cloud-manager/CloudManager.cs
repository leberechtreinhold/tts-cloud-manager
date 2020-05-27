﻿using Newtonsoft.Json;
using Newtonsoft.Json.Bson;
using Steamworks;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace tts_cloud_manager
{
    public struct CloudData
    {
        // All are public with get/set for the serializer
        public string Name;
        public string URL;
        public int Size;
        public string Date; // Yep, it's just a string
        public string Folder;

        public CloudData(string name, string url, int size, string date, string folder)
        {
            this.Name = name;
            this.URL = url;
            this.Size = size;
            this.Date = date;
            this.Folder = folder;
        }
    }

    public static class CloudManager
    {
        const string TTS_APP_ID = "286160";

        public static T ParseBson<T>(byte[] data)
        {
            using (MemoryStream memoryStream = new MemoryStream(data))
            {
                using (BsonDataReader bsonReader = new BsonDataReader(memoryStream))
                {
                    JsonSerializer serializer = new JsonSerializer();
                    return serializer.Deserialize<T>(bsonReader);
                }
            }
        }

        private static byte[] ToBson(object obj)
        {
            byte[] result;
            using (MemoryStream memoryStream = new MemoryStream())
            {
                using (BsonDataWriter bsonWriter = new BsonDataWriter(memoryStream))
                {
                    JsonSerializer serializer = new JsonSerializer { NullValueHandling = NullValueHandling.Ignore };
                    serializer.Serialize(bsonWriter, obj);
                }
                result = memoryStream.ToArray();
            }
            return result;
        }

        public static void ConnectToSteam()
        {
            Environment.SetEnvironmentVariable("SteamAppID", TTS_APP_ID);
            bool initialized = SteamAPI.Init();
            if (!initialized)
            {
                // SteamAPI uses the app id from the environment variable, but if it's
                // not available, it uses a steam_appid file.
                try
                {
                    File.WriteAllText("steam_appid.txt", TTS_APP_ID);
                    initialized = SteamAPI.Init();
                    File.Delete("steam_appid.txt");
                }
                catch
                {
                    // Failure wriitng, maybe lack of privileges
                }
            }

            if (!initialized)
            {
                throw new Exception("Cannot connect to TTS with ID " + TTS_APP_ID + ". "
                    + "Plese ensure the Steam Client is running with the same privileges and "
                    + "account as the user who is launching the tts-cloud-manager.");
            }
        }

        private static CloudItem CreateFather(string path, Dictionary<string, CloudItem> folders)
        {
            if (folders.ContainsKey(path))
            {
                return folders[path];
            }

            if (path.Contains('\\'))
            {
                int lastindex = path.LastIndexOf('\\');
                string father = path.Substring(0, lastindex);
                string child = path.Substring(lastindex + 1);

                folders[path] = new CloudItem(path, child);
                var parent = CreateFather(father, folders);
                folders[father].AddChildren(folders[path]);
                return folders[path];
            }
            else
            {
                folders[path] = new CloudItem(path, path);
                folders[""].AddChildren(folders[path]);
                return folders[path];
            }
        }
        public static CloudItem GetCloudData()
        {
            if (!SteamRemoteStorage.FileExists("CloudInfo.bson"))
            {
                throw new Exception("There's no CloudInfo.bson, but it should exist. "
                    + "Check your Cloud Manager inside TTS.");
            }
            if (!SteamRemoteStorage.FileExists("CloudFolder.bson"))
            {
                throw new Exception("There's no CloudFolder.bson, but it should exist. "
                    + "Check your Cloud Manager inside TTS.");
            }
            var data = GetFile("CloudInfo.bson");
            BackupFile("CloudInfo.bson", data);

            var cloud_info = ParseBson<Dictionary<string, CloudData>>(data);
            if (cloud_info == null)
            {
                throw new Exception("There's something weird with your CloudInfo.bson, "
                    + "which is in your local folder. Please, save it and report it to the github page:");
            }

            var folders = new Dictionary<string, CloudItem>();
            var folder_tree_root = new CloudItem("", "root");

            folders.Add("", folder_tree_root);

            var cloud_info_list = cloud_info.Values.OrderBy(d => string.IsNullOrWhiteSpace(d.Folder) ? 0 : d.Folder.Length);
            foreach (var value in cloud_info_list)
            {
                string foldername = string.IsNullOrWhiteSpace(value.Folder) ? "" : value.Folder;
                if (!folders.ContainsKey(foldername))
                {
                    if (foldername.Contains('\\'))
                    {
                        int lastindex = foldername.LastIndexOf('\\');
                        string father = foldername.Substring(0, lastindex);
                        string child = foldername.Substring(lastindex + 1);
                        folders[foldername] = new CloudItem(foldername, child);
                        if (!folders.ContainsKey(father))
                        {
                            CreateFather(father, folders);
                        }
                        folders[father].AddChildren(folders[foldername]);
                    }
                    else if (foldername.Length > 0)
                    {
                        folders[foldername] = new CloudItem(foldername, foldername);
                        folders[""].AddChildren(folders[foldername]);
                    }
                }
                var item = new CloudItem(foldername, value.Name);
                item.data = value;
                item.cloud_url = value.URL;
                item.size = byte_size_to_str(value.Size);
                folders[foldername].AddChildren(item);
            }

            return folder_tree_root;
        }

        private static string byte_size_to_str(int value)
        {
            // No need for bigger numbers, steam cloud doesn't support more
            string[] suffix = { " B", " KB", " MB", " GB", " TB" };
            double dvalue = value;
            int scale = 0;
            while (dvalue > 1024)
            {
                dvalue = dvalue / 1024;
                scale++;
            }
            return Math.Round(dvalue, 1) + suffix[scale];
        }

        private static void BackupFile(string name, byte[] data)
        {
            File.WriteAllBytes(DateTime.Now.ToString("yyyy_MM_dd_HH_mm_ss_") + name, data);
        }

        private static bool ByteArrayEquals(byte[] a1, byte[] a2)
        {
            if (a1.Length != a2.Length)
                return false;

            for (int i = 0; i < a1.Length; i++)
                if (a1[i] != a2[i])
                    return false;

            return true;
        }

        public static async Task<bool> UploadFiles(string folderpath, string[] filenames, IProgress<double> progress)
        {
            var data_original = GetFile("CloudInfo.bson");
            BackupFile("CloudInfo_preupload.bson", data_original);
            var cloud_data = ParseBson<Dictionary<string, CloudData>>(data_original);

            progress.Report(0.1);
            int nfiles = filenames.Length;
            int files_processed = 0;
            foreach (var name in filenames)
            {
                var file_data = File.ReadAllBytes(name);
                var file_name = Path.GetFileName(name);
                var file_hash = BitConverter.ToString(new SHA1CryptoServiceProvider().ComputeHash(file_data)).Replace("-", "");
                var cloud_name = file_hash + "_" + file_name;

                var file_url = await UploadFileAndShare(cloud_name, file_hash, file_data);
                var cloudfile_data = new CloudData(file_name, file_url, file_data.Length, DateTime.Now.ToString(), folderpath);
                cloud_data[cloud_name] = cloudfile_data;

                files_processed++;
                progress.Report(0.1 + 0.9 * files_processed / nfiles);
            }
            var new_data = ToBson(cloud_data);
            BackupFile("CloudInfo_postload.bson", new_data);
            UploadFile("CloudInfo.bson", new_data);
            return true;
        }

        public static void DeleteFile(CloudData data)
        {
            var data_original = GetFile("CloudInfo.bson");
            BackupFile("CloudInfo_predelete.bson", data_original);
            var cloud_data = ParseBson<Dictionary<string, CloudData>>(data_original);
            KeyValuePair<string, CloudData>? found = null;
            foreach(var kv in cloud_data)
            {
                if (kv.Value.Folder == data.Folder
                    && kv.Value.Name == data.Name
                    && kv.Value.URL == data.URL)
                {
                    found = kv;
                    break;
                }
            }
            if (found == null)
            {
                throw new Exception("Could not find file " + data.Name + ". Please, refresh the tree.");
            }
            cloud_data.Remove(found.Value.Key);
            var new_data = ToBson(cloud_data);
            BackupFile("CloudInfo_postdelete.bson", new_data);
            UploadFile("CloudInfo.bson", new_data);
        }

        private static void UploadFile(string name, byte[] data)
        {
            if (!SteamRemoteStorage.FileWrite(name, data, data.Length))
            {
                throw new Exception("Cannot upload " + name);
            }
        }

        private static async Task<string> UploadFileAndShare(string name, string hash, byte[] data)
        {
            if (!SteamRemoteStorage.FileWrite(name, data, data.Length))
            {
                throw new Exception("Cannot upload " + name);
            }
            var sharer = new FileSharer(name, hash);
            return await sharer.Share();
        }

        private static byte[] GetFile(string name)
        {
            int size = SteamRemoteStorage.GetFileSize(name);
            byte[] bytes = new byte[size];
            SteamRemoteStorage.FileRead(name, bytes, size);
            return bytes;
        }
    }

    public class FileSharer
    {
        private CallResult<RemoteStorageFileShareResult_t> result;
        private string name;
        private string sha1;
        private bool finished;
        private string url;

        public FileSharer(string _name, string _sha1)
        {
            name = _name;
            sha1 = _sha1;
            result = CallResult<RemoteStorageFileShareResult_t>.Create(OnShareFinished);
            finished = false;
            url = "";
        }

        public async Task<string> Share()
        {
            var ret = SteamRemoteStorage.FileShare(name);
            result.Set(ret);
            while (!finished)
            {
                SteamAPI.RunCallbacks();
                await Task.Delay(100);
            }
            return url;
        }

        private void OnShareFinished(RemoteStorageFileShareResult_t pCallback, bool fail)
        {
            if (fail || pCallback.m_eResult != EResult.k_EResultOK)
            {
                throw new Exception("Error sharing " + name);
            }
            finished = true;
            url = "http://cloud-3.steamusercontent.com/ugc/" + pCallback.m_hFile.ToString() + "/" + sha1 + "/";
        }
    }
}
