using System.Collections.ObjectModel;
using System.Globalization;
using UltraViagem.Android.Services;
using UltraViagem.Core;

namespace UltraViagem.Android.ViewModels;

public sealed class TripViewModel : BindableObject
{
    public static TripViewModel? Current { get; set; }

    private static readonly CultureInfo PtBr = new("pt-BR");

    private readonly TripFileService _fileService;
    private string? _currentTripUri;
    private string? _currentFolderUri;

    public Trip Trip { get; private set; } = new();
    public ItineraryVersion? ActiveVersion { get; private set; }

    public string DurationLabel { get; private set; } = "";
    public string TotalLabel    { get; private set; } = "";
    public string PaidLabel     { get; private set; } = "";
    public bool   HasMapUrl     => !string.IsNullOrWhiteSpace(Trip.MyMapsUrl);
    public bool   HasTasks      => Trip.Tasks.Count > 0;
    public bool   HasLinks      => Trip.Links.Count > 0;
    public bool   HasExpenses   => Trip.Expenses.Count > 0;

    // ── Resumos dos blocos da Visão Geral ───────────────────
    public string ItineraryInfo { get; private set; } = "";
    public string TasksInfo      { get; private set; } = "";
    public string MapInfo        { get; private set; } = "";
    public string ExpensesInfo   { get; private set; } = "";
    public string LinksInfo      { get; private set; } = "";
    public string FilesInfo      { get; private set; } = "";

    // ── Gastos agrupados por categoria + resumo ──────────────
    public ObservableCollection<ExpenseGroup> ExpenseGroups { get; } = [];
    public string PendingLabel { get; private set; } = "";
    public double PaidFraction { get; private set; }

    // Disparado quando o usuário toca num bloco da Visão Geral; TripPage troca de seção.
    public event Action<int>? SectionRequested;
    public void RequestSection(int index) => SectionRequested?.Invoke(index);

    public List<NumberedDay> DisplayItinerary { get; private set; } = [];
    public ObservableCollection<ObservableTaskItem> ObservableTasks  { get; } = [];
    public ObservableCollection<LinkItem>           ObservableLinks  { get; } = [];

    public TripViewModel(TripFileService fileService)
    {
        _fileService = fileService;
    }

    public void Load(Trip trip, string? tripUri = null, string? folderUri = null)
    {
        Trip = trip;
        _currentTripUri   = tripUri;
        _currentFolderUri = folderUri;

        ActiveVersion = trip.ItineraryVersions.FirstOrDefault(v => v.Id == trip.ActiveVersionId)
                     ?? trip.ItineraryVersions.FirstOrDefault();

        DurationLabel    = BuildDurationLabel(trip);
        TotalLabel       = BuildTotalLabel(trip);
        PaidLabel        = BuildPaidLabel(trip);
        DisplayItinerary = ActiveVersion?.Itinerary
            .Select((d, i) => new NumberedDay(d, i + 1, trip.StartDate?.AddDays(i)))
            .ToList() ?? [];

        BuildOverviewInfos(trip);
        BuildExpenseGroups(trip);

        ObservableTasks.Clear();
        foreach (var t in trip.Tasks)
            ObservableTasks.Add(new ObservableTaskItem(t));

        ObservableLinks.Clear();
        foreach (var l in trip.Links)
            ObservableLinks.Add(l);

        OnPropertyChanged(nameof(Trip));
        OnPropertyChanged(nameof(ActiveVersion));
        OnPropertyChanged(nameof(DurationLabel));
        OnPropertyChanged(nameof(TotalLabel));
        OnPropertyChanged(nameof(PaidLabel));
        OnPropertyChanged(nameof(HasMapUrl));
        OnPropertyChanged(nameof(HasTasks));
        OnPropertyChanged(nameof(HasLinks));
        OnPropertyChanged(nameof(HasExpenses));
        OnPropertyChanged(nameof(DisplayItinerary));
        OnPropertyChanged(nameof(ItineraryInfo));
        OnPropertyChanged(nameof(TasksInfo));
        OnPropertyChanged(nameof(MapInfo));
        OnPropertyChanged(nameof(ExpensesInfo));
        OnPropertyChanged(nameof(LinksInfo));
        OnPropertyChanged(nameof(FilesInfo));
        OnPropertyChanged(nameof(ExpenseGroups));
        OnPropertyChanged(nameof(PendingLabel));
        OnPropertyChanged(nameof(PaidFraction));
    }

