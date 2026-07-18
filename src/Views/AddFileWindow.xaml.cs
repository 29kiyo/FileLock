using System.Windows;
using Microsoft.Win32;

namespace FileLockApp.Views
{
    public partial class AddFileWindow : Window
    {
        public string SelectedPath { get; private set; } = "";

        public AddFileWindow()
        {
            InitializeComponent();
        }

        private void BrowseFile_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog { Title = "ロックするファイルを選択" };
            if (dialog.ShowDialog() == true)
                PathBox.Text = dialog.FileName;
        }

        private void BrowseFolder_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFolderDialog { Title = "ロックするフォルダーを選択" };
            if (dialog.ShowDialog() == true)
                PathBox.Text = dialog.FolderName;
        }

        private void Add_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(PathBox.Text))
            {
                MessageBox.Show(this, "パスを入力するか選択してください。");
                return;
            }
            if (!System.IO.File.Exists(PathBox.Text) && !System.IO.Directory.Exists(PathBox.Text))
            {
                MessageBox.Show(this, "指定されたパスが見つかりません。");
                return;
            }
            SelectedPath = PathBox.Text.Trim();
            DialogResult = true;
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}
