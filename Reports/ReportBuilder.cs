using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using KaraokeClub.Data;

namespace KaraokeClub.Reports
{
    /// <summary>
    /// Строит FlowDocument-отчёты для аналитики KaraokeClub.
    /// Данные загружаются исключительно через SQL-представления:
    ///   - vw_RevenueReport       → Отчёт по выручке
    ///   - vw_MenuSalesReport     → Анализ меню и Karaoke-опций
    ///   - vw_StaffWorkloadReport → Нагрузка на персонал
    /// </summary>
    public static class ReportBuilder
    {
        // ── Цветовая палитра ─────────────────────────────────────
        private static readonly SolidColorBrush HeaderBg = new(Color.FromRgb(74, 124, 31));
        private static readonly SolidColorBrush TableHead = new(Color.FromRgb(74, 124, 31));
        private static readonly SolidColorBrush AltRow = new(Color.FromRgb(245, 250, 240));
        private static readonly SolidColorBrush WarnBg = new(Color.FromRgb(255, 243, 224));
        private static readonly SolidColorBrush TextDark = new(Color.FromRgb(30, 30, 30));
        private static readonly SolidColorBrush TextGray = new(Color.FromRgb(100, 100, 100));
        private static readonly SolidColorBrush White = Brushes.White;
        private static readonly SolidColorBrush BorderBrush = new(Color.FromRgb(190, 215, 160));
        private static readonly SolidColorBrush TotalBg = new(Color.FromRgb(200, 224, 0));

        // ════════════════════════════════════════════════════════
        // ОТЧЁТ 1 — Выручка по заказам за период
        // Источник данных: представление vw_RevenueReport
        // ════════════════════════════════════════════════════════
        public static FlowDocument BuildRevenueReport(
            AppDbContext ctx,
            DateTime dateFrom,
            DateTime dateTo,
            string workerFilter,   // "Все сотрудники" или имя
            string statusFilter)   // "Все статусы" | "paid" | "unpaid"
        {
            var doc = CreateDocument();

            AddHeader(doc,
                "Отчёт по выручке за период",
                $"Период: {dateFrom:dd.MM.yyyy} — {dateTo:dd.MM.yyyy}   |   " +
                $"Сотрудник: {workerFilter}   |   Статус оплаты: {statusFilter}");

            // ── Загрузка через представление vw_RevenueReport ────
            var dfUtc = dateFrom.Date;
            var dtUtc = dateTo.Date.AddDays(1);

            var query = ctx.RevenueReportView
                .Where(r => r.BillDate >= dfUtc && r.BillDate < dtUtc);

            if (statusFilter != "Все статусы")
                query = query.Where(r => r.BillStatus == statusFilter);

            var rows = query.ToList();

            if (workerFilter != "Все сотрудники")
                rows = rows.Where(r => r.WorkerName == workerFilter).ToList();

            // ── KPI ──────────────────────────────────────────────
            var totalRev = rows.Sum(r => r.TotalAmount ?? 0);
            var paidRev = rows.Where(r => r.BillStatus == "paid").Sum(r => r.TotalAmount ?? 0);
            var unpaidRev = rows.Where(r => r.BillStatus == "unpaid").Sum(r => r.TotalAmount ?? 0);
            var billCount = rows.Count;
            var avgBill = billCount > 0 ? rows.Average(r => r.TotalAmount ?? 0) : 0;
            var avgGuests = rows.Any(r => r.GuestCount > 0)
                ? rows.Where(r => r.GuestCount > 0).Average(r => (double)r.GuestCount!.Value)
                : 0;

            AddSectionTitle(doc, "Ключевые показатели");
            var kpiTable = CreateTable(doc, new[] { "*", "*", "*", "*", "*", "*" });
            AddTableRow(kpiTable, true,
                "Общая выручка", "Оплачено", "Не оплачено",
                "Кол-во счетов", "Средний счёт", "Ср. гостей/стол");
            AddTableRow(kpiTable, false,
                $"{totalRev:F2} MDL", $"{paidRev:F2} MDL", $"{unpaidRev:F2} MDL",
                $"{billCount}", $"{avgBill:F2} MDL", $"{avgGuests:F1}");

            // ── Выручка по официантам ────────────────────────────
            AddSectionTitle(doc, "Выручка по официантам");
            var byWorker = rows
                .GroupBy(r => r.WorkerName)
                .Select(g => new {
                    Worker = g.Key,
                    Revenue = g.Sum(r => r.TotalAmount ?? 0),
                    Count = g.Count(),
                    Avg = g.Average(r => r.TotalAmount ?? 0)
                })
                .OrderByDescending(x => x.Revenue).ToList();

            var wTable = CreateTable(doc, new[] { "3*", "*", "*", "*" });
            AddTableRow(wTable, true, "Сотрудник", "Выручка (MDL)", "Счетов", "Средний счёт");
            bool alt = false;
            foreach (var w in byWorker)
            {
                AddTableRow(wTable, false,
                    w.Worker, $"{w.Revenue:F2}", $"{w.Count}", $"{w.Avg:F2}",
                    bg: alt ? AltRow : White);
                alt = !alt;
            }
            AddTotalRow(wTable, "ИТОГО", $"{totalRev:F2}", $"{billCount}", "");

            // ── Способы оплаты ───────────────────────────────────
            AddSectionTitle(doc, "Способы оплаты");
            var byPayment = rows
                .Where(r => r.BillStatus == "paid")
                .GroupBy(r => r.PaymentMethod ?? "—")
                .Select(g => new {
                    Method = g.Key,
                    Revenue = g.Sum(r => r.TotalAmount ?? 0),
                    Count = g.Count()
                })
                .OrderByDescending(x => x.Revenue).ToList();

            var pmTable = CreateTable(doc, new[] { "3*", "*", "*" });
            AddTableRow(pmTable, true, "Метод оплаты", "Выручка (MDL)", "Счетов");
            alt = false;
            foreach (var pm in byPayment)
            {
                AddTableRow(pmTable, false,
                    pm.Method, $"{pm.Revenue:F2}", $"{pm.Count}",
                    bg: alt ? AltRow : White);
                alt = !alt;
            }

            // ── Детализация счетов ───────────────────────────────
            AddSectionTitle(doc, "Детализация счетов");
            var dTable = CreateTable(doc, new[] { "*", "*", "2*", "*", "*", "*" });
            AddTableRow(dTable, true,
                "№ счёта", "Стол", "Официант",
                "Сумма (MDL)", "Статус", "Дата");
            alt = false;
            foreach (var r in rows)
            {
                var rowBg = r.BillStatus == "unpaid" ? WarnBg : (alt ? AltRow : White);
                AddTableRow(dTable, false,
                    $"{r.BillId}",
                    $"Стол {r.TableNumber}",
                    r.WorkerName,
                    $"{r.TotalAmount:F2}",
                    r.BillStatus == "paid" ? "Оплачен" : "Не оплачен",
                    $"{r.BillDate:dd.MM.yyyy HH:mm}",
                    rowBg);
                alt = !alt;
            }

            AddFooter(doc);
            return doc;
        }

