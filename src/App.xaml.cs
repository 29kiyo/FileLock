using System.Windows;
using FileLockApp.Services;

namespace FileLockApp
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // アンインストーラーから呼ばれる: 全グループのロックをパスワードなしで解除する。
            // 「パスワードを忘れても困らないように」という要件に対応。
            if (e.Args.Contains("--unlock-all"))
            {
                var settings = SettingsStore.Load();
                foreach (var group in settings.Groups)
                {
                    foreach (var path in group.FilePaths)
                        LockService.Unlock(path, group.IncludeSubfolders);
                }
                Shutdown();
                return;
            }

            // タスクスケジューラから呼ばれる: 一時アンロックの期限切れによる自動再ロック。
            var relockIndex = Array.IndexOf(e.Args, "--relock");
            if (relockIndex >= 0 && relockIndex + 1 < e.Args.Length)
            {
                var groupId = e.Args[relockIndex + 1];
                var settings = SettingsStore.Load();
                var group = settings.Groups.FirstOrDefault(g => g.Id == groupId);
                if (group != null)
                {
                    foreach (var path in group.FilePaths)
                        LockService.Lock(path, group.IncludeSubfolders);
                    group.TempUnlockUntil = null;
                    SettingsStore.Save(settings);
                }
                Shutdown();
                return;
            }

            // 通常起動: 管理GUIを表示（パスワード不要）
            var main = new MainWindow();
            main.Show();
        }
    }
}
