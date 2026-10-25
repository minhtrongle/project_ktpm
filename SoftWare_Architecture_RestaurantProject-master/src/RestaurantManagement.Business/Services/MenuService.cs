using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using RestaurantManagement.Business.DTOs;
using RestaurantManagement.Common.Enums;
using RestaurantManagement.Common.Results;
using RestaurantManagement.DataAccess.Entities;
using RestaurantManagement.DataAccess.UnitOfWork;

namespace RestaurantManagement.Business.Services;

public interface IMenuService
{
    Task<List<CategoryDto>> GetCategoriesAsync(CancellationToken cancellationToken = default);
    Task<List<MenuItemDto>> GetMenuItemsAsync(int? categoryId = null, bool onlyAvailable = false, CancellationToken cancellationToken = default);
    Task<Result<MenuItemDto>> GetMenuItemByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<Result<MenuItemDto>> CreateMenuItemAsync(CreateMenuItemDto dto, CancellationToken cancellationToken = default);
    Task<Result<MenuItemDto>> UpdateMenuItemAsync(UpdateMenuItemDto dto, CancellationToken cancellationToken = default);
    Task<Result> UpdateItemPriceAsync(UpdateItemPriceDto dto, CancellationToken cancellationToken = default);
    Task<Result> ToggleItemAvailabilityAsync(int menuItemId, CancellationToken cancellationToken = default);
}

public class MenuService : IMenuService
{
    private readonly IUnitOfWork _uow;
    private readonly IPasswordHasher<Staff> _passwordHasher;

    public MenuService(IUnitOfWork uow, IPasswordHasher<Staff> passwordHasher)
    {
        _uow = uow;
        _passwordHasher = passwordHasher;
    }

    public async Task<List<CategoryDto>> GetCategoriesAsync(CancellationToken cancellationToken = default)
    {
        var categories = await _uow.Categories.GetAll()
            .Include(c => c.MenuItems)
            .OrderBy(c => c.DisplayOrder)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return categories.Select(c => new CategoryDto(
            c.Id,
            c.CategoryName,
            c.TargetKitchen,
            c.DisplayOrder,
            c.MenuItems.Count
        )).ToList();
    }

    public async Task<List<MenuItemDto>> GetMenuItemsAsync(int? categoryId = null, bool onlyAvailable = false, CancellationToken cancellationToken = default)
    {
        var query = _uow.MenuItems.GetAll()
            .Include(m => m.Category)
            .AsNoTracking();

        if (categoryId.HasValue && categoryId.Value > 0)
        {
            query = query.Where(m => m.CategoryId == categoryId.Value);
        }

        if (onlyAvailable)
        {
            query = query.Where(m => m.IsAvailable);
        }

        var items = await query.OrderBy(m => m.Category.DisplayOrder).ThenBy(m => m.ItemCode).ToListAsync(cancellationToken);

        return items.Select(m => new MenuItemDto(
            m.Id,
            m.ItemCode,
            m.ItemName,
            m.CategoryId,
            m.Category.CategoryName,
            m.Price,
            m.Unit,
            m.KitchenStation,
            m.Description,
            m.IsAvailable
        )).ToList();
    }

