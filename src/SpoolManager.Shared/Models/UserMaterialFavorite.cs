namespace SpoolManager.Shared.Models;

public class UserMaterialFavorite
{
    public Guid UserId { get; set; }
    public Guid MaterialId { get; set; }
    public DateTime CreatedAt { get; set; }
}
