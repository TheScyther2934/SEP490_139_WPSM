using Domain.Rules;

namespace Domain.Entities;

public partial class Rating
{
    /// <summary>One checked rating (PostEventPolicy.ValidateRatings); never edited afterwards (GBR-10).</summary>
    public static Rating Submit(int projectId, RatingInput input, DateTime nowUtc) => new()
    {
        ProjectId = projectId,
        Module = input.Module!,
        Stars = input.Stars,
        Comment = input.Comment,
        CreatedAt = nowUtc
    };
}
