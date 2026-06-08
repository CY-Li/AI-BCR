using System.Collections.Generic;

namespace PlustekBCR.Models
{
    public enum BusinessCardSurface
    {
        Edit,
        Detail,
        Import,
        Export
    }

    public class BusinessCardFieldDefinition
    {
        public required string Key { get; init; }
        public required string Label { get; init; }
        public required string PropertyName { get; init; }
        public HashSet<MarketCode> EditMarkets { get; init; } = new();
        public HashSet<MarketCode> DetailMarkets { get; init; } = new();
        public HashSet<MarketCode> ImportMarkets { get; init; } = new();
        public HashSet<MarketCode> ExportMarkets { get; init; } = new();
    }
}
