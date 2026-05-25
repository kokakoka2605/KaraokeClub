using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using Microsoft.EntityFrameworkCore;
using KaraokeClub.Data;
using KaraokeClub.Models;

namespace KaraokeClub.Reports
{
    /// <summary>
    /// Строит FlowDocument-отчёты для аналитики KaraokeClub.
    /// Все стили задаются программно — никаких внешних словарей не нужно.
    /// </summary>
    public static class ReportBuilder
    {
        // ── Цветовая палитра (светлая тема, акценты проекта) ─────────────
        private static readonly SolidColorBrush HeaderBg = new(Color.FromRgb(74, 124, 31));    // #4a7c1f — основной зелёный
        private static readonly SolidColorBrush SubHeaderBg = new(Color.FromRgb(90, 150, 37));    // #5a9625 — зелёный hover
        private static readonly SolidColorBrush TableHead = new(Color.FromRgb(74, 124, 31));    // зелёная шапка таблицы
        private static readonly SolidColorBrush AltRow = new(Color.FromRgb(245, 250, 240));  // очень светло-зелёный
        private static readonly SolidColorBrush WarnBg = new(Color.FromRgb(255, 243, 224));  // предупреждение — оранжеватый
        private static readonly SolidColorBrush AccentBg = new(Color.FromRgb(220, 237, 200));  // светло-зелёный акцент
        private static readonly SolidColorBrush TextDark = new(Color.FromRgb(30, 30, 30));     // #1e1e1e
        private static readonly SolidColorBrush TextGray = new(Color.FromRgb(100, 100, 100));  // #646464
        private static readonly SolidColorBrush White = Brushes.White;
        private static readonly SolidColorBrush BorderBrush = new(Color.FromRgb(190, 215, 160));  // зелёная рамка таблицы
        private static readonly SolidColorBrush TotalBg = new(Color.FromRgb(200, 224, 0));    // #c8e000 — итоговая строка

