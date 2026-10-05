using FamilyExpenses.Api.Models;
using FamilyExpenses.Api.Repositories;
using static FamilyExpenses.Api.Models.ResponseMapper;

namespace FamilyExpenses.Api.Services;

public interface ISubcategoryService
{
    IReadOnlyCollection<SubcategoryResponse>? List(EntryKind kind, long? categoryId, bool? isActive);
    SubcategoryResponse? Get(EntryKind kind, long id);
    (SubcategoryResponse? Response, ApiError? Error, bool Unauthorized) Create(EntryKind kind, CreateSubcategoryRequest request);
    (SubcategoryResponse? Response, ApiError? Error, bool Unauthorized, bool NotFound) Update(EntryKind kind, long id, UpdateSubcategoryRequest request);
    bool Deactivate(EntryKind kind, long id, out bool unauthorized, out bool notFound);
}

public sealed class SubcategoryService(
    ICurrentUserService currentUser,
    ICategoryRepository categories,
    ISubcategoryRepository subcategories) : ISubcategoryService
{
    public IReadOnlyCollection<SubcategoryResponse>? List(EntryKind kind, long? categoryId, bool? isActive)
    {
        if (!currentUser.TryGetUserId(out var userId))
        {
            return null;
        }

        return subcategories.GetByUser(userId, kind, categoryId, isActive).Select(ToSubcategoryResponse).ToList();
    }

    public SubcategoryResponse? Get(EntryKind kind, long id)
    {
        if (!currentUser.TryGetUserId(out var userId))
        {
            return null;
        }

        var subcategory = subcategories.GetById(userId, kind, id);
        return subcategory is null ? null : ToSubcategoryResponse(subcategory);
    }

    public (SubcategoryResponse? Response, ApiError? Error, bool Unauthorized) Create(EntryKind kind, CreateSubcategoryRequest request)
    {
        if (!currentUser.TryGetUserId(out var userId))
        {
            return (null, null, true);
        }

        var errors = Validate(userId, kind, request.CategoryId, request.Name, null);
        if (errors.Count > 0)
        {
            return (null, ApiError.Validation(errors), false);
        }

        var subcategory = subcategories.Add(userId, kind, request.CategoryId, request.Name, request.IsActive ?? true);
        return (ToSubcategoryResponse(subcategory), null, false);
    }

    public (SubcategoryResponse? Response, ApiError? Error, bool Unauthorized, bool NotFound) Update(EntryKind kind, long id, UpdateSubcategoryRequest request)
    {
        if (!currentUser.TryGetUserId(out var userId))
        {
            return (null, null, true, false);
        }

        var subcategory = subcategories.GetById(userId, kind, id);
        if (subcategory is null)
        {
            return (null, null, false, true);
        }

        var targetCategoryId = request.CategoryId ?? subcategory.CategoryId;
        var targetName = string.IsNullOrWhiteSpace(request.Name) ? subcategory.Name : request.Name;
        var errors = Validate(userId, kind, targetCategoryId, targetName, id);
        if (errors.Count > 0)
        {
            return (null, ApiError.Validation(errors), false, false);
        }

        subcategory.CategoryId = targetCategoryId;
        if (!string.IsNullOrWhiteSpace(request.Name))
        {
            subcategory.Name = request.Name.Trim();
        }

        if (request.IsActive is not null)
        {
            subcategory.IsActive = request.IsActive.Value;
        }

        subcategory = subcategories.Update(subcategory);
        return (ToSubcategoryResponse(subcategory), null, false, false);
    }

    public bool Deactivate(EntryKind kind, long id, out bool unauthorized, out bool notFound)
    {
        unauthorized = false;
        notFound = false;

        if (!currentUser.TryGetUserId(out var userId))
        {
            unauthorized = true;
            return false;
        }

        var subcategory = subcategories.GetById(userId, kind, id);
        if (subcategory is null)
        {
            notFound = true;
            return false;
        }

        subcategory.IsActive = false;
        subcategories.Update(subcategory);
        return true;
    }

    private List<ErrorDetail> Validate(long userId, EntryKind kind, long categoryId, string name, long? exceptId)
    {
        var errors = new List<ErrorDetail>();

        if (categories.GetById(userId, kind, categoryId) is null)
        {
            errors.Add(new ErrorDetail("category_id", "Category was not found"));
        }

        if (ServiceHelpers.IsNumericName(name))
        {
            errors.Add(new ErrorDetail("name", "Subcategory name cannot be a number"));
        }

        if (subcategories.NameExists(userId, kind, categoryId, name, exceptId))
        {
            errors.Add(new ErrorDetail("name", "Subcategory name must be unique inside category"));
        }

        return errors;
    }
}
