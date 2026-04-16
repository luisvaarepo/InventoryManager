namespace InventoryManagement.Security;

public static class Roles
{
    public const string Manager = "Manager";
    public const string Staff = "Staff";

    public static readonly string[] All = [Manager, Staff];
}
