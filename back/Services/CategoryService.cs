using FamilyExpenses.Api.Models;
using FamilyExpenses.Api.Repositories;
using static FamilyExpenses.Api.Models.ResponseMapper;

namespace FamilyExpenses.Api.Services;

public interface ICategoryService
{
    IReadOnlyCollection<CategoryResponse>? List(EntryKind kind, bool? isActive);
    CategoryResponse? Get(EntryKind kind, long id);
    (CategoryResponse? Response, ApiError? Error, bool Unauthorized) Create(EntryKind kind, CreateCategoryRequest request);
    (CategoryResponse? Response, ApiError? Error, bool Unauthorized, bool NotFound) Update(EntryKind kind, long id, UpdateCategoryRequest request);
    bool Deactivate(EntryKind kind, long id, out bool unauthorized, out bool notFound);
}

public sealed class CategoryService(ICurrentUserService currentUser, ICategoryRepository categories) : ICategoryService
{
    public IReadOnlyCollection<CategoryResponse>? List(EntryKind kind, bool? isActive)
    {
        if (!currentUser.TryGetUserId(out var userId))
        {
            return null;
        }

        return categories.GetByUser(userId, kind, isActive).Select(ToCategoryResponse).ToList();
    }

    public CategoryResponse? Get(EntryKind kind, long id)
    {
        if (!currentUser.TryGetUserId(out var userId))
        {
            return null;
        }

        var category = categories.GetById(userId, kind, id);
        return category is null ? null : ToCategoryResponse(category);
    }

    public (CategoryResponse? Response, ApiError? Error, bool Unauthorized) Create(EntryKind kind, CreateCategoryRequest request)
    {
        if (!currentUser.TryGetUserId(out var userId))
        {
            return (null, null, true);
        }

        if (ServiceHelpers.IsNumericName(request.Name))
        {
            return (null, ApiError.Validation([new ErrorDetail("name", "Category name cannot be a number")]), false);
        }

        if (categories.NameExists(userId, kind, request.Name))
        {
            return (null, ApiError.Validation([new ErrorDetail("name", "Category name must be unique")]), false);
        }

        var category = categories.Add(userId, kind, request.Name, request.IsActive ?? true);
        return (ToCategoryResponse(category), null, false);
    }

    public (CategoryResponse? Response, ApiError? Error, bool Unauthorized, bool NotFound) Update(EntryKind kind, long id, UpdateCategoryRequest request)
    {
        if (!currentUser.TryGetUserId(out var userId))
        {
            return (null, null, true, false);
        }

        var category = categories.GetById(userId, kind, id);
        if (category is null)
        {
            return (null, null, false, true);
        }

        if (!string.IsNullOrWhiteSpace(request.Name) && ServiceHelpers.IsNumericName(request.Name))
        {
            return (null, ApiError.Validation([new ErrorDetail("name", "Category name cannot be a number")]), false, false);
        }

        if (!string.IsNullOrWhiteSpace(request.Name) && categories.NameExists(userId, kind, request.Name, id))
        {
            return (null, ApiError.Validation([new ErrorDetail("name", "Category name must be unique")]), false, false);
        }

        if (!string.IsNullOrWhiteSpace(request.Name))
        {
            category.Name = request.Name.Trim();
        }

        if (request.IsActive is not null)
        {
            category.IsActive = request.IsActive.Value;
        }

        category = categories.Update(category);
        return (ToCategoryResponse(category), null, false, false);
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

        var category = categories.GetById(userId, kind, id);
        if (category is null)
        {
            notFound = true;
            return false;
        }

        category.IsActive = false;
        categories.Update(category);
        return true;
    }
}