        // ══════════════════════════════════════════════════════════
        // ОТЧЁТ 1 — Выручка по заказам за период
        // ══════════════════════════════════════════════════════════
        public static FlowDocument BuildRevenueReport(
            AppDbContext ctx,
            DateTime dateFrom,
            DateTime dateTo,
            string workerFilter,    // "Все сотрудники" или имя
            string statusFilter)    // "Все статусы" | "paid" | "unpaid"
        {
            var doc = CreateDocument();

            AddHeader(doc, "Отчёт по выручке за период",
                $"Период: {dateFrom:dd.MM.yyyy} — {dateTo:dd.MM.yyyy}   |   " +
                $"Сотрудник: {workerFilter}   |   Статус оплаты: {statusFilter}");

            // Загружаем счета за период
            var dfUtc = dateFrom.Date;
            var dtUtc = dateTo.Date.AddDays(1);

            var billsQ = ctx.Bills
                .Include(b => b.Order).ThenInclude(o => o!.Worker)
                .Include(b => b.Order).ThenInclude(o => o!.OrderItems)
                .Where(b => b.CreatedAt >= dfUtc && b.CreatedAt < dtUtc);

            if (statusFilter != "Все статусы")
                billsQ = billsQ.Where(b => b.BillStatus == statusFilter);

            var bills = billsQ.OrderBy(b => b.CreatedAt).ToList();

            if (workerFilter != "Все сотрудники")
                bills = bills.Where(b => b.Order?.Worker?.Name == workerFilter).ToList();

            // ── KPI ──────────────────────────────────────────────
            var totalRev = bills.Sum(b => b.TotalAmount ?? 0);
            var paidRev = bills.Where(b => b.BillStatus == "paid").Sum(b => b.TotalAmount ?? 0);
            var unpaidRev = bills.Where(b => b.BillStatus == "unpaid").Sum(b => b.TotalAmount ?? 0);
            var billCount = bills.Count;
            var avgBill = billCount > 0 ? bills.Average(b => b.TotalAmount ?? 0) : 0;
            var avgGuests = bills.Any(b => b.Order?.GuestCount > 0)
                                ? bills.Where(b => b.Order?.GuestCount > 0)
                                       .Average(b => b.Order!.GuestCount!.Value)
                                : 0;

            AddSectionTitle(doc, "Ключевые показатели");
            var kpiTable = CreateTable(doc, new[] { "*", "*", "*", "*", "*", "*" });
            AddTableRow(kpiTable, true,
                "Общая выручка", "Оплачено", "Не оплачено",
                "Кол-во счетов", "Средний счёт", "Ср. гостей/стол");
            AddTableRow(kpiTable, false,
                $"{totalRev:F2} MDL", $"{paidRev:F2} MDL", $"{unpaidRev:F2} MDL",
                $"{billCount}", $"{avgBill:F2} MDL", $"{avgGuests:F1}");

            // ── Выручка по сотрудникам ───────────────────────────
            AddSectionTitle(doc, "Выручка по официантам");
            var byWorker = bills
                .GroupBy(b => b.Order?.Worker?.Name ?? "—")
                .Select(g => new {
                    Worker = g.Key,
                    Revenue = g.Sum(b => b.TotalAmount ?? 0),
                    Count = g.Count(),
                    Avg = g.Average(b => b.TotalAmount ?? 0)
                })
                .OrderByDescending(x => x.Revenue).ToList();

            var wTable = CreateTable(doc, new[] { "3*", "*", "*", "*" });
            AddTableRow(wTable, true, "Сотрудник", "Выручка (MDL)", "Счетов", "Средний счёт");
            bool alt = false;
            foreach (var w in byWorker)
            {
                AddTableRow(wTable, false,
                    w.Worker, $"{w.Revenue:F2}", $"{w.Count}", $"{w.Avg:F2}",
                    "", "",
                    alt ? AltRow : White);
                alt = !alt;
            }
            AddTotalRow(wTable, "ИТОГО", $"{totalRev:F2}", $"{billCount}", "");

            // ── Выручка по методу оплаты ─────────────────────────
            AddSectionTitle(doc, "Способы оплаты");
            var byPayment = bills
                .Where(b => b.BillStatus == "paid")
                .GroupBy(b => b.PaymentMethod ?? "—")
                .Select(g => new {
                    Method = g.Key,
                    Revenue = g.Sum(b => b.TotalAmount ?? 0),
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
                    "", "", "",
                    alt ? AltRow : White);
                alt = !alt;
            }

            // ── Детализация счетов ───────────────────────────────
            AddSectionTitle(doc, "Детализация счетов");
            var dTable = CreateTable(doc, new[] { "*", "*", "2*", "*", "*", "*" });
            AddTableRow(dTable, true,
                "№ счёта", "Стол", "Официант",
                "Сумма (MDL)", "Статус", "Дата");
            alt = false;
            foreach (var b in bills)
            {
                var rowBg = b.BillStatus == "unpaid" ? WarnBg : (alt ? AltRow : White);
                AddTableRow(dTable, false,
                    $"{b.Id}",
                    $"Стол {b.Order?.TableNumber ?? 0}",
                    b.Order?.Worker?.Name ?? "—",
                    $"{b.TotalAmount:F2}",
                    b.BillStatus == "paid" ? "Оплачен" : "Не оплачен",
                    $"{b.CreatedAt:dd.MM.yyyy HH:mm}",
                    rowBg);
                alt = !alt;
            }

            AddFooter(doc);
            return doc;
        }

