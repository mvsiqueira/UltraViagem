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
    private static readonly string[] BudgetCategoryColors =
    [
        "#0F766E",
        "#2563EB",
        "#F59E0B",
        "#DC2626",
        "#7C3AED",
        "#16A34A",
        "#DB2777",
        "#0891B2",
        "#EA580C"
    ];

    private Trip? _trip;
    private string _rootPath = "";
    private string? _selectedTripId;
    private TaskEditorViewModel? _selectedTask;
    private LinkEditorViewModel? _selectedTip;
    private AttachmentEditorViewModel? _selectedAttachment;
    private ExpenseEditorViewModel? _selectedExpense;
    private ItineraryActivityViewModel? _selectedActivity;
    private record ActivityStyle(string Color, string Icon, string Type);
    private record ActivitySnapshot(
        string Title, string Type, string Color, string Icon,
        string? Details, string? AdditionalData, int DurationSlots, int StartSlot);
    private ActivityStyle?   _styleClipboard;
    private ActivitySnapshot? _activityClipboard;
    private string? _tripFolderPath;
    private string _statusMessage = "Pronto";
    private string _taskFilter = "all";
    private bool _isCurrentTripFavorite;
    private bool _isSidebarExpanded = true;
    private bool _isBankExpanded = true;
    private bool _showItineraryGrid;
    private string _activeVersionId = "";
    private bool _isLoadingTasks;
    private bool _isLoadingTips;
    private bool _isLoadingAttachments;
    private bool _isLoadingExpenses;
    private double _itinerarySlotWidth = 44;

    public ObservableCollection<string> TripIds { get; } = [];
    public ObservableCollection<TripSelectionItem> TripSelectionItems { get; } = [];
    public ObservableCollection<ItineraryDayViewModel> Itinerary { get; } = [];
    public ObservableCollection<BankRowViewModel> BankRows { get; } = [];
    public ObservableCollection<ItineraryVersionTabViewModel> VersionTabs { get; } = [];
    public ObservableCollection<TaskEditorViewModel> AllTasks { get; } = [];
    public ObservableCollection<TaskEditorViewModel> Tasks { get; } = [];
    public ObservableCollection<LinkEditorViewModel> Tips { get; } = [];
    public ObservableCollection<AttachmentEditorViewModel> Attachments { get; } = [];
    public ObservableCollection<ExpenseEditorViewModel> Expenses { get; } = [];
    public ObservableCollection<ExpenseCategoryGroupViewModel> ExpenseGroups { get; } = [];
    public ObservableCollection<CurrencyRateViewModel> CurrencyRates { get; } = [];
    public ObservableCollection<BudgetCategoryViewModel> BudgetCategories { get; } = [];
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
        set
        {
            if (ReferenceEquals(_selectedTask, value))
            {
                return;
            }

            if (_selectedTask is not null)
            {
                _selectedTask.IsSelectedForEdit = false;
            }

            if (SetField(ref _selectedTask, value) && _selectedTask is not null)
            {
                _selectedTask.IsSelectedForEdit = true;
            }
        }
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

    public ExpenseEditorViewModel? SelectedExpense
    {
        get => _selectedExpense;
        set
        {
            if (ReferenceEquals(_selectedExpense, value))
            {
                return;
            }

            if (_selectedExpense is not null)
            {
                _selectedExpense.IsSelectedForEdit = false;
            }

            if (SetField(ref _selectedExpense, value))
            {
                if (_selectedExpense is not null)
                {
                    _selectedExpense.IsSelectedForEdit = true;
                }

                OnPropertyChanged(nameof(HasSelectedExpense));
            }
        }
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

    public string TripPath => _tripFolderPath ?? RootPath;
    public string TripTitle => _trip?.Title ?? "Nenhuma viagem";
    public string TripSubtitle => _trip is null ? "Selecione uma pasta com viagens." : BuildTripSubtitle(_trip);
    public bool IsSidebarExpanded
    {
        get => _isSidebarExpanded;
        set => SetField(ref _isSidebarExpanded, value);
    }

    public bool IsBankExpanded
    {
        get => _isBankExpanded;
        set => SetField(ref _isBankExpanded, value);
    }

    public string ActiveVersionId
    {
        get => _activeVersionId;
        private set => SetField(ref _activeVersionId, value);
    }

    public bool ShowItineraryGrid
    {
        get => _showItineraryGrid;
        set
        {
            if (SetField(ref _showItineraryGrid, value) && _trip is not null)
                _trip.ShowItineraryGrid = value;
        }
    }

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
    public string DaysCount => Itinerary.Count.ToString(Culture);
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

    public string BudgetSubtitle => _trip is null ? "" : $"{Expenses.Count} itens cadastrados em {_trip.BaseCurrency}";
    public string BaseCurrencyCode => NormalizeCurrency(_trip?.BaseCurrency ?? "BRL");
    public string BaseCurrencySymbol => CurrencyRates.FirstOrDefault(rate => string.Equals(rate.Currency, BaseCurrencyCode, StringComparison.OrdinalIgnoreCase))?.Symbol ?? BaseCurrencyCode;
    public string EstimatedTotal => FormatMoney(Expenses.Sum(expense => expense.SubtotalBase));
    public string PaidTotal => FormatMoney(Expenses.Where(expense => expense.IsActive).Sum(expense => expense.PaidAmount));
    public string PendingTotal
    {
        get
        {
            var estimated = Expenses.Sum(expense => expense.SubtotalBase);
            var paid = Expenses.Where(expense => expense.IsActive).Sum(expense => expense.PaidAmount);
            return FormatMoney(Math.Max(estimated - paid, 0));
        }
    }
    public string ActiveExpensesCount => Expenses.Count(expense => expense.IsActive).ToString(Culture);
    public bool HasSelectedExpense => SelectedExpense is not null;

    public ItineraryActivityViewModel? SelectedActivity
    {
        get => _selectedActivity;
        set
        {
            if (ReferenceEquals(_selectedActivity, value)) return;
            if (_selectedActivity is not null) _selectedActivity.IsSelected = false;
            if (SetField(ref _selectedActivity, value) && _selectedActivity is not null)
            {
                _selectedActivity.IsSelected = true;
                ClearSlotSelection(); // selecionar atividade limpa a seleção de slot
            }
            OnPropertyChanged(nameof(HasSelectedActivity));
        }
    }

    public void SelectEmptySlot(ItineraryDayViewModel day, int slot)
    {
        SelectedActivity = null;
        foreach (var d in Itinerary) d.SelectedSlot = d == day ? slot : -1;
        foreach (var r in BankRows) r.SelectedSlot = -1;
    }

    public void SelectEmptySlot(BankRowViewModel bankRow, int slot)
    {
        SelectedActivity = null;
        foreach (var d in Itinerary) d.SelectedSlot = -1;
        foreach (var r in BankRows) r.SelectedSlot = r == bankRow ? slot : -1;
    }

    public void ClearSlotSelection()
    {
        foreach (var d in Itinerary) d.SelectedSlot = -1;
        foreach (var r in BankRows) r.SelectedSlot = -1;
    }

    public bool HasSelectedActivity => SelectedActivity is not null;
    public bool HasStyleClipboard    => _styleClipboard    is not null;
    public bool HasActivityClipboard => _activityClipboard is not null;

    public double ItinerarySlotWidth
    {
        get => _itinerarySlotWidth;
        set
        {
            var clamped = Math.Max(value, 20.0);
            if (_itinerarySlotWidth == clamped) return;
            _itinerarySlotWidth = clamped;
            ItineraryActivityViewModel.Configure(clamped);
            ItineraryDayViewModel.Configure(ItinerarySlotsPerDay, clamped);
            BankRowViewModel.Configure(ItinerarySlotsPerDay, clamped);
            foreach (var day in Itinerary) day.NotifyLayoutChanged();
            foreach (var row in BankRows) row.NotifyLayoutChanged();
            OnPropertyChanged(nameof(ItinerarySlotWidth));
        }
    }

    public int ItinerarySlotsPerDay => _trip?.ItinerarySlotsPerDay ?? 16;

    public int ItineraryBlockHeight
    {
        get => _trip?.ItineraryBlockHeight ?? 44;
        set
        {
            if (_trip is null) return;
            var clamped = Math.Clamp(value, 20, 120);
            if (_trip.ItineraryBlockHeight == clamped) return;
            _trip.ItineraryBlockHeight = clamped;
            ItineraryDayViewModel.ConfigureBlockHeight(clamped);
            ItineraryActivityViewModel.ConfigureBlockHeight(clamped);
            BankRowViewModel.ConfigureBlockHeight(clamped);
            foreach (var day in Itinerary) day.NotifyLayoutChanged();
            foreach (var row in BankRows) row.NotifyLayoutChanged();
            OnPropertyChanged(nameof(ItineraryBlockHeight));
        }
    }

    public int ItineraryFontSize
    {
        get => _trip?.ItineraryFontSize ?? 11;
        set
        {
            if (_trip is null) return;
            var clamped = Math.Clamp(value, 7, 22);
            if (_trip.ItineraryFontSize == clamped) return;
            _trip.ItineraryFontSize = clamped;
            ItineraryActivityViewModel.ConfigureFontSize(clamped);
            foreach (var day in Itinerary) day.NotifyLayoutChanged();
            OnPropertyChanged(nameof(ItineraryFontSize));
        }
    }

    public Trip? CurrentTrip => _trip;
    public event EventHandler? TasksChanged;
    public event EventHandler? TipsChanged;
    public event EventHandler? AttachmentsChanged;
    public event EventHandler? ExpensesChanged;
    public event EventHandler? ItineraryChanged;

    public void SetTripsRoot(string rootPath, IReadOnlyList<string> tripIds, IReadOnlyList<TripSelectionItem> tripSelectionItems, string? selectedTripId)
    {
        RootPath = rootPath;
        TripIds.ReplaceWith(tripIds);
        TripSelectionItems.ReplaceWith(tripSelectionItems);
        SelectedTripId = selectedTripId;
    }

    public void LoadTrip(Trip? trip, string? folderPath = null)
    {
        _tripFolderPath = folderPath;
        _isLoadingTasks = true;
        _isLoadingTips = true;
        _isLoadingAttachments = true;
        _isLoadingExpenses = true;
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
        foreach (var expense in Expenses)
        {
            expense.PropertyChanged -= Expense_PropertyChanged;
        }
        foreach (var rate in CurrencyRates)
        {
            rate.PropertyChanged -= CurrencyRate_PropertyChanged;
        }

        _trip = trip;
        IsCurrentTripFavorite = false;

        var slotsPerDay = trip?.ItinerarySlotsPerDay ?? 16;
        var blockHeight = trip?.ItineraryBlockHeight ?? 44;
        var fontSize = trip?.ItineraryFontSize ?? 11;
        ItineraryDayViewModel.Configure(slotsPerDay, _itinerarySlotWidth);
        ItineraryDayViewModel.ConfigureBlockHeight(blockHeight);
        BankRowViewModel.Configure(slotsPerDay, _itinerarySlotWidth);
        BankRowViewModel.ConfigureBlockHeight(blockHeight);
        ItineraryActivityViewModel.Configure(_itinerarySlotWidth);
        ItineraryActivityViewModel.ConfigureBlockHeight(blockHeight);
        ItineraryActivityViewModel.ConfigureFontSize(fontSize);
        ItineraryActivityViewModel.ConfigureSlotsPerDay(slotsPerDay);
        _showItineraryGrid = trip?.ShowItineraryGrid ?? false;
        OnPropertyChanged(nameof(ShowItineraryGrid));

        var activeVersionId = trip?.ActiveVersionId ?? "";
        var activeVersion = trip?.ItineraryVersions?.FirstOrDefault(v => v.Id == activeVersionId)
                            ?? trip?.ItineraryVersions?.FirstOrDefault();

        VersionTabs.ReplaceWith(trip?.ItineraryVersions?.Select(v => new ItineraryVersionTabViewModel
        {
            Id = v.Id,
            Name = v.Name,
            IsActive = v.Id == activeVersion?.Id
        }) ?? []);

        _activeVersionId = activeVersion?.Id ?? "";
        OnPropertyChanged(nameof(ActiveVersionId));

        ItineraryDayViewModel.ConfigureStartDate(_trip?.StartDate ?? DateOnly.MinValue);
        Itinerary.ReplaceWith((activeVersion?.Itinerary ?? []).Select(ItineraryDayViewModel.FromDay));
        RefreshDayNumbers();

        var bankRowCount = activeVersion?.BankRows ?? 2;
        BankRows.Clear();
        for (int i = 0; i < Math.Max(1, bankRowCount); i++)
        {
            var row = new BankRowViewModel { RowIndex = i };
            foreach (var a in (activeVersion?.BankActivities ?? []).Where(a => a.BankRow == i))
                row.Activities.Add(ItineraryActivityViewModel.FromActivity(a));
            BankRows.Add(row);
        }
        AllTasks.ReplaceWith(trip?.Tasks.Select(TaskEditorViewModel.FromTask) ?? []);
        Tips.ReplaceWith(trip?.Links.Select(LinkEditorViewModel.FromLink) ?? []);
        Attachments.ReplaceWith(trip?.Attachments.Select(AttachmentEditorViewModel.FromAttachment) ?? []);
        Expenses.ReplaceWith(trip?.Expenses.Select(ExpenseEditorViewModel.FromExpense) ?? []);
        CurrencyRates.ReplaceWith(BuildCurrencyRates(trip));
        OnPropertyChanged(nameof(BaseCurrencyCode));
        OnPropertyChanged(nameof(BaseCurrencySymbol));
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
        foreach (var expense in Expenses)
        {
            expense.PropertyChanged += Expense_PropertyChanged;
        }
        foreach (var rate in CurrencyRates)
        {
            rate.PropertyChanged += CurrencyRate_PropertyChanged;
        }
        var rateDecimalDigits = trip?.RateDecimalDigits ?? 2;
        ExpenseEditorViewModel.Configure(CurrencyRates, BaseCurrencyCode, rateDecimalDigits);
        CurrencyRateViewModel.ConfigureRateDecimalDigits(rateDecimalDigits);

        ApplyTaskFilter();
        BudgetCategories.ReplaceWith(BuildBudgetCategories(Expenses));
        ExpenseGroups.ReplaceWith(BuildExpenseGroups(Expenses));
        OverviewFiles.ReplaceWith(Attachments.Take(4));
        OverviewTips.ReplaceWith(Tips.Take(4));

        SelectedTask = Tasks.FirstOrDefault();
        SelectedTip = Tips.FirstOrDefault();
        SelectedAttachment = Attachments.FirstOrDefault();
        SelectedExpense = Expenses.FirstOrDefault();
        _isLoadingTasks = false;
        _isLoadingTips = false;
        _isLoadingAttachments = false;
        _isLoadingExpenses = false;
        RefreshSummary();

        StartLoadingCoverImages();
    }

    /// <summary>Inicia o carregamento assíncrono de imagens de capa para os cards do roteiro.</summary>
    public void StartLoadingCoverImages()
    {
        if (string.IsNullOrEmpty(TripPath)) return;
        var cacheDir = Path.Combine(TripPath, ".cache");
        foreach (var day in Itinerary)
            _ = day.LoadCoverImageAsync(cacheDir);
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

    public void MoveTask(TaskEditorViewModel task, int targetIndexInTasks)
    {
        var targetTask = targetIndexInTasks >= 0 && targetIndexInTasks < Tasks.Count
            ? Tasks[targetIndexInTasks]
            : null;

        if (targetTask is null || ReferenceEquals(targetTask, task))
        {
            return;
        }

        var oldIndexInAll = AllTasks.IndexOf(task);
        var targetIndexInAll = AllTasks.IndexOf(targetTask);

        if (oldIndexInAll < 0 || targetIndexInAll < 0 || oldIndexInAll == targetIndexInAll)
        {
            return;
        }

        AllTasks.Move(oldIndexInAll, targetIndexInAll);
        ApplyTaskFilter();
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

    public void MoveTip(LinkEditorViewModel tip, int targetIndex)
    {
        var oldIndex = Tips.IndexOf(tip);
        if (oldIndex < 0) return;

        var newIndex = Math.Clamp(targetIndex, 0, Math.Max(Tips.Count - 1, 0));
        if (oldIndex == newIndex) return;

        Tips.Move(oldIndex, newIndex);
        OverviewTips.ReplaceWith(Tips.Take(4));
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

    /// <summary>Remove vários anexos da lista sem disparar autosave. Chamador é responsável por salvar.</summary>
    public void RemoveAttachmentsSilent(IEnumerable<AttachmentEditorViewModel> toRemove)
    {
        foreach (var attachment in toRemove.ToList())
        {
            attachment.PropertyChanged -= Attachment_PropertyChanged;
            Attachments.Remove(attachment);
        }
        OverviewFiles.ReplaceWith(Attachments.Take(4));
        if (SelectedAttachment is not null && !Attachments.Contains(SelectedAttachment))
            SelectedAttachment = Attachments.FirstOrDefault();
        RefreshSummary();
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


    public void AddItineraryDay()
    {
        var vm = new ItineraryDayViewModel { Id = $"dia-{Guid.NewGuid():N}" };
        Itinerary.Add(vm);
        RefreshDayNumbers();
        RefreshSummary();
        ItineraryChanged?.Invoke(this, EventArgs.Empty);
    }

    public void RemoveItineraryDay(ItineraryDayViewModel day)
    {
        if (SelectedActivity is not null && day.Activities.Contains(SelectedActivity))
            SelectedActivity = null;
        Itinerary.Remove(day);
        RefreshDayNumbers();
        RefreshSummary();
        ItineraryChanged?.Invoke(this, EventArgs.Empty);
    }

    public void AddActivity(ItineraryDayViewModel day)
    {
        var vm = new ItineraryActivityViewModel
        {
            Id = $"act-{Guid.NewGuid():N}",
            Title = "Nova atividade",
            Color = "#DBEAFE",
            StartSlot = 0,
            DurationSlots = 2
        };
        day.Activities.Add(vm);
        SelectedActivity = vm;
        ItineraryChanged?.Invoke(this, EventArgs.Empty);
    }

    public void RemoveSelectedActivity()
    {
        if (SelectedActivity is null) return;
        foreach (var day in Itinerary)
        {
            if (day.Activities.Remove(SelectedActivity))
            {
                SelectedActivity = null;
                ItineraryChanged?.Invoke(this, EventArgs.Empty);
                return;
            }
        }
        foreach (var row in BankRows)
        {
            if (row.Activities.Remove(SelectedActivity))
            {
                SelectedActivity = null;
                ItineraryChanged?.Invoke(this, EventArgs.Empty);
                return;
            }
        }
    }

    public void ApplyItineraryToTrip()
    {
        if (_trip is null) return;

        var days = Itinerary.Select(d => d.ToDay()).ToList();
        var bankRowCount = BankRows.Count;
        var bankActivities = BankRows
            .SelectMany(row => row.Activities.Select(a =>
            {
                var act = a.ToActivity();
                act.BankRow = row.RowIndex;
                return act;
            }))
            .ToList();

        // Save to active version
        var activeVersion = _trip.ItineraryVersions?.FirstOrDefault(v => v.Id == _activeVersionId);
        if (activeVersion is not null)
        {
            activeVersion.Itinerary = days;
            activeVersion.BankRows = bankRowCount;
            activeVersion.BankActivities = bankActivities;
        }

        _trip.ActiveVersionId = _activeVersionId;

        RefreshSummary();
    }

    // ── Version management ──────────────────────────────────────────────────

    public void AddVersion(bool duplicateCurrent = false)
    {
        if (_trip is null) return;
        ApplyItineraryToTrip();

        var number = (_trip.ItineraryVersions?.Count ?? 0) + 1;
        var source = duplicateCurrent
            ? _trip.ItineraryVersions?.FirstOrDefault(v => v.Id == _activeVersionId)
            : null;

        var newVersion = new ItineraryVersion
        {
            Id = $"v-{Guid.NewGuid():N}",
            Name = $"Versão {number}",
            Itinerary = source?.Itinerary.Select(CloneDay).ToList() ?? [],
            BankRows = source?.BankRows ?? 2,
            BankActivities = source?.BankActivities.Select(CloneActivity).ToList() ?? []
        };

        (_trip.ItineraryVersions ??= []).Add(newVersion);
        VersionTabs.Add(new ItineraryVersionTabViewModel { Id = newVersion.Id, Name = newVersion.Name });
        SwitchToVersion(newVersion.Id);
    }

    public void ShiftItineraryStartDate(DateOnly newStartDate)
    {
        if (_trip is not null) _trip.StartDate = newStartDate;
        ItineraryDayViewModel.ConfigureStartDate(newStartDate);
        foreach (var day in Itinerary) day.NotifyDateChanged();
    }

    public void SwitchToVersion(string id)
    {
        if (_trip is null) return;

        // Save current version to memory
        ApplyItineraryToTrip();

        var version = _trip.ItineraryVersions?.FirstOrDefault(v => v.Id == id);
        if (version is null) return;

        _trip.ActiveVersionId = id;
        _activeVersionId = id;
        OnPropertyChanged(nameof(ActiveVersionId));

        foreach (var tab in VersionTabs)
            tab.IsActive = tab.Id == id;

        // Close any open edits
        SelectedActivity = null;
        ClearAllActivityDims();
        ClearAllDayDims();
        ClearSlotSelection();
        foreach (var day in Itinerary) { day.RejectEdit(); day.RejectDayEdit(); }
        foreach (var row in BankRows) row.RejectEdit();

        ItineraryDayViewModel.ConfigureStartDate(_trip?.StartDate ?? DateOnly.MinValue);
        Itinerary.ReplaceWith(version.Itinerary.Select(ItineraryDayViewModel.FromDay));
        RefreshDayNumbers();

        BankRows.Clear();
        for (int i = 0; i < Math.Max(1, version.BankRows); i++)
        {
            var row = new BankRowViewModel { RowIndex = i };
            foreach (var a in version.BankActivities.Where(a => a.BankRow == i))
                row.Activities.Add(ItineraryActivityViewModel.FromActivity(a));
            BankRows.Add(row);
        }

        RefreshSummary();
        StartLoadingCoverImages();
    }

    public bool RenameVersion(string id, string newName)
    {
        if (_trip is null || string.IsNullOrWhiteSpace(newName)) return false;
        var version = _trip.ItineraryVersions?.FirstOrDefault(v => v.Id == id);
        if (version is null) return false;
        version.Name = newName.Trim();
        var tab = VersionTabs.FirstOrDefault(t => t.Id == id);
        if (tab is not null) tab.Name = newName.Trim();
        return true;
    }

    public bool DeleteVersion(string id, out string switchedToId)
    {
        switchedToId = _activeVersionId;
        if (_trip is null || (_trip.ItineraryVersions?.Count ?? 0) <= 1) return false;
        var idx = _trip.ItineraryVersions!.FindIndex(v => v.Id == id);
        if (idx < 0) return false;

        _trip.ItineraryVersions.RemoveAt(idx);
        var tabToRemove = VersionTabs.FirstOrDefault(t => t.Id == id);
        if (tabToRemove is not null) VersionTabs.Remove(tabToRemove);

        if (_activeVersionId == id)
        {
            var nextVersion = _trip.ItineraryVersions[Math.Max(0, idx - 1)];
            SwitchToVersion(nextVersion.Id);
            switchedToId = nextVersion.Id;
        }
        return true;
    }

    private static ItineraryActivity CloneActivity(ItineraryActivity a) => new()
    {
        Id = $"act-{Guid.NewGuid():N}",
        Title = a.Title,
        Type = a.Type,
        Color = a.Color,
        Icon = a.Icon,
        StartSlot = a.StartSlot,
        DurationSlots = a.DurationSlots,
        BankRow = a.BankRow,
        Details = a.Details,
        AdditionalData = a.AdditionalData
    };

    private static ItineraryDay CloneDay(ItineraryDay d) => new()
    {
        Id = $"dia-{Guid.NewGuid():N}",
        Summary = d.Summary,
        Activities = d.Activities.Select(CloneActivity).ToList()
    };

    public ItineraryDayViewModel? FindDayForActivity(ItineraryActivityViewModel activity)
        => Itinerary.FirstOrDefault(d => d.Activities.Contains(activity));

    public void ClearAllActivityDims()
    {
        foreach (var day in Itinerary)
            foreach (var a in day.Activities)
                a.IsDimmed = false;
        foreach (var row in BankRows)
            foreach (var a in row.Activities)
                a.IsDimmed = false;
    }

    public void SetDayFocus(ItineraryDayViewModel focusedDay)
    {
        foreach (var day in Itinerary)
            day.IsDimmed = day != focusedDay;
    }

    public void ClearAllDayDims()
    {
        foreach (var day in Itinerary)
            day.IsDimmed = false;
    }

    public BankRowViewModel? FindBankRowForActivity(ItineraryActivityViewModel activity)
        => BankRows.FirstOrDefault(r => r.Activities.Contains(activity));

    public void AddBankRow()
    {
        var row = new BankRowViewModel { RowIndex = BankRows.Count };
        BankRows.Add(row);
        ItineraryChanged?.Invoke(this, EventArgs.Empty);
    }

    public void RemoveBankRow()
    {
        if (BankRows.Count <= 1) return;
        var last = BankRows[^1];
        var prev = BankRows[^2];
        foreach (var a in last.Activities.ToList())
            prev.Activities.Add(a);
        BankRows.RemoveAt(BankRows.Count - 1);
        ItineraryChanged?.Invoke(this, EventArgs.Empty);
    }

    public void AddBankActivity(BankRowViewModel? targetRow = null)
    {
        var row = targetRow ?? (BankRows.Count > 0 ? BankRows[0] : null);
        if (row is null) return;
        var vm = new ItineraryActivityViewModel
        {
            Id = $"act-{Guid.NewGuid():N}",
            Title = "Nova atividade",
            Color = "#DBEAFE",
            StartSlot = 0,
            DurationSlots = 2
        };
        row.Activities.Add(vm);
        SelectedActivity = vm;
        ItineraryChanged?.Invoke(this, EventArgs.Empty);
    }

    public void CopySelectedActivity()
    {
        if (SelectedActivity is null) return;
        var source = SelectedActivity;
        var day = FindDayForActivity(source);
        var bankRow = day is null ? FindBankRowForActivity(source) : null;
        if (day is null && bankRow is null) return;

        var copy = new ItineraryActivityViewModel
        {
            Id = $"act-{Guid.NewGuid():N}",
            Title = source.Title,
            Type = source.Type,
            Color = source.Color,
            Icon = source.Icon,
            Details = source.Details,
            AdditionalData = source.AdditionalData,
            DurationSlots = source.DurationSlots,
            StartSlot = Math.Clamp(source.StartSlot + source.DurationSlots, 0, ItinerarySlotsPerDay - source.DurationSlots)
        };
        if (day is not null) day.Activities.Add(copy);
        else bankRow!.Activities.Add(copy);
        SelectedActivity = copy;
        ItineraryChanged?.Invoke(this, EventArgs.Empty);
    }

    // Copia estilo do formulário de edição (usa Edit* em andamento)
    public void CopyStyle(ItineraryActivityViewModel source)
    {
        _styleClipboard = new ActivityStyle(source.EditColor, source.EditIcon, source.EditType);
        OnPropertyChanged(nameof(HasStyleClipboard));
    }

    // Copia estilo de um bloco selecionado fora de edição (usa valores committed)
    public void CopyStyleFromSelected()
    {
        if (SelectedActivity is null) return;
        _styleClipboard = new ActivityStyle(SelectedActivity.Color, SelectedActivity.Icon, SelectedActivity.Type);
        OnPropertyChanged(nameof(HasStyleClipboard));
    }

    // Cola estilo no formulário de edição (atualiza Edit* para refletir no form)
    public void PasteStyle(ItineraryActivityViewModel target)
    {
        if (_styleClipboard is null) return;
        target.EditColor = _styleClipboard.Color;
        target.EditIcon = _styleClipboard.Icon;
        target.EditType = _styleClipboard.Type;
    }

    // Cola estilo diretamente num bloco selecionado fora de edição (committed)
    public void PasteStyleToSelected()
    {
        if (_styleClipboard is null || SelectedActivity is null) return;
        SelectedActivity.Color = _styleClipboard.Color;
        SelectedActivity.Icon = _styleClipboard.Icon;
        SelectedActivity.Type = _styleClipboard.Type;
        ItineraryChanged?.Invoke(this, EventArgs.Empty);
    }

    // Copia bloco inteiro para o clipboard de atividade (Ctrl+C).
    // Usa Display* para capturar o estado atual mesmo em edição.
    // Atualiza também o styleClipboard para que Ctrl+Shift+V funcione.
    public void CopyActivityToClipboard()
    {
        var source = Itinerary.Select(d => d.EditingActivity).FirstOrDefault(a => a is not null)
                  ?? BankRows.Select(r => r.EditingActivity).FirstOrDefault(a => a is not null)
                  ?? SelectedActivity;
        if (source is null) return;

        _activityClipboard = new ActivitySnapshot(
            source.DisplayTitle, source.DisplayType, source.DisplayColor, source.DisplayIcon,
            source.DisplayDetails, source.AdditionalData, source.DurationSlots, source.StartSlot);
        _styleClipboard = new ActivityStyle(_activityClipboard.Color, _activityClipboard.Icon, _activityClipboard.Type);
        OnPropertyChanged(nameof(HasActivityClipboard));
        OnPropertyChanged(nameof(HasStyleClipboard));
    }

    // Cria novo bloco a partir do clipboard no mesmo dia/linha do bloco selecionado (Ctrl+V).
    // - Com bloco selecionado: cola no mesmo slot do bloco selecionado.
    // - Sem bloco selecionado (dia em edição): cola no primeiro slot livre da linha.
    // Funciona entre versões porque o clipboard persiste na troca de versão.
    public bool PasteActivityFromClipboard()
    {
        if (_activityClipboard is null) return false;

        var snap = _activityClipboard;

        // Determina container e slot de destino
        var editingAct = Itinerary.Select(d => d.EditingActivity).FirstOrDefault(a => a is not null)
                      ?? BankRows.Select(r => r.EditingActivity).FirstOrDefault(a => a is not null);

        ItineraryDayViewModel? day     = null;
        BankRowViewModel?      bankRow = null;
        int pasteSlot;

        if (editingAct is not null)
        {
            day       = FindDayForActivity(editingAct);
            bankRow   = day is null ? FindBankRowForActivity(editingAct) : null;
            pasteSlot = editingAct.StartSlot;
        }
        else if (SelectedActivity is not null)
        {
            day       = FindDayForActivity(SelectedActivity);
            bankRow   = day is null ? FindBankRowForActivity(SelectedActivity) : null;
            pasteSlot = SelectedActivity.StartSlot;
        }
        else
        {
            // Sem bloco selecionado: cola no slot selecionado (clique em área vazia)
            day = Itinerary.FirstOrDefault(d => d.HasSelectedSlot);
            if (day is not null)
            {
                pasteSlot = day.SelectedSlot;
            }
            else
            {
                bankRow = BankRows.FirstOrDefault(r => r.HasSelectedSlot);
                if (bankRow is null) return false;
                pasteSlot = bankRow.SelectedSlot;
            }
        }

        if (day is null && bankRow is null) return false;

        var copy = new ItineraryActivityViewModel
        {
            Id             = $"act-{Guid.NewGuid():N}",
            Title          = snap.Title,
            Type           = snap.Type,
            Color          = snap.Color,
            Icon           = snap.Icon,
            Details        = snap.Details,
            AdditionalData = snap.AdditionalData,
            DurationSlots  = snap.DurationSlots,
            StartSlot      = pasteSlot,
        };

        if (day is not null) day.Activities.Add(copy);
        else                 bankRow!.Activities.Add(copy);

        SelectedActivity = copy;
        ItineraryChanged?.Invoke(this, EventArgs.Empty);
        return true;
    }



    public void AddExpense()
    {
        var defaultCurrency = _trip?.BaseCurrency ?? "BRL";
        var expense = new ExpenseEditorViewModel
        {
            Id = $"gasto-{DateTime.Now:yyyyMMddHHmmssfff}",
            IsActive = true,
            Title = "Novo gasto",
            Type = "Outros",
            People = 1,
            Quantity = 1,
            Currency = defaultCurrency,
            ExchangeRateToBase = GetRateForCurrency(defaultCurrency)
        };

        expense.PropertyChanged += Expense_PropertyChanged;
        Expenses.Add(expense);
        SelectedExpense = expense;
        RefreshBudget();
        ExpensesChanged?.Invoke(this, EventArgs.Empty);
    }

    public void DeleteSelectedExpense()
    {
        if (SelectedExpense is null)
        {
            return;
        }

        var index = Expenses.IndexOf(SelectedExpense);
        SelectedExpense.PropertyChanged -= Expense_PropertyChanged;
        Expenses.Remove(SelectedExpense);
        SelectedExpense = Expenses.ElementAtOrDefault(Math.Max(0, index - 1));
        RefreshBudget();
        ExpensesChanged?.Invoke(this, EventArgs.Empty);
    }

    public void AddCurrencyRate(string currency, decimal rateToBase, DateTime? updatedAt = null, string? symbol = null, string? name = null, int decimalDigits = 2)
    {
        currency = NormalizeCurrency(currency);
        if (string.IsNullOrWhiteSpace(currency))
        {
            return;
        }

        var rate = CurrencyRates.FirstOrDefault(item => string.Equals(item.Currency, currency, StringComparison.OrdinalIgnoreCase));
        if (rate is null)
        {
            rate = new CurrencyRateViewModel
            {
                Currency = currency,
                Symbol = string.IsNullOrWhiteSpace(symbol) ? currency : symbol.Trim(),
                Name = string.IsNullOrWhiteSpace(name) ? currency : name.Trim(),
                DecimalDigits = decimalDigits,
                RateToBase = rateToBase,
                UpdatedAt = updatedAt
            };
            rate.PropertyChanged += CurrencyRate_PropertyChanged;
            CurrencyRates.Add(rate);
        }
        else
        {
            rate.RateToBase = rateToBase;
            rate.UpdatedAt = updatedAt;
            if (!string.IsNullOrWhiteSpace(symbol))
            {
                rate.Symbol = symbol.Trim();
            }
            if (!string.IsNullOrWhiteSpace(name))
            {
                rate.Name = name.Trim();
            }
        }

        ApplyRateToExpenses(currency, rateToBase);
    }

    public bool RemoveCurrencyRate(CurrencyRateViewModel rate, out string message)
    {
        var currency = NormalizeCurrency(rate.Currency);
        if (Expenses.Any(expense => string.Equals(NormalizeCurrency(expense.Currency), currency, StringComparison.OrdinalIgnoreCase)))
        {
            message = $"A moeda {currency} está em uso nos gastos e não pode ser excluída.";
            return false;
        }

        rate.PropertyChanged -= CurrencyRate_PropertyChanged;
        CurrencyRates.Remove(rate);
        ApplyExpensesToTrip();
        ExpensesChanged?.Invoke(this, EventArgs.Empty);
        message = $"Moeda {currency} excluída.";
        return true;
    }

    public void ApplyExpensesToTrip()
    {
        if (_trip is null)
        {
            return;
        }

        _trip.Expenses = Expenses
            .Where(expense => !string.IsNullOrWhiteSpace(expense.Title))
            .Select(expense => expense.ToExpenseItem())
            .ToList();
        _trip.CurrencyRates = CurrencyRates
            .Where(rate => !string.IsNullOrWhiteSpace(rate.Currency))
            .Select(rate => rate.ToCurrencyRateItem())
            .ToList();
        RefreshSummary();
    }

    private void ApplyRateToExpenses(string currency, decimal rateToBase)
    {
        _isLoadingExpenses = true;
        foreach (var expense in Expenses.Where(expense =>
                     expense.PaidAmount <= 0 &&
                     !expense.UseFixedRate &&
                     string.Equals(expense.Currency, currency, StringComparison.OrdinalIgnoreCase)))
        {
            expense.ExchangeRateToBase = rateToBase;
        }
        _isLoadingExpenses = false;
        RefreshBudget();
        ExpensesChanged?.Invoke(this, EventArgs.Empty);
    }

    private decimal GetRateForCurrency(string currency)
    {
        var normalized = NormalizeCurrency(currency);
        return CurrencyRates.FirstOrDefault(rate => string.Equals(rate.Currency, normalized, StringComparison.OrdinalIgnoreCase))?.RateToBase ?? 1m;
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
        OnPropertyChanged(nameof(ActiveExpensesCount));
        OnPropertyChanged(nameof(HasSelectedExpense));
    }

    public void RefreshTripDetails()
    {
        RefreshSummary();
    }

    public void MoveDay(ItineraryDayViewModel day, int newIndex)
    {
        var current = Itinerary.IndexOf(day);
        if (current < 0) return;
        newIndex = Math.Clamp(newIndex, 0, Itinerary.Count - 1);
        if (current == newIndex) return;
        Itinerary.Move(current, newIndex);
        RefreshDayNumbers();
    }

    private void RefreshDayNumbers()
    {
        for (var i = 0; i < Itinerary.Count; i++)
            Itinerary[i].Number = i + 1;
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

        if (e.PropertyName is nameof(TaskEditorViewModel.IsEditing) or nameof(TaskEditorViewModel.IsSelectedForEdit) or nameof(TaskEditorViewModel.HasNotes))
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

        // File e IsEditing são gerenciados pelo fluxo de rename explícito;
        // ignorar aqui evita auto-save disparado a cada tecla durante edição inline.
        if (e.PropertyName is nameof(AttachmentEditorViewModel.File) or nameof(AttachmentEditorViewModel.IsEditing))
        {
            return;
        }

        OverviewFiles.ReplaceWith(Attachments.Take(4));
        RefreshSummary();
        AttachmentsChanged?.Invoke(this, EventArgs.Empty);
    }

    private void Expense_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_isLoadingExpenses)
        {
            return;
        }

        if (e.PropertyName is nameof(ExpenseEditorViewModel.IsSelectedForEdit) or nameof(ExpenseEditorViewModel.IsEditing))
        {
            return;
        }

        if (sender is ExpenseEditorViewModel expense &&
            expense.PaidAmount <= 0 &&
            !expense.UseFixedRate &&
            e.PropertyName is nameof(ExpenseEditorViewModel.Currency))
        {
            expense.ExchangeRateToBase = GetRateForCurrency(expense.Currency);
        }

        if (sender is ExpenseEditorViewModel { IsEditing: true } &&
            e.PropertyName is nameof(ExpenseEditorViewModel.Type))
        {
            RefreshBudget(rebuildGroups: false);
        }
        else if (e.PropertyName is nameof(ExpenseEditorViewModel.Type))
        {
            RefreshBudget();
        }
        else if (e.PropertyName is nameof(ExpenseEditorViewModel.IsActive)
                 or nameof(ExpenseEditorViewModel.Price)
                 or nameof(ExpenseEditorViewModel.Taxes)
                 or nameof(ExpenseEditorViewModel.People)
                 or nameof(ExpenseEditorViewModel.Quantity)
                 or nameof(ExpenseEditorViewModel.ExchangeRateToBase)
                 or nameof(ExpenseEditorViewModel.PaidAmount))
        {
            RefreshBudget(rebuildGroups: false);
        }

        ExpensesChanged?.Invoke(this, EventArgs.Empty);
    }

    private void CurrencyRate_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_isLoadingExpenses)
        {
            return;
        }

        if (sender is CurrencyRateViewModel rate && e.PropertyName is nameof(CurrencyRateViewModel.RateToBase) or nameof(CurrencyRateViewModel.Currency))
        {
            ApplyRateToExpenses(rate.Currency, rate.RateToBase);
        }
        if (e.PropertyName is nameof(CurrencyRateViewModel.Currency) or nameof(CurrencyRateViewModel.Symbol))
        {
            OnPropertyChanged(nameof(BaseCurrencySymbol));
        }
        if (e.PropertyName is nameof(CurrencyRateViewModel.DecimalDigits))
        {
            if (sender is CurrencyRateViewModel changedRate)
            {
                foreach (var expense in Expenses)
                    expense.NotifyDecimalDigitsChanged(changedRate.Currency);
            }
            RefreshBudget(rebuildGroups: false);
        }

        ExpensesChanged?.Invoke(this, EventArgs.Empty);
    }

    public void RefreshBudget(bool rebuildGroups = true)
    {
        BudgetCategories.ReplaceWith(BuildBudgetCategories(Expenses));
        if (rebuildGroups)
        {
            ExpenseGroups.ReplaceWith(BuildExpenseGroups(Expenses));
        }
        else
        {
            RefreshExpenseGroupTotals();
        }

        RefreshSummary();
    }

    private void RefreshExpenseGroupTotals()
    {
        foreach (var group in ExpenseGroups)
        {
            group.TotalLabel = FormatMoney(group.Items.Where(expense => expense.IsActive).Sum(expense => expense.SubtotalBase));
        }
    }

    private static IReadOnlyList<CurrencyRateViewModel> BuildCurrencyRates(Trip? trip)
    {
        if (trip is null)
        {
            return [];
        }

        var rates = trip.CurrencyRates
            .Select(CurrencyRateViewModel.FromCurrencyRate)
            .ToDictionary(rate => NormalizeCurrency(rate.Currency), StringComparer.OrdinalIgnoreCase);
        var baseCurrency = NormalizeCurrency(trip.BaseCurrency);
        rates[baseCurrency] = new CurrencyRateViewModel
        {
            Currency = baseCurrency,
            Symbol = GetDefaultCurrencyMetadata(trip.BaseCurrency, rates).Symbol,
            Name = GetDefaultCurrencyMetadata(trip.BaseCurrency, rates).Name,
            DecimalDigits = GetDefaultCurrencyMetadata(trip.BaseCurrency, rates).DecimalDigits,
            RateToBase = 1m,
            IsBaseCurrency = true
        };

        foreach (var expense in trip.Expenses)
        {
            var currency = NormalizeCurrency(expense.Currency);
            if (!rates.ContainsKey(currency))
            {
                rates[currency] = new CurrencyRateViewModel
                {
                    Currency = currency,
                    Symbol = currency,
                    Name = currency,
                    DecimalDigits = 2,
                    RateToBase = expense.ExchangeRateToBase <= 0 ? 1m : expense.ExchangeRateToBase,
                    IsBaseCurrency = string.Equals(currency, baseCurrency, StringComparison.OrdinalIgnoreCase)
                };
            }
        }

        foreach (var rate in rates.Values)
        {
            rate.IsBaseCurrency = string.Equals(rate.Currency, baseCurrency, StringComparison.OrdinalIgnoreCase);
        }
        return rates.Values
            .OrderBy(rate => string.Equals(rate.Currency, baseCurrency, StringComparison.OrdinalIgnoreCase) ? 0 : 1)
            .ThenBy(rate => rate.Currency)
            .ToList();
    }

    private static CurrencyRateViewModel GetDefaultCurrencyMetadata(string currency, IReadOnlyDictionary<string, CurrencyRateViewModel> rates)
    {
        var normalized = NormalizeCurrency(currency);
        return rates.TryGetValue(normalized, out var rate)
            ? rate
            : new CurrencyRateViewModel
            {
                Currency = normalized,
                Symbol = normalized,
                Name = normalized,
                DecimalDigits = 2,
                RateToBase = 1m
            };
    }

    private IReadOnlyList<BudgetCategoryViewModel> BuildBudgetCategories(IEnumerable<ExpenseEditorViewModel> expenses)
    {
        var totals = expenses
            .Where(expense => expense.IsActive)
            .GroupBy(expense => string.IsNullOrWhiteSpace(expense.Type) ? "Outros" : expense.Type.Trim())
            .Select(group => new
            {
                Name = group.Key,
                Total = group.Sum(expense => expense.SubtotalBase)
            })
            .ToList();
        var max = totals.Select(total => total.Total).DefaultIfEmpty(0).Max();
        var sum = totals.Sum(total => total.Total);

        return totals
            .Select((total, index) => new BudgetCategoryViewModel(
                total.Name,
                FormatMoney(total.Total),
                total.Total,
                sum == 0 ? 0 : (double)(total.Total / sum),
                sum == 0 ? "0%" : (total.Total / sum).ToString("P0", Culture),
                BudgetCategoryColors[index % BudgetCategoryColors.Length],
                max == 0 ? 0 : (double)(total.Total / max)))
            .OrderByDescending(category => category.BarWidth)
            .ToList();
    }

    private IReadOnlyList<ExpenseCategoryGroupViewModel> BuildExpenseGroups(IEnumerable<ExpenseEditorViewModel> expenses)
    {
        return expenses
            .GroupBy(expense => string.IsNullOrWhiteSpace(expense.Type) ? "Outros" : expense.Type.Trim())
            .Select(group => new ExpenseCategoryGroupViewModel(
                group.Key,
                FormatMoney(group.Where(expense => expense.IsActive).Sum(expense => expense.SubtotalBase)),
                group.ToList()))
            .ToList();
    }

    public void MoveExpense(ExpenseEditorViewModel expense, ExpenseEditorViewModel targetExpense)
    {
        if (ReferenceEquals(expense, targetExpense)) return;

        var oldIndex = Expenses.IndexOf(expense);
        var newIndex = Expenses.IndexOf(targetExpense);
        if (oldIndex < 0 || newIndex < 0 || oldIndex == newIndex) return;

        Expenses.Move(oldIndex, newIndex);
        RefreshBudget();
        ExpensesChanged?.Invoke(this, EventArgs.Empty);
    }

    public void MoveExpenseGroup(ExpenseCategoryGroupViewModel group, ExpenseCategoryGroupViewModel targetGroup)
    {
        if (ReferenceEquals(group, targetGroup)) return;

        var groupItems = group.Items.ToList();
        if (groupItems.Count == 0) return;

        var isDraggingDown = ExpenseGroups.IndexOf(group) < ExpenseGroups.IndexOf(targetGroup);

        var anchorExpense = isDraggingDown
            ? targetGroup.Items.LastOrDefault()
            : targetGroup.Items.FirstOrDefault();

        foreach (var expense in groupItems)
            Expenses.Remove(expense);

        var insertAt = anchorExpense is not null ? Expenses.IndexOf(anchorExpense) : -1;
        if (insertAt < 0)
        {
            insertAt = Expenses.Count;
        }
        else if (isDraggingDown)
        {
            insertAt++;
        }

        for (var i = groupItems.Count - 1; i >= 0; i--)
            Expenses.Insert(insertAt, groupItems[i]);

        RefreshBudget();
        ExpensesChanged?.Invoke(this, EventArgs.Empty);
    }

    private static string NormalizeCurrency(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? "BRL" : value.Trim().ToUpperInvariant();
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

    private string FormatMoney(decimal value)
    {
        var digits = CurrencyRates.FirstOrDefault(r => string.Equals(r.Currency, BaseCurrencyCode, StringComparison.OrdinalIgnoreCase))?.DecimalDigits ?? 2;
        return $"{BaseCurrencySymbol} {value.ToString($"N{digits}", Culture)}";
    }
}

public sealed class TaskEditorViewModel : NotifyObject
{
    private string _id = "";
    private string _title = "";
    private string _status = "pending";
    private string _notes = "";
    private bool _isEditing;
    private bool _isSelectedForEdit;

    public string Id { get => _id; set => SetField(ref _id, value); }
    public string Title { get => _title; set => SetField(ref _title, value); }
    public string Status
    {
        get => _status;
        set => SetField(ref _status, value == "done" ? "done" : "pending");
    }
    public string Notes { get => _notes; set => SetField(ref _notes, value); }
    public bool IsEditing { get => _isEditing; set => SetField(ref _isEditing, value); }
    public bool IsSelectedForEdit { get => _isSelectedForEdit; set => SetField(ref _isSelectedForEdit, value); }
    public bool IsDone
    {
        get => Status == "done";
        set => Status = value ? "done" : "pending";
    }
    public bool HasNotes => !string.IsNullOrWhiteSpace(Notes);
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
            Notes = task.Notes ?? ""
        };
    }

    public TaskItem ToTaskItem()
    {
        return new TaskItem
        {
            Id = string.IsNullOrWhiteSpace(Id) ? $"tarefa-{Guid.NewGuid():N}" : Id.Trim(),
            Title = Title.Trim(),
            Status = Status == "done" ? "done" : "pending",
            Notes = string.IsNullOrWhiteSpace(Notes) ? null : Notes.Trim()
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
        if (propertyName is nameof(Notes))
        {
            base.OnPropertyChanged(nameof(HasNotes));
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
            File = file
        };
    }
}

