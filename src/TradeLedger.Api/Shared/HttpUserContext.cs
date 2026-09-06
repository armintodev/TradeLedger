using System.Security.Claims;
using TradeLedger.Core.Shared;

namespace TradeLedger.Api.Shared;

public sealed class HttpUserContext(IHttpContextAccessor accessor) : IUserContext
{
    public Guid? UserId
    {
        get
        {
            var raw = accessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier);
            return Guid.TryParse(raw, out var id) ? id : null;
        }
    }
}
