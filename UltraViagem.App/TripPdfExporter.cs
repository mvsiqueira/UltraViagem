using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SkiaSharp;
using System.Globalization;
using UltraViagem.Core;

namespace UltraViagem.App;

public static class TripPdfExporter
{
    private static readonly CultureInfo Ptbr = new("pt-BR");

    private const string Accent      = "#0F766E";
    private const string AccentLight = "#CCFBF1";
    private const string TextDefault = "#111827";
    private const string TextMuted   = "#6B7280";
    private const string RowAlt      = "#F9FAFB";
    private const string BorderColor = "#E5E7EB";

    public static void Export(Trip trip, string outputPath)
    {
        QuestPDF.Settings.License = LicenseType.Community;

        var activeVersion = trip.ItineraryVersions.FirstOrDefault(v => v.Id == trip.ActiveVersionId)
                         ?? trip.ItineraryVersions.FirstOrDefault();

        Document.Create(container =>
        {
            // ── 1. Roteiro ───────────────────────────────────────────────
            container.Page(page =>
            {
                ConfigurePortraitPage(page, trip, "Roteiro");
                page.Content().PaddingTop(10).Column(col =>
                {
                    if (activeVersion?.Itinerary.Count > 0)
                        foreach (var day in activeVersion.Itinerary)
                            col.Item().PaddingBottom(8).Element(c => DayBlock(c, day));
                    else
                        col.Item().Text("Nenhum dia cadastrado.").FontColor(TextMuted);
                });
            });

            // ── 2. Roteiro Detalhado (landscape, uma página por versão) ──
            foreach (var version in trip.ItineraryVersions)
            {
                container.Page(page =>
                {
                    page.Size(new PageSize(297, 210, Unit.Millimetre)); // A4 landscape
                    page.MarginHorizontal(1.5f, Unit.Centimetre);
                    page.MarginVertical(1.5f, Unit.Centimetre);
                    page.DefaultTextStyle(ts => ts.FontFamily("Calibri").FontSize(10).FontColor(TextDefault));

                    page.Header().Column(col =>
                    {
                        col.Item().Row(row =>
                        {
                            row.RelativeItem().Column(c =>
                            {
                                c.Item().Text(trip.Title).FontSize(15).SemiBold().FontColor(Accent);
                                c.Item().Text($"Roteiro Detalhado — {version.Name}").FontSize(9).FontColor(TextMuted);
                            });
                            row.ConstantItem(80).AlignRight().AlignBottom()
                               .Text("UltraViagem").FontSize(9).Italic().FontColor(TextMuted);
                        });
                        col.Item().PaddingTop(5).LineHorizontal(1).LineColor(Accent);
                    });

                    page.Content().PaddingTop(8).Element(c => ItineraryDiagram(c, version, trip));

                    page.Footer().AlignCenter().Text(text =>
                    {
                        text.Span("Página ").FontSize(8).FontColor(TextMuted);
                        text.CurrentPageNumber().FontSize(8).FontColor(TextMuted);
                        text.Span(" de ").FontSize(8).FontColor(TextMuted);
                        text.TotalPages().FontSize(8).FontColor(TextMuted);
                    });
                });
            }

            // ── 3. Dicas ─────────────────────────────────────────────────
            if (trip.Links.Count > 0)
            {
                container.Page(page =>
                {
                    ConfigurePortraitPage(page, trip, "Dicas");
                    page.Content().PaddingTop(10).Column(col =>
                    {
                        foreach (var link in trip.Links)
                            col.Item().PaddingBottom(8).Element(c => TipRow(c, link));
                    });
                });
            }

            // ── 4. Gastos ─────────────────────────────────────────────────
            if (trip.Expenses.Count > 0)
            {
                container.Page(page =>
                {
                    ConfigurePortraitPage(page, trip, "Gastos");
                    page.Content().PaddingTop(10).Element(c => BudgetTable(c, trip));
                });
            }

            // ── 5. Tarefas ────────────────────────────────────────────────
            if (trip.Tasks.Count > 0)
            {
                container.Page(page =>
                {
                    ConfigurePortraitPage(page, trip, "Tarefas");
                    page.Content().PaddingTop(10).Column(col =>
                    {
                        foreach (var task in trip.Tasks.OrderBy(t => t.Status == "done" ? 1 : 0))
                            col.Item().PaddingBottom(4).Element(c => TaskRow(c, task));
                    });
                });
            }

        }).GeneratePdf(outputPath);
    }

