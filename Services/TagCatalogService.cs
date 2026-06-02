using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

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
            var normalized = Normalize(tag);
            if (string.IsNullOrEmpty(normalized))
            {
                return false;
            }

            if (_tags.Any(x => string.Equals(x, normalized, StringComparison.OrdinalIgnoreCase)))
            {
                return false;
            }

            _tags.Add(normalized);
            TagsChanged?.Invoke();
            return true;
        }

        public bool RemoveTag(string? tag)
        {
            var normalized = Normalize(tag);
            if (string.IsNullOrEmpty(normalized))
            {
                return false;
            }

            var existing = _tags.FirstOrDefault(x => string.Equals(x, normalized, StringComparison.OrdinalIgnoreCase));
            if (existing == null)
            {
                return false;
            }

            _tags.Remove(existing);
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

                    var normalized = Normalize(node.ToString());
                    if (!string.IsNullOrEmpty(normalized) && !_tags.Any(x => string.Equals(x, normalized, StringComparison.OrdinalIgnoreCase)))
                    {
                        _tags.Add(normalized);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Load tag catalog failed: {ex.Message}");
            }
        }

        private static string Normalize(string? value)
        {
            return (value ?? string.Empty).Trim();
        }
    }
}