        // ════════════════════════════════════════════════════════
        // ОТЧЁТ 2 — Анализ меню и Karaoke-опций
        // Источник данных: представление vw_MenuSalesReport
        // ════════════════════════════════════════════════════════
        public static FlowDocument BuildMenuReport(
            AppDbContext ctx,
            DateTime dateFrom,
            DateTime dateTo,
            string itemTypeFilter,  // "Всё" | "product" | "karaoke"
            int topN)
        {
            var doc = CreateDocument();

            AddHeader(doc,
                "Анализ продаж меню и Karaoke-опций",
                $"Период: {dateFrom:dd.MM.yyyy} — {dateTo:dd.MM.yyyy}   |   " +
                $"Тип: {(itemTypeFilter == "Всё" ? "Все позиции" : itemTypeFilter == "product" ? "Блюда и напитки" : "Karaoke-опции")}   |   Топ {topN}");

            // ── Загрузка через представление vw_MenuSalesReport ──
            var dfUtc = dateFrom.Date;
            var dtUtc = dateTo.Date.AddDays(1);

            var query = ctx.MenuSalesReportView
                .Where(r => r.OrderDate >= dfUtc && r.OrderDate < dtUtc);

            if (itemTypeFilter != "Всё")
                query = query.Where(r => r.ItemType == itemTypeFilter);

            var rows = query.ToList();

            // ── Сводка product vs karaoke ────────────────────────
            AddSectionTitle(doc, "Сводка по типам позиций");
            var byType = rows
                .GroupBy(r => r.ItemType == "product" ? "Блюда / Напитки" : "Karaoke-опции")
                .Select(g => new {
                    Type = g.Key,
                    Revenue = g.Sum(r => r.LineTotal),
                    Qty = g.Sum(r => r.Quantity),
                    Avg = g.Average(r => r.PriceAtOrder)
                }).ToList();

            var typeTable = CreateTable(doc, new[] { "2*", "*", "*", "*" });
            AddTableRow(typeTable, true, "Тип", "Выручка (MDL)", "Кол-во ед.", "Ср. цена");
            foreach (var t in byType)
                AddTableRow(typeTable, false,
                    t.Type, $"{t.Revenue:F2}", $"{t.Qty}", $"{t.Avg:F2}");

            // ── Топ N блюд ───────────────────────────────────────
            var topProducts = rows
                .Where(r => r.ItemType == "product" && r.ProductName != null)
                .GroupBy(r => new { r.ProductId, r.ProductName, r.TypeName, r.ProductSection })
                .Select(g => new {
                    g.Key.ProductName,
                    TypeName = g.Key.TypeName ?? "—",
                    Section = g.Key.ProductSection ?? "—",
                    TotalQty = g.Sum(r => r.Quantity),
                    Revenue = g.Sum(r => r.LineTotal),
                    AvgPrice = g.Average(r => r.PriceAtOrder)
                })
                .OrderByDescending(x => x.Revenue)
                .Take(topN).ToList();

            AddSectionTitle(doc, $"Топ {topN} блюд и напитков по выручке");
            var pTable = CreateTable(doc, new[] { "3*", "2*", "2*", "*", "*" });
            AddTableRow(pTable, true,
                "Позиция", "Тип", "Раздел", "Кол-во", "Выручка (MDL)");
            bool alt = false;
            int rank = 1;
            foreach (var p in topProducts)
            {
                AddTableRow(pTable, false,
                    $"{rank++}. {p.ProductName}", p.TypeName, p.Section,
                    $"{p.TotalQty}", $"{p.Revenue:F2}",
                    bg: alt ? AltRow : White);
                alt = !alt;
            }
            var prodTotal = rows.Where(r => r.ItemType == "product").Sum(r => r.LineTotal);
            AddTotalRow(pTable, "ИТОГО (блюда)", "", "",
                $"{rows.Where(r => r.ItemType == "product").Sum(r => r.Quantity)}",
                $"{prodTotal:F2}");

            // ── Топ N Karaoke-опций ──────────────────────────────
            var topOptions = rows
                .Where(r => r.ItemType == "karaoke" && r.OptionName != null)
                .GroupBy(r => new { r.OptionId, r.OptionName })
                .Select(g => new {
                    g.Key.OptionName,
                    TotalQty = g.Sum(r => r.Quantity),
                    Revenue = g.Sum(r => r.LineTotal),
                    AvgPrice = g.Average(r => r.PriceAtOrder)
                })
                .OrderByDescending(x => x.Revenue)
                .Take(topN).ToList();

            if (topOptions.Any())
            {
                AddSectionTitle(doc, $"Топ {topN} Karaoke-опций по выручке");
                var kTable = CreateTable(doc, new[] { "3*", "*", "*", "*" });
                AddTableRow(kTable, true,
                    "Опция", "Кол-во", "Выручка (MDL)", "Ср. цена");
                alt = false;
                rank = 1;
                foreach (var k in topOptions)
                {
                    AddTableRow(kTable, false,
                        $"{rank++}. {k.OptionName}",
                        $"{k.TotalQty}", $"{k.Revenue:F2}", $"{k.AvgPrice:F2}",
                        bg: alt ? AltRow : White);
                    alt = !alt;
                }
                var optTotal = rows.Where(r => r.ItemType == "karaoke").Sum(r => r.LineTotal);
                AddTotalRow(kTable, "ИТОГО (опции)",
                    $"{rows.Where(r => r.ItemType == "karaoke").Sum(r => r.Quantity)}",
                    $"{optTotal:F2}", "");
            }

            // ── По разделам меню ─────────────────────────────────
            AddSectionTitle(doc, "Выручка по разделам меню");
            var bySection = rows
                .Where(r => r.ItemType == "product" && r.ProductSection != null)
                .GroupBy(r => r.ProductSection!)
                .Select(g => new {
                    Section = g.Key,
                    Revenue = g.Sum(r => r.LineTotal),
                    Qty = g.Sum(r => r.Quantity)
                })
                .OrderByDescending(x => x.Revenue).ToList();

            var secTable = CreateTable(doc, new[] { "3*", "*", "*" });
            AddTableRow(secTable, true, "Раздел меню", "Выручка (MDL)", "Кол-во ед.");
            alt = false;
            foreach (var s in bySection)
            {
                AddTableRow(secTable, false,
                    s.Section, $"{s.Revenue:F2}", $"{s.Qty}",
                    bg: alt ? AltRow : White);
                alt = !alt;
            }

            AddFooter(doc);
            return doc;
        }

