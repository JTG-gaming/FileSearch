using Microsoft.Win32;
using System.Configuration;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Windows;
using Wpf.Ui.Appearance;

namespace FileSearch
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            string? startingPath = "";
            if (e.Args.Length > 0)
            {
                string argument = e.Args[0];
                if (Directory.Exists(argument))
                {
                    startingPath = argument;
                }
                else if (File.Exists(argument))
                {
                    startingPath = Path.GetDirectoryName(argument);
                }
            }

            MainWindow mainWindow = new MainWindow(startingPath);
            MainWindow = mainWindow;
            //SystemThemeWatcher.Watch(mainWindow);
            mainWindow.Show();

        }

        public static void InstallContextMenu()
        {
            string exePath = Environment.ProcessPath
                ?? Process.GetCurrentProcess().MainModule?.FileName
                ?? throw new InvalidOperationException("Could not determine executable path.");

            const string appKeyName = "FileSearch";
            const string folderText = "Open with File Search";
            const string folderBackgroundText = "Open File Search here";

            // Right-click directly ON a folder
            string folderKeyPath =
                $@"Software\Classes\Directory\shell\{appKeyName}";

            using (RegistryKey folderKey = Registry.CurrentUser.CreateSubKey(folderKeyPath))
            {
                folderKey.SetValue("", folderText);
                folderKey.SetValue("Icon", exePath);
            }

            using (RegistryKey commandKey =
                   Registry.CurrentUser.CreateSubKey(folderKeyPath + @"\command"))
            {
                commandKey.SetValue("", $"\"{exePath}\" \"%1\"");
            }


            // Right-click INSIDE a folder background
            string backgroundKeyPath =
                $@"Software\Classes\Directory\Background\shell\{appKeyName}";

            using (RegistryKey backgroundKey =
                   Registry.CurrentUser.CreateSubKey(backgroundKeyPath))
            {
                backgroundKey.SetValue("", folderBackgroundText);
                backgroundKey.SetValue("Icon", exePath);
            }

            using (RegistryKey commandKey =
                   Registry.CurrentUser.CreateSubKey(backgroundKeyPath + @"\command"))
            {
                commandKey.SetValue("", $"\"{exePath}\" \"%V\"");
            }

            MessageBox.Show(
                "File Search was added to the folder context menu.",
                "File Search",
                MessageBoxButton.OK,
                MessageBoxImage.Information
            );
        }

        public static void RemoveContextMenu()
        {
            const string appKeyName = "FileSearch";

            Registry.CurrentUser.DeleteSubKeyTree(
                $@"Software\Classes\Directory\shell\{appKeyName}",
                throwOnMissingSubKey: false
            );

            Registry.CurrentUser.DeleteSubKeyTree(
                $@"Software\Classes\Directory\Background\shell\{appKeyName}",
                throwOnMissingSubKey: false
            );

            MessageBox.Show(
                "File Search was removed from the folder context menu.",
                "File Search",
                MessageBoxButton.OK,
                MessageBoxImage.Information
            );
        }
    }

}
