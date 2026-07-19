using System.Windows;

namespace FileLockApp.Views
{
    public partial class PasswordPromptWindow : Window
    {
        public string EnteredPassword { get; private set; } = "";
        private bool _syncing;

        public PasswordPromptWindow(string message)
        {
            InitializeComponent();
            MessageText.Text = message;
        }

        private void PasswordInput_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (_syncing) return;
            _syncing = true;
            PasswordVisibleInput.Text = PasswordInput.Password;
            _syncing = false;
        }

        private void PasswordVisibleInput_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            if (_syncing) return;
            _syncing = true;
            PasswordInput.Password = PasswordVisibleInput.Text;
            _syncing = false;
        }

        private void ShowPasswordCheck_Click(object sender, RoutedEventArgs e)
        {
            bool show = ShowPasswordCheck.IsChecked == true;
            PasswordVisibleInput.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
            PasswordInput.Visibility = show ? Visibility.Collapsed : Visibility.Visible;
        }

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            EnteredPassword = PasswordInput.Password;
            DialogResult = true;
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}