    // ─────────────────────────────────────────────────────────────────────
    // Page setup
    // ─────────────────────────────────────────────────────────────────────

    private static void ConfigurePortraitPage(PageDescriptor page, Trip trip, string sectionTitle)
    {
        page.Size(PageSizes.A4);
        page.MarginHorizontal(2.2f, Unit.Centimetre);
        page.MarginVertical(2f, Unit.Centimetre);
        page.DefaultTextStyle(ts => ts.FontFamily("Calibri").FontSize(10).FontColor(TextDefault));

        page.Header().Column(col =>
        {
            col.Item().Row(row =>
            {
                row.RelativeItem().Column(c =>
                {
                    c.Item().Text(trip.Title).FontSize(18).SemiBold().FontColor(Accent);
                    var sub = BuildSubtitle(trip);
                    if (!string.IsNullOrEmpty(sub))
                        c.Item().PaddingTop(2).Text(sub).FontSize(9).FontColor(TextMuted);
                });
                row.ConstantItem(80).AlignRight().AlignBottom()
                   .Text("UltraViagem").FontSize(9).Italic().FontColor(TextMuted);
            });
            col.Item().PaddingTop(4).Background(Accent).Padding(5)
               .Text(sectionTitle.ToUpperInvariant()).FontSize(10).SemiBold().FontColor(Colors.White);
        });

        page.Footer().AlignCenter().Text(text =>
        {
            text.Span("Página ").FontSize(8).FontColor(TextMuted);
            text.CurrentPageNumber().FontSize(8).FontColor(TextMuted);
            text.Span(" de ").FontSize(8).FontColor(TextMuted);
            text.TotalPages().FontSize(8).FontColor(TextMuted);
        });
    }

    // ─────────────────────────────────────────────────────────────────────
    // Itinerary diagram (SkiaSharp canvas)
    // ─────────────────────────────────────────────────────────────────────

    private static void ItineraryDiagram(IContainer container, ItineraryVersion version, Trip trip)
    {
        var days = version.Itinerary;
        if (days.Count == 0)
        {
            container.AlignCenter().AlignMiddle()
                     .Text("Nenhum dia nesta versão.").FontColor(TextMuted);
            return;
        }

        container.Canvas((canvas, size) =>
        {
            int dayCount = days.Count;
            int slotsPerDay = trip.ItinerarySlotsPerDay;

            float totalW = size.Width;
            float totalH = size.Height;
            float dayHeaderH = 26f;
            float colW = totalW / dayCount;
            float slotH = (totalH - dayHeaderH) / slotsPerDay;

            var colSepColor  = new SKColor(209, 213, 219);
            var gridColor    = new SKColor(229, 231, 235, 100);
            var accentColor  = ParseHex(Accent);
            var borderC      = new SKColor(180, 180, 180);

            for (int d = 0; d < dayCount; d++)
            {
                var day = days[d];
                float x = d * colW;

                // Left column border
                using (var p = new SKPaint { Color = colSepColor, StrokeWidth = 0.5f })
                    canvas.DrawLine(x, 0, x, totalH, p);

                // Day header fill
                using (var p = new SKPaint { Color = accentColor })
                    canvas.DrawRect(x, 0, colW, dayHeaderH, p);

                // Day header text
                using (var p = new SKPaint { Color = SKColors.White, TextSize = 7.5f, IsAntialias = true })
                {
                    var label = day.Date.HasValue
                        ? $"{day.Title}  {day.Date.Value.ToString("dd/MM", Ptbr)}"
                        : day.Title;
                    label = Truncate(label, p, colW - 4);
                    canvas.DrawText(label, x + 3, dayHeaderH - 7, p);
                }

                // Horizontal slot grid
                using (var p = new SKPaint { Color = gridColor, StrokeWidth = 0.5f })
                {
                    for (int s = 1; s < slotsPerDay; s++)
                    {
                        float gy = dayHeaderH + s * slotH;
                        canvas.DrawLine(x, gy, x + colW, gy, p);
                    }
                }

                // Activity blocks
                foreach (var act in day.Activities.OrderBy(a => a.StartSlot))
                {
                    float ay = dayHeaderH + act.StartSlot * slotH + 1f;
                    float ah = act.DurationSlots * slotH - 2f;
                    float ax = x + 1f;
                    float aw = colW - 2f;
                    if (ah < 1f) continue;

                    // Fill
                    using (var p = new SKPaint { Color = ParseHex(act.Color), IsAntialias = true })
                        canvas.DrawRoundRect(ax, ay, aw, ah, 2, 2, p);

                    // Border
                    using (var p = new SKPaint
                    {
                        Color = DarkenColor(ParseHex(act.Color), 0.65f),
                        StrokeWidth = 0.7f,
                        Style = SKPaintStyle.Stroke,
                        IsAntialias = true
                    })
                        canvas.DrawRoundRect(ax, ay, aw, ah, 2, 2, p);

                    // Text
                    if (ah >= 10f)
                    {
                        canvas.Save();
                        canvas.ClipRect(SKRect.Create(ax + 1, ay + 1, aw - 2, ah - 2));

                        using (var p = new SKPaint { Color = SKColors.Black, TextSize = 7f, IsAntialias = true })
                        {
                            var title = Truncate(act.Title, p, aw - 4);
                            canvas.DrawText(title, ax + 2, ay + p.TextSize + 1, p);

                            if (ah >= 20f)
                            {
                                using var ps = new SKPaint { Color = new SKColor(75, 85, 99), TextSize = 6f, IsAntialias = true };
                                canvas.DrawText(Truncate(act.Type, ps, aw - 4), ax + 2, ay + p.TextSize + ps.TextSize + 3, ps);
                            }
                        }

                        canvas.Restore();
                    }
                }
            }

            // Right border + bottom border
            using (var p = new SKPaint { Color = colSepColor, StrokeWidth = 0.5f })
            {
                canvas.DrawLine(totalW, 0, totalW, totalH, p);
                canvas.DrawLine(0, totalH, totalW, totalH, p);
            }
        });
    }

    // ─────────────────────────────────────────────────────────────────────
    // Section renderers
    // ─────────────────────────────────────────────────────────────────────

    private static void DayBlock(IContainer container, ItineraryDay day)
    {
        container.Column(col =>
        {
            col.Item().Row(row =>
            {
                row.ConstantItem(32).Background(AccentLight).AlignCenter().AlignMiddle()
                   .Text(day.Title.Replace("Dia ", "")).FontSize(9).SemiBold().FontColor(Accent);

                row.RelativeItem().PaddingLeft(8).AlignMiddle().Column(c =>
                {
                    var parts = new List<string>();
                    if (day.Date.HasValue)
                        parts.Add(day.Date.Value.ToString("ddd dd/MM", Ptbr));
                    if (!string.IsNullOrWhiteSpace(day.Summary))
                        parts.Add(day.Summary);
                    c.Item().Text(string.Join("  —  ", parts)).SemiBold().FontSize(10);
                });
            });

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
                cols.RelativeColumn(4);
                cols.RelativeColumn(2);
                cols.RelativeColumn(2);
            });

            table.Header(h =>
            {
                foreach (var (label, right) in new[] { ("Item", false), ("Fornecedor", false), ("Total", true) })
                {
                    var cell = h.Cell().Background(AccentLight).Padding(4);
                    if (right) cell.AlignRight().Text(label).FontSize(8).SemiBold().FontColor(Accent);
                    else cell.Text(label).FontSize(8).SemiBold().FontColor(Accent);
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

    // ─────────────────────────────────────────────────────────────────────
    // Utilities
    // ─────────────────────────────────────────────────────────────────────

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

    private static SKColor ParseHex(string hex)
        => SKColor.TryParse(hex, out var c) ? c : new SKColor(219, 234, 254);

    private static SKColor DarkenColor(SKColor color, float factor)
    {
        color.ToHsl(out float h, out float s, out float l);
        return SKColor.FromHsl(h, s, l * factor);
    }

    private static string Truncate(string text, SKPaint paint, float maxWidth)
    {
        if (paint.MeasureText(text) <= maxWidth) return text;
        const string Ellipsis = "…";
        var ew = paint.MeasureText(Ellipsis);
        while (text.Length > 0 && paint.MeasureText(text) + ew > maxWidth)
            text = text[..^1];
        return text + Ellipsis;
    }

    private static string Fmt(decimal value, string currency) =>
        currency == "BRL" ? value.ToString("C2", Ptbr) : $"{currency} {value:N2}";
}
