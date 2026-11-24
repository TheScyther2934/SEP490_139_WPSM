using Domain.Rules;

namespace Infrastructure.Persistence;

/// <summary>Kept for the read models; the category list itself is a rule in Domain/Rules/ServiceCategories.</summary>
internal static class WpsmCategoryNames
{
    internal static string Normalize(string category) => ServiceCategories.Normalize(category);

    internal static string ToDisplay(string category) => ServiceCategories.Display(category);
}
