using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;
using FileLockApp.Models;
using FileLockApp.Services;
using FileLockApp.Views;

namespace FileLockApp
{
    public partial class MainWindow : Window
    {
        private readonly AppSettings _settings = SettingsStore.Load();
        private Point _dragStart;
        private TreeViewItem? _draggedItem;

        public MainWindow()
        {
            InitializeComponent();
            RefreshTree();
        }

        private void RefreshTree()
        {
            GroupTree.Items.Clear();
            foreach (var group in _settings.Groups)
            {
                var status = group.TempUnlockUntil.HasValue && group.TempUnlockUntil > DateTime.UtcNow
                    ? " [一時アンロック中]" : "";
                var groupItem = new TreeViewItem
                {
                    Header = $"📁 {group.Name} ({group.FilePaths.Count} 件){status}",
                    Tag = group,
                    IsExpanded = true
                };
                foreach (var path in group.FilePaths)
                    groupItem.Items.Add(new TreeViewItem { Header = path, Tag = path });

                GroupTree.Items.Add(groupItem);
            }
        }

        private LockGroup? GetSelectedGroup()
        {
            if (GroupTree.SelectedItem is TreeViewItem { Tag: LockGroup group }) return group;
            if (GroupTree.SelectedItem is TreeViewItem { Tag: string path })
                return _settings.Groups.FirstOrDefault(g => g.FilePaths.Contains(path));
            return null;
        }

        /// <summary>
        /// グループのパスワードを確認する。GUIセッション中、グレース期間内であれば再入力を求めない。
        /// </summary>
        private bool RequirePassword(LockGroup group, string title)
        {
            if (group.SessionUnlockedUntil.HasValue && group.SessionUnlockedUntil > DateTime.Now)
                return true;

            var prompt = new PasswordPromptWindow(title) { Owner = this };
            if (prompt.ShowDialog() != true) return false;

            if (!PasswordService.Verify(prompt.EnteredPassword, group.PasswordHash, group.PasswordSalt))
            {
                MessageBox.Show(this, "パスワードが違います。", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }

            // 0分の場合は「時間による自動失効」をせず、管理画面（アプリ）を閉じるまで再入力不要にする
            group.SessionUnlockedUntil = group.PasswordGraceMinutes > 0
                ? DateTime.Now.AddMinutes(group.PasswordGraceMinutes)
                : DateTime.MaxValue;

            return true;
        }

        private void AddGroupButton_Click(object sender, RoutedEventArgs e)
        {
            var editor = new GroupEditWindow(null) { Owner = this };
            if (editor.ShowDialog() == true && editor.ResultGroup != null)
            {
                _settings.Groups.Add(editor.ResultGroup);
                SettingsStore.Save(_settings);
                RefreshTree();
            }
        }

        private void EditGroupButton_Click(object sender, RoutedEventArgs e)
        {
            var group = GetSelectedGroup();
            if (group == null) { MessageBox.Show(this, "グループを選択してください。"); return; }
            if (!RequirePassword(group, $"「{group.Name}」の設定を変更するにはパスワードを入力してください")) return;

            var editor = new GroupEditWindow(group) { Owner = this };
            if (editor.ShowDialog() == true)
            {
                SettingsStore.Save(_settings);
                RefreshTree();
            }
        }

        private void DeleteGroupButton_Click(object sender, RoutedEventArgs e)
        {
            var group = GetSelectedGroup();
            if (group == null) { MessageBox.Show(this, "グループを選択してください。"); return; }
            if (!RequirePassword(group, $"「{group.Name}」を削除するにはパスワードを入力してください")) return;

            foreach (var path in group.FilePaths)
                LockService.Unlock(path, group.IncludeSubfolders);

            UnlockScheduler.CancelRelock(group.Id);
            _settings.Groups.Remove(group);
            SettingsStore.Save(_settings);
            RefreshTree();
        }

        private void OpenFileButton_Click(object sender, RoutedEventArgs e)
        {
            if (GroupTree.SelectedItem is not TreeViewItem { Tag: string path })
            {
                MessageBox.Show(this, "開くファイルを選択してください。");
                return;
            }
            OpenLockedFile(path);
        }

        private void GroupTree_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            var item = FindAncestor<TreeViewItem>((DependencyObject)e.OriginalSource);
            if (item?.Tag is string path)
                OpenLockedFile(path);
        }

