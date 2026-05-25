using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Runtime.CompilerServices;

namespace KaraokeClub.Models
{
    // ??????? ????? ? ?????????? ??????????? ? ????? WPF ?????
    // ????????? ????????????? ??????? (Type.Name, Role.Name ? ?.?.)
    public abstract class NotifyBase : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        protected void Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
        {
            if (Equals(field, value)) return;
            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }

    [Table("roles")]
    public class Role : NotifyBase
    {
        private int _id;
        private string _name = "";
        private decimal _salary;

        [Key][Column("id_role")] public int Id { get => _id; set => Set(ref _id, value); }
        [Column("name_role")] public string Name { get => _name; set => Set(ref _name, value); }
        [Column("salary")] public decimal Salary { get => _salary; set => Set(ref _salary, value); }

        public ICollection<Worker> Workers { get; set; } = new List<Worker>();
        public override string ToString() => Name;
    }

    [Table("type")]
    public class MenuType : NotifyBase
    {
        private int _id;
        private string _name = "";

        [Key][Column("id_type")] public int Id { get => _id; set => Set(ref _id, value); }
        [Column("name_type")] public string Name { get => _name; set => Set(ref _name, value); }

        public ICollection<MenuItem> MenuItems { get; set; } = new List<MenuItem>();
        public override string ToString() => Name;
    }

    [Table("karaoke")]
    public class KaraokeOption : NotifyBase
    {
        private int _id;
        private string _name = "";
        private decimal _price;
        private string? _description;

        [Key][Column("id_option")] public int Id { get => _id; set => Set(ref _id, value); }
        [Column("name_option")] public string Name { get => _name; set => Set(ref _name, value); }
        [Column("price_option")] public decimal Price { get => _price; set => Set(ref _price, value); }
        [Column("descriptionn")] public string? Description { get => _description; set => Set(ref _description, value); }

        public ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();
        public override string ToString() => Name;
    }

    [Table("workers")]
    public class Worker : NotifyBase
    {
        private int _id;
        private string _name = "";
        private int _roleId;
        private string? _idnp;
        private DateTime? _birth;
        private string? _address;
        private string? _phone;
        private Role? _role;

        [Key][Column("id_worker")] public int Id { get => _id; set => Set(ref _id, value); }
        [Column("name_worker")] public string Name { get => _name; set => Set(ref _name, value); }
        [Column("id_role")] public int RoleId { get => _roleId; set => Set(ref _roleId, value); }
        [Column("idnp")] public string? Idnp { get => _idnp; set => Set(ref _idnp, value); }
        [Column("birth")] public DateTime? Birth { get => _birth; set => Set(ref _birth, value); }
        [Column("addres")] public string? Address { get => _address; set => Set(ref _address, value); }
        [Column("phone")] public string? Phone { get => _phone; set => Set(ref _phone, value); }

        public Role? Role { get => _role; set => Set(ref _role, value); }
        public ICollection<Order> Orders { get; set; } = new List<Order>();

        public override string ToString() => Name;
    }

    [Table("menu")]
    public class MenuItem : NotifyBase
    {
        private int _id;
        private string _name = "";
        private decimal _price;
        private int _typeId;
        private string _section = "";
        private string _weightVolume = "";
        private string _ingredients = "";
        private int? _cookingTime;
        private string? _imagePath;
        private MenuType? _type;

        [Key][Column("id_product")] public int Id { get => _id; set => Set(ref _id, value); }
        [Column("name_product")] public string Name { get => _name; set => Set(ref _name, value); }
        [Column("price_product")] public decimal Price { get => _price; set => Set(ref _price, value); }
        [Column("id_type")] public int TypeId { get => _typeId; set => Set(ref _typeId, value); }
        [Column("section")] public string Section { get => _section; set => Set(ref _section, value); }
        [Column("weight_volume")] public string WeightVolume { get => _weightVolume; set => Set(ref _weightVolume, value); }
        [Column("ingredients")] public string Ingredients { get => _ingredients; set => Set(ref _ingredients, value); }
        [Column("cooking_time")] public int? CookingTime { get => _cookingTime; set => Set(ref _cookingTime, value); }
        [Column("image_path")] public string? ImagePath { get => _imagePath; set => Set(ref _imagePath, value); }

        public MenuType? Type { get => _type; set => Set(ref _type, value); }
        public ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();
        public override string ToString() => Name;
    }

