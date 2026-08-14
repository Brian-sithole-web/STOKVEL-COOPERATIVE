namespace Stokvel.Domain.Entities;

public class Announcement : BaseEntity
{
    public Guid? GroupId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public Guid AuthorUserId { get; set; }
}