        // ══════════════════════════════════════════════════════════
        // ОТЧЁТ 2 — Анализ меню и Karaoke-опций
        // ══════════════════════════════════════════════════════════
        public static FlowDocument BuildMenuReport(
            AppDbContext ctx,
            DateTime dateFrom,
            DateTime dateTo,
            string itemTypeFilter,  // "Всё" | "product" | "option"
            int topN)
        {
            var doc = CreateDocument();

            AddHeader(doc, "Анализ продаж меню и Karaoke-опций",
                $"Период: {dateFrom:dd.MM.yyyy} — {dateTo:dd.MM.yyyy}   |   " +
                $"Тип: {(itemTypeFilter == "Всё" ? "Все позиции" : itemTypeFilter == "product" ? "Блюда и напитки" : "Karaoke-опции")}   |   Топ {topN}");

            var dfUtc = dateFrom.Date;
            var dtUtc = dateTo.Date.AddDays(1);

            var itemsQ = ctx.OrderItems
                .Include(oi => oi.Product).ThenInclude(p => p!.Type)
                .Include(oi => oi.Option)
                .Include(oi => oi.Order)
                .Where(oi => oi.Order!.CreatedAt >= dfUtc && oi.Order.CreatedAt < dtUtc);

            if (itemTypeFilter != "Всё")
                itemsQ = itemsQ.Where(oi => oi.ItemType == itemTypeFilter);

            var items = itemsQ.ToList();

            // ── Сводка product vs option ─────────────────────────
            AddSectionTitle(doc, "Сводка по типам позиций");
            var byType = items
                .GroupBy(oi => oi.ItemType == "product" ? "Блюда / Напитки" : "Karaoke-опции")
                .Select(g => new {
                    Type = g.Key,
                    Revenue = g.Sum(oi => oi.PriceAtOrder * oi.Quantity),
                    Qty = g.Sum(oi => oi.Quantity),
                    Avg = g.Average(oi => oi.PriceAtOrder)
                }).ToList();

            var typeTable = CreateTable(doc, new[] { "2*", "*", "*", "*" });
            AddTableRow(typeTable, true, "Тип", "Выручка (MDL)", "Кол-во ед.", "Ср. цена");
            foreach (var t in byType)
                AddTableRow(typeTable, false,
                    t.Type, $"{t.Revenue:F2}", $"{t.Qty}", $"{t.Avg:F2}");

            // ── Топ N блюд ───────────────────────────────────────
            var topProducts = items
                .Where(oi => oi.ItemType == "product" && oi.Product != null)
                .GroupBy(oi => new {
                    oi.ProductId,
                    Name = oi.Product!.Name,
                    TypeName = oi.Product.Type?.Name ?? "—",
                    Section = oi.Product.Section
                })
                .Select(g => new {
                    g.Key.Name,
                    g.Key.TypeName,
                    g.Key.Section,
                    TotalQty = g.Sum(oi => oi.Quantity),
                    Revenue = g.Sum(oi => oi.PriceAtOrder * oi.Quantity),
                    AvgPrice = g.Average(oi => oi.PriceAtOrder)
                })
                .OrderByDescending(x => x.Revenue)
                .Take(topN).ToList();

            AddSectionTitle(doc, $"Топ {topN} блюд и напитков по выручке");
            var pTable = CreateTable(doc, new[] { "3*", "2*", "2*", "*", "*" });
            AddTableRow(pTable, true,
                "Позиция", "Тип", "Раздел",
                "Кол-во", "Выручка (MDL)");
            bool alt = false;
            int rank = 1;
            foreach (var p in topProducts)
            {
                AddTableRow(pTable, false,
                    $"{rank++}. {p.Name}", p.TypeName, p.Section,
                    $"{p.TotalQty}", $"{p.Revenue:F2}",
                    "", alt ? AltRow : White);
                alt = !alt;
            }
            var prodTotal = items.Where(oi => oi.ItemType == "product")
                                 .Sum(oi => oi.PriceAtOrder * oi.Quantity);
            AddTotalRow(pTable, "ИТОГО (блюда)", "", "",
                $"{items.Where(oi => oi.ItemType == "product").Sum(oi => oi.Quantity)}",
                $"{prodTotal:F2}");

            // ── Топ N Karaoke-опций ──────────────────────────────
            var topOptions = items
                .Where(oi => oi.ItemType == "option" && oi.Option != null)
                .GroupBy(oi => new {
                    oi.OptionId,
                    Name = oi.Option!.Name
                })
                .Select(g => new {
                    g.Key.Name,
                    TotalQty = g.Sum(oi => oi.Quantity),
                    Revenue = g.Sum(oi => oi.PriceAtOrder * oi.Quantity),
                    AvgPrice = g.Average(oi => oi.PriceAtOrder)
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
                        $"{rank++}. {k.Name}",
                        $"{k.TotalQty}", $"{k.Revenue:F2}", $"{k.AvgPrice:F2}",
                        "", "",
                        alt ? AltRow : White);
                    alt = !alt;
                }
                var optTotal = items.Where(oi => oi.ItemType == "option")
                                    .Sum(oi => oi.PriceAtOrder * oi.Quantity);
                AddTotalRow(kTable, "ИТОГО (опции)",
                    $"{items.Where(oi => oi.ItemType == "option").Sum(oi => oi.Quantity)}",
                    $"{optTotal:F2}", "");
            }

