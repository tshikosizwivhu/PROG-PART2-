using System.ComponentModel.DataAnnotations;
using RaceDay.Api.Models;
using RaceDay.Api.Security;

namespace RaceDay.Api.Dtos;

/// <summary>Standard error body returned for every failure response.</summary>
public class ApiError
{
    public ApiError(string message) { Message = message; }
    public string Message { get; set; }
}

// ---------------- Authentication / profile ----------------

public class RegisterDto
{
    [Required, StringLength(100, MinimumLength = 2)]
    public string FullName { get; set; } = string.Empty;

    [Required, EmailAddress, StringLength(150)]
    public string Email { get; set; } = string.Empty;

    [Required, StringLength(100, MinimumLength = 8)]
    public string Password { get; set; } = string.Empty;
}

public class RegisterOrganiserDto : RegisterDto
{
    /// <summary>Secret organiser registration code (configured in appsettings).</summary>
    [Required]
    public string OrganiserCode { get; set; } = string.Empty;
}

public class LoginDto
{
    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string Password { get; set; } = string.Empty;
}

public class ProfileUpdateDto
{
    [Required, StringLength(100, MinimumLength = 2)]
    public string FullName { get; set; } = string.Empty;

    [Required, EmailAddress, StringLength(150)]
    public string Email { get; set; } = string.Empty;
}

public class UserDto
{
    public int Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;

    public static UserDto From(Organiser o) =>
        new() { Id = o.OrganiserId, FullName = o.FullName, Email = o.Email, Role = Roles.Organiser };

    public static UserDto From(Participant p) =>
        new() { Id = p.ParticipantId, FullName = p.FullName, Email = p.Email, Role = Roles.Participant };
}

// ---------------- Events ----------------

public class EventUpsertDto
{
    [Required, StringLength(150)]
    public string EventName { get; set; } = string.Empty;

    [Required]
    public DateTime? EventDate { get; set; }

    [Required, StringLength(150)]
    public string Location { get; set; } = string.Empty;

    [StringLength(4000)]
    public string? Description { get; set; }

    /// <summary>Run, Walk or Cycle.</summary>
    [Required]
    public string EventType { get; set; } = string.Empty;

    /// <summary>Headline distance of the event in kilometres.</summary>
    [Range(0.1, 1000)]
    public decimal DistanceKm { get; set; }
}

public class EventDto
{
    public int EventId { get; set; }
    public int OrganiserId { get; set; }
    public string EventName { get; set; } = string.Empty;
    public DateTime EventDate { get; set; }
    public string Location { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string EventType { get; set; } = string.Empty;
    public decimal DistanceKm { get; set; }

    public static EventDto From(Event e) => new()
    {
        EventId = e.EventId,
        OrganiserId = e.OrganiserId,
        EventName = e.EventName,
        EventDate = e.EventDate,
        Location = e.Location,
        Description = e.Description,
        EventType = e.EventType,
        DistanceKm = e.DistanceKm
    };
}

public class EventDetailDto : EventDto
{
    public List<CategoryDto> Categories { get; set; } = new();
    public RouteDto? Route { get; set; }

    public static EventDetailDto FromDetail(Event e) => new()
    {
        EventId = e.EventId,
        OrganiserId = e.OrganiserId,
        EventName = e.EventName,
        EventDate = e.EventDate,
        Location = e.Location,
        Description = e.Description,
        EventType = e.EventType,
        DistanceKm = e.DistanceKm,
        Categories = e.Categories.OrderBy(c => c.DistanceKm).Select(CategoryDto.From).ToList(),
        Route = e.Route == null ? null : RouteDto.From(e.Route)
    };
}

// ---------------- Categories ----------------

public class CategoryUpsertDto
{
    [Required, StringLength(50)]
    public string CategoryName { get; set; } = string.Empty;

    [Range(0.1, 1000)]
    public decimal DistanceKm { get; set; }

    [Range(0, 100000)]
    public decimal EntryFee { get; set; }

