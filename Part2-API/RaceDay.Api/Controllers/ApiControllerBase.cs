using Microsoft.AspNetCore.Mvc;
using RaceDay.Api.Dtos;
using RaceDay.Api.Security;

namespace RaceDay.Api.Controllers;

/// <summary>Shared helpers: reads the logged-in user from the server-side session.</summary>
public abstract class ApiControllerBase : ControllerBase
{
    protected int? CurrentUserId => HttpContext.Session.GetInt32(SessionKeys.UserId);

    protected string? CurrentRole => HttpContext.Session.GetString(SessionKeys.Role);

    protected ObjectResult Forbidden(string message) =>
        StatusCode(StatusCodes.Status403Forbidden, new ApiError(message));
}