public sealed class ItineraryVersionTabViewModel : NotifyObject
{
    private string _id = "";
    private string _name = "Versão 1";
    private bool _isActive;
    private bool _isRenaming;
    private string _editName = "";

    public string Id { get => _id; set => SetField(ref _id, value); }
    public string Name { get => _name; set => SetField(ref _name, value); }
    public bool IsActive { get => _isActive; set => SetField(ref _isActive, value); }
    public bool IsRenaming { get => _isRenaming; set => SetField(ref _isRenaming, value); }
    public string EditName { get => _editName; set => SetField(ref _editName, value); }
}

public sealed class ItineraryDayViewModel : NotifyObject
{
    private static int _slotsPerDay = 16;
    private static double _slotWidth = 44;
    private static int _blockHeight = 44;

    public static void Configure(int slotsPerDay, double slotWidth)
    {
        _slotsPerDay = slotsPerDay;
        _slotWidth = slotWidth;
    }

    public static void ConfigureBlockHeight(int height) => _blockHeight = height;

    private static DateOnly _tripStartDate;
    public static void ConfigureStartDate(DateOnly d) { _tripStartDate = d; }
    public void NotifyDateChanged() => OnPropertyChanged(nameof(DateLabel));

    private string _id = "", _summary = "";
    private bool _isDragTarget, _isDimmed, _isDragging;
    private ItineraryActivityViewModel? _editingActivity;

