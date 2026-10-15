using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using RaceDay.Api.Controllers;
using RaceDay.Api.Data;
using RaceDay.Api.Dtos;
using RaceDay.Api.Security;

namespace RaceDay.Tests;

public class AuthTests
{
    private static AuthController CreateController(RaceDayDbContext db) =>
        new AuthController(db, Options.Create(new RegistrationOptions { OrganiserCode = "SECRET" })).WithSession();

    private static RegisterDto ParticipantDto(string email = "sipho@example.com") =>
        new() { FullName = "Sipho Ndlovu", Email = email, Password = "Password123!" };

    // ---------- Registration ----------

    [Fact]
    public async Task RegisterParticipant_ValidData_CreatesAccountWithHashedPassword()
    {
        using var db = TestHelpers.NewDb();
        var controller = CreateController(db);

        var result = await controller.RegisterParticipant(ParticipantDto("Sipho@Example.com"));

        var created = Assert.IsType<CreatedResult>(result.Result);
        var user = Assert.IsType<UserDto>(created.Value);
        Assert.Equal(Roles.Participant, user.Role);

        var saved = await db.Participants.SingleAsync();
        Assert.Equal("sipho@example.com", saved.Email);                        // normalised
        Assert.NotEqual("Password123!", saved.PasswordHash);                   // never plain text
        Assert.True(PasswordService.Verify("Password123!", saved.PasswordHash));
    }

    [Fact]
    public async Task RegisterParticipant_DuplicateEmail_ReturnsConflict()
    {
        using var db = TestHelpers.NewDb();
        var controller = CreateController(db);
        await controller.RegisterParticipant(ParticipantDto());

        var result = await controller.RegisterParticipant(ParticipantDto());

        Assert.IsType<ConflictObjectResult>(result.Result);
        Assert.Equal(1, await db.Participants.CountAsync());
    }

    [Fact]
    public async Task RegisterOrganiser_WithCorrectCode_CreatesOrganiser()
    {
        using var db = TestHelpers.NewDb();
        var controller = CreateController(db);

        var result = await controller.RegisterOrganiser(new RegisterOrganiserDto
        {
            FullName = "Thandiwe Mokoena",
            Email = "thandiwe@raceday.co.za",
            Password = "Password123!",
            OrganiserCode = "SECRET"
        });

        var created = Assert.IsType<CreatedResult>(result.Result);
        Assert.Equal(Roles.Organiser, Assert.IsType<UserDto>(created.Value).Role);
        Assert.Equal(1, await db.Organisers.CountAsync());
    }

    [Fact]
    public async Task RegisterOrganiser_WithWrongCode_ReturnsForbidden()
    {
        using var db = TestHelpers.NewDb();
        var controller = CreateController(db);

        var result = await controller.RegisterOrganiser(new RegisterOrganiserDto
        {
            FullName = "Sneaky Person",
            Email = "sneaky@example.com",
            Password = "Password123!",
            OrganiserCode = "WRONG"
        });

        var response = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(403, response.StatusCode);
        Assert.Equal(0, await db.Organisers.CountAsync());
    }

    [Fact]
    public async Task Register_EmailAlreadyUsedByOtherRole_ReturnsConflict()
    {
        using var db = TestHelpers.NewDb();
        var controller = CreateController(db);
        await controller.RegisterOrganiser(new RegisterOrganiserDto
        {
            FullName = "Org", Email = "shared@example.com", Password = "Password123!", OrganiserCode = "SECRET"
        });

        var result = await controller.RegisterParticipant(ParticipantDto("shared@example.com"));

        Assert.IsType<ConflictObjectResult>(result.Result);
    }

    // ---------- Login / logout ----------

    [Fact]
    public async Task Login_ParticipantWithCorrectCredentials_StoresUserIdAndRoleInSession()
    {
        using var db = TestHelpers.NewDb();
        var controller = CreateController(db);
        var registered = await controller.RegisterParticipant(ParticipantDto());
        var id = Assert.IsType<UserDto>(Assert.IsType<CreatedResult>(registered.Result).Value).Id;

        var result = await controller.Login(new LoginDto { Email = "sipho@example.com", Password = "Password123!" });

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal(Roles.Participant, Assert.IsType<UserDto>(ok.Value).Role);
        Assert.Equal(id, controller.HttpContext.Session.GetInt32(SessionKeys.UserId));
        Assert.Equal(Roles.Participant, controller.HttpContext.Session.GetString(SessionKeys.Role));
    }

    [Fact]
    public async Task Login_OrganiserWithCorrectCredentials_StoresOrganiserRole()
    {
        using var db = TestHelpers.NewDb();
        var controller = CreateController(db);
        await controller.RegisterOrganiser(new RegisterOrganiserDto
        {
            FullName = "Org", Email = "org@example.com", Password = "Password123!", OrganiserCode = "SECRET"
        });

        var result = await controller.Login(new LoginDto { Email = "ORG@example.com", Password = "Password123!" });

        Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal(Roles.Organiser, controller.HttpContext.Session.GetString(SessionKeys.Role));
    }

    [Fact]
    public async Task Login_WrongPassword_ReturnsUnauthorizedAndNoSession()
    {
        using var db = TestHelpers.NewDb();
        var controller = CreateController(db);
        await controller.RegisterParticipant(ParticipantDto());

        var result = await controller.Login(new LoginDto { Email = "sipho@example.com", Password = "WrongPassword!" });

        Assert.IsType<UnauthorizedObjectResult>(result.Result);
        Assert.Null(controller.HttpContext.Session.GetInt32(SessionKeys.UserId));
        Assert.Null(controller.HttpContext.Session.GetString(SessionKeys.Role));
    }

    [Fact]
    public async Task Login_UnknownEmail_ReturnsUnauthorized()
    {
        using var db = TestHelpers.NewDb();
        var controller = CreateController(db);

        var result = await controller.Login(new LoginDto { Email = "nobody@example.com", Password = "Password123!" });

        Assert.IsType<UnauthorizedObjectResult>(result.Result);
    }

    [Fact]
    public void Logout_ClearsTheSession()
    {
        using var db = TestHelpers.NewDb();
        var controller = new AuthController(db, Options.Create(new RegistrationOptions())).WithSession(5, Roles.Participant);

        var result = controller.Logout();

        Assert.IsType<NoContentResult>(result);
        Assert.Null(controller.HttpContext.Session.GetInt32(SessionKeys.UserId));
        Assert.Null(controller.HttpContext.Session.GetString(SessionKeys.Role));
    }
}
