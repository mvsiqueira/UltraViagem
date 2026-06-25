using System.Globalization;
using MigraDoc.DocumentObjectModel;
using MigraDoc.DocumentObjectModel.Tables;
using MigraDoc.Rendering;
using PdfSharp.Fonts;
using UltraViagem.Core;
using Cell  = MigraDoc.DocumentObjectModel.Tables.Cell;
using Color = MigraDoc.DocumentObjectModel.Color;
using VerticalAlignment = MigraDoc.DocumentObjectModel.Tables.VerticalAlignment;

namespace UltraViagem.Android.Services;

/// <summary>
/// Geração de PDF no Android via MigraDoc/PDFsharp (o QuestPDF usado no Windows não roda
/// no Android). Reproduz de perto o layout do <c>TripPdfExporter</c> do app desktop.
/// </summary>
public static class AndroidPdfExporter
{
    private static readonly CultureInfo Ptbr = new("pt-BR");
    private static bool _fontReady;

    private const string Accent      = "0F766E";
    private const string AccentLight = "CCFBF1";
    private const string TextDefault = "111827";
    private const string TextMuted   = "6B7280";
    private const string RowAlt      = "F9FAFB";
    private const string BorderHex   = "E5E7EB";
    private const string SlotEmpty   = "F3F4F6";
    private const string LinkBlue    = "2563EB";

    private const string Font = "Calibri";

    public static void Export(Trip trip, string outputPath)
    {
        if (!_fontReady)
        {
            GlobalFontSettings.FontResolver = CalibriFontResolver.Instance;
            _fontReady = true;
        }

        var activeVersion = trip.ItineraryVersions.FirstOrDefault(v => v.Id == trip.ActiveVersionId)
                         ?? trip.ItineraryVersions.FirstOrDefault();

        var doc = new Document();
        var normal = doc.Styles[StyleNames.Normal]!;
        normal.Font.Name = Font;
        normal.Font.Size = 10;
        normal.Font.Color = C(TextDefault);
        normal.ParagraphFormat.SpaceAfter = 0;
        normal.ParagraphFormat.LineSpacingRule = LineSpacingRule.Single;

        // 1. Roteiro (portrait)
        var s1 = NewSection(doc, trip, "Roteiro", landscape: false);
        if (activeVersion?.Itinerary.Count > 0)
            for (int i = 0; i < activeVersion.Itinerary.Count; i++)
                DayBlock(s1, activeVersion.Itinerary[i], i, trip.StartDate);
        else
            Muted(s1.AddParagraph("Nenhum dia cadastrado."));

        // 2. Roteiro Detalhado (landscape, uma seção por versão)
        foreach (var version in trip.ItineraryVersions)
        {
            var s = NewSection(doc, trip, $"Roteiro Detalhado — {version.Name}", landscape: true);
            ItineraryDiagram(s, version, trip);
        }

        // 3. Dicas
        if (trip.Links.Count > 0)
        {
            var s = NewSection(doc, trip, "Dicas", landscape: false);
            foreach (var link in trip.Links) TipRow(s, link);
        }

        // 4. Gastos
        if (trip.Expenses.Count > 0)
        {
            var s = NewSection(doc, trip, "Gastos", landscape: false);
            BudgetTable(s, trip);
        }

        // 5. Orçamento Detalhado (landscape)
        if (trip.Expenses.Count > 0)
        {
            var s = NewSection(doc, trip, "Orçamento Detalhado", landscape: true);
            DetailedBudgetTable(s, trip);
        }

        // 6. Tarefas
        if (trip.Tasks.Count > 0)
        {
            var s = NewSection(doc, trip, "Tarefas", landscape: false);
            foreach (var task in trip.Tasks) TaskRow(s, task);
        }

        var renderer = new PdfDocumentRenderer { Document = doc };
        renderer.RenderDocument();
        renderer.PdfDocument.Save(outputPath);
    }