    public async Task<Result<MenuItemDto>> GetMenuItemByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var item = await _uow.MenuItems.GetAll()
            .Include(m => m.Category)
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.Id == id, cancellationToken);

        if (item == null)
            return Result<MenuItemDto>.Failure(Error.NotFound("MenuItem.NotFound", "Không tìm thấy món ăn."));

        return Result<MenuItemDto>.Success(new MenuItemDto(
            item.Id,
            item.ItemCode,
            item.ItemName,
            item.CategoryId,
            item.Category.CategoryName,
            item.Price,
            item.Unit,
            item.KitchenStation,
            item.Description,
            item.IsAvailable
        ));
    }

    public async Task<Result<MenuItemDto>> CreateMenuItemAsync(CreateMenuItemDto dto, CancellationToken cancellationToken = default)
    {
        if (dto.Price <= 0)
            return Result<MenuItemDto>.Failure(Error.Validation("MenuItem.InvalidPrice", "Đơn giá món phải lớn hơn 0đ."));

        var codeExists = await _uow.MenuItems.GetAll().AnyAsync(m => m.ItemCode == dto.ItemCode.Trim(), cancellationToken);
        if (codeExists)
            return Result<MenuItemDto>.Failure(Error.Conflict("MenuItem.CodeExists", $"Mã món '{dto.ItemCode}' đã tồn tại trong thực đơn."));

        var nameExists = await _uow.MenuItems.GetAll().AnyAsync(m => m.ItemName == dto.ItemName.Trim(), cancellationToken);
        if (nameExists)
            return Result<MenuItemDto>.Failure(Error.Conflict("MenuItem.NameExists", $"Tên món '{dto.ItemName}' đã tồn tại trong thực đơn."));

        var category = await _uow.Categories.GetByIdAsync(dto.CategoryId, cancellationToken);
        if (category == null)
            return Result<MenuItemDto>.Failure(Error.NotFound("Category.NotFound", "Danh mục không tồn tại."));

        var item = new MenuItem
        {
            ItemCode = dto.ItemCode.Trim(),
            ItemName = dto.ItemName.Trim(),
            CategoryId = dto.CategoryId,
            Price = dto.Price,
            Unit = dto.Unit?.Trim(),
            KitchenStation = dto.KitchenStation,
            Description = dto.Description,
            IsAvailable = true
        };

        await _uow.MenuItems.AddAsync(item, cancellationToken);
        await _uow.SaveChangesAsync(cancellationToken);

        return Result<MenuItemDto>.Success(new MenuItemDto(
            item.Id,
            item.ItemCode,
            item.ItemName,
            category.Id,
            category.CategoryName,
            item.Price,
            item.Unit,
            item.KitchenStation,
            item.Description,
            item.IsAvailable
        ));
    }

    public async Task<Result<MenuItemDto>> UpdateMenuItemAsync(UpdateMenuItemDto dto, CancellationToken cancellationToken = default)
    {
        var item = await _uow.MenuItems.GetAll()
            .Include(m => m.Category)
            .FirstOrDefaultAsync(m => m.Id == dto.Id, cancellationToken);

        if (item == null)
            return Result<MenuItemDto>.Failure(Error.NotFound("MenuItem.NotFound", "Không tìm thấy món ăn."));

        if (dto.Price <= 0)
            return Result<MenuItemDto>.Failure(Error.Validation("MenuItem.InvalidPrice", "Đơn giá món phải lớn hơn 0đ."));

        var nameExists = await _uow.MenuItems.GetAll().AnyAsync(m => m.ItemName == dto.ItemName.Trim() && m.Id != dto.Id, cancellationToken);
        if (nameExists)
            return Result<MenuItemDto>.Failure(Error.Conflict("MenuItem.NameExists", $"Tên món '{dto.ItemName}' đã bị trùng lặp."));

        var oldPrice = item.Price;
        var nowUtc = DateTime.UtcNow;

        return await _uow.ExecuteInTransactionAsync(async () =>
        {
            if (oldPrice != dto.Price)
            {
                // Lưu vết lịch sử giá
                await _uow.MenuPriceHistories.AddAsync(new MenuPriceHistory
                {
                    MenuItemId = item.Id,
                    OldPrice = oldPrice,
                    NewPrice = dto.Price,
                    EffectiveFrom = nowUtc,
                    ChangedById = 1
                }, cancellationToken);

                await _uow.AuditLogs.AddAsync(new AuditLog
                {
                    Action = "UpdatePrice",
                    EntityName = "MenuItems",
                    EntityId = item.Id.ToString(),
                    Details = $"Đổi giá từ {oldPrice:N0}đ sang {dto.Price:N0}đ",
                    CreatedAt = nowUtc
                }, cancellationToken);
            }

            item.ItemName = dto.ItemName.Trim();
            item.CategoryId = dto.CategoryId;
            item.Price = dto.Price;
            item.Unit = dto.Unit?.Trim();
            item.KitchenStation = dto.KitchenStation;
            item.Description = dto.Description;
            item.IsAvailable = dto.IsAvailable;

            await _uow.SaveChangesAsync(cancellationToken);

            var category = await _uow.Categories.GetByIdAsync(item.CategoryId, cancellationToken);

            return Result<MenuItemDto>.Success(new MenuItemDto(
                item.Id,
                item.ItemCode,
                item.ItemName,
                item.CategoryId,
                category?.CategoryName ?? string.Empty,
                item.Price,
                item.Unit,
                item.KitchenStation,
                item.Description,
                item.IsAvailable
            ));
        }, cancellationToken);
    }

    public async Task<Result> UpdateItemPriceAsync(UpdateItemPriceDto dto, CancellationToken cancellationToken = default)
    {
        var item = await _uow.MenuItems.GetByIdAsync(dto.MenuItemId, cancellationToken);
        if (item == null)
            return Result.Failure(Error.NotFound("MenuItem.NotFound", "Không tìm thấy món ăn."));

        if (dto.NewPrice <= 0)
            return Result.Failure(Error.Validation("MenuItem.InvalidPrice", "Giá niêm yết mới phải lớn hơn 0đ."));

        var isPinValid = await ValidateManagerPinAsync(dto.ManagerPin, cancellationToken);
        if (!isPinValid)
            return Result.Failure(Error.Validation("Auth.InvalidPin", "Cần mã PIN Quản lý để phê duyệt điều chỉnh giá niêm yết."));

        var oldPrice = item.Price;
        var nowUtc = DateTime.UtcNow;

        return await _uow.ExecuteInTransactionAsync(async () =>
        {
            await _uow.MenuPriceHistories.AddAsync(new MenuPriceHistory
            {
                MenuItemId = item.Id,
                OldPrice = oldPrice,
                NewPrice = dto.NewPrice,
                EffectiveFrom = nowUtc,
                ChangedById = 1
            }, cancellationToken);

            item.Price = dto.NewPrice;

            await _uow.AuditLogs.AddAsync(new AuditLog
            {
                Action = "UpdatePrice",
                EntityName = "MenuItems",
                EntityId = item.Id.ToString(),
                Details = $"Đổi giá từ {oldPrice:N0}đ sang {dto.NewPrice:N0}đ. Lý do: {dto.Reason}",
                CreatedAt = nowUtc
            }, cancellationToken);

            await _uow.SaveChangesAsync(cancellationToken);
            return Result.Success();
        }, cancellationToken);
    }

    public async Task<Result> ToggleItemAvailabilityAsync(int menuItemId, CancellationToken cancellationToken = default)
    {
        var item = await _uow.MenuItems.GetByIdAsync(menuItemId, cancellationToken);
        if (item == null)
            return Result.Failure(Error.NotFound("MenuItem.NotFound", "Không tìm thấy món ăn."));

        item.IsAvailable = !item.IsAvailable;
        await _uow.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    private async Task<bool> ValidateManagerPinAsync(string? pin, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(pin)) return false;

        var managers = await _uow.Staffs.GetAll()
            .Where(s => s.Role == StaffRole.Manager && s.IsActive && s.ApprovalPinHash != null)
            .ToListAsync(cancellationToken);

        foreach (var mgr in managers)
        {
            var verify = _passwordHasher.VerifyHashedPassword(mgr, mgr.ApprovalPinHash!, pin);
            if (verify == PasswordVerificationResult.Success)
                return true;
        }

        return false;
    }
}
