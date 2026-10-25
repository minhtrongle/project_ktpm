namespace RestaurantManagement.Business.Options;

public class RestaurantRulesOptions
{
    public const string SectionName = "RestaurantRules";

    public DepositRules Deposit { get; set; } = new();
    public BookingRules Booking { get; set; } = new();
    public BillingRules Billing { get; set; } = new();
    public ShiftRules Shift { get; set; } = new();
    public string TimeZone { get; set; } = "Asia/Ho_Chi_Minh";
}

public class DepositRules
{
    public decimal Small { get; set; } = 200000;
    public decimal Large { get; set; } = 500000;
    public int LargeGroupFrom { get; set; } = 6;
}

public class BookingRules
{
    public int HoldMinutes { get; set; } = 15;
    public int SlotHours { get; set; } = 2;
    public int MinLeadMinutes { get; set; } = 60;
    public int NoShowGraceMinutes { get; set; } = 30;
}

public class BillingRules
{
    public decimal VatRate { get; set; } = 0.08m;
    public decimal PeakSurcharge { get; set; } = 50000;
    public List<string> PeakDays { get; set; } = ["Saturday", "Sunday"];
    public string PeakFrom { get; set; } = "18:00";
    public string PeakTo { get; set; } = "21:00";
    public MembershipDiscountRules MembershipDiscount { get; set; } = new();
    public decimal ManagerApprovalDiscountAbove { get; set; } = 0.20m;
}

public class MembershipDiscountRules
{
    public decimal Silver { get; set; } = 0.05m;
    public decimal Gold { get; set; } = 0.10m;
    public decimal Diamond { get; set; } = 0.15m;
}

public class ShiftRules
{
    public decimal DefaultInitialCash { get; set; } = 2000000;
    public int LateThresholdMinutes { get; set; } = 15;
}
