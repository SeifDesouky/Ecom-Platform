namespace EcomPlatform.Core.Enums
{
    public enum AuditAction
    {
        Create = 1,
        Update = 2,
        Delete = 3,
        Login = 4,
        Logout = 5,
        StatusChange = 6,
        PasswordChange = 7,
        RoleChange = 8,
        FailedLogin = 9,   // ← جديد
        SecurityAlert = 10   // ← جديد
    }
}