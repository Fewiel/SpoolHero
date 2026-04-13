using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SpoolManager.Infrastructure.Repositories;

namespace SpoolManager.Server.Controllers;

[ApiController]
[Route("api/favorites")]
[Authorize]
public class FavoritesController : ControllerBase
{
    private readonly IFavoriteRepository _favorites;

    public FavoritesController(IFavoriteRepository favorites)
    {
        _favorites = favorites;
    }

    private Guid UserId => Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

    [HttpGet]
    public async Task<IActionResult> GetFavorites()
    {
        var ids = await _favorites.GetMaterialIdsByUserAsync(UserId);
        return Ok(ids);
    }

    [HttpPost("{materialId}")]
    public async Task<IActionResult> AddFavorite(Guid materialId)
    {
        await _favorites.AddAsync(UserId, materialId);
        return NoContent();
    }

    [HttpDelete("{materialId}")]
    public async Task<IActionResult> RemoveFavorite(Guid materialId)
    {
        await _favorites.RemoveAsync(UserId, materialId);
        return NoContent();
    }
}