    // ── Setup de seção (header + footer + margens) ───────────────────────

    private static Section NewSection(Document doc, Trip trip, string sectionTitle, bool landscape)
    {
        var section = doc.AddSection();
        var ps = section.PageSetup;
        ps.PageFormat = PageFormat.A4;
        if (landscape)
        {
            ps.Orientation = Orientation.Landscape;
            ps.LeftMargin = ps.RightMargin = Unit.FromCentimeter(1.5);
            ps.TopMargin    = Unit.FromCentimeter(2.4);
            ps.BottomMargin = Unit.FromCentimeter(1.8);
            ps.HeaderDistance = Unit.FromCentimeter(0.8);
            ps.FooterDistance = Unit.FromCentimeter(0.8);
        }
        else
        {
            ps.Orientation = Orientation.Portrait;
            ps.LeftMargin = ps.RightMargin = Unit.FromCentimeter(2.2);
            ps.TopMargin    = Unit.FromCentimeter(2.8);
            ps.BottomMargin = Unit.FromCentimeter(2.0);
            ps.HeaderDistance = Unit.FromCentimeter(1.0);
            ps.FooterDistance = Unit.FromCentimeter(1.0);
        }

        // Header: título + seção (esq) / "UltraViagem" (dir) + linha accent
        var header = section.Headers.Primary;
        var ht = header.AddTable();
        ht.Borders.Width = 0;
        ht.AddColumn(Unit.FromCentimeter(landscape ? 24.7 : 14.6));
        ht.AddColumn(Unit.FromCentimeter(2.0));
        var hr = ht.AddRow();

        var left = hr.Cells[0];
        var pTitle = left.AddParagraph(trip.Title);
        pTitle.Format.Font.Size = 15; pTitle.Format.Font.Bold = true; pTitle.Format.Font.Color = C(Accent);
        var pSec = left.AddParagraph(sectionTitle);
        pSec.Format.Font.Size = 9; pSec.Format.Font.Color = C(TextMuted);

        var right = hr.Cells[1];
        right.VerticalAlignment = VerticalAlignment.Bottom;
        var pApp = right.AddParagraph("UltraViagem");
        pApp.Format.Alignment = ParagraphAlignment.Right;
        pApp.Format.Font.Size = 9; pApp.Format.Font.Italic = true; pApp.Format.Font.Color = C(TextMuted);

        var line = header.AddParagraph();
        line.Format.SpaceBefore = Unit.FromPoint(5);
        line.Format.Borders.Bottom.Width = 1;
        line.Format.Borders.Bottom.Color = C(Accent);
        line.Format.SpaceAfter = Unit.FromPoint(8);

        // Footer: "Página X de Y"
        var footer = section.Footers.Primary.AddParagraph();
        footer.Format.Alignment = ParagraphAlignment.Center;
        footer.Format.Font.Size = 8; footer.Format.Font.Color = C(TextMuted);
        footer.AddText("Página ");
        footer.AddPageField();
        footer.AddText(" de ");
        footer.AddNumPagesField();

        return section;
    }

    // ── Roteiro (lista por dia) ──────────────────────────────────────────

