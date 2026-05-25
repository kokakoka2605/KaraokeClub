using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using KaraokeClub.ViewModels;
using KaraokeClub.Views;
using KaraokeClub.Services;

namespace KaraokeClub
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            Loaded += (_, _) => ToastService.Register(ToastHost);
        }

        private void OpenDashboard_Click(object sender, RoutedEventArgs e)
        {
            var vm = (MainViewModel)DataContext;
            var win = new DashboardWindow(vm.Ctx);
            win.Show();
        }

        private void OpenReports_Click(object sender, RoutedEventArgs e)
        {
            var vm = (MainViewModel)DataContext;
            var win = new ReportsWindow(vm.Ctx) { Owner = this };
            win.ShowDialog();
        }

        private void Logout_Click(object sender, RoutedEventArgs e)
        {
            var conn = new ConnectionWindow();
            conn.Show();
            Close();
        }

        private void TabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.OriginalSource is not TabControl tc || DataContext is not MainViewModel vm)
                return;

            switch (tc.SelectedIndex)
            {
                case 0: vm.RolesVM.Load(); break;
                case 1: vm.MenuTypesVM.Load(); break;
                case 2: vm.KaraokeVM.Load(); break;
                case 3: vm.WorkersVM.Load(); break;
                case 4: vm.MenuItemsVM.Load(); break;
                case 5: vm.OrdersVM.Load(); break;
                case 6: vm.OrderItemsVM.Load(); break;
                case 7: vm.BillsVM.Load(); break;
                case 8: vm.AppUsersVM.Load(); break;
            }
        }

        // ─── Резервная копия ─────────────────────────────────────────────
        private void Backup_Click(object sender, RoutedEventArgs e)
        {
            var backupsDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "backups");
            Directory.CreateDirectory(backupsDir);

            var defaultName = $"KaraokeClub_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.bak";

            var dlg = new SaveFileDialog
            {
                Title = "Сохранить резервную копию",
                InitialDirectory = backupsDir,
                FileName = defaultName,
                Filter = "Backup files (*.bak)|*.bak|All files (*.*)|*.*",
                DefaultExt = "bak"
            };

            if (dlg.ShowDialog() != true) return;

            try
            {
                if (DataContext is MainViewModel vm)
                    vm.Ctx.BackupDatabase(dlg.FileName);

                MessageBox.Show("Резервная копия успешно создана!", "Бэкап",
                                MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка при создании бэкапа:\n" + ex.Message,
                                "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ─── Восстановление ──────────────────────────────────────────────
        private void Restore_Click(object sender, RoutedEventArgs e)
        {
            var backupsDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "backups");
            Directory.CreateDirectory(backupsDir);

            var dlg = new OpenFileDialog
            {
                Title = "Выбрать файл резервной копии",
                InitialDirectory = Directory.Exists(backupsDir) ? backupsDir : null,
                Filter = "Backup files (*.bak)|*.bak|All files (*.*)|*.*"
            };

            if (dlg.ShowDialog() != true) return;

            var confirm = MessageBox.Show(
                "Внимание! Восстановление перезапишет текущую базу данных.\nПродолжить?",
                "Подтверждение", MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (confirm != MessageBoxResult.Yes) return;

            try
            {
                if (DataContext is MainViewModel vm)
                    vm.Ctx.RestoreDatabase(dlg.FileName);

                MessageBox.Show("База данных успешно восстановлена!\nПерезапустите приложение.",
                                "Восстановление", MessageBoxButton.OK, MessageBoxImage.Information);
                Application.Current.Shutdown();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка при восстановлении:\n" + ex.Message,
                                "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}