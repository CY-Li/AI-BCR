using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using PlustekBCR.Helpers;

namespace PlustekBCR.Services
{
    public class TagCatalogService : ITagCatalogService
    {
        private readonly List<string> _tags = new();
        private readonly string _settingsPath;

        public event Action? TagsChanged;

        public TagCatalogService()
        {
            _settingsPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
            LoadFromSettings();
        }

        public IReadOnlyList<string> GetAllTags()
        {
            return _tags
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public bool AddTag(string? tag)
        {
            var normalized = TagTextHelper.Normalize(tag);
            if (string.IsNullOrEmpty(normalized))
            {
                return false;
            }

            if (!TagTextHelper.AddIfMissing(_tags, normalized))
            {
                return false;
            }

            TagsChanged?.Invoke();
            return true;
        }

        public bool RemoveTag(string? tag)
        {
            var normalized = TagTextHelper.Normalize(tag);
            if (string.IsNullOrEmpty(normalized))
            {
                return false;
            }

            if (!TagTextHelper.RemoveFirstIgnoreCase(_tags, normalized))
            {
                return false;
            }
            TagsChanged?.Invoke();
            return true;
        }

        public async Task SaveAsync()
        {
            try
            {
                JsonObject root;
                if (File.Exists(_settingsPath))
                {
                    var text = await File.ReadAllTextAsync(_settingsPath);
                    root = JsonNode.Parse(text) as JsonObject ?? new JsonObject();
                }
                else
                {
                    root = new JsonObject();
                }

                var arr = new JsonArray();
                foreach (var item in GetAllTags())
                {
                    arr.Add(item);
                }

                root["TagOptions"] = arr;
                var json = root.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(_settingsPath, json);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Save tag catalog failed: {ex.Message}");
            }
        }

        private void LoadFromSettings()
        {
            try
            {
                if (!File.Exists(_settingsPath))
                {
                    return;
                }

                var text = File.ReadAllText(_settingsPath);
                var root = JsonNode.Parse(text) as JsonObject;
                var arr = root?["TagOptions"] as JsonArray;
                if (arr == null)
                {
                    return;
                }

                foreach (var node in arr)
                {
                    if (node == null)
                    {
                        continue;
                    }

                    TagTextHelper.AddIfMissing(_tags, node.ToString());
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Load tag catalog failed: {ex.Message}");
            }
        }
    }
}