    // ── day edit buffer ─────────────────────────────────────────────────────
    private bool _isEditingDay;
    private string _editDaySummary = "";

    public string Id { get => _id; set => SetField(ref _id, value); }
    public string Title => $"Dia {_number}";
    public string Summary { get => _summary; set { if (SetField(ref _summary, value)) { OnPropertyChanged(nameof(HasSummary)); OnPropertyChanged(nameof(CardTitle)); } } }
    public bool HasSummary => !string.IsNullOrWhiteSpace(_summary);

    /// <summary>Título exibido no card da visão geral: Summary se preenchido, senão nome do Pernoite.</summary>
    public string CardTitle => !string.IsNullOrWhiteSpace(_summary)
        ? _summary
        : Activities.FirstOrDefault(a => string.Equals(a.Type, "Pernoite", StringComparison.OrdinalIgnoreCase))?.Title ?? "";
    public bool IsDragTarget { get => _isDragTarget; set => SetField(ref _isDragTarget, value); }
    public bool IsDimmed     { get => _isDimmed;     set => SetField(ref _isDimmed,     value); }
    public bool IsDragging   { get => _isDragging;   set => SetField(ref _isDragging,   value); }

    private int _number = 1;
    public int Number
    {
        get => _number;
        set { if (SetField(ref _number, value)) { OnPropertyChanged(nameof(Title)); OnPropertyChanged(nameof(DateLabel)); } }
    }

