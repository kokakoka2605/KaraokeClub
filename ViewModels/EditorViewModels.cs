using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using KaraokeClub.Models;
using System.ComponentModel;

namespace KaraokeClub.ViewModels
{
    public class WorkerEditorViewModel
    {
        public Worker Entity { get; }
        public ObservableCollection<Role> Roles { get; }

        public WorkerEditorViewModel(Worker entity, ObservableCollection<Role> roles)
        {
            Entity = entity;
            Roles = roles;
        }
    }

    public class MenuItemEditorViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        public MenuItem Entity { get; }
        public ObservableCollection<MenuType> MenuTypes { get; }
        public string[] Sections { get; } = { "kitchen", "bar" };

        // Ключевые слова в названии типа => секция "bar"
        private static readonly string[] BarKeywords =
            { "алкогол", "alcohol", "напит", "drink", "cocktail", "коктейл", "beer", "пиво", "wine", "вино" };

        public int SelectedTypeId
        {
            get => Entity.TypeId;
            set
            {
                if (Entity.TypeId == value) return;
                Entity.TypeId = value;
                OnPropertyChanged(nameof(SelectedTypeId));

                // Автовыбор секции по названию типа
                var typeName = MenuTypes.FirstOrDefault(t => t.Id == value)?.Name ?? "";
                var lower = typeName.ToLowerInvariant();
                Entity.Section = BarKeywords.Any(k => lower.Contains(k)) ? "bar" : "kitchen";
                OnPropertyChanged(nameof(Entity));
            }
        }

        public MenuItemEditorViewModel(MenuItem entity, ObservableCollection<MenuType> menuTypes)
        {
            Entity = entity;
            MenuTypes = menuTypes;
        }

        private void OnPropertyChanged(string name) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public class OrderEditorViewModel
    {
        public Order Entity { get; }
        public ObservableCollection<Worker> Workers { get; }
        public string[] Statuses { get; } = { "open", "closed", "cancelled" };
        public bool IsNewOrder { get; }
        public bool IsEditOrder => !IsNewOrder;

        public OrderEditorViewModel(Order entity, ObservableCollection<Worker> workers, bool isNew)
        {
            Entity = entity;
            // Only show waiters in the worker selector for orders
            Workers = new ObservableCollection<Worker>(
                workers.Where(w => w.Role?.Name.Equals("Waiter", System.StringComparison.OrdinalIgnoreCase) == true
                                || w.Role?.Name.Equals("Официант", System.StringComparison.OrdinalIgnoreCase) == true));
            IsNewOrder = isNew;
        }
    }

    public class OrderItemEditorViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        public ObservableCollection<Order> Orders { get; }
        public ObservableCollection<MenuItem> Products { get; }
        public ObservableCollection<KaraokeOption> KaraokeOptions { get; }
        public string[] ItemTypes { get; } = { "product", "karaoke" };

        private OrderItem _entity;
        public OrderItem Entity
        {
            get => _entity;
            set { _entity = value; OnPropertyChanged(nameof(Entity)); }
        }

        public bool IsProductEnabled => _entity.ItemType == "product";
        public bool IsKaraokeEnabled => _entity.ItemType == "karaoke";

        public string SelectedItemType
        {
            get => _entity.ItemType;
            set
            {
                if (_entity.ItemType == value) return;
                _entity.ItemType = value;

                // При смене типа сбрасываем противоположное поле
                if (value == "product")
                    _entity.OptionId = null;
                else
                    _entity.ProductId = null;

                OnPropertyChanged(nameof(SelectedItemType));
                OnPropertyChanged(nameof(IsProductEnabled));
                OnPropertyChanged(nameof(IsKaraokeEnabled));
                OnPropertyChanged(nameof(Entity));
            }
        }

        public bool IsNewItem { get; }
        public bool IsEditItem => !IsNewItem;

        /// <summary>Название блюда или опции для отображения в заблокированном блоке</summary>
        public string ItemDisplayName =>
            _entity.Product?.Name ?? _entity.Option?.Name ?? $"ID: {_entity.ProductId ?? _entity.OptionId}";

        public OrderItemEditorViewModel(
            OrderItem entity,
            ObservableCollection<Order> orders,
            ObservableCollection<MenuItem> products,
            ObservableCollection<KaraokeOption> karaokeOptions,
            bool isNew = true)
        {
            _entity = entity;
            Orders = orders;
            Products = products;
            KaraokeOptions = karaokeOptions;
            IsNewItem = isNew;
        }

        private void OnPropertyChanged(string name) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public class BillEditorViewModel
    {
        public Bill Entity { get; }
        public ObservableCollection<Order> Orders { get; }
        public string[] PaymentMethods { get; } = { "cash", "card", "online" };
        public string[] Statuses { get; } = { "unpaid", "paid", "partial" };

        public BillEditorViewModel(Bill entity, ObservableCollection<Order> orders)
        {
            Entity = entity;
            Orders = orders;
        }
    }

    public class AppUserEditorViewModel
    {
        public AppUser Entity { get; }
        public ObservableCollection<Worker> Workers { get; }

        /// <param name="takenWorkerIds">
        /// Id сотрудников, у которых уже есть аккаунт.
        /// Они скрываются из выпадающего списка (кроме текущего — при редактировании).
        /// </param>
        public AppUserEditorViewModel(AppUser entity, ObservableCollection<Worker> workers,
            HashSet<int>? takenWorkerIds = null)
        {
            Entity = entity;
            // Показываем только официантов без аккаунта (+ текущий сотрудник при редактировании)
            Workers = new ObservableCollection<Worker>(
                workers.Where(w => w.Role?.Name.Equals("Waiter", System.StringComparison.OrdinalIgnoreCase) == true &&
                                   (takenWorkerIds == null || !takenWorkerIds.Contains(w.Id))));
            // Роль всегда user — менять нельзя
            entity.AppRole = "user";
        }
    }
}