using Pottmayer.Tars.Security.Authorization.Abstractions;

namespace Pottmayer.Pandora.Modules.Finances.Abstractions;

/// <summary>
/// Permission keys of the Finances module (<c>finances.resource.action</c>). The module declares them in its
/// <c>ApplicationDI</c>; requests, Assistant tools and checks reference these constants instead of strings.
/// </summary>
public static class FinancesPermissions
{
    /// <summary>Module segment of every key.</summary>
    public const string Module = FinancesModule.Name;

    /// <summary>Actions of this module beyond the standard ones in <see cref="PermissionActions"/>.</summary>
    public static class Actions
    {
        public const string Approve = "approve";
        public const string Assign = "assign";
        public const string Close = "close";
        public const string Pay = "pay";
        public const string Post = "post";
        public const string Reject = "reject";
        public const string Reverse = "reverse";
        public const string Run = "run";
        public const string Void = "void";
    }

    public static class Accounts
    {
        public const string Resource = "accounts";

        public const string Read = Module + "." + Resource + "." + PermissionActions.Read;
        public const string Create = Module + "." + Resource + "." + PermissionActions.Create;
        public const string Update = Module + "." + Resource + "." + PermissionActions.Update;
        public const string Delete = Module + "." + Resource + "." + PermissionActions.Delete;
    }

    public static class Cards
    {
        public const string Resource = "cards";

        public const string Read = Module + "." + Resource + "." + PermissionActions.Read;
        public const string Create = Module + "." + Resource + "." + PermissionActions.Create;
        public const string Update = Module + "." + Resource + "." + PermissionActions.Update;
        public const string Delete = Module + "." + Resource + "." + PermissionActions.Delete;
    }

    public static class Statements
    {
        public const string Resource = "statements";

        public const string Read = Module + "." + Resource + "." + PermissionActions.Read;
        public const string Close = Module + "." + Resource + "." + Actions.Close;
        public const string Pay = Module + "." + Resource + "." + Actions.Pay;
    }

    public static class Transactions
    {
        public const string Resource = "transactions";

        public const string Read = Module + "." + Resource + "." + PermissionActions.Read;
        public const string Create = Module + "." + Resource + "." + PermissionActions.Create;
        public const string Update = Module + "." + Resource + "." + PermissionActions.Update;
        public const string Post = Module + "." + Resource + "." + Actions.Post;
        public const string Reverse = Module + "." + Resource + "." + Actions.Reverse;
        public const string Void = Module + "." + Resource + "." + Actions.Void;
    }

    public static class Pending
    {
        public const string Resource = "pending";

        public const string Read = Module + "." + Resource + "." + PermissionActions.Read;
        public const string Create = Module + "." + Resource + "." + PermissionActions.Create;
        public const string Update = Module + "." + Resource + "." + PermissionActions.Update;
        public const string Approve = Module + "." + Resource + "." + Actions.Approve;
        public const string Reject = Module + "." + Resource + "." + Actions.Reject;
    }

    public static class Recurrences
    {
        public const string Resource = "recurrences";

        public const string Read = Module + "." + Resource + "." + PermissionActions.Read;
        public const string Create = Module + "." + Resource + "." + PermissionActions.Create;
        public const string Update = Module + "." + Resource + "." + PermissionActions.Update;
        public const string Delete = Module + "." + Resource + "." + PermissionActions.Delete;
    }

    public static class Categories
    {
        public const string Resource = "categories";

        public const string Read = Module + "." + Resource + "." + PermissionActions.Read;
        public const string Create = Module + "." + Resource + "." + PermissionActions.Create;
        public const string Update = Module + "." + Resource + "." + PermissionActions.Update;
    }

    public static class Tags
    {
        public const string Resource = "tags";

        public const string Read = Module + "." + Resource + "." + PermissionActions.Read;
        public const string Create = Module + "." + Resource + "." + PermissionActions.Create;
        public const string Update = Module + "." + Resource + "." + PermissionActions.Update;
        public const string Delete = Module + "." + Resource + "." + PermissionActions.Delete;
        public const string Assign = Module + "." + Resource + "." + Actions.Assign;
    }

    public static class Attachments
    {
        public const string Resource = "attachments";

        public const string Read = Module + "." + Resource + "." + PermissionActions.Read;
        public const string Create = Module + "." + Resource + "." + PermissionActions.Create;
        public const string Update = Module + "." + Resource + "." + PermissionActions.Update;
        public const string Delete = Module + "." + Resource + "." + PermissionActions.Delete;
    }

    public static class Imports
    {
        public const string Resource = "imports";

        public const string Read = Module + "." + Resource + "." + PermissionActions.Read;
        public const string Run = Module + "." + Resource + "." + Actions.Run;
    }

    public static class Audit
    {
        public const string Resource = "audit";

        public const string Read = Module + "." + Resource + "." + PermissionActions.Read;
    }
}