    private int _selectedSlot = -1;
    public int SelectedSlot
    {
        get => _selectedSlot;
        set { if (SetField(ref _selectedSlot, value)) { OnPropertyChanged(nameof(HasSelectedSlot)); OnPropertyChanged(nameof(SelectedSlotX)); } }
    }
    public bool   HasSelectedSlot => _selectedSlot >= 0;
    public double SelectedSlotX        => _selectedSlot * _slotWidth;
    public double SlotWidth             => _slotWidth;
    public double SelectionRectTop      => 1;
    public double SelectionRectHeight   => Math.Max(0, _blockHeight + 8 - 2);

    private System.Windows.Media.ImageSource? _coverImage;
    public System.Windows.Media.ImageSource? CoverImage { get => _coverImage; private set { if (SetField(ref _coverImage, value)) OnPropertyChanged(nameof(HasCoverImage)); } }
    public bool HasCoverImage => _coverImage is not null;

    private string? _coverCacheDir;
    private string? _lastCoverQuery;
    private int _imageOffset;

    /// <summary>
    /// Busca imagem no Wikimedia Commons e cacheia localmente.
    /// Salva cacheDir e query para permitir rebusca automática quando CardTitle mudar.
    /// </summary>
    public async Task LoadCoverImageAsync(string cacheDir)
    {
        _coverCacheDir = cacheDir;
        var query = !string.IsNullOrWhiteSpace(CardTitle) ? CardTitle : Title;
        _lastCoverQuery = query;
        if (string.IsNullOrWhiteSpace(query)) return;

        await FetchAndApplyImageAsync(query, cacheDir, _imageOffset);
    }