        /// <summary>
        /// ロック中のファイルをパスワード確認のうえ一時的に解除し、既定のアプリで開く。
        /// 開いたアプリを閉じると自動的に再ロックされる。
        /// </summary>
        private void OpenLockedFile(string path)
        {
            var group = _settings.Groups.FirstOrDefault(g => g.FilePaths.Contains(path));
            if (group == null) return;

            if (!RequirePassword(group, $"「{path}」を開くにはパスワードを入力してください")) return;

            LockService.Unlock(path, group.IncludeSubfolders);
            RefreshTree();

            try
            {
                var psi = new ProcessStartInfo(path) { UseShellExecute = true };
                var process = Process.Start(psi);

                if (process != null && !process.HasExited)
                {
                    process.EnableRaisingEvents = true;
                    process.Exited += (_, _) =>
                    {
                        // 開いたアプリが閉じられたタイミングで自動的に再ロックする
                        Dispatcher.Invoke(() =>
                        {
                            LockService.Lock(path, group.IncludeSubfolders);
                            RefreshTree();
                        });
                    };
                }
                else
                {
                    // 既に起動している別インスタンスに委譲された等でプロセスの終了を検知できない場合、
                    // 保険として一定時間後に自動で再ロックする
                    var until = DateTime.UtcNow.AddHours(1);
                    UnlockScheduler.ScheduleRelock(group.Id, until);
                    MessageBox.Show(this,
                        "ファイルを開きました。既に起動しているアプリで開かれた可能性があり、閉じたタイミングを検知できないため、\n" +
                        "安全のため1時間後に自動で再ロックされます（グループ全体が対象になります）。");
                }
            }
            catch (Exception ex)
            {
                // 開けなかった場合はロックを元に戻す
                LockService.Lock(path, group.IncludeSubfolders);
                RefreshTree();
                MessageBox.Show(this, $"ファイルを開けませんでした: {ex.Message}");
            }

            SettingsStore.Save(_settings);
        }

        private void AddFileButton_Click(object sender, RoutedEventArgs e)
        {
            var group = GetSelectedGroup();
            if (group == null) { MessageBox.Show(this, "追加先のグループを選択してください。"); return; }

            var addWindow = new AddFileWindow { Owner = this };
            if (addWindow.ShowDialog() == true && !string.IsNullOrWhiteSpace(addWindow.SelectedPath))
            {
                if (!group.FilePaths.Contains(addWindow.SelectedPath))
                {
                    group.FilePaths.Add(addWindow.SelectedPath);
                    LockService.Lock(addWindow.SelectedPath, group.IncludeSubfolders);
                    SettingsStore.Save(_settings);
                    RefreshTree();
                }
            }
        }

        private void RemoveFileButton_Click(object sender, RoutedEventArgs e)
        {
            if (GroupTree.SelectedItem is not TreeViewItem { Tag: string path })
            {
                MessageBox.Show(this, "削除するファイルを選択してください。");
                return;
            }
            var group = _settings.Groups.FirstOrDefault(g => g.FilePaths.Contains(path));
            if (group == null) return;

            if (group.RequirePasswordEachDelete)
            {
                // このオプションが有効な場合は、GUIのグレース期間を無視して毎回パスワードを求める
                var prompt = new PasswordPromptWindow($"「{path}」をロック解除・削除するにはパスワードを入力してください") { Owner = this };
                if (prompt.ShowDialog() != true) return;
                if (!PasswordService.Verify(prompt.EnteredPassword, group.PasswordHash, group.PasswordSalt))
                {
                    MessageBox.Show(this, "パスワードが違います。", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
            }
            else if (!RequirePassword(group, $"「{path}」をロック対象から外すにはパスワードを入力してください"))
            {
                return;
            }

            LockService.Unlock(path, group.IncludeSubfolders);
            group.FilePaths.Remove(path);
            SettingsStore.Save(_settings);
            RefreshTree();
        }

        private void TempUnlockButton_Click(object sender, RoutedEventArgs e)
        {
            var group = GetSelectedGroup();
            if (group == null) { MessageBox.Show(this, "グループを選択してください。"); return; }

            var window = new TempUnlockWindow(group) { Owner = this };
            if (window.ShowDialog() == true)
            {
                SettingsStore.Save(_settings);
                RefreshTree();
                MessageBox.Show(this, $"{window.Hours} 時間、ロックを解除しました。時間経過後は自動的に再ロックされます。", "一時アンロック");
            }
        }

        private void ExportButton_Click(object sender, RoutedEventArgs e)
        {
            var group = GetSelectedGroup();
            if (group == null) { MessageBox.Show(this, "グループを選択してください。"); return; }

            var dialog = new SaveFileDialog { Filter = "FileLock テンプレート (*.flt)|*.flt", FileName = $"{group.Name}.flt" };
            if (dialog.ShowDialog() == true)
            {
                TemplateService.Export(group, dialog.FileName);
                MessageBox.Show(this, "テンプレートを出力しました。（パスワードとファイル一覧は含まれません）");
            }
        }

        private void ImportButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog { Filter = "FileLock テンプレート (*.flt)|*.flt" };
            if (dialog.ShowDialog() == true)
            {
                var template = TemplateService.Import(dialog.FileName);
                if (template == null)
                {
                    MessageBox.Show(this, "テンプレートを読み込めませんでした。");
                    return;
                }

                var editor = new GroupEditWindow(null, template) { Owner = this };
                if (editor.ShowDialog() == true && editor.ResultGroup != null)
                {
                    _settings.Groups.Add(editor.ResultGroup);
                    SettingsStore.Save(_settings);
                    RefreshTree();
                }
            }
        }

        private void UninstallButton_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(this,
                "FileLock をアンインストールしますか？\nすべてのロックがパスワードなしで解除されます。",
                "アンインストール確認", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (result != MessageBoxResult.Yes) return;

            foreach (var group in _settings.Groups)
            {
                foreach (var path in group.FilePaths)
                    LockService.Unlock(path, group.IncludeSubfolders);
                UnlockScheduler.CancelRelock(group.Id);
            }

            try
            {
                var uninstallerPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "unins000.exe");
                if (System.IO.File.Exists(uninstallerPath))
                {
                    System.Diagnostics.Process.Start(uninstallerPath, "/SILENT");
                }
                else
                {
                    MessageBox.Show(this, "アンインストーラーが見つかりませんでした。「設定 > アプリ」からアンインストールしてください。\n（ロックは既に解除済みです）");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"アンインストーラーの起動に失敗しました: {ex.Message}\n（ロックは既に解除済みです）");
            }
            Close();
        }

