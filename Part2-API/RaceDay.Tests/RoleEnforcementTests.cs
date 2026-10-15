using System.Reflection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using RaceDay.Api.Controllers;
using RaceDay.Api.Security;

namespace RaceDay.Tests;

/// <summary>
/// Role-based access is enforced by [SessionAuthorize]. These tests run the filter directly
/// (success and failure cases) and check that the protected endpoints declare the right role.
/// </summary>
public class RoleEnforcementTests
{
    private static AuthorizationFilterContext BuildContext(int? userId, string? role, bool withSession = true)
    {
        var http = new DefaultHttpContext();
        if (withSession)
        {
            var session = new TestSession();
            if (userId.HasValue) session.SetInt32(SessionKeys.UserId, userId.Value);
            if (role != null) session.SetString(SessionKeys.Role, role);
            http.Session = session;
        }

        var actionContext = new ActionContext(http, new RouteData(), new ActionDescriptor());
        return new AuthorizationFilterContext(actionContext, new List<IFilterMetadata>());
    }

    [Fact]
    public void NoSession_IsRejectedAsUnauthorized()
    {
        var context = BuildContext(null, null, withSession: false);

        new SessionAuthorizeAttribute(Roles.Organiser).OnAuthorization(context);

        Assert.IsType<UnauthorizedObjectResult>(context.Result);
    }

    [Fact]
    public void EmptySession_IsRejectedAsUnauthorized()
    {
        var context = BuildContext(null, null);

        new SessionAuthorizeAttribute(Roles.Participant).OnAuthorization(context);

        Assert.IsType<UnauthorizedObjectResult>(context.Result);
    }

    [Fact]
    public void Participant_CannotUseOrganiserEndpoint()
    {
        var context = BuildContext(1, Roles.Participant);

        new SessionAuthorizeAttribute(Roles.Organiser).OnAuthorization(context);

        var result = Assert.IsType<ObjectResult>(context.Result);
        Assert.Equal(403, result.StatusCode);
    }

    [Fact]
    public void Organiser_CannotUseParticipantEndpoint()
    {
        var context = BuildContext(1, Roles.Organiser);

        new SessionAuthorizeAttribute(Roles.Participant).OnAuthorization(context);

        var result = Assert.IsType<ObjectResult>(context.Result);
        Assert.Equal(403, result.StatusCode);
    }

    [Theory]
    [InlineData(Roles.Organiser)]
    [InlineData(Roles.Participant)]
    public void MatchingRole_IsAllowed(string role)
    {
        var context = BuildContext(1, role);

        new SessionAuthorizeAttribute(role).OnAuthorization(context);

        Assert.Null(context.Result);
    }

    [Theory]
    [InlineData(Roles.Organiser)]
    [InlineData(Roles.Participant)]
    public void AnyLoggedInUser_IsAllowed_WhenNoRoleRequired(string role)
    {
        var context = BuildContext(1, role);

        new SessionAuthorizeAttribute().OnAuthorization(context);

        Assert.Null(context.Result);
    }

    [Theory]
    [InlineData(typeof(EventsController), nameof(EventsController.CreateEvent), Roles.Organiser)]
    [InlineData(typeof(EventsController), nameof(EventsController.UpdateEvent), Roles.Organiser)]
    [InlineData(typeof(EventsController), nameof(EventsController.DeleteEvent), Roles.Organiser)]
    [InlineData(typeof(CategoriesController), nameof(CategoriesController.CreateCategory), Roles.Organiser)]
    [InlineData(typeof(CategoriesController), nameof(CategoriesController.DeleteCategory), Roles.Organiser)]
    [InlineData(typeof(RouteInfoController), nameof(RouteInfoController.SaveRoute), Roles.Organiser)]
    [InlineData(typeof(EnrolmentsController), nameof(EnrolmentsController.GetEventEnrolments), Roles.Organiser)]
    [InlineData(typeof(ResultsController), nameof(ResultsController.CaptureResult), Roles.Organiser)]
    [InlineData(typeof(ResultsController), nameof(ResultsController.UpdateResult), Roles.Organiser)]
    [InlineData(typeof(EnrolmentsController), nameof(EnrolmentsController.Enrol), Roles.Participant)]
    [InlineData(typeof(EnrolmentsController), nameof(EnrolmentsController.GetMyEnrolments), Roles.Participant)]
    [InlineData(typeof(EnrolmentsController), nameof(EnrolmentsController.CancelEnrolment), Roles.Participant)]
    [InlineData(typeof(ResultsController), nameof(ResultsController.GetMyResults), Roles.Participant)]
    [InlineData(typeof(ProfileController), nameof(ProfileController.GetOrganiser), Roles.Organiser)]
    [InlineData(typeof(ProfileController), nameof(ProfileController.GetParticipant), Roles.Participant)]
    public void ProtectedEndpoint_DeclaresExpectedRole(Type controller, string action, string expectedRole)
    {
        var method = controller.GetMethod(action);

        Assert.NotNull(method);
        var attribute = method!.GetCustomAttribute<SessionAuthorizeAttribute>();
        Assert.NotNull(attribute);
        Assert.Equal(expectedRole, attribute!.Role);
    }

    [Theory]
    [InlineData(typeof(EventsController), nameof(EventsController.GetEvents))]
    [InlineData(typeof(EventsController), nameof(EventsController.GetEvent))]
    [InlineData(typeof(CategoriesController), nameof(CategoriesController.GetCategories))]
    [InlineData(typeof(AuthController), nameof(AuthController.Login))]
    [InlineData(typeof(AuthController), nameof(AuthController.RegisterParticipant))]
    public void PublicEndpoint_HasNoAuthorizationRequirement(Type controller, string action)
    {
        var method = controller.GetMethod(action);

        Assert.NotNull(method);
        Assert.Null(method!.GetCustomAttribute<SessionAuthorizeAttribute>());
    }
}
