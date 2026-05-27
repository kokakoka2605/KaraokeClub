using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using KaraokeClub.Commands;
using KaraokeClub.Data;
using KaraokeClub.Models;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace KaraokeClub.ViewModels
{
    // ─────────────────────────────────────────────────────────────
    //  Строка в корзине (UI-модель, не БД)
    // ─────────────────────────────────────────────────────────────
    public class CartItem : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        private void Notify([CallerMemberName] string? n = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));

        public string ItemType { get; init; } = "product"; // "product" | "karaoke"
        public int ItemId { get; init; }
        public string Name { get; init; } = "";
        public decimal UnitPrice { get; init; }
        public string? ImagePath { get; init; }
        public string Icon { get; init; } = "🍽️";

        private int _qty = 1;
        public int Quantity
        {
            get => _qty;
            set { if (value < 1) return; _qty = value; Notify(); Notify(nameof(Subtotal)); }
        }
        public decimal Subtotal => UnitPrice * Quantity;

        private string? _notes;
        public string? Notes
        {
            get => _notes;
            set { _notes = value; Notify(); }
        }
    }

    // ─────────────────────────────────────────────────────────────
    //  Группа товаров с заголовком (для режима "все категории")
    // ─────────────────────────────────────────────────────────────
    public class MenuGroup
    {
        public MenuType Type { get; init; } = null!;
        public List<MenuItem> Items { get; init; } = new();
    }

    // ─────────────────────────────────────────────────────────────
    //  Главная ViewModel окна официанта
    // ─────────────────────────────────────────────────────────────
    public class WaiterViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        private void Notify([CallerMemberName] string? n = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));

        // ── данные ──────────────────────────────────────────────
        private readonly AppDbContext _ctx;
        public readonly Worker CurrentWorker;

        // Удобные свойства для биндинга в XAML
        public string FullName => CurrentWorker.Name;
        public string RoleName => CurrentWorker.Role?.Name ?? "Официант";

        // ── меню-данные ──────────────────────────────────────────
        public ObservableCollection<MenuType> MenuTypes { get; } = new();
        public ObservableCollection<MenuItem> AllItems { get; } = new();
        public ObservableCollection<KaraokeOption> KaraokeOptions { get; } = new();

        // ── отображаемый список карточек ─────────────────────────
        private ObservableCollection<MenuItem> _displayedItems = new();
        public ObservableCollection<MenuItem> DisplayedItems
        {
            get => _displayedItems;
            private set { _displayedItems = value; Notify(); }
        }

        private ObservableCollection<KaraokeOption> _displayedKaraoke = new();
        public ObservableCollection<KaraokeOption> DisplayedKaraoke
        {
            get => _displayedKaraoke;
            private set { _displayedKaraoke = value; Notify(); }
        }

        // ── навигация ────────────────────────────────────────────
        public enum NavSection { None, Bar, Kitchen, Karaoke, MyOrders }

        private NavSection _activeSection = NavSection.None;
        public NavSection ActiveSection
        {
            get => _activeSection;
            private set
            {
                _activeSection = value;
                Notify();
                Notify(nameof(ShowMenuGrid));
                Notify(nameof(ShowGroupedBar));
                Notify(nameof(ShowGroupedKitchen));
                Notify(nameof(ShowKaraokeGrid));
                Notify(nameof(ShowOrdersPanel));
                Notify(nameof(ShowWelcome));
            }
        }

        private MenuType? _activeSubType;
        public MenuType? ActiveSubType
        {
            get => _activeSubType;
            set { _activeSubType = value; Notify(); }
        }

        public bool ShowMenuGrid => false; // больше не используем flat grid
        public bool ShowGroupedBar => ActiveSection == NavSection.Bar;
        public bool ShowGroupedKitchen => ActiveSection == NavSection.Kitchen;
        public bool ShowKaraokeGrid => ActiveSection == NavSection.Karaoke;
        public bool ShowOrdersPanel => ActiveSection == NavSection.MyOrders;
        public bool ShowWelcome => ActiveSection == NavSection.None;

        // ── фильтрация Bar / Kitchen ──────────────────────────────
        public ObservableCollection<MenuType> BarTypes { get; } = new();
        public ObservableCollection<MenuType> KitchenTypes { get; } = new();

        // ── сгруппированные списки для "показать всё" ─────────────
        private ObservableCollection<MenuGroup> _groupedBarItems = new();
        public ObservableCollection<MenuGroup> GroupedBarItems
        {
            get => _groupedBarItems;
            private set { _groupedBarItems = value; Notify(); }
        }

        private ObservableCollection<MenuGroup> _groupedKitchenItems = new();
        public ObservableCollection<MenuGroup> GroupedKitchenItems
        {
            get => _groupedKitchenItems;
            private set { _groupedKitchenItems = value; Notify(); }
        }

        // событие для code-behind: скроллировать к секции
        public event Action<MenuType>? RequestScrollToType;

        // ── поиск ────────────────────────────────────────────────
        private string _searchText = "";
        public string SearchText
        {
            get => _searchText;
            set { _searchText = value; Notify(); ApplyFilter(); }
        }

        // ── выбор стола ──────────────────────────────────────────
        private int _selectedTable = 1;
        public int SelectedTable
        {
            get => _selectedTable;
            set { _selectedTable = value; Notify(); }
        }
        public List<int> Tables { get; } = Enumerable.Range(1, 12).ToList();
        private int _guestCount = 1;
        public int GuestCount
        {
            get => _guestCount;
            set { _guestCount = value < 1 ? 1 : value; Notify(); }
        }

        // ── корзина ──────────────────────────────────────────────
        public ObservableCollection<CartItem> Cart { get; } = new();

        private decimal _cartTotal;
        public decimal CartTotal { get => _cartTotal; private set { _cartTotal = value; Notify(); } }

        // ── мои заказы ───────────────────────────────────────────
        public ObservableCollection<Order> MyOrders { get; } = new();

        // ── команды ──────────────────────────────────────────────
        public ICommand NavBarCommand { get; }
        public ICommand NavKitchenCommand { get; }
        public ICommand NavKaraokeCommand { get; }
        public ICommand NavOrdersCommand { get; }

        public ICommand SelectTableCommand { get; }
        public ICommand SelectSubTypeCommand { get; }
        public ICommand AddMenuItemCommand { get; }
        public ICommand AddKaraokeCommand { get; }
        public ICommand IncreaseQtyCommand { get; }
        public ICommand DecreaseQtyCommand { get; }
        public ICommand RemoveCartItemCommand { get; }
        public ICommand ClearCartCommand { get; }
        public ICommand SubmitOrderCommand { get; }
        public ICommand ScrollToTypeCommand { get; }

        // ════════════════════════════════════════════════════════
        public WaiterViewModel(AppDbContext ctx, Worker worker)
        {
            _ctx = ctx;
            CurrentWorker = worker;

            // ── команды ─────────────────────────────────────────
            NavBarCommand = new RelayCommand(_ => GoSection(NavSection.Bar));
            NavKitchenCommand = new RelayCommand(_ => GoSection(NavSection.Kitchen));
            NavKaraokeCommand = new RelayCommand(_ => GoSection(NavSection.Karaoke));
            NavOrdersCommand = new RelayCommand(_ => GoSection(NavSection.MyOrders));

            SelectTableCommand = new RelayCommand(t => { if (t is int n) SelectedTable = n; });
            SelectSubTypeCommand = new RelayCommand(t => SelectSubType(t as MenuType));
            ScrollToTypeCommand = new RelayCommand(t => { if (t is MenuType mt) RequestScrollToType?.Invoke(mt); });
            AddMenuItemCommand = new RelayCommand(item => AddToCart(item as MenuItem));
            AddKaraokeCommand = new RelayCommand(opt => AddKaraokeToCart(opt as KaraokeOption));
            IncreaseQtyCommand = new RelayCommand(ci => ChangeQty(ci as CartItem, +1));
            DecreaseQtyCommand = new RelayCommand(ci => ChangeQty(ci as CartItem, -1));
            RemoveCartItemCommand = new RelayCommand(ci => RemoveFromCart(ci as CartItem));
            ClearCartCommand = new RelayCommand(_ => ClearCart());
            SubmitOrderCommand = new RelayCommand(_ => SubmitOrder(), _ => Cart.Count > 0);

            Cart.CollectionChanged += (_, __) => RecalcTotal();

            LoadData();
        }

        // ════════════════════════════════════════════════════════
        //  Загрузка данных
        // ════════════════════════════════════════════════════════
        private void LoadData()
        {
            // Типы меню
            var types = _ctx.MenuTypes.OrderBy(t => t.Name).ToList();
            foreach (var t in types) MenuTypes.Add(t);

            // Позиции меню (с типом)
            var items = _ctx.MenuItems
                .Include(m => m.Type)
                .OrderBy(m => m.Name)
                .ToList();
            foreach (var i in items) AllItems.Add(i);

            // Карточки Karaoke
            var karaoke = _ctx.KaraokeOptions.OrderBy(k => k.Name).ToList();
            foreach (var k in karaoke) KaraokeOptions.Add(k);

            // Разбиваем типы по секциям
            var barSections = new[] { "алкогольное", "безалкогольное", "горячие напитки" };
            var kitchenSections = new[] { "горячее", "закуски", "салаты", "десерты", "соусы" };

            foreach (var t in types)
            {
                if (barSections.Any(s => t.Name.Contains(s, StringComparison.OrdinalIgnoreCase)))
                    BarTypes.Add(t);
                else if (kitchenSections.Any(s => t.Name.Contains(s, StringComparison.OrdinalIgnoreCase)))
                    KitchenTypes.Add(t);
            }

            // Если какой-то тип не попал ни в bar, ни в kitchen — смотрим по section у блюд
            foreach (var t in types)
            {
                if (BarTypes.Contains(t) || KitchenTypes.Contains(t)) continue;
                var sec = AllItems.FirstOrDefault(i => i.TypeId == t.Id)?.Section;
                if (sec == "bar") BarTypes.Add(t);
                else if (sec == "kitchen") KitchenTypes.Add(t);
            }

            // Строим сгруппированные списки
            RebuildGroups();

            LoadMyOrders();
        }

        private void LoadMyOrders()
        {
            MyOrders.Clear();
            var orders = _ctx.Orders
                .Include(o => o.OrderItems)
                .Where(o => o.WorkerId == CurrentWorker.Id && o.Status == "open")
                .OrderByDescending(o => o.CreatedAt)
                .ToList();
            foreach (var o in orders) MyOrders.Add(o);
        }

        // ── Закрытие заказа (наличные / карта) ──────────────────
        public void CloseOrder(int orderId, string paymentMethod)
        {
            var conn = _ctx.Database.GetDbConnection();
            try
            {
                _ctx.Database.OpenConnection();

                // 1. Закрываем заказ
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "EXEC usp_Close_Order @id_order";
                    cmd.Parameters.Add(new SqlParameter("@id_order", orderId));
                    cmd.ExecuteNonQuery();
                }

                // 2. Обновляем bill: способ оплаты + статус 'paid'
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "EXEC usp_Update_Bill @id_order, @payment_method, @bill_status, @deposit";
                    cmd.Parameters.Add(new SqlParameter("@id_order", orderId));
                    cmd.Parameters.Add(new SqlParameter("@payment_method", paymentMethod));
                    cmd.Parameters.Add(new SqlParameter("@bill_status", "paid"));
                    cmd.Parameters.Add(new SqlParameter("@deposit", DBNull.Value));
                    cmd.ExecuteNonQuery();
                }
            }
            finally
            {
                _ctx.Database.CloseConnection();
            }

            // 3. Убираем из списка (заказ больше не 'open')
            var toRemove = MyOrders.FirstOrDefault(o => o.Id == orderId);
            if (toRemove != null)
                MyOrders.Remove(toRemove);
        }

        // ════════════════════════════════════════════════════════
        //  Навигация
        // ════════════════════════════════════════════════════════
        private void GoSection(NavSection sec)
        {
            ActiveSection = sec;
            SearchText = "";
            ActiveSubType = null;

            if (sec == NavSection.Karaoke)
                DisplayedKaraoke = KaraokeOptions;
            else if (sec == NavSection.MyOrders)
                LoadMyOrders();
        }

        private void SelectSubType(MenuType? type)
        {
            if (type == null) return;
            ActiveSubType = type;
            ApplyFilter();
        }

        private void RebuildGroups()
        {
            var barGroups = BarTypes
                .Select(t => new MenuGroup
                {
                    Type = t,
                    Items = AllItems.Where(i => i.TypeId == t.Id).OrderBy(i => i.Name).ToList()
                })
                .Where(g => g.Items.Count > 0)
                .ToList();
            GroupedBarItems = new ObservableCollection<MenuGroup>(barGroups);

            var kitchenGroups = KitchenTypes
                .Select(t => new MenuGroup
                {
                    Type = t,
                    Items = AllItems.Where(i => i.TypeId == t.Id).OrderBy(i => i.Name).ToList()
                })
                .Where(g => g.Items.Count > 0)
                .ToList();
            GroupedKitchenItems = new ObservableCollection<MenuGroup>(kitchenGroups);
        }

        private void ApplyFilter()
        {
            var search = SearchText?.Trim() ?? "";

            if (ActiveSection == NavSection.Bar)
            {
                var barGroups = BarTypes
                    .Select(t => new MenuGroup
                    {
                        Type = t,
                        Items = AllItems
                            .Where(i => i.TypeId == t.Id &&
                                (string.IsNullOrEmpty(search) ||
                                 i.Name.Contains(search, StringComparison.OrdinalIgnoreCase)))
                            .OrderBy(i => i.Name)
                            .ToList()
                    })
                    .Where(g => g.Items.Count > 0)
                    .ToList();
                GroupedBarItems = new ObservableCollection<MenuGroup>(barGroups);
            }
            else if (ActiveSection == NavSection.Kitchen)
            {
                var kitchenGroups = KitchenTypes
                    .Select(t => new MenuGroup
                    {
                        Type = t,
                        Items = AllItems
                            .Where(i => i.TypeId == t.Id &&
                                (string.IsNullOrEmpty(search) ||
                                 i.Name.Contains(search, StringComparison.OrdinalIgnoreCase)))
                            .OrderBy(i => i.Name)
                            .ToList()
                    })
                    .Where(g => g.Items.Count > 0)
                    .ToList();
                GroupedKitchenItems = new ObservableCollection<MenuGroup>(kitchenGroups);
            }
            else if (ActiveSection == NavSection.Karaoke)
            {
                var q = KaraokeOptions.AsEnumerable();
                if (!string.IsNullOrEmpty(search))
                    q = q.Where(k => k.Name.Contains(search, StringComparison.OrdinalIgnoreCase));
                DisplayedKaraoke = new ObservableCollection<KaraokeOption>(q);
            }
        }

        // ════════════════════════════════════════════════════════
        //  Корзина
        // ════════════════════════════════════════════════════════
        private void AddToCart(MenuItem? item)
        {
            if (item == null) return;
            var existing = Cart.FirstOrDefault(c => c.ItemType == "product" && c.ItemId == item.Id);
            if (existing != null) { existing.Quantity++; RecalcTotal(); return; }

            Cart.Add(new CartItem
            {
                ItemType = "product",
                ItemId = item.Id,
                Name = item.Name,
                UnitPrice = item.Price,
                ImagePath = item.ImagePath,
                Icon = item.Section == "bar" ? "🍹" : "🍽️"
            });
        }

        private void AddKaraokeToCart(KaraokeOption? opt)
        {
            if (opt == null) return;
            var existing = Cart.FirstOrDefault(c => c.ItemType == "karaoke" && c.ItemId == opt.Id);
            if (existing != null) { existing.Quantity++; RecalcTotal(); return; }

            Cart.Add(new CartItem
            {
                ItemType = "karaoke",
                ItemId = opt.Id,
                Name = opt.Name,
                UnitPrice = opt.Price,
                Icon = "🎤"
            });
        }

        private void ChangeQty(CartItem? ci, int delta)
        {
            if (ci == null) return;
            if (ci.Quantity + delta < 1) { RemoveFromCart(ci); return; }
            ci.Quantity += delta;
            RecalcTotal();
        }

        private void RemoveFromCart(CartItem? ci)
        {
            if (ci == null) return;
            Cart.Remove(ci);
        }

        private void ClearCart() => Cart.Clear();

        private void RecalcTotal()
        {
            CartTotal = Cart.Sum(c => c.Subtotal);
            Notify(nameof(CartTotal));
        }

        // ════════════════════════════════════════════════════════
        //  Отправка заказа
        // ════════════════════════════════════════════════════════
        private void SubmitOrder()
        {
            if (!Cart.Any()) return;

            // ── подтверждение ───────────────────────────────────
            var lines = Cart.Select(c => $"  • {c.Name}  ×{c.Quantity}  — {c.Subtotal:F2} MDL");
            var summary = string.Join("\n", lines);

            var result = MessageBox.Show(
                $"Стол {SelectedTable}  ·  {Cart.Count} позиций\n\n{summary}\n\nИтого: {CartTotal:F2} MDL\n\nОтправить заказ?",
                "Подтверждение заказа",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes) return;

            try
            {
                // 1. Создаём заказ — процедура возвращает NewId
                int orderId;
                using (var cmd = _ctx.Database.GetDbConnection().CreateCommand())
                {
                    _ctx.Database.OpenConnection();
                    cmd.CommandText = "EXEC usp_Insert_Order @table_number, @id_worker, @guest_count";
                    cmd.Parameters.Add(new SqlParameter("@table_number", SelectedTable));
                    cmd.Parameters.Add(new SqlParameter("@id_worker", CurrentWorker.Id));
                    cmd.Parameters.Add(new SqlParameter("@guest_count", GuestCount));
                    orderId = Convert.ToInt32(cmd.ExecuteScalar());
                    _ctx.Database.CloseConnection();
                }

                // 2. Добавляем каждую позицию
                foreach (var ci in Cart)
                {
                    _ctx.ExecProc("usp_Insert_OrderItem",
                        new SqlParameter("@id_order", orderId),
                        new SqlParameter("@item_type", ci.ItemType),
                        new SqlParameter("@id_product", ci.ItemType == "product" ? (object)ci.ItemId : DBNull.Value),
                        new SqlParameter("@id_option", ci.ItemType == "karaoke" ? (object)ci.ItemId : DBNull.Value),
                        new SqlParameter("@quantity", ci.Quantity),
                        new SqlParameter("@notes", string.IsNullOrWhiteSpace(ci.Notes) ? (object)DBNull.Value : ci.Notes));
                }

                MessageBox.Show($"Заказ #{orderId} принят!\nСтол {SelectedTable}  ·  {CartTotal:F2} MDL",
                    "Заказ отправлен", MessageBoxButton.OK, MessageBoxImage.Information);

                ClearCart();
                LoadMyOrders();
                ActiveSection = NavSection.None;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка при сохранении заказа:\n" +
                    (ex.InnerException?.Message ?? ex.Message),
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ── Получить позиции заказа для диалога ─────────────────
        public List<OrderItem> GetOrderItems(int orderId)
        {
            return _ctx.OrderItems
                .Include(oi => oi.Product)
                .Include(oi => oi.Option)
                .Where(oi => oi.OrderId == orderId)
                .OrderBy(oi => oi.Id)
                .ToList();
        }
    }
}