using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using UltraViagem.Core;

namespace UltraViagem.App;

public sealed class AppViewModel : NotifyObject
{
    private static readonly CultureInfo Culture = new("pt-BR");

    private Trip? _trip;
    private string _rootPath = "";
    private string? _selectedTripId;
    private TaskEditorViewModel? _selectedTask;
    private LinkEditorViewModel? _selectedTip;
    private AttachmentEditorViewModel? _selectedAttachment;
    private string _statusMessage = "Pronto";
    private string _taskFilter = "all";
    private bool _isCurrentTripFavorite;
    private bool _isLoadingTasks;
    private bool _isLoadingTips;
    private bool _isLoadingAttachments;

    public ObservableCollection<string> TripIds { get; } = [];
    public ObservableCollection<TripSelectionItem> TripSelectionItems { get; } = [];
    public ObservableCollection<ItineraryDayViewModel> Itinerary { get; } = [];
    public ObservableCollection<TaskEditorViewModel> AllTasks { get; } = [];
    public ObservableCollection<TaskEditorViewModel> Tasks { get; } = [];
    public ObservableCollection<LinkEditorViewModel> Tips { get; } = [];
    public ObservableCollection<AttachmentEditorViewModel> Attachments { get; } = [];
    public ObservableCollection<BudgetCategoryViewModel> BudgetCategories { get; } = [];
    public ObservableCollection<string> Places { get; } = [];
    public ObservableCollection<AttachmentEditorViewModel> OverviewFiles { get; } = [];
    public ObservableCollection<LinkEditorViewModel> OverviewTips { get; } = [];

    public string RootPath
    {
        get => _rootPath;
        set => SetField(ref _rootPath, value);
    }

    public string? SelectedTripId
    {
        get => _selectedTripId;
        set => SetField(ref _selectedTripId, value);
    }

    public TaskEditorViewModel? SelectedTask
    {
        get => _selectedTask;
        set => SetField(ref _selectedTask, value);
    }

    public LinkEditorViewModel? SelectedTip
    {
        get => _selectedTip;
        set => SetField(ref _selectedTip, value);
    }

