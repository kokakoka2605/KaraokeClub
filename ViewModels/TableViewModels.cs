using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Data;
using KaraokeClub.Commands;
using KaraokeClub.Data;
using KaraokeClub.Models;
using KaraokeClub.Services;
using KaraokeClub.Views;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace KaraokeClub.ViewModels
{
    // ══════════════════════════════════════════════════════════════
    // ROLES
    // ══════════════════════════════════════════════════════════════
    public class RolesViewModel : BaseViewModel
    {
        private readonly AppDbContext _ctx;
        private readonly ObservableCollection<Role> _items = new();
        public ICollectionView View { get; }

        // Публичный доступ к коллекции нужен MainViewModel для подписки
        public ObservableCollection<Role> Items => _items;

        private string _search = "";
        public string SearchText
        {
            get => _search;
            set { SetProperty(ref _search, value); View.Refresh(); }
        }

        public RelayCommand AddCommand { get; }
        public RelayCommand EditCommand { get; }
        public RelayCommand DeleteCommand { get; }
        public RelayCommand ReloadCommand { get; }

        // Fired after Add/Delete — нужен снаружи для синхронизации зависимых вкладок
        public event Action? ItemsChanged;

        public RolesViewModel(AppDbContext ctx)
        {
            _ctx = ctx;
            View = CollectionViewSource.GetDefaultView(_items);
            View.Filter = o => string.IsNullOrWhiteSpace(_search) || (o is Role r &&
                (r.Name.ToLower().Contains(_search.ToLower()) ||
                 r.Salary.ToString().Contains(_search) ||
                 r.Id.ToString().Contains(_search)));

            AddCommand = new RelayCommand(_ => Add());
            EditCommand = new RelayCommand(o => Edit(o as Role), o => o is Role);
            DeleteCommand = new RelayCommand(o => Delete(o as Role), o => o is Role);
            ReloadCommand = new RelayCommand(_ => Load());
            Load();
        }

        public void Load()
        {
            _items.Clear();
            foreach (var r in _ctx.Roles.OrderBy(x => x.Id).ToList()) _items.Add(r);
            View.Refresh();
        }

        private void Add()
        {
            var entity = new Role();
            var dlg = new EditDialog(entity, _ctx, true) { Owner = Application.Current.MainWindow };
            if (dlg.ShowDialog() != true) return;
            try
            {
                _ctx.ExecProc("usp_Insert_Role",
                    new SqlParameter("@name_role", entity.Name),
                    new SqlParameter("@salary", entity.Salary));
                entity.Id = _ctx.Roles.OrderByDescending(x => x.Id).First().Id;
                _items.Add(entity);
                View.Refresh();
                ItemsChanged?.Invoke();
                ToastService.Show($"Роль «{entity.Name}» успешно добавлена");
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.InnerException?.Message ?? ex.Message,
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Edit(Role? r)
        {
            if (r == null) return;

            // Редактируем копию, чтобы не менять UI до подтверждения
            var copy = new Role { Id = r.Id, Name = r.Name, Salary = r.Salary };
            var dlg = new EditDialog(copy, _ctx, false) { Owner = Application.Current.MainWindow };
            if (dlg.ShowDialog() != true) return;
            try
            {
                _ctx.ExecProc("usp_Update_Role",
                    new SqlParameter("@id_role", copy.Id),
                    new SqlParameter("@name_role", copy.Name),
                    new SqlParameter("@salary", copy.Salary));

                // ★ Обновляем свойства ОРИГИНАЛЬНОГО объекта на месте.
                // Так как Role реализует INotifyPropertyChanged через NotifyBase,
                // все DataGrid-ячейки и ComboBox-элементы, ссылающиеся на этот
                // же экземпляр, обновятся автоматически без перезагрузки.
                r.Name = copy.Name;
                r.Salary = copy.Salary;

                View.Refresh();
                ToastService.Show($"Роль «{r.Name}» успешно изменена");
                // ItemsChanged НЕ нужен
                // на тот же объект Role и видят изменение через INotifyPropertyChanged
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.InnerException?.Message ?? ex.Message,
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Delete(Role? r)
        {
            if (r == null) return;

            // Проверка: нельзя удалить роль, если к ней привязаны сотрудники
            var workerCount = _ctx.Workers.Count(w => w.RoleId == r.Id);
            if (workerCount > 0)
            {
                MessageBox.Show(
                    $"Невозможно удалить роль «{r.Name}»:\nк ней привязано сотрудников: {workerCount}.\nСначала переназначьте или удалите этих сотрудников.",
                    "Удаление запрещено", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (MessageBox.Show($"Удалить роль «{r.Name}»?", "Подтверждение",
                    MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
            try
            {
                _ctx.ExecProc("usp_Delete_Role", new SqlParameter("@id_role", r.Id));
                _items.Remove(r);
                View.Refresh();
                ItemsChanged?.Invoke();
                ToastService.Show($"Роль «{r.Name}» успешно удалена");
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.InnerException?.Message ?? ex.Message,
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    // ══════════════════════════════════════════════════════════════
    // MENU TYPES
    // ══════════════════════════════════════════════════════════════
    public class MenuTypesViewModel : BaseViewModel
    {
        private readonly AppDbContext _ctx;
        private readonly ObservableCollection<MenuType> _items = new();
        public ICollectionView View { get; }
        public ObservableCollection<MenuType> Items => _items;

        private string _search = "";
        public string SearchText
        {
            get => _search;
            set { SetProperty(ref _search, value); View.Refresh(); }
        }

        public RelayCommand AddCommand { get; }
        public RelayCommand EditCommand { get; }
        public RelayCommand DeleteCommand { get; }
        public RelayCommand ReloadCommand { get; }
        public event Action? ItemsChanged;

        public MenuTypesViewModel(AppDbContext ctx)
        {
            _ctx = ctx;
            View = CollectionViewSource.GetDefaultView(_items);
            View.Filter = o => string.IsNullOrWhiteSpace(_search) || (o is MenuType t &&
                (t.Name.ToLower().Contains(_search.ToLower()) ||
                 t.Id.ToString().Contains(_search)));

            AddCommand = new RelayCommand(_ => Add());
            EditCommand = new RelayCommand(o => Edit(o as MenuType), o => o is MenuType);
            DeleteCommand = new RelayCommand(o => Delete(o as MenuType), o => o is MenuType);
            ReloadCommand = new RelayCommand(_ => Load());
            Load();
        }

        public void Load()
        {
            _items.Clear();
            foreach (var t in _ctx.MenuTypes.OrderBy(x => x.Id).ToList()) _items.Add(t);
            View.Refresh();
        }

        private void Add()
        {
            var entity = new MenuType();
            var dlg = new EditDialog(entity, _ctx, true) { Owner = Application.Current.MainWindow };
            if (dlg.ShowDialog() != true) return;
            try
            {
                _ctx.ExecProc("usp_Insert_Type",
                    new SqlParameter("@name_type", entity.Name));
                entity.Id = _ctx.MenuTypes.OrderByDescending(x => x.Id).First().Id;
                _items.Add(entity);
                View.Refresh();
                ItemsChanged?.Invoke();
                ToastService.Show($"Тип «{entity.Name}» успешно добавлен");
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.InnerException?.Message ?? ex.Message,
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Edit(MenuType? t)
        {
            if (t == null) return;
            var copy = new MenuType { Id = t.Id, Name = t.Name };
            var dlg = new EditDialog(copy, _ctx, false) { Owner = Application.Current.MainWindow };
            if (dlg.ShowDialog() != true) return;
            try
            {
                _ctx.ExecProc("usp_Update_Type",
                    new SqlParameter("@id_type", copy.Id),
                    new SqlParameter("@name_type", copy.Name));

                // ★ Обновляем оригинальный объект на месте.
                // MenuItem.Type ссылается на этот же экземпляр MenuType,
                // поэтому колонка "Тип" в таблице меню обновится мгновенно.
                t.Name = copy.Name;

                View.Refresh();
                ToastService.Show($"Тип «{t.Name}» успешно изменён");
                // ItemsChanged не нужен — MenuItem.Type.Name обновится сам
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.InnerException?.Message ?? ex.Message,
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Delete(MenuType? t)
        {
            if (t == null) return;

            // Проверка: нельзя удалить тип меню, если к нему привязаны позиции меню
            var itemCount = _ctx.MenuItems.Count(m => m.TypeId == t.Id);
            if (itemCount > 0)
            {
                MessageBox.Show(
                    $"Невозможно удалить тип «{t.Name}»:\nк нему привязано позиций меню: {itemCount}.\nСначала удалите или переназначьте эти позиции.",
                    "Удаление запрещено", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (MessageBox.Show($"Удалить тип «{t.Name}»?", "Подтверждение",
                    MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
            try
            {
                _ctx.ExecProc("usp_Delete_Type", new SqlParameter("@id_type", t.Id));
                _items.Remove(t);
                View.Refresh();
                ItemsChanged?.Invoke();
                ToastService.Show($"Тип «{t.Name}» успешно удалён");
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.InnerException?.Message ?? ex.Message,
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    // ══════════════════════════════════════════════════════════════
    // KARAOKE OPTIONS
    // ══════════════════════════════════════════════════════════════
    public class KaraokeOptionsViewModel : BaseViewModel
    {
        private readonly AppDbContext _ctx;
        private readonly ObservableCollection<KaraokeOption> _items = new();
        public ICollectionView View { get; }
        public ObservableCollection<KaraokeOption> Items => _items;
        public event Action? ItemsChanged;

        private string _search = "";
        public string SearchText
        {
            get => _search;
            set { SetProperty(ref _search, value); View.Refresh(); }
        }

        public RelayCommand AddCommand { get; }
        public RelayCommand EditCommand { get; }
        public RelayCommand DeleteCommand { get; }
        public RelayCommand ReloadCommand { get; }

        public KaraokeOptionsViewModel(AppDbContext ctx)
        {
            _ctx = ctx;
            View = CollectionViewSource.GetDefaultView(_items);
            View.Filter = o => string.IsNullOrWhiteSpace(_search) || (o is KaraokeOption k &&
                (k.Name.ToLower().Contains(_search.ToLower()) ||
                 k.Price.ToString().Contains(_search) ||
                 (k.Description?.ToLower().Contains(_search.ToLower()) ?? false) ||
                 k.Id.ToString().Contains(_search)));

            AddCommand = new RelayCommand(_ => Add());
            EditCommand = new RelayCommand(o => Edit(o as KaraokeOption), o => o is KaraokeOption);
            DeleteCommand = new RelayCommand(o => Delete(o as KaraokeOption), o => o is KaraokeOption);
            ReloadCommand = new RelayCommand(_ => Load());
            Load();
        }

        public void Load()
        {
            _items.Clear();
            foreach (var k in _ctx.KaraokeOptions.OrderBy(x => x.Id).ToList()) _items.Add(k);
            View.Refresh();
        }

        private void Add()
        {
            var entity = new KaraokeOption();
            var dlg = new EditDialog(entity, _ctx, true) { Owner = Application.Current.MainWindow };
            if (dlg.ShowDialog() != true) return;
            try
            {
                _ctx.ExecProc("usp_Insert_Karaoke",
                    new SqlParameter("@name_option", entity.Name),
                    new SqlParameter("@price_option", entity.Price),
                    new SqlParameter("@descriptionn", (object?)entity.Description ?? DBNull.Value));
                entity.Id = _ctx.KaraokeOptions.OrderByDescending(x => x.Id).First().Id;
                _items.Add(entity);
                View.Refresh();
                ItemsChanged?.Invoke();
                ToastService.Show($"Опция «{entity.Name}» успешно добавлена");
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.InnerException?.Message ?? ex.Message,
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Edit(KaraokeOption? k)
        {
            if (k == null) return;
            var copy = new KaraokeOption { Id = k.Id, Name = k.Name, Price = k.Price, Description = k.Description };
            var dlg = new EditDialog(copy, _ctx, false) { Owner = Application.Current.MainWindow };
            if (dlg.ShowDialog() != true) return;
            try
            {
                _ctx.ExecProc("usp_Update_Karaoke",
                    new SqlParameter("@id_option", copy.Id),
                    new SqlParameter("@name_option", copy.Name),
                    new SqlParameter("@price_option", copy.Price),
                    new SqlParameter("@descriptionn", (object?)copy.Description ?? DBNull.Value));

                // ★ Обновляем оригинальный объект на месте —
                // OrderItem.Option.Name обновится в таблице позиций заказа сразу.
                k.Name = copy.Name;
                k.Price = copy.Price;
                k.Description = copy.Description;

                View.Refresh();
                ToastService.Show($"Опция «{k.Name}» успешно изменена");
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.InnerException?.Message ?? ex.Message,
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Delete(KaraokeOption? k)
        {
            if (k == null) return;

            // Проверка: нельзя удалить опцию karaoke, если она используется в позициях заказов
            var usageCount = _ctx.OrderItems.Count(oi => oi.OptionId == k.Id);
            if (usageCount > 0)
            {
                MessageBox.Show(
                    $"Невозможно удалить опцию «{k.Name}»:\nона используется в позициях заказов: {usageCount}.\nСначала удалите связанные позиции заказов.",
                    "Удаление запрещено", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (MessageBox.Show($"Удалить опцию «{k.Name}»?", "Подтверждение",
                    MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
            try
            {
                _ctx.ExecProc("usp_Delete_Karaoke", new SqlParameter("@id_option", k.Id));
                _items.Remove(k);
                View.Refresh();
                ItemsChanged?.Invoke();
                ToastService.Show($"Опция «{k.Name}» успешно удалена");
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.InnerException?.Message ?? ex.Message,
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    // ══════════════════════════════════════════════════════════════
    // WORKERS
    // ══════════════════════════════════════════════════════════════
    public class WorkersViewModel : BaseViewModel
    {
        private readonly AppDbContext _ctx;
        private readonly ObservableCollection<Worker> _items = new();
        public readonly ObservableCollection<Role> Roles;
        public ICollectionView View { get; }
        public ObservableCollection<Worker> Items => _items;
        public event Action? ItemsChanged;

        private string _search = "";
        public string SearchText
        {
            get => _search;
            set { SetProperty(ref _search, value); View.Refresh(); }
        }

        public RelayCommand AddCommand { get; }
        public RelayCommand EditCommand { get; }
        public RelayCommand DeleteCommand { get; }
        public RelayCommand ReloadCommand { get; }

        public WorkersViewModel(AppDbContext ctx, ObservableCollection<Role> roles)
        {
            _ctx = ctx;
            Roles = roles;
            View = CollectionViewSource.GetDefaultView(_items);
            View.Filter = o => string.IsNullOrWhiteSpace(_search) || (o is Worker w &&
                (w.Name.ToLower().Contains(_search.ToLower()) ||
                 (w.Role?.Name.ToLower().Contains(_search.ToLower()) ?? false) ||
                 (w.Idnp?.ToLower().Contains(_search.ToLower()) ?? false) ||
                 (w.Phone?.ToLower().Contains(_search.ToLower()) ?? false) ||
                 (w.Address?.ToLower().Contains(_search.ToLower()) ?? false) ||
                 (w.Birth?.ToString("dd.MM.yyyy").Contains(_search) ?? false) ||
                 w.Id.ToString().Contains(_search)));

            AddCommand = new RelayCommand(_ => Add());
            EditCommand = new RelayCommand(o => Edit(o as Worker), o => o is Worker);
            DeleteCommand = new RelayCommand(o => Delete(o as Worker), o => o is Worker);
            ReloadCommand = new RelayCommand(_ => Load());
            Load();
        }

        public void Load()
        {
            _items.Clear();
            foreach (var w in _ctx.Workers.Include(x => x.Role).OrderBy(x => x.Id).ToList())
                _items.Add(w);
            View.Refresh();
        }

        private void Add()
        {
            var entity = new Worker();
            var vm = new WorkerEditorViewModel(entity, Roles);
            var dlg = new EditDialog(vm, _ctx, true) { Owner = Application.Current.MainWindow };
            if (dlg.ShowDialog() != true) return;
            try
            {
                _ctx.ExecProc("usp_Insert_Worker",
                    new SqlParameter("@name_worker", entity.Name),
                    new SqlParameter("@id_role", entity.RoleId),
                    new SqlParameter("@idnp", (object?)entity.Idnp ?? DBNull.Value),
                    new SqlParameter("@birth", (object?)entity.Birth ?? DBNull.Value),
                    new SqlParameter("@addres", (object?)entity.Address ?? DBNull.Value),
                    new SqlParameter("@phone", (object?)entity.Phone ?? DBNull.Value));
                entity.Id = _ctx.Workers.OrderByDescending(x => x.Id).First().Id;
                entity.Role = Roles.FirstOrDefault(r => r.Id == entity.RoleId);
                _items.Add(entity);
                View.Refresh();
                ItemsChanged?.Invoke();
                ToastService.Show($"Сотрудник «{entity.Name}» успешно добавлен");
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.InnerException?.Message ?? ex.Message,
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Edit(Worker? w)
        {
            if (w == null) return;
            var copy = new Worker
            {
                Id = w.Id,
                Name = w.Name,
                RoleId = w.RoleId,
                Idnp = w.Idnp,
                Birth = w.Birth,
                Address = w.Address,
                Phone = w.Phone
            };
            var vm = new WorkerEditorViewModel(copy, Roles);
            var dlg = new EditDialog(vm, _ctx, false) { Owner = Application.Current.MainWindow };
            if (dlg.ShowDialog() != true) return;
            try
            {
                _ctx.ExecProc("usp_Update_Worker",
                    new SqlParameter("@id_worker", copy.Id),
                    new SqlParameter("@name_worker", copy.Name),
                    new SqlParameter("@id_role", copy.RoleId),
                    new SqlParameter("@idnp", (object?)copy.Idnp ?? DBNull.Value),
                    new SqlParameter("@birth", (object?)copy.Birth ?? DBNull.Value),
                    new SqlParameter("@addres", (object?)copy.Address ?? DBNull.Value),
                    new SqlParameter("@phone", (object?)copy.Phone ?? DBNull.Value));

                // ★ Обновляем оригинальный объект на месте.
                // Order.Worker.Name в таблице заказов обновится сразу.
                w.Name = copy.Name;
                w.Idnp = copy.Idnp;
                w.Birth = copy.Birth;
                w.Address = copy.Address;
                w.Phone = copy.Phone;
                w.RoleId = copy.RoleId;
                // Важно: перепривязываем Role — Worker.Role.Name тоже обновится
                w.Role = Roles.FirstOrDefault(r => r.Id == copy.RoleId);

                View.Refresh();
                ToastService.Show($"Сотрудник «{w.Name}» успешно изменён");
                // ItemsChanged не нужен для Edit
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.InnerException?.Message ?? ex.Message,
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Delete(Worker? w)
        {
            if (w == null) return;

            // Проверка: нельзя удалить сотрудника, если он назначен на заказы
            var orderCount = _ctx.Orders.Count(o => o.WorkerId == w.Id);
            if (orderCount > 0)
            {
                MessageBox.Show(
                    $"Невозможно удалить сотрудника «{w.Name}»:\nон назначен на заказов: {orderCount}.\nСначала удалите или переназначьте эти заказы.",
                    "Удаление запрещено", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (MessageBox.Show($"Удалить сотрудника «{w.Name}»?", "Подтверждение",
                    MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
            try
            {
                _ctx.ExecProc("usp_Delete_Worker", new SqlParameter("@id_worker", w.Id));
                _items.Remove(w);
                View.Refresh();
                ItemsChanged?.Invoke();
                ToastService.Show($"Сотрудник «{w.Name}» успешно удалён");
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.InnerException?.Message ?? ex.Message,
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    // ══════════════════════════════════════════════════════════════
    // MENU ITEMS
    // ══════════════════════════════════════════════════════════════
    public class MenuItemsViewModel : BaseViewModel
    {
        private readonly AppDbContext _ctx;
        private readonly ObservableCollection<MenuItem> _items = new();
        public readonly ObservableCollection<MenuType> MenuTypes;
        public ICollectionView View { get; }
        public ObservableCollection<MenuItem> Items => _items;
        public event Action? ItemsChanged;

        private string _search = "";
        public string SearchText
        {
            get => _search;
            set { SetProperty(ref _search, value); View.Refresh(); }
        }

        public RelayCommand AddCommand { get; }
        public RelayCommand EditCommand { get; }
        public RelayCommand DeleteCommand { get; }
        public RelayCommand ReloadCommand { get; }

        public MenuItemsViewModel(AppDbContext ctx, ObservableCollection<MenuType> menuTypes)
        {
            _ctx = ctx;
            MenuTypes = menuTypes;
            View = CollectionViewSource.GetDefaultView(_items);
            View.Filter = o => string.IsNullOrWhiteSpace(_search) ||
                (o is MenuItem m &&
                (m.Name.ToLower().Contains(_search.ToLower()) ||
                 m.Section.ToLower().Contains(_search.ToLower()) ||
                 m.Price.ToString().Contains(_search) ||
                 m.WeightVolume.ToLower().Contains(_search.ToLower()) ||
                 m.Ingredients.ToLower().Contains(_search.ToLower()) ||
                 (m.Type?.Name.ToLower().Contains(_search.ToLower()) ?? false) ||
                 (m.CookingTime?.ToString().Contains(_search) ?? false) ||
                 m.Id.ToString().Contains(_search)));

            AddCommand = new RelayCommand(_ => Add());
            EditCommand = new RelayCommand(o => Edit(o as MenuItem), o => o is MenuItem);
            DeleteCommand = new RelayCommand(o => Delete(o as MenuItem), o => o is MenuItem);
            ReloadCommand = new RelayCommand(_ => Load());
            Load();
        }

        public void Load()
        {
            _items.Clear();
            // Важно: Include(Type) — чтобы MenuItem.Type был тем же объектом,
            // что лежит в коллекции MenuTypesVM.Items
            foreach (var m in _ctx.MenuItems.Include(x => x.Type).OrderBy(x => x.Id).ToList())
            {
                // Перепривязываем Type к живому объекту из общей коллекции
                m.Type = MenuTypes.FirstOrDefault(t => t.Id == m.TypeId) ?? m.Type;
                _items.Add(m);
            }
            View.Refresh();
        }

        private void Add()
        {
            var entity = new MenuItem();
            var vm = new MenuItemEditorViewModel(entity, MenuTypes);
            var dlg = new EditDialog(vm, _ctx, true) { Owner = Application.Current.MainWindow };
            if (dlg.ShowDialog() != true) return;
            try
            {
                _ctx.ExecProc("usp_Insert_Menu",
                    new SqlParameter("@name_product", entity.Name),
                    new SqlParameter("@price_product", entity.Price),
                    new SqlParameter("@id_type", entity.TypeId),
                    new SqlParameter("@section", entity.Section),
                    new SqlParameter("@weight_volume", entity.WeightVolume),
                    new SqlParameter("@ingredients", entity.Ingredients),
                    new SqlParameter("@cooking_time", (object?)entity.CookingTime ?? DBNull.Value),
                    new SqlParameter("@image_path", (object?)entity.ImagePath ?? DBNull.Value));
                entity.Id = _ctx.MenuItems.OrderByDescending(x => x.Id).First().Id;
                // Берём Type из общей коллекции, чтобы связь была живой
                entity.Type = MenuTypes.FirstOrDefault(t => t.Id == entity.TypeId);
                _items.Add(entity);
                View.Refresh();
                ItemsChanged?.Invoke();
                ToastService.Show($"Позиция меню «{entity.Name}» успешно добавлена");
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.InnerException?.Message ?? ex.Message,
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Edit(MenuItem? m)
        {
            if (m == null) return;
            var copy = new MenuItem
            {
                Id = m.Id,
                Name = m.Name,
                Price = m.Price,
                TypeId = m.TypeId,
                Section = m.Section,
                WeightVolume = m.WeightVolume,
                Ingredients = m.Ingredients,
                CookingTime = m.CookingTime,
                ImagePath = m.ImagePath
            };
            var vm = new MenuItemEditorViewModel(copy, MenuTypes);
            var dlg = new EditDialog(vm, _ctx, false) { Owner = Application.Current.MainWindow };
            if (dlg.ShowDialog() != true) return;
            try
            {
                _ctx.ExecProc("usp_Update_Menu",
                    new SqlParameter("@id_product", copy.Id),
                    new SqlParameter("@name_product", copy.Name),
                    new SqlParameter("@price_product", copy.Price),
                    new SqlParameter("@id_type", copy.TypeId),
                    new SqlParameter("@section", copy.Section),
                    new SqlParameter("@weight_volume", copy.WeightVolume),
                    new SqlParameter("@ingredients", copy.Ingredients),
                    new SqlParameter("@cooking_time", (object?)copy.CookingTime ?? DBNull.Value),
                    new SqlParameter("@image_path", (object?)copy.ImagePath ?? DBNull.Value));

                // ★ Обновляем оригинальный объект на месте.
                m.Name = copy.Name;
                m.Price = copy.Price;
                m.Section = copy.Section;
                m.WeightVolume = copy.WeightVolume;
                m.Ingredients = copy.Ingredients;
                m.CookingTime = copy.CookingTime;
                m.TypeId = copy.TypeId;
                m.ImagePath = copy.ImagePath;
                // Перепривязываем к живому объекту Type из общей коллекции
                m.Type = MenuTypes.FirstOrDefault(t => t.Id == copy.TypeId);

                View.Refresh();
                ToastService.Show($"Позиция меню «{m.Name}» успешно изменена");
                // ItemsChanged не нужен — OrderItem.Product ссылается на тот же экземпляр
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.InnerException?.Message ?? ex.Message,
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Delete(MenuItem? m)
        {
            if (m == null) return;

            // Проверка: нельзя удалить позицию меню, если она используется в заказах
            var usageCount = _ctx.OrderItems.Count(oi => oi.ProductId == m.Id);
            if (usageCount > 0)
            {
                MessageBox.Show(
                    $"Невозможно удалить позицию «{m.Name}»:\nона используется в позициях заказов: {usageCount}.\nСначала удалите связанные позиции заказов.",
                    "Удаление запрещено", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (MessageBox.Show($"Удалить позицию меню «{m.Name}»?", "Подтверждение",
                    MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
            try
            {
                _ctx.ExecProc("usp_Delete_Menu", new SqlParameter("@id_product", m.Id));
                _items.Remove(m);
                View.Refresh();
                ItemsChanged?.Invoke();
                ToastService.Show($"Позиция меню «{m.Name}» успешно удалена");
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.InnerException?.Message ?? ex.Message,
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    // ══════════════════════════════════════════════════════════════
    // ORDERS
    // ══════════════════════════════════════════════════════════════
    public class OrdersViewModel : BaseViewModel
    {
        private readonly AppDbContext _ctx;
        private readonly ObservableCollection<Order> _items = new();
        public readonly ObservableCollection<Worker> Workers;
        public ICollectionView View { get; }
        public ObservableCollection<Order> Items => _items;
        public event Action? ItemsChanged;

        private string _search = "";
        public string SearchText
        {
            get => _search;
            set { SetProperty(ref _search, value); View.Refresh(); }
        }

        public RelayCommand AddCommand { get; }
        public RelayCommand EditCommand { get; }
        public RelayCommand DeleteCommand { get; }
        public RelayCommand ReloadCommand { get; }

        public OrdersViewModel(AppDbContext ctx, ObservableCollection<Worker> workers)
        {
            _ctx = ctx;
            Workers = workers;
            View = CollectionViewSource.GetDefaultView(_items);
            View.Filter = o => string.IsNullOrWhiteSpace(_search) ||
                (o is Order ord &&
                (ord.Status.ToLower().Contains(_search.ToLower()) ||
                 ord.TableNumber.ToString().Contains(_search) ||
                 ord.Id.ToString().Contains(_search) ||
                 (ord.GuestCount?.ToString().Contains(_search) ?? false) ||
                 (ord.Worker?.Name.ToLower().Contains(_search.ToLower()) ?? false) ||
                 ord.CreatedAt.ToString("dd.MM.yyyy").Contains(_search)));

            AddCommand = new RelayCommand(_ => Add());
            EditCommand = new RelayCommand(o => Edit(o as Order), o => o is Order);
            DeleteCommand = new RelayCommand(o => Delete(o as Order), o => o is Order);
            ReloadCommand = new RelayCommand(_ => Load());
            Load();
        }

        public void Load()
        {
            _items.Clear();
            foreach (var o in _ctx.Orders.Include(x => x.Worker).OrderBy(x => x.Id).ToList())
            {
                // Перепривязываем Worker к живому объекту из общей коллекции
                o.Worker = Workers.FirstOrDefault(w => w.Id == o.WorkerId) ?? o.Worker;
                _items.Add(o);
            }
            View.Refresh();
        }

        private void Add()
        {
            var entity = new Order { CreatedAt = DateTime.Now, Status = "open" };
            var vm = new OrderEditorViewModel(entity, Workers, isNew: true);
            var dlg = new EditDialog(vm, _ctx, true) { Owner = Application.Current.MainWindow };
            if (dlg.ShowDialog() != true) return;
            try
            {
                _ctx.ExecProc("usp_Insert_Order",
                    new SqlParameter("@table_number", entity.TableNumber),
                    new SqlParameter("@id_worker", entity.WorkerId),
                    new SqlParameter("@guest_count", (object?)entity.GuestCount ?? DBNull.Value));
                var saved = _ctx.Orders.Include(x => x.Worker).OrderByDescending(x => x.Id).First();
                entity.Id = saved.Id;
                entity.Worker = Workers.FirstOrDefault(w => w.Id == entity.WorkerId);
                _items.Add(entity);
                View.Refresh();
                ItemsChanged?.Invoke();
                ToastService.Show($"Заказ #{entity.Id} успешно создан");
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.InnerException?.Message ?? ex.Message,
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Edit(Order? o)
        {
            if (o == null) return;
            var copy = new Order
            {
                Id = o.Id,
                TableNumber = o.TableNumber,
                WorkerId = o.WorkerId,
                Status = o.Status,
                GuestCount = o.GuestCount,
                CreatedAt = o.CreatedAt
            };
            var vm = new OrderEditorViewModel(copy, Workers, isNew: false);
            var dlg = new EditDialog(vm, _ctx, false) { Owner = Application.Current.MainWindow };
            if (dlg.ShowDialog() != true) return;
            try
            {
                _ctx.ExecProc("usp_Update_Order",
                    new SqlParameter("@id_order", copy.Id),
                    new SqlParameter("@table_number", copy.TableNumber),
                    new SqlParameter("@id_worker", copy.WorkerId),
                    new SqlParameter("@guest_count", (object?)copy.GuestCount ?? DBNull.Value));

                // ★ Обновляем оригинальный объект на месте.
                // Bill.Order.xxx и OrderItem.Order.xxx обновятся сразу.
                o.TableNumber = copy.TableNumber;
                // Статус не обновляем — usp_Update_Order его не меняет
                o.GuestCount = copy.GuestCount;
                o.WorkerId = copy.WorkerId;
                o.Worker = Workers.FirstOrDefault(w => w.Id == copy.WorkerId);

                View.Refresh();
                ToastService.Show($"Заказ #{o.Id} успешно изменён");
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.InnerException?.Message ?? ex.Message,
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Delete(Order? o)
        {
            if (o == null) return;

            // Проверка: нельзя удалить закрытый заказ
            if (o.Status == "closed")
            {
                MessageBox.Show(
                    $"Невозможно удалить заказ #{o.Id}:\n  • заказ уже закрыт и оплачен.",
                    "Удаление запрещено", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            // Позиции и счёт удалятся каскадно через БД

            if (MessageBox.Show($"Удалить заказ #{o.Id}?", "Подтверждение",
                    MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
            try
            {
                _ctx.ExecProc("usp_Delete_Order", new SqlParameter("@id_order", o.Id));
                _items.Remove(o);
                View.Refresh();
                ItemsChanged?.Invoke();
                ToastService.Show($"Заказ #{o.Id} успешно удалён");
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.InnerException?.Message ?? ex.Message,
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>Принудительно обновить строку заказа в View (например после оплаты).</summary>
        public void Refresh(int orderId)
        {
            var o = _items.FirstOrDefault(x => x.Id == orderId);
            if (o == null) return;
            View.Refresh();
        }
    }

    // ══════════════════════════════════════════════════════════════
    // ORDER ITEMS
    // ══════════════════════════════════════════════════════════════
    public class OrderItemsViewModel : BaseViewModel
    {
        private readonly AppDbContext _ctx;
        private readonly ObservableCollection<OrderItem> _items = new();
        public readonly ObservableCollection<Order> Orders;
        public readonly ObservableCollection<MenuItem> Products;
        public readonly ObservableCollection<KaraokeOption> KaraokeOptions;
        public ICollectionView View { get; }
        public ObservableCollection<OrderItem> Items => _items;

        private string _search = "";
        public string SearchText
        {
            get => _search;
            set { SetProperty(ref _search, value); View.Refresh(); }
        }

        public RelayCommand AddCommand { get; }
        public RelayCommand EditCommand { get; }
        public RelayCommand DeleteCommand { get; }
        public RelayCommand ReloadCommand { get; }

        public OrderItemsViewModel(AppDbContext ctx,
            ObservableCollection<Order> orders,
            ObservableCollection<MenuItem> products,
            ObservableCollection<KaraokeOption> karaokeOptions)
        {
            _ctx = ctx;
            Orders = orders;
            Products = products;
            KaraokeOptions = karaokeOptions;
            View = CollectionViewSource.GetDefaultView(_items);
            View.Filter = o => string.IsNullOrWhiteSpace(_search) ||
                (o is OrderItem oi &&
                (oi.ItemType.ToLower().Contains(_search.ToLower()) ||
                 oi.OrderId.ToString().Contains(_search) ||
                 oi.Id.ToString().Contains(_search) ||
                 oi.Quantity.ToString().Contains(_search) ||
                 oi.PriceAtOrder.ToString().Contains(_search) ||
                 (oi.Notes?.ToLower().Contains(_search.ToLower()) ?? false) ||
                 (oi.Product?.Name.ToLower().Contains(_search.ToLower()) ?? false) ||
                 (oi.Option?.Name.ToLower().Contains(_search.ToLower()) ?? false)));

            AddCommand = new RelayCommand(_ => Add());
            EditCommand = new RelayCommand(o => Edit(o as OrderItem), o => o is OrderItem);
            DeleteCommand = new RelayCommand(o => Delete(o as OrderItem), o => o is OrderItem);
            ReloadCommand = new RelayCommand(_ => Load());
            Load();
        }

        public void Load()
        {
            _items.Clear();
            foreach (var oi in _ctx.OrderItems
                .Include(x => x.Product)
                .Include(x => x.Option)
                .Include(x => x.Order)
                .OrderBy(x => x.Id).ToList())
            {
                // Перепривязываем к живым объектам из общих коллекций
                oi.Order = Orders.FirstOrDefault(o => o.Id == oi.OrderId) ?? oi.Order;
                oi.Product = Products.FirstOrDefault(p => p.Id == oi.ProductId) ?? oi.Product;
                oi.Option = KaraokeOptions.FirstOrDefault(k => k.Id == oi.OptionId) ?? oi.Option;
                _items.Add(oi);
            }
            View.Refresh();
        }

        private void Add()
        {
            var entity = new OrderItem();
            var vm = new OrderItemEditorViewModel(entity, Orders, Products, KaraokeOptions, isNew: true);
            var dlg = new EditDialog(vm, _ctx, true) { Owner = Application.Current.MainWindow };
            if (dlg.ShowDialog() != true) return;
            try
            {
                _ctx.ExecProc("usp_Insert_OrderItem",
                    new SqlParameter("@id_order", entity.OrderId),
                    new SqlParameter("@item_type", entity.ItemType),
                    new SqlParameter("@id_product", (object?)entity.ProductId ?? DBNull.Value),
                    new SqlParameter("@id_option", (object?)entity.OptionId ?? DBNull.Value),
                    new SqlParameter("@quantity", entity.Quantity),
                    new SqlParameter("@notes", (object?)entity.Notes ?? DBNull.Value));

                var added = _ctx.OrderItems
                    .Include(x => x.Product)
                    .Include(x => x.Option)
                    .Include(x => x.Order)
                    .OrderByDescending(x => x.Id)
                    .FirstOrDefault();

                if (added != null)
                {
                    // Перепривязываем к живым объектам
                    added.Order = Orders.FirstOrDefault(o => o.Id == added.OrderId) ?? added.Order;
                    added.Product = Products.FirstOrDefault(p => p.Id == added.ProductId) ?? added.Product;
                    added.Option = KaraokeOptions.FirstOrDefault(k => k.Id == added.OptionId) ?? added.Option;
                    _items.Add(added);
                    View.Refresh();
                    ToastService.Show($"Позиция заказа #{added.Id} успешно добавлена");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.InnerException?.Message ?? ex.Message,
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Edit(OrderItem? oi)
        {
            if (oi == null) return;
            var copy = new OrderItem
            {
                Id = oi.Id,
                OrderId = oi.OrderId,
                ItemType = oi.ItemType,
                ProductId = oi.ProductId,
                OptionId = oi.OptionId,
                Quantity = oi.Quantity,
                PriceAtOrder = oi.PriceAtOrder,
                Notes = oi.Notes,
                Product = oi.Product,
                Option = oi.Option
            };
            var vm = new OrderItemEditorViewModel(copy, Orders, Products, KaraokeOptions, isNew: false);
            var dlg = new EditDialog(vm, _ctx, false) { Owner = Application.Current.MainWindow };
            if (dlg.ShowDialog() != true) return;
            try
            {
                _ctx.ExecProc("usp_Update_OrderItem",
                    new SqlParameter("@id", copy.Id),
                    new SqlParameter("@quantity", copy.Quantity),
                    new SqlParameter("@notes", (object?)copy.Notes ?? DBNull.Value));

                // ★ Обновляем только те поля, которые реально меняет usp_Update_OrderItem
                oi.Quantity = copy.Quantity;
                oi.Notes = copy.Notes;

                View.Refresh();
                ToastService.Show($"Позиция заказа #{oi.Id} успешно изменена");
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.InnerException?.Message ?? ex.Message,
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Delete(OrderItem? oi)
        {
            if (oi == null) return;
            if (MessageBox.Show($"Удалить позицию #{oi.Id}?", "Подтверждение",
                    MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
            try
            {
                _ctx.ExecProc("usp_Delete_OrderItem", new SqlParameter("@id", oi.Id));
                _items.Remove(oi);
                View.Refresh();
                ToastService.Show($"Позиция заказа #{oi.Id} успешно удалена");
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.InnerException?.Message ?? ex.Message,
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    // ══════════════════════════════════════════════════════════════
    // BILLS
    // ══════════════════════════════════════════════════════════════
    public class BillsViewModel : BaseViewModel
    {
        private readonly AppDbContext _ctx;
        private readonly ObservableCollection<Bill> _items = new();
        public readonly ObservableCollection<Order> Orders;
        public ICollectionView View { get; }
        public ObservableCollection<Bill> Items => _items;

        private string _search = "";
        public string SearchText
        {
            get => _search;
            set { SetProperty(ref _search, value); View.Refresh(); }
        }

        // Событие: когда счёт помечен "paid" — заказ нужно закрыть
        public event Action<int>? OrderShouldClose;

        public RelayCommand EditCommand { get; }
        public RelayCommand ReloadCommand { get; }

        public BillsViewModel(AppDbContext ctx, ObservableCollection<Order> orders)
        {
            _ctx = ctx;
            Orders = orders;
            View = CollectionViewSource.GetDefaultView(_items);
            View.Filter = o => string.IsNullOrWhiteSpace(_search) ||
                (o is Bill b &&
                (b.BillStatus.ToLower().Contains(_search.ToLower()) ||
                 (b.PaymentMethod?.ToLower().Contains(_search.ToLower()) ?? false) ||
                 b.Id.ToString().Contains(_search) ||
                 b.OrderId.ToString().Contains(_search) ||
                 (b.TotalAmount?.ToString().Contains(_search) ?? false) ||
                 (b.Deposit?.ToString().Contains(_search) ?? false) ||
                 b.CreatedAt.ToString("dd.MM.yyyy").Contains(_search)));

            EditCommand = new RelayCommand(o => Edit(o as Bill), o => o is Bill);
            ReloadCommand = new RelayCommand(_ => Load());
            Load();
        }

        public void Load()
        {
            _items.Clear();
            foreach (var b in _ctx.Bills.Include(x => x.Order).OrderBy(x => x.Id).ToList())
            {
                b.Order = Orders.FirstOrDefault(o => o.Id == b.OrderId) ?? b.Order;
                _items.Add(b);
            }
            View.Refresh();
        }

        private void Edit(Bill? b)
        {
            if (b == null) return;
            var copy = new Bill
            {
                Id = b.Id,
                OrderId = b.OrderId,
                CreatedAt = b.CreatedAt,
                TotalAmount = b.TotalAmount,
                Deposit = b.Deposit,
                PaymentMethod = b.PaymentMethod,
                BillStatus = b.BillStatus
            };
            var vm = new BillEditorViewModel(copy, Orders);
            var dlg = new EditDialog(vm, _ctx, false) { Owner = Application.Current.MainWindow };
            if (dlg.ShowDialog() != true) return;
            try
            {
                // usp_Update_Bill принимает @id_order (не @id_bill), @payment_method, @bill_status, @deposit
                _ctx.ExecProc("usp_Update_Bill",
                    new SqlParameter("@id_order", copy.OrderId),
                    new SqlParameter("@payment_method", (object?)copy.PaymentMethod ?? DBNull.Value),
                    new SqlParameter("@bill_status", copy.BillStatus),
                    new SqlParameter("@deposit", (object?)copy.Deposit ?? DBNull.Value));

                // ★ Обновляем оригинальный объект на месте
                b.Deposit = copy.Deposit;
                b.PaymentMethod = copy.PaymentMethod;
                b.BillStatus = copy.BillStatus;

                View.Refresh();
                ToastService.Show($"Счёт #{b.Id} успешно обновлён");

                // Если счёт оплачен — закрываем заказ через usp_Close_Order
                if (copy.BillStatus == "paid")
                {
                    try
                    {
                        _ctx.ExecProc("usp_Close_Order",
                            new SqlParameter("@id_order", copy.OrderId));

                        // Синхронизируем объект Order в памяти
                        var relatedOrder = Orders.FirstOrDefault(o => o.Id == copy.OrderId);
                        if (relatedOrder != null)
                            relatedOrder.Status = "closed";

                        OrderShouldClose?.Invoke(copy.OrderId);
                        ToastService.Show($"Заказ #{copy.OrderId} закрыт после оплаты");
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Счёт обновлён, но не удалось закрыть заказ:\n{ex.InnerException?.Message ?? ex.Message}",
                            "Предупреждение", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.InnerException?.Message ?? ex.Message,
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    // ══════════════════════════════════════════════════════════════
    // APP USERS
    // ══════════════════════════════════════════════════════════════
    public class AppUsersViewModel : BaseViewModel
    {
        private readonly AppDbContext _ctx;
        private readonly ObservableCollection<AppUser> _items = new();
        public readonly ObservableCollection<Worker> Workers;
        public ICollectionView View { get; }
        public ObservableCollection<AppUser> Items => _items;
        public event Action? ItemsChanged;

        public string[] AppRoles { get; } = { "admin", "user" };

        private string _search = "";
        public string SearchText
        {
            get => _search;
            set { SetProperty(ref _search, value); View.Refresh(); }
        }

        public RelayCommand AddCommand { get; }
        public RelayCommand EditCommand { get; }
        public RelayCommand DeleteCommand { get; }
        public RelayCommand ReloadCommand { get; }

        public AppUsersViewModel(AppDbContext ctx, ObservableCollection<Worker> workers)
        {
            _ctx = ctx;
            Workers = workers;
            View = CollectionViewSource.GetDefaultView(_items);
            View.Filter = o => string.IsNullOrWhiteSpace(_search) ||
                (o is AppUser u &&
                (u.Username.ToLower().Contains(_search.ToLower()) ||
                 u.AppRole.ToLower().Contains(_search.ToLower()) ||
                 (u.Worker?.Name.ToLower().Contains(_search.ToLower()) ?? false) ||
                 u.Id.ToString().Contains(_search)));

            AddCommand = new RelayCommand(_ => Add());
            EditCommand = new RelayCommand(o => Edit(o as AppUser), o => o is AppUser);
            DeleteCommand = new RelayCommand(o => Delete(o as AppUser), o => o is AppUser);
            ReloadCommand = new RelayCommand(_ => Load());
            Load();
        }

        public void Load()
        {
            _items.Clear();
            foreach (var u in _ctx.AppUsers.Include(x => x.Worker).OrderBy(x => x.Id).ToList())
                _items.Add(u);
            View.Refresh();
        }

        // Возвращает Id сотрудников, у которых уже есть аккаунт (исключая текущего пользователя при редактировании)
        private HashSet<int> GetWorkerIdsWithAccount(int? excludeUserId = null) =>
            _items
                .Where(u => u.WorkerId.HasValue && u.Id != excludeUserId)
                .Select(u => u.WorkerId!.Value)
                .ToHashSet();

        private void Add()
        {
            var entity = new AppUser();
            var takenWorkerIds = GetWorkerIdsWithAccount();
            var vm = new AppUserEditorViewModel(entity, Workers, takenWorkerIds);
            var dlg = new EditDialog(vm, _ctx, true) { Owner = Application.Current.MainWindow };
            if (dlg.ShowDialog() != true) return;
            try
            {
                _ctx.ExecProc("usp_Insert_AppUser",
                    new SqlParameter("@username", entity.Username),
                    new SqlParameter("@password", entity.Password),
                    new SqlParameter("@app_role", entity.AppRole),
                    new SqlParameter("@id_worker", (object?)entity.WorkerId ?? DBNull.Value));
                entity.Id = _ctx.AppUsers.OrderByDescending(x => x.Id).First().Id;
                entity.Worker = Workers.FirstOrDefault(w => w.Id == entity.WorkerId);
                _items.Add(entity);
                View.Refresh();
                ItemsChanged?.Invoke();
                ToastService.Show($"Пользователь «{entity.Username}» успешно добавлен");
            }
            catch (Exception ex)
            {
                var msg = ex.InnerException?.Message ?? ex.Message;
                if (msg.Contains("UQ__app_user", StringComparison.OrdinalIgnoreCase) ||
                    (msg.Contains("duplicate key", StringComparison.OrdinalIgnoreCase) &&
                     msg.Contains("app_user", StringComparison.OrdinalIgnoreCase)))
                {
                    MessageBox.Show($"Логин «{entity.Username}» уже занят.\nВыберите другой логин.",
                        "Дубликат логина", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
                else
                {
                    MessageBox.Show(msg, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void Edit(AppUser? u)
        {
            if (u == null) return;

            // Защита: администратора нельзя редактировать
            if (u.AppRole == "admin")
            {
                MessageBox.Show("Аккаунт администратора нельзя редактировать.",
                    "Доступ запрещён", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var copy = new AppUser
            {
                Id = u.Id,
                Username = u.Username,
                Password = u.Password,
                AppRole = u.AppRole,
                WorkerId = u.WorkerId
            };
            var takenWorkerIds = GetWorkerIdsWithAccount(excludeUserId: u.Id);
            var vm = new AppUserEditorViewModel(copy, Workers, takenWorkerIds);
            var dlg = new EditDialog(vm, _ctx, false) { Owner = Application.Current.MainWindow };
            if (dlg.ShowDialog() != true) return;
            try
            {
                _ctx.ExecProc("usp_Update_AppUser",
                    new SqlParameter("@id_user", copy.Id),
                    new SqlParameter("@username", copy.Username),
                    new SqlParameter("@password", copy.Password),
                    new SqlParameter("@app_role", copy.AppRole),
                    new SqlParameter("@id_worker", (object?)copy.WorkerId ?? DBNull.Value));

                u.Username = copy.Username;
                u.Password = copy.Password;
                u.AppRole = copy.AppRole;
                u.WorkerId = copy.WorkerId;
                u.Worker = Workers.FirstOrDefault(w => w.Id == copy.WorkerId);

                View.Refresh();
                ToastService.Show($"Пользователь «{u.Username}» успешно изменён");
            }
            catch (Exception ex)
            {
                var msg = ex.InnerException?.Message ?? ex.Message;
                if (msg.Contains("UQ__app_user", StringComparison.OrdinalIgnoreCase) ||
                    (msg.Contains("duplicate key", StringComparison.OrdinalIgnoreCase) &&
                     msg.Contains("app_user", StringComparison.OrdinalIgnoreCase)))
                {
                    MessageBox.Show($"Логин «{copy.Username}» уже занят.\nВыберите другой логин.",
                        "Дубликат логина", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
                else
                {
                    MessageBox.Show(msg, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void Delete(AppUser? u)
        {
            if (u == null) return;

            // Защита: администратора нельзя удалить
            if (u.AppRole == "admin")
            {
                MessageBox.Show("Аккаунт администратора нельзя удалить.",
                    "Доступ запрещён", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (MessageBox.Show($"Удалить пользователя «{u.Username}»?", "Подтверждение",
                    MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
            try
            {
                _ctx.ExecProc("usp_Delete_AppUser", new SqlParameter("@id_user", u.Id));
                _items.Remove(u);
                View.Refresh();
                ItemsChanged?.Invoke();
                ToastService.Show($"Пользователь «{u.Username}» успешно удалён");
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.InnerException?.Message ?? ex.Message,
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}