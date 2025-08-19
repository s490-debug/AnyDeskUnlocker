using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace AnyDeskUnlocker
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
        }

        static bool IsAdministrator()
        {
            var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
            var principal = new System.Security.Principal.WindowsPrincipal(identity);
            return principal.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
        }

        static bool RegistryKeyExists(string keyPath)
        {
            try
            {
                using (var key = Registry.Users.OpenSubKey(keyPath))
                {
                    return key != null;
                }
            }
            catch
            {
                return false;
            }
        }

        static void DeleteFileIfExists(string path)
        {
            try
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
            catch {}
        }

        static void MoveFile(string source, string destination)
        {
            try
            {
                if (File.Exists(source))
                {
                    File.Move(source, destination);
                }
                else if (File.Exists(destination))
                {
                    File.Delete(destination);
                    File.Move(source, destination);
                }
                else
                {
                    File.Copy(source, destination, true);
                }
            }
            catch {}
        }

        static void CopyFile(string source, string destination)
        {
            try
            {
                if (File.Exists(source))
                    File.Copy(source, destination, true);
            }
            catch {}
        }

        static void DeleteDirectoryIfExists(string path)
        {
            try
            {
                if (Directory.Exists(path))
                    Directory.Delete(path, true);
            }
            catch {}
        }

        static void CopyDirectory(string sourceDir, string targetDir)
        {
            try
            {
                if (!Directory.Exists(sourceDir))
                    return;

                Directory.CreateDirectory(targetDir);

                foreach (var file in Directory.GetFiles(sourceDir))
                {
                    var destFileName = Path.Combine(targetDir, Path.GetFileName(file));
                    File.Copy(file, destFileName, true);
                }

                foreach (var dir in Directory.GetDirectories(sourceDir))
                {
                    var destDir = Path.Combine(targetDir, Path.GetFileName(dir));
                    CopyDirectory(dir, destDir);
                }
            }
            catch {}
        }

        static void DeleteAllFilesInDirectory(string dir)
        {
            try
            {
                if (!Directory.Exists(dir))
                    return;

                foreach (var file in Directory.GetFiles(dir))
                {
                    File.Delete(file);
                }

                foreach (var subdir in Directory.GetDirectories(dir))
                {
                    Directory.Delete(subdir, true);
                }
            }
            catch {}
        }

        static void StartAnyDeskService()
        {
            try
            {
                using (ServiceController sc = new ServiceController("AnyDesk"))
                {
                    sc.Start();
                    sc.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(30));

                    while (sc.Status != ServiceControllerStatus.Running)
                    {
                        System.Threading.Thread.Sleep(500);
                        sc.Refresh();
                    }
                }
            }
            catch {}
        }

        static void StopAnyDeskService()
        {
            try
            {
                using (ServiceController sc = new ServiceController("AnyDesk"))
                {
                    if (sc.Status != ServiceControllerStatus.Stopped && sc.Status != ServiceControllerStatus.StopPending)
                    {
                        sc.Stop();
                        sc.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(30));

                        while (sc.Status != ServiceControllerStatus.Stopped)
                        {
                            System.Threading.Thread.Sleep(500);
                            sc.Refresh();
                        }
                    }

                    foreach (var process in Process.GetProcessesByName("AnyDesk"))
                    {
                        process.Kill();
                        process.WaitForExit();
                    }
                }
            }
            catch {}
        }

        private void button1_Click(object sender, EventArgs e)
        {
            StopAnyDeskService();

            string allUsersProfile = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string tempPath = Path.GetTempPath();

            string anyDeskProfilePath = Path.Combine(allUsersProfile, "AnyDesk");
            string appDataAnyDeskPath = Path.Combine(appData, "AnyDesk");

            DeleteFileIfExists(Path.Combine(anyDeskProfilePath, "service.conf"));
            DeleteFileIfExists(Path.Combine(appDataAnyDeskPath, "service.conf"));

            string userConfSource = Path.Combine(appDataAnyDeskPath, "user.conf");
            string userConfTemp = Path.Combine(tempPath, "user.conf");
            CopyFile(userConfSource, userConfTemp);

            string tempThumbnailsDir = Path.Combine(tempPath, "thumbnails");
            DeleteDirectoryIfExists(tempThumbnailsDir);

            string sourceThumbnailsDir = Path.Combine(appDataAnyDeskPath, "thumbnails");
            CopyDirectory(sourceThumbnailsDir, tempThumbnailsDir);

            DeleteAllFilesInDirectory(anyDeskProfilePath);
            DeleteAllFilesInDirectory(appDataAnyDeskPath);

            StartAnyDeskService();

            string systemConfPath = Path.Combine(anyDeskProfilePath, "system.conf");
            if (File.Exists(systemConfPath))
            {
                bool hasIdLine = false;
                using (var reader = new StreamReader(systemConfPath))
                {
                    string line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        if (line.Contains("ad.anynet.id="))
                        {
                            hasIdLine = true;
                            break;
                        }
                    }
                }

                if (hasIdLine)
                {
                    StopAnyDeskService();
                    MoveFile(userConfTemp, userConfSource);

                    CopyDirectory(tempThumbnailsDir, sourceThumbnailsDir);
                    DeleteDirectoryIfExists(tempThumbnailsDir);

                    StartAnyDeskService();
                }
            }

            MessageBox.Show("Succesfully unlocked AnyDesk!", "AnyDeskUnlocker", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }
}
