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

        /// <summary>
        /// Кнопка «👁 Позиции» в табе Заказы.
        /// Открывает диалог просмотра/редактирования позиций выбранного заказа.
        /// После закрытия диалога:
        ///   - пересчитывает сумму чека (через триггер БД — автоматически);
        ///   - если все позиции были удалены и заказ уничтожен — обновляет таблицу заказов и счетов;
        ///   - иначе — только обновляет таблицу позиций и счетов.
        /// </summary>
        private void ViewOrderItems_Click(object sender, RoutedEventArgs e)
        {
            // Получаем выбранный заказ из DataGrid
            if (OrdersGrid.SelectedItem is not KaraokeClub.Models.Order order)
            {
                MessageBox.Show("Выберите заказ в таблице.",
                    "Нет выбранного заказа", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var vm = (MainViewModel)DataContext;

            var dlg = new KaraokeClub.Views.OrderItemsDialog(
                vm.Ctx,
                order,
                vm.MenuItems,
                vm.KaraokeOptions,
                vm.Orders)
            {
                Owner = this
            };

            dlg.ShowDialog();

            if (!dlg.HasChanges) return;

            if (dlg.OrderWasDeleted)
            {
                // Заказ удалён — обновляем обе таблицы
                vm.OrdersVM.Load();
                vm.OrderItemsVM.Load();
                vm.BillsVM.Load();
                ToastService.Show($"Заказ #{order.Id} и его чек удалены (все позиции были удалены)",
                    KaraokeClub.Services.ToastKind.Warning);
            }
            else
            {
                // Только изменились позиции → пересчёт суммы чека произошёл триггером в БД
                vm.OrderItemsVM.Load();
                vm.BillsVM.Load();
            }
        }

        /// <summary>
        /// Обработчик смены выделения в таблице заказов.
        /// Включает/выключает кнопку «👁 Позиции» в зависимости от того,
        /// выбрана ли строка.
        /// </summary>
        private void OrdersGrid_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            // Ищем кнопку «Позиции» в панели над таблицей
            // (она находится в StackPanel рядом с кнопками Add/Edit/Delete)
            if (sender is not System.Windows.Controls.DataGrid dg) return;

            bool hasSelection = dg.SelectedItem != null;

            // Обходим визуальное дерево чтобы найти кнопку по тегу
            // Альтернатива: дать кнопке x:Name="BtnViewOrderItems" в XAML
            // и обращаться напрямую. Если вы добавили x:Name — используйте:
            // BtnViewOrderItems.IsEnabled = hasSelection;
            //
            // Если x:Name не добавлен — находим через логическое дерево:
            foreach (var btn in FindVisualChildren<System.Windows.Controls.Button>(this))
            {
                if (btn.Content?.ToString() == "👁 Позиции")
                {
                    btn.IsEnabled = hasSelection;
                    break;
                }
            }
        }

        /// <summary>Вспомогательный метод обхода визуального дерева.</summary>
        private static System.Collections.Generic.IEnumerable<T> FindVisualChildren<T>(
            System.Windows.DependencyObject depObj) where T : System.Windows.DependencyObject
        {
            if (depObj == null) yield break;
            int count = System.Windows.Media.VisualTreeHelper.GetChildrenCount(depObj);
            for (int i = 0; i < count; i++)
            {
                var child = System.Windows.Media.VisualTreeHelper.GetChild(depObj, i);
                if (child is T t) yield return t;
                foreach (var childOfChild in FindVisualChildren<T>(child))
                    yield return childOfChild;
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