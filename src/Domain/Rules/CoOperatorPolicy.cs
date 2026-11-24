namespace Domain.Rules;

/// <summary>
/// Co-operating an event (đồng điều hành). The creator is the primary operator; only they or an Admin change the list of operators.
/// A co-operator is an active Manager added with a reason; the primary operator is never removed; a settled or closed event no
/// longer takes new operators. Pure functions.
/// </summary>
public static class CoOperatorPolicy
{
    public const string Primary = "Primary";
    public const string CoManager = "CoManager";

    public static bool CanManage(int primaryManagerId, int actorId, string actorRole) =>
        actorRole == AccountPolicy.Admin || actorRole == AccountPolicy.Manager && primaryManagerId == actorId;

    /// <summary>The reason of an addition or a removal: 5–500 characters. Returns it trimmed.</summary>
    public static string ValidateReason(string? reason, bool removal)
    {
        var text = reason?.Trim() ?? "";
        if (text.Length is < 5 or > 500)
            throw new BusinessRuleException(removal ? "Vui lòng nhập lý do gỡ quyền (5–500 ký tự)." : "Vui lòng nhập lý do ủy quyền (5–500 ký tự).");
        return text;
    }
}