    private void BuildExpenseGroups(Trip trip)
    {
        ExpenseGroups.Clear();
        var baseCur = trip.BaseCurrency;

        // Agrupa por categoria preservando a ordem de primeira aparição
        var order = new List<string>();
        var buckets = new Dictionary<string, List<ExpenseRow>>(StringComparer.OrdinalIgnoreCase);
        foreach (var e in trip.Expenses)
        {
            var cat = string.IsNullOrWhiteSpace(e.Type) ? "Outros" : e.Type!.Trim();
            if (!buckets.TryGetValue(cat, out var list))
            {
                list = [];
                buckets[cat] = list;
                order.Add(cat);
            }
            list.Add(new ExpenseRow(e, baseCur));
        }

        foreach (var cat in order)
        {
            var rows     = buckets[cat];
            var subtotal = rows.Where(r => r.IsActive).Sum(r => r.ValueBaseRaw);
            ExpenseGroups.Add(new ExpenseGroup(cat, ExpenseCategoryStyle.For(cat),
                FormatCurrency(subtotal, baseCur), rows));
        }

        var total   = trip.Expenses.Where(e => e.IsActive).Sum(e => e.SubtotalBase);
        var paid    = trip.Expenses.Where(e => e.IsActive).Sum(e => e.PaidAmount);
        var pending = Math.Max(0m, total - paid);
        PendingLabel = FormatCurrency(pending, baseCur);
        PaidFraction = total > 0 ? (double)Math.Clamp(paid / total, 0m, 1m) : 0d;
    }

    // ── Edição de gastos ─────────────────────────────────────

    public async Task AddExpenseAsync(ExpenseItem values)
    {
        values.Id = Guid.NewGuid().ToString("N")[..8];
        Trip.Expenses.Add(values);
        RefreshExpenseState();
        await SaveAsync();
    }

    public async Task UpdateExpenseAsync(ExpenseItem target, ExpenseItem v)
    {
        target.Title    = v.Title;
        target.Type     = v.Type;
        target.Company  = v.Company;
        target.Link     = v.Link;
        target.Notes    = v.Notes;
        target.Price    = v.Price;
        target.Taxes    = v.Taxes;
        target.People   = v.People;
        target.Quantity = v.Quantity;
        target.Currency = v.Currency;
        target.ExchangeRateToBase = v.ExchangeRateToBase;
        target.PaidAmount = v.PaidAmount;
        target.IsActive   = v.IsActive;
        RefreshExpenseState();
        await SaveAsync();
    }

    public async Task DeleteExpenseAsync(ExpenseItem item)
    {
        Trip.Expenses.Remove(item);
        RefreshExpenseState();
        await SaveAsync();
    }

    private void RefreshExpenseState()
    {
        TotalLabel = BuildTotalLabel(Trip);
        PaidLabel  = BuildPaidLabel(Trip);
        BuildExpenseGroups(Trip);
        BuildOverviewInfos(Trip);
        OnPropertyChanged(nameof(TotalLabel));
        OnPropertyChanged(nameof(PaidLabel));
        OnPropertyChanged(nameof(HasExpenses));
        OnPropertyChanged(nameof(ExpensesInfo));
        OnPropertyChanged(nameof(ExpenseGroups));
        OnPropertyChanged(nameof(PendingLabel));
        OnPropertyChanged(nameof(PaidFraction));
    }

