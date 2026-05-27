using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using KaraokeClub.Data;
using KaraokeClub.Models;
using KaraokeClub.Services;
using KaraokeClub.ViewModels;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

// Явные псевдонимы чтобы устранить конфликт:
// System.Windows.Controls.MenuItem  vs  KaraokeClub.Models.MenuItem
using WpfMenuItem = System.Windows.Controls.MenuItem;
using WpfDataGrid = System.Windows.Controls.DataGrid;
using SelectionChangedEventArgs = System.Windows.Controls.SelectionChangedEventArgs;

namespace KaraokeClub.Views
{
    // ─────────────────────────────────────────────────────────────
    //  UI-строка для DataGrid — отображает одну позицию заказа
    // ─────────────────────────────────────────────────────────────
    public class OrderItemRow : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        private void Notify([CallerMemberName] string? n = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));

        private int _quantity;
        private string? _notes;

        public int Id { get; set; }
        public int OrderId { get; set; }
        public string ItemType { get; set; } = "";
        public int? ProductId { get; set; }
        public int? OptionId { get; set; }
        public decimal PriceAtOrder { get; set; }
        public string DisplayName { get; set; } = "";

        public int Quantity
        {
            get => _quantity;
            set { _quantity = value; Notify(); Notify(nameof(LineTotal)); }
        }

        public string? Notes
        {
            get => _notes;
            set { _notes = value; Notify(); }
        }

        public decimal LineTotal => PriceAtOrder * Quantity;
    }

    // ─────────────────────────────────────────────────────────────
    //  Диалог просмотра / редактирования позиций заказа
    // ─────────────────────────────────────────────────────────────
    public partial class OrderItemsDialog : Window
    {
        private readonly AppDbContext _ctx;
        private readonly Order _order;
        private readonly ObservableCollection<KaraokeClub.Models.MenuItem> _products;
        private readonly ObservableCollection<KaraokeOption> _karaokeOptions;
        private readonly ObservableCollection<Order> _orders;

        private readonly ObservableCollection<OrderItemRow> _rows = new();

        /// <summary>true — все позиции удалены, заказ уничтожен.</summary>
        public bool OrderWasDeleted { get; private set; }

        /// <summary>true — были любые изменения (добавление / правка / удаление).</summary>
        public bool HasChanges { get; private set; }

        public OrderItemsDialog(
            AppDbContext ctx,
            Order order,
            ObservableCollection<KaraokeClub.Models.MenuItem> products,
            ObservableCollection<KaraokeOption> karaokeOptions,
            ObservableCollection<Order> orders)
        {
            InitializeComponent();

            _ctx = ctx;
            _order = order;
            _products = products;
            _karaokeOptions = karaokeOptions;
            _orders = orders;

            TxtTitle.Text = $"Позиции заказа  #{order.Id}  ·  Стол {order.TableNumber}";
            TxtSubtitle.Text = $"Официант: {order.Worker?.Name ?? "—"}   |   " +
                               $"Статус: {order.Status}   |   " +
                               $"Создан: {order.CreatedAt:dd.MM.yyyy HH:mm}";

            bool isClosed = order.Status is "closed" or "cancelled";
            BtnAdd.IsEnabled = !isClosed;
            BtnEdit.IsEnabled = false;
            BtnDelete.IsEnabled = false;
            ClosedBadge.Visibility = isClosed ? Visibility.Visible : Visibility.Collapsed;

            ItemsGrid.ItemsSource = _rows;
            LoadRows();
        }

        // ════════════════════════════════════════════════════════
        //  Загрузка строк из БД
        // ════════════════════════════════════════════════════════
        private void LoadRows()
        {
            _rows.Clear();

            var items = _ctx.OrderItems
                .Include(oi => oi.Product)
                .Include(oi => oi.Option)
                .Where(oi => oi.OrderId == _order.Id)
                .OrderBy(oi => oi.Id)
                .ToList();

            foreach (var oi in items)
                _rows.Add(ToRow(oi));

            RefreshState();
        }

        private static OrderItemRow ToRow(OrderItem oi) => new()
        {
            Id = oi.Id,
            OrderId = oi.OrderId,
            ItemType = oi.ItemType,
            ProductId = oi.ProductId,
            OptionId = oi.OptionId,
            Quantity = oi.Quantity,
            PriceAtOrder = oi.PriceAtOrder,
            Notes = oi.Notes,
            DisplayName = oi.Product?.Name ?? oi.Option?.Name ?? "—"
        };

        // ════════════════════════════════════════════════════════
        //  Пересчёт итога + видимость пустой заглушки
        // ════════════════════════════════════════════════════════
        private void RefreshState()
        {
            decimal total = _rows.Sum(r => r.LineTotal);
            TxtTotal.Text = $"{total:F2} MDL";

            bool empty = _rows.Count == 0;
            EmptyPanel.Visibility = empty ? Visibility.Visible : Visibility.Collapsed;
            ItemsGrid.Visibility = empty ? Visibility.Collapsed : Visibility.Visible;
        }

        // ════════════════════════════════════════════════════════
        //  Выбор строки → включаем / выключаем кнопки
        // ════════════════════════════════════════════════════════
        private void ItemsGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            bool selected = ItemsGrid.SelectedItem is OrderItemRow;
            bool isClosed = _order.Status is "closed" or "cancelled";

            BtnEdit.IsEnabled = selected && !isClosed;
            BtnDelete.IsEnabled = selected && !isClosed;
        }

        // ════════════════════════════════════════════════════════
        //  ДОБАВИТЬ позицию
        // ════════════════════════════════════════════════════════
        private void BtnAdd_Click(object sender, RoutedEventArgs e)
        {
            var entity = new OrderItem { OrderId = _order.Id };
            var ordersSnap = new ObservableCollection<Order>(
                                 _orders.Where(o => o.Id == _order.Id));

            var vm = new OrderItemEditorViewModel(
                          entity, ordersSnap, _products, _karaokeOptions, isNew: true);
            var dlg = new EditDialog(vm, _ctx, isNew: true) { Owner = this };
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

                HasChanges = true;
                LoadRows();
                ToastService.Show("Позиция успешно добавлена");
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.InnerException?.Message ?? ex.Message,
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ════════════════════════════════════════════════════════
        //  ИЗМЕНИТЬ позицию (количество + заметка)
        // ════════════════════════════════════════════════════════
        private void BtnEdit_Click(object sender, RoutedEventArgs e)
        {
            if (ItemsGrid.SelectedItem is not OrderItemRow row) return;

            var oi = _ctx.OrderItems
                .Include(x => x.Product)
                .Include(x => x.Option)
                .FirstOrDefault(x => x.Id == row.Id);
            if (oi == null) return;

            var ordersSnap = new ObservableCollection<Order>(
                                 _orders.Where(o => o.Id == _order.Id));
            var vm = new OrderItemEditorViewModel(
                          oi, ordersSnap, _products, _karaokeOptions, isNew: false);
            var dlg = new EditDialog(vm, _ctx, isNew: false) { Owner = this };
            if (dlg.ShowDialog() != true) return;

            try
            {
                _ctx.ExecProc("usp_Update_OrderItem",
                    new SqlParameter("@id", oi.Id),
                    new SqlParameter("@quantity", oi.Quantity),
                    new SqlParameter("@notes", (object?)oi.Notes ?? DBNull.Value));

                HasChanges = true;
                LoadRows();
                ToastService.Show("Позиция успешно изменена");
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.InnerException?.Message ?? ex.Message,
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ════════════════════════════════════════════════════════
        //  УДАЛИТЬ позицию → пересчёт чека → если пуст — удалить заказ
        // ════════════════════════════════════════════════════════
        private void BtnDelete_Click(object sender, RoutedEventArgs e)
        {
            if (ItemsGrid.SelectedItem is not OrderItemRow row) return;

            var confirm = MessageBox.Show(
                $"Удалить позицию «{row.DisplayName}»  ×{row.Quantity}?\n\n" +
                "Если это последняя позиция — заказ и чек будут удалены.",
                "Подтверждение", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (confirm != MessageBoxResult.Yes) return;

            try
            {
                _ctx.ExecProc("usp_Delete_OrderItem",
                    new SqlParameter("@id", row.Id));
                HasChanges = true;

                int remaining = _ctx.OrderItems.Count(oi => oi.OrderId == _order.Id);
                if (remaining == 0)
                {
                    _ctx.ExecProc("usp_Delete_Order",
                        new SqlParameter("@id_order", _order.Id));
                    OrderWasDeleted = true;

                    MessageBox.Show(
                        $"Все позиции удалены. Заказ #{_order.Id} и связанный чек удалены.",
                        "Заказ удалён", MessageBoxButton.OK, MessageBoxImage.Information);
                    Close();
                    return;
                }

                LoadRows();
                ToastService.Show("Позиция удалена, сумма чека пересчитана");
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.InnerException?.Message ?? ex.Message,
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();
    }
}