using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Xps;
using System.Windows.Xps.Packaging;
using Microsoft.Win32;
using KaraokeClub.Data;
using KaraokeClub.Reports;

namespace KaraokeClub.Views
{
    public partial class ReportsWindow : Window
    {
        private readonly AppDbContext _ctx;
        private FlowDocument? _currentDoc;

        public ReportsWindow(AppDbContext ctx)
        {
            InitializeComponent();
            _ctx = ctx;
            InitFilters();
        }

        // ── Инициализация фильтров ────────────────────────────────

        private void InitFilters()
        {
            // Период по умолчанию — последние 6 месяцев
            DpRevFrom.SelectedDate   = DateTime.Today.AddMonths(-6);
            DpRevTo.SelectedDate     = DateTime.Today;
            DpMenuFrom.SelectedDate  = DateTime.Today.AddMonths(-6);
            DpMenuTo.SelectedDate    = DateTime.Today;
            DpStaffFrom.SelectedDate = DateTime.Today.AddMonths(-6);
            DpStaffTo.SelectedDate   = DateTime.Today;

            // Сотрудники
            var workers = _ctx.Workers.OrderBy(w => w.Name).Select(w => w.Name).ToList();
            CbRevWorker.ItemsSource = new[] { "Все сотрудники" }.Concat(workers).ToList();
            CbRevWorker.SelectedIndex = 0;

            // Статусы оплаты
            CbRevStatus.ItemsSource = new[] { "Все статусы", "paid", "unpaid" };
            CbRevStatus.SelectedIndex = 0;

            // Типы позиций меню
            CbMenuItemType.ItemsSource = new[] { "Всё", "product", "option" };
            CbMenuItemType.SelectedIndex = 0;

            // Роли/должности
            var roles = _ctx.Roles.OrderBy(r => r.Name).Select(r => r.Name).ToList();
            CbStaffRole.ItemsSource = new[] { "Все должности" }.Concat(roles).ToList();
            CbStaffRole.SelectedIndex = 0;
        }

        // ── Смена типа отчёта ────────────────────────────────────

        private void ReportType_Changed(object sender, RoutedEventArgs e)
        {
            if (PanelRevenueFilters == null) return;

            PanelRevenueFilters.Visibility = RbRevenue.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
            PanelMenuFilters.Visibility    = RbMenu.IsChecked    == true ? Visibility.Visible : Visibility.Collapsed;
            PanelStaffFilters.Visibility   = RbStaff.IsChecked   == true ? Visibility.Visible : Visibility.Collapsed;

            DocReader.Visibility       = Visibility.Collapsed;
            PlaceholderPanel.Visibility = Visibility.Visible;
            _currentDoc = null;
        }

        // ── Генерация отчёта ─────────────────────────────────────

        private void BtnGenerate_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                FlowDocument doc;

                if (RbRevenue.IsChecked == true)
                    doc = BuildRevenueReport();
                else if (RbMenu.IsChecked == true)
                    doc = BuildMenuReport();
                else
                    doc = BuildStaffReport();

                _currentDoc = doc;
                DocReader.Document = doc;
                DocReader.Visibility        = Visibility.Visible;
                PlaceholderPanel.Visibility = Visibility.Collapsed;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.InnerException?.Message ?? ex.Message,
                    "Ошибка формирования отчёта",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private FlowDocument BuildRevenueReport()
        {
            var dateFrom = DpRevFrom.SelectedDate
                ?? throw new InvalidOperationException("Укажите дату начала периода.");
            var dateTo = DpRevTo.SelectedDate
                ?? throw new InvalidOperationException("Укажите дату конца периода.");
            if (dateFrom > dateTo)
                throw new InvalidOperationException("Дата начала должна быть раньше даты окончания.");

            return ReportBuilder.BuildRevenueReport(
                _ctx,
                dateFrom, dateTo,
                CbRevWorker.SelectedItem?.ToString() ?? "Все сотрудники",
                CbRevStatus.SelectedItem?.ToString() ?? "Все статусы");
        }

        private FlowDocument BuildMenuReport()
        {
            var dateFrom = DpMenuFrom.SelectedDate
                ?? throw new InvalidOperationException("Укажите дату начала периода.");
            var dateTo = DpMenuTo.SelectedDate
                ?? throw new InvalidOperationException("Укажите дату конца периода.");
            if (dateFrom > dateTo)
                throw new InvalidOperationException("Дата начала должна быть раньше даты окончания.");

            if (!int.TryParse(TbTopN.Text, out int topN) || topN <= 0)
                topN = 10;

            return ReportBuilder.BuildMenuReport(
                _ctx,
                dateFrom, dateTo,
                CbMenuItemType.SelectedItem?.ToString() ?? "Всё",
                topN);
        }

        private FlowDocument BuildStaffReport()
        {
            var dateFrom = DpStaffFrom.SelectedDate
                ?? throw new InvalidOperationException("Укажите дату начала периода.");
            var dateTo = DpStaffTo.SelectedDate
                ?? throw new InvalidOperationException("Укажите дату конца периода.");
            if (dateFrom > dateTo)
                throw new InvalidOperationException("Дата начала должна быть раньше даты окончания.");

            return ReportBuilder.BuildStaffReport(
                _ctx,
                dateFrom, dateTo,
                CbStaffRole.SelectedItem?.ToString() ?? "Все должности");
        }

        // ── Печать ───────────────────────────────────────────────

        private void BtnPrint_Click(object sender, RoutedEventArgs e)
        {
            if (_currentDoc == null)
            {
                MessageBox.Show("Сначала сформируйте отчёт.",
                    "Нет отчёта", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var dlg = new PrintDialog();
            if (dlg.ShowDialog() != true) return;

            var paginator = ((IDocumentPaginatorSource)_currentDoc).DocumentPaginator;
            paginator.PageSize = new System.Windows.Size(
                dlg.PrintableAreaWidth, dlg.PrintableAreaHeight);
            dlg.PrintDocument(paginator, "KaraokeClub — Отчёт");
        }

        // ── Сохранить как XPS ────────────────────────────────────

        private void BtnSaveXps_Click(object sender, RoutedEventArgs e)
        {
            if (_currentDoc == null)
            {
                MessageBox.Show("Сначала сформируйте отчёт.",
                    "Нет отчёта", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var dlg = new SaveFileDialog
            {
                Title      = "Сохранить отчёт",
                Filter     = "XPS документ (*.xps)|*.xps",
                DefaultExt = ".xps",
                FileName   = $"KaraokeClub_Report_{DateTime.Now:yyyyMMdd_HHmm}.xps"
            };

            if (dlg.ShowDialog() != true) return;

            try
            {
                if (File.Exists(dlg.FileName))
                    File.Delete(dlg.FileName);

                using var xpsDoc = new XpsDocument(dlg.FileName, FileAccess.ReadWrite);
                var writer    = XpsDocument.CreateXpsDocumentWriter(xpsDoc);
                var paginator = ((IDocumentPaginatorSource)_currentDoc).DocumentPaginator;
                paginator.PageSize = new System.Windows.Size(1100, 850);
                writer.Write(paginator);

                MessageBox.Show($"Отчёт сохранён:\n{dlg.FileName}",
                    "Готово", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Ошибка сохранения",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ── Валидация ввода ──────────────────────────────────────

        private void OnlyDigits_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !e.Text.All(char.IsDigit);
        }
    }
}
