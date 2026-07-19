namespace FileLockApp.Models
{
    /// <summary>
    /// グループ「テンプレート」。パスワードやファイルパスなど個別・機密の情報は含めず、
    /// 挙動設定のみをエクスポート/インポートできるようにする。
    /// </summary>
    public class GroupTemplate
    {
        public string Name { get; set; } = "";
        public bool IncludeSubfolders { get; set; } = true;
        public bool RequirePasswordEachDelete { get; set; } = false;
        public int PasswordGraceMinutes { get; set; } = 0;
    }
}