    private static void DayBlock(Section section, ItineraryDay day, int index, DateOnly? tripStart)
    {
        var dayDate = tripStart?.AddDays(index);

        var head = section.AddTable();
        head.Borders.Width = 0;
        head.AddColumn(Unit.FromPoint(32));
        head.AddColumn(Unit.FromCentimeter(15));
        var hr = head.AddRow();
        hr.VerticalAlignment = VerticalAlignment.Center;

        hr.Cells[0].Shading.Color = C(AccentLight);
        var num = hr.Cells[0].AddParagraph($"{index + 1}");
        num.Format.Alignment = ParagraphAlignment.Center;
        num.Format.Font.Size = 9; num.Format.Font.Bold = true; num.Format.Font.Color = C(Accent);

        var parts = new List<string>();
        if (dayDate.HasValue) parts.Add(dayDate.Value.ToString("ddd dd/MM", Ptbr));
        if (!string.IsNullOrWhiteSpace(day.Summary)) parts.Add(day.Summary);
        var ptxt = hr.Cells[1].AddParagraph(string.Join("  —  ", parts));
        ptxt.Format.LeftIndent = Unit.FromPoint(8);
        ptxt.Format.Font.Size = 10; ptxt.Format.Font.Bold = true;

        if (day.Activities.Count > 0)
        {
            var at = section.AddTable();
            at.Borders.Width = 0;
            at.AddColumn(Unit.FromPoint(40)); // recuo
            at.AddColumn(Unit.FromPoint(70)); // tipo
            at.AddColumn(Unit.FromCentimeter(12)); // título
            foreach (var act in day.Activities.OrderBy(a => a.StartSlot))
            {
                var r = at.AddRow();
                var pt = r.Cells[1].AddParagraph(act.Type ?? "");
                pt.Format.Font.Size = 8; pt.Format.Font.Color = C(TextMuted);
                r.Cells[2].AddParagraph(act.Title).Format.Font.Size = 9;
            }
        }

        section.AddParagraph().Format.SpaceAfter = Unit.FromPoint(8);
    }

    // ── Roteiro Detalhado (diagrama horizontal por slots) ────────────────

    private static void ItineraryDiagram(Section section, ItineraryVersion version, Trip trip)
    {
        var days = version.Itinerary;
        if (days.Count == 0)
        {
            Muted(section.AddParagraph("Nenhum dia nesta versão."));
            return;
        }

        int slots = Math.Max(1, trip.ItinerarySlotsPerDay);
        float actFont    = Math.Clamp(7.5f - Math.Max(0, slots - 8) * 0.3f, 6f, 7.5f);
        float detailFont = Math.Clamp(actFont - 1.5f, 4.5f, 6f);

        double labelCm = Unit.FromPoint(62).Centimeter;
        double slotCm  = (26.7 - labelCm) / slots;

        var table = section.AddTable();
        table.Borders.Width = 0;
        table.AddColumn(Unit.FromPoint(62));
        for (int i = 0; i < slots; i++)
            table.AddColumn(Unit.FromCentimeter(slotCm));

        for (int idx = 0; idx < days.Count; idx++)
        {
            var day = days[idx];
            var row = table.AddRow();
            row.Height = Unit.FromPoint(28);
            row.HeightRule = RowHeightRule.AtLeast;
            row.VerticalAlignment = VerticalAlignment.Center;

            // borda inferior em toda a linha
            for (int c = 0; c <= slots; c++)
            {
                row.Cells[c].Borders.Bottom.Width = 0.5;
                row.Cells[c].Borders.Bottom.Color = C(BorderHex);
            }

            // rótulo do dia
            var dayDate = trip.StartDate?.AddDays(idx);
            row.Cells[0].Shading.Color = C(Accent);
            var lbl = row.Cells[0].AddParagraph(dayDate.HasValue
                ? $"Dia {idx + 1} · {dayDate.Value.ToString("dd/MM", Ptbr)}"
                : $"Dia {idx + 1}");
            lbl.Format.Font.Size = 7.5; lbl.Format.Font.Bold = true; lbl.Format.Font.Color = C("FFFFFF");

            int slot = 0;
            foreach (var act in day.Activities.OrderBy(a => a.StartSlot))
            {
                int start = Math.Max(act.StartSlot, slot);
                if (start >= slots) break;
                for (int g = slot; g < start; g++)
                    row.Cells[1 + g].Shading.Color = C(SlotEmpty);

                int dur = Math.Max(1, act.DurationSlots);
                int end = Math.Min(start + dur, slots);
                int span = end - start;

                var cell = row.Cells[1 + start];
                cell.Shading.Color = C(act.Color);
                if (span > 1) cell.MergeRight = span - 1;
                cell.Borders.Left.Width = 0.5;
                cell.Borders.Left.Color = C(BorderHex);

                var fg = C(ContrastColor(act.Color));
                var pa = cell.AddParagraph(act.Title);
                pa.Format.Alignment = ParagraphAlignment.Center;
                pa.Format.Font.Size = actFont; pa.Format.Font.Color = fg;
                if (!string.IsNullOrWhiteSpace(act.Details))
                {
                    var pd = cell.AddParagraph(act.Details);
                    pd.Format.Alignment = ParagraphAlignment.Center;
                    pd.Format.Font.Size = detailFont; pd.Format.Font.Color = fg;
                }

                slot = end;
            }

            for (int g = slot; g < slots; g++)
                row.Cells[1 + g].Shading.Color = C(SlotEmpty);
        }
    }

