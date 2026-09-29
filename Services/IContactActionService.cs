using System.Threading.Tasks;

namespace PlustekBCR.Services
{
    public enum ContactActionResult
    {
        Success,
        InvalidValue,
        LaunchFailed
    }

    public interface IContactActionService
    {
        Task<ContactActionResult> OpenEmailAsync(string? email);
        Task<ContactActionResult> OpenPhoneAsync(string? phoneNumber);
        Task<ContactActionResult> OpenWebsiteAsync(string? website);
    }
}