            // ── По разделам меню ─────────────────────────────────
            AddSectionTitle(doc, "Выручка по разделам меню");
            var bySection = items
                .Where(oi => oi.ItemType == "product" && oi.Product != null)
                .GroupBy(oi => oi.Product!.Section)
                .Select(g => new {
                    Section = g.Key,
                    Revenue = g.Sum(oi => oi.PriceAtOrder * oi.Quantity),
                    Qty = g.Sum(oi => oi.Quantity)
                })
                .OrderByDescending(x => x.Revenue).ToList();

            var secTable = CreateTable(doc, new[] { "3*", "*", "*" });
            AddTableRow(secTable, true, "Раздел меню", "Выручка (MDL)", "Кол-во ед.");
            alt = false;
            foreach (var s in bySection)
            {
                AddTableRow(secTable, false,
                    s.Section, $"{s.Revenue:F2}", $"{s.Qty}",
                    "", "", "",
                    alt ? AltRow : White);
                alt = !alt;
            }

            AddFooter(doc);
            return doc;
        }

        // ══════════════════════════════════════════════════════════
        // ОТЧЁТ 3 — Нагрузка на персонал
        // ══════════════════════════════════════════════════════════
        public static FlowDocument BuildStaffReport(
            AppDbContext ctx,
            DateTime dateFrom,
            DateTime dateTo,
            string roleFilter)   // "Все должности" или название роли
        {
            var doc = CreateDocument();

            AddHeader(doc, "Отчёт по нагрузке на персонал",
                $"Период: {dateFrom:dd.MM.yyyy} — {dateTo:dd.MM.yyyy}   |   " +
                $"Должность: {roleFilter}   |   Дата формирования: {DateTime.Now:dd.MM.yyyy HH:mm}");

            var dfUtc = dateFrom.Date;
            var dtUtc = dateTo.Date.AddDays(1);

            var ordersQ = ctx.Orders
                .Include(o => o.Worker).ThenInclude(w => w!.Role)
                .Include(o => o.Bill)
                .Include(o => o.OrderItems)
                .Where(o => o.CreatedAt >= dfUtc && o.CreatedAt < dtUtc);

            var orders = ordersQ.ToList();

            if (roleFilter != "Все должности")
                orders = orders.Where(o => o.Worker?.Role?.Name == roleFilter).ToList();

            // ── Сводка ───────────────────────────────────────────
            var totalOrders = orders.Count;
            var closedOrders = orders.Count(o => o.Status == "closed");
            var openOrders = orders.Count(o => o.Status == "open");
            var totalRev = orders.Sum(o => o.Bill?.TotalAmount ?? 0);
            var avgItems = orders.Any()
                                 ? orders.Average(o => o.OrderItems.Count)
                                 : 0;

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
            var byWorker = orders
                .GroupBy(o => new {
                    Name = o.Worker?.Name ?? "—",
                    Role = o.Worker?.Role?.Name ?? "—"
                })
                .Select(g => new {
                    g.Key.Name,
                    g.Key.Role,
                    OrderCount = g.Count(),
                    Revenue = g.Sum(o => o.Bill?.TotalAmount ?? 0),
                    AvgGuests = g.Any(o => o.GuestCount > 0)
                                    ? g.Where(o => o.GuestCount > 0)
                                       .Average(o => o.GuestCount!.Value)
                                    : 0,
                    AvgItems = g.Average(o => o.OrderItems.Count),
                    ClosedCount = g.Count(o => o.Status == "closed")
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
                    w.Name, w.Role,
                    $"{w.OrderCount}", $"{w.ClosedCount}",
                    $"{w.AvgGuests:F1}", $"{w.Revenue:F2}",
                    alt ? AltRow : White);
                alt = !alt;
            }
            AddTotalRow(wTable, "ИТОГО", "", $"{totalOrders}", $"{closedOrders}", "", $"{totalRev:F2}");

            // ── Нагрузка по ролям ────────────────────────────────
            AddSectionTitle(doc, "Нагрузка по должностям");
            var byRole = orders
                .GroupBy(o => o.Worker?.Role?.Name ?? "—")
                .Select(g => new {
                    Role = g.Key,
                    OrderCount = g.Count(),
                    Revenue = g.Sum(o => o.Bill?.TotalAmount ?? 0),
                    Workers = g.Select(o => o.Worker?.Name).Distinct().Count()
                })
                .OrderByDescending(x => x.Revenue).ToList();

            var rTable = CreateTable(doc, new[] { "2*", "*", "*", "*" });
            AddTableRow(rTable, true,
                "Должность", "Сотрудников", "Заказов", "Выручка (MDL)");
            alt = false;
            foreach (var r in byRole)
            {
                AddTableRow(rTable, false,
                    r.Role, $"{r.Workers}", $"{r.OrderCount}", $"{r.Revenue:F2}",
                    "", "",
                    alt ? AltRow : White);
                alt = !alt;
            }

            // ── Загруженность по столам ──────────────────────────
            AddSectionTitle(doc, "Загруженность столов");
            var byTable = orders
                .GroupBy(o => o.TableNumber)
                .Select(g => new {
                    Table = g.Key,
                    Sessions = g.Count(),
                    Revenue = g.Sum(o => o.Bill?.TotalAmount ?? 0),
                    AvgGuests = g.Any(o => o.GuestCount > 0)
                                   ? g.Where(o => o.GuestCount > 0)
                                      .Average(o => o.GuestCount!.Value)
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
                    "", "",
                    alt ? AltRow : White);
                alt = !alt;
            }
            AddTotalRow(tTable, "ИТОГО", $"{totalOrders}", "", $"{totalRev:F2}");

            AddFooter(doc);
            return doc;
        }

        // ══════════════════════════════════════════════════════════
        // ВСПОМОГАТЕЛЬНЫЕ МЕТОДЫ
        // ══════════════════════════════════════════════════════════

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
                            Foreground = new SolidColorBrush(Color.FromRgb(200, 224, 0)),  // #c8e000
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
                Foreground = new SolidColorBrush(Color.FromRgb(74, 124, 31)),   // #4a7c1f
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

        private static void AddTableRow(Table table, bool isHeader, params string[] cells)
            => AddTableRow(table, isHeader, cells, null);

        private static void AddTableRow(Table table, bool isHeader,
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
                if (isHeader)
                    para.Foreground = Brushes.White;

                var tc = new TableCell(para)
                {
                    BorderBrush = BorderBrush,
                    BorderThickness = new Thickness(0, 0, 1, 1)
                };
                row.Cells.Add(tc);
            }

            table.RowGroups[0].Rows.Add(row);
        }

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
            AddTableRow(table, isHeader, cells.ToArray(), bg);
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