    private void BuildOverviewInfos(Trip trip)
    {
        var days       = ActiveVersion?.Itinerary.Count ?? 0;
        var activities = ActiveVersion?.Itinerary.Sum(d => d.Activities.Count) ?? 0;
        ItineraryInfo = days == 0
            ? "Sem roteiro"
            : $"{days} {(days == 1 ? "dia" : "dias")} · {activities} {(activities == 1 ? "atividade" : "atividades")}";

        var doneCount = trip.Tasks.Count(t => t.Status == "done");
        TasksInfo = trip.Tasks.Count == 0
            ? "Nenhuma tarefa"
            : $"{doneCount} de {trip.Tasks.Count} feitas";

        MapInfo = HasMapUrl ? "Ver no Google Maps" : "Não configurado";

        if (trip.Expenses.Count == 0)
            ExpensesInfo = "Sem gastos";
        else
        {
            var total = trip.Expenses.Where(e => e.IsActive).Sum(e => e.SubtotalBase);
            var paid  = trip.Expenses.Where(e => e.IsActive).Sum(e => e.PaidAmount);
            var pct   = total > 0 ? (int)Math.Round(paid / total * 100) : 0;
            ExpensesInfo = $"{FormatCurrency(total, trip.BaseCurrency)} · {pct}% pago";
        }

        LinksInfo = trip.Links.Count == 0
            ? "Nenhum link"
            : $"{trip.Links.Count} {(trip.Links.Count == 1 ? "link útil" : "links úteis")}";

        FilesInfo = trip.Attachments.Count == 0
            ? "Nenhum anexo"
            : $"{trip.Attachments.Count} {(trip.Attachments.Count == 1 ? "anexo" : "anexos")}";
    }

    // ── Tarefas ─────────────────────────────────────────────

    public async Task ToggleTaskAsync(ObservableTaskItem item)
    {
        item.Toggle();
        await SaveAsync();
    }

    public async Task AddTaskAsync(string title, string? notes = null)
    {
        var task = new TaskItem
        {
            Id     = Guid.NewGuid().ToString("N")[..8],
            Title  = title,
            Notes  = notes,
            Status = "pending"
        };
        Trip.Tasks.Add(task);
        ObservableTasks.Add(new ObservableTaskItem(task));
        await SaveAsync();
    }

    public async Task DeleteTaskAsync(ObservableTaskItem item)
    {
        ObservableTasks.Remove(item);
        Trip.Tasks.Remove(item.Inner);
        await SaveAsync();
    }

    public async Task SaveAsync()
    {
        if (_currentTripUri != null)
            await _fileService.SaveTripAsync(_currentTripUri, Trip);
    }

    // ── Exportação PDF ───────────────────────────────────────

    public async Task<string> ExportPdfAsync()
    {
        var safe = string.IsNullOrWhiteSpace(Trip.Id) ? "viagem" : Trip.Id;
        var path = Path.Combine(FileSystem.CacheDirectory, $"{safe}.pdf");
        await Task.Run(() => Services.AndroidPdfExporter.Export(Trip, path));
        return path;
    }

    // ── Dicas ────────────────────────────────────────────────

    public async Task AddLinkAsync(string title, string url)
    {
        var link = new LinkItem
        {
            Id    = Guid.NewGuid().ToString("N")[..8],
            Title = title,
            Url   = url
        };
        Trip.Links.Add(link);
        ObservableLinks.Add(link);
        await SaveAsync();
    }

    public async Task EditLinkAsync(LinkItem link, string title, string url)
    {
        link.Title = title;
        link.Url   = url;
        var idx = ObservableLinks.IndexOf(link);
        if (idx >= 0)
        {
            ObservableLinks.RemoveAt(idx);
            ObservableLinks.Insert(idx, link);
        }
        await SaveAsync();
    }

    // ── Arquivos ─────────────────────────────────────────────

