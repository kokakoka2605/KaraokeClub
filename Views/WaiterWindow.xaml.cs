using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using KaraokeClub.Converters;
using KaraokeClub.Models;
using KaraokeClub.ViewModels;

namespace KaraokeClub.Views
{
    public partial class WaiterWindow : Window
    {
        private KaraokeClub.Models.MenuItem? _detailItem;

        public WaiterWindow()
        {
            InitializeComponent();
            Loaded += WaiterWindow_Loaded;
        }

        private void WaiterWindow_Loaded(object sender, RoutedEventArgs e)
        {
            if (DataContext is WaiterViewModel vm)
            {
                vm.RequestScrollToType += ScrollToType;

                // Подписываемся на скролл для обновления активной категории
                BarScrollViewer.ScrollChanged += OnMenuScrollChanged;
                KitchenScrollViewer.ScrollChanged += OnMenuScrollChanged;
            }
        }

        // ── Обновить ActiveSubType при скролле ──────────────────
        private void OnMenuScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            if (DataContext is not WaiterViewModel vm) return;
            if (sender is not ScrollViewer sv) return;

            // Находим все GroupAnchor-ы, ищем последний который выше верхней границы + небольшой запас
            Border? best = null;
            double bestY = double.MinValue;

            foreach (var anchor in FindAllGroupAnchors(sv))
            {
                try
                {
                    var transform = anchor.TransformToAncestor(sv);
                    var pt = transform.Transform(new System.Windows.Point(0, 0));
                    // Секция "активна" если её заголовок находится выше середины экрана
                    if (pt.Y <= sv.ViewportHeight / 2 && pt.Y > bestY)
                    {
                        bestY = pt.Y;
                        best = anchor;
                    }
                }
                catch { /* элемент ещё не в визуальном дереве */ }
            }

            if (best?.Tag is MenuType mt && mt.Id != vm.ActiveSubType?.Id)
                vm.ActiveSubType = mt;
        }

        private static System.Collections.Generic.List<Border> FindAllGroupAnchors(DependencyObject parent)
        {
            var result = new System.Collections.Generic.List<Border>();
            int count = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < count; i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is Border b && b.Name == "GroupAnchor" && b.Tag is MenuType)
                    result.Add(b);
                result.AddRange(FindAllGroupAnchors(child));
            }
            return result;
        }

        // ── Scroll к нужной секции ───────────────────────────────
        private void ScrollToType(MenuType targetType)
        {
            ScrollViewer? sv = null;
            if (BarScrollViewer.Visibility == Visibility.Visible)
                sv = BarScrollViewer;
            else if (KitchenScrollViewer.Visibility == Visibility.Visible)
                sv = KitchenScrollViewer;

            if (sv == null) return;

            Dispatcher.InvokeAsync(() =>
            {
                var anchor = FindGroupAnchor(sv, targetType);
                if (anchor != null)
                {
                    var transform = anchor.TransformToAncestor(sv);
                    var point = transform.Transform(new System.Windows.Point(0, 0));
                    sv.ScrollToVerticalOffset(sv.VerticalOffset + point.Y - 8);
                }
            }, System.Windows.Threading.DispatcherPriority.Loaded);
        }

        private static Border? FindGroupAnchor(DependencyObject parent, MenuType targetType)
        {
            int count = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < count; i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is Border border && border.Name == "GroupAnchor"
                    && border.Tag is MenuType mt && mt.Id == targetType.Id)
                    return border;
                var found = FindGroupAnchor(child, targetType);
                if (found != null) return found;
            }
            return null;
        }

        private void Logout_Click(object sender, RoutedEventArgs e)
        {
            var conn = new ConnectionWindow();
            conn.Show();
            Close();
        }

        private void Card_Click(object sender, MouseButtonEventArgs e)
        {
            if (e.OriginalSource is System.Windows.Controls.Button) return;

            if (sender is System.Windows.Controls.Border border &&
                border.Tag is KaraokeClub.Models.MenuItem item)
            {
                ShowDetail(item);
                e.Handled = true;
            }
        }

        private void ShowDetail(KaraokeClub.Models.MenuItem item)
        {
            _detailItem = item;

            DetailName.Text = item.Name;
            DetailWeight.Text = item.WeightVolume;
            DetailIngredients.Text = string.IsNullOrWhiteSpace(item.Ingredients) ? "—" : item.Ingredients;
            DetailPrice.Text = item.Price.ToString("F0");

            if (item.CookingTime.HasValue && item.CookingTime.Value > 0)
            {
                DetailCooking.Text = $"{item.CookingTime} мин";
                DetailCookingPanel.Visibility = Visibility.Visible;
            }
            else
            {
                DetailCookingPanel.Visibility = Visibility.Collapsed;
            }

            var converter = (ImagePathConverter)Resources["ImgPath"];
            var bmp = converter.Convert(item.ImagePath, typeof(BitmapImage), null,
                          System.Globalization.CultureInfo.InvariantCulture) as BitmapImage;

            if (bmp != null)
            {
                DetailImage.Source = bmp;
                DetailImage.Visibility = Visibility.Visible;
                DetailIcon.Visibility = Visibility.Collapsed;
            }
            else
            {
                DetailImage.Source = null;
                DetailImage.Visibility = Visibility.Collapsed;
                DetailIcon.Text = item.Section?.ToLower() == "bar" ? "🍹" : "🍽️";
                DetailIcon.Visibility = Visibility.Visible;
            }

            DetailOverlay.Visibility = Visibility.Visible;
        }

        private void CloseDetail_Click(object sender, RoutedEventArgs e)
        {
            DetailOverlay.Visibility = Visibility.Collapsed;
            _detailItem = null;
        }

        private void DetailOverlay_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.Source == DetailOverlay)
                CloseDetail_Click(sender, e);
        }

        private void DetailAdd_Click(object sender, RoutedEventArgs e)
        {
            if (_detailItem != null && DataContext is WaiterViewModel vm)
            {
                vm.AddMenuItemCommand.Execute(_detailItem);
                CloseDetail_Click(sender, e);
            }
        }

        // ── Клик по карточке заказа ─────────────────────────────
        private void OrderCard_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (sender is not System.Windows.Controls.Border border) return;
            if (border.Tag is not KaraokeClub.Models.Order order) return;
            if (DataContext is not WaiterViewModel vm) return;

            var items = vm.GetOrderItems(order.Id);
            var dlg = new OrderDetailDialog(order, items, vm) { Owner = this };
            dlg.ShowDialog();
        }

        private void GuestMinus_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is WaiterViewModel vm && vm.GuestCount > 1)
                vm.GuestCount--;
        }

        private void GuestPlus_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is WaiterViewModel vm)
                vm.GuestCount++;
        }
    }

}