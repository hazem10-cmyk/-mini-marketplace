namespace Marketplace.Api.Data;

public enum UserRole
{
    Buyer,
    Seller,
    Admin
}

public enum OrderStatus
{
    Pending,
    InProgress,
    Delivered,
    Completed,
    Cancelled
}

public class User
{
    public int Id { get; set; }
    public string Email { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public UserRole Role { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public List<Service> Services { get; set; } = [];
    public List<Order> PlacedOrders { get; set; } = [];
}

public class Service
{
    public int Id { get; set; }
    public int SellerId { get; set; }
    public User Seller { get; set; } = null!;
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public decimal Price { get; set; }
    public int DeliveryDays { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public List<Order> Orders { get; set; } = [];
}

public class Order
{
    public int Id { get; set; }
    public int ServiceId { get; set; }
    public Service Service { get; set; } = null!;
    public int BuyerId { get; set; }
    public User Buyer { get; set; } = null!;
    public decimal PriceAtOrder { get; set; }
    public OrderStatus Status { get; set; } = OrderStatus.Pending;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public Review? Review { get; set; }
}

public class Review
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public Order Order { get; set; } = null!;
    public int Rating { get; set; }
    public string? Comment { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}