        // ════════════════════════════════════════════════════════
        // ОТЧЁТ 3 — Нагрузка на персонал
        // Источник данных: представление vw_StaffWorkloadReport
        // ════════════════════════════════════════════════════════
        public static FlowDocument BuildStaffReport(
            AppDbContext ctx,
            DateTime dateFrom,
            DateTime dateTo,
            string roleFilter)   // "Все должности" или название роли
        {
            var doc = CreateDocument();

            AddHeader(doc,
                "Отчёт по нагрузке на персонал",
                $"Период: {dateFrom:dd.MM.yyyy} — {dateTo:dd.MM.yyyy}   |   " +
                $"Должность: {roleFilter}   |   Дата формирования: {DateTime.Now:dd.MM.yyyy HH:mm}");

            // ── Загрузка через vw_StaffWorkloadReport ────────────
            var dfUtc = dateFrom.Date;
            var dtUtc = dateTo.Date.AddDays(1);

            var query = ctx.StaffWorkloadReportView
                .Where(r => r.OrderDate >= dfUtc && r.OrderDate < dtUtc);

            if (roleFilter != "Все должности")
                query = query.Where(r => r.RoleName == roleFilter);

            var rows = query.ToList();

            // ── Общая сводка ─────────────────────────────────────
            var totalOrders = rows.Count;
            var closedOrders = rows.Count(r => r.OrderStatus == "closed");
            var openOrders = rows.Count(r => r.OrderStatus == "open");
            var totalRev = rows.Sum(r => r.TotalAmount ?? 0);
            var avgItems = totalOrders > 0 ? rows.Average(r => (double)r.ItemCount) : 0;

            AddSectionTitle(doc, "Общая сводка за период");
            var summTable = CreateTable(doc, new[] { "*", "*", "*", "*", "*" });
            AddTableRow(summTable, true,
                "Всего заказов", "Закрытых", "Открытых",
                "Общая выручка", "Ср. позиций/заказ");
            AddTableRow(summTable, false,
                $"{totalOrders}", $"{closedOrders}", $"{openOrders}",
                $"{totalRev:F2} MDL", $"{avgItems:F1}");

            // ── Нагрузка по сотрудникам ──────────────────────────
            AddSectionTitle(doc, "Нагрузка по сотрудникам");
            var byWorker = rows
                .GroupBy(r => new { r.WorkerId, r.WorkerName, r.RoleName })
                .Select(g => new {
                    g.Key.WorkerName,
                    g.Key.RoleName,
                    OrderCount = g.Count(),
                    Revenue = g.Sum(r => r.TotalAmount ?? 0),
                    AvgGuests = g.Any(r => r.GuestCount > 0)
                                    ? g.Where(r => r.GuestCount > 0).Average(r => (double)r.GuestCount!.Value)
                                    : 0,
                    AvgItems = g.Average(r => (double)r.ItemCount),
                    ClosedCount = g.Count(r => r.OrderStatus == "closed")
                })
                .OrderByDescending(x => x.Revenue).ToList();

            var wTable = CreateTable(doc, new[] { "2*", "2*", "*", "*", "*", "*" });
            AddTableRow(wTable, true,
                "Сотрудник", "Должность",
                "Заказов", "Закрыто", "Ср. гостей", "Выручка (MDL)");
            bool alt = false;
            foreach (var w in byWorker)
            {
                AddTableRow(wTable, false,
                    w.WorkerName, w.RoleName,
                    $"{w.OrderCount}", $"{w.ClosedCount}",
                    $"{w.AvgGuests:F1}", $"{w.Revenue:F2}",
                    bg: alt ? AltRow : White);
                alt = !alt;
            }
            AddTotalRow(wTable, "ИТОГО", "", $"{totalOrders}", $"{closedOrders}", "", $"{totalRev:F2}");

            // ── Нагрузка по должностям ───────────────────────────
            AddSectionTitle(doc, "Нагрузка по должностям");
            var byRole = rows
                .GroupBy(r => r.RoleName)
                .Select(g => new {
                    Role = g.Key,
                    OrderCount = g.Count(),
                    Revenue = g.Sum(r => r.TotalAmount ?? 0),
                    WorkerCount = g.Select(r => r.WorkerId).Distinct().Count()
                })
                .OrderByDescending(x => x.Revenue).ToList();

            var rTable = CreateTable(doc, new[] { "2*", "*", "*", "*" });
            AddTableRow(rTable, true,
                "Должность", "Сотрудников", "Заказов", "Выручка (MDL)");
            alt = false;
            foreach (var r in byRole)
            {
                AddTableRow(rTable, false,
                    r.Role, $"{r.WorkerCount}", $"{r.OrderCount}", $"{r.Revenue:F2}",
                    bg: alt ? AltRow : White);
                alt = !alt;
            }

            // ── Загруженность столов ─────────────────────────────
            AddSectionTitle(doc, "Загруженность столов");
            var byTable = rows
                .GroupBy(r => r.TableNumber)
                .Select(g => new {
                    Table = g.Key,
                    Sessions = g.Count(),
                    Revenue = g.Sum(r => r.TotalAmount ?? 0),
                    AvgGuests = g.Any(r => r.GuestCount > 0)
                                    ? g.Where(r => r.GuestCount > 0).Average(r => (double)r.GuestCount!.Value)
                                    : 0
                })
                .OrderBy(x => x.Table).ToList();

            var tTable = CreateTable(doc, new[] { "*", "*", "*", "*" });
            AddTableRow(tTable, true,
                "Стол №", "Сессий", "Ср. гостей", "Выручка (MDL)");
            alt = false;
            foreach (var t in byTable)
            {
                AddTableRow(tTable, false,
                    $"Стол {t.Table}", $"{t.Sessions}",
                    $"{t.AvgGuests:F1}", $"{t.Revenue:F2}",
                    bg: alt ? AltRow : White);
                alt = !alt;
            }
            AddTotalRow(tTable, "ИТОГО", $"{totalOrders}", "", $"{totalRev:F2}");

            AddFooter(doc);
            return doc;
        }

