using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using KaraokeClub.Models;
using KaraokeClub.ViewModels;

namespace KaraokeClub.Views
{
    // UI-модель строки позиции для диалога
    public class OrderDetailItem
    {
        public string Icon { get; init; } = "🍽️";
        public string Name { get; init; } = "";
        public int Quantity { get; init; }
        public decimal UnitPrice { get; init; }
        public decimal Subtotal => UnitPrice * Quantity;
        public string? Notes { get; init; }
        public bool HasNotes => !string.IsNullOrWhiteSpace(Notes);
    }

    public partial class OrderDetailDialog : Window
    {
        private readonly Order _order;
        private readonly WaiterViewModel? _vm;

        public OrderDetailDialog(Order order, List<OrderItem> items, WaiterViewModel? vm = null)
        {
            InitializeComponent();
            _order = order;
            _vm = vm;
            Populate(order, items);
        }

        private void Populate(Order order, List<OrderItem> items)
        {
            // Шапка
            TxtTable.Text = order.TableNumber.ToString();
            TxtOrderId.Text = order.Id.ToString();
            TxtDate.Text = order.CreatedAt.ToString("HH:mm  dd.MM.yyyy");

            // Статус-бейдж
            (StatusBadge.Background, TxtStatus.Foreground, TxtStatus.Text) = order.Status switch
            {
                "open" => (new SolidColorBrush(Color.FromRgb(29, 48, 8)),
                                new SolidColorBrush(Color.FromRgb(99, 153, 34)),
                                "open"),
                "closed" => (new SolidColorBrush(Color.FromRgb(40, 40, 40)),
                                new SolidColorBrush(Color.FromRgb(150, 150, 150)),
                                "closed"),
                "cancelled" => (new SolidColorBrush(Color.FromRgb(61, 26, 26)),
                                new SolidColorBrush(Color.FromRgb(224, 82, 82)),
                                "cancelled"),
                _ => (new SolidColorBrush(Color.FromRgb(40, 40, 40)),
                                new SolidColorBrush(Color.FromRgb(150, 150, 150)),
                                order.Status)
            };

            // Кнопки оплаты — только для открытых заказов
            PaymentPanel.Visibility = order.Status == "open"
                ? Visibility.Visible
                : Visibility.Collapsed;

            // Позиции
            var displayItems = items.Select(oi => new OrderDetailItem
            {
                Icon = oi.ItemType == "karaoke" ? "🎤"
                          : (oi.Product?.Section?.ToLower() == "bar" ? "🍹" : "🍽️"),
                Name = oi.Product?.Name ?? oi.Option?.Name ?? "—",
                Quantity = oi.Quantity,
                UnitPrice = oi.PriceAtOrder,
                Notes = oi.Notes
            }).ToList();

            if (displayItems.Count == 0)
            {
                EmptyPanel.Visibility = Visibility.Visible;
                ItemsList.Visibility = Visibility.Collapsed;
            }
            else
            {
                ItemsList.ItemsSource = displayItems;
            }

            // Футер-статистика
            int posCount = displayItems.Count;
            int unitCount = displayItems.Sum(i => i.Quantity);
            decimal total = displayItems.Sum(i => i.Subtotal);

            TxtItemCount.Text = posCount.ToString();
            TxtUnitCount.Text = unitCount.ToString();
            TxtTotal.Text = $"{total:F2} MDL";
        }

        private void PayCash_Click(object sender, RoutedEventArgs e)
        {
            CloseOrderWithPayment("cash");
        }

        private void PayCard_Click(object sender, RoutedEventArgs e)
        {
            CloseOrderWithPayment("card");
        }

        private void CloseOrderWithPayment(string paymentMethod)
        {
            if (_vm == null) return;

            string methodLabel = paymentMethod == "cash" ? "Наличные" : "Карта";

            var result = MessageBox.Show(
                $"Закрыть заказ #{_order.Id}  ·  Стол {_order.TableNumber}?\n\nСпособ оплаты: {methodLabel}",
                "Закрыть заказ",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes) return;

            try
            {
                _vm.CloseOrder(_order.Id, paymentMethod);
                MessageBox.Show(
                    $"Заказ #{_order.Id} успешно закрыт!\nОплата: {methodLabel}",
                    "Заказ закрыт",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                Close();
            }
            catch (System.Exception ex)
            {
                MessageBox.Show(
                    "Ошибка при закрытии заказа:\n" + (ex.InnerException?.Message ?? ex.Message),
                    "Ошибка",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e) => Close();
    }
}
