using System.Threading;
using System.Threading.Tasks;

namespace PlustekBCR.Services
{
    public enum DefaultLinkAppState
    {
        Available,
        NotConfigured,
        Unavailable
    }

    public sealed record DefaultLinkAppInfo(
        string Protocol,
        DefaultLinkAppState State,
        string? DisplayName = null);

    public sealed record DefaultLinkAppsSnapshot(
        DefaultLinkAppInfo Phone,
        DefaultLinkAppInfo Email,
        DefaultLinkAppInfo Http,
        DefaultLinkAppInfo Https);

    public interface IDefaultLinkAppService
    {
        Task<DefaultLinkAppsSnapshot> GetCurrentAsync(CancellationToken cancellationToken = default);
        Task<bool> OpenWindowsDefaultAppsSettingsAsync();
    }
}
