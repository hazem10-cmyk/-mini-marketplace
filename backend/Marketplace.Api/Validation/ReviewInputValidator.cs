using Marketplace.Api.Dtos;

namespace Marketplace.Api.Validation;

public static class ReviewInputValidator
{
    public static Dictionary<string, string[]> Validate(CreateReviewRequest input)
    {
        var errors = new Dictionary<string, string[]>();

        if (input.Rating is null || input.Rating < 1 || input.Rating > 5)
            errors["rating"] = ["rating is required and must be between 1 and 5"];

        var comment = input.Comment?.Trim();
        if (comment is not null && comment.Length > 1000)
            errors["comment"] = ["comment can be up to 1000 characters"];

        return errors;
    }
}