        // ════════════════════════════════════════════════════════
        //  ВСПОМОГАТЕЛЬНЫЕ МЕТОДЫ (без изменений)
        // ════════════════════════════════════════════════════════

        private static FlowDocument CreateDocument()
        {
            return new FlowDocument
            {
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 11,
                Foreground = TextDark,
                Background = Brushes.White,
                PagePadding = new Thickness(40, 30, 40, 30),
                ColumnWidth = double.PositiveInfinity,
                PageWidth = 1100
            };
        }

        private static void AddHeader(FlowDocument doc, string title, string subtitle)
        {
            var headerBorder = new BlockUIContainer(new System.Windows.Controls.Border
            {
                Background = HeaderBg,
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(20, 14, 20, 14),
                Child = new System.Windows.Controls.StackPanel
                {
                    Children =
                    {
                        new System.Windows.Controls.TextBlock
                        {
                            Text       = "PARK ZONE — Система управления",
                            Foreground = new SolidColorBrush(Color.FromRgb(200, 224, 0)),
                            FontSize   = 10,
                            FontFamily = new FontFamily("Segoe UI")
                        },
                        new System.Windows.Controls.TextBlock
                        {
                            Text       = title,
                            Foreground = Brushes.White,
                            FontSize   = 20,
                            FontWeight = FontWeights.Bold,
                            FontFamily = new FontFamily("Segoe UI"),
                            Margin     = new Thickness(0, 4, 0, 0)
                        },
                        new System.Windows.Controls.TextBlock
                        {
                            Text         = subtitle,
                            Foreground   = new SolidColorBrush(Color.FromRgb(210, 237, 160)),
                            FontSize     = 10,
                            FontFamily   = new FontFamily("Segoe UI"),
                            Margin       = new Thickness(0, 6, 0, 0),
                            TextWrapping = System.Windows.TextWrapping.Wrap
                        },
                        new System.Windows.Controls.TextBlock
                        {
                            Text       = $"Сформировано: {DateTime.Now:dd.MM.yyyy HH:mm:ss}",
                            Foreground = new SolidColorBrush(Color.FromRgb(210, 237, 160)),
                            FontSize   = 9,
                            FontFamily = new FontFamily("Segoe UI"),
                            Margin     = new Thickness(0, 4, 0, 0)
                        }
                    }
                }
            });
            doc.Blocks.Add(headerBorder);
            doc.Blocks.Add(new Paragraph { Margin = new Thickness(0, 8, 0, 0) });
        }