    /// <summary>Força recarregamento com a próxima imagem disponível (incrementa offset).</summary>
    public async Task ReloadCoverImageAsync()
    {
        if (string.IsNullOrEmpty(_coverCacheDir)) return;
        _imageOffset++;
        _lastCoverQuery = null;
        await System.Windows.Application.Current.Dispatcher.InvokeAsync(() => CoverImage = null);
        await LoadCoverImageAsync(_coverCacheDir);
    }

    /// <summary>Rebusca a imagem se CardTitle mudou de valor desde a última busca.</summary>
    private async Task RefetchCoverImageAsync()
    {
        if (string.IsNullOrEmpty(_coverCacheDir)) return;

        var query = !string.IsNullOrWhiteSpace(CardTitle) ? CardTitle : Title;
        if (query == _lastCoverQuery) return;

        // Apaga cache do query anterior para forçar nova busca
        if (!string.IsNullOrEmpty(_lastCoverQuery))
        {
            var oldHash = Convert.ToHexString(
                System.Security.Cryptography.MD5.HashData(
                    System.Text.Encoding.UTF8.GetBytes(_lastCoverQuery.ToLowerInvariant())))[..10];
            var oldPath = System.IO.Path.Combine(_coverCacheDir, $"day-{oldHash}.jpg");
            try { if (System.IO.File.Exists(oldPath)) System.IO.File.Delete(oldPath); } catch { }
        }

        _lastCoverQuery = query;
        // Não limpa CoverImage aqui — a imagem antiga permanece visível até a nova carregar
        await FetchAndApplyImageAsync(query, _coverCacheDir, _imageOffset);
    }

    private async Task FetchAndApplyImageAsync(string query, string cacheDir, int offset = 0)
    {
        var path = await WikimediaImageService.FetchAndCacheAsync(query, cacheDir, offset);
        if (path is null) return;

        await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
        {
            try
            {
                var bmp = new System.Windows.Media.Imaging.BitmapImage();
                bmp.BeginInit();
                bmp.UriSource = new Uri(path);
                bmp.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                bmp.EndInit();
                bmp.Freeze();
                CoverImage = bmp;
            }
            catch { }
        });
    }

    /// <summary>"Pernoite em {nome}" para exibição no card da visão geral, ou vazio se não houver Pernoite.</summary>
    public string PernoiteLabel
    {
        get
        {
            var title = Activities
                .FirstOrDefault(a => string.Equals(a.Type, "Pernoite", StringComparison.OrdinalIgnoreCase))
                ?.Title;
            return string.IsNullOrEmpty(title) ? "" : $"Pernoite em {title}";
        }
    }
    public bool HasPernoite => !string.IsNullOrEmpty(PernoiteLabel);

    /// <summary>Título da primeira atividade do tipo Pernoite, ou o Summary/Title do dia como fallback.</summary>
    public string OvernightLabel => Activities
        .FirstOrDefault(a => string.Equals(a.Type, "Pernoite", StringComparison.OrdinalIgnoreCase))
        ?.Title
        ?? (string.IsNullOrWhiteSpace(Summary) ? "" : Summary);

    /// <summary>
    /// Atividades para exibição no card da visão geral (sem limite de quantidade — o card clipa pelo espaço disponível).
    /// Refeição é excluída. Pernoite é sempre incluído e ordenado por slot junto com as demais.
    /// </summary>
    public IEnumerable<ItineraryActivityViewModel> TopActivities
    {
        get
        {
            return Activities
                .Where(a => !string.Equals(a.Type, "Refeição", StringComparison.OrdinalIgnoreCase)
                         && !string.Equals(a.Type, "Pernoite", StringComparison.OrdinalIgnoreCase))
                .OrderBy(a => a.StartSlot);
        }
    }

    public bool IsEditingDay { get => _isEditingDay; private set { if (SetField(ref _isEditingDay, value)) OnPropertyChanged(nameof(IsInEditMode)); } }
    public string EditDaySummary { get => _editDaySummary; set => SetField(ref _editDaySummary, value); }

    public void BeginDayEdit()
    {
        RejectEdit(); // fecha edição de atividade se aberta
        _editDaySummary = Summary;
        OnPropertyChanged(nameof(EditDaySummary));
        IsEditingDay = true;
    }

    public void AcceptDayEdit()
    {
        Summary = EditDaySummary.Trim();
        IsEditingDay = false;
    }

    public void RejectDayEdit() => IsEditingDay = false;

    public ItineraryActivityViewModel? EditingActivity
    {
        get => _editingActivity;
        private set { if (SetField(ref _editingActivity, value)) { OnPropertyChanged(nameof(HasEditingBlock)); OnPropertyChanged(nameof(IsInEditMode)); } }
    }
    public bool HasEditingBlock => _editingActivity is not null;
    public bool IsInEditMode => _isEditingDay || _editingActivity is not null;

    public void BeginEdit(ItineraryActivityViewModel activity)
    {
        RejectDayEdit(); // fecha edição do dia se aberta
        ClearEditState();
        activity.BeginEdit();
        EditingActivity = activity;
        foreach (var a in Activities) a.IsDimmed = a != activity;
    }

    public void AcceptEdit()
    {
        _editingActivity?.AcceptEdit();
        ClearEditState();
    }

    public void RejectEdit()
    {
        _editingActivity?.RejectEdit();
        ClearEditState();
    }

    private void ClearEditState()
    {
        foreach (var a in Activities) a.IsDimmed = false;
        EditingActivity = null;
    }

    public ObservableCollection<ItineraryActivityViewModel> Activities { get; } = [];

    public ItineraryDayViewModel()
    {
        Activities.CollectionChanged += OnActivitiesCollectionChanged;
        PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(CardTitle))
                _ = RefetchCoverImageAsync();
        };
    }

    private void OnActivitiesCollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems != null)
            foreach (ItineraryActivityViewModel a in e.NewItems)
                a.PropertyChanged += OnActivityPropertyChanged;
        if (e.OldItems != null)
            foreach (ItineraryActivityViewModel a in e.OldItems)
                a.PropertyChanged -= OnActivityPropertyChanged;
        RefreshOverviewProps();
        RefreshOverlaps();
    }

    private void OnActivityPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ItineraryActivityViewModel.Title)
                           or nameof(ItineraryActivityViewModel.Type)
                           or nameof(ItineraryActivityViewModel.StartSlot))
            RefreshOverviewProps();
        if (e.PropertyName is nameof(ItineraryActivityViewModel.StartSlot)
                           or nameof(ItineraryActivityViewModel.DurationSlots))
            RefreshOverlaps();
    }

    private void RefreshOverlaps()
    {
        var acts = Activities;
        foreach (var a in acts) a.IsOverlapping = false;
        for (int i = 0; i < acts.Count; i++)
            for (int j = i + 1; j < acts.Count; j++)
            {
                var a = acts[i]; var b = acts[j];
                if (a.StartSlot < b.StartSlot + b.DurationSlots &&
                    b.StartSlot < a.StartSlot + a.DurationSlots)
                {
                    a.IsOverlapping = true;
                    b.IsOverlapping = true;
                }
            }
    }

    private void RefreshOverviewProps()
    {
        OnPropertyChanged(nameof(OvernightLabel));
        OnPropertyChanged(nameof(PernoiteLabel));
        OnPropertyChanged(nameof(HasPernoite));
        OnPropertyChanged(nameof(CardTitle));
        OnPropertyChanged(nameof(TopActivities));
        OnPropertyChanged(nameof(ActivitiesLabel));
    }

    public double CanvasWidth => _slotsPerDay * _slotWidth;
    public int CanvasHeight => _blockHeight + 8;
    public double MorningWidth => (_slotsPerDay / 3) * _slotWidth;
    public double AfternoonWidth => (_slotsPerDay / 3) * _slotWidth;
    public double EveningStartX => MorningWidth + AfternoonWidth;
    public double EveningWidth => CanvasWidth - EveningStartX;
    public double AfternoonLabelX => MorningWidth + 4;
    public double EveningLabelX => EveningStartX + 4;
    public string DateLabel => _tripStartDate == default
        ? ""
        : _tripStartDate.AddDays(_number - 1).ToString("ddd dd/MM", CultureInfo.GetCultureInfo("pt-BR"));
    public string ActivitiesLabel => Activities.Count == 0 ? "Sem atividades" : $"{Activities.Count} atividade{(Activities.Count == 1 ? "" : "s")}";
    public IEnumerable<double> SlotLinePositions => Enumerable.Range(1, _slotsPerDay - 1).Select(i => i * _slotWidth);

    public void NotifyLayoutChanged()
    {
        OnPropertyChanged(nameof(CanvasWidth));
        OnPropertyChanged(nameof(CanvasHeight));
        OnPropertyChanged(nameof(MorningWidth));
        OnPropertyChanged(nameof(AfternoonWidth));
        OnPropertyChanged(nameof(EveningStartX));
        OnPropertyChanged(nameof(EveningWidth));
        OnPropertyChanged(nameof(AfternoonLabelX));
        OnPropertyChanged(nameof(EveningLabelX));
        OnPropertyChanged(nameof(SlotLinePositions));
        OnPropertyChanged(nameof(SlotWidth));
        OnPropertyChanged(nameof(SelectedSlotX));
        OnPropertyChanged(nameof(SelectionRectHeight));
        foreach (var a in Activities) a.NotifyLayoutChanged();
    }

    public static ItineraryDayViewModel FromDay(ItineraryDay day)
    {
        var vm = new ItineraryDayViewModel { Id = day.Id, Summary = day.Summary };
        foreach (var a in day.Activities)
            vm.Activities.Add(ItineraryActivityViewModel.FromActivity(a));
        return vm;
    }

    public ItineraryDay ToDay() => new()
    {
        Id = string.IsNullOrWhiteSpace(Id) ? $"dia-{Guid.NewGuid():N}" : Id,
        Summary = Summary.Trim(),
        Activities = Activities.Select(a => a.ToActivity()).ToList()
    };
}