    private global::Android.Net.Uri? GetAttachmentUri(string filename)
    {
        if (_currentTripUri != null)
        {
            var uri = _fileService.BuildSiblingUri(_currentTripUri, filename);
            if (uri != null) return uri;
        }
        if (_currentFolderUri != null)
            return _fileService.FindSiblingInFolder(_currentFolderUri, filename);
        return null;
    }

    public async Task DeleteAttachmentAsync(AttachmentItem attachment)
    {
        var sibUri = GetAttachmentUri(attachment.File);
        if (sibUri != null)
            try { global::Android.Provider.DocumentsContract.DeleteDocument(Platform.AppContext.ContentResolver!, sibUri); }
            catch { }
        Trip.Attachments.Remove(attachment);
        await SaveAsync();
    }

    public async Task<bool> DownloadAttachmentAsync(AttachmentItem attachment)
    {
        var srcUri = GetAttachmentUri(attachment.File);
        if (srcUri == null) return false;
        try
        {
            var ext  = Path.GetExtension(attachment.File).TrimStart('.').ToLowerInvariant();
            var mime = ext switch
            {
                "pdf"  => "application/pdf",
                "jpg" or "jpeg" => "image/jpeg",
                "png"  => "image/png",
                "gif"  => "image/gif",
                "docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                "doc"  => "application/msword",
                "xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                "xls"  => "application/vnd.ms-excel",
                "txt"  => "text/plain",
                "mp4"  => "video/mp4",
                "mov"  => "video/quicktime",
                _      => "application/octet-stream"
            };
            var ctx = Platform.AppContext;
            var values = new global::Android.Content.ContentValues();
            values.Put("_display_name", attachment.File);
            values.Put("mime_type", mime);
            values.Put("relative_path", "Download/UltraViagem/");
            var destUri = ctx.ContentResolver!.Insert(
                global::Android.Provider.MediaStore.Downloads.GetContentUri("external")!, values)!;
            using var src = ctx.ContentResolver!.OpenInputStream(srcUri)!;
            using var dst = ctx.ContentResolver!.OpenOutputStream(destUri)!;
            await src.CopyToAsync(dst);
            return true;
        }
        catch { return false; }
    }

    public async Task OpenAttachmentAsync(AttachmentItem attachment)
    {
        var siblingUri = GetAttachmentUri(attachment.File);
        if (siblingUri == null) return;
        try
        {
            var ext  = Path.GetExtension(attachment.File).TrimStart('.').ToLowerInvariant();
            var mime = ext switch
            {
                "pdf"  => "application/pdf",
                "jpg" or "jpeg" => "image/jpeg",
                "png"  => "image/png",
                "gif"  => "image/gif",
                "docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                "doc"  => "application/msword",
                "xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                "xls"  => "application/vnd.ms-excel",
                "txt"  => "text/plain",
                "mp4"  => "video/mp4",
                "mov"  => "video/quicktime",
                _      => "*/*"
            };
            var intent = new global::Android.Content.Intent(global::Android.Content.Intent.ActionView);
            intent.SetDataAndType(siblingUri, mime);
            intent.AddFlags(global::Android.Content.ActivityFlags.GrantReadUriPermission);
            Platform.CurrentActivity!.StartActivity(intent);
        }
        catch
        {
            MainThread.BeginInvokeOnMainThread(async () =>
                await Application.Current!.Windows[0].Page!.DisplayAlert(
                    "Não foi possível abrir",
                    $"Nenhum aplicativo encontrado para '{attachment.File}'.",
                    "OK"));
        }
    }

    public async Task DeleteLinkAsync(LinkItem link)
    {
        ObservableLinks.Remove(link);
        Trip.Links.Remove(link);
        await SaveAsync();
    }

    // ── Labels ──────────────────────────────────────────────

