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

    [Fact]
    public async Task ExportAsync_GoogleCsv_UsesGoogleHeadersAndContactMappings()
    {
        var service = new BusinessCardExportService(new StubFieldService());
        var destination = Path.Combine(Path.GetTempPath(), $"bcr-google-{Guid.NewGuid():N}.csv");

        try
        {
            var card = CreateMappedCard();
            await service.ExportAsync(new[] { card }, destination, ContactExportFormat.GoogleCsv);

            var csv = await File.ReadAllTextAsync(destination, Encoding.UTF8);
            Assert.StartsWith("First Name,Middle Name,Last Name,Name Suffix", csv);
            Assert.Contains("Organization Name,Organization Title,Organization Department", csv);
            Assert.Contains("Example Corp", csv);
            Assert.Contains("E-mail 1 - Type,E-mail 1 - Value", csv);
            Assert.DoesNotContain("Email 1 - Label", csv);
            Assert.Contains("user@example.com", csv);
            Assert.Contains("重要", csv);
        }
        finally
        {
            File.Delete(destination);
        }
    }

    [Fact]
    public async Task ExportAsync_OutlookCsv_UsesUtf8BomAndOutlookMappings()
    {
        var service = new BusinessCardExportService(new StubFieldService());
        var destination = Path.Combine(Path.GetTempPath(), $"bcr-outlook-{Guid.NewGuid():N}.csv");

        try
        {
            await service.ExportAsync(
                new[] { CreateMappedCard() },
                destination,
                ContactExportFormat.OutlookCsv);

            var bytes = await File.ReadAllBytesAsync(destination);
            Assert.True(bytes.AsSpan().StartsWith(Encoding.UTF8.Preamble));
            var csv = await File.ReadAllTextAsync(destination, Encoding.UTF8);
            Assert.StartsWith("First Name,Middle Name,Last Name,Suffix,Company,Department", csv);
            Assert.Contains("Business Phone,Mobile Phone,Business Fax,E-mail Address", csv);
            Assert.Contains("Example Corp", csv);
            Assert.Contains("Taipei", csv);
        }
        finally
        {
            File.Delete(destination);
        }
    }

    [Fact]
    public async Task ExportAsync_VCard_WritesMultipleEscapedUtf8CardsWithoutBom()
    {
        var service = new BusinessCardExportService(new StubFieldService());
        var destination = Path.Combine(Path.GetTempPath(), $"bcr-vcard-{Guid.NewGuid():N}.vcf");

        try
        {
            var first = CreateMappedCard();
            first.FullName = "王,小明";
            first.Notes = new List<Note> { new() { Content = "第一行\n第二行" } };
            var second = new BusinessCard { FullName = "Jane Doe", FirstName = "Jane", LastName = "Doe" };

            await service.ExportAsync(new[] { first, second }, destination, ContactExportFormat.VCard);

            var bytes = await File.ReadAllBytesAsync(destination);
            Assert.False(bytes.AsSpan().StartsWith(Encoding.UTF8.Preamble));
            var vcard = await File.ReadAllTextAsync(destination, Encoding.UTF8);
            Assert.Equal(2, vcard.Split("BEGIN:VCARD", StringSplitOptions.None).Length - 1);
            Assert.Contains("VERSION:3.0\r\n", vcard);
            Assert.Contains("FN:王\\,小明", vcard);
            Assert.Contains("EMAIL;TYPE=INTERNET,WORK:user@example.com", vcard);
            Assert.Contains("NOTE:Phonetic name: オウ ショウメイ\\n第一行\\n第二行", vcard);
            Assert.Contains("CATEGORIES:重要", vcard);
            Assert.EndsWith("END:VCARD\r\n", vcard);
        }
        finally
        {
            File.Delete(destination);
        }
    }

    [Fact]
    public async Task ExportAsync_PlainText_WritesEverySelectedCard()
    {
        var service = new BusinessCardExportService(new StubFieldService());
        var destination = Path.Combine(Path.GetTempPath(), $"bcr-text-{Guid.NewGuid():N}.txt");

        try
        {
            var first = CreateMappedCard();
            var second = new BusinessCard
            {
                FullName = "Jane Doe",
                CompanyName = "Second Corp",
                Email = "jane@example.com",
                Status = ProcessingStatus.Manual
            };

            var count = await service.ExportAsync(
                new[] { first, second },
                destination,
                ContactExportFormat.PlainText);

            Assert.Equal(2, count);
            var text = await File.ReadAllTextAsync(destination, Encoding.UTF8);
            Assert.Contains("Full Name: 王 小明", text);
            Assert.Contains("Email: user@example.com", text);
            Assert.Contains("Full Name: Jane Doe", text);
            Assert.Contains("Company: Second Corp", text);
        }
        finally
        {
            File.Delete(destination);
        }
    }

    private static BusinessCard CreateMappedCard() => new()
    {
        FirstName = "小明",
        LastName = "王",
        FullName = "王 小明",
        FullNameKana = "オウ ショウメイ",
        CompanyName = "Example Corp",
        Department1 = "Sales",
        DepartmentFull = "Sales",
        JobTitle = "Manager",
        Tel = "02-1234-5678",
        Mobile = "0912-345-678",
        Fax = "02-8765-4321",
        Email = "user@example.com",
        AddressLine1 = "1 Main Street",
        City = "Taipei",
        State = "Taiwan",
        ZipCode = "100",
        Country = "Taiwan",
        Website = "https://example.com",
        Tag = "重要",
        Status = ProcessingStatus.Done
    };

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
