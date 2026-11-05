using Marketplace.Api.Dtos;

namespace Marketplace.Api.Validation;

public static class ServiceInputValidator
{
    public static Dictionary<string, string[]> Validate(ServiceInput input)
    {
        var errors = new Dictionary<string, string[]>();

        var title = input.Title?.Trim() ?? "";
        if (title.Length < 5 || title.Length > 80)
            errors["title"] = ["title must be 5 to 80 characters"];

        var description = input.Description?.Trim() ?? "";
        if (description.Length == 0 || description.Length > 2000)
            errors["description"] = ["description is required and can be up to 2000 characters"];

        if (input.Price <= 0 || input.Price > 99_999_999.99m)
            errors["price"] = ["price must be greater than 0 and at most 99999999.99"];
        else if (decimal.Round(input.Price, 2) != input.Price)
            errors["price"] = ["price can have at most 2 decimal places"];

        if (input.DeliveryDays < 1 || input.DeliveryDays > 60)
            errors["deliveryDays"] = ["deliveryDays must be between 1 and 60"];

        return errors;
    }
}