public sealed class BankRowViewModel : NotifyObject
{
    private static int _slotsPerDay = 16;
    private static double _slotWidth = 44;
    private static int _blockHeight = 44;

    public static void Configure(int slotsPerDay, double slotWidth) { _slotsPerDay = slotsPerDay; _slotWidth = slotWidth; }
    public static void ConfigureBlockHeight(int height) => _blockHeight = height;

    private bool _isDragTarget;
    private ItineraryActivityViewModel? _editingActivity;

    public int RowIndex { get; set; }
    public ObservableCollection<ItineraryActivityViewModel> Activities { get; }

    public BankRowViewModel()
    {
        Activities = [];
        Activities.CollectionChanged += (_, e) =>
        {
            if (e.NewItems != null)
                foreach (ItineraryActivityViewModel a in e.NewItems)
                    a.PropertyChanged += OnActivityPropertyChanged;
            if (e.OldItems != null)
                foreach (ItineraryActivityViewModel a in e.OldItems)
                    a.PropertyChanged -= OnActivityPropertyChanged;
            RefreshOverlaps();
        };
    }

    private void OnActivityPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ItineraryActivityViewModel.StartSlot)
                           or nameof(ItineraryActivityViewModel.DurationSlots))
            RefreshOverlaps();
    }

    private void RefreshOverlaps()
    {
        var acts = Activities;
        foreach (var a in acts) a.IsOverlapping = false;
        for (int i = 0; i < acts.Count; i++)
            for (int j = i + 1; j < acts.Count; j++)
            {
                var a = acts[i]; var b = acts[j];
                if (a.StartSlot < b.StartSlot + b.DurationSlots &&
                    b.StartSlot < a.StartSlot + a.DurationSlots)
                {
                    a.IsOverlapping = true;
                    b.IsOverlapping = true;
                }
            }
    }

    public bool IsDragTarget { get => _isDragTarget; set => SetField(ref _isDragTarget, value); }
    public ItineraryActivityViewModel? EditingActivity
    {
        get => _editingActivity;
        private set { if (SetField(ref _editingActivity, value)) OnPropertyChanged(nameof(HasEditingBlock)); }
    }
    public bool HasEditingBlock => _editingActivity is not null;

    public double CanvasWidth => _slotsPerDay * _slotWidth;
    public int    CanvasHeight => _blockHeight + 8;

    private int _selectedSlot = -1;
    public int SelectedSlot
    {
        get => _selectedSlot;
        set { if (SetField(ref _selectedSlot, value)) { OnPropertyChanged(nameof(HasSelectedSlot)); OnPropertyChanged(nameof(SelectedSlotX)); } }
    }
    public bool   HasSelectedSlot    => _selectedSlot >= 0;
    public double SelectedSlotX      => _selectedSlot * _slotWidth;
    public double SlotWidth          => _slotWidth;
    public double SelectionRectTop   => 1;
    public double SelectionRectHeight => Math.Max(0, _blockHeight + 8 - 2);

    public void BeginEdit(ItineraryActivityViewModel activity)
    {
        ClearEditState();
        activity.BeginEdit();
        EditingActivity = activity;
        foreach (var a in Activities) a.IsDimmed = a != activity;
    }

    public void AcceptEdit()
    {
        _editingActivity?.AcceptEdit();
        ClearEditState();
    }

    public void RejectEdit()
    {
        _editingActivity?.RejectEdit();
        ClearEditState();
    }

    private void ClearEditState()
    {
        foreach (var a in Activities) a.IsDimmed = false;
        EditingActivity = null;
    }

    public IEnumerable<double> SlotLinePositions => Enumerable.Range(1, _slotsPerDay - 1).Select(i => i * _slotWidth);

    public void NotifyLayoutChanged()
    {
        OnPropertyChanged(nameof(CanvasWidth));
        OnPropertyChanged(nameof(CanvasHeight));
        OnPropertyChanged(nameof(SlotLinePositions));
        OnPropertyChanged(nameof(SlotWidth));
        OnPropertyChanged(nameof(SelectedSlotX));
        OnPropertyChanged(nameof(SelectionRectHeight));
        foreach (var a in Activities) a.NotifyLayoutChanged();
    }
}

public sealed class ItineraryActivityViewModel : NotifyObject
{
    private static double _slotWidth = 44;
    private static int _blockHeight = 44;
    private static int _fontSize = 11;
    private static int _slotsPerDay = 16;

    public static IReadOnlyList<string> TypeOptions { get; } = ["Atividade", "Refeição", "Pernoite"];

    public static void Configure(double slotWidth) => _slotWidth = slotWidth;
    public static void ConfigureBlockHeight(int height) => _blockHeight = height;
    public static void ConfigureFontSize(int size) => _fontSize = size;
    public static void ConfigureSlotsPerDay(int slots) => _slotsPerDay = Math.Max(1, slots);

    private string _id = "", _title = "", _color = "#DBEAFE", _icon = "", _type = "Atividade";
    private int _startSlot, _durationSlots = 2;
    private string? _details, _additionalData;
    private bool _isSelected, _isEditing, _isDimmed, _isOverlapping;

    // Edit buffer
    private string _editTitle = "", _editIcon = "", _editColor = "#DBEAFE", _editType = "Atividade";
    private string? _editDetails, _editAdditionalData;
    private int _editDurationSlots = 2;

    public string Id { get => _id; set => SetField(ref _id, value); }
    public string Title { get => _title; set { if (SetField(ref _title, value)) OnPropertyChanged(nameof(OverviewTitle)); } }
    public string Color { get => _color; set => SetField(ref _color, value); }
    public string Icon { get => _icon; set { if (SetField(ref _icon, value)) OnPropertyChanged(nameof(OverviewIcon)); } }
    public string Type { get => _type; set { if (SetField(ref _type, value)) { OnPropertyChanged(nameof(OverviewIcon)); OnPropertyChanged(nameof(OverviewTitle)); } } }
    public int StartSlot { get => _startSlot; set => SetField(ref _startSlot, Math.Max(0, value)); }
    public int DurationSlots { get => _durationSlots; set => SetField(ref _durationSlots, Math.Max(1, value)); }
    public string? Details { get => _details; set => SetField(ref _details, value); }
    public string? AdditionalData { get => _additionalData; set => SetField(ref _additionalData, value); }
    public bool IsSelected { get => _isSelected; set => SetField(ref _isSelected, value); }
    public bool IsEditing { get => _isEditing; set => SetField(ref _isEditing, value); }
    public bool IsDimmed { get => _isDimmed; set => SetField(ref _isDimmed, value); }
    public bool IsOverlapping { get => _isOverlapping; set => SetField(ref _isOverlapping, value); }
    public bool HasDetails => !string.IsNullOrWhiteSpace(_details);

    /// <summary>Ícone para exibição compacta: usa o emoji do campo Icon, ou um fallback por tipo.</summary>
    public string OverviewIcon => !string.IsNullOrEmpty(_icon) ? _icon
        : _type switch { "Refeição" => "🍴", "Pernoite" => "🛏", _ => "📍" };

    /// <summary>Título formatado para o card da visão geral. Pernoite aparece como "Pernoite em {nome}".</summary>
    public string OverviewTitle => string.Equals(_type, "Pernoite", StringComparison.OrdinalIgnoreCase)
        ? $"Pernoite em {_title}"
        : _title;

    /// <summary>Horário estimado derivado do slot (base 08:00, 16h/dia).</summary>
    public string TimeLabel
    {
        get
        {
            const int startHour = 8;
            const int hoursSpan = 16; // 08:00–24:00
            var minutesPerSlot = hoursSpan * 60.0 / _slotsPerDay;
            var totalMin = (int)(startHour * 60 + _startSlot * minutesPerSlot);
            totalMin = Math.Clamp(totalMin, 0, 23 * 60 + 59);
            return $"{totalMin / 60:D2}:{totalMin % 60:D2}";
        }
    }

    public string EditTitle { get => _editTitle; set => SetField(ref _editTitle, value); }
    public string EditIcon { get => _editIcon; set => SetField(ref _editIcon, value); }
    public string EditColor { get => _editColor; set { if (SetField(ref _editColor, value)) OnPropertyChanged(nameof(EditTextColor)); } }
    public string EditType { get => _editType; set => SetField(ref _editType, value); }
    public string? EditDetails { get => _editDetails; set => SetField(ref _editDetails, value); }
    public string? EditAdditionalData { get => _editAdditionalData; set => SetField(ref _editAdditionalData, value); }
    public int EditDurationSlots { get => _editDurationSlots; set => SetField(ref _editDurationSlots, Math.Max(1, value)); }
    public string EditTextColor => ComputeTextColor(_editColor);

    // Display properties: reflect edit buffer while editing, real values otherwise
    public string DisplayTitle => _isEditing ? _editTitle : _title;
    public string DisplayIcon => _isEditing ? _editIcon : _icon;
    public string DisplayColor => _isEditing ? _editColor : _color;
    public string DisplayType => _isEditing ? _editType : _type;
    public bool IsPernoite => DisplayType == "Pernoite";
    public bool IsRefeicao => DisplayType == "Refeição";
    public string? DisplayDetails => _isEditing ? _editDetails : _details;
    public bool DisplayHasDetails => !string.IsNullOrWhiteSpace(_isEditing ? _editDetails : _details);
    public string DisplayTextColor => ComputeTextColor(_isEditing ? _editColor : _color);

