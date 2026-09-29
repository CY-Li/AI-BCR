using System.Text;
using PlustekBCR.Models;
using PlustekBCR.Services;
using Xunit;

namespace PlustekBCR.Tests;

public sealed class BusinessCardExportServiceTests
{
    [Fact]
    public async Task ExportCsvAsync_PreservesFieldOrderAndEscapesValues()
    {
        var service = new BusinessCardExportService(new StubFieldService());
        var destination = Path.Combine(Path.GetTempPath(), $"bcr-export-{Guid.NewGuid():N}.csv");

        try
        {
            var cards = new[]
            {
                new BusinessCard
                {
                    CompanyName = "Example, Inc.",
                    FullName = "A \"quoted\" name\nSecond line",
                    Status = ProcessingStatus.Done
                }
            };

            var count = await service.ExportCsvAsync(cards, destination);

            Assert.Equal(1, count);
            var bytes = await File.ReadAllBytesAsync(destination);
            Assert.True(bytes.AsSpan().StartsWith(Encoding.UTF8.Preamble));
            var csv = await File.ReadAllTextAsync(destination, Encoding.UTF8);
            var lines = csv.Split(Environment.NewLine, StringSplitOptions.None);
            Assert.Equal("company_name,full_name", lines[0]);
            Assert.StartsWith("\"Example, Inc.\",\"A \"\"quoted\"\" name", lines[1]);
            Assert.Contains("Second line\"", csv);
        }
        finally
        {
            if (File.Exists(destination))
            {
                File.Delete(destination);
            }
        }
    }

    [Fact]
    public async Task ExportCsvAsync_CancellationDoesNotReplaceExistingFile()
    {
        var service = new BusinessCardExportService(new StubFieldService());
        var destination = Path.Combine(Path.GetTempPath(), $"bcr-export-{Guid.NewGuid():N}.csv");
        await File.WriteAllTextAsync(destination, "existing");
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        try
        {
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                service.ExportCsvAsync(
                    new[] { new BusinessCard { Status = ProcessingStatus.Done } },
                    destination,
                    cancellation.Token));

            Assert.Equal("existing", await File.ReadAllTextAsync(destination));
            Assert.Empty(Directory.GetFiles(
                Path.GetDirectoryName(destination)!,
                $".{Path.GetFileName(destination)}.*.tmp"));
        }
        finally
        {
            if (File.Exists(destination))
            {
                File.Delete(destination);
            }
        }
    }

    private sealed class StubFieldService : IBusinessCardFieldService
    {
        private static readonly IReadOnlyList<BusinessCardFieldDefinition> Fields =
            new[]
            {
                new BusinessCardFieldDefinition
                {
                    Key = "company_name",
                    Label = "Company",
                    PropertyName = nameof(BusinessCard.CompanyName)
                },
                new BusinessCardFieldDefinition
                {
                    Key = "full_name",
                    Label = "Full Name",
                    PropertyName = nameof(BusinessCard.FullName)
                }
            };

        public MarketCode CurrentMarket => MarketCode.JP;

        public IReadOnlyList<BusinessCardFieldDefinition> GetFields(BusinessCardSurface surface) => Fields;

        public IReadOnlyList<BusinessCardFieldDefinition> GetDuplicateComparisonFields() => Fields;

        public bool IsVisible(string key, BusinessCardSurface surface) => true;

        public string GetLabel(string key) => key;

        public string[] GetCsvHeaders(BusinessCardSurface surface) => Fields.Select(field => field.Key).ToArray();
    }
}
