namespace RaceDay.Api.Models;

public class Participant
{
    public int ParticipantId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public DateTime DateRegistered { get; set; } = DateTime.Now;

    public ICollection<Enrolment> Enrolments { get; set; } = new List<Enrolment>();
}