    // ── Dicas ─────────────────────────────────────────────────────────────

    private static void TipRow(Section section, LinkItem link)
    {
        var pt = section.AddParagraph(link.Title);
        pt.Format.Font.Size = 10; pt.Format.Font.Bold = true;
        if (!string.IsNullOrWhiteSpace(link.Url))
        {
            bool isUrl = link.Url.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                      || link.Url.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
            var pu = section.AddParagraph(link.Url);
            pu.Format.Font.Size = 8; pu.Format.Font.Color = isUrl ? C(LinkBlue) : C(TextMuted);
        }
        section.AddParagraph().Format.SpaceAfter = Unit.FromPoint(8);
    }

    // ── Tarefas ─────────────────────────────────────────────────────────

    private static void TaskRow(Section section, TaskItem task)
    {
        bool done = task.Status == "done";

        var t = section.AddTable();
        t.Borders.Width = 0;
        t.AddColumn(Unit.FromPoint(18));
        t.AddColumn(Unit.FromCentimeter(15.5));
        var r = t.AddRow();

        var box = r.Cells[0];
        var pb = box.AddParagraph(done ? "✓" : "");
        pb.Format.Alignment = ParagraphAlignment.Center;
        pb.Format.Font.Size = done ? 7 : 1;
        if (done)
        {
            box.Shading.Color = C(Accent);
            pb.Format.Font.Bold = true; pb.Format.Font.Color = C("FFFFFF");
        }
        else
        {
            box.Borders.Width = 1; box.Borders.Color = C("9CA3AF");
        }

        var pTitle = r.Cells[1].AddParagraph(task.Title);
        pTitle.Format.LeftIndent = Unit.FromPoint(7);
        pTitle.Format.Font.Size = 10;
        pTitle.Format.Font.Color = done ? C(TextMuted) : C(TextDefault);
        if (!string.IsNullOrWhiteSpace(task.Notes))
        {
            var pn = r.Cells[1].AddParagraph(task.Notes);
            pn.Format.LeftIndent = Unit.FromPoint(7);
            pn.Format.Font.Size = 8; pn.Format.Font.Color = C(TextMuted);
        }

        section.AddParagraph().Format.SpaceAfter = Unit.FromPoint(4);
    }

    // ── Gastos (resumo) ──────────────────────────────────────────────────

