using PlustekBCR.Models;
using PlustekBCR.Models.Plustek;

namespace PlustekBCR.Services.Plustek
{
    public interface IPlustekOptionsProvider
    {
        PlustekConsoleOptions GetCurrent();
        PlustekConsoleOptions Get(MarketCode market);
    }
}
