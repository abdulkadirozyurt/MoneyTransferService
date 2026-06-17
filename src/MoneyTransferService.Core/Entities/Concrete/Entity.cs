namespace MoneyTransferService.Core.Entities.Concrete;

public abstract class Entity
{
    public Entity()
    {
        Id = Guid.CreateVersion7();
        CreatedAt = DateTime.UtcNow;
    }

    public Guid Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public DateTime? DeletedAt { get; set; }
    public bool IsDeleted { get; set; } = false;

}