    public AttachmentEditorViewModel? SelectedAttachment
    {
        get => _selectedAttachment;
        set => SetField(ref _selectedAttachment, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetField(ref _statusMessage, value);
    }

    public string TaskFilter
    {
        get => _taskFilter;
        set
        {
            if (SetField(ref _taskFilter, value))
            {
                ApplyTaskFilter();
            }
        }
    }

    public string TripPath => _trip is null ? RootPath : Path.Combine(RootPath, _trip.Id);
    public string TripTitle => _trip?.Title ?? "Nenhuma viagem";
    public string TripSubtitle => _trip is null ? "Selecione uma pasta com viagens." : BuildTripSubtitle(_trip);
    public bool IsCurrentTripFavorite
    {
        get => _isCurrentTripFavorite;
        set
        {
            if (SetField(ref _isCurrentTripFavorite, value))
            {
                OnPropertyChanged(nameof(CurrentTripFavoriteGlyph));
            }
        }
    }

    public string CurrentTripFavoriteGlyph => IsCurrentTripFavorite ? "★" : "☆";
    public string DaysCount => (_trip?.Itinerary.Count ?? 0).ToString(Culture);
    public string PendingTasksCount => AllTasks.Count(task => task.Status == "pending").ToString(Culture);
    public string AttachmentsCount => Attachments.Count.ToString(Culture);
    public string MyMapsUrl
    {
        get => _trip?.MyMapsUrl ?? "";
        set
        {
            if (_trip is null)
            {
                return;
            }

            var normalized = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
            if (_trip.MyMapsUrl == normalized)
            {
                return;
            }

            _trip.MyMapsUrl = normalized;
            OnPropertyChanged(nameof(MyMapsUrl));
            OnPropertyChanged(nameof(HasMyMapsUrl));
        }
    }
    public bool HasMyMapsUrl => LinkEditorViewModel.IsHttpUrl(MyMapsUrl);
    public string BudgetSubtitle => _trip is null ? "" : $"{_trip.Expenses.Count} itens cadastrados em {_trip.BaseCurrency}";
    public string EstimatedTotal => FormatMoney(_trip?.Expenses.Sum(expense => expense.SubtotalBase) ?? 0);
    public string PaidTotal => FormatMoney(_trip?.Expenses.Sum(expense => expense.PaidAmount) ?? 0);
    public string PendingTotal
    {
        get
        {
            var estimated = _trip?.Expenses.Sum(expense => expense.SubtotalBase) ?? 0;
            var paid = _trip?.Expenses.Sum(expense => expense.PaidAmount) ?? 0;
            return FormatMoney(Math.Max(estimated - paid, 0));
        }
    }

    public Trip? CurrentTrip => _trip;
    public event EventHandler? TasksChanged;
    public event EventHandler? TipsChanged;
    public event EventHandler? AttachmentsChanged;

    public void SetTripsRoot(string rootPath, IReadOnlyList<string> tripIds, IReadOnlyList<TripSelectionItem> tripSelectionItems, string? selectedTripId)
    {
        RootPath = rootPath;
        TripIds.ReplaceWith(tripIds);
        TripSelectionItems.ReplaceWith(tripSelectionItems);
        SelectedTripId = selectedTripId;
    }

    public void LoadTrip(Trip? trip)
    {
        _isLoadingTasks = true;
        _isLoadingTips = true;
        _isLoadingAttachments = true;
        foreach (var task in AllTasks)
        {
            task.PropertyChanged -= Task_PropertyChanged;
        }
        foreach (var tip in Tips)
        {
            tip.PropertyChanged -= Tip_PropertyChanged;
        }
        foreach (var attachment in Attachments)
        {
            attachment.PropertyChanged -= Attachment_PropertyChanged;
        }

        _trip = trip;
        IsCurrentTripFavorite = false;

        Itinerary.ReplaceWith(trip?.Itinerary.Select(ItineraryDayViewModel.FromDay) ?? []);
        AllTasks.ReplaceWith(trip?.Tasks.Select(TaskEditorViewModel.FromTask) ?? []);
        Tips.ReplaceWith(trip?.Links.Select(LinkEditorViewModel.FromLink) ?? []);
        Attachments.ReplaceWith(trip?.Attachments.Select(AttachmentEditorViewModel.FromAttachment) ?? []);
        foreach (var task in AllTasks)
        {
            task.PropertyChanged += Task_PropertyChanged;
        }
        foreach (var tip in Tips)
        {
            tip.PropertyChanged += Tip_PropertyChanged;
        }
        foreach (var attachment in Attachments)
        {
            attachment.PropertyChanged += Attachment_PropertyChanged;
        }

        ApplyTaskFilter();
        BudgetCategories.ReplaceWith(BuildBudgetCategories(trip));
        Places.ReplaceWith(trip?.Places.Take(5).Select(place => $"{place.Name} · {place.Type ?? "lugar"}") ?? []);
        OverviewFiles.ReplaceWith(Attachments.Take(4));
        OverviewTips.ReplaceWith(Tips.Take(4));

        SelectedTask = Tasks.FirstOrDefault();
        SelectedTip = Tips.FirstOrDefault();
        SelectedAttachment = Attachments.FirstOrDefault();
        _isLoadingTasks = false;
        _isLoadingTips = false;
        _isLoadingAttachments = false;
        RefreshSummary();
    }

    public void AddTask()
    {
        var task = new TaskEditorViewModel
        {
            Id = $"tarefa-{DateTime.Now:yyyyMMddHHmmss}",
            Title = "Nova tarefa",
            Status = "pending"
        };

        task.PropertyChanged += Task_PropertyChanged;
        AllTasks.Add(task);
        ApplyTaskFilter();
        SelectedTask = task;
        RefreshSummary();
        TasksChanged?.Invoke(this, EventArgs.Empty);
    }

    public void DeleteSelectedTask()
    {
        if (SelectedTask is null)
        {
            return;
        }

        var index = AllTasks.IndexOf(SelectedTask);
        SelectedTask.PropertyChanged -= Task_PropertyChanged;
        AllTasks.Remove(SelectedTask);
        ApplyTaskFilter();
        SelectedTask = Tasks.ElementAtOrDefault(Math.Max(0, index - 1));
        RefreshSummary();
        TasksChanged?.Invoke(this, EventArgs.Empty);
    }

    public void ApplyTasksToTrip()
    {
        if (_trip is null)
        {
            return;
        }

        _trip.Tasks = AllTasks.Select(task => task.ToTaskItem()).ToList();
        RefreshSummary();
    }

    public void AddTip()
    {
        var tip = new LinkEditorViewModel
        {
            Id = $"dica-{DateTime.Now:yyyyMMddHHmmss}",
            Title = "Nova dica",
            Url = "",
            IsEditing = true
        };

        tip.PropertyChanged += Tip_PropertyChanged;
        Tips.Add(tip);
        OverviewTips.ReplaceWith(Tips.Take(4));
        SelectedTip = tip;
        TipsChanged?.Invoke(this, EventArgs.Empty);
    }

    public void DeleteSelectedTip()
    {
        if (SelectedTip is null)
        {
            return;
        }

        var index = Tips.IndexOf(SelectedTip);
        SelectedTip.PropertyChanged -= Tip_PropertyChanged;
        Tips.Remove(SelectedTip);
        OverviewTips.ReplaceWith(Tips.Take(4));
        SelectedTip = Tips.ElementAtOrDefault(Math.Max(0, index - 1));
        TipsChanged?.Invoke(this, EventArgs.Empty);
    }

    public void ApplyTipsToTrip()
    {
        if (_trip is null)
        {
            return;
        }

        _trip.Links = Tips
            .Where(tip => !string.IsNullOrWhiteSpace(tip.Title) || !string.IsNullOrWhiteSpace(tip.Url))
            .Select(tip => tip.ToLinkItem())
            .ToList();
        OverviewTips.ReplaceWith(Tips.Take(4));
    }

    public void AddAttachment(string fileName)
    {
        var attachment = new AttachmentEditorViewModel
        {
            Id = $"arquivo-{DateTime.Now:yyyyMMddHHmmssfff}",
            File = fileName,
            OriginalFile = fileName
        };

        attachment.PropertyChanged += Attachment_PropertyChanged;
        Attachments.Add(attachment);
        OverviewFiles.ReplaceWith(Attachments.Take(4));
        SelectedAttachment = attachment;
        RefreshSummary();
        AttachmentsChanged?.Invoke(this, EventArgs.Empty);
    }

    public void DeleteSelectedAttachment()
    {
        if (SelectedAttachment is null)
        {
            return;
        }

        var index = Attachments.IndexOf(SelectedAttachment);
        SelectedAttachment.PropertyChanged -= Attachment_PropertyChanged;
        Attachments.Remove(SelectedAttachment);
        OverviewFiles.ReplaceWith(Attachments.Take(4));
        SelectedAttachment = Attachments.ElementAtOrDefault(Math.Max(0, index - 1));
        RefreshSummary();
        AttachmentsChanged?.Invoke(this, EventArgs.Empty);
    }

    public void MoveAttachment(AttachmentEditorViewModel attachment, int targetIndex)
    {
        var oldIndex = Attachments.IndexOf(attachment);
        if (oldIndex < 0)
        {
            return;
        }

        var newIndex = Math.Clamp(targetIndex, 0, Math.Max(Attachments.Count - 1, 0));
        if (oldIndex == newIndex)
        {
            return;
        }

        Attachments.Move(oldIndex, newIndex);
        OverviewFiles.ReplaceWith(Attachments.Take(4));
        SelectedAttachment = attachment;
        AttachmentsChanged?.Invoke(this, EventArgs.Empty);
    }

    public void ApplyAttachmentsToTrip()
    {
        if (_trip is null)
        {
            return;
        }

        _trip.Attachments = Attachments
            .Where(attachment => !string.IsNullOrWhiteSpace(attachment.File))
            .Select(attachment => attachment.ToAttachmentItem())
            .ToList();
        OverviewFiles.ReplaceWith(Attachments.Take(4));
        RefreshSummary();
    }

    public void RefreshSummary()
    {
        OnPropertyChanged(nameof(TripPath));
        OnPropertyChanged(nameof(TripTitle));
        OnPropertyChanged(nameof(TripSubtitle));
        OnPropertyChanged(nameof(DaysCount));
        OnPropertyChanged(nameof(PendingTasksCount));
        OnPropertyChanged(nameof(AttachmentsCount));
        OnPropertyChanged(nameof(MyMapsUrl));
        OnPropertyChanged(nameof(HasMyMapsUrl));
        OnPropertyChanged(nameof(BudgetSubtitle));
        OnPropertyChanged(nameof(EstimatedTotal));
        OnPropertyChanged(nameof(PaidTotal));
        OnPropertyChanged(nameof(PendingTotal));
    }

    public void RefreshTripDetails()
    {
        RefreshSummary();
    }

    private void ApplyTaskFilter()
    {
        IEnumerable<TaskEditorViewModel> source = AllTasks;
        source = TaskFilter switch
        {
            "pending" => source.Where(task => task.Status == "pending"),
            "done" => source.Where(task => task.Status == "done"),
            _ => source
        };

        var selectedId = SelectedTask?.Id;
        Tasks.ReplaceWith(source);
        SelectedTask = Tasks.FirstOrDefault(task => task.Id == selectedId) ?? Tasks.FirstOrDefault();
    }

    private void Task_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_isLoadingTasks)
        {
            return;
        }