    [Range(1, 100000)]
    public int MaxParticipants { get; set; } = 500;
}

public class CategoryDto
{
    public int CategoryId { get; set; }
    public int EventId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public decimal DistanceKm { get; set; }
    public decimal EntryFee { get; set; }
    public int MaxParticipants { get; set; }

    public static CategoryDto From(Category c) => new()
    {
        CategoryId = c.CategoryId,
        EventId = c.EventId,
        CategoryName = c.CategoryName,
        DistanceKm = c.DistanceKm,
        EntryFee = c.EntryFee,
        MaxParticipants = c.MaxParticipants
    };
}

// ---------------- Route info ----------------

public class RouteUpsertDto
{
    [StringLength(4000)]
    public string? RouteDescription { get; set; }

    [Range(0, 10000)]
    public int? ElevationGainM { get; set; }

    [StringLength(255)]
    public string? MapUrl { get; set; }

    [StringLength(255)]
    public string? WeatherNotes { get; set; }
}

public class RouteDto
{
    public int RouteId { get; set; }
    public int EventId { get; set; }
    public string? RouteDescription { get; set; }
    public int? ElevationGainM { get; set; }
    public string? MapUrl { get; set; }
    public string? WeatherNotes { get; set; }

    public static RouteDto From(EventRoute r) => new()
    {
        RouteId = r.RouteId,
        EventId = r.EventId,
        RouteDescription = r.RouteDescription,
        ElevationGainM = r.ElevationGainM,
        MapUrl = r.MapUrl,
        WeatherNotes = r.WeatherNotes
    };
}

// ---------------- Enrolments ----------------

public class EnrolmentDto
{
    public int EnrolmentId { get; set; }
    public int ParticipantId { get; set; }
    public string ParticipantName { get; set; } = string.Empty;
    public int CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public int EventId { get; set; }
    public string EventName { get; set; } = string.Empty;
    public DateTime EnrolmentDate { get; set; }
    public string Status { get; set; } = string.Empty;

    /// <summary>Requires Participant, Category and Category.Event to be loaded.</summary>
    public static EnrolmentDto From(Enrolment e) => new()
    {
        EnrolmentId = e.EnrolmentId,
        ParticipantId = e.ParticipantId,
        ParticipantName = e.Participant.FullName,
        CategoryId = e.CategoryId,
        CategoryName = e.Category.CategoryName,
        EventId = e.Category.EventId,
        EventName = e.Category.Event.EventName,
        EnrolmentDate = e.EnrolmentDate,
        Status = e.Status
    };
}

// ---------------- Results ----------------

public class ResultUpsertDto
{
    /// <summary>Finish time as hh:mm:ss (leave empty for a DNF).</summary>
    public TimeSpan? FinishTime { get; set; }

    [Range(1, 1000000)]
    public int? OverallPosition { get; set; }

    [Range(1, 1000000)]
    public int? CategoryPosition { get; set; }
}

public class ResultDto
{
    public int ResultId { get; set; }
    public int EnrolmentId { get; set; }
    public int ParticipantId { get; set; }
    public string ParticipantName { get; set; } = string.Empty;
    public int EventId { get; set; }
    public string EventName { get; set; } = string.Empty;
    public DateTime EventDate { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public TimeSpan? FinishTime { get; set; }
    public int? OverallPosition { get; set; }
    public int? CategoryPosition { get; set; }

    /// <summary>Requires Enrolment, Enrolment.Participant, Enrolment.Category and Category.Event to be loaded.</summary>
    public static ResultDto From(Result r) => new()
    {
        ResultId = r.ResultId,
        EnrolmentId = r.EnrolmentId,
        ParticipantId = r.Enrolment.ParticipantId,
        ParticipantName = r.Enrolment.Participant.FullName,
        EventId = r.Enrolment.Category.EventId,
        EventName = r.Enrolment.Category.Event.EventName,
        EventDate = r.Enrolment.Category.Event.EventDate,
        CategoryName = r.Enrolment.Category.CategoryName,
        FinishTime = r.FinishTime,
        OverallPosition = r.OverallPosition,
        CategoryPosition = r.CategoryPosition
    };
}
