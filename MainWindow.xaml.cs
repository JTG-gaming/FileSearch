using Microsoft.Win32;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using static System.Net.Mime.MediaTypeNames;

namespace FileSearch
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(
            IntPtr hwnd,
            int dwAttribute,
            ref int pvAttribute,
            int cbAttribute);
        private static int ColorRef(byte r, byte g, byte b)
        {
            return r | (g << 8) | (b << 16);
        }

        private List<FileInfo> allFiles = new();
        public MainWindow(string? startingPath=null)
        {
            InitializeComponent();
            SourceInitialized += MainWindow_SourceInitialized;
            
            if (!string.IsNullOrWhiteSpace(startingPath))
            {
                PathTextBox.Text = startingPath;
            }
        }
        private void MainWindow_SourceInitialized(object? sender, EventArgs e)
        {
            IntPtr hwnd = new WindowInteropHelper(this).Handle;
            
            // Windows 10 20H1+ / Windows 11
            const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
            const int DWMWA_CAPTION_COLOR = 35;
            const int DWMWA_TEXT_COLOR = 36;

            int enabled = 1;
            DwmSetWindowAttribute(
                hwnd,
                DWMWA_USE_IMMERSIVE_DARK_MODE,
                ref enabled,
                sizeof(int));

            int captionColor = ColorRef(32, 32, 32);

            DwmSetWindowAttribute(
                hwnd,
                DWMWA_CAPTION_COLOR,
                ref captionColor,
                sizeof(int));

            // White title text
            int textColor = ColorRef(255, 255, 255);

            DwmSetWindowAttribute(
                hwnd,
                DWMWA_TEXT_COLOR,
                ref textColor,
                sizeof(int));
        }


        private void InstallRegistry_Click(object? sender, EventArgs e)
        {
            try { App.InstallContextMenu(); }
            catch (Exception except) { ErrorMessageBox(except.Message); }
            
        }

        private void RemoveRegistry_Click(object? sender, EventArgs e)
        {
            try { App.RemoveContextMenu(); }
            catch (Exception except) { ErrorMessageBox(except.Message);  }
        }

        private void BrowseButton_Click(object sender, RoutedEventArgs e)
        {
            OpenFolderDialog dialog = new OpenFolderDialog();

            if (dialog.ShowDialog() == true)
            {
                PathTextBox.Text = dialog.FolderName; 
            }
        }

        private async void PathTextBox_TextChanged(object sender, RoutedEventArgs e)
        {
            SearchTextBox.Text = "";
            await LoadFilesAsync(PathTextBox.Text);
        }
        
        private async void SearchTextBox_TextChanged(object sender, RoutedEventArgs e)
        {
            string search = SearchTextBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(search))
            {
                _filterCancellation?.Cancel();
                FileList.ItemsSource = allFiles;
                StatusText.Text = $"{allFiles.Count:N0} files found.";
                return;
            }

            await FilterFilesAsync(allFiles, search);
        }

        private void FileList_MouseDoubleClick(object sender, RoutedEventArgs e)
        {
            if (FileList.SelectedItem is not FileInfo file)
                return;

            OpenFile(file);
        }

        private async void FileList_Sorting(object sender, DataGridSortingEventArgs e)
        {
            e.Handled = true;

            string property = e.Column.SortMemberPath;
            if (string.IsNullOrWhiteSpace(property))
                return;

            ListSortDirection direction = e.Column.SortDirection != ListSortDirection.Ascending ? ListSortDirection.Ascending : ListSortDirection.Descending;

            List<FileInfo> snapshot = FileList.ItemsSource.Cast<FileInfo>().ToList();
            SetFileListLoading(true);
            List<FileInfo> sortedFiles = await Task.Run(() =>
            {
                IEnumerable<FileInfo> sorted = property switch
                {
                    "Name" => direction == ListSortDirection.Ascending
                        ? snapshot.OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
                        : snapshot.OrderByDescending(x => x.Name, StringComparer.OrdinalIgnoreCase),

                    "Length" => direction == ListSortDirection.Ascending
                        ? snapshot.OrderBy(x => x.Length)
                        : snapshot.OrderByDescending(x => x.Length),

                    "LastWriteTime" => direction == ListSortDirection.Ascending
                        ? snapshot.OrderBy(x => x.LastWriteTime)
                        : snapshot.OrderByDescending(x => x.LastWriteTime),

                    "FullName" => direction == ListSortDirection.Ascending
                        ? snapshot.OrderBy(x => x.FullName, StringComparer.OrdinalIgnoreCase)
                        : snapshot.OrderByDescending(x => x.FullName, StringComparer.OrdinalIgnoreCase),

                    _ => snapshot
                };

                return sorted.ToList();

            });

            FileList.ItemsSource = sortedFiles;

            foreach (DataGridColumn column in FileList.Columns)
                column.SortDirection = null;

            e.Column.SortDirection = direction;
            SetFileListLoading(false);

        }

        private void FileList_Open_Click(object sender, RoutedEventArgs e)
        {
            if (FileList.SelectedItem is not FileInfo file)
                return;

            OpenFile(file);
        }
        private void FileList_OpenLocation_Click(object sender, RoutedEventArgs e)
        {
            if (FileList.SelectedItem is not FileInfo file)
                return;

            Process.Start("explorer.exe", $"/select,\"{file.FullName}\"");
        }
        private void FileList_CopyPath_Click(object sender, RoutedEventArgs e)
        {
            if (FileList.SelectedItem is not FileInfo file)
                return;

            Clipboard.SetText(file.FullName);
        }
        private void FileList_CopyRelative_Click(object sender, RoutedEventArgs e)
        {
            if (FileList.SelectedItem is not FileInfo file)
                return;
            string relativePath = file.FullName.Replace(PathTextBox.Text + "\\", "");
            Clipboard.SetText(relativePath);
        }


        private CancellationTokenSource? _filterCancellation;
        private async Task FilterFilesAsync(List<FileInfo> files, string search)
        {
            _filterCancellation?.Cancel();
            _filterCancellation = new CancellationTokenSource();
            CancellationToken token = _filterCancellation.Token;

            try
            {
                await Task.Delay(300, token);
                SetFileListLoading(true);

                List<FileInfo> filteredFiles = await Task.Run(() =>
                {
                    return files
                        .Where(file => file.Name.Contains(search, StringComparison.OrdinalIgnoreCase))
                        .ToList();
                });

                FileList.ItemsSource = filteredFiles;
                StatusText.Text = $"{filteredFiles.Count:N0} results.";
            }
            catch (OperationCanceledException) { }
            finally
            {
                SetFileListLoading(false);
            }
            
        }
        private async Task LoadFilesAsync(string path)
        {
            if (!Directory.Exists(path))
                return;

            StatusText.Text = "Loading...";
            SetFileListLoading(true);

            var files = await Task.Run(() =>
            {
                var options = new EnumerationOptions
                {
                    RecurseSubdirectories = true,
                    IgnoreInaccessible = true,
                    ReturnSpecialDirectories = false,
                    AttributesToSkip = FileAttributes.ReparsePoint
                };

                return Directory
                    .EnumerateFiles(path, "*", options)
                    .Select(file => new FileInfo(file))
                    .ToList();
            });

            allFiles = files;
            FileList.ItemsSource = allFiles;

            StatusText.Text = $"{allFiles.Count:N0} files found.";
            SetFileListLoading(false);
        }
        private void OpenFile(FileInfo file)
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = file.FullName,
                UseShellExecute = true
            });
        }
        private void ErrorMessageBox(string error)
        {
            MessageBox.Show(
                $"An error has occured:\n{error}",
                "File Search",
                MessageBoxButton.OK,
                MessageBoxImage.Error
            );
        }
        private void SetFileListEnabled(bool enabled=true)
        {
            FileList.IsEnabled = enabled;
            FileList.Opacity = enabled ? 1.0f : 0.5f;
        }
        private void SetFileListLoading(bool loading=true)
        {
            LoadingOverlay.Visibility = loading ? Visibility.Visible : Visibility.Collapsed;
            LoadingRing.Visibility = loading ? Visibility.Visible : Visibility.Collapsed;
            FileList.IsEnabled = !loading;
            FileList.Opacity = loading ? 0.5f : 1.0f;
        }
    }
}