using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows;
using UltraViagem.Core;
using Forms = System.Windows.Forms;

namespace UltraViagem.App;

public partial class MainWindow : Window
{
    private readonly AppViewModel _viewModel = new();
    private TripRepository? _repository;
    private bool _isLoadingTrip;
    private bool _isSavingTasks;
    private bool _isSavingTips;

    public MainWindow()
    {
        InitializeComponent();
        DataContext = _viewModel;
        _viewModel.TasksChanged += ViewModel_TasksChanged;
        _viewModel.TipsChanged += ViewModel_TipsChanged;

        LoadRepository(LocalSettings.Load().RepositoryPath ?? FindWorkspaceRoot());
        ShowOverview();
    }

    private void SelectRepository_Click(object sender, RoutedEventArgs e)
    {
        using var dialog = new Forms.FolderBrowserDialog
        {
            Description = "Selecione a pasta raiz do UltraViagem",
            InitialDirectory = Directory.Exists(_viewModel.RootPath) ? _viewModel.RootPath : Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            UseDescriptionForTitle = true
        };

        if (dialog.ShowDialog() != Forms.DialogResult.OK)
        {
            return;
        }

        LoadRepository(dialog.SelectedPath);
        LocalSettings.Save(new LocalSettings { RepositoryPath = dialog.SelectedPath });
    }

    private void OpenTripSelection_Click(object sender, RoutedEventArgs e)
    {
        if (_repository is null)
        {
            return;
        }

        var config = _repository.LoadOrCreateConfig();
        var window = new TripSelectionWindow(_viewModel.TripSelectionItems.ToList()) { Owner = this };
        window.FavoriteChanged += (_, item) => SetFavorite(item.Id, item.IsFavorite);
        if (window.ShowDialog() != true)
        {
            return;
        }

        if (window.CreateNewTripRequested)
        {
            CreateNewTrip();
            return;
        }

        if (!string.IsNullOrWhiteSpace(window.SelectedTripId))
        {
            LoadTrip(window.SelectedTripId);
        }
    }