        // ---------- ドラッグ&ドロップ：ファイルを別グループへ移動 ----------

        private void GroupTree_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _dragStart = e.GetPosition(null);
            _draggedItem = FindAncestor<TreeViewItem>((DependencyObject)e.OriginalSource);
        }

        private void GroupTree_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed || _draggedItem == null) return;
            if (_draggedItem.Tag is not string) return; // ファイル項目のみドラッグ対象

            var diff = _dragStart - e.GetPosition(null);
            if (Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance ||
                Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance)
            {
                DragDrop.DoDragDrop(_draggedItem, _draggedItem, DragDropEffects.Move);
            }
        }

        private void GroupTree_DragOver(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
                e.Effects = DragDropEffects.Copy; // エクスプローラーからの新規ファイル追加
            else if (e.Data.GetDataPresent(typeof(TreeViewItem)))
                e.Effects = DragDropEffects.Move; // グループ間でのファイル移動
            else
                e.Effects = DragDropEffects.None;
            e.Handled = true;
        }

        private void GroupTree_Drop(object sender, DragEventArgs e)
        {
            var targetItem = FindAncestor<TreeViewItem>((DependencyObject)e.OriginalSource);
            var targetGroup = targetItem?.Tag as LockGroup
                ?? (targetItem?.Tag is string tPath ? _settings.Groups.FirstOrDefault(g => g.FilePaths.Contains(tPath)) : null)
                ?? GetSelectedGroup(); // ドロップ位置にグループが無い場合は現在選択中のグループにフォールバック

            // --- エクスプローラーなど外部からのファイルドロップ: 選んだグループへ追加(パスワード不要) ---
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                if (targetGroup == null)
                {
                    MessageBox.Show(this, "追加先のグループの上にドロップするか、事前にグループを選択してください。");
                    return;
                }

                var droppedPaths = (string[])e.Data.GetData(DataFormats.FileDrop)!;
                int added = 0;
                foreach (var p in droppedPaths)
                {
                    if (targetGroup.FilePaths.Contains(p)) continue;
                    targetGroup.FilePaths.Add(p);
                    LockService.Lock(p, targetGroup.IncludeSubfolders);
                    added++;
                }

                if (added > 0)
                {
                    SettingsStore.Save(_settings);
                    RefreshTree();
                }
                return;
            }

            // --- グループ間でのファイル移動(内部ドラッグ): 移動元グループのパスワードが必要 ---
            if (!e.Data.GetDataPresent(typeof(TreeViewItem))) return;
            var sourceItem = (TreeViewItem)e.Data.GetData(typeof(TreeViewItem))!;
            if (sourceItem.Tag is not string path) return;
            if (targetGroup == null) return;

            var sourceGroup = _settings.Groups.FirstOrDefault(g => g.FilePaths.Contains(path));
            if (sourceGroup == null || ReferenceEquals(sourceGroup, targetGroup)) return;

            // 移動前（移動元）グループのパスワードを要求
            var prompt = new PasswordPromptWindow($"「{sourceGroup.Name}」からファイルを移動するにはパスワードを入力してください") { Owner = this };
            if (prompt.ShowDialog() != true) return;
            if (!PasswordService.Verify(prompt.EnteredPassword, sourceGroup.PasswordHash, sourceGroup.PasswordSalt))
            {
                MessageBox.Show(this, "パスワードが違います。", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            LockService.Unlock(path, sourceGroup.IncludeSubfolders);
            sourceGroup.FilePaths.Remove(path);
            targetGroup.FilePaths.Add(path);
            LockService.Lock(path, targetGroup.IncludeSubfolders);

            SettingsStore.Save(_settings);
            RefreshTree();
        }

        private static T? FindAncestor<T>(DependencyObject current) where T : DependencyObject
        {
            while (current != null)
            {
                if (current is T match) return match;
                current = VisualTreeHelper.GetParent(current);
            }
            return null;
        }
    }
}
