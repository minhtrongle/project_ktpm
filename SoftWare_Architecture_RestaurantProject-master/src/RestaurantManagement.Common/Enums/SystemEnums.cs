namespace RestaurantManagement.Common.Enums;

public enum TableStatus
{
    Available = 0,      // Trống
    Occupied = 1,       // Đang phục vụ khách
    PreCheck = 2,       // Đang tạm tính (chờ thanh toán)
    Cleaning = 3,       // Đang dọn dẹp
    OutOfService = 4    // Tạm ngưng phục vụ (bảo trì, hỏng)
}

public enum BookingStatus
{
    PendingPayment = 0, // Chờ thanh toán tiền cọc (trong 15 phút)
    Confirmed = 1,      // Đã xác nhận (đã cọc)
    CheckedIn = 2,      // Khách đã đến nhận bàn
    Completed = 3,      // Đã hoàn thành phục vụ
    Cancelled = 4,      // Đã hủy bởi khách/nhà hàng
    Expired = 5,        // Quá hạn 15 phút không thanh toán cọc
    NoShow = 6          // Khách không đến quá 30 phút, tịch thu cọc
}

public enum BookingSource
{
    Online = 0,
    Phone = 1,
    WalkIn = 2
}

public enum OrderItemStatus
{
    Pending = 0,    // Mới gọi, chưa gửi bếp
    Cooking = 1,    // Đang chế biến trong bếp
    Ready = 2,      // Đã xong, chờ bưng
    Served = 3,     // Đã phục vụ ra bàn
    Cancelled = 4,  // Hủy món trước khi nấu
    Returned = 5    // Trả món sau khi nấu (hỏng, khách đổi)
}

public enum StaffRole
{
    Manager = 0,    // Quản lý
    Cashier = 1,    // Thu ngân
    Staff = 2,      // Nhân viên phục vụ
    Kitchen = 3     // Bếp
}

public enum KitchenStation
{
    ColdKitchen = 0, // Bếp lạnh (gỏi, khai vị...)
    HotKitchen = 1,  // Bếp nóng (chiên, xào, nướng, lẩu...)
    Bar = 2          // Quầy bar (pha chế đồ uống)
}

public enum PaymentMethod
{
    Cash = 0,
    Gateway = 1,      // VNPay / Cổng thanh toán trực tuyến
    BankTransfer = 2  // Chuyển khoản VietQR
}

public enum PaymentTransactionType
{
    DepositCharge = 0, // Thu tiền cọc đặt bàn
    DepositRefund = 1  // Hoàn cọc đặt bàn
}

public enum TransactionStatus
{
    Pending = 0,
    Success = 1,
    Failed = 2
}

public enum InvoiceStatus
{
    Paid = 0,    // Đã thanh toán
    Voided = 1   // Đã hủy
}

public enum ShiftStatus
{
    Active = 0,  // Đang hoạt động
    Locked = 1,  // Đang khóa đối soát két
    Closed = 2   // Đã chốt ca
}

public enum KotTicketType
{
    New = 0,
    Adjust = 1,
    Cancel = 2,
    Reprint = 3
}

public enum KotTicketStatus
{
    Queued = 0,
    Printed = 1,
    Failed = 2
}

public enum MembershipRank
{
    Standard = 0,
    Silver = 1,
    Gold = 2,
    Diamond = 3
}
