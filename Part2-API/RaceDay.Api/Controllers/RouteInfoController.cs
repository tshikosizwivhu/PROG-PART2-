using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Data;
using RaceDay.Api.Dtos;
using RaceDay.Api.Models;
using RaceDay.Api.Security;

namespace RaceDay.Api.Controllers;

/// <summary>Route, elevation, map and weather notes that help participants prepare for race day.</summary>
[ApiController]
[Route("api/events/{eventId:int}/route")]
[Produces("application/json")]
public class RouteInfoController : ApiControllerBase
{
    private readonly RaceDayDbContext _db;

    public RouteInfoController(RaceDayDbContext db)
    {
        _db = db;
    }

    /// <summary>Returns the route information of an event.</summary>
    /// <response code="200">The route information.</response>
    /// <response code="404">Event or route not found.</response>
    [HttpGet]
    [ProducesResponseType(typeof(RouteDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RouteDto>> GetRoute(int eventId)
    {
        var route = await _db.Routes.FirstOrDefaultAsync(r => r.EventId == eventId);
        if (route == null) return NotFound(new ApiError("No route information found for this event."));
        return Ok(RouteDto.From(route));
    }

    /// <summary>Creates or replaces the route information of an event. Only the owning Organiser may do this.</summary>
    /// <remarks>Body: { routeDescription, elevationGainM, mapUrl, weatherNotes }.</remarks>
    /// <response code="200">The saved route information.</response>
    /// <response code="400">Validation error.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">Participant, or an Organiser who does not own the event.</response>
    /// <response code="404">Event not found.</response>
    [HttpPut]
    [SessionAuthorize(Roles.Organiser)]
    [ProducesResponseType(typeof(RouteDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RouteDto>> SaveRoute(int eventId, [FromBody] RouteUpsertDto dto)
    {
        var ev = await _db.Events.FindAsync(eventId);
        if (ev == null) return NotFound(new ApiError("Event not found."));
        if (ev.OrganiserId != CurrentUserId) return Forbidden("You can only change the route of your own events.");

        var route = await _db.Routes.FirstOrDefaultAsync(r => r.EventId == eventId);
        if (route == null)
        {
            route = new EventRoute { EventId = eventId };
            _db.Routes.Add(route);
        }

        route.RouteDescription = dto.RouteDescription;
        route.ElevationGainM = dto.ElevationGainM;
        route.MapUrl = dto.MapUrl;
        route.WeatherNotes = dto.WeatherNotes;
        await _db.SaveChangesAsync();

        return Ok(RouteDto.From(route));
    }
}
