namespace RestaurantManagement.Common.Constants;

public static class Roles
{
    public const string Manager = "Manager";
    public const string Cashier = "Cashier";
    public const string Staff = "Staff";
    public const string Kitchen = "Kitchen";
}

public static class Policies
{
    public const string ManagerOnly = "ManagerOnly";
    public const string CashierOrManager = "CashierOrManager";
    public const string StaffOnly = "StaffOnly";
    public const string PosAccess = "PosAccess";
    public const string AdminAccess = "AdminAccess";
}
