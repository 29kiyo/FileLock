using System.Text.Json.Serialization;

namespace FileLockApp.Models
{
    /// <summary>
    /// 1つのロックグループ（パスワード・対象ファイル・各種オプションをまとめて管理）
    /// </summary>
    public class LockGroup
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = "新しいグループ";

        public string PasswordHash { get; set; } = "";
        public string PasswordSalt { get; set; } = "";

        /// <summary>子ファイル・子フォルダーまでロック対象に含めるか</summary>
        public bool IncludeSubfolders { get; set; } = true;

        /// <summary>ロックされたファイルを削除する度にパスワードを求めるか</summary>
        public bool RequirePasswordEachDelete { get; set; } = false;

        /// <summary>
        /// パスワード入力後、管理GUI上で再度パスワードを求めないグレース期間（分）。0の場合は毎回必須。
        /// ※これはGUI操作のセッション上の話で、ディスク上のロック（ACL）自体を解除するものではない。
        /// </summary>
        public int PasswordGraceMinutes { get; set; } = 0;

        public List<string> FilePaths { get; set; } = new();

        /// <summary>GUIセッション中、このグループのパスワード再入力が不要な期限（保存はしない）</summary>
        [JsonIgnore]
        public DateTime? SessionUnlockedUntil { get; set; }

        /// <summary>「一時アンロック」機能で実際にACLロックを外している期限</summary>
        public DateTime? TempUnlockUntil { get; set; }
    }
}
