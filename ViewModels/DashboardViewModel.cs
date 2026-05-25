// ViewModels/DashboardViewModel.cs
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Input;
using KaraokeClub.Commands;
using KaraokeClub.Data;
using KaraokeClub.Models;
using Microsoft.EntityFrameworkCore;

namespace KaraokeClub.ViewModels
{
    public class DashboardViewModel : BaseViewModel
    {
        private readonly AppDbContext _ctx;

        // ── Фильтры ──────────────────────────────────────────────
        private DateTime _dateFrom = DateTime.Today.AddMonths(-1);
        public DateTime DateFrom
        {
            get => _dateFrom;
            set { SetProperty(ref _dateFrom, value); }
        }

        private DateTime _dateTo = DateTime.Today;
        public DateTime DateTo
        {
            get => _dateTo;
            set { SetProperty(ref _dateTo, value); }
        }

        private string _selectedStatus = "Все";
        public string SelectedStatus
        {
            get => _selectedStatus;
            set { SetProperty(ref _selectedStatus, value); }
        }

        private string _selectedWorker = "Все";
        public string SelectedWorker
        {
            get => _selectedWorker;
            set { SetProperty(ref _selectedWorker, value); }
        }

        private decimal _minAmount = 0;
        public decimal MinAmount
        {
            get => _minAmount;
            set { SetProperty(ref _minAmount, value); }
        }

        private decimal _maxAmount = 999999;
        public decimal MaxAmount
        {
            get => _maxAmount;
            set { SetProperty(ref _maxAmount, value); }
        }

        // ── Статус-строка ─────────────────────────────────────────
        private string _statusText = "";
        public string StatusText
        {
            get => _statusText;
            set => SetProperty(ref _statusText, value);
        }

        private string _lastUpdated = "";
        public string LastUpdated
        {
            get => _lastUpdated;
            set => SetProperty(ref _lastUpdated, value);
        }

        // ── Списки для ComboBox ───────────────────────────────────
        public ObservableCollection<string> StatusList { get; } =
            new() { "Все", "open", "closed", "cancelled" };

        public ObservableCollection<string> WorkerList { get; } = new();

        // ── KPI-карточки ─────────────────────────────────────────
        public ObservableCollection<KpiCard> KpiCards { get; } = new();

        // ── Данные для таблицы ────────────────────────────────────
        public ObservableCollection<OrderSummary> FilteredOrders { get; } = new();

        // ── Данные для графиков ───────────────────────────────────
        public ObservableCollection<ChartPoint> RevenueByDay { get; } = new();
        public ObservableCollection<PieSlice> OrdersByStatus { get; } = new();
        public ObservableCollection<ChartPoint> RevenueByWorker { get; } = new();

        // ── Команды ───────────────────────────────────────────────
        public ICommand ApplyFiltersCommand { get; }
        public ICommand ResetFiltersCommand { get; }
        public ICommand ExportCommand { get; }

        // ─────────────────────────────────────────────────────────
        public DashboardViewModel(AppDbContext ctx)
        {
            _ctx = ctx;

            WorkerList.Add("Все");
            foreach (var w in ctx.Workers.AsNoTracking().ToList())
                WorkerList.Add(w.Name);

            ApplyFiltersCommand = new RelayCommand(_ => ApplyFilters());
            ResetFiltersCommand = new RelayCommand(_ => ResetFilters());
            ExportCommand = new RelayCommand(_ => ExportCsv());

            ApplyFilters();
        }

        private void ResetFilters()
        {
            DateFrom = DateTime.Today.AddMonths(-1);
            DateTo = DateTime.Today;
            SelectedStatus = "Все";
            SelectedWorker = "Все";
            MinAmount = 0;
            MaxAmount = 999999;
            ApplyFilters();
        }