    private static string BuildDurationLabel(Trip trip)
    {
        if (trip.StartDate is null && trip.EndDate is null) return "";
        var parts = new List<string>();
        if (trip.StartDate.HasValue && trip.EndDate.HasValue)
        {
            var start = trip.StartDate.Value;
            var end   = trip.EndDate.Value;
            int days  = end.DayNumber - start.DayNumber + 1;
            if (start.Month == end.Month && start.Year == end.Year)
                parts.Add($"{start.Day} – {end.Day} {start.ToString("MMM", PtBr)} {start.Year}");
            else
                parts.Add($"{start.ToString("dd MMM", PtBr)} – {end.ToString("dd MMM", PtBr)} {end.Year}");
            parts.Add($"{days} {(days == 1 ? "dia" : "dias")}");
        }
        else if (trip.StartDate.HasValue)
            parts.Add(trip.StartDate.Value.ToString("dd MMM yyyy", PtBr));
        return string.Join(" · ", parts);
    }

    private static string BuildTotalLabel(Trip trip)
        => FormatCurrency(trip.Expenses.Where(e => e.IsActive).Sum(e => e.SubtotalBase), trip.BaseCurrency);

    private static string BuildPaidLabel(Trip trip)
        => FormatCurrency(trip.Expenses.Where(e => e.IsActive).Sum(e => e.PaidAmount), trip.BaseCurrency);

    public static string FormatCurrency(decimal value, string currency)
        => currency == "BRL"
            ? value.ToString("C2", PtBr)
            : $"{currency} {value.ToString("N2", PtBr)}";

    public static string ContrastColor(string hexColor)
    {
        hexColor = hexColor.TrimStart('#');
        if (hexColor.Length < 6) return "#111827";
        int r = Convert.ToInt32(hexColor[0..2], 16);
        int g = Convert.ToInt32(hexColor[2..4], 16);
        int b = Convert.ToInt32(hexColor[4..6], 16);
        double brightness = (0.299 * r + 0.587 * g + 0.114 * b) / 255.0;
        return brightness > 0.55 ? "#111827" : "#FFFFFF";
    }
}

public sealed class NumberedDay
{
    public string Label     { get; }
    public string DateLabel { get; }
    public string Summary   { get; }
    public List<ActivityRow> Activities { get; }
    public bool   HasActivities => Activities.Count > 0;

    public NumberedDay(ItineraryDay day, int number, DateOnly? date)
    {
        Label      = $"D{number}";
        DateLabel  = date?.ToString("ddd, dd/MM/yyyy", new CultureInfo("pt-BR")) ?? "";
        Summary    = string.IsNullOrWhiteSpace(day.Summary) ? $"Dia {number}" : day.Summary;
        Activities = day.Activities
            .OrderBy(a => a.StartSlot)
            .Select(a => new ActivityRow(a))
            .ToList();
    }
}

// Atividade do roteiro com estado de expansão (somente leitura)
public sealed class ActivityRow : BindableObject
{
    private readonly ItineraryActivity _a;

    public ActivityRow(ItineraryActivity a) => _a = a;

    public string Title      => _a.Title;
    public string Color      => string.IsNullOrWhiteSpace(_a.Color) ? "#E5E7EB" : _a.Color;
    public string TypeLabel  => _a.Type ?? "";
    public bool   HasType    => !string.IsNullOrWhiteSpace(_a.Type);
    public bool   HasDetails => !string.IsNullOrWhiteSpace(_a.Details);
    public string Details    => _a.Details ?? "";

    private bool _isExpanded;
    public bool IsExpanded
    {
        get => _isExpanded;
        set { if (_isExpanded != value) { _isExpanded = value; OnPropertyChanged(); OnPropertyChanged(nameof(ShowDetails)); } }
    }

    public bool ShowDetails => _isExpanded && HasDetails;
}

public sealed class ObservableTaskItem : BindableObject
{
    internal readonly TaskItem Inner;

    public string  Title  => Inner.Title;
    public string? Notes  => Inner.Notes;
    public string  Status => Inner.Status;

