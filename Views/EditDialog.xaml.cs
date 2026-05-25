using KaraokeClub.Data;
using KaraokeClub.Models;
using KaraokeClub.ViewModels;
using Microsoft.Win32;
using System;
using System.IO;
using System.Linq;
using System.Windows;

namespace KaraokeClub.Views
{
    public partial class EditDialog : Window
    {
        private readonly AppDbContext _ctx;
        private readonly bool _isNew;

        // Папка внутри выходного каталога, куда копируются картинки блюд.
        // Относительный путь вида "Images\filename.jpg" сохраняется в БД.
        private static readonly string ImagesFolder =
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Images");

        public EditDialog(object dataContext, AppDbContext ctx, bool isNew)
        {
            InitializeComponent();
            DataContext = dataContext;
            _ctx = ctx;
            _isNew = isNew;

            if (isNew)
            {
                Title = "Добавление";
                HeaderText.Text = "＋  Добавление записи";
            }
        }

        // ─── Выбор картинки ───────────────────────────────────────────
        private void PickImage_Click(object sender, RoutedEventArgs e)
        {
            var vm = DataContext as MenuItemEditorViewModel;
            if (vm == null) return;

            var dlg = new OpenFileDialog
            {
                Title = "Выберите изображение",
                Filter = "Изображения|*.jpg;*.jpeg;*.png;*.bmp;*.gif;*.webp|Все файлы|*.*",
                Multiselect = false
            };

            if (dlg.ShowDialog() != true) return;

            try
            {
                // Создаём папку Images рядом с .exe, если её нет
                Directory.CreateDirectory(ImagesFolder);

                // Копируем файл в Images\ (не перезаписываем если уже есть такой)
                var fileName = Path.GetFileName(dlg.FileName);
                var destPath = Path.Combine(ImagesFolder, fileName);

                // Если файл с таким именем уже есть — добавляем уникальный суффикс
                if (File.Exists(destPath) && !IsSameFile(dlg.FileName, destPath))
                {
                    var nameWithout = Path.GetFileNameWithoutExtension(fileName);
                    var ext = Path.GetExtension(fileName);
                    fileName = $"{nameWithout}_{DateTime.Now:yyyyMMddHHmmss}{ext}";
                    destPath = Path.Combine(ImagesFolder, fileName);
                }

                if (!File.Exists(destPath))
                    File.Copy(dlg.FileName, destPath);

                // Сохраняем относительный путь: Images\filename.jpg
                vm.Entity.ImagePath = Path.Combine("Images", fileName);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Не удалось скопировать изображение:\n" + ex.Message,
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ─── Очистить картинку ────────────────────────────────────────
        private void ClearImage_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is MenuItemEditorViewModel vm)
                vm.Entity.ImagePath = null;
        }

