namespace Rok.Domain.Entities;

public abstract class BaseEntity : IEntity
{
    [Dapper.Contrib.Extensions.Key]
    public long Id { get; set; }

    public DateTime CreatDate { get; set; }

    public DateTime? EditDate { get; set; }
}
