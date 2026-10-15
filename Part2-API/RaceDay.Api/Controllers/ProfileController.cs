using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Data;
using RaceDay.Api.Dtos;
using RaceDay.Api.Security;

namespace RaceDay.Api.Controllers;

/// <summary>Each user can view and update their own profile.</summary>
[ApiController]
[Route("api")]
[Produces("application/json")]
public class ProfileController : ApiControllerBase
{
    private readonly RaceDayDbContext _db;

    public ProfileController(RaceDayDbContext db)
    {
        _db = db;
    }

    /// <summary>Returns the logged-in Organiser's own profile.</summary>
    /// <response code="200">The organiser profile.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">Wrong role, or the id is not your own.</response>
    /// <response code="404">Organiser not found.</response>
    [HttpGet("organisers/{id:int}")]
    [SessionAuthorize(Roles.Organiser)]
    [ProducesResponseType(typeof(UserDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UserDto>> GetOrganiser(int id)
    {
        if (CurrentUserId != id) return Forbidden("You can only view your own profile.");

        var organiser = await _db.Organisers.FindAsync(id);
        if (organiser == null) return NotFound(new ApiError("Organiser not found."));

        return Ok(UserDto.From(organiser));
    }

    /// <summary>Updates the logged-in Organiser's own name and email.</summary>
    /// <remarks>Body: { fullName, email }.</remarks>
    /// <response code="200">The updated profile.</response>
    /// <response code="400">Validation error.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">Wrong role, or the id is not your own.</response>
    /// <response code="404">Organiser not found.</response>
    /// <response code="409">Email is used by another account.</response>
    [HttpPut("organisers/{id:int}")]
    [SessionAuthorize(Roles.Organiser)]
    [ProducesResponseType(typeof(UserDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<UserDto>> UpdateOrganiser(int id, [FromBody] ProfileUpdateDto dto)
    {
        if (CurrentUserId != id) return Forbidden("You can only update your own profile.");

        var organiser = await _db.Organisers.FindAsync(id);
        if (organiser == null) return NotFound(new ApiError("Organiser not found."));

        var email = dto.Email.Trim().ToLowerInvariant();
        var taken = await _db.Organisers.AnyAsync(o => o.Email == email && o.OrganiserId != id) ||
                    await _db.Participants.AnyAsync(p => p.Email == email);
        if (taken) return Conflict(new ApiError("This email is already used by another account."));

        organiser.FullName = dto.FullName.Trim();
        organiser.Email = email;
        await _db.SaveChangesAsync();

        return Ok(UserDto.From(organiser));
    }

    /// <summary>Returns the logged-in Participant's own profile.</summary>
    /// <response code="200">The participant profile.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">Wrong role, or the id is not your own.</response>
    /// <response code="404">Participant not found.</response>
    [HttpGet("participants/{id:int}")]
    [SessionAuthorize(Roles.Participant)]
    [ProducesResponseType(typeof(UserDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UserDto>> GetParticipant(int id)
    {
        if (CurrentUserId != id) return Forbidden("You can only view your own profile.");

        var participant = await _db.Participants.FindAsync(id);
        if (participant == null) return NotFound(new ApiError("Participant not found."));

        return Ok(UserDto.From(participant));
    }

    /// <summary>Updates the logged-in Participant's own name and email.</summary>
    /// <remarks>Body: { fullName, email }.</remarks>
    /// <response code="200">The updated profile.</response>
    /// <response code="400">Validation error.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">Wrong role, or the id is not your own.</response>
    /// <response code="404">Participant not found.</response>
    /// <response code="409">Email is used by another account.</response>
    [HttpPut("participants/{id:int}")]
    [SessionAuthorize(Roles.Participant)]
    [ProducesResponseType(typeof(UserDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<UserDto>> UpdateParticipant(int id, [FromBody] ProfileUpdateDto dto)
    {
        if (CurrentUserId != id) return Forbidden("You can only update your own profile.");

        var participant = await _db.Participants.FindAsync(id);
        if (participant == null) return NotFound(new ApiError("Participant not found."));

        var email = dto.Email.Trim().ToLowerInvariant();
        var taken = await _db.Participants.AnyAsync(p => p.Email == email && p.ParticipantId != id) ||
                    await _db.Organisers.AnyAsync(o => o.Email == email);
        if (taken) return Conflict(new ApiError("This email is already used by another account."));

        participant.FullName = dto.FullName.Trim();
        participant.Email = email;
        await _db.SaveChangesAsync();

        return Ok(UserDto.From(participant));
    }
}
