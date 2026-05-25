using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MiniExcelLibs;

namespace PlustekBCR.Helpers
{
    public static class CsvHelper
    {
        /// <summary>
        /// Reads column headers from an Excel or CSV file.
        /// </summary>
        public static List<string> ReadHeaders(string filePath)
        {
            try
            {
                // Query returns a collection of rows represented as IDictionary<string, object> when headers are read
                var firstRow = MiniExcel.Query(filePath, useHeaderRow: true).FirstOrDefault() as IDictionary<string, object>;
                if (firstRow != null)
                {
                    return firstRow.Keys.ToList();
                }
                
                // Fallback: Query without headers and take the first row keys or values
                var fallbackRow = MiniExcel.Query(filePath, useHeaderRow: false).FirstOrDefault() as IDictionary<string, object>;
                if (fallbackRow != null)
                {
                    return fallbackRow.Values.Select(v => v?.ToString() ?? "Column").ToList();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error reading headers: {ex.Message}");
            }
            return new List<string>();
        }

        /// <summary>
        /// Reads the first N rows of an Excel or CSV file for previewing.
        /// </summary>
        public static List<Dictionary<string, string>> ReadPreviewRows(string filePath, int maxRows = 3)
        {
            var previewList = new List<Dictionary<string, string>>();
            try
            {
                var rows = MiniExcel.Query(filePath, useHeaderRow: true).Take(maxRows);
                foreach (var rowObj in rows)
                {
                    if (rowObj is IDictionary<string, object> rowDict)
                    {
                        var dict = new Dictionary<string, string>();
                        foreach (var kvp in rowDict)
                        {
                            dict[kvp.Key] = kvp.Value?.ToString() ?? string.Empty;
                        }
                        previewList.Add(dict);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error reading preview rows: {ex.Message}");
            }
            return previewList;
        }

        /// <summary>
        /// Reads all rows from an Excel or CSV file.
        /// </summary>
        public static List<Dictionary<string, string>> ReadAllRows(string filePath)
        {
            var result = new List<Dictionary<string, string>>();
            try
            {
                var rows = MiniExcel.Query(filePath, useHeaderRow: true);
                foreach (var rowObj in rows)
                {
                    if (rowObj is IDictionary<string, object> rowDict)
                    {
                        var dict = new Dictionary<string, string>();
                        foreach (var kvp in rowDict)
                        {
                            dict[kvp.Key] = kvp.Value?.ToString() ?? string.Empty;
                        }
                        result.Add(dict);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error reading all rows: {ex.Message}");
            }
            return result;
        }
    }
}
