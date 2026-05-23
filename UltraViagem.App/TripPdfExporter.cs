using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System.Globalization;
using UltraViagem.Core;

namespace UltraViagem.App;

public static class TripPdfExporter
{
    private static readonly CultureInfo Ptbr = new("pt-BR");

    private const string Accent       = "#0F766E";
    private const string AccentLight  = "#CCFBF1";
    private const string TextDefault  = "#111827";
    private const string TextMuted    = "#6B7280";
    private const string RowAlt       = "#F9FAFB";
    private const string BorderColor  = "#E5E7EB";

    public static void Export(Trip trip, string outputPath)
    {
        QuestPDF.Settings.License = LicenseType.Community;

        var version = trip.ItineraryVersions.FirstOrDefault(v => v.Id == trip.ActiveVersionId)
                   ?? trip.ItineraryVersions.FirstOrDefault();

        Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.MarginHorizontal(2.2f, Unit.Centimetre);
                page.MarginVertical(2f, Unit.Centimetre);
                page.DefaultTextStyle(ts => ts.FontFamily("Calibri").FontSize(10).FontColor(TextDefault));

                // ── Header ──────────────────────────────────────────────
                page.Header().Column(col =>
                {
                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().Text(trip.Title).FontSize(22).SemiBold().FontColor(Accent);
                            var subtitle = BuildSubtitle(trip);
                            if (!string.IsNullOrEmpty(subtitle))
                                c.Item().PaddingTop(2).Text(subtitle).FontSize(10).FontColor(TextMuted);
                        });
                        row.ConstantItem(80).AlignRight().AlignBottom()
                           .Text("UltraViagem").FontSize(9).Italic().FontColor(TextMuted);
                    });
                    col.Item().PaddingTop(8).LineHorizontal(1).LineColor(Accent);
                });

                // ── Content ─────────────────────────────────────────────
                page.Content().PaddingTop(14).Column(content =>
                {
                    // Roteiro
                    if (version?.Itinerary.Count > 0)
                    {
                        content.Item().Element(c => SectionHeader(c, "Roteiro"));
                        content.Item().PaddingTop(6).Column(col =>
                        {
                            foreach (var day in version.Itinerary)
                            {
                                col.Item().PaddingBottom(8).Element(c => DayBlock(c, day));
                            }
                        });
                    }

                    // Tarefas
                    if (trip.Tasks.Count > 0)
                    {
                        content.Item().PaddingTop(14).Element(c => SectionHeader(c, "Tarefas"));
                        content.Item().PaddingTop(6).Column(col =>
                        {
                            foreach (var task in trip.Tasks.OrderBy(t => t.Status == "done" ? 1 : 0))
                                col.Item().PaddingBottom(3).Element(c => TaskRow(c, task));
                        });
                    }

                    // Gastos
                    if (trip.Expenses.Count > 0)
                    {
                        content.Item().PaddingTop(14).Element(c => SectionHeader(c, "Gastos"));
                        content.Item().PaddingTop(6).Element(c => BudgetTable(c, trip));
                    }

                    // Dicas
                    if (trip.Links.Count > 0)
                    {
                        content.Item().PaddingTop(14).Element(c => SectionHeader(c, "Dicas"));
                        content.Item().PaddingTop(6).Column(col =>
                        {
                            foreach (var link in trip.Links)
                                col.Item().PaddingBottom(6).Element(c => TipRow(c, link));
                        });
                    }
                });

                // ── Footer ───────────────────────────────────────────────
                page.Footer().AlignCenter().Text(text =>
                {
                    text.Span("Página ").FontSize(8).FontColor(TextMuted);
                    text.CurrentPageNumber().FontSize(8).FontColor(TextMuted);
                    text.Span(" de ").FontSize(8).FontColor(TextMuted);
                    text.TotalPages().FontSize(8).FontColor(TextMuted);
                });
            });
        }).GeneratePdf(outputPath);
    }

    // ── Helpers ─────────────────────────────────────────────────────────

    private static string BuildSubtitle(Trip trip)
    {
        var parts = new List<string>();
        if (trip.StartDate.HasValue && trip.EndDate.HasValue)
        {
            var s = trip.StartDate.Value.ToString("dd MMM. yyyy", Ptbr);
            var e = trip.EndDate.Value.ToString("dd MMM. yyyy", Ptbr);
            var days = trip.EndDate.Value.DayNumber - trip.StartDate.Value.DayNumber + 1;
            parts.Add($"{s} – {e} ({days} dias)");
        }
        else if (trip.StartDate.HasValue)
            parts.Add(trip.StartDate.Value.ToString("dd MMM. yyyy", Ptbr));

        if (trip.People > 1) parts.Add($"{trip.People} pessoas");
        return string.Join("  ·  ", parts);
    }

    private static void SectionHeader(IContainer c, string title) =>
        c.Background(Accent).Padding(5)
         .Text(title.ToUpperInvariant()).FontSize(9).SemiBold().FontColor(Colors.White);

    private static void DayBlock(IContainer container, ItineraryDay day)
    {
        container.Column(col =>
        {
            // Cabeçalho do dia
            col.Item().Row(row =>
            {
                row.ConstantItem(32).Background(AccentLight).AlignCenter().AlignMiddle()
                   .Text(day.Title.Replace("Dia ", "")).FontSize(9).SemiBold().FontColor(Accent);

                row.RelativeItem().PaddingLeft(8).AlignMiddle().Column(c =>
                {
                    var headerParts = new List<string>();
                    if (day.Date.HasValue)
                        headerParts.Add(day.Date.Value.ToString("ddd dd/MM", Ptbr));
                    if (!string.IsNullOrWhiteSpace(day.Summary))
                        headerParts.Add(day.Summary);
                    c.Item().Text(string.Join("  —  ", headerParts)).SemiBold().FontSize(10);
                });
            });

            // Atividades
            if (day.Activities.Count > 0)
            {
                col.Item().PaddingLeft(40).PaddingTop(3).Column(actCol =>
                {
                    foreach (var act in day.Activities.OrderBy(a => a.StartSlot))
                    {
                        actCol.Item().PaddingBottom(2).Row(row =>
                        {
                            row.ConstantItem(70).Text(act.Type).FontSize(8).FontColor(TextMuted);
                            row.RelativeItem().Text(act.Title).FontSize(9);
                        });
                    }
                });
            }
        });
    }

    private static void TaskRow(IContainer container, TaskItem task)
    {
        var done = task.Status == "done";
        container.Row(row =>
        {
            row.ConstantItem(16).Text(done ? "☑" : "☐").FontSize(10)
               .FontColor(done ? Accent : "#9CA3AF");
            row.RelativeItem().PaddingLeft(6).Column(col =>
            {
                col.Item().Text(task.Title).FontSize(10)
                   .FontColor(done ? TextMuted : TextDefault);
                if (!string.IsNullOrWhiteSpace(task.Notes))
                    col.Item().Text(task.Notes).FontSize(8).FontColor(TextMuted);
            });
        });
    }

    private static void BudgetTable(IContainer container, Trip trip)
    {
        var total = trip.Expenses.Where(e => e.IsActive).Sum(e => e.SubtotalBase);

        container.Table(table =>
        {
            table.ColumnsDefinition(cols =>
            {
                cols.RelativeColumn(4); // Título
                cols.RelativeColumn(2); // Fornecedor
                cols.RelativeColumn(2); // Total
            });

            table.Header(h =>
            {
                foreach (var label in new[] { "Item", "Fornecedor", "Total" })
                {
                    var cell = h.Cell().Background(AccentLight).Padding(4);
                    if (label == "Total")
                        cell.AlignRight().Text(label).FontSize(8).SemiBold().FontColor(Accent);
                    else
                        cell.Text(label).FontSize(8).SemiBold().FontColor(Accent);
                }
            });

            for (var i = 0; i < trip.Expenses.Count; i++)
            {
                var exp = trip.Expenses[i];
                var bg = i % 2 == 0 ? "#FFFFFF" : RowAlt;
                var color = exp.IsActive ? TextDefault : TextMuted;

                table.Cell().Background(bg).Padding(4).Text(exp.Title).FontSize(9).FontColor(color);
                table.Cell().Background(bg).Padding(4).Text(exp.Company ?? "").FontSize(9).FontColor(TextMuted);
                table.Cell().Background(bg).Padding(4).AlignRight()
                     .Text(exp.IsActive ? Fmt(exp.SubtotalBase, trip.BaseCurrency) : "—")
                     .FontSize(9).FontColor(color);
            }

            // Linha de total
            table.Cell().ColumnSpan(2).BorderTop(1).BorderColor(BorderColor).Padding(4)
                 .Text("Total estimado").FontSize(9).SemiBold();
            table.Cell().BorderTop(1).BorderColor(BorderColor).Padding(4).AlignRight()
                 .Text(Fmt(total, trip.BaseCurrency)).FontSize(9).SemiBold().FontColor(Accent);
        });
    }

    private static void TipRow(IContainer container, LinkItem link)
    {
        container.Column(col =>
        {
            col.Item().Text(link.Title).FontSize(10).SemiBold();
            if (!string.IsNullOrWhiteSpace(link.Url))
                col.Item().Text(link.Url).FontSize(8).FontColor("#2563EB");
        });
    }

    private static string Fmt(decimal value, string currency) =>
        currency == "BRL"
            ? value.ToString("C2", Ptbr)
            : $"{currency} {value:N2}";
}
