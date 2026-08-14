namespace Stokvel.Domain.Entities;

public class Document : BaseEntity
{
    public Guid GroupId { get; set; }
    public Guid? RelatedEntityId { get; set; }
    public string RelatedEntityType { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public string StoragePath { get; set; } = string.Empty;
}