    public ObservableTaskItem(TaskItem item) => Inner = item;

    public void Toggle()
    {
        Inner.Status = Inner.Status == "done" ? "pending" : "done";
        OnPropertyChanged(nameof(Status));
    }

    public void UpdateContent(string title, string? notes)
    {
        Inner.Title = title;
        Inner.Notes = notes;
        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(Notes));
    }
}

// ── Gastos ──────────────────────────────────────────────────────────────────

public sealed class ExpenseGroup : List<ExpenseRow>
{
    public string    Name          { get; }
    public string    SubtotalLabel { get; }
    public Color     HeaderColor   { get; }
    public Color     MarkerColor   { get; }
    public Microsoft.Maui.Controls.Shapes.Geometry? Icon { get; }

    public ExpenseGroup(string name, ExpenseCategoryStyle style, string subtotal, List<ExpenseRow> rows)
        : base(rows)
    {
        Name          = name;
        SubtotalLabel = subtotal;
        HeaderColor   = style.TextColor;
        MarkerColor   = style.MarkerColor;
        Icon          = ParseGeometry(style.IconData);
    }

    private static Microsoft.Maui.Controls.Shapes.Geometry? ParseGeometry(string data)
    {
        try { return (Microsoft.Maui.Controls.Shapes.Geometry?)new Microsoft.Maui.Controls.Shapes.PathGeometryConverter().ConvertFromInvariantString(data); }
        catch { return null; }
    }
}

public sealed class ExpenseRow : BindableObject
{
    private static readonly CultureInfo PtBr  = new("pt-BR");
    private static readonly Color PaidGreen   = Color.FromArgb("#3B6D11");
    private static readonly Color Muted        = Color.FromArgb("#9CA3AF");
    private static readonly Color Pending      = Color.FromArgb("#EF4444");

    private readonly ExpenseItem _e;
    private readonly string _baseCurrency;

    public ExpenseRow(ExpenseItem e, string baseCurrency)
    {
        _e = e;
        _baseCurrency = baseCurrency;
    }

    public ExpenseItem Source => _e;

    public string Title    => _e.Title;
    public bool   IsActive => _e.IsActive;

    // Valor na moeda base (ignora o flag ativo para exibição do item)
    public decimal ValueBaseRaw => _e.Subtotal * _e.ExchangeRateToBase;
    public string  ValueLabel   => TripViewModel.FormatCurrency(ValueBaseRaw, _baseCurrency);

    public bool  IsPaid      => _e.IsActive && _e.PaidAmount > 0 && _e.PaidAmount >= _e.SubtotalBase;
    public string StatusLabel => !_e.IsActive ? "inativo" : (IsPaid ? "✓ pago" : "pendente");
    public Color StatusColor  => !_e.IsActive ? Muted : (IsPaid ? PaidGreen : Pending);

    // Detalhes (modo expandido)
    public bool   HasCompany => !string.IsNullOrWhiteSpace(_e.Company);
    public string Company    => _e.Company ?? "";
    public bool   HasNotes   => !string.IsNullOrWhiteSpace(_e.Notes);
    public string Notes      => _e.Notes ?? "";
    public bool   HasLink    => !string.IsNullOrWhiteSpace(_e.Link);
    public string Link       => _e.Link ?? "";

    public string PriceUnitLabel => TripViewModel.FormatCurrency(_e.Price + _e.Taxes, _e.Currency);
    public string PeopleQtyLabel => $"{_e.People} × {_e.Quantity}";
    public bool   ShowExchange   => _e.Currency != _baseCurrency;
    public string ExchangeLabel  => _e.ExchangeRateToBase.ToString("N4", PtBr);
    public string PaidValueLabel => TripViewModel.FormatCurrency(_e.PaidAmount, _baseCurrency);

