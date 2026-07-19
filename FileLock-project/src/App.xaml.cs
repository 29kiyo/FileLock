using System.IO;
using System.Windows;
using System.Windows.Threading;
using FileLockApp.Services;

namespace FileLockApp
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // 未処理の例外で無言のままクラッシュしないよう、ログに残してから通知する
            DispatcherUnhandledException += OnDispatcherUnhandledException;
            AppDomain.CurrentDomain.UnhandledException += OnAppDomainUnhandledException;

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

        private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            LogError(e.Exception);
            MessageBox.Show(
                $"予期しないエラーが発生しました。アプリは続行しますが、操作をやり直してください。\n\n{e.Exception.Message}\n\n詳細は %AppData%\\FileLock\\error.log に記録されました。",
                "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
            e.Handled = true; // ここで処理済みにして、アプリが強制終了するのを防ぐ
        }

        private void OnAppDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            if (e.ExceptionObject is Exception ex)
                LogError(ex);
        }

        private static void LogError(Exception ex)
        {
            try
            {
                var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "FileLock");
                Directory.CreateDirectory(dir);
                var logPath = Path.Combine(dir, "error.log");
                File.AppendAllText(logPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]\n{ex}\n\n");
            }
            catch
            {
                // ログ出力自体に失敗しても何もできることはない
            }
        }
    }
}