    private void OpenTripFolder_Click(object sender, RoutedEventArgs e)
    {
        if (!Directory.Exists(_viewModel.TripPath))
        {
            return;
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = _viewModel.TripPath,
            UseShellExecute = true
        });
    }

    private void OpenRepositoryFolder_Click(object sender, RoutedEventArgs e)
    {
        if (!Directory.Exists(_viewModel.RootPath))
        {
            return;
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = _viewModel.RootPath,
            UseShellExecute = true
        });
    }

    private void NewTrip_Click(object sender, RoutedEventArgs e)
    {
        CreateNewTrip();
    }

    private void CreateNewTrip()
    {
        if (_repository is null)
        {
            return;
        }

        var draft = new TripDetailsDraft
        {
            Title = "Nova Viagem",
            StartDate = DateTime.Today.ToString("yyyy-MM-dd"),
            EndDate = DateTime.Today.AddDays(7).ToString("yyyy-MM-dd"),
            People = 1,
            BaseCurrency = "BRL"
        };

        var window = new TripDetailsWindow(draft, "Nova Viagem") { Owner = this };
        if (window.ShowDialog() != true)
        {
            return;
        }

        var id = CreateUniqueTripId(window.Draft.Title);
        var trip = new Trip { Id = id };
        ApplyDraftToTrip(window.Draft, trip);
        _repository.SaveTrip(trip);
        AddRecentTrip(id);
        RefreshTrips(id);
        _viewModel.StatusMessage = $"Viagem {trip.Title} criada.";
    }

    private void EditTrip_Click(object sender, RoutedEventArgs e)
    {
        if (_repository is null || _viewModel.CurrentTrip is null)
        {
            return;
        }

        var draft = TripDetailsWindow.FromTrip(_viewModel.CurrentTrip);
        var window = new TripDetailsWindow(draft, "Editar Viagem") { Owner = this };
        if (window.ShowDialog() != true)
        {
            return;
        }

        ApplyDraftToTrip(window.Draft, _viewModel.CurrentTrip);
        _repository.SaveTrip(_viewModel.CurrentTrip);
        LoadTrip(_viewModel.CurrentTrip.Id);
        _viewModel.StatusMessage = $"Viagem {window.Draft.Title} atualizada.";
    }

    private void ShowOverview_Click(object sender, RoutedEventArgs e)
    {
        ShowOverview();
    }

    private void ShowTasks_Click(object sender, RoutedEventArgs e)
    {
        ShowTasks();
    }

    private void AddTask_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.AddTask();
        SaveTasksInternal("Nova tarefa salva automaticamente.");
    }

    private void DeleteTask_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.DeleteSelectedTask();
        SaveTasksInternal("Tarefa removida e salva.");
    }

    private void SaveTasks_Click(object sender, RoutedEventArgs e)
    {
        SaveTasksInternal($"Tarefas salvas em {DateTime.Now:HH:mm}.");
    }

    private void ShowTips_Click(object sender, RoutedEventArgs e)
    {
        ShowTips();
    }

    private void AddTip_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.AddTip();
        SaveTipsInternal("Nova dica salva automaticamente.");
    }

    private void DeleteTip_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.DeleteSelectedTip();
        SaveTipsInternal("Dica removida e salva.");
    }

    private void SaveTips_Click(object sender, RoutedEventArgs e)
    {
        SaveTipsInternal($"Dicas salvas em {DateTime.Now:HH:mm}.");
    }

    private void OpenSelectedTip_Click(object sender, RoutedEventArgs e)
    {
        var url = _viewModel.SelectedTip?.Url;
        if (string.IsNullOrWhiteSpace(url))
        {
            _viewModel.StatusMessage = "Selecione uma dica com link para abrir.";
            return;
        }

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            _viewModel.StatusMessage = "O link selecionado não parece ser uma URL válida.";
            return;
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = uri.ToString(),
            UseShellExecute = true
        });
    }

    private void ShowAllTasks_Click(object sender, RoutedEventArgs e) => _viewModel.TaskFilter = "all";
    private void ShowPendingTasks_Click(object sender, RoutedEventArgs e) => _viewModel.TaskFilter = "pending";
    private void ShowDoneTasks_Click(object sender, RoutedEventArgs e) => _viewModel.TaskFilter = "done";

    private void ViewModel_TasksChanged(object? sender, EventArgs e)
    {
        if (_isLoadingTrip || _isSavingTasks)
        {
            return;
        }

        SaveTasksInternal($"Salvo automaticamente às {DateTime.Now:HH:mm}.");
    }

    private void ViewModel_TipsChanged(object? sender, EventArgs e)
    {
        if (_isLoadingTrip || _isSavingTips)
        {
            return;
        }

        SaveTipsInternal($"Salvo automaticamente às {DateTime.Now:HH:mm}.");
    }

    private void LoadRepository(string rootPath)
    {
        _repository = new TripRepository(rootPath);
        var config = _repository.LoadOrCreateConfig();
        var tripIds = _repository.GetTripIds();
        var selectedTripId = config.RecentTrips.FirstOrDefault(tripIds.Contains) ?? tripIds.FirstOrDefault();
        var selectionItems = BuildTripSelectionItems(tripIds, config);

        _isLoadingTrip = true;
        _viewModel.SetTripsRoot(rootPath, tripIds, selectionItems, selectedTripId);
        _isLoadingTrip = false;

        LoadTrip(selectedTripId);
        _viewModel.StatusMessage = tripIds.Count == 0
            ? "Repositório selecionado. Nenhuma viagem encontrada ainda."
            : $"Repositório selecionado com {tripIds.Count} viagem(ns).";
    }

    private void LoadTrip(string? tripId)
    {
        if (_repository is null || string.IsNullOrWhiteSpace(tripId))
        {
            _isLoadingTrip = true;
            _viewModel.LoadTrip(null);
            _isLoadingTrip = false;
            return;
        }

        var trip = _repository.LoadTrip(tripId);
        _isLoadingTrip = true;
        _viewModel.LoadTrip(trip);
        _isLoadingTrip = false;

        var config = _repository.LoadOrCreateConfig();
        config.RecentTrips.Remove(tripId);
        config.RecentTrips.Insert(0, tripId);
        _repository.SaveConfig(config);
        RefreshTripSelectionItems(config);

        _viewModel.StatusMessage = trip is null
            ? $"Não foi possível abrir {tripId}."
            : $"Viagem {trip.Title} carregada.";
    }

    private void ShowOverview()
    {
        OverviewPanel.Visibility = Visibility.Visible;
        TasksPanel.Visibility = Visibility.Collapsed;
        TipsPanel.Visibility = Visibility.Collapsed;
        SetActiveNav(OverviewNavButton);
    }

    private void ShowTasks()
    {
        OverviewPanel.Visibility = Visibility.Collapsed;
        TasksPanel.Visibility = Visibility.Visible;
        TipsPanel.Visibility = Visibility.Collapsed;
        SetActiveNav(TasksNavButton);
    }

    private void ShowTips()
    {
        OverviewPanel.Visibility = Visibility.Collapsed;
        TasksPanel.Visibility = Visibility.Collapsed;
        TipsPanel.Visibility = Visibility.Visible;
        SetActiveNav(TipsNavButton);
    }

    private void SetActiveNav(System.Windows.Controls.Button activeButton)
    {
        var accentBrush = (System.Windows.Media.Brush)FindResource("AccentBrush");
        foreach (var button in new[] { OverviewNavButton, TasksNavButton, TipsNavButton })
        {
            button.Foreground = System.Windows.Media.Brushes.Black;
            button.Background = System.Windows.Media.Brushes.Transparent;
            button.FontWeight = FontWeights.Normal;
        }

        activeButton.Foreground = System.Windows.Media.Brushes.White;
        activeButton.Background = accentBrush;
        activeButton.FontWeight = FontWeights.SemiBold;
    }

    private void SaveTasksInternal(string message)
    {
        if (_repository is null || _viewModel.CurrentTrip is null)
        {
            _viewModel.StatusMessage = "Nenhuma viagem carregada para salvar.";
            return;
        }

        _isSavingTasks = true;
        _viewModel.ApplyTasksToTrip();
        _repository.SaveTrip(_viewModel.CurrentTrip);
        _viewModel.StatusMessage = message;
        _isSavingTasks = false;
    }

    private void SaveTipsInternal(string message)
    {
        if (_repository is null || _viewModel.CurrentTrip is null)
        {
            _viewModel.StatusMessage = "Nenhuma viagem carregada para salvar.";
            return;
        }

        _isSavingTips = true;
        _viewModel.ApplyTipsToTrip();
        _repository.SaveTrip(_viewModel.CurrentTrip);
        _viewModel.StatusMessage = message;
        _isSavingTips = false;
    }

    private void RefreshTrips(string selectedTripId)
    {
        if (_repository is null)
        {
            return;
        }

        var tripIds = _repository.GetTripIds();
        var config = _repository.LoadOrCreateConfig();
        var selectionItems = BuildTripSelectionItems(tripIds, config);
        _isLoadingTrip = true;
        _viewModel.SetTripsRoot(_repository.RootPath, tripIds, selectionItems, selectedTripId);
        _isLoadingTrip = false;
        LoadTrip(selectedTripId);
    }

    private void AddRecentTrip(string tripId)
    {
        if (_repository is null)
        {
            return;
        }

        var config = _repository.LoadOrCreateConfig();
        config.RecentTrips.Remove(tripId);
        config.RecentTrips.Insert(0, tripId);
        _repository.SaveConfig(config);
    }

    private void SetFavorite(string tripId, bool isFavorite)
    {
        if (_repository is null)
        {
            return;
        }

        var config = _repository.LoadOrCreateConfig();
        if (isFavorite)
        {
            if (!config.FavoriteTrips.Contains(tripId))
            {
                config.FavoriteTrips.Add(tripId);
            }
        }
        else
        {
            config.FavoriteTrips.Remove(tripId);
        }

        _repository.SaveConfig(config);
        RefreshTripSelectionItems(config);
    }

    private void RefreshTripSelectionItems(AppConfig config)
    {
        if (_repository is null)
        {
            return;
        }

        _viewModel.TripSelectionItems.ReplaceWith(BuildTripSelectionItems(_repository.GetTripIds(), config));
    }

    private IReadOnlyList<TripSelectionItem> BuildTripSelectionItems(IReadOnlyList<string> tripIds, AppConfig config)
    {
        if (_repository is null)
        {
            return [];
        }

        var items = new List<TripSelectionItem>();
        foreach (var tripId in tripIds)
        {
            var trip = _repository.LoadTrip(tripId);
            if (trip is null)
            {
                continue;
            }

            var year = trip.StartDate?.Year ?? 0;
            if (year == 0 && trip.EndDate is not null)
            {
                year = trip.EndDate.Value.Year;
            }

            if (year == 0)
            {
                year = 9999;
            }

            var dateLabel = BuildSelectionDateLabel(trip);
            items.Add(new TripSelectionItem(
                trip.Id,
                trip.Title,
                year,
                dateLabel,
                config.FavoriteTrips.Contains(trip.Id)));
        }

        return items;
    }

    private static string BuildSelectionDateLabel(Trip trip)
    {
        if (trip.StartDate is not null && trip.EndDate is not null)
        {
            return $"{trip.StartDate:dd MMM yyyy} - {trip.EndDate:dd MMM yyyy}";
        }

        return "Datas a definir";
    }

    private string CreateUniqueTripId(string title)
    {
        if (_repository is null)
        {
            return Slugify(title);
        }

        var baseId = Slugify(title);
        var id = baseId;
        var suffix = 2;
        while (Directory.Exists(Path.Combine(_repository.RootPath, id)))
        {
            id = $"{baseId}-{suffix}";
            suffix++;
        }

        return id;
    }

    private static void ApplyDraftToTrip(TripDetailsDraft draft, Trip trip)
    {
        trip.Title = draft.Title;
        trip.StartDate = DateOnly.TryParse(draft.StartDate, out var startDate) ? startDate : null;
        trip.EndDate = DateOnly.TryParse(draft.EndDate, out var endDate) ? endDate : null;
        trip.People = draft.People;
        trip.BaseCurrency = draft.BaseCurrency;
        trip.MyMapsUrl = draft.MyMapsUrl;
    }

    private static string Slugify(string value)
    {
        var normalized = value.Trim().ToLowerInvariant()
            .Replace("á", "a").Replace("à", "a").Replace("ã", "a").Replace("â", "a")
            .Replace("é", "e").Replace("ê", "e")
            .Replace("í", "i")
            .Replace("ó", "o").Replace("õ", "o").Replace("ô", "o")
            .Replace("ú", "u")
            .Replace("ç", "c");
        normalized = Regex.Replace(normalized, @"[^a-z0-9]+", "-").Trim('-');
        return string.IsNullOrWhiteSpace(normalized) ? $"viagem-{DateTime.Now:yyyyMMddHHmmss}" : normalized;
    }

    private static string FindWorkspaceRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "config.json")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        return Directory.GetCurrentDirectory();
    }
}

public sealed class LocalSettings
{
    public string? RepositoryPath { get; set; }

    private static string SettingsPath
    {
        get
        {
            var directory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "UltraViagem");
            Directory.CreateDirectory(directory);
            return Path.Combine(directory, "settings.json");
        }
    }

    public static LocalSettings Load()
    {
        if (!File.Exists(SettingsPath))
        {
            return new LocalSettings();
        }

        var json = File.ReadAllText(SettingsPath);
        return JsonSerializer.Deserialize<LocalSettings>(json) ?? new LocalSettings();
    }

    public static void Save(LocalSettings settings)
    {
        var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(SettingsPath, json);
    }
}
