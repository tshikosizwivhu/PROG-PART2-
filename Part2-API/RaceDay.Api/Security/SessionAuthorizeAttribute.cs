using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using RaceDay.Api.Dtos;

namespace RaceDay.Api.Security;

/// <summary>
/// Server-side authorization based on the session.
/// With no role it only requires a logged-in user; with a role it also requires that role.
/// Returns 401 when nobody is logged in and 403 when the role is not allowed.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
public class SessionAuthorizeAttribute : Attribute, IAuthorizationFilter
{
    public string? Role { get; }

    public SessionAuthorizeAttribute() { }

    public SessionAuthorizeAttribute(string role)
    {
        Role = role;
    }

    public void OnAuthorization(AuthorizationFilterContext context)
    {
        var session = context.HttpContext.Features.Get<ISessionFeature>()?.Session;
        var userId = session?.GetInt32(SessionKeys.UserId);
        var role = session?.GetString(SessionKeys.Role);

        if (userId is null || string.IsNullOrEmpty(role))
        {
            context.Result = new UnauthorizedObjectResult(new ApiError("You must be logged in to use this endpoint."));
            return;
        }

        if (Role is not null && !string.Equals(role, Role, StringComparison.Ordinal))
        {
            context.Result = new ObjectResult(new ApiError($"This endpoint is only available to the {Role} role."))
            {
                StatusCode = StatusCodes.Status403Forbidden
            };
        }
    }
}
