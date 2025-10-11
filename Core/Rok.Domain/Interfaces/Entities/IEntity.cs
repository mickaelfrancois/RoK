namespace Rok.Domain.Interfaces.Entities;

public interface IEntity
{
    long Id { get; set; }

    DateTime CreatDate { get; set; }

    DateTime? EditDate { get; set; }
}
