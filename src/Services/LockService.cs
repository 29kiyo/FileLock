using System.Security.AccessControl;
using System.Security.Principal;
using System.IO;

namespace FileLockApp.Services
{
    /// <summary>
    /// NTFSのアクセス許可（ACL）に「拒否」ルールを追加/削除することでファイルをロックする。
    /// この方式はアプリのプロセスに依存しないため、ソフトが起動していなくてもロックが有効。
    ///
    /// 【重要な注意点】
    /// これはWindows標準のACL機構を利用した「抑止」であり、暗号化のような強固な保護ではない。
    /// ファイルの所有者は理論上いつでも「セキュリティ」タブや icacls コマンドから権限を
    /// 変更してロックを解除できてしまう（Windowsの仕様上、所有者から完全に権限変更能力を
    /// 奪うことはできない）。誤操作や第三者による簡易的な削除・閲覧の防止には有効だが、
    /// 「絶対に開けない」ことを保証するものではない、という前提でREADME等に明記すること。
    /// </summary>
    public static class LockService
    {
        private static readonly FileSystemRights DenyRights =
            FileSystemRights.Delete |
            FileSystemRights.DeleteSubdirectoriesAndFiles |
            FileSystemRights.Write |
            FileSystemRights.WriteData |
            FileSystemRights.Read |
            FileSystemRights.ReadData |
            FileSystemRights.ReadAndExecute |
            FileSystemRights.Modify;

        public static void Lock(string path, bool includeSubfolders)
        {
            if (Directory.Exists(path))
            {
                ApplyDeny(path, isDirectory: true);
                if (includeSubfolders)
                {
                    foreach (var entry in SafeEnumerate(path))
                        ApplyDeny(entry, isDirectory: Directory.Exists(entry));
                }
            }
            else if (File.Exists(path))
            {
                ApplyDeny(path, isDirectory: false);
            }
        }

        public static void Unlock(string path, bool includeSubfolders)
        {
            if (Directory.Exists(path))
            {
                RemoveDeny(path, isDirectory: true);
                if (includeSubfolders)
                {
                    foreach (var entry in SafeEnumerate(path))
                        RemoveDeny(entry, isDirectory: Directory.Exists(entry));
                }
            }
            else if (File.Exists(path))
            {
                RemoveDeny(path, isDirectory: false);
            }
        }

        private static IEnumerable<string> SafeEnumerate(string root)
        {
            var result = new List<string>();
            try
            {
                result.AddRange(Directory.GetDirectories(root, "*", SearchOption.AllDirectories));
                result.AddRange(Directory.GetFiles(root, "*", SearchOption.AllDirectories));
            }
            catch
            {
                // アクセスできない項目があってもできる範囲で続行
            }
            return result;
        }

        private static void ApplyDeny(string path, bool isDirectory)
        {
            try
            {
                var identity = WindowsIdentity.GetCurrent().User;
                if (identity == null) return;

                if (isDirectory)
                {
                    var info = new DirectoryInfo(path);
                    var security = info.GetAccessControl();
                    security.AddAccessRule(new FileSystemAccessRule(
                        identity, DenyRights, InheritanceFlags.None, PropagationFlags.None, AccessControlType.Deny));
                    info.SetAccessControl(security);
                }
                else
                {
                    var info = new FileInfo(path);
                    var security = info.GetAccessControl();
                    security.AddAccessRule(new FileSystemAccessRule(identity, DenyRights, AccessControlType.Deny));
                    info.SetAccessControl(security);
                }
            }
            catch
            {
                // 個別の項目でエラーが出ても他の項目の処理は続ける
            }
        }

        private static void RemoveDeny(string path, bool isDirectory)
        {
            try
            {
                var identity = WindowsIdentity.GetCurrent().User;
                if (identity == null) return;

                if (isDirectory)
                {
                    var info = new DirectoryInfo(path);
                    var security = info.GetAccessControl();
                    security.RemoveAccessRule(new FileSystemAccessRule(
                        identity, DenyRights, InheritanceFlags.None, PropagationFlags.None, AccessControlType.Deny));
                    info.SetAccessControl(security);
                }
                else
                {
                    var info = new FileInfo(path);
                    var security = info.GetAccessControl();
                    security.RemoveAccessRule(new FileSystemAccessRule(identity, DenyRights, AccessControlType.Deny));
                    info.SetAccessControl(security);
                }
            }
            catch
            {
                // 既に解除済み等のエラーは無視
            }
        }
    }
}
