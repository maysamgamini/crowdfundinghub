using CrowdFunding.BuildingBlocks.Application.Security;

namespace CrowdFunding.API.Security;

/// <summary>
/// Adapts the ASP.NET Core HTTP context into the shared <see cref="IRequestContext"/> abstraction
/// (TICKET-039), mirroring how <see cref="HttpContextCurrentUser"/> adapts identity.
/// </summary>
public sealed class HttpRequestContext : IRequestContext
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public HttpRequestContext(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public string? IpAddress => _httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString();

    public string? UserAgent
    {
        get
        {
            var values = _httpContextAccessor.HttpContext?.Request.Headers.UserAgent;
            return values is null or { Count: 0 } ? null : values.ToString();
        }
    }
}
