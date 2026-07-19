using System.Windows;
using FileLockApp.Models;
using FileLockApp.Services;

namespace FileLockApp.Views
{
    public partial class TempUnlockWindow : Window
    {
        private readonly LockGroup _group;
        public double Hours { get; private set; }

        public TempUnlockWindow(LockGroup group)
        {
            InitializeComponent();
            _group = group;
            TitleText.Text = $"「{group.Name}」を一時的にアンロックします。指定した時間が経過すると自動的に再ロックされます（アプリを閉じていても再ロックされます）。";
        }

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            if (!double.TryParse(HoursBox.Text, out double hours) || hours <= 0)
            {
                MessageBox.Show(this, "正しい時間を入力してください。");
                return;
            }

            if (!PasswordService.Verify(PasswordInput.Password, _group.PasswordHash, _group.PasswordSalt))
            {
                MessageBox.Show(this, "パスワードが違います。", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            foreach (var path in _group.FilePaths)
                LockService.Unlock(path, _group.IncludeSubfolders);

            var until = DateTime.UtcNow.AddHours(hours);
            _group.TempUnlockUntil = until;
            UnlockScheduler.ScheduleRelock(_group.Id, until);

            Hours = hours;
            DialogResult = true;
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}