    public double CanvasLeft => StartSlot * _slotWidth;
    public double BlockWidth => Math.Max(DurationSlots * _slotWidth - 2, 10);
    public int BlockHeight => _blockHeight;
    public int FontSize => _fontSize;
    public int IconFontSize => _fontSize + 1;
    public int DetailsFontSize => Math.Max(_fontSize - 2, 7);
    public string TextColor => ComputeTextColor(_color);

    private static string ComputeTextColor(string? hex)
    {
        try
        {
            var h = (hex ?? "DBEAFE").TrimStart('#');
            if (h.Length < 6) return "#1E293B";
            var r = Convert.ToInt32(h[0..2], 16);
            var g = Convert.ToInt32(h[2..4], 16);
            var b = Convert.ToInt32(h[4..6], 16);
            return (0.299 * r + 0.587 * g + 0.114 * b) / 255.0 > 0.5 ? "#1E293B" : "#FFFFFF";
        }
        catch { return "#1E293B"; }
    }

    protected override void OnPropertyChanged(string propertyName)
    {
        base.OnPropertyChanged(propertyName);
        if (propertyName is nameof(StartSlot))    { base.OnPropertyChanged(nameof(CanvasLeft)); base.OnPropertyChanged(nameof(TimeLabel)); }
        if (propertyName is nameof(DurationSlots)) base.OnPropertyChanged(nameof(BlockWidth));
        if (propertyName is nameof(Color))        { base.OnPropertyChanged(nameof(TextColor)); base.OnPropertyChanged(nameof(DisplayColor)); base.OnPropertyChanged(nameof(DisplayTextColor)); }
        if (propertyName is nameof(Title))        base.OnPropertyChanged(nameof(DisplayTitle));
        if (propertyName is nameof(Icon))         base.OnPropertyChanged(nameof(DisplayIcon));
        if (propertyName is nameof(Details))      { base.OnPropertyChanged(nameof(HasDetails)); base.OnPropertyChanged(nameof(DisplayDetails)); base.OnPropertyChanged(nameof(DisplayHasDetails)); }
        if (propertyName is nameof(EditTitle))    base.OnPropertyChanged(nameof(DisplayTitle));
        if (propertyName is nameof(EditIcon))     base.OnPropertyChanged(nameof(DisplayIcon));
        if (propertyName is nameof(EditColor))    { base.OnPropertyChanged(nameof(EditTextColor)); base.OnPropertyChanged(nameof(DisplayColor)); base.OnPropertyChanged(nameof(DisplayTextColor)); }
        if (propertyName is nameof(EditType))     { base.OnPropertyChanged(nameof(DisplayType)); base.OnPropertyChanged(nameof(IsPernoite)); base.OnPropertyChanged(nameof(IsRefeicao)); }
        if (propertyName is nameof(EditDetails))  { base.OnPropertyChanged(nameof(DisplayDetails)); base.OnPropertyChanged(nameof(DisplayHasDetails)); }
        if (propertyName is nameof(Type))         { base.OnPropertyChanged(nameof(DisplayType)); base.OnPropertyChanged(nameof(IsPernoite)); base.OnPropertyChanged(nameof(IsRefeicao)); }
        if (propertyName is nameof(IsEditing))    { base.OnPropertyChanged(nameof(DisplayTitle)); base.OnPropertyChanged(nameof(DisplayIcon)); base.OnPropertyChanged(nameof(DisplayColor)); base.OnPropertyChanged(nameof(DisplayType)); base.OnPropertyChanged(nameof(IsPernoite)); base.OnPropertyChanged(nameof(IsRefeicao)); base.OnPropertyChanged(nameof(DisplayDetails)); base.OnPropertyChanged(nameof(DisplayHasDetails)); base.OnPropertyChanged(nameof(DisplayTextColor)); }
    }

    public void BeginEdit()
    {
        EditTitle = Title;
        EditIcon = Icon;
        EditColor = Color;
        EditType = Type;
        EditDetails = Details;
        EditAdditionalData = AdditionalData;
        EditDurationSlots = DurationSlots;
        IsEditing = true;
    }

    public void AcceptEdit()
    {
        Title = EditTitle;
        Icon = EditIcon;
        Color = EditColor;
        Type = EditType;
        Details = string.IsNullOrWhiteSpace(EditDetails) ? null : EditDetails.Trim();
        AdditionalData = string.IsNullOrWhiteSpace(EditAdditionalData) ? null : EditAdditionalData.Trim();
        DurationSlots = EditDurationSlots;
        IsEditing = false;
    }

    public void RejectEdit()
    {
        IsEditing = false;
    }

    public void NotifyLayoutChanged()
    {
        OnPropertyChanged(nameof(CanvasLeft));
        OnPropertyChanged(nameof(BlockWidth));
        OnPropertyChanged(nameof(BlockHeight));
        OnPropertyChanged(nameof(FontSize));
        OnPropertyChanged(nameof(IconFontSize));
        OnPropertyChanged(nameof(DetailsFontSize));
    }

    public static ItineraryActivityViewModel FromActivity(ItineraryActivity a) => new()
    {
        Id = a.Id,
        Title = a.Title,
        Type = string.IsNullOrWhiteSpace(a.Type) ? "Atividade" : a.Type,
        Color = string.IsNullOrWhiteSpace(a.Color) ? "#DBEAFE" : a.Color,
        Icon = a.Icon ?? "",
        StartSlot = a.StartSlot,
        DurationSlots = a.DurationSlots,
        Details = a.Details,
        AdditionalData = a.AdditionalData
    };

    public ItineraryActivity ToActivity() => new()
    {
        Id = string.IsNullOrWhiteSpace(Id) ? $"act-{Guid.NewGuid():N}" : Id,
        Title = Title.Trim(),
        Type = _type,
        Color = _color,
        Icon = _icon,
        StartSlot = StartSlot,
        DurationSlots = DurationSlots,
        Details = string.IsNullOrWhiteSpace(Details) ? null : Details.Trim(),
        AdditionalData = string.IsNullOrWhiteSpace(AdditionalData) ? null : AdditionalData.Trim()
    };
}

public sealed class ExpenseEditorViewModel : NotifyObject
{
    private static readonly CultureInfo Culture = new("pt-BR");
    private static IReadOnlyList<CurrencyRateViewModel> CurrencyRates { get; set; } = [];
    private static string BaseCurrency { get; set; } = "BRL";
    private static int RateDecimalDigits { get; set; } = 2;

    private string _id = "";
    private bool _isActive = true;
    private string _title = "";
    private string _type = "";
    private string _company = "";
    private string _link = "";
    private string _notes = "";
    private decimal _price;
    private decimal _taxes;
    private int _people = 1;
    private int _quantity = 1;
    private string _currency = "BRL";
    private decimal _exchangeRateToBase = 1m;
    private bool _useFixedRate = false;
    private decimal _paidAmount;
    private bool _isSelectedForEdit;
    private bool _isEditing;

    public string Id { get => _id; set => SetField(ref _id, value); }
    public bool IsActive { get => _isActive; set => SetField(ref _isActive, value); }
    public string Title { get => _title; set => SetField(ref _title, value); }
    public string Type { get => _type; set => SetField(ref _type, value); }
    public string Company { get => _company; set => SetField(ref _company, value); }
    public string Link { get => _link; set => SetField(ref _link, value); }
    public string Notes { get => _notes; set => SetField(ref _notes, value); }
    public decimal Price { get => _price; set => SetField(ref _price, value); }
    public decimal Taxes { get => _taxes; set => SetField(ref _taxes, value); }
    public int People { get => _people; set => SetField(ref _people, Math.Max(1, value)); }
    public int Quantity { get => _quantity; set => SetField(ref _quantity, Math.Max(1, value)); }
    public string Currency { get => _currency; set => SetField(ref _currency, NormalizeCurrency(value)); }
    public decimal ExchangeRateToBase { get => _exchangeRateToBase; set => SetField(ref _exchangeRateToBase, value <= 0 ? 1m : value); }
    public bool UseFixedRate { get => _useFixedRate; set => SetField(ref _useFixedRate, value); }
    public bool UseAutomaticRate { get => !UseFixedRate; set => UseFixedRate = !value; }
    public decimal PaidAmount { get => _paidAmount; set => SetField(ref _paidAmount, Math.Max(0, value)); }
    public bool IsSelectedForEdit { get => _isSelectedForEdit; set => SetField(ref _isSelectedForEdit, value); }
    public bool IsEditing { get => _isEditing; set => SetField(ref _isEditing, value); }
    public decimal Subtotal => (Price + Taxes) * People * Quantity;
    public decimal SubtotalBase => IsActive ? Subtotal * ExchangeRateToBase : 0m;
    public decimal PendingAmount => IsActive ? Math.Max(decimal.Round(SubtotalBase - PaidAmount, 2, MidpointRounding.AwayFromZero), 0) : 0m;
    public string ActiveGlyph => IsActive ? "►" : "";
    public string ActiveStatusLabel => IsActive ? "Ativo" : "Inativo";
    public void NotifyDecimalDigitsChanged(string changedCurrency)
    {
        var normalized = NormalizeCurrency(changedCurrency);
        var isOwnCurrency = string.Equals(NormalizeCurrency(Currency), normalized, StringComparison.OrdinalIgnoreCase);
        var isBase = string.Equals(normalized, BaseCurrency, StringComparison.OrdinalIgnoreCase);

        if (isOwnCurrency)
        {
            OnPropertyChanged(nameof(PriceText));
            OnPropertyChanged(nameof(TaxesText));
            OnPropertyChanged(nameof(SummaryLine));
            OnPropertyChanged(nameof(QuantityLine));
        }
        if (isBase)
        {
            OnPropertyChanged(nameof(PaidAmountText));
            OnPropertyChanged(nameof(SubtotalBaseLabel));
            OnPropertyChanged(nameof(PaidLabel));
            OnPropertyChanged(nameof(PendingLabel));
            OnPropertyChanged(nameof(PaymentStatusLabel));
        }
    }

    public static void Configure(IReadOnlyList<CurrencyRateViewModel> currencyRates, string baseCurrency, int rateDecimalDigits)
    {
        CurrencyRates = currencyRates;
        BaseCurrency = NormalizeCurrency(baseCurrency);
        RateDecimalDigits = rateDecimalDigits;
    }

    public string SummaryLine
    {
        get
        {
            var amount = $"total {Currency} {FormatCurrencyAmount(Currency, Subtotal)}";
            var currencyDetail = IsBaseCurrency
                ? amount
                : $"{amount} · 💱 1 {Currency} = {BaseCurrency} {FormatCompactDecimal(ExchangeRateToBase)}";
            return currencyDetail;
        }
    }
    public string QuantityLine
    {
        get
        {
            var taxes = Taxes > 0 ? $" + {Currency} {FormatCurrencyAmount(Currency, Taxes)}" : "";
            return $"preço {Currency} {FormatCurrencyAmount(Currency, Price)}{taxes} · 👤{People} · ＃{Quantity}";
        }
    }
    public string DetailLine
    {
        get
        {
            var parts = new[]
            {
                Company,
                Notes
            }.Where(part => !string.IsNullOrWhiteSpace(part));
            return string.Join(" · ", parts);
        }
    }
    public string TypeLabel => string.IsNullOrWhiteSpace(Type) ? "Outros" : Type.Trim();
    public string SubtotalBaseLabel => FormatBaseCurrencyAmount(SubtotalBase);
    public string PaidLabel => PaidAmount > 0 ? $"Pago {FormatBaseCurrencyAmount(PaidAmount)}" : "Não pago";
    public string PendingLabel => PendingAmount > 0 ? $"Falta {FormatBaseCurrencyAmount(PendingAmount)}" : "Quitado";
    public string PaymentStatusLabel => PendingAmount <= 0
        ? "Quitado"
        : PaidAmount > 0
            ? $"Falta {FormatBaseCurrencyAmount(PendingAmount)}"
            : "A pagar";
    public bool IsPaymentSettled => PendingAmount <= 0;
    public bool IsBaseCurrency => string.Equals(NormalizeCurrency(Currency), BaseCurrency, StringComparison.OrdinalIgnoreCase);
    public bool HasLink => LinkEditorViewModel.IsHttpUrl(Link);

