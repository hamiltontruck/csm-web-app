namespace CSM.Web.Models.Users;

public class UserAuditModel
{
    public Guid Id { get; set; }

    public DateTime PerformedAtUtc { get; set; }

    public string ActorName { get; set; } = "";

    public string Action { get; set; } = "";

    public string TargetName { get; set; } = "";

    public string Details { get; set; } = "";
}