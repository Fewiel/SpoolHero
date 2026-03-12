using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using SpoolManager.Infrastructure.Repositories;

namespace SpoolManager.Server.Filters;

public class ProjectAuthFilter : IAsyncActionFilter
{
    private readonly IProjectMemberRepository _members;

    public ProjectAuthFilter(IProjectMemberRepository members) => _members = members;

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (!context.HttpContext.Request.Headers.TryGetValue("X-Project-Id", out var headerValue) ||
            !Guid.TryParse(headerValue, out var projectId))
        {
            context.Result = new ObjectResult(new { message = "X-Project-Id header is required." }) { StatusCode = 400 };
            return;
        }

        var idClaim = context.HttpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(idClaim, out var userId))
        {
            context.Result = new UnauthorizedResult();
            return;
        }

        var member = await _members.GetAsync(projectId, userId);
        if (member == null)
        {
            context.Result = new ObjectResult(new { message = "Not a member of this project." }) { StatusCode = 403 };
            return;
        }

        context.HttpContext.Items["ProjectMember"] = member;
        await next();
    }
}
