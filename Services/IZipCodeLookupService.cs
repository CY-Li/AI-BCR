using System.Threading;
using System.Threading.Tasks;
using PlustekBCR.Models;

namespace PlustekBCR.Services
{
    public interface IZipCodeLookupService
    {
        Task<ZipCloudResult?> LookupJapanAddressAsync(string zipCode, CancellationToken cancellationToken = default);
    }
}