        if (e.PropertyName is nameof(TaskEditorViewModel.Status))
        {
            ApplyTaskFilter();
        }

        RefreshSummary();
        TasksChanged?.Invoke(this, EventArgs.Empty);
    }

    private void Tip_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_isLoadingTips)
        {
            return;
        }

        OverviewTips.ReplaceWith(Tips.Take(4));
        TipsChanged?.Invoke(this, EventArgs.Empty);
    }

    private void Attachment_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_isLoadingAttachments)
        {
            return;
        }

        OverviewFiles.ReplaceWith(Attachments.Take(4));
        RefreshSummary();
        AttachmentsChanged?.Invoke(this, EventArgs.Empty);
    }

    private static IReadOnlyList<BudgetCategoryViewModel> BuildBudgetCategories(Trip? trip)
    {
        if (trip is null)
        {
            return [];
        }

        var totals = trip.Expenses
            .GroupBy(expense => string.IsNullOrWhiteSpace(expense.Type) ? "Outros" : expense.Type)
            .Select(group => new
            {
                Name = group.Key ?? "Outros",
                Total = group.Sum(expense => expense.SubtotalBase)
            })
            .ToList();
        var max = totals.Select(total => total.Total).DefaultIfEmpty(0).Max();

        return totals
            .Select(total => new BudgetCategoryViewModel(
                total.Name,
                FormatMoney(total.Total),
                max == 0 ? 0 : Math.Max(8, (double)(total.Total / max) * 220)))
            .OrderByDescending(category => category.BarWidth)
            .ToList();
    }

    private static string BuildTripSubtitle(Trip trip)
    {
        if (trip.StartDate is null || trip.EndDate is null)
        {
            return "Datas a definir";
        }

        var days = Math.Max(1, trip.EndDate.Value.DayNumber - trip.StartDate.Value.DayNumber + 1);
        var dayLabel = days == 1 ? "1 dia" : $"{days} dias";
        return $"{trip.StartDate:dd MMM yyyy} - {trip.EndDate:dd MMM yyyy} ({dayLabel})";
    }

    private static string FormatMoney(decimal value)
    {
        return value.ToString("C", Culture);
    }
}

