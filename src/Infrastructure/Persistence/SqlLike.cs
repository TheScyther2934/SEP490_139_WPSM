namespace Infrastructure.Persistence;

/// <summary>Patterns for EF.Functions.ILike (case-insensitive search on PostgreSQL); the user's text is matched literally.</summary>
internal static class SqlLike
{
    public static string Contains(string term) =>
        "%" + term.Replace("\\", "\\\\").Replace("%", @"\%").Replace("_", @"\_") + "%";
}
