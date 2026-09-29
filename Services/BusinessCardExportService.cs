using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using PlustekBCR.Helpers;
using PlustekBCR.Models;

namespace PlustekBCR.Services
{
    public sealed class BusinessCardExportService : IBusinessCardExportService
    {
        private readonly IBusinessCardFieldService _fieldService;

        public BusinessCardExportService(IBusinessCardFieldService fieldService)
        {
            _fieldService = fieldService;
        }

        public async Task<int> ExportCsvAsync(
            IReadOnlyList<BusinessCard> cards,
            string destinationPath,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(cards);
            ArgumentException.ThrowIfNullOrWhiteSpace(destinationPath);

            var fields = _fieldService.GetFields(BusinessCardSurface.Export).ToArray();
            var rows = cards
                .Select(card => fields
                    .Select(field => EscapeCsv(BusinessCardFieldAccessor.GetTextValue(card, field.PropertyName)))
                    .ToArray())
                .ToArray();

            var fullPath = Path.GetFullPath(destinationPath);
            var directory = Path.GetDirectoryName(fullPath)
                ?? throw new InvalidOperationException("The export destination has no parent directory.");
            Directory.CreateDirectory(directory);

            var temporaryPath = Path.Combine(
                directory,
                $".{Path.GetFileName(fullPath)}.{Guid.NewGuid():N}.tmp");

            try
            {
                await using (var stream = new FileStream(
                    temporaryPath,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None,
                    4096,
                    FileOptions.Asynchronous | FileOptions.SequentialScan))
                await using (var writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true)))
                {
                    await writer.WriteLineAsync(string.Join(",", fields.Select(field => field.Key)));
                    foreach (var row in rows)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        await writer.WriteLineAsync(string.Join(",", row));
                    }

                    await writer.FlushAsync(cancellationToken);
                }

                cancellationToken.ThrowIfCancellationRequested();
                File.Move(temporaryPath, fullPath, overwrite: true);
                return cards.Count;
            }
            finally
            {
                if (File.Exists(temporaryPath))
                {
                    File.Delete(temporaryPath);
                }
            }
        }

        internal static string EscapeCsv(string? value)
        {
            var text = value ?? string.Empty;
            if (text.Contains('"') || text.Contains(',') || text.Contains('\n') || text.Contains('\r'))
            {
                return $"\"{text.Replace("\"", "\"\"")}\"";
            }

            return text;
        }
    }
}
