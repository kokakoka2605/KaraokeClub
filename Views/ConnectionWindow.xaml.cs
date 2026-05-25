using System;
using System.Linq;
using System.Windows;
using KaraokeClub.Data;
using KaraokeClub.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace KaraokeClub.Views
{
    public partial class ConnectionWindow : Window
    {
        public ConnectionWindow()
        {
            InitializeComponent();
        }

        private void Connect_Click(object sender, RoutedEventArgs e)
        {
            TxtError.Visibility = Visibility.Collapsed;

            var login = TxtLogin.Text.Trim();
            var pwd = PwdApp.Password;

            if (string.IsNullOrEmpty(login))
            {
                ShowError("Введите логин.");
             
                return;
            }

            // Строка подключения к MS SQL Server
            var cs = $"Server=localhost\\SQLEXPRESS;Database=KaraokeClub;" +
                     $"Integrated Security=True;" +
                     $"TrustServerCertificate=True;";

            try
            {
                var ctx = new AppDbContext(cs);

                if (!ctx.Database.CanConnect())
                {
                    ShowError("Не удалось подключиться к базе данных. Проверьте параметры.");
                    return;
                }

                // Ищем пользователя по логину и паролю в таблице app_users
                var user = ctx.AppUsers
                    .FirstOrDefault(u => u.Username == login && u.Password == pwd);

                if (user == null)
                {
                    ShowError("Неверный логин или пароль.");
                    return;
                }

                if (user.AppRole == "admin")
                {
                    // ── Администратор ─────────────────────────────
                    var vm = new MainViewModel(ctx, isAdmin: true);
                    var main = new MainWindow { DataContext = vm };
                    main.Show();
                }
                else
                {
                    // ── Официант ──────────────────────────────────
                    var worker = ctx.Workers
                        .Include(w => w.Role)
                        .FirstOrDefault(w => w.Id == user.WorkerId);

                    if (worker == null)
                    {
                        ShowError("Аккаунт не привязан к сотруднику.\nОбратитесь к администратору.");
                        return;
                    }

                    var waiterVm = new WaiterViewModel(ctx, worker);
                    var waiter = new WaiterWindow { DataContext = waiterVm };
                    waiter.Show();
                }

                Close();
            }
            catch (Exception ex)
            {
                ShowError("Ошибка подключения:\n" + (ex.InnerException?.Message ?? ex.Message));
            }
        }

        private void ShowError(string msg)
        {
            TxtError.Text = msg;
            TxtError.Visibility = Visibility.Visible;
        }


    }
}