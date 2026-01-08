namespace api.Constants;

// API 에러 코드 상수
public static class ErrorCodes
{
    // ==================== API 에러 코드 ====================

    // 인증/인가 관련
    public const string INVALID_TOKEN = "INVALID_TOKEN";
    public const string UNAUTHORIZED = "UNAUTHORIZED";
    public const string FORBIDDEN = "FORBIDDEN";

    // 리소스 관련
    public const string NOT_FOUND = "NOT_FOUND";
    public const string ALREADY_EXISTS = "ALREADY_EXISTS";

    // 서버 에러
    public const string INTERNAL_SERVER_ERROR = "INTERNAL_SERVER_ERROR";
    public const string INVALID_OPERATION = "INVALID_OPERATION";

    // ==================== 검증 에러 코드 ====================

    // 공통 에러 코드
    public const string FIELD_REQUIRED = "FIELD_REQUIRED";
    public const string FIELD_TOO_SHORT = "FIELD_TOO_SHORT";
    public const string FIELD_TOO_LONG = "FIELD_TOO_LONG";
    public const string INVALID_ENUM_VALUE = "INVALID_ENUM_VALUE";

    // 가격 관련
    public const string PRICE_TOO_LOW = "PRICE_TOO_LOW";
    public const string PRICE_TOO_HIGH = "PRICE_TOO_HIGH";

    // 카테고리 관련
    public const string INVALID_CATEGORY = "INVALID_CATEGORY";
    public const string INVALID_SUBCATEGORY = "INVALID_SUBCATEGORY";
    public const string CATEGORY_SUBCATEGORY_MISMATCH = "CATEGORY_SUBCATEGORY_MISMATCH";

    // 장비 상태
    public const string INVALID_CONDITION = "INVALID_CONDITION";
    public const string INVALID_STATUS = "INVALID_STATUS";

    // 거래 방식
    public const string INVALID_TRADE_METHOD = "INVALID_TRADE_METHOD";

    // 지역
    public const string INVALID_REGION = "INVALID_REGION";

    // 이미지 관련
    public const string IMAGE_REQUIRED = "IMAGE_REQUIRED";
    public const string TOO_MANY_IMAGES = "TOO_MANY_IMAGES";
    public const string IMAGE_TOO_LARGE = "IMAGE_TOO_LARGE";
    public const string INVALID_IMAGE_TYPE = "INVALID_IMAGE_TYPE";
    public const string NO_VALID_IMAGES = "NO_VALID_IMAGES";
    public const string INVALID_IMAGE_URL = "INVALID_IMAGE_URL";

    // 페이징 관련
    public const string INVALID_PAGE = "INVALID_PAGE";
    public const string INVALID_PAGE_SIZE = "INVALID_PAGE_SIZE";

    // 가격 범위 관련
    public const string INVALID_PRICE_RANGE = "INVALID_PRICE_RANGE";

    // 정렬 관련
    public const string INVALID_SORT_BY = "INVALID_SORT_BY";
    public const string INVALID_SORT_ORDER = "INVALID_SORT_ORDER";
}
