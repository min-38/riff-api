using FluentValidation.TestHelper;
using api.DTOs.Requests;
using api.Models.Enums;
using api.Validation;

namespace api.Tests.Validation;

public class GetGearsRequestValidationTests
{
    private readonly GetGearsRequestValidation _validator;

    public GetGearsRequestValidationTests()
    {
        _validator = new GetGearsRequestValidation();
    }

    #region Page 검증

    [Fact]
    public void Page_Zero_ShouldHaveValidationError()
    {
        var request = new GetGearsRequest
        {
            Page = 0
        };

        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.Page);
    }

    [Fact]
    public void Page_Negative_ShouldHaveValidationError()
    {
        var request = new GetGearsRequest
        {
            Page = -1
        };

        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.Page);
    }

    [Fact]
    public void Page_Valid_ShouldNotHaveValidationError()
    {
        var request = new GetGearsRequest
        {
            Page = 1
        };

        var result = _validator.TestValidate(request);
        result.ShouldNotHaveValidationErrorFor(x => x.Page);
    }

    #endregion

    #region PageSize 검증

    [Fact]
    public void PageSize_Zero_ShouldHaveValidationError()
    {
        var request = new GetGearsRequest
        {
            PageSize = 0
        };

        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.PageSize);
    }

    [Fact]
    public void PageSize_Negative_ShouldHaveValidationError()
    {
        var request = new GetGearsRequest
        {
            PageSize = -1
        };

        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.PageSize);
    }

    [Fact]
    public void PageSize_TooLarge_ShouldHaveValidationError()
    {
        var request = new GetGearsRequest
        {
            PageSize = 101
        };

        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.PageSize);
    }

    [Fact]
    public void PageSize_Valid_ShouldNotHaveValidationError()
    {
        var request = new GetGearsRequest
        {
            PageSize = 20
        };

        var result = _validator.TestValidate(request);
        result.ShouldNotHaveValidationErrorFor(x => x.PageSize);
    }

    [Fact]
    public void PageSize_MaxValid_ShouldNotHaveValidationError()
    {
        var request = new GetGearsRequest
        {
            PageSize = 100
        };

        var result = _validator.TestValidate(request);
        result.ShouldNotHaveValidationErrorFor(x => x.PageSize);
    }

    #endregion

    #region Price 범위 필터 (검증 없음)

    [Fact]
    public void MinPrice_Negative_ShouldNotHaveValidationError()
    {
        var request = new GetGearsRequest
        {
            MinPrice = -100
        };

        var result = _validator.TestValidate(request);
        result.ShouldNotHaveValidationErrorFor(x => x.MinPrice);
    }

    [Fact]
    public void MaxPrice_Negative_ShouldNotHaveValidationError()
    {
        var request = new GetGearsRequest
        {
            MaxPrice = -100
        };

        var result = _validator.TestValidate(request);
        result.ShouldNotHaveValidationErrorFor(x => x.MaxPrice);
    }

    [Fact]
    public void PriceRange_MinGreaterThanMax_ShouldNotHaveValidationError()
    {
        var request = new GetGearsRequest
        {
            MinPrice = 200000,
            MaxPrice = 100000
        };

        var result = _validator.TestValidate(request);
        result.ShouldNotHaveValidationErrorFor(x => x.MinPrice);
        result.ShouldNotHaveValidationErrorFor(x => x.MaxPrice);
    }

    [Fact]
    public void PriceRange_Valid_ShouldNotHaveValidationError()
    {
        var request = new GetGearsRequest
        {
            MinPrice = 100000,
            MaxPrice = 200000
        };

        var result = _validator.TestValidate(request);
        result.ShouldNotHaveValidationErrorFor(x => x.MinPrice);
        result.ShouldNotHaveValidationErrorFor(x => x.MaxPrice);
    }

    [Fact]
    public void PriceRange_MinEqualsMax_ShouldNotHaveValidationError()
    {
        var request = new GetGearsRequest
        {
            MinPrice = 150000,
            MaxPrice = 150000
        };

        var result = _validator.TestValidate(request);
        result.ShouldNotHaveValidationErrorFor(x => x.MinPrice);
        result.ShouldNotHaveValidationErrorFor(x => x.MaxPrice);
    }

    #endregion

    #region SearchKeyword 검증

    [Fact]
    public void SearchKeyword_TooLong_ShouldHaveValidationError()
    {
        var request = new GetGearsRequest
        {
            SearchKeyword = new string('A', 101)
        };

        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.SearchKeyword);
    }

    [Fact]
    public void SearchKeyword_Valid_ShouldNotHaveValidationError()
    {
        var request = new GetGearsRequest
        {
            SearchKeyword = "Fender Stratocaster"
        };

        var result = _validator.TestValidate(request);
        result.ShouldNotHaveValidationErrorFor(x => x.SearchKeyword);
    }

    [Fact]
    public void SearchKeyword_Null_ShouldNotHaveValidationError()
    {
        var request = new GetGearsRequest
        {
            SearchKeyword = null
        };

        var result = _validator.TestValidate(request);
        result.ShouldNotHaveValidationErrorFor(x => x.SearchKeyword);
    }

    [Fact]
    public void SearchKeyword_Empty_ShouldNotHaveValidationError()
    {
        var request = new GetGearsRequest
        {
            SearchKeyword = ""
        };

        var result = _validator.TestValidate(request);
        result.ShouldNotHaveValidationErrorFor(x => x.SearchKeyword);
    }

    #endregion

    #region SortBy 검증

    [Fact]
    public void SortBy_Empty_ShouldHaveValidationError()
    {
        var request = new GetGearsRequest
        {
            SortBy = ""
        };

        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.SortBy);
    }

    [Fact]
    public void SortBy_Invalid_ShouldHaveValidationError()
    {
        var request = new GetGearsRequest
        {
            SortBy = "invalid_field"
        };

        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.SortBy);
    }

    [Fact]
    public void SortBy_CreatedAt_ShouldNotHaveValidationError()
    {
        var request = new GetGearsRequest
        {
            SortBy = "created_at"
        };

        var result = _validator.TestValidate(request);
        result.ShouldNotHaveValidationErrorFor(x => x.SortBy);
    }

    [Fact]
    public void SortBy_Price_ShouldNotHaveValidationError()
    {
        var request = new GetGearsRequest
        {
            SortBy = "price"
        };

        var result = _validator.TestValidate(request);
        result.ShouldNotHaveValidationErrorFor(x => x.SortBy);
    }

    [Fact]
    public void SortBy_ViewCount_ShouldNotHaveValidationError()
    {
        var request = new GetGearsRequest
        {
            SortBy = "view_count"
        };

        var result = _validator.TestValidate(request);
        result.ShouldNotHaveValidationErrorFor(x => x.SortBy);
    }

    [Fact]
    public void SortBy_CaseInsensitive_ShouldNotHaveValidationError()
    {
        var request = new GetGearsRequest
        {
            SortBy = "CREATED_AT"
        };

        var result = _validator.TestValidate(request);
        result.ShouldNotHaveValidationErrorFor(x => x.SortBy);
    }

    #endregion

    #region SortOrder 검증

    [Fact]
    public void SortOrder_Empty_ShouldHaveValidationError()
    {
        var request = new GetGearsRequest
        {
            SortOrder = ""
        };

        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.SortOrder);
    }

    [Fact]
    public void SortOrder_Invalid_ShouldHaveValidationError()
    {
        var request = new GetGearsRequest
        {
            SortOrder = "invalid"
        };

        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.SortOrder);
    }

    [Fact]
    public void SortOrder_Asc_ShouldNotHaveValidationError()
    {
        var request = new GetGearsRequest
        {
            SortOrder = "asc"
        };

        var result = _validator.TestValidate(request);
        result.ShouldNotHaveValidationErrorFor(x => x.SortOrder);
    }

    [Fact]
    public void SortOrder_Desc_ShouldNotHaveValidationError()
    {
        var request = new GetGearsRequest
        {
            SortOrder = "desc"
        };

        var result = _validator.TestValidate(request);
        result.ShouldNotHaveValidationErrorFor(x => x.SortOrder);
    }

    [Fact]
    public void SortOrder_CaseInsensitive_ShouldNotHaveValidationError()
    {
        var request = new GetGearsRequest
        {
            SortOrder = "DESC"
        };

        var result = _validator.TestValidate(request);
        result.ShouldNotHaveValidationErrorFor(x => x.SortOrder);
    }

    #endregion

    #region Enum 필터 검증

    [Fact]
    public void Category_Valid_ShouldNotHaveValidationError()
    {
        var request = new GetGearsRequest
        {
            Category = GearCategory.Instrument
        };

        var result = _validator.TestValidate(request);
        result.ShouldNotHaveValidationErrorFor(x => x.Category);
    }

    [Fact]
    public void SubCategory_Valid_ShouldNotHaveValidationError()
    {
        var request = new GetGearsRequest
        {
            SubCategory = GearSubCategory.Guitar
        };

        var result = _validator.TestValidate(request);
        result.ShouldNotHaveValidationErrorFor(x => x.SubCategory);
    }

    [Fact]
    public void DetailCategory_Valid_ShouldNotHaveValidationError()
    {
        var request = new GetGearsRequest
        {
            DetailCategory = GearDetailCategory.Electric
        };

        var result = _validator.TestValidate(request);
        result.ShouldNotHaveValidationErrorFor(x => x.DetailCategory);
    }

    [Fact]
    public void Status_Valid_ShouldNotHaveValidationError()
    {
        var request = new GetGearsRequest
        {
            Status = GearStatus.Selling
        };

        var result = _validator.TestValidate(request);
        result.ShouldNotHaveValidationErrorFor(x => x.Status);
    }

    [Fact]
    public void Condition_Valid_ShouldNotHaveValidationError()
    {
        var request = new GetGearsRequest
        {
            Condition = GearCondition.Good
        };

        var result = _validator.TestValidate(request);
        result.ShouldNotHaveValidationErrorFor(x => x.Condition);
    }

    [Fact]
    public void TradeMethod_Valid_ShouldNotHaveValidationError()
    {
        var request = new GetGearsRequest
        {
            TradeMethod = TradeMethod.Both
        };

        var result = _validator.TestValidate(request);
        result.ShouldNotHaveValidationErrorFor(x => x.TradeMethod);
    }

    [Fact]
    public void Region_Valid_ShouldNotHaveValidationError()
    {
        var request = new GetGearsRequest
        {
            Region = Region.Seoul
        };

        var result = _validator.TestValidate(request);
        result.ShouldNotHaveValidationErrorFor(x => x.Region);
    }

    #endregion

    #region 복합 필터 시나리오

    [Fact]
    public void ComplexFilter_AllFiltersValid_ShouldNotHaveValidationError()
    {
        var request = new GetGearsRequest
        {
            Page = 2,
            PageSize = 30,
            Category = GearCategory.Instrument,
            SubCategory = GearSubCategory.Guitar,
            DetailCategory = GearDetailCategory.Electric,
            Status = GearStatus.Selling,
            Condition = GearCondition.Good,
            TradeMethod = TradeMethod.Both,
            Region = Region.Seoul,
            MinPrice = 100000,
            MaxPrice = 500000,
            SearchKeyword = "Fender",
            SortBy = "price",
            SortOrder = "asc"
        };

        var result = _validator.TestValidate(request);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void DefaultValues_ShouldNotHaveValidationError()
    {
        var request = new GetGearsRequest();

        var result = _validator.TestValidate(request);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void MinimalFilter_OnlyPageAndSize_ShouldNotHaveValidationError()
    {
        var request = new GetGearsRequest
        {
            Page = 1,
            PageSize = 10
        };

        var result = _validator.TestValidate(request);
        result.ShouldNotHaveAnyValidationErrors();
    }

    #endregion
}