    private bool _isExpanded;
    public bool IsExpanded
    {
        get => _isExpanded;
        set { if (_isExpanded != value) { _isExpanded = value; OnPropertyChanged(); } }
    }
}

public sealed class ExpenseCategoryStyle
{
    // Ícones Tabler (outline) embutidos como path SVG
    private const string IconBed     = "M5 9a2 2 0 1 0 4 0a2 2 0 1 0 -4 0 M22 17v-3h-20 M2 8v9 M12 14h10v-2a3 3 0 0 0 -3 -3h-7v5";
    private const string IconPlane   = "M16 10h4a2 2 0 0 1 0 4h-4l-4 7h-3l2 -7h-4l-2 2h-3l2 -4l-2 -4h3l2 2h4l-2 -7h3l4 7";
    private const string IconCamera  = "M5 7h1a2 2 0 0 0 2 -2a1 1 0 0 1 1 -1h6a1 1 0 0 1 1 1a2 2 0 0 0 2 2h1a2 2 0 0 1 2 2v9a2 2 0 0 1 -2 2h-14a2 2 0 0 1 -2 -2v-9a2 2 0 0 1 2 -2 M9 13a3 3 0 1 0 6 0a3 3 0 0 0 -6 0";
    private const string IconFork    = "M19 3v12h-5c-.023 -3.681 .184 -7.406 5 -12m0 12v6h-1v-3m-10 -14v17m-3 -17v3a3 3 0 1 0 6 0v-3";
    private const string IconBag     = "M6.331 8h11.339a2 2 0 0 1 1.977 2.304l-1.255 8.152a3 3 0 0 1 -2.966 2.544h-6.852a3 3 0 0 1 -2.965 -2.544l-1.255 -8.152a2 2 0 0 1 1.977 -2.304 M9 11v-5a3 3 0 0 1 6 0v5";
    private const string IconReceipt = "M5 21v-16a2 2 0 0 1 2 -2h10a2 2 0 0 1 2 2v16l-3 -2l-2 2l-2 -2l-2 2l-2 -2l-3 2m4 -14h6m-6 4h6m-2 4h2";

    public Color  MarkerColor { get; init; } = Color.FromArgb("#6B7280");
    public Color  TextColor   { get; init; } = Color.FromArgb("#374151");
    public string IconData    { get; init; } = IconReceipt;

    public static ExpenseCategoryStyle For(string category) => Normalize(category) switch
    {
        "hospedagem" or "hotel" =>
            new() { MarkerColor = Color.FromArgb("#534AB7"), TextColor = Color.FromArgb("#26215C"), IconData = IconBed },
        "transporte" or "transportes" or "voo" or "voos" =>
            new() { MarkerColor = Color.FromArgb("#185FA5"), TextColor = Color.FromArgb("#042C53"), IconData = IconPlane },
        "passeios" or "passeio" or "atividades" or "ingressos" or "passeios e ingressos" =>
            new() { MarkerColor = Color.FromArgb("#0F6E56"), TextColor = Color.FromArgb("#04342C"), IconData = IconCamera },
        "refeicao" or "refeicoes" or "alimentacao" or "comida" =>
            new() { MarkerColor = Color.FromArgb("#854F0B"), TextColor = Color.FromArgb("#412402"), IconData = IconFork },
        "compras" or "shopping" =>
            new() { MarkerColor = Color.FromArgb("#993556"), TextColor = Color.FromArgb("#4B1528"), IconData = IconBag },
        _ => new(),
    };

    private static string Normalize(string s)
    {
        var formD = s.Trim().ToLowerInvariant().Normalize(System.Text.NormalizationForm.FormD);
        var sb = new System.Text.StringBuilder(formD.Length);
        foreach (var ch in formD)
            if (System.Globalization.CharUnicodeInfo.GetUnicodeCategory(ch) != System.Globalization.UnicodeCategory.NonSpacingMark)
                sb.Append(ch);
        return sb.ToString();
    }
}
