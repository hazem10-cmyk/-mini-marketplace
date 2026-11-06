namespace Marketplace.Api.Endpoints;

public static class Paging
{
    public static Dictionary<string, string[]> Validate(int page, int pageSize)
    {
        var errors = new Dictionary<string, string[]>();

        if (page < 1 || page > 100_000)
            errors["page"] = ["page must be between 1 and 100000"];

        if (pageSize < 1 || pageSize > 50)
            errors["pageSize"] = ["pageSize must be between 1 and 50"];

        return errors;
    }

    public static int TotalPages(int totalItems, int pageSize) =>
        (int)Math.Ceiling(totalItems / (double)pageSize);
}