        private static void AddSectionTitle(FlowDocument doc, string text)
        {
            doc.Blocks.Add(new Paragraph(new Run(text))
            {
                FontSize = 13,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(74, 124, 31)),
                Margin = new Thickness(0, 14, 0, 4),
                BorderBrush = new SolidColorBrush(Color.FromRgb(190, 215, 160)),
                BorderThickness = new Thickness(0, 0, 0, 1),
                Padding = new Thickness(0, 0, 0, 3)
            });
        }

        private static Table CreateTable(FlowDocument doc, string[] columnWidths)
        {
            var table = new Table
            {
                CellSpacing = 0,
                BorderBrush = BorderBrush,
                BorderThickness = new Thickness(1),
                Margin = new Thickness(0, 0, 0, 4)
            };

            foreach (var w in columnWidths)
            {
                var col = new TableColumn();
                if (w == "*")
                    col.Width = new GridLength(1, GridUnitType.Star);
                else if (w.EndsWith("*") && double.TryParse(w[..^1], out double d))
                    col.Width = new GridLength(d, GridUnitType.Star);
                else if (double.TryParse(w, out double px))
                    col.Width = new GridLength(px);
                table.Columns.Add(col);
            }

            var rowGroup = new TableRowGroup();
            table.RowGroups.Add(rowGroup);
            doc.Blocks.Add(table);
            return table;
        }

