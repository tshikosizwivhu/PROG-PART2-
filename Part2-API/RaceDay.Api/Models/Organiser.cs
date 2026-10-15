namespace RaceDay.Api.Models;

public class Organiser
{
    public int OrganiserId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public DateTime DateRegistered { get; set; } = DateTime.Now;

    public ICollection<Event> Events { get; set; } = new List<Event>();
}
