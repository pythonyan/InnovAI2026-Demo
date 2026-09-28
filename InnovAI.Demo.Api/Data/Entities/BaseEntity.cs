namespace InnovAI.Demo.Api.Data.Entities;

public abstract class BaseEntity
{
    public virtual Guid Id { get; set; }
    public virtual DateTime LastModified { get; set; }
}