    public string PriceText
    {
        get => _price.ToString($"N{GetDecimalDigits(Currency)}", Culture);
        set { if (TryParseDecimal(value, out var v)) Price = v; }
    }

    public string TaxesText
    {
        get => _taxes.ToString($"N{GetDecimalDigits(Currency)}", Culture);
        set { if (TryParseDecimal(value, out var v)) Taxes = v; }
    }

    public string ExchangeRateText
    {
        get => _exchangeRateToBase.ToString($"N{RateDecimalDigits}", Culture);
        set { if (TryParseDecimal(value, out var v)) ExchangeRateToBase = v; }
    }

    public string PaidAmountText
    {
        get => _paidAmount.ToString($"N{GetDecimalDigits(BaseCurrency)}", Culture);
        set { if (TryParseDecimal(value, out var v)) PaidAmount = v; }
    }

    public static ExpenseEditorViewModel FromExpense(ExpenseItem expense)
    {
        return new ExpenseEditorViewModel
        {
            Id = expense.Id,
            IsActive = expense.IsActive,
            Title = expense.Title,
            Type = expense.Type ?? "",
            Company = expense.Company ?? "",
            Link = expense.Link ?? "",
            Notes = expense.Notes ?? "",
            Price = expense.Price,
            Taxes = expense.Taxes,
            People = Math.Max(1, expense.People),
            Quantity = Math.Max(1, expense.Quantity),
            Currency = NormalizeCurrency(expense.Currency),
            ExchangeRateToBase = expense.ExchangeRateToBase <= 0 ? 1m : expense.ExchangeRateToBase,
            UseFixedRate = expense.UseFixedRate,
            PaidAmount = expense.PaidAmount
        };
    }

    public ExpenseItem ToExpenseItem()
    {
        return new ExpenseItem
        {
            Id = string.IsNullOrWhiteSpace(Id) ? $"gasto-{Guid.NewGuid():N}" : Id.Trim(),
            IsActive = IsActive,
            Title = Title.Trim(),
            Type = string.IsNullOrWhiteSpace(Type) ? null : Type.Trim(),
            Company = string.IsNullOrWhiteSpace(Company) ? null : Company.Trim(),
            Link = string.IsNullOrWhiteSpace(Link) ? null : Link.Trim(),
            Notes = string.IsNullOrWhiteSpace(Notes) ? null : Notes.Trim(),
            Price = Price,
            Taxes = Taxes,
            People = Math.Max(1, People),
            Quantity = Math.Max(1, Quantity),
            Currency = NormalizeCurrency(Currency),
            ExchangeRateToBase = ExchangeRateToBase <= 0 ? 1m : ExchangeRateToBase,
            UseFixedRate = UseFixedRate,
            PaidAmount = PaidAmount
        };
    }

    protected override void OnPropertyChanged(string propertyName)
    {
        base.OnPropertyChanged(propertyName);
        if (propertyName is nameof(IsActive) or nameof(Price) or nameof(Taxes) or nameof(People) or nameof(Quantity) or nameof(ExchangeRateToBase) or nameof(PaidAmount))
        {
            base.OnPropertyChanged(nameof(Subtotal));
            base.OnPropertyChanged(nameof(SubtotalBase));
            base.OnPropertyChanged(nameof(PendingAmount));
            base.OnPropertyChanged(nameof(ActiveGlyph));
            base.OnPropertyChanged(nameof(ActiveStatusLabel));
            base.OnPropertyChanged(nameof(SummaryLine));
            base.OnPropertyChanged(nameof(QuantityLine));
            base.OnPropertyChanged(nameof(SubtotalBaseLabel));
            base.OnPropertyChanged(nameof(PaidLabel));
            base.OnPropertyChanged(nameof(PendingLabel));
            base.OnPropertyChanged(nameof(PaymentStatusLabel));
            base.OnPropertyChanged(nameof(IsPaymentSettled));
        }

        if (propertyName is nameof(Price))
            base.OnPropertyChanged(nameof(PriceText));
        if (propertyName is nameof(Taxes))
            base.OnPropertyChanged(nameof(TaxesText));
        if (propertyName is nameof(ExchangeRateToBase))
            base.OnPropertyChanged(nameof(ExchangeRateText));
        if (propertyName is nameof(PaidAmount))
            base.OnPropertyChanged(nameof(PaidAmountText));

        if (propertyName is nameof(Type) or nameof(Currency))
        {
            base.OnPropertyChanged(nameof(TypeLabel));
            base.OnPropertyChanged(nameof(SummaryLine));
            base.OnPropertyChanged(nameof(IsBaseCurrency));
        }

        if (propertyName is nameof(Currency))
        {
            base.OnPropertyChanged(nameof(PriceText));
            base.OnPropertyChanged(nameof(TaxesText));
            base.OnPropertyChanged(nameof(ExchangeRateText));
        }

        if (propertyName is nameof(UseFixedRate))
        {
            base.OnPropertyChanged(nameof(UseAutomaticRate));
            if (!UseFixedRate)
            {
                var rate = CurrencyRates.FirstOrDefault(r =>
                    string.Equals(r.Currency, NormalizeCurrency(Currency), StringComparison.OrdinalIgnoreCase));
                if (rate is not null)
                    ExchangeRateToBase = rate.RateToBase;
            }
        }

        if (propertyName is nameof(Company) or nameof(Notes))
        {
            base.OnPropertyChanged(nameof(DetailLine));
        }

        if (propertyName is nameof(Link))
        {
            base.OnPropertyChanged(nameof(HasLink));
        }
    }

    private static string NormalizeCurrency(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? "BRL" : value.Trim().ToUpperInvariant();
    }

    private static string FormatCompactDecimal(decimal value)
    {
        return value.ToString("0.######", Culture);
    }

    private static int GetDecimalDigits(string currency)
    {
        var rate = CurrencyRates.FirstOrDefault(r => string.Equals(r.Currency, NormalizeCurrency(currency), StringComparison.OrdinalIgnoreCase));
        return rate?.DecimalDigits ?? 2;
    }

    private static string FormatBaseCurrencyAmount(decimal value)
        => $"{BaseCurrency} {value.ToString($"N{GetDecimalDigits(BaseCurrency)}", Culture)}";

    private static bool TryParseDecimal(string? value, out decimal result)
    {
        return decimal.TryParse(value, System.Globalization.NumberStyles.Any, Culture, out result);
    }

    private static string FormatCurrencyAmount(string currency, decimal value)
    {
        var digits = GetDecimalDigits(currency);
        return value.ToString($"N{digits}", Culture);
    }
}

public sealed class ExpenseCategoryGroupViewModel : NotifyObject
{
    private string _totalLabel;

    public ExpenseCategoryGroupViewModel(string name, string totalLabel, IReadOnlyList<ExpenseEditorViewModel> items)
    {
        Name = name;
        _totalLabel = totalLabel;
        Items = items;
    }

    public string Name { get; }
    public string TotalLabel { get => _totalLabel; set => SetField(ref _totalLabel, value); }
    public IReadOnlyList<ExpenseEditorViewModel> Items { get; }
}

public sealed class CurrencyRateViewModel : NotifyObject
{
    private static int _rateDecimalDigits = 2;
    public static void ConfigureRateDecimalDigits(int digits) => _rateDecimalDigits = digits;

    private string _currency = "BRL";
    private string _symbol = "BRL";
    private string _name = "BRL";
    private int _decimalDigits = 2;
    private decimal _rateToBase = 1m;
    private DateTime? _updatedAt;

    public string Currency { get => _currency; set => SetField(ref _currency, NormalizeCurrency(value)); }
    public string Symbol { get => _symbol; set => SetField(ref _symbol, string.IsNullOrWhiteSpace(value) ? Currency : value.Trim()); }
    public string Name { get => _name; set => SetField(ref _name, string.IsNullOrWhiteSpace(value) ? Currency : value.Trim()); }
    public int DecimalDigits { get => _decimalDigits; set => SetField(ref _decimalDigits, Math.Clamp(value, 0, 6)); }
    public decimal RateToBase { get => _rateToBase; set => SetField(ref _rateToBase, value <= 0 ? 1m : value); }
    public DateTime? UpdatedAt { get => _updatedAt; set => SetField(ref _updatedAt, value); }
    public bool IsBaseCurrency { get; set; }
    public string RateLabel => RateToBase.ToString($"N{_rateDecimalDigits}", CultureInfo.GetCultureInfo("pt-BR"));
    public string RateText
    {
        get => RateLabel;
        set
        {
            if (decimal.TryParse(value, NumberStyles.Number, CultureInfo.GetCultureInfo("pt-BR"), out var parsed) ||
                decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out parsed))
            {
                RateToBase = parsed;
            }
        }
    }
    public string UpdatedAtLabel => UpdatedAt?.ToString("dd/MM/yyyy HH:mm", CultureInfo.GetCultureInfo("pt-BR")) ?? "manual";

    public static CurrencyRateViewModel FromCurrencyRate(CurrencyRateItem rate)
    {
        var currency = NormalizeCurrency(rate.Currency);
        var symbol = string.IsNullOrWhiteSpace(rate.Symbol) || (!string.Equals(currency, "BRL", StringComparison.OrdinalIgnoreCase) && string.Equals(rate.Symbol, "BRL", StringComparison.OrdinalIgnoreCase))
            ? currency
            : rate.Symbol.Trim();
        var name = string.IsNullOrWhiteSpace(rate.Name) || (!string.Equals(currency, "BRL", StringComparison.OrdinalIgnoreCase) && string.Equals(rate.Name, "BRL", StringComparison.OrdinalIgnoreCase))
            ? currency
            : rate.Name.Trim();
        return new CurrencyRateViewModel
        {
            Currency = currency,
            Symbol = symbol,
            Name = name,
            DecimalDigits = rate.DecimalDigits is < 0 or > 6 ? 2 : rate.DecimalDigits,
            RateToBase = rate.RateToBase <= 0 ? 1m : rate.RateToBase,
            UpdatedAt = rate.UpdatedAt
        };
    }

    public CurrencyRateItem ToCurrencyRateItem()
    {
        return new CurrencyRateItem
        {
            Currency = NormalizeCurrency(Currency),
            Symbol = string.IsNullOrWhiteSpace(Symbol) ? NormalizeCurrency(Currency) : Symbol.Trim(),
            Name = string.IsNullOrWhiteSpace(Name) ? NormalizeCurrency(Currency) : Name.Trim(),
            DecimalDigits = Math.Clamp(DecimalDigits, 0, 6),
            RateToBase = RateToBase <= 0 ? 1m : RateToBase,
            UpdatedAt = UpdatedAt
        };
    }

    protected override void OnPropertyChanged(string propertyName)
    {
        base.OnPropertyChanged(propertyName);
        if (propertyName is nameof(Currency))
        {
            if (string.IsNullOrWhiteSpace(Symbol))
            {
                Symbol = Currency;
            }
            if (string.IsNullOrWhiteSpace(Name))
            {
                Name = Currency;
            }
        }

        if (propertyName is nameof(UpdatedAt))
        {
            base.OnPropertyChanged(nameof(UpdatedAtLabel));
        }
        if (propertyName is nameof(RateToBase) or nameof(DecimalDigits))
        {
            base.OnPropertyChanged(nameof(RateLabel));
            base.OnPropertyChanged(nameof(RateText));
        }
    }

    private static string NormalizeCurrency(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? "BRL" : value.Trim().ToUpperInvariant();
    }
}

public sealed record BudgetCategoryViewModel(
    string Name,
    string TotalLabel,
    decimal Total,
    double Share,
    string ShareLabel,
    string Color,
    double BarWidth)
{
    public System.Windows.GridLength BarGridLength =>
        new System.Windows.GridLength(Math.Max(BarWidth, 0.001), System.Windows.GridUnitType.Star);
    public System.Windows.GridLength FillGridLength =>
        new System.Windows.GridLength(Math.Max(1.0 - BarWidth, 0.001), System.Windows.GridUnitType.Star);
}

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
