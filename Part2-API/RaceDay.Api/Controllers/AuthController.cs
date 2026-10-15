using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using RaceDay.Api.Data;
using RaceDay.Api.Dtos;
using RaceDay.Api.Models;
using RaceDay.Api.Security;

namespace RaceDay.Api.Controllers;

/// <summary>Registration, login and logout for both roles.</summary>
[ApiController]
[Route("api/auth")]
[Produces("application/json")]
public class AuthController : ApiControllerBase
{
    private readonly RaceDayDbContext _db;
    private readonly RegistrationOptions _options;

    public AuthController(RaceDayDbContext db, IOptions<RegistrationOptions> options)
    {
        _db = db;
        _options = options.Value;
    }

    /// <summary>Registers a new Organiser account.</summary>
    /// <remarks>
    /// Organiser accounts can create and manage events, so registration requires the secret
    /// <c>organiserCode</c>. The password is stored as a BCrypt hash.
    /// Body: { fullName, email, password, organiserCode }.
    /// </remarks>
    /// <response code="201">Organiser created. Returns the new user (never the password).</response>
    /// <response code="400">Validation error (missing field, invalid email, password shorter than 8 characters).</response>
    /// <response code="403">The organiser registration code is wrong.</response>
    /// <response code="409">An account with this email already exists.</response>
    [HttpPost("register/organiser")]
    [ProducesResponseType(typeof(UserDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<UserDto>> RegisterOrganiser([FromBody] RegisterOrganiserDto dto)
    {
        if (string.IsNullOrWhiteSpace(_options.OrganiserCode) || dto.OrganiserCode != _options.OrganiserCode)
        {
            return Forbidden("Invalid organiser registration code.");
        }

        var email = NormaliseEmail(dto.Email);
        if (await EmailInUseAsync(email))
        {
            return Conflict(new ApiError("An account with this email already exists."));
        }

        var organiser = new Organiser
        {
            FullName = dto.FullName.Trim(),
            Email = email,
            PasswordHash = PasswordService.Hash(dto.Password)
        };
        _db.Organisers.Add(organiser);
        await _db.SaveChangesAsync();

        return Created($"/api/organisers/{organiser.OrganiserId}", UserDto.From(organiser));
    }

    /// <summary>Registers a new Participant account.</summary>
    /// <remarks>Body: { fullName, email, password }. The password is stored as a BCrypt hash.</remarks>
    /// <response code="201">Participant created. Returns the new user (never the password).</response>
    /// <response code="400">Validation error (missing field, invalid email, password shorter than 8 characters).</response>
    /// <response code="409">An account with this email already exists.</response>
    [HttpPost("register/participant")]
    [ProducesResponseType(typeof(UserDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<UserDto>> RegisterParticipant([FromBody] RegisterDto dto)
    {
        var email = NormaliseEmail(dto.Email);
        if (await EmailInUseAsync(email))
        {
            return Conflict(new ApiError("An account with this email already exists."));
        }

        var participant = new Participant
        {
            FullName = dto.FullName.Trim(),
            Email = email,
            PasswordHash = PasswordService.Hash(dto.Password)
        };
        _db.Participants.Add(participant);
        await _db.SaveChangesAsync();

        return Created($"/api/participants/{participant.ParticipantId}", UserDto.From(participant));
    }

    /// <summary>Logs in an Organiser or Participant and starts a server-side session.</summary>
    /// <remarks>
    /// On success the server stores the user's ID and role in the session and sends back a session cookie.
    /// Every later request is authorised from that session. Body: { email, password }.
    /// </remarks>
    /// <response code="200">Logged in. Returns { id, fullName, email, role }.</response>
    /// <response code="401">Email or password is incorrect.</response>
    [HttpPost("login")]
    [ProducesResponseType(typeof(UserDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<UserDto>> Login([FromBody] LoginDto dto)
    {
        var email = NormaliseEmail(dto.Email);

        var organiser = await _db.Organisers.FirstOrDefaultAsync(o => o.Email == email);
        if (organiser != null && PasswordService.Verify(dto.Password, organiser.PasswordHash))
        {
            StartSession(organiser.OrganiserId, Roles.Organiser);
            return Ok(UserDto.From(organiser));
        }

        var participant = await _db.Participants.FirstOrDefaultAsync(p => p.Email == email);
        if (participant != null && PasswordService.Verify(dto.Password, participant.PasswordHash))
        {
            StartSession(participant.ParticipantId, Roles.Participant);
            return Ok(UserDto.From(participant));
        }

        return Unauthorized(new ApiError("Invalid email or password."));
    }

    /// <summary>Logs out the current user by clearing the server-side session.</summary>
    /// <response code="204">Session ended.</response>
    /// <response code="401">Nobody is logged in.</response>
    [HttpPost("logout")]
    [SessionAuthorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status401Unauthorized)]
    public IActionResult Logout()
    {
        HttpContext.Session.Clear();
        return NoContent();
    }

    private void StartSession(int userId, string role)
    {
        HttpContext.Session.Clear(); // fresh session on every login
        HttpContext.Session.SetInt32(SessionKeys.UserId, userId);
        HttpContext.Session.SetString(SessionKeys.Role, role);
    }

    private static string NormaliseEmail(string email) => email.Trim().ToLowerInvariant();

    // One email = one account across BOTH tables, so login can tell which role the user has.
    private async Task<bool> EmailInUseAsync(string email) =>
        await _db.Organisers.AnyAsync(o => o.Email == email) ||
        await _db.Participants.AnyAsync(p => p.Email == email);
}