    [Table("orders")]
    public class Order : NotifyBase
    {
        private int _id;
        private int _tableNumber;
        private int _workerId;
        private string _status = "open";
        private int? _guestCount;
        private DateTime _createdAt = DateTime.Now;
        private Worker? _worker;

        [Key][Column("id_order")] public int Id { get => _id; set => Set(ref _id, value); }
        [Column("table_number")] public int TableNumber { get => _tableNumber; set => Set(ref _tableNumber, value); }
        [Column("id_worker")] public int WorkerId { get => _workerId; set => Set(ref _workerId, value); }
        [Column("order_status")] public string Status { get => _status; set => Set(ref _status, value); }
        [Column("guest_count")] public int? GuestCount { get => _guestCount; set => Set(ref _guestCount, value); }
        [Column("created_at")] public DateTime CreatedAt { get => _createdAt; set => Set(ref _createdAt, value); }

        public Worker? Worker { get => _worker; set => Set(ref _worker, value); }
        public Bill? Bill { get; set; }
        public ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();

        public override string ToString() => Id.ToString();
    }

    [Table("order_items")]
    public class OrderItem : NotifyBase
    {
        private int _id;
        private int _orderId;
        private string _itemType = "product";
        private int? _productId;
        private int? _optionId;
        private int _quantity;
        private decimal _priceAtOrder;
        private string? _notes;
        private Order? _order;
        private MenuItem? _product;
        private KaraokeOption? _option;

        [Key][Column("id")] public int Id { get => _id; set => Set(ref _id, value); }
        [Column("id_order")] public int OrderId { get => _orderId; set => Set(ref _orderId, value); }
        [Column("item_type")] public string ItemType { get => _itemType; set => Set(ref _itemType, value); }
        [Column("id_product")] public int? ProductId { get => _productId; set => Set(ref _productId, value); }
        [Column("id_option")] public int? OptionId { get => _optionId; set => Set(ref _optionId, value); }
        [Column("quantity")] public int Quantity { get => _quantity; set => Set(ref _quantity, value); }
        [Column("price_at_order")] public decimal PriceAtOrder { get => _priceAtOrder; set => Set(ref _priceAtOrder, value); }
        [Column("notes")] public string? Notes { get => _notes; set => Set(ref _notes, value); }

        public Order? Order { get => _order; set => Set(ref _order, value); }
        public MenuItem? Product { get => _product; set => Set(ref _product, value); }
        public KaraokeOption? Option { get => _option; set => Set(ref _option, value); }
    }

    [Table("bill")]
    public class Bill : NotifyBase
    {
        private int _id;
        private int _orderId;
        private DateTime _createdAt = DateTime.Now;
        private decimal? _totalAmount;
        private decimal? _deposit;
        private string? _paymentMethod;
        private string _billStatus = "unpaid";
        private Order? _order;

        [Key][Column("id_bill")] public int Id { get => _id; set => Set(ref _id, value); }
        [Column("id_order")] public int OrderId { get => _orderId; set => Set(ref _orderId, value); }
        [Column("created_at")] public DateTime CreatedAt { get => _createdAt; set => Set(ref _createdAt, value); }
        [Column("total_amount")] public decimal? TotalAmount { get => _totalAmount; set => Set(ref _totalAmount, value); }
        [Column("deposit")] public decimal? Deposit { get => _deposit; set => Set(ref _deposit, value); }
        [Column("payment_method")] public string? PaymentMethod { get => _paymentMethod; set => Set(ref _paymentMethod, value); }
        [Column("bill_status")] public string BillStatus { get => _billStatus; set => Set(ref _billStatus, value); }

        public Order? Order { get => _order; set => Set(ref _order, value); }
    }

    [Table("app_users")]
    public class AppUser : NotifyBase
    {
        private int _id;
        private string _username = "";
        private string _password = "";
        private string _appRole = "user";
        private int? _workerId;
        private Worker? _worker;

        [Key][Column("id_user")] public int Id { get => _id; set => Set(ref _id, value); }
        [Column("username")] public string Username { get => _username; set => Set(ref _username, value); }
        [Column("password")] public string Password { get => _password; set => Set(ref _password, value); }
        [Column("app_role")] public string AppRole { get => _appRole; set => Set(ref _appRole, value); }
        [Column("id_worker")] public int? WorkerId { get => _workerId; set => Set(ref _workerId, value); }

        public Worker? Worker { get => _worker; set => Set(ref _worker, value); }

        public override string ToString() => Username;
    }
}