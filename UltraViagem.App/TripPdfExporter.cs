using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
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

                    SetupHeader(page, trip, $"Roteiro Detalhado — {version.Name}");
                    page.Content().PaddingTop(8).Element(c => ItineraryDiagram(c, version, trip));
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

            // ── 5. Orçamento Detalhado (landscape) ───────────────────────
            if (trip.Expenses.Count > 0)
            {
                container.Page(page =>
                {
                    page.Size(new PageSize(297, 210, Unit.Millimetre));
                    page.MarginHorizontal(1.5f, Unit.Centimetre);
                    page.MarginVertical(1.5f, Unit.Centimetre);
                    page.DefaultTextStyle(ts => ts.FontFamily("Calibri").FontSize(10).FontColor(TextDefault));
                    SetupHeader(page, trip, "Orçamento Detalhado");
                    page.Content().PaddingTop(8).Element(c => DetailedBudgetTable(c, trip));
                });
            }

            // ── 6. Tarefas ────────────────────────────────────────────────
            if (trip.Tasks.Count > 0)
            {
                container.Page(page =>
                {
                    ConfigurePortraitPage(page, trip, "Tarefas");
                    page.Content().PaddingTop(10).Column(col =>
                    {
                        foreach (var task in trip.Tasks)
                            col.Item().PaddingBottom(4).Element(c => TaskRow(c, task));
                    });
                });
            }

        }).GeneratePdf(outputPath);
    }

    // ─────────────────────────────────────────────────────────────────────
    // Page setup
    // ─────────────────────────────────────────────────────────────────────

    /// <summary>Header e footer padronizados (portrait e landscape).</summary>
    private static void SetupHeader(PageDescriptor page, Trip trip, string sectionTitle)
    {
        page.Header().Column(col =>
        {
            col.Item().Row(row =>
            {
                row.RelativeItem().Column(c =>
                {
                    c.Item().Text(trip.Title).FontSize(15).SemiBold().FontColor(Accent);
                    c.Item().Text(sectionTitle).FontSize(9).FontColor(TextMuted);
                });
                row.ConstantItem(80).AlignRight().AlignBottom()
                   .Text("UltraViagem").FontSize(9).Italic().FontColor(TextMuted);
            });
            col.Item().PaddingTop(5).LineHorizontal(1).LineColor(Accent);
        });

        page.Footer().AlignCenter().Text(text =>
        {
            text.Span("Página ").FontSize(8).FontColor(TextMuted);
            text.CurrentPageNumber().FontSize(8).FontColor(TextMuted);
            text.Span(" de ").FontSize(8).FontColor(TextMuted);
            text.TotalPages().FontSize(8).FontColor(TextMuted);
        });
    }

    private static void ConfigurePortraitPage(PageDescriptor page, Trip trip, string sectionTitle)
    {
        page.Size(PageSizes.A4);
        page.MarginHorizontal(2.2f, Unit.Centimetre);
        page.MarginVertical(2f, Unit.Centimetre);
        page.DefaultTextStyle(ts => ts.FontFamily("Calibri").FontSize(10).FontColor(TextDefault));
        SetupHeader(page, trip, sectionTitle);
    }

    // ─────────────────────────────────────────────────────────────────────
    // Itinerary diagram — pivotado: dias = linhas, slots = eixo horizontal
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

        const float DayLabelPt = 62f;  // largura fixa da célula do dia
        const float MinRowPt   = 28f;  // altura mínima de cada linha (cresce se o texto quebrar)
        int slotsPerDay        = trip.ItinerarySlotsPerDay;

        // Reduz fonte levemente quando há muitos slots (blocos mais estreitos).
        // Trips com ≤ 8 slots ficam com o tamanho padrão; acima disso diminui 0.3pt por slot extra.
        float actFontSize    = Math.Clamp(7.5f - Math.Max(0, slotsPerDay - 8) * 0.3f, 6f, 7.5f);
        float detailFontSize = Math.Clamp(actFontSize - 1.5f, 4.5f, 6f);

        container.Column(col =>
        {
            foreach (var day in days)
            {
                col.Item().BorderBottom(0.5f).BorderColor(BorderColor)
                   .MinHeight(MinRowPt).Row(row =>
                   {
                       // Rótulo do dia
                       row.ConstantItem(DayLabelPt)
                          .Background(Accent)
                          .PaddingHorizontal(4).AlignMiddle()
                          .Text(day.Date.HasValue
                              ? $"{day.Title} · {day.Date.Value.ToString("dd/MM", Ptbr)}"
                              : day.Title)
                          .FontSize(7.5f).SemiBold().FontColor("#FFFFFF");

                       // Linha do tempo horizontal
                       int slot = 0;
                       foreach (var act in day.Activities.OrderBy(a => a.StartSlot))
                       {
                           int start = Math.Max(act.StartSlot, slot);
                           if (start > slot)
                               row.RelativeItem(start - slot).Background("#F3F4F6");

                           int dur = Math.Max(1, act.DurationSlots);
                           row.RelativeItem(dur)
                              .Background(act.Color)
                              .BorderLeft(0.5f).BorderColor(BorderColor)
                              .Padding(2).AlignCenter().AlignMiddle()
                              .Column(c =>
                              {
                                  c.Item().AlignCenter()
                                          .Text(act.Title).FontSize(actFontSize)
                                          .FontColor(ContrastColor(act.Color));
                                  if (!string.IsNullOrWhiteSpace(act.Details))
                                      c.Item().AlignCenter()
                                              .Text(act.Details).FontSize(detailFontSize)
                                              .FontColor(ContrastColor(act.Color));
                              });

                           slot = start + dur;
                       }

                       // Slots restantes vazios
                       if (slot < slotsPerDay)
                           row.RelativeItem(slotsPerDay - slot).Background("#F3F4F6");
                   });
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
            Checkbox(row.ConstantItem(18), done);
            row.RelativeItem().PaddingLeft(7).Column(col =>
            {
                col.Item().Text(task.Title).FontSize(10)
                   .FontColor(done ? TextMuted : TextDefault)
                   .Strikethrough(done);
                if (!string.IsNullOrWhiteSpace(task.Notes))
                    col.Item().Text(task.Notes).FontSize(8).FontColor(TextMuted);
            });
        });
    }

    private static void Checkbox(IContainer container, bool done)
    {
        var box = container.AlignTop().PaddingTop(1).Width(12).Height(12);
        if (done)
            box.Background(Accent).Border(1).BorderColor(Accent)
               .AlignCenter().AlignMiddle()
               .Text("✓").FontSize(6.5f).SemiBold().FontColor("#FFFFFF");
        else
            box.Border(1).BorderColor("#9CA3AF")
               .AlignCenter().AlignMiddle()
               .Text(" ").FontSize(1f);
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

    private static void DetailedBudgetTable(IContainer container, Trip trip)
    {
        decimal totalBase = trip.Expenses.Where(e => e.IsActive).Sum(e => e.SubtotalBase);
        decimal totalPaid = trip.Expenses.Where(e => e.IsActive).Sum(e => e.PaidAmount);

        container.Table(table =>
        {
            table.ColumnsDefinition(cols =>
            {
                cols.ConstantColumn(18);    // #
                cols.RelativeColumn(3.5f);  // Item
                cols.RelativeColumn(1.5f);  // Tipo
                cols.RelativeColumn(1.5f);  // Fornecedor
                cols.RelativeColumn(0.8f);  // Moeda
                cols.RelativeColumn(1.5f);  // Preço unit.
                cols.RelativeColumn(1f);    // Pes.×Qtd
                cols.RelativeColumn(1.2f);  // Câmbio
                cols.RelativeColumn(1.8f);  // Total base
                cols.RelativeColumn(1.8f);  // Pago base
            });

            table.Header(h =>
            {
                void HCell(string label, bool right = false)
                {
                    var cell = h.Cell().Background(AccentLight).Padding(3);
                    (right ? cell.AlignRight() : cell)
                        .Text(label).FontSize(7.5f).SemiBold().FontColor(Accent);
                }
                HCell("#",                        right: true);
                HCell("Item");
                HCell("Tipo");
                HCell("Fornecedor");
                HCell("Moeda");
                HCell("Preço unit.",               right: true);
                HCell("Pes.×Qtd",                  right: true);
                HCell("Câmbio",                    right: true);
                HCell($"Total ({trip.BaseCurrency})",right: true);
                HCell($"Pago ({trip.BaseCurrency})", right: true);
            });

            for (var i = 0; i < trip.Expenses.Count; i++)
            {
                var exp = trip.Expenses[i];
                var bg  = i % 2 == 0 ? "#FFFFFF" : RowAlt;
                var fg  = exp.IsActive ? TextDefault : TextMuted;

                decimal unitPrice = exp.Price + exp.Taxes;
                string  rateStr   = string.Equals(exp.Currency, trip.BaseCurrency,
                                        StringComparison.OrdinalIgnoreCase)
                                  ? "—"
                                  : exp.ExchangeRateToBase.ToString($"N{trip.RateDecimalDigits}", Ptbr);

                table.Cell().Background(bg).Padding(3).AlignRight()
                     .Text($"{i + 1}").FontSize(7.5f).FontColor(TextMuted);

                table.Cell().Background(bg).Padding(3).Column(c =>
                {
                    c.Item().Text(exp.IsActive ? exp.Title : $"[inativo] {exp.Title}")
                            .FontSize(8f).FontColor(fg);
                    if (!string.IsNullOrWhiteSpace(exp.Notes))
                        c.Item().Text(exp.Notes).FontSize(6.5f).FontColor(TextMuted);
                    if (!string.IsNullOrWhiteSpace(exp.Link))
                        c.Item().Text(exp.Link).FontSize(6.5f).FontColor("#2563EB");
                });

                table.Cell().Background(bg).Padding(3)
                     .Text(exp.Type ?? "").FontSize(7.5f).FontColor(TextMuted);
                table.Cell().Background(bg).Padding(3)
                     .Text(exp.Company ?? "").FontSize(7.5f).FontColor(TextMuted);
                table.Cell().Background(bg).Padding(3)
                     .Text(exp.Currency).FontSize(7.5f).FontColor(TextMuted);
                table.Cell().Background(bg).Padding(3).AlignRight()
                     .Text(unitPrice.ToString("N2", Ptbr)).FontSize(7.5f).FontColor(fg);
                table.Cell().Background(bg).Padding(3).AlignRight()
                     .Text($"{exp.People}×{exp.Quantity}").FontSize(7.5f).FontColor(fg);
                table.Cell().Background(bg).Padding(3).AlignRight()
                     .Text(rateStr).FontSize(7.5f).FontColor(TextMuted);
                table.Cell().Background(bg).Padding(3).AlignRight()
                     .Text(exp.IsActive ? Fmt(exp.SubtotalBase, trip.BaseCurrency) : "—")
                     .FontSize(7.5f).FontColor(fg);
                table.Cell().Background(bg).Padding(3).AlignRight()
                     .Text(exp.IsActive && exp.PaidAmount > 0
                           ? Fmt(exp.PaidAmount, trip.BaseCurrency) : "—")
                     .FontSize(7.5f).FontColor(fg);
            }

            // Linha de totais
            table.Cell().ColumnSpan(8).BorderTop(1).BorderColor(BorderColor).Padding(3)
                 .Text("Total").FontSize(8).SemiBold();
            table.Cell().BorderTop(1).BorderColor(BorderColor).Padding(3).AlignRight()
                 .Text(Fmt(totalBase, trip.BaseCurrency)).FontSize(8).SemiBold().FontColor(Accent);
            table.Cell().BorderTop(1).BorderColor(BorderColor).Padding(3).AlignRight()
                 .Text(Fmt(totalPaid, trip.BaseCurrency)).FontSize(8).SemiBold().FontColor(Accent);
        });
    }

    private static void TipRow(IContainer container, LinkItem link)
    {
        container.Column(col =>
        {
            col.Item().Text(link.Title).FontSize(10).SemiBold();
            if (!string.IsNullOrWhiteSpace(link.Url))
            {
                var isUrl = link.Url.StartsWith("http://",  StringComparison.OrdinalIgnoreCase)
                         || link.Url.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
                col.Item().Text(link.Url).FontSize(8).FontColor(isUrl ? "#2563EB" : TextMuted);
            }
        });
    }

    // ─────────────────────────────────────────────────────────────────────
    // Utilities
    // ─────────────────────────────────────────────────────────────────────

    /// <summary>Retorna preto ou branco conforme o brilho percebido do fundo hex.</summary>
    private static string ContrastColor(string hexColor)
    {
        hexColor = hexColor.TrimStart('#');
        if (hexColor.Length < 6) return TextDefault;
        int r = Convert.ToInt32(hexColor[0..2], 16);
        int g = Convert.ToInt32(hexColor[2..4], 16);
        int b = Convert.ToInt32(hexColor[4..6], 16);
        double brightness = (0.299 * r + 0.587 * g + 0.114 * b) / 255.0;
        return brightness > 0.55 ? TextDefault : "#FFFFFF";
    }

    private static string Fmt(decimal value, string currency) =>
        currency == "BRL" ? value.ToString("C2", Ptbr) : $"{currency} {value:N2}";
}
