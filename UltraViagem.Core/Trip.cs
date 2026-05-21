namespace UltraViagem.Core;

public sealed class Trip
{
    public int SchemaVersion { get; set; } = 1;
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    public DateOnly? StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public string BaseCurrency { get; set; } = "BRL";
    public int People { get; set; } = 1;
    public int RateDecimalDigits { get; set; } = 2;
    public string? MyMapsUrl { get; set; }
    public List<ItineraryDay> Itinerary { get; set; } = [];
    public List<TaskItem> Tasks { get; set; } = [];
    public List<PlaceItem> Places { get; set; } = [];
    public List<LinkItem> Links { get; set; } = [];
    public List<ExpenseItem> Expenses { get; set; } = [];
    public List<CurrencyRateItem> CurrencyRates { get; set; } = [];
    public List<AttachmentItem> Attachments { get; set; } = [];
}

public sealed class ItineraryDay
{
    public string Id { get; set; } = "";
    public DateOnly? Date { get; set; }
    public string Title { get; set; } = "";
    public string? Overnight { get; set; }
    public List<ItineraryBlock> Blocks { get; set; } = [];
    public List<string> Optional { get; set; } = [];
}

public sealed class ItineraryBlock
{
    public string Period { get; set; } = "";
    public string Text { get; set; } = "";
}

public sealed class TaskItem
{
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    public string Status { get; set; } = "pending";
    public string? Notes { get; set; }
    public string? RelatedDayId { get; set; }
    public string? RelatedExpenseId { get; set; }
    public string? RelatedPlaceId { get; set; }
    public string? RelatedAttachment { get; set; }
}

public sealed class PlaceItem
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string? City { get; set; }
    public string? Type { get; set; }
    public string? Description { get; set; }
    public string? Url { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
}

public sealed class LinkItem
{
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    public string Url { get; set; } = "";
    public string? Notes { get; set; }
}

public sealed class ExpenseItem
{
    public string Id { get; set; } = "";
    public bool IsActive { get; set; } = true;
    public string Title { get; set; } = "";
    public string? Type { get; set; }
    public string? Company { get; set; }
    public string? Link { get; set; }
    public string? Notes { get; set; }
    public decimal Price { get; set; }
    public decimal Taxes { get; set; }
    public int People { get; set; } = 1;
    public int Quantity { get; set; } = 1;
    public string Currency { get; set; } = "BRL";
    public decimal ExchangeRateToBase { get; set; } = 1m;
    public bool UseFixedRate { get; set; } = false;
    public decimal PaidAmount { get; set; }

    public decimal Subtotal => (Price + Taxes) * People * Quantity;
    public decimal SubtotalBase => IsActive ? Subtotal * ExchangeRateToBase : 0m;
}

public sealed class CurrencyRateItem
{
    public string Currency { get; set; } = "BRL";
    public string Symbol { get; set; } = "";
    public string Name { get; set; } = "";
    public int DecimalDigits { get; set; } = 2;
    public decimal RateToBase { get; set; } = 1m;
    public DateTime? UpdatedAt { get; set; }
}

public sealed class AttachmentItem
{
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    public string File { get; set; } = "";
    public string? Type { get; set; }
    public string? RelatedExpenseId { get; set; }
    public string? RelatedDayId { get; set; }
}