        // ─── Вспомогательный метод: одинаковый ли это файл ───────────
        private static bool IsSameFile(string a, string b)
        {
            try
            {
                var infoA = new FileInfo(a);
                var infoB = new FileInfo(b);
                return infoA.Length == infoB.Length &&
                       string.Equals(infoA.FullName, infoB.FullName, StringComparison.OrdinalIgnoreCase);
            }
            catch { return false; }
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            // Извлекаем реальную сущность из обёртки
            var entity = DataContext switch
            {
                WorkerEditorViewModel vm => (object)vm.Entity,
                MenuItemEditorViewModel vm => vm.Entity,
                OrderEditorViewModel vm => vm.Entity,
                OrderItemEditorViewModel vm => vm.Entity,
                BillEditorViewModel vm => vm.Entity,
                AppUserEditorViewModel vm => vm.Entity,
                _ => DataContext
            };

            var error = Validate(entity);
            if (error != null)
            {
                MessageBox.Show(error, "Ошибка валидации",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            DialogResult = true;
        }

        // ─── Валидация ────────────────────────────────────────────────
        private static string? Validate(object entity) => entity switch
        {
            Role r => ValidateRole(r),
            MenuType t => ValidateMenuType(t),
            KaraokeOption k => ValidateKaraokeOption(k),
            Worker w => ValidateWorker(w),
            MenuItem m => ValidateMenuItem(m),
            Order o => ValidateOrder(o),
            OrderItem oi => ValidateOrderItem(oi),
            Bill b => ValidateBill(b),
            AppUser au => ValidateAppUser(au),
            _ => null
        };

        private static string? ValidateRole(Role r)
        {
            if (string.IsNullOrWhiteSpace(r.Name)) return "Введите название роли.";
            if (r.Name.Length > 100) return "Название роли не должно превышать 100 символов.";
            if (r.Salary < 0) return "Зарплата не может быть отрицательной.";
            if (r.Salary > 1_000_000) return "Зарплата не может превышать 1 000 000.";
            return null;
        }

        private static string? ValidateMenuType(MenuType t)
        {
            if (string.IsNullOrWhiteSpace(t.Name)) return "Введите название типа.";
            if (t.Name.Length > 100) return "Название типа не должно превышать 100 символов.";
            return null;
        }

        private static string? ValidateKaraokeOption(KaraokeOption k)
        {
            if (string.IsNullOrWhiteSpace(k.Name)) return "Введите название опции.";
            if (k.Name.Length > 150) return "Название опции не должно превышать 150 символов.";
            if (k.Price < 0) return "Цена не может быть отрицательной.";
            if (k.Price > 100_000) return "Цена не может превышать 100 000.";
            // Trim leading/trailing whitespace from description before saving
            if (k.Description != null)
                k.Description = k.Description.Trim();
            if (string.IsNullOrEmpty(k.Description))
                k.Description = null;
            return null;
        }

        private static string? ValidateWorker(Worker w)
        {
            if (string.IsNullOrWhiteSpace(w.Name)) return "Введите имя сотрудника.";
            if (w.Name.Length < 2) return "Имя должно содержать не менее 2 символов.";
            if (w.Name.Length > 150) return "Имя не должно превышать 150 символов.";
            if (w.RoleId == 0) return "Выберите роль.";

            if (!string.IsNullOrEmpty(w.Idnp))
            {
                if (w.Idnp.Length != 13) return "IDNP должен содержать ровно 13 цифр.";
                foreach (var c in w.Idnp)
                    if (!char.IsDigit(c)) return "IDNP должен содержать только цифры.";
            }

            if (!string.IsNullOrEmpty(w.Phone))
            {
                // Допустимый формат: +373xxxxxxxx, 0xxxxxxxx или просто цифры (7–15 знаков)
                var digits = new string(w.Phone.Where(char.IsDigit).ToArray());
                if (digits.Length < 7 || digits.Length > 15)
                    return "Телефон должен содержать от 7 до 15 цифр.";
            }

            if (w.Birth.HasValue)
            {
                var age = DateTime.Today.Year - w.Birth.Value.Year;
                if (w.Birth.Value.Date > DateTime.Today.AddYears(-age)) age--;
                if (age < 16) return "Сотрудник должен быть старше 16 лет.";
                if (age > 100) return "Проверьте дату рождения — возраст превышает 100 лет.";
            }

            return null;
        }

        private static string? ValidateMenuItem(MenuItem m)
        {
            if (string.IsNullOrWhiteSpace(m.Name)) return "Введите название позиции меню.";
            if (m.Name.Length > 200) return "Название не должно превышать 200 символов.";
            if (m.Price <= 0) return "Цена должна быть больше нуля.";
            if (m.Price > 100_000) return "Цена не может превышать 100 000.";
            if (m.TypeId == 0) return "Выберите тип меню.";
            if (string.IsNullOrWhiteSpace(m.Section)) return "Введите секцию (kitchen / bar).";
            if (m.Section != "kitchen" && m.Section != "bar") return "Секция должна быть «kitchen» или «bar».";
            if (string.IsNullOrWhiteSpace(m.WeightVolume)) return "Введите вес / объём.";
            if (m.WeightVolume.Length > 50) return "Вес/объём не должен превышать 50 символов.";
            if (string.IsNullOrWhiteSpace(m.Ingredients)) return "Введите состав.";
            if (m.CookingTime.HasValue && m.CookingTime.Value < 0) return "Время приготовления не может быть отрицательным.";
            if (m.CookingTime.HasValue && m.CookingTime.Value > 600) return "Время приготовления не может превышать 600 минут.";
            return null;
        }

        private static string? ValidateOrder(Order o)
        {
            if (o.TableNumber <= 0) return "Номер стола должен быть больше нуля.";
            if (o.TableNumber > 500) return "Номер стола не может превышать 500.";
            if (o.WorkerId == 0) return "Выберите сотрудника.";
            if (string.IsNullOrWhiteSpace(o.Status)) return "Выберите статус заказа.";
            if (o.GuestCount.HasValue && o.GuestCount.Value < 1) return "Количество гостей должно быть не менее 1.";
            if (o.GuestCount.HasValue && o.GuestCount.Value > 1000) return "Количество гостей не может превышать 1000.";
            return null;
        }

        private static string? ValidateOrderItem(OrderItem oi)
        {
            if (oi.OrderId == 0) return "Выберите заказ.";
            if (string.IsNullOrWhiteSpace(oi.ItemType)) return "Выберите тип позиции.";
            if (oi.ItemType == "product" && oi.ProductId == null) return "Выберите позицию меню.";
            if (oi.ItemType == "karaoke" && oi.OptionId == null) return "Выберите опцию karaoke.";
            if (oi.Quantity <= 0) return "Количество должно быть больше нуля.";
            if (oi.Quantity > 9999) return "Количество не может превышать 9999.";
            return null;
        }

        private static string? ValidateBill(Bill b)
        {
            if (b.OrderId == 0) return "Выберите заказ.";
            if (string.IsNullOrWhiteSpace(b.BillStatus)) return "Выберите статус счёта.";
            if (b.BillStatus == "paid" && string.IsNullOrWhiteSpace(b.PaymentMethod))
                return "При статусе «paid» необходимо указать способ оплаты.";
            if (b.Deposit.HasValue && b.Deposit.Value < 0) return "Депозит не может быть отрицательным.";
            if (b.TotalAmount.HasValue && b.TotalAmount.Value < 0) return "Сумма не может быть отрицательной.";
            if (b.Deposit.HasValue && b.TotalAmount.HasValue && b.Deposit.Value > b.TotalAmount.Value)
                return "Депозит не может превышать итоговую сумму.";
            return null;
        }

        private static string? ValidateAppUser(AppUser au)
        {
            if (string.IsNullOrWhiteSpace(au.Username)) return "Введите логин.";
            if (au.Username.Length < 3) return "Логин должен быть не менее 3 символов.";
            if (au.Username.Length > 50) return "Логин не должен превышать 50 символов.";
            if (au.Username.Any(char.IsWhiteSpace)) return "Логин не должен содержать пробелы.";
            if (string.IsNullOrWhiteSpace(au.Password)) return "Введите пароль.";
            if (au.Password.Length < 4) return "Пароль должен быть не менее 4 символов.";
            if (au.Password.Length > 100) return "Пароль не должен превышать 100 символов.";
            if (string.IsNullOrWhiteSpace(au.AppRole)) return "Выберите роль.";
            return null;
        }

        // ─── Только цифры (IDNP, количество) ─────────────────────
        private void OnlyNumbers_PreviewTextInput(object sender, System.Windows.Input.TextCompositionEventArgs e)
        {
            e.Handled = !e.Text.All(char.IsDigit);
        }

        // ─── Телефон: цифры, +, -, пробел ────────────────────────
        private void Phone_PreviewTextInput(object sender, System.Windows.Input.TextCompositionEventArgs e)
        {
            e.Handled = !e.Text.All(c => char.IsDigit(c) || c == '+' || c == '-' || c == ' ');
        }

        private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;

        // ─── Запрет пробелов (логин, пароль) ─────────────────────────
        private void NoSpaces_PreviewTextInput(object sender, System.Windows.Input.TextCompositionEventArgs e)
        {
            e.Handled = e.Text.Contains(' ');
        }

        // ─── Только буквы (название роли) ────────────────────────────
        private void OnlyLetters_PreviewTextInput(object sender, System.Windows.Input.TextCompositionEventArgs e)
        {
            e.Handled = !e.Text.All(c => char.IsLetter(c) || char.IsWhiteSpace(c));
        }

        // ─── Только цифры и точка (зарплата) ─────────────────────────
        private void OnlyDecimal_PreviewTextInput(object sender, System.Windows.Input.TextCompositionEventArgs e)
        {
            var tb = sender as System.Windows.Controls.TextBox;
            var current = tb?.Text ?? "";
            if (e.Text == "." || e.Text == ",")
            {
                e.Handled = current.Contains('.') || current.Contains(',');
                return;
            }
            e.Handled = !e.Text.All(c => char.IsDigit(c));
        }
    }
}