        // Перегрузка с именованным параметром bg
        private static void AddTableRow(Table table, bool isHeader,
            string c1, string c2, string c3 = "", string c4 = "",
            string c5 = "", string c6 = "",
            SolidColorBrush? bg = null)
        {
            var cells = new List<string> { c1, c2 };
            if (table.Columns.Count > 2) cells.Add(c3);
            if (table.Columns.Count > 3) cells.Add(c4);
            if (table.Columns.Count > 4) cells.Add(c5);
            if (table.Columns.Count > 5) cells.Add(c6);
            AddTableRowCore(table, isHeader, cells.ToArray(), bg);
        }

        // Перегрузка с массивом ячеек
        private static void AddTableRow(Table table, bool isHeader, params string[] cells)
            => AddTableRowCore(table, isHeader, cells, null);

        private static void AddTableRowCore(Table table, bool isHeader,
            string[] cells, SolidColorBrush? bg)
        {
            var row = new TableRow
            {
                Background = isHeader ? TableHead : (bg ?? White)
            };

            foreach (var cell in cells)
            {
                var para = new Paragraph(new Run(cell ?? ""))
                {
                    Margin = new Thickness(8, 4, 8, 4),
                    FontWeight = isHeader ? FontWeights.SemiBold : FontWeights.Normal,
                    FontSize = 11
                };
                if (isHeader) para.Foreground = Brushes.White;

                var tc = new TableCell(para)
                {
                    BorderBrush = BorderBrush,
                    BorderThickness = new Thickness(0, 0, 1, 1)
                };
                row.Cells.Add(tc);
            }

            table.RowGroups[0].Rows.Add(row);
        }

        private static void AddTotalRow(Table table, params string[] cells)
        {
            var row = new TableRow { Background = TotalBg };

            for (int i = 0; i < table.Columns.Count; i++)
            {
                var text = i < cells.Length ? cells[i] : "";
                var para = new Paragraph(new Run(text))
                {
                    Margin = new Thickness(8, 4, 8, 4),
                    FontWeight = FontWeights.Bold,
                    FontSize = 11,
                    Foreground = new SolidColorBrush(Color.FromRgb(30, 30, 30))
                };
                row.Cells.Add(new TableCell(para)
                {
                    BorderBrush = BorderBrush,
                    BorderThickness = new Thickness(0, 1, 1, 1)
                });
            }

            table.RowGroups[0].Rows.Add(row);
        }

        private static void AddFooter(FlowDocument doc)
        {
            doc.Blocks.Add(new Paragraph { Margin = new Thickness(0, 20, 0, 0) });
            doc.Blocks.Add(new Paragraph(
                new Run($"© {DateTime.Now.Year} PARK ZONE — Система управления   |   " +
                        $"Документ сформирован: {DateTime.Now:dd.MM.yyyy HH:mm}"))
            {
                Foreground = TextGray,
                FontSize = 9,
                TextAlignment = TextAlignment.Center,
                BorderBrush = new SolidColorBrush(Color.FromRgb(190, 215, 160)),
                BorderThickness = new Thickness(0, 1, 0, 0),
                Padding = new Thickness(0, 6, 0, 0)
            });
        }
    }
}
