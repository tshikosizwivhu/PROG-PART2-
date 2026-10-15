using Microsoft.AspNetCore.Mvc;
using RaceDay.Api.Controllers;
using RaceDay.Api.Dtos;
using RaceDay.Api.Security;

namespace RaceDay.Tests;

public class ProfileTests
{
    [Fact]
    public async Task GetParticipant_OwnProfile_ReturnsProfile()
    {
        using var db = TestHelpers.NewDb();
        var participant = await TestHelpers.AddParticipantAsync(db, "Emma Peters");
        var controller = new ProfileController(db).WithSession(participant.ParticipantId, Roles.Participant);

        var result = await controller.GetParticipant(participant.ParticipantId);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal("Emma Peters", Assert.IsType<UserDto>(ok.Value).FullName);
    }

    [Fact]
    public async Task GetParticipant_AnotherUsersProfile_ReturnsForbidden()
    {
        using var db = TestHelpers.NewDb();
        var me = await TestHelpers.AddParticipantAsync(db);
        var other = await TestHelpers.AddParticipantAsync(db);
        var controller = new ProfileController(db).WithSession(me.ParticipantId, Roles.Participant);

        var result = await controller.GetParticipant(other.ParticipantId);

        Assert.Equal(403, Assert.IsType<ObjectResult>(result.Result).StatusCode);
    }

    [Fact]
    public async Task UpdateOrganiser_OwnProfile_SavesChanges()
    {
        using var db = TestHelpers.NewDb();
        var organiser = await TestHelpers.AddOrganiserAsync(db, "Old Name");
        var controller = new ProfileController(db).WithSession(organiser.OrganiserId, Roles.Organiser);

        var result = await controller.UpdateOrganiser(organiser.OrganiserId,
            new ProfileUpdateDto { FullName = "New Name", Email = "New@Example.com" });

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var dto = Assert.IsType<UserDto>(ok.Value);
        Assert.Equal("New Name", dto.FullName);
        Assert.Equal("new@example.com", dto.Email);
    }

    [Fact]
    public async Task UpdateParticipant_EmailUsedByAnotherAccount_ReturnsConflict()
    {
        using var db = TestHelpers.NewDb();
        var me = await TestHelpers.AddParticipantAsync(db);
        await TestHelpers.AddParticipantAsync(db, email: "taken@example.com");
        var controller = new ProfileController(db).WithSession(me.ParticipantId, Roles.Participant);

        var result = await controller.UpdateParticipant(me.ParticipantId,
            new ProfileUpdateDto { FullName = "Me", Email = "taken@example.com" });

        Assert.IsType<ConflictObjectResult>(result.Result);
    }
}