public sealed class TaskEditorViewModel : NotifyObject
{
    private string _id = "";
    private string _title = "";
    private string _status = "pending";
    private string _notes = "";

    public string Id { get => _id; set => SetField(ref _id, value); }
    public string Title { get => _title; set => SetField(ref _title, value); }
    public string Status
    {
        get => _status;
        set => SetField(ref _status, value == "done" ? "done" : "pending");
    }
    public string Notes { get => _notes; set => SetField(ref _notes, value); }
    public string? RelatedDayId { get; set; }
    public string? RelatedExpenseId { get; set; }
    public string? RelatedPlaceId { get; set; }
    public string? RelatedAttachment { get; set; }
    public bool IsDone
    {
        get => Status == "done";
        set => Status = value ? "done" : "pending";
    }
    public string StatusLabel => Status switch
    {
        "done" => "concluída",
        _ => "pendente"
    };

    public static TaskEditorViewModel FromTask(TaskItem task)
    {
        return new TaskEditorViewModel
        {
            Id = task.Id,
            Title = task.Title,
            Status = task.Status == "done" ? "done" : "pending",
            Notes = task.Notes ?? "",
            RelatedDayId = task.RelatedDayId,
            RelatedExpenseId = task.RelatedExpenseId,
            RelatedPlaceId = task.RelatedPlaceId,
            RelatedAttachment = task.RelatedAttachment
        };
    }

    public TaskItem ToTaskItem()
    {
        return new TaskItem
        {
            Id = string.IsNullOrWhiteSpace(Id) ? $"tarefa-{Guid.NewGuid():N}" : Id.Trim(),
            Title = Title.Trim(),
            Status = Status == "done" ? "done" : "pending",
            Notes = string.IsNullOrWhiteSpace(Notes) ? null : Notes.Trim(),
            RelatedDayId = RelatedDayId,
            RelatedExpenseId = RelatedExpenseId,
            RelatedPlaceId = RelatedPlaceId,
            RelatedAttachment = RelatedAttachment
        };
    }