        public void ApplyFilters()
        {
            var query = _ctx.Orders
                .AsNoTracking()
                .Include(o => o.Worker)
                .Include(o => o.Bill)
                .Where(o => o.CreatedAt >= DateFrom && o.CreatedAt <= DateTo.AddDays(1));

            if (SelectedStatus != "Все")
                query = query.Where(o => o.Status == SelectedStatus);

            if (SelectedWorker != "Все")
                query = query.Where(o => o.Worker != null && o.Worker.Name == SelectedWorker);

            var orders = query.ToList();

            var filtered = orders
                .Where(o => (o.Bill?.TotalAmount ?? 0) >= MinAmount
                         && (o.Bill?.TotalAmount ?? 999999) <= MaxAmount)
                .ToList();

            // ── KPI ──
            int totalOrders = filtered.Count;
            decimal totalRev = filtered.Sum(o => o.Bill?.TotalAmount ?? 0);
            decimal avgBill = totalOrders > 0 ? totalRev / totalOrders : 0;
            decimal maxBill = filtered.Any() ? filtered.Max(o => o.Bill?.TotalAmount ?? 0) : 0;
            decimal minBill = filtered.Any(o => o.Bill != null)
                                      ? filtered.Where(o => o.Bill != null).Min(o => o.Bill!.TotalAmount ?? 0)
                                      : 0;
            int openOrders = filtered.Count(o => o.Status == "open");

            KpiCards.Clear();
            KpiCards.Add(new KpiCard { Icon = "💰", Title = "Выручка", Value = $"{totalRev:N2} lei", Color = "#5E35B1", SubValue = $"за период" });
            KpiCards.Add(new KpiCard { Icon = "📋", Title = "Заказов", Value = $"{totalOrders}", Color = "#2E7D32", SubValue = $"открытых: {openOrders}" });
            KpiCards.Add(new KpiCard { Icon = "📊", Title = "Средний чек", Value = $"{avgBill:N2} lei", Color = "#E65100", SubValue = "avg" });
            KpiCards.Add(new KpiCard { Icon = "🔺", Title = "Макс. чек", Value = $"{maxBill:N2} lei", Color = "#C62828", SubValue = "max" });
            KpiCards.Add(new KpiCard { Icon = "🔻", Title = "Мин. чек", Value = $"{minBill:N2} lei", Color = "#4A148C", SubValue = "min" });

            // ── Таблица ──
            FilteredOrders.Clear();
            foreach (var o in filtered.OrderByDescending(x => x.CreatedAt))
                FilteredOrders.Add(new OrderSummary
                {
                    OrderId = o.Id,
                    Date = o.CreatedAt,
                    Worker = o.Worker?.Name ?? "—",
                    Status = o.Status,
                    GuestCount = o.GuestCount ?? 0,
                    TableNumber = o.TableNumber,
                    TotalAmount = o.Bill?.TotalAmount ?? 0,
                    PaymentMethod = o.Bill?.PaymentMethod ?? "—"
                });

            // ── График 1: выручка по дням ──
            RevenueByDay.Clear();
            var byDay = filtered
                .GroupBy(o => o.CreatedAt.Date)
                .Select(g => new ChartPoint
                {
                    Label = g.Key.ToString("dd.MM"),
                    Value = (double)g.Sum(o => o.Bill?.TotalAmount ?? 0),
                    Color = "#5E35B1",
                    Tooltip = $"{g.Key:dd.MM.yyyy}: {g.Sum(o => o.Bill?.TotalAmount ?? 0):N2} lei"
                })
                .OrderBy(p => p.Label);
            foreach (var p in byDay) RevenueByDay.Add(p);

            // ── График 2: по статусам (Pie) ──
            var statusColors = new[] { "#5E35B1", "#2E7D32", "#E65100", "#C62828", "#1565C0" };
            OrdersByStatus.Clear();
            var byStatus = filtered
                .GroupBy(o => o.Status)
                .Select((g, idx) => new { g.Key, Count = g.Count(), Idx = idx })
                .ToList();
            double totalForPie = byStatus.Sum(x => x.Count);
            for (int i = 0; i < byStatus.Count; i++)
            {
                var s = byStatus[i];
                OrdersByStatus.Add(new PieSlice
                {
                    Label = s.Key,
                    Value = s.Count,
                    Percentage = totalForPie > 0 ? s.Count / totalForPie * 100 : 0,
                    Color = statusColors[i % statusColors.Length],
                    Tooltip = $"{s.Key}: {s.Count} заказ(ов)"
                });
            }

            // ── График 3: выручка по сотрудникам ──
            var workerColors = new[] { "#1565C0", "#2E7D32", "#E65100", "#C62828", "#5E35B1", "#00695C" };
            RevenueByWorker.Clear();
            var byWorker = filtered
                .GroupBy(o => o.Worker?.Name ?? "Неизвестно")
                .Select((g, idx) => new { g.Key, Rev = g.Sum(o => o.Bill?.TotalAmount ?? 0), Idx = idx })
                .OrderByDescending(x => x.Rev)
                .ToList();
            for (int i = 0; i < byWorker.Count; i++)
            {
                var wr = byWorker[i];
                RevenueByWorker.Add(new ChartPoint
                {
                    Label = wr.Key,
                    Value = (double)wr.Rev,
                    Color = workerColors[i % workerColors.Length],
                    Tooltip = $"{wr.Key}: {wr.Rev:N2} lei"
                });
            }

            StatusText = $"Показано {totalOrders} заказов • {DateFrom:dd.MM.yyyy} — {DateTo:dd.MM.yyyy}";
            LastUpdated = $"Обновлено: {DateTime.Now:HH:mm:ss}";
        }

        private void ExportCsv()
        {
            try
            {
                var dlg = new Microsoft.Win32.SaveFileDialog
                {
                    Filter = "CSV файл|*.csv",
                    FileName = $"karaoke_dashboard_{DateTime.Today:yyyyMMdd}.csv"
                };
                if (dlg.ShowDialog() != true) return;

                var sb = new StringBuilder();
                sb.AppendLine("ID,Дата,Стол,Сотрудник,Статус,Гостей,Сумма,Оплата");
                foreach (var o in FilteredOrders)
                    sb.AppendLine($"{o.OrderId},{o.Date:dd.MM.yyyy HH:mm},{o.TableNumber},{o.Worker},{o.Status},{o.GuestCount},{o.TotalAmount:F2},{o.PaymentMethod}");

                File.WriteAllText(dlg.FileName, sb.ToString(), Encoding.UTF8);
                MessageBox.Show($"Экспорт завершён:\n{dlg.FileName}", "Готово",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка экспорта: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    // ── Вспомогательные классы ────────────────────────────────────────
    public class OrderSummary
    {
        public int OrderId { get; set; }
        public DateTime Date { get; set; }
        public string Worker { get; set; } = "";
        public string Status { get; set; } = "";
        public int GuestCount { get; set; }
        public int TableNumber { get; set; }
        public decimal TotalAmount { get; set; }
        public string PaymentMethod { get; set; } = "";
    }

    public class ChartPoint
    {
        public string Label { get; set; } = "";
        public double Value { get; set; }
        public string Color { get; set; } = "#5E35B1";
        public string Tooltip { get; set; } = "";
    }

    public class PieSlice
    {
        public string Label { get; set; } = "";
        public double Value { get; set; }
        public double Percentage { get; set; }
        public string Color { get; set; } = "#5E35B1";
        public string Tooltip { get; set; } = "";
    }

    public class KpiCard
    {
        public string Icon { get; set; } = "";
        public string Title { get; set; } = "";
        public string Value { get; set; } = "";
        public string SubValue { get; set; } = "";
        public string Color { get; set; } = "#5E35B1";
    }
}