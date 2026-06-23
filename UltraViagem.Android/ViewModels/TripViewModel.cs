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
    public List<ItineraryActivity> Activities { get; }

    public NumberedDay(ItineraryDay day, int number, DateOnly? date)
    {
        Label      = $"D{number}";
        DateLabel  = date?.ToString("ddd, dd/MM/yyyy", new CultureInfo("pt-BR")) ?? "";
        Summary    = day.Summary;
        Activities = day.Activities;
    }
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
