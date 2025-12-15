using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace api.Filters;

// 개발 환경에서만 접근 가능하도록 제한하는 필터
// 운여 환경에서 접근 시 404 Not Found 반환 -> 보안상 엔드포인트 존재를 숨김
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class DevelopmentOnlyAttribute : Attribute, IAuthorizationFilter
{
    public void OnAuthorization(AuthorizationFilterContext context)
    {
        var environment = context.HttpContext.RequestServices
            .GetRequiredService<IWebHostEnvironment>();

        if (!environment.IsDevelopment())
            context.Result = new NotFoundResult();
    }
}
