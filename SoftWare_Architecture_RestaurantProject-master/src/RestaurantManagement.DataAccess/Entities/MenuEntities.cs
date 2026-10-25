using RestaurantManagement.Common.Enums;

namespace RestaurantManagement.DataAccess.Entities;

public class Category
{
    public int Id { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public KitchenStation TargetKitchen { get; set; } = KitchenStation.HotKitchen;
    public int DisplayOrder { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<MenuItem> MenuItems { get; set; } = new List<MenuItem>();
}

public class MenuItem
{
    public int Id { get; set; }
    public string ItemCode { get; set; } = string.Empty; // M01, M02...
    public string ItemName { get; set; } = string.Empty;
    public int CategoryId { get; set; }
    public Category Category { get; set; } = null!;
    public decimal Price { get; set; }
    public string Unit { get; set; } = "Phần";
    public string? Description { get; set; }
    public string? ImageUrl { get; set; }
    public KitchenStation KitchenStation { get; set; } = KitchenStation.HotKitchen;
    public bool IsAvailable { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<MenuPriceHistory> PriceHistories { get; set; } = new List<MenuPriceHistory>();
    public ICollection<OrderDetail> OrderDetails { get; set; } = new List<OrderDetail>();
}

public class MenuPriceHistory
{
    public int Id { get; set; }
    public int MenuItemId { get; set; }
    public MenuItem MenuItem { get; set; } = null!;
    public decimal OldPrice { get; set; }
    public decimal NewPrice { get; set; }
    public DateTime EffectiveFrom { get; set; } = DateTime.UtcNow;
    public int ChangedById { get; set; }
    public Staff ChangedBy { get; set; } = null!;
}