    private static void BudgetTable(Section section, Trip trip)
    {
        var total = trip.Expenses.Where(e => e.IsActive).Sum(e => e.SubtotalBase);

        double w = 16.6 / 8.0;
        var table = section.AddTable();
        table.Borders.Width = 0;
        table.AddColumn(Unit.FromCentimeter(w * 4));
        table.AddColumn(Unit.FromCentimeter(w * 2));
        table.AddColumn(Unit.FromCentimeter(w * 2));

        var h = table.AddRow();
        HeaderCell(h.Cells[0], "Item", false);
        HeaderCell(h.Cells[1], "Fornecedor", false);
        HeaderCell(h.Cells[2], "Total", true);

        for (int i = 0; i < trip.Expenses.Count; i++)
        {
            var exp = trip.Expenses[i];
            var bg = i % 2 == 0 ? "FFFFFF" : RowAlt;
            var fg = exp.IsActive ? TextDefault : TextMuted;
            var row = table.AddRow();
            Cell(row.Cells[0], exp.Title, 9, fg, bg);
            Cell(row.Cells[1], exp.Company ?? "", 9, TextMuted, bg);
            Cell(row.Cells[2], exp.IsActive ? Fmt(exp.SubtotalBase, trip.BaseCurrency) : "—", 9, fg, bg, right: true);
        }

        var tr = table.AddRow();
        var tc0 = tr.Cells[0]; tc0.MergeRight = 1;
        TotalCell(tc0, "Total estimado", false, TextDefault);
        TotalCell(tr.Cells[2], Fmt(total, trip.BaseCurrency), true, Accent);
    }

    // ── Orçamento Detalhado ──────────────────────────────────────────────

    private static void DetailedBudgetTable(Section section, Trip trip)
    {
        decimal totalBase = trip.Expenses.Where(e => e.IsActive).Sum(e => e.SubtotalBase);
        decimal totalPaid = trip.Expenses.Where(e => e.IsActive).Sum(e => e.PaidAmount);

        double u = (26.7 - Unit.FromPoint(18).Centimeter) / 14.6;
        var table = section.AddTable();
        table.Borders.Width = 0;
        table.AddColumn(Unit.FromPoint(18));          // #
        foreach (var f in new[] { 3.5, 1.5, 1.5, 0.8, 1.5, 1.0, 1.2, 1.8, 1.8 })
            table.AddColumn(Unit.FromCentimeter(u * f));

        var h = table.AddRow();
        HeaderCell(h.Cells[0], "#", true);
        HeaderCell(h.Cells[1], "Item", false);
        HeaderCell(h.Cells[2], "Tipo", false);
        HeaderCell(h.Cells[3], "Fornecedor", false);
        HeaderCell(h.Cells[4], "Moeda", false);
        HeaderCell(h.Cells[5], "Preço unit.", true);
        HeaderCell(h.Cells[6], "Pes.×Qtd", true);
        HeaderCell(h.Cells[7], "Câmbio", true);
        HeaderCell(h.Cells[8], $"Total ({trip.BaseCurrency})", true);
        HeaderCell(h.Cells[9], $"Pago ({trip.BaseCurrency})", true);
        foreach (Cell c in h.Cells) c.Format.Font.Size = 7.5;

        for (int i = 0; i < trip.Expenses.Count; i++)
        {
            var exp = trip.Expenses[i];
            var bg = i % 2 == 0 ? "FFFFFF" : RowAlt;
            var fg = exp.IsActive ? TextDefault : TextMuted;
            decimal unitPrice = exp.Price + exp.Taxes;
            string rateStr = string.Equals(exp.Currency, trip.BaseCurrency, StringComparison.OrdinalIgnoreCase)
                ? "—" : exp.ExchangeRateToBase.ToString($"N{trip.RateDecimalDigits}", Ptbr);

            var row = table.AddRow();
            Cell(row.Cells[0], $"{i + 1}", 7.5, TextMuted, bg, right: true);

            var item = row.Cells[1]; item.Shading.Color = C(bg);
            var pi = item.AddParagraph(exp.IsActive ? exp.Title : $"[inativo] {exp.Title}");
            pi.Format.Font.Size = 8; pi.Format.Font.Color = C(fg);
            if (!string.IsNullOrWhiteSpace(exp.Notes))
            {
                var pn = item.AddParagraph(exp.Notes);
                pn.Format.Font.Size = 6.5; pn.Format.Font.Color = C(TextMuted);
            }
            if (!string.IsNullOrWhiteSpace(exp.Link))
            {
                var pl = item.AddParagraph(exp.Link);
                pl.Format.Font.Size = 6.5; pl.Format.Font.Color = C(LinkBlue);
            }

            Cell(row.Cells[2], exp.Type ?? "", 7.5, TextMuted, bg);
            Cell(row.Cells[3], exp.Company ?? "", 7.5, TextMuted, bg);
            Cell(row.Cells[4], exp.Currency, 7.5, TextMuted, bg);
            Cell(row.Cells[5], unitPrice.ToString("N2", Ptbr), 7.5, fg, bg, right: true);
            Cell(row.Cells[6], $"{exp.People}×{exp.Quantity}", 7.5, fg, bg, right: true);
            Cell(row.Cells[7], rateStr, 7.5, TextMuted, bg, right: true);
            Cell(row.Cells[8], exp.IsActive ? Fmt(exp.SubtotalBase, trip.BaseCurrency) : "—", 7.5, fg, bg, right: true);
            Cell(row.Cells[9], exp.IsActive && exp.PaidAmount > 0 ? Fmt(exp.PaidAmount, trip.BaseCurrency) : "—", 7.5, fg, bg, right: true);
        }

        var tr = table.AddRow();
        var c0 = tr.Cells[0]; c0.MergeRight = 7;
        TotalCell(c0, "Total", false, TextDefault, 8);
        TotalCell(tr.Cells[8], Fmt(totalBase, trip.BaseCurrency), true, Accent, 8);
        TotalCell(tr.Cells[9], Fmt(totalPaid, trip.BaseCurrency), true, Accent, 8);
    }