    protected override void OnPropertyChanged(string propertyName)
    {
        base.OnPropertyChanged(propertyName);
        if (propertyName is nameof(Status))
        {
            base.OnPropertyChanged(nameof(IsDone));
            base.OnPropertyChanged(nameof(StatusLabel));
        }
    }
}

public sealed class LinkEditorViewModel : NotifyObject
{
    private string _id = "";
    private string _title = "";
    private string _url = "";
    private bool _isEditing;

    public string Id { get => _id; set => SetField(ref _id, value); }
    public string Title { get => _title; set => SetField(ref _title, value); }
    public string Url { get => _url; set => SetField(ref _url, value); }
    public bool IsEditing { get => _isEditing; set => SetField(ref _isEditing, value); }
    public bool HasValidUrl => IsHttpUrl(Url);

    public static LinkEditorViewModel FromLink(LinkItem link)
    {
        return new LinkEditorViewModel
        {
            Id = link.Id,
            Title = link.Title,
            Url = link.Url
        };
    }

    public LinkItem ToLinkItem()
    {
        return new LinkItem
        {
            Id = string.IsNullOrWhiteSpace(Id) ? $"dica-{Guid.NewGuid():N}" : Id.Trim(),
            Title = Title.Trim(),
            Url = Url.Trim()
        };
    }

    protected override void OnPropertyChanged(string propertyName)
    {
        base.OnPropertyChanged(propertyName);
        if (propertyName is nameof(Url))
        {
            base.OnPropertyChanged(nameof(HasValidUrl));
        }
    }

    public static bool IsHttpUrl(string value)
    {
        return Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
               (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
    }
}

public sealed class AttachmentEditorViewModel : NotifyObject
{
    private string _id = "";
    private string _file = "";
    private string _originalFile = "";
    private bool _isEditing;

    public string Id { get => _id; set => SetField(ref _id, value); }
    public string File { get => _file; set => SetField(ref _file, value); }
    public string OriginalFile { get => _originalFile; set => SetField(ref _originalFile, value); }
    public bool IsEditing { get => _isEditing; set => SetField(ref _isEditing, value); }

    public static AttachmentEditorViewModel FromAttachment(AttachmentItem attachment)
    {
        var file = attachment.File;
        return new AttachmentEditorViewModel
        {
            Id = attachment.Id,
            File = file,
            OriginalFile = file
        };
    }

    public AttachmentItem ToAttachmentItem()
    {
        var file = Path.GetFileName(File.Trim());
        return new AttachmentItem
        {
            Id = string.IsNullOrWhiteSpace(Id) ? $"arquivo-{Guid.NewGuid():N}" : Id.Trim(),
            Title = file,
            File = file
        };
    }
}

public sealed record ItineraryDayViewModel(
    string DateLabel,
    string Title,
    IReadOnlyList<string> Blocks,
    string OvernightLabel)
{
    public static ItineraryDayViewModel FromDay(ItineraryDay day)
    {
        var date = day.Date?.ToString("dd MMM", CultureInfo.GetCultureInfo("pt-BR")) ?? "sem data";
        var blocks = day.Blocks.Select(block => $"{block.Period}: {block.Text}").ToList();
        var overnight = string.IsNullOrWhiteSpace(day.Overnight) ? "" : $"Pernoite: {day.Overnight}";
        return new ItineraryDayViewModel(date, day.Title, blocks, overnight);
    }
}

public sealed record BudgetCategoryViewModel(string Name, string TotalLabel, double BarWidth);

public sealed class TripSelectionItem
{
    public TripSelectionItem(string id, string title, int year, string dateLabel, bool isFavorite)
    {
        Id = id;
        Title = title;
        Year = year;
        DateLabel = dateLabel;
        IsFavorite = isFavorite;
    }

    public string Id { get; }
    public string Title { get; }
    public int Year { get; }
    public string DateLabel { get; }
    public bool IsFavorite { get; set; }
}

public abstract class NotifyObject : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected bool SetField<T>(ref T field, T value, [CallerMemberName] string propertyName = "")
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    protected virtual void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

public static class ObservableCollectionExtensions
{
    public static void ReplaceWith<T>(this ObservableCollection<T> collection, IEnumerable<T> items)
    {
        collection.Clear();
        foreach (var item in items)
        {
            collection.Add(item);
        }
    }
}
