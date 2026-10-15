namespace RaceDay.Api.Security;

public static class Roles
{
    public const string Organiser = "Organiser";
    public const string Participant = "Participant";
}

public static class SessionKeys
{
    public const string UserId = "UserId";
    public const string Role = "Role";
}

/// <summary>Bound from appsettings "Registration" section.</summary>
public class RegistrationOptions
{
    /// <summary>
    /// Secret code required to register as an Organiser, so that nobody can simply
    /// "pick" the Organiser role. Participants register freely.
    /// </summary>
    public string OrganiserCode { get; set; } = string.Empty;
}