    // ── Helpers de célula ────────────────────────────────────────────────

    private static void HeaderCell(Cell cell, string label, bool right)
    {
        cell.Shading.Color = C(AccentLight);
        var p = cell.AddParagraph(label);
        if (right) p.Format.Alignment = ParagraphAlignment.Right;
        p.Format.Font.Size = 8; p.Format.Font.Bold = true; p.Format.Font.Color = C(Accent);
    }

    private static void Cell(Cell cell, string text, double size, string colorHex, string bgHex, bool right = false)
    {
        cell.Shading.Color = C(bgHex);
        var p = cell.AddParagraph(text);
        if (right) p.Format.Alignment = ParagraphAlignment.Right;
        p.Format.Font.Size = size; p.Format.Font.Color = C(colorHex);
    }

    private static void TotalCell(Cell cell, string text, bool right, string colorHex, double size = 9)
    {
        cell.Borders.Top.Width = 1; cell.Borders.Top.Color = C(BorderHex);
        var p = cell.AddParagraph(text);
        if (right) p.Format.Alignment = ParagraphAlignment.Right;
        p.Format.Font.Size = size; p.Format.Font.Bold = true; p.Format.Font.Color = C(colorHex);
    }

    private static void Muted(Paragraph p) { p.Format.Font.Color = C(TextMuted); }

    // ── Utilidades ───────────────────────────────────────────────────────

    private static Color C(string hex)
    {
        hex = hex.TrimStart('#');
        return new Color(
            Convert.ToByte(hex[0..2], 16),
            Convert.ToByte(hex[2..4], 16),
            Convert.ToByte(hex[4..6], 16));
    }

    private static string ContrastColor(string hexColor)
    {
        hexColor = hexColor.TrimStart('#');
        if (hexColor.Length < 6) return TextDefault;
        int r = Convert.ToInt32(hexColor[0..2], 16);
        int g = Convert.ToInt32(hexColor[2..4], 16);
        int b = Convert.ToInt32(hexColor[4..6], 16);
        double brightness = (0.299 * r + 0.587 * g + 0.114 * b) / 255.0;
        return brightness > 0.55 ? TextDefault : "FFFFFF";
    }

    private static string Fmt(decimal value, string currency) =>
        currency == "BRL" ? value.ToString("C2", Ptbr) : $"{currency} {value.ToString("N2", Ptbr)}";
}
