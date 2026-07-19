using System.Windows;
using FileLockApp.Models;
using FileLockApp.Services;

namespace FileLockApp.Views
{
    public partial class GroupEditWindow : Window
    {
        private readonly LockGroup? _existing;
        public LockGroup? ResultGroup { get; private set; }

        /// <param name="existing">編集対象の既存グループ。新規作成の場合はnull。</param>
        /// <param name="template">テンプレートから作成する場合の設定値。</param>
        public GroupEditWindow(LockGroup? existing, GroupTemplate? template = null)
        {
            InitializeComponent();
            _existing = existing;

            if (existing != null)
            {
                NameBox.Text = existing.Name;
                IncludeSubfoldersCheck.IsChecked = existing.IncludeSubfolders;
                ConfirmEachDeleteCheck.IsChecked = existing.RequirePasswordEachDelete;
                GraceMinutesBox.Text = existing.PasswordGraceMinutes.ToString();
                PasswordLabel.Text = "新しいパスワード（変更しない場合は空欄のままでOK）";
            }
            else if (template != null)
            {
                NameBox.Text = template.Name;
                IncludeSubfoldersCheck.IsChecked = template.IncludeSubfolders;
                ConfirmEachDeleteCheck.IsChecked = template.RequirePasswordEachDelete;
                GraceMinutesBox.Text = template.PasswordGraceMinutes.ToString();
            }
            else
            {
                IncludeSubfoldersCheck.IsChecked = true;
                GraceMinutesBox.Text = "0";
            }
        }

        private void ShowPasswordCheck_Click(object sender, RoutedEventArgs e)
        {
            bool show = ShowPasswordCheck.IsChecked == true;
            if (show)
            {
                Password1Visible.Text = Password1.Password;
                Password2Visible.Text = Password2.Password;
            }
            else
            {
                Password1.Password = Password1Visible.Text;
                Password2.Password = Password2Visible.Text;
            }
            Password1.Visibility = show ? Visibility.Collapsed : Visibility.Visible;
            Password1Visible.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
            Password2.Visibility = show ? Visibility.Collapsed : Visibility.Visible;
            Password2Visible.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
        }

        private string GetPassword1() => ShowPasswordCheck.IsChecked == true ? Password1Visible.Text : Password1.Password;
        private string GetPassword2() => ShowPasswordCheck.IsChecked == true ? Password2Visible.Text : Password2.Password;

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(NameBox.Text))
            {
                MessageBox.Show(this, "グループ名を入力してください。");
                return;
            }

            if (!int.TryParse(GraceMinutesBox.Text, out int graceMinutes) || graceMinutes < 0)
            {
                MessageBox.Show(this, "時間（分）は0以上の数値で入力してください。");
                return;
            }

            var pw1 = GetPassword1();
            var pw2 = GetPassword2();

            var group = _existing ?? new LockGroup();
            bool needsNewPassword = _existing == null; // 新規作成時は必須
            bool changingPassword = !string.IsNullOrEmpty(pw1) || !string.IsNullOrEmpty(pw2);

            if (needsNewPassword || changingPassword)
            {
                if (string.IsNullOrEmpty(pw1))
                {
                    MessageBox.Show(this, "パスワードを入力してください。");
                    return;
                }
                // 入力ミスの可能性を考慮し、2つの入力が一致するか確認
                if (pw1 != pw2)
                {
                    MessageBox.Show(this, "2つのパスワードが一致しません。もう一度入力してください。");
                    return;
                }
                var (hash, salt) = PasswordService.Hash(pw1);
                group.PasswordHash = hash;
                group.PasswordSalt = salt;
            }

            group.Name = NameBox.Text.Trim();
            group.IncludeSubfolders = IncludeSubfoldersCheck.IsChecked == true;
            group.RequirePasswordEachDelete = ConfirmEachDeleteCheck.IsChecked == true;
            group.PasswordGraceMinutes = graceMinutes;

            ResultGroup = group;
            DialogResult = true;
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}
