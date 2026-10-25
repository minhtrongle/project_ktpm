using RestaurantManagement.Common.Enums;

namespace RestaurantManagement.Business.DTOs;

public record CategoryDto(
    int Id,
    string CategoryName,
    KitchenStation TargetKitchen,
    int DisplayOrder,
    int ItemCount
);

public record MenuItemDto(
    int Id,
    string ItemCode,
    string ItemName,
    int CategoryId,
    string CategoryName,
    decimal Price,
    string? Unit,
    KitchenStation KitchenStation,
    string? Description,
    bool IsAvailable
);

public record CreateMenuItemDto(
    string ItemCode,
    string ItemName,
    int CategoryId,
    decimal Price,
    string? Unit,
    KitchenStation KitchenStation,
    string? Description
);

public record UpdateMenuItemDto(
    int Id,
    string ItemName,
    int CategoryId,
    decimal Price,
    string? Unit,
    KitchenStation KitchenStation,
    string? Description,
    bool IsAvailable,
    string? ManagerPin = null
);

public record UpdateItemPriceDto(
    int MenuItemId,
    decimal NewPrice,
    string Reason,
    string? ManagerPin = null
);
