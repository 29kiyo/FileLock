using System.Diagnostics;

namespace FileLockApp.Services
{
    /// <summary>
    /// 「一時アンロック」の期限が来たときに、アプリを起動していなくても
    /// 自動で再ロックされるよう、Windowsタスクスケジューラに1回限りのタスクを登録する。
    /// タスクは "FileLock.exe --relock &lt;groupId&gt;" を実行し、実行後にApp.xaml.csが
    /// 該当グループを再ロックしてすぐ終了する。
    /// </summary>
    public static class UnlockScheduler
    {
        public static void ScheduleRelock(string groupId, DateTime whenUtc)
        {
            var exePath = Environment.ProcessPath ?? "FileLock.exe";
            var local = whenUtc.ToLocalTime();
            var taskName = $"FileLock_Relock_{groupId}";
            var dateStr = local.ToString("MM/dd/yyyy");
            var timeStr = local.ToString("HH:mm");

            // 既存の同名タスクがあれば削除してから作り直す
            RunSchtasks($"/Delete /TN \"{taskName}\" /F");
            RunSchtasks($"/Create /TN \"{taskName}\" /TR \"\\\"{exePath}\\\" --relock {groupId}\" /SC ONCE /SD {dateStr} /ST {timeStr} /RL HIGHEST /F");
        }

        public static void CancelRelock(string groupId)
        {
            var taskName = $"FileLock_Relock_{groupId}";
            RunSchtasks($"/Delete /TN \"{taskName}\" /F");
        }

        private static void RunSchtasks(string args)
        {
            try
            {
                var psi = new ProcessStartInfo("schtasks.exe", args)
                {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                };
                using var p = Process.Start(psi);
                p?.WaitForExit();
            }
            catch
            {
                // タスク登録に失敗しても、アプリ起動中の手動再ロックは可能なので致命的ではない
            }
        }
    }
}
