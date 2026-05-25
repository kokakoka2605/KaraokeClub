using System.Collections.ObjectModel;
using System.Linq;
using KaraokeClub.Data;
using KaraokeClub.Models;
using Microsoft.EntityFrameworkCore;

namespace KaraokeClub.ViewModels
{
    public class MainViewModel : BaseViewModel
    {
        public AppDbContext Ctx { get; }

        public ObservableCollection<Role> Roles { get; } = new();
        public ObservableCollection<MenuType> MenuTypes { get; } = new();
        public ObservableCollection<Worker> Workers { get; } = new();
        public ObservableCollection<MenuItem> MenuItems { get; } = new();
        public ObservableCollection<Order> Orders { get; } = new();
        public ObservableCollection<KaraokeOption> KaraokeOptions { get; } = new();

        public RolesViewModel RolesVM { get; }
        public MenuTypesViewModel MenuTypesVM { get; }
        public KaraokeOptionsViewModel KaraokeVM { get; }
        public WorkersViewModel WorkersVM { get; }
        public MenuItemsViewModel MenuItemsVM { get; }
        public OrdersViewModel OrdersVM { get; }
        public OrderItemsViewModel OrderItemsVM { get; }
        public BillsViewModel BillsVM { get; }
        public AppUsersViewModel AppUsersVM { get; }

        private bool _isAdmin;
        public bool IsAdmin { get => _isAdmin; set => SetProperty(ref _isAdmin, value); }

        public MainViewModel(AppDbContext ctx, bool isAdmin)
        {
            Ctx = ctx;
            IsAdmin = isAdmin;

            foreach (var r in ctx.Roles.ToList()) Roles.Add(r);
            foreach (var t in ctx.MenuTypes.ToList()) MenuTypes.Add(t);
            foreach (var w in ctx.Workers.Include(w => w.Role).ToList()) Workers.Add(w);
            foreach (var m in ctx.MenuItems.ToList()) MenuItems.Add(m);
            foreach (var o in ctx.Orders.ToList()) Orders.Add(o);
            foreach (var k in ctx.KaraokeOptions.ToList()) KaraokeOptions.Add(k);

            RolesVM = new RolesViewModel(ctx);
            MenuTypesVM = new MenuTypesViewModel(ctx);
            KaraokeVM = new KaraokeOptionsViewModel(ctx);
            WorkersVM = new WorkersViewModel(ctx, Roles);
            MenuItemsVM = new MenuItemsViewModel(ctx, MenuTypes);
            OrdersVM = new OrdersViewModel(ctx, Workers);
            OrderItemsVM = new OrderItemsViewModel(ctx, Orders, MenuItems, KaraokeOptions);
            BillsVM = new BillsViewModel(ctx, Orders);
            AppUsersVM = new AppUsersViewModel(ctx, Workers);

            // Роль изменилась → обновляем свойство Role у всех Workers в памяти
            // Так как Worker.Role теперь INotifyPropertyChanged — DataGrid обновится сам
            RolesVM.ItemsChanged += () =>
            {
                var fresh = ctx.Roles.ToList();
                // Обновляем справочник Roles (для комбобоксов)
                Roles.Clear();
                foreach (var r in fresh) Roles.Add(r);
                // Обновляем навигационное свойство у каждого Worker в таблице
                foreach (var w in WorkersVM.Items)
                    w.Role = fresh.FirstOrDefault(r => r.Id == w.RoleId);
            };

            // Тип меню изменился → обновляем свойство Type у всех MenuItem в памяти
            MenuTypesVM.ItemsChanged += () =>
            {
                var fresh = ctx.MenuTypes.ToList();
                MenuTypes.Clear();
                foreach (var t in fresh) MenuTypes.Add(t);
                foreach (var m in MenuItemsVM.Items)
                    m.Type = fresh.FirstOrDefault(t => t.Id == m.TypeId);
            };

            // Karaoke опция изменилась → обновляем Option у OrderItems
            KaraokeVM.ItemsChanged += () =>
            {
                var fresh = ctx.KaraokeOptions.ToList();
                KaraokeOptions.Clear();
                foreach (var k in fresh) KaraokeOptions.Add(k);
                foreach (var oi in OrderItemsVM.Items)
                    oi.Option = fresh.FirstOrDefault(k => k.Id == oi.OptionId);
            };

            // Worker изменился → обновляем Worker у Orders
            WorkersVM.ItemsChanged += () =>
            {
                var fresh = ctx.Workers.ToList();
                Workers.Clear();
                foreach (var w in fresh) Workers.Add(w);
                foreach (var o in OrdersVM.Items)
                    o.Worker = fresh.FirstOrDefault(w => w.Id == o.WorkerId);
            };

            // MenuItem изменился → обновляем Product у OrderItems
            MenuItemsVM.ItemsChanged += () =>
            {
                var fresh = ctx.MenuItems.ToList();
                MenuItems.Clear();
                foreach (var m in fresh) MenuItems.Add(m);
                foreach (var oi in OrderItemsVM.Items)
                    oi.Product = fresh.FirstOrDefault(m => m.Id == oi.ProductId);
            };

            // Order изменился → обновляем Order у Bills и OrderItems
            OrdersVM.ItemsChanged += () =>
            {
                var fresh = ctx.Orders.ToList();
                Orders.Clear();
                foreach (var o in fresh) Orders.Add(o);
                foreach (var b in BillsVM.Items)
                    b.Order = fresh.FirstOrDefault(o => o.Id == b.OrderId);
                foreach (var oi in OrderItemsVM.Items)
                    oi.Order = fresh.FirstOrDefault(o => o.Id == oi.OrderId);
            };

            // Счёт оплачен → заказ закрыт, обновляем UI таблицы заказов
            BillsVM.OrderShouldClose += orderId =>
            {
                OrdersVM.Refresh(orderId);
            };
        }
    }
}