namespace Stokvel.Domain.Entities;

public class AppNotification : BaseEntity
{
    public Guid UserId { get; set; }
    public Guid? GroupId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public NotificationType Type { get; set; } = NotificationType.General;
    public bool IsRead { get; set; }
    public Guid? InvitationId { get; set; }
}
