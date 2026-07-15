namespace GymAffiliate.Shared.Constants;

public static class StoredProcedures
{
    public const string Affiliates    = "sp_Affiliates";
    public const string Memberships   = "sp_Memberships";
    public const string Payments      = "sp_Payments";
    public const string CheckIn       = "sp_CheckIn";
    public const string Notifications = "sp_Notifications";
    public const string Branches      = "sp_Branches";
    public const string Reports       = "sp_Reports";
    public const string Auth          = "sp_Auth";
    public const string MembershipTypes = "sp_MembershipTypes_Listar";
 }

public static class Roles
{
    public const string SuperAdmin = "SUPERADMIN";
    public const string Admin      = "ADMIN";
    public const string Reception  = "RECEPTION";
    public const string ReadOnly   = "READONLY";
    public const string Trainer    = "TRAINER"; 

}

public static class Policies
{
    public const string AdminOnly        = "AdminOnly";
    public const string ReceptionOrAdmin = "ReceptionOrAdmin";
    public const string AnyRole          = "AnyRole";
    public const string SuperAdminOnly = "SuperAdminOnly"; 

}
public static class ClaimTypes
{
    // Nombres de claims que viajarán en el JWT
    public const string UserId = "userId";
    public const string RoleCode = "roleCode";
    public const string BranchId = "branchId";
    public const string FullName = "fullName";
}


