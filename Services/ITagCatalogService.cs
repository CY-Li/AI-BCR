using System.Collections.Generic;
using System.Threading.Tasks;

namespace PlustekBCR.Services
{
    public interface ITagCatalogService
    {
        IReadOnlyList<string> GetAllTags();
        bool AddTag(string? tag);
        bool RemoveTag(string? tag);
        Task SaveAsync();
        event System.Action? TagsChanged;
    }
}
