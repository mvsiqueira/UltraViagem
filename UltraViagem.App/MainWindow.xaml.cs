using System.Diagnostics;
using System.Collections;
using System.Windows.Media.Animation;
using System.Collections.Specialized;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using UltraViagem.Core;
using Forms = System.Windows.Forms;

namespace UltraViagem.App;

public partial class MainWindow : Window
{
    private readonly AppViewModel _viewModel = new();
    private readonly HttpClient _httpClient = new();
    private readonly DispatcherTimer _expensesSaveTimer = new() { Interval = TimeSpan.FromMilliseconds(450) };
    private TripRepository? _repository;
    private readonly Dictionary<LinkEditorViewModel, (string Title, string Url)> _tipEditSnapshots = [];
    private readonly Dictionary<AttachmentEditorViewModel, string> _attachmentEditSnapshots = [];
    private readonly Dictionary<ExpenseEditorViewModel, ExpenseItem> _expenseEditSnapshots = [];
    private readonly HashSet<ExpenseEditorViewModel> _newExpenseEdits = [];
    private readonly Dictionary<TaskEditorViewModel, TaskItem> _taskEditSnapshots = [];
    private readonly HashSet<TaskEditorViewModel> _newTaskEdits = [];
    private LocalSettings _settings = new();
    private System.Windows.Point _attachmentDragStartPoint;
    private System.Windows.Point _taskDragStartPoint;
    private System.Windows.Point _tipDragStartPoint;
    private System.Windows.Point _expenseDragStartPoint;
    private DependencyObject? _expenseDragSource;
    private bool _pendingExpenseDragIsGroup;
    private bool _isLoadingTrip;
    private bool _isSavingTasks;
    private bool _isSavingTips;
    private bool _isSavingAttachments;

    // Itinerary drag state
    private ItineraryActivityViewModel? _draggingActivity;
    private ItineraryActivityViewModel? _resizingActivity;
    private bool _resizingLeft;
    private ItineraryDayViewModel? _activitySourceDay;
    private ItineraryDayViewModel? _activityDragTargetDay;
    private BankRowViewModel? _activitySourceBankRow;
    private BankRowViewModel? _activityDragTargetBankRow;
    private System.Windows.Point _activityDragOriginPoint;
    private double _activityDragGrabOffset; // pixels from block left edge where grab occurred
    private int _activityOriginSlot;
    private int _activityOriginDuration;
    private bool _activityDragMoved;
    private ItineraryDayViewModel? _activityCurrentDay;     // container visual atual durante drag
    private BankRowViewModel? _activityCurrentBankRow;

    private static readonly HashSet<string> _windowsReservedNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "CON", "PRN", "AUX", "NUL",
        "COM0", "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT0", "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"
    };
    private bool _isSavingExpenses;
    private bool _typeComboOpen;

    public MainWindow()
    {
        InitializeComponent();
        _settings = LocalSettings.Load();
        RestoreWindowPlacement(_settings);
        DataContext = _viewModel;
        _viewModel.TasksChanged += ViewModel_TasksChanged;
        _viewModel.TipsChanged += ViewModel_TipsChanged;
        _viewModel.AttachmentsChanged += ViewModel_AttachmentsChanged;
        _viewModel.ExpensesChanged += ViewModel_ExpensesChanged;
        _viewModel.ItineraryChanged += ViewModel_ItineraryChanged;
        _expensesSaveTimer.Tick += ExpensesSaveTimer_Tick;
        Closing += MainWindow_Closing;

        LoadRepository(_settings.RepositoryPath ?? FindWorkspaceRoot());
        ShowOverview();

        if (_settings.IsSidebarCollapsed)
        {
            _viewModel.IsSidebarExpanded = false;
            SidebarBorder.Width = 68.0;
            RepoExpandedPanel.Visibility = Visibility.Collapsed;
            RepoCollapsedButton.Visibility = Visibility.Visible;
            LogoButton.Padding = new Thickness(0);
            LogoButton.HorizontalContentAlignment = System.Windows.HorizontalAlignment.Center;
        }
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
        _settings.RepositoryPath = dialog.SelectedPath;
        LocalSettings.Save(_settings);
    }

    private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        var bounds = WindowState == WindowState.Normal ? new Rect(Left, Top, Width, Height) : RestoreBounds;
        _settings.WindowLeft = bounds.Left;
        _settings.WindowTop = bounds.Top;
        _settings.WindowWidth = bounds.Width;
        _settings.WindowHeight = bounds.Height;
        _settings.WindowState = WindowState == WindowState.Maximized ? nameof(WindowState.Maximized) : nameof(WindowState.Normal);
        LocalSettings.Save(_settings);
    }

    private void RestoreWindowPlacement(LocalSettings settings)
    {
        if (settings.WindowLeft is not { } left ||
            settings.WindowTop is not { } top ||
            settings.WindowWidth is not { } width ||
            settings.WindowHeight is not { } height)
        {
            return;
        }

        width = Math.Max(width, MinWidth);
        height = Math.Max(height, MinHeight);
        if (!IsWindowPlacementVisible(left, top, width, height))
        {
            return;
        }

        WindowStartupLocation = WindowStartupLocation.Manual;
        Left = left;
        Top = top;
        Width = width;
        Height = height;

        if (string.Equals(settings.WindowState, nameof(WindowState.Maximized), StringComparison.Ordinal))
        {
            WindowState = WindowState.Maximized;
        }
    }

    private static bool IsWindowPlacementVisible(double left, double top, double width, double height)
    {
        var windowBounds = new System.Drawing.Rectangle(
            (int)Math.Round(left),
            (int)Math.Round(top),
            (int)Math.Round(Math.Max(width, 1)),
            (int)Math.Round(Math.Max(height, 1)));

        return Forms.Screen.AllScreens.Any(screen => screen.WorkingArea.IntersectsWith(windowBounds));
    }

    private void ShowAbout_Click(object sender, RoutedEventArgs e)
    {
        var window = new AboutWindow { Owner = this };
        window.ShowDialog();
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

        var id = CreateUniqueTripId(window.Draft.Title, window.Draft.StartDate);
        var trip = new Trip { Id = id };
        ApplyDraftToTrip(window.Draft, trip);
        _repository.SaveTrip(trip);
        AddRecentTrip(id);
        RefreshTrips(id);
        _viewModel.StatusMessage = $"Viagem {trip.Title} criada.";
    }

    private void EditTrip_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel.CurrentTrip is null)
        {
            return;
        }

        PopulateTripDetailsPanel();
        ShowTripDetails();
    }

    private void DeleteTrip_Click(object sender, RoutedEventArgs e)
    {
        if (_repository is null || _viewModel.CurrentTrip is null)
        {
            return;
        }

        var trip = _viewModel.CurrentTrip;
        var result = System.Windows.MessageBox.Show(
            $"Excluir permanentemente a viagem \"{trip.Title}\"?\n\nTodos os arquivos da pasta serão removidos. Essa ação não pode ser desfeita.",
            "Excluir Viagem",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result != MessageBoxResult.Yes)
        {
            return;
        }

        var tripId = trip.Id;
        var tripPath = Path.Combine(_repository.RootPath, tripId);

        try
        {
            Directory.Delete(tripPath, recursive: true);
        }
        catch (IOException ex)
        {
            System.Windows.MessageBox.Show($"Erro ao excluir a viagem:\n{ex.Message}", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }
        catch (UnauthorizedAccessException ex)
        {
            System.Windows.MessageBox.Show($"Sem permissão para excluir a viagem:\n{ex.Message}", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        var config = _repository.LoadOrCreateConfig();
        config.RecentTrips.Remove(tripId);
        config.FavoriteTrips.Remove(tripId);
        _repository.SaveConfig(config);

        var nextTripId = _repository.GetTripIds().FirstOrDefault() ?? "";
        ShowOverview();
        RefreshTrips(nextTripId);
        _viewModel.StatusMessage = $"Viagem \"{trip.Title}\" excluída.";
    }

    private void CopyTrip_Click(object sender, RoutedEventArgs e)
    {
        if (_repository is null || _viewModel.CurrentTrip is null)
        {
            return;
        }

        var source = _viewModel.CurrentTrip;
        var draft = new TripDetailsDraft
        {
            Title = $"Cópia de {source.Title}",
            StartDate = source.StartDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? "",
            EndDate = source.EndDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? "",
            People = source.People,
            BaseCurrency = source.BaseCurrency,
            MyMapsUrl = source.MyMapsUrl
        };

        var window = new TripDetailsWindow(draft, "Copiar Viagem") { Owner = this };
        if (window.ShowDialog() != true)
        {
            return;
        }

        var sourceId = source.Id;
        var newId = CreateUniqueTripId(window.Draft.Title, window.Draft.StartDate);

        try
        {
            _repository.CopyTripFolder(sourceId, newId);
        }
        catch (IOException ex)
        {
            System.Windows.MessageBox.Show($"Erro ao copiar a pasta da viagem:\n{ex.Message}", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        var copiedTrip = _repository.LoadTrip(newId) ?? new Trip { Id = newId };
        copiedTrip.Id = newId;
        ApplyDraftToTrip(window.Draft, copiedTrip);
        _repository.SaveTrip(copiedTrip);

        AddRecentTrip(newId);
        RefreshTrips(newId);
        _viewModel.StatusMessage = $"Viagem copiada como \"{copiedTrip.Title}\".";
    }

    private void SaveTripDetails_Click(object sender, RoutedEventArgs e)
    {
        if (_repository is null || _viewModel.CurrentTrip is null)
        {
            return;
        }

        var draft = BuildTripDetailsDraft();
        if (!ValidateTripDetailsDraft(draft))
        {
            return;
        }

        ApplyDraftToTrip(draft, _viewModel.CurrentTrip);
        _viewModel.CurrentTrip.ShowItineraryGrid = TripDetailsGridCheckBox.IsChecked == true;
        _repository.SaveTrip(_viewModel.CurrentTrip);
        LoadTrip(_viewModel.CurrentTrip.Id);
        PopulateTripDetailsPanel();
        ShowTripDetails();
        _viewModel.StatusMessage = $"Viagem {draft.Title} atualizada.";
    }

    private void RenameTripFolder_Click(object sender, RoutedEventArgs e)
    {
        if (_repository is null || _viewModel.CurrentTrip is null)
        {
            return;
        }

        var oldId = _viewModel.CurrentTrip.Id;
        var newId = ShowRenameDialog(oldId, "Renomear pasta da viagem", "Novo nome da pasta");
        if (string.IsNullOrWhiteSpace(newId) || string.Equals(newId, oldId, StringComparison.Ordinal))
        {
            return;
        }

        newId = newId.Trim();
        if (!ValidateTripFolderName(newId))
        {
            return;
        }

        var oldPath = Path.Combine(_repository.RootPath, oldId);
        var newPath = Path.Combine(_repository.RootPath, newId);
        if (Directory.Exists(newPath))
        {
            ShowTripDetailsError("Já existe uma pasta de viagem com esse nome.");
            return;
        }

        try
        {
            Directory.Move(oldPath, newPath);
            _viewModel.CurrentTrip.Id = newId;
            _repository.SaveTrip(_viewModel.CurrentTrip);
            ReplaceTripIdInConfig(oldId, newId);
            RefreshTrips(newId);
            PopulateTripDetailsPanel();
            ShowTripDetails();
            _viewModel.StatusMessage = $"Pasta da viagem renomeada para {newId}.";
        }
        catch (IOException ex)
        {
            ShowTripDetailsError($"Não foi possível renomear a pasta: {ex.Message}");
        }
        catch (UnauthorizedAccessException ex)
        {
            ShowTripDetailsError($"Sem permissão para renomear a pasta: {ex.Message}");
        }
    }

    private void ToggleCurrentTripFavorite_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel.CurrentTrip is null)
        {
            return;
        }

        var isFavorite = !_viewModel.IsCurrentTripFavorite;
        SetFavorite(_viewModel.CurrentTrip.Id, isFavorite);
        _viewModel.IsCurrentTripFavorite = isFavorite;
    }

    private void ToggleSidebar_Click(object sender, RoutedEventArgs e) => ToggleSidebar();

    private void ToggleSidebar()
    {
        var isExpanding = !_viewModel.IsSidebarExpanded;
        var targetWidth = isExpanding ? 220.0 : 68.0;

        if (!isExpanding)
        {
            _viewModel.IsSidebarExpanded = false;
            RepoExpandedPanel.Visibility = Visibility.Collapsed;
            RepoCollapsedButton.Visibility = Visibility.Visible;
            LogoButton.Padding = new Thickness(0);
            LogoButton.HorizontalContentAlignment = System.Windows.HorizontalAlignment.Center;
        }

        var animation = new DoubleAnimation
        {
            To = targetWidth,
            Duration = TimeSpan.FromMilliseconds(200),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
            FillBehavior = FillBehavior.HoldEnd
        };

        if (isExpanding)
        {
            animation.Completed += (_, _) =>
            {
                _viewModel.IsSidebarExpanded = true;
                RepoExpandedPanel.Visibility = Visibility.Visible;
                RepoCollapsedButton.Visibility = Visibility.Collapsed;
                LogoButton.Padding = new Thickness(14, 0, 14, 0);
                LogoButton.HorizontalContentAlignment = System.Windows.HorizontalAlignment.Left;
            };
        }

        SidebarBorder.BeginAnimation(FrameworkElement.WidthProperty, animation);

        _settings.IsSidebarCollapsed = !isExpanding;
        LocalSettings.Save(_settings);
    }

    private void ShowOverview_Click(object sender, RoutedEventArgs e)
    {
        ShowOverview();
    }

    private void ShowTasks_Click(object sender, RoutedEventArgs e)
    {
        ShowTasks();
    }

    private void ShowBudget_Click(object sender, RoutedEventArgs e)
    {
        ShowBudget();
    }

    private void ShowItinerary_Click(object sender, RoutedEventArgs e)
    {
        ShowItinerary();
    }

    private void ExpenseGroupsList_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _expenseDragStartPoint = e.GetPosition(null);
        _expenseDragSource = e.OriginalSource as DependencyObject;
        _pendingExpenseDragIsGroup = FindAncestorWithTag(_expenseDragSource, "GroupHeader") is not null;
    }

    private void ExpenseGroupsList_PreviewMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        var currentPos = e.GetPosition(null);
        if (Math.Abs(currentPos.X - _expenseDragStartPoint.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(currentPos.Y - _expenseDragStartPoint.Y) < SystemParameters.MinimumVerticalDragDistance)
        {
            return;
        }

        if (_pendingExpenseDragIsGroup)
        {
            var group = FindExpenseCategoryGroup(_expenseDragSource);
            if (group is not null)
            {
                System.Windows.DragDrop.DoDragDrop(ExpenseGroupsList, group, System.Windows.DragDropEffects.Move);
            }
        }
        else if (_viewModel.SelectedExpense is { IsEditing: false })
        {
            System.Windows.DragDrop.DoDragDrop(ExpenseGroupsList, _viewModel.SelectedExpense, System.Windows.DragDropEffects.Move);
        }
    }

    private void ExpenseGroupsList_DragOver(object sender, System.Windows.DragEventArgs e)
    {
        if (e.Data.GetDataPresent(typeof(ExpenseCategoryGroupViewModel)))
        {
            e.Effects = System.Windows.DragDropEffects.Move;
        }
        else if (e.Data.GetDataPresent(typeof(ExpenseEditorViewModel)))
        {
            var expense = e.Data.GetData(typeof(ExpenseEditorViewModel)) as ExpenseEditorViewModel;
            var sourceGroup = _viewModel.ExpenseGroups.FirstOrDefault(g => g.Items.Contains(expense));
            var targetGroup = FindExpenseCategoryGroup(e.OriginalSource as DependencyObject);
            e.Effects = sourceGroup is not null && targetGroup is not null && ReferenceEquals(sourceGroup, targetGroup)
                ? System.Windows.DragDropEffects.Move
                : System.Windows.DragDropEffects.None;
        }
        else
        {
            e.Effects = System.Windows.DragDropEffects.None;
        }

        e.Handled = true;
    }

    private void ExpenseGroupsList_Drop(object sender, System.Windows.DragEventArgs e)
    {
        if (e.Data.GetDataPresent(typeof(ExpenseCategoryGroupViewModel)))
        {
            var group = e.Data.GetData(typeof(ExpenseCategoryGroupViewModel)) as ExpenseCategoryGroupViewModel;
            var targetGroup = FindExpenseCategoryGroup(e.OriginalSource as DependencyObject);
            if (group is not null && targetGroup is not null)
            {
                _viewModel.MoveExpenseGroup(group, targetGroup);
            }

            e.Handled = true;
            return;
        }

        if (e.Data.GetDataPresent(typeof(ExpenseEditorViewModel)))
        {
            var expense = e.Data.GetData(typeof(ExpenseEditorViewModel)) as ExpenseEditorViewModel;
            var targetExpense = FindExpenseEditorViewModel(e.OriginalSource as DependencyObject);
            if (expense is not null && targetExpense is not null)
            {
                var sourceGroup = _viewModel.ExpenseGroups.FirstOrDefault(g => g.Items.Contains(expense));
                var targetGroup = _viewModel.ExpenseGroups.FirstOrDefault(g => g.Items.Contains(targetExpense));
                if (sourceGroup is not null && ReferenceEquals(sourceGroup, targetGroup))
                {
                    _viewModel.MoveExpense(expense, targetExpense);
                }
            }

            e.Handled = true;
        }
    }

    private static ExpenseCategoryGroupViewModel? FindExpenseCategoryGroup(DependencyObject? current)
    {
        while (current is not null)
        {
            if (current is ContentPresenter { DataContext: ExpenseCategoryGroupViewModel group })
            {
                return group;
            }

            current = System.Windows.Media.VisualTreeHelper.GetParent(current);
        }

        return null;
    }

    private static ExpenseEditorViewModel? FindExpenseEditorViewModel(DependencyObject? current)
    {
        while (current is not null)
        {
            if (current is ContentPresenter { DataContext: ExpenseEditorViewModel expense })
            {
                return expense;
            }

            current = System.Windows.Media.VisualTreeHelper.GetParent(current);
        }

        return null;
    }

    private static FrameworkElement? FindAncestorWithTag(DependencyObject? current, string tag)
    {
        while (current is not null)
        {
            if (current is FrameworkElement fe && string.Equals(fe.Tag as string, tag, StringComparison.Ordinal))
            {
                return fe;
            }

            current = System.Windows.Media.VisualTreeHelper.GetParent(current);
        }

        return null;
    }

    private void AddExpense_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.AddExpense();
        SaveExpensesInternal("Novo gasto salvo automaticamente.");
        if (_viewModel.SelectedExpense is not null)
        {
            _newExpenseEdits.Add(_viewModel.SelectedExpense);
        }
        BeginExpenseEdit(_viewModel.SelectedExpense);
    }

    private void DeleteExpense_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: ExpenseEditorViewModel expense })
        {
            _viewModel.SelectedExpense = expense;
        }

        _viewModel.DeleteSelectedExpense();
        SaveExpensesInternal("Gasto removido e salvo.");
    }

    private void SelectExpenseCard_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: ExpenseEditorViewModel expense })
        {
            _viewModel.SelectedExpense = expense;
        }
    }

    private void SelectExpenseCard_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: ExpenseEditorViewModel expense })
        {
            return;
        }

        _viewModel.SelectedExpense = expense;
        if (e.ClickCount == 2)
        {
            if (expense.IsEditing)
            {
                RejectExpenseEdit(expense);
            }
            else
            {
                BeginExpenseEdit(expense);
            }

            e.Handled = true;
        }
    }

    private void EditExpense_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: ExpenseEditorViewModel expense })
        {
            _viewModel.SelectedExpense = expense;
        }

        BeginExpenseEdit(_viewModel.SelectedExpense);
    }

    private void CompleteExpenseEdit_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: ExpenseEditorViewModel expense })
        {
            CompleteExpenseEdit(expense);
        }
    }

    private void RejectExpenseEdit_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: ExpenseEditorViewModel expense })
        {
            return;
        }

        RejectExpenseEdit(expense);
    }

    private void ExpenseEditView_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: ExpenseEditorViewModel expense })
        {
            return;
        }

        if (e.Key == Key.Enter)
        {
            CompleteExpenseEdit(expense);
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            RejectExpenseEdit(expense);
            e.Handled = true;
        }
    }

    private void OpenExpenseLink_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: ExpenseEditorViewModel expense })
        {
            _viewModel.SelectedExpense = expense;
            OpenUrl(expense.Link, "Este gasto não tem link cadastrado.");
        }
    }

    private void SaveExpenses_Click(object sender, RoutedEventArgs e)
    {
        AddCurrencyErrorText.Visibility = Visibility.Collapsed;
        SaveExpensesInternal($"Gastos salvos em {DateTime.Now:HH:mm}.");
    }

    private void BeginExpenseEdit(ExpenseEditorViewModel? expense)
    {
        if (expense is null)
        {
            _viewModel.StatusMessage = "Selecione um gasto para editar.";
            return;
        }

        foreach (var item in _viewModel.Expenses)
        {
            item.IsEditing = ReferenceEquals(item, expense);
        }

        if (!_expenseEditSnapshots.ContainsKey(expense))
        {
            _expenseEditSnapshots[expense] = expense.ToExpenseItem();
        }

        _viewModel.SelectedExpense = expense;
    }

    private void CompleteExpenseEdit(ExpenseEditorViewModel expense)
    {
        expense.IsEditing = false;
        _viewModel.SelectedExpense = expense;
        _expenseEditSnapshots.Remove(expense);
        _newExpenseEdits.Remove(expense);
        _viewModel.RefreshBudget();
        SaveExpensesInternal($"Gasto salvo em {DateTime.Now:HH:mm}.");
    }

    private void RejectExpenseEdit(ExpenseEditorViewModel expense)
    {
        if (_newExpenseEdits.Remove(expense))
        {
            _viewModel.SelectedExpense = expense;
            _expenseEditSnapshots.Remove(expense);
            _viewModel.DeleteSelectedExpense();
            SaveExpensesInternal("Novo gasto descartado.");
            return;
        }

        if (_expenseEditSnapshots.Remove(expense, out var snapshot))
        {
            RestoreExpense(expense, snapshot);
        }

        expense.IsEditing = false;
        _viewModel.SelectedExpense = expense;
        _viewModel.RefreshBudget();
        SaveExpensesInternal("Edição descartada.");
    }

    private static void RestoreExpense(ExpenseEditorViewModel expense, ExpenseItem snapshot)
    {
        expense.Id = snapshot.Id;
        expense.IsActive = snapshot.IsActive;
        expense.Title = snapshot.Title;
        expense.Type = snapshot.Type ?? "";
        expense.Company = snapshot.Company ?? "";
        expense.Link = snapshot.Link ?? "";
        expense.Notes = snapshot.Notes ?? "";
        expense.Price = snapshot.Price;
        expense.Taxes = snapshot.Taxes;
        expense.People = snapshot.People;
        expense.Quantity = snapshot.Quantity;
        expense.Currency = snapshot.Currency;
        expense.ExchangeRateToBase = snapshot.ExchangeRateToBase;
        expense.PaidAmount = snapshot.PaidAmount;
    }

    private async void UpdateExchangeRates_Click(object sender, RoutedEventArgs e)
    {
        await UpdateExchangeRatesAsync();
    }

    private async void UpdateSingleExchangeRate_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: CurrencyRateViewModel rate })
        {
            await UpdateExchangeRatesAsync([rate.Currency]);
        }
    }

    private void AddCurrencyRate_Click(object sender, RoutedEventArgs e)
    {
        var currency = NewCurrencyCodeBox.Text.Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(currency))
        {
            ShowAddCurrencyError("Informe o código da moeda.");
            return;
        }

        if (currency.Length != 3)
        {
            ShowAddCurrencyError("Use o código ISO com 3 letras, como USD, CLP ou EUR.");
            return;
        }

        if (_viewModel.CurrencyRates.Any(rate => string.Equals(rate.Currency, currency, StringComparison.OrdinalIgnoreCase)))
        {
            ShowAddCurrencyError($"Moeda {currency} já cadastrada.");
            return;
        }

        AddCurrencyErrorText.Visibility = Visibility.Collapsed;
        var baseCurrency = _viewModel.BaseCurrencyCode;
        _viewModel.AddCurrencyRate(
            currency,
            string.Equals(currency, baseCurrency, StringComparison.OrdinalIgnoreCase) ? 1m : 1m,
            symbol: currency,
            name: currency);
        NewCurrencyCodeBox.Text = "";
        SaveExpensesInternal($"Moeda {currency} cadastrada.");
    }

    private void ShowAddCurrencyError(string message)
    {
        AddCurrencyErrorText.Text = message;
        AddCurrencyErrorText.Visibility = Visibility.Visible;
    }

    private void DeleteCurrencyRate_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: CurrencyRateViewModel rate })
        {
            return;
        }

        if (_viewModel.RemoveCurrencyRate(rate, out var message))
        {
            SaveExpensesInternal(message);
        }
        else
        {
            _viewModel.StatusMessage = message;
            System.Windows.MessageBox.Show(this, message, "Moeda em uso", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private async Task UpdateExchangeRatesAsync(IReadOnlyList<string>? currenciesToUpdate = null)
    {
        if (_viewModel.CurrentTrip is null)
        {
            return;
        }

        var baseCurrency = string.IsNullOrWhiteSpace(_viewModel.CurrentTrip.BaseCurrency)
            ? "BRL"
            : _viewModel.CurrentTrip.BaseCurrency.Trim().ToUpperInvariant();
        if (!string.Equals(baseCurrency, "BRL", StringComparison.OrdinalIgnoreCase))
        {
            _viewModel.StatusMessage = "Atualização automática disponível por enquanto para base BRL.";
            return;
        }

        var currencies = currenciesToUpdate is not null
            ? currenciesToUpdate
                .Select(currency => currency.Trim().ToUpperInvariant())
                .Where(currency => !string.IsNullOrWhiteSpace(currency) && currency != baseCurrency)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList()
            : _viewModel.Expenses
                .Select(expense => expense.Currency.Trim().ToUpperInvariant())
                .Concat(_viewModel.CurrencyRates.Select(rate => rate.Currency.Trim().ToUpperInvariant()))
                .Where(currency => !string.IsNullOrWhiteSpace(currency) && currency != baseCurrency)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        if (currencies.Count == 0)
        {
            _viewModel.StatusMessage = "Nenhuma moeda estrangeira cadastrada para atualizar.";
            return;
        }

        try
        {
            var pairs = string.Join(",", currencies.Select(currency => $"{currency}-{baseCurrency}"));
            using var response = await _httpClient.GetAsync($"https://economia.awesomeapi.com.br/json/last/{pairs}");
            response.EnsureSuccessStatusCode();
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var updated = 0;
            foreach (var currency in currencies)
            {
                var key = $"{currency}{baseCurrency}";
                if (!json.RootElement.TryGetProperty(key, out var item) ||
                    !item.TryGetProperty("bid", out var bidProperty) ||
                    !decimal.TryParse(bidProperty.GetString(), NumberStyles.Number, CultureInfo.InvariantCulture, out var rate))
                {
                    continue;
                }

                _viewModel.AddCurrencyRate(currency, rate, DateTime.Now);
                updated++;
            }

            SaveExpensesInternal(updated == 0
                ? "Nenhuma cotação foi atualizada."
                : $"{updated} cotação(ões) atualizada(s). Itens com valor pago mantiveram a cotação do item.");
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or TaskCanceledException)
        {
            _viewModel.StatusMessage = $"Não foi possível atualizar cotações: {ex.Message}";
        }
    }

    private void AddTask_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.AddTask();
        SaveTasksInternal("Nova tarefa salva automaticamente.");
        if (_viewModel.SelectedTask is not null)
        {
            _newTaskEdits.Add(_viewModel.SelectedTask);
        }
        BeginTaskEdit(_viewModel.SelectedTask);
    }

    private void DeleteTask_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: TaskEditorViewModel task })
        {
            _viewModel.SelectedTask = task;
        }

        _viewModel.DeleteSelectedTask();
        SaveTasksInternal("Tarefa removida e salva.");
    }

    private void TasksList_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _taskDragStartPoint = e.GetPosition(null);
    }

    private void TasksList_PreviewMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || _viewModel.SelectedTask is null || _viewModel.SelectedTask.IsEditing)
        {
            return;
        }

        var currentPosition = e.GetPosition(null);
        if (Math.Abs(currentPosition.X - _taskDragStartPoint.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(currentPosition.Y - _taskDragStartPoint.Y) < SystemParameters.MinimumVerticalDragDistance)
        {
            return;
        }

        System.Windows.DragDrop.DoDragDrop(TasksList, _viewModel.SelectedTask, System.Windows.DragDropEffects.Move);
    }

    private void TasksList_DragOver(object sender, System.Windows.DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(typeof(TaskEditorViewModel))
            ? System.Windows.DragDropEffects.Move
            : System.Windows.DragDropEffects.None;
        e.Handled = true;
    }

    private void TasksList_Drop(object sender, System.Windows.DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(typeof(TaskEditorViewModel)))
        {
            return;
        }

        var task = e.Data.GetData(typeof(TaskEditorViewModel)) as TaskEditorViewModel;
        if (task is null)
        {
            return;
        }

        var targetIndex = GetTaskDropIndex(e.OriginalSource);
        _viewModel.MoveTask(task, targetIndex);
        e.Handled = true;
    }

    private int GetTaskDropIndex(object originalSource)
    {
        var cp = FindAncestor<ContentPresenter>(originalSource as DependencyObject);
        if (cp?.DataContext is TaskEditorViewModel target)
        {
            return _viewModel.Tasks.IndexOf(target);
        }

        return Math.Max(_viewModel.Tasks.Count - 1, 0);
    }

    private void SelectTaskCard_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: TaskEditorViewModel task })
        {
            return;
        }

        _viewModel.SelectedTask = task;
        if (e.ClickCount == 2)
        {
            if (task.IsEditing)
            {
                RejectTaskEdit(task);
            }
            else
            {
                BeginTaskEdit(task);
            }

            e.Handled = true;
        }
    }

    private void EditTask_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: TaskEditorViewModel task })
        {
            _viewModel.SelectedTask = task;
        }

        BeginTaskEdit(_viewModel.SelectedTask);
    }

    private void CompleteTaskEdit_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: TaskEditorViewModel task })
        {
            CompleteTaskEdit(task);
        }
    }

    private void RejectTaskEdit_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: TaskEditorViewModel task })
        {
            return;
        }

        RejectTaskEdit(task);
    }

    private void TaskEditView_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: TaskEditorViewModel task })
        {
            return;
        }

        if (e.Key == Key.Enter && (Keyboard.Modifiers & ModifierKeys.Shift) == 0)
        {
            CompleteTaskEdit(task);
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            RejectTaskEdit(task);
            e.Handled = true;
        }
    }

    private void BeginTaskEdit(TaskEditorViewModel? task)
    {
        if (task is null)
        {
            _viewModel.StatusMessage = "Selecione uma tarefa para editar.";
            return;
        }

        foreach (var item in _viewModel.AllTasks)
        {
            item.IsEditing = ReferenceEquals(item, task);
        }

        if (!_taskEditSnapshots.ContainsKey(task))
        {
            _taskEditSnapshots[task] = task.ToTaskItem();
        }

        _viewModel.SelectedTask = task;

        Dispatcher.BeginInvoke(() =>
        {
            if (FindTaskTitleEditor(task) is { } editor)
            {
                editor.Focus();
                editor.SelectAll();
            }
        });
    }

    private void CompleteTaskEdit(TaskEditorViewModel task)
    {
        task.IsEditing = false;
        _viewModel.SelectedTask = task;
        _taskEditSnapshots.Remove(task);
        _newTaskEdits.Remove(task);
        SaveTasksInternal($"Tarefa salva em {DateTime.Now:HH:mm}.");
    }

    private void RejectTaskEdit(TaskEditorViewModel task)
    {
        if (_newTaskEdits.Remove(task))
        {
            _viewModel.SelectedTask = task;
            _taskEditSnapshots.Remove(task);
            _viewModel.DeleteSelectedTask();
            SaveTasksInternal("Nova tarefa descartada.");
            return;
        }

        if (_taskEditSnapshots.Remove(task, out var snapshot))
        {
            RestoreTask(task, snapshot);
        }

        task.IsEditing = false;
        _viewModel.SelectedTask = task;
        SaveTasksInternal("Edição descartada.");
    }

    private static void RestoreTask(TaskEditorViewModel task, TaskItem snapshot)
    {
        task.Id = snapshot.Id;
        task.Title = snapshot.Title;
        task.Status = snapshot.Status == "done" ? "done" : "pending";
        task.Notes = snapshot.Notes ?? "";
        task.RelatedDayId = snapshot.RelatedDayId;
        task.RelatedExpenseId = snapshot.RelatedExpenseId;
        task.RelatedPlaceId = snapshot.RelatedPlaceId;
        task.RelatedAttachment = snapshot.RelatedAttachment;
    }

    private System.Windows.Controls.TextBox? FindTaskTitleEditor(TaskEditorViewModel task)
    {
        var presenter = TaskItemsPresenter(TasksPanel);
        if (presenter is null)
        {
            return null;
        }

        for (var i = 0; i < presenter.Items.Count; i++)
        {
            if (!ReferenceEquals(presenter.Items[i], task))
            {
                continue;
            }

            var container = presenter.ItemContainerGenerator.ContainerFromIndex(i) as DependencyObject;
            if (container is null)
            {
                return null;
            }

            return FindVisualChildByName<System.Windows.Controls.TextBox>(container, "TaskTitleEditor");
        }

        return null;
    }

    private static ItemsControl? TaskItemsPresenter(DependencyObject root)
    {
        return FindVisualChild<ItemsControl>(root);
    }

    private static T? FindVisualChildByName<T>(DependencyObject parent, string name) where T : FrameworkElement
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T typed && typed.Name == name)
            {
                return typed;
            }

            var nested = FindVisualChildByName<T>(child, name);
            if (nested is not null)
            {
                return nested;
            }
        }

        return null;
    }

    private void TipsList_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _tipDragStartPoint = e.GetPosition(null);
    }

    private void TipsList_PreviewMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || _viewModel.SelectedTip is null || _viewModel.SelectedTip.IsEditing)
        {
            return;
        }

        var currentPosition = e.GetPosition(null);
        if (Math.Abs(currentPosition.X - _tipDragStartPoint.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(currentPosition.Y - _tipDragStartPoint.Y) < SystemParameters.MinimumVerticalDragDistance)
        {
            return;
        }

        System.Windows.DragDrop.DoDragDrop(TipsList, _viewModel.SelectedTip, System.Windows.DragDropEffects.Move);
    }

    private void TipsList_DragOver(object sender, System.Windows.DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(typeof(LinkEditorViewModel))
            ? System.Windows.DragDropEffects.Move
            : System.Windows.DragDropEffects.None;
        e.Handled = true;
    }

    private void TipsList_Drop(object sender, System.Windows.DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(typeof(LinkEditorViewModel)))
        {
            return;
        }

        var tip = e.Data.GetData(typeof(LinkEditorViewModel)) as LinkEditorViewModel;
        if (tip is null)
        {
            return;
        }

        var targetIndex = GetTipDropIndex(e.OriginalSource);
        _viewModel.MoveTip(tip, targetIndex);
        e.Handled = true;
    }

    private int GetTipDropIndex(object originalSource)
    {
        var item = FindAncestor<ListBoxItem>(originalSource as DependencyObject);
        if (item?.DataContext is LinkEditorViewModel target)
        {
            return _viewModel.Tips.IndexOf(target);
        }

        return Math.Max(_viewModel.Tips.Count - 1, 0);
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

    private void SaveTasks_Click(object sender, RoutedEventArgs e)
    {
        SaveTasksInternal($"Tarefas salvas em {DateTime.Now:HH:mm}.");
    }

    private void SaveTips_Click(object sender, RoutedEventArgs e)
    {
        SaveTipsInternal($"Dicas salvas em {DateTime.Now:HH:mm}.");
    }

    private void ShowFiles_Click(object sender, RoutedEventArgs e)
    {
        ShowFiles();
    }

    private void ShowMap_Click(object sender, RoutedEventArgs e)
    {
        ShowMap();
    }

    private void OpenMyMaps_Click(object sender, RoutedEventArgs e)
    {
        OpenUrl(_viewModel.MyMapsUrl, "Nenhum link do Google My Maps cadastrado.");
    }

    private void SaveMap_Click(object sender, RoutedEventArgs e)
    {
        SaveMapInternal($"Mapa salvo em {DateTime.Now:HH:mm}.");
    }

    private void MapUrlBox_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        SaveMapInternal("Link do mapa salvo.");
    }

    private void MapUrlBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
        {
            return;
        }

        SaveMapInternal("Link do mapa salvo.");
        Keyboard.ClearFocus();
        e.Handled = true;
    }

    private void AttachFiles_Click(object sender, RoutedEventArgs e)
    {
        using var dialog = new Forms.OpenFileDialog
        {
            Title = "Selecione arquivos para anexar",
            Multiselect = true,
            CheckFileExists = true
        };

        if (dialog.ShowDialog() != Forms.DialogResult.OK)
        {
            return;
        }

        AddAttachmentFiles(dialog.FileNames);
    }

    private void DeleteAttachment_Click(object sender, RoutedEventArgs e)
    {
        SelectAttachmentFromSender(sender);
        DeleteSelectedAttachmentWithConfirmation();
    }

    private void RenameAttachment_Click(object sender, RoutedEventArgs e)
    {
        if (SelectAttachmentFromSender(sender) is { } attachment)
        {
            BeginAttachmentEdit(attachment);
        }
    }

    private void RenameSelectedAttachment()
    {
        if (_viewModel.SelectedAttachment is not { } attachment)
        {
            _viewModel.StatusMessage = "Selecione um arquivo para renomear.";
            return;
        }

        var newName = ShowRenameDialog(attachment.File);
        if (string.IsNullOrWhiteSpace(newName) || string.Equals(newName, attachment.File, StringComparison.Ordinal))
        {
            return;
        }

        attachment.File = newName.Trim();
        SaveAttachmentsInternal($"Arquivo renomeado para {attachment.File}.");
    }

    private void AttachmentEditView_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: AttachmentEditorViewModel attachment } editView ||
            e.NewFocus is DependencyObject newFocus && IsDescendantOf(newFocus, editView))
        {
            return;
        }

        CompleteAttachmentEdit(attachment);
    }

    private void AttachmentEditView_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: AttachmentEditorViewModel attachment })
        {
            return;
        }

        if (e.Key == Key.Enter)
        {
            CompleteAttachmentEdit(attachment);
            AttachmentsList.Focus();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            CancelAttachmentEdit(attachment);
            AttachmentsList.Focus();
            e.Handled = true;
        }
    }

    private void BeginAttachmentEdit(AttachmentEditorViewModel attachment)
    {
        _viewModel.SelectedAttachment = attachment;
        if (!attachment.IsEditing)
        {
            _attachmentEditSnapshots[attachment] = attachment.File;
        }

        attachment.IsEditing = true;

        Dispatcher.BeginInvoke(() =>
        {
            if (AttachmentsList.ItemContainerGenerator.ContainerFromItem(attachment) is ListBoxItem item &&
                FindVisualChild<System.Windows.Controls.TextBox>(item) is { } input)
            {
                input.Focus();
                var nameOnly = Path.GetFileNameWithoutExtension(attachment.File);
                input.Select(0, nameOnly.Length);
            }
        });
    }

    private void CompleteAttachmentEdit(AttachmentEditorViewModel attachment)
    {
        if (!attachment.IsEditing)
        {
            return;
        }

        attachment.IsEditing = false;
        _attachmentEditSnapshots.Remove(attachment);
        SaveAttachmentsInternal($"Arquivo renomeado para {attachment.File}.");
    }

    private void CancelAttachmentEdit(AttachmentEditorViewModel attachment)
    {
        if (_attachmentEditSnapshots.Remove(attachment, out var originalFile))
        {
            attachment.File = originalFile;
        }

        attachment.IsEditing = false;
        _viewModel.StatusMessage = "Edição cancelada.";
    }

    private AttachmentEditorViewModel? SelectAttachmentFromSender(object sender)
    {
        if ((sender as FrameworkElement)?.DataContext is not AttachmentEditorViewModel attachment)
        {
            return null;
        }

        _viewModel.SelectedAttachment = attachment;
        return attachment;
    }

    private void DeleteSelectedAttachmentWithConfirmation()
    {
        if (_viewModel.SelectedAttachment is null)
        {
            _viewModel.StatusMessage = "Selecione um arquivo para excluir da lista.";
            return;
        }

        var fileName = _viewModel.SelectedAttachment.File;
        var result = System.Windows.MessageBox.Show(
            $"Excluir \"{fileName}\" desta viagem?\n\nO arquivo também será apagado da pasta da viagem.",
            "Confirmar exclusão",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result != MessageBoxResult.Yes)
        {
            return;
        }

        var path = Path.Combine(_viewModel.TripPath, fileName);
        if (File.Exists(path))
        {
            try
            {
                File.Delete(path);
            }
            catch (IOException ex)
            {
                _viewModel.StatusMessage = $"Não foi possível apagar {fileName}: {ex.Message}";
                return;
            }
            catch (UnauthorizedAccessException ex)
            {
                _viewModel.StatusMessage = $"Sem permissão para apagar {fileName}: {ex.Message}";
                return;
            }
        }

        _viewModel.DeleteSelectedAttachment();
        SaveAttachmentsInternal("Arquivo excluído da pasta e salvo.");
    }

    private void AttachmentsGrid_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (IsEditingText(e.OriginalSource))
        {
            return;
        }

        if (e.Key == Key.Delete)
        {
            DeleteSelectedAttachmentWithConfirmation();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.F2)
        {
            if (_viewModel.SelectedAttachment is { } attachment)
            {
                BeginAttachmentEdit(attachment);
            }
            e.Handled = true;
        }
    }

    private static bool IsEditingText(object originalSource)
    {
        var current = originalSource as DependencyObject;
        while (current is not null)
        {
            if (current is System.Windows.Controls.TextBox)
            {
                return true;
            }

            current = System.Windows.Media.VisualTreeHelper.GetParent(current);
        }

        return false;
    }

    private void AttachmentsList_PreviewMouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        _attachmentDragStartPoint = e.GetPosition(null);
    }

    private void AttachmentsList_PreviewMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || _viewModel.SelectedAttachment is null)
        {
            return;
        }

        var currentPosition = e.GetPosition(null);
        if (Math.Abs(currentPosition.X - _attachmentDragStartPoint.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(currentPosition.Y - _attachmentDragStartPoint.Y) < SystemParameters.MinimumVerticalDragDistance)
        {
            return;
        }

        System.Windows.DragDrop.DoDragDrop(AttachmentsList, _viewModel.SelectedAttachment, System.Windows.DragDropEffects.Move);
    }

    private void AttachmentsList_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (FindAncestor<System.Windows.Controls.Button>(e.OriginalSource as DependencyObject) is not null)
        {
            return;
        }

        if (FindAncestor<System.Windows.Controls.TextBox>(e.OriginalSource as DependencyObject) is not null)
        {
            return;
        }

        if (FindAncestor<ListBoxItem>(e.OriginalSource as DependencyObject) is not { DataContext: AttachmentEditorViewModel attachment })
        {
            return;
        }

        _viewModel.SelectedAttachment = attachment;
        OpenSelectedAttachment();
        e.Handled = true;
    }

    private void AttachmentsList_DragOver(object sender, System.Windows.DragEventArgs e)
    {
        if (e.Data.GetDataPresent(typeof(AttachmentEditorViewModel)))
        {
            e.Effects = System.Windows.DragDropEffects.Move;
            e.Handled = true;
            return;
        }

        FilesPanel_DragOver(sender, e);
    }

    private void AttachmentsList_Drop(object sender, System.Windows.DragEventArgs e)
    {
        if (e.Data.GetDataPresent(typeof(AttachmentEditorViewModel)))
        {
            var attachment = e.Data.GetData(typeof(AttachmentEditorViewModel)) as AttachmentEditorViewModel;
            if (attachment is null)
            {
                return;
            }

            var targetIndex = GetAttachmentDropIndex(e.OriginalSource);
            _viewModel.MoveAttachment(attachment, targetIndex);
            SaveAttachmentsInternal("Ordem dos arquivos salva.");
            e.Handled = true;
            return;
        }

        FilesPanel_Drop(sender, e);
        e.Handled = true;
    }

    private void SaveAttachments_Click(object sender, RoutedEventArgs e)
    {
        SaveAttachmentsInternal($"Arquivos salvos em {DateTime.Now:HH:mm}.");
    }

    private void OpenSelectedFile_Click(object sender, RoutedEventArgs e)
    {
        OpenSelectedAttachment();
    }

    private void OpenSelectedAttachment()
    {
        if (_viewModel.SelectedAttachment is null)
        {
            _viewModel.StatusMessage = "Selecione um arquivo para abrir.";
            return;
        }

        var path = Path.Combine(_viewModel.TripPath, _viewModel.SelectedAttachment.File);
        if (!File.Exists(path))
        {
            _viewModel.StatusMessage = "Arquivo não encontrado na pasta da viagem.";
            return;
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = path,
            UseShellExecute = true
        });
    }

    private void FilesPanel_DragOver(object sender, System.Windows.DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop)
            ? System.Windows.DragDropEffects.Copy
            : System.Windows.DragDropEffects.None;
        e.Handled = true;
    }

    private void FilesPanel_Drop(object sender, System.Windows.DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop))
        {
            return;
        }

        var paths = e.Data.GetData(System.Windows.DataFormats.FileDrop) as string[];
        AddAttachmentFiles(paths ?? []);
        e.Handled = true;
    }

    private void OverviewCard_PreviewMouseWheel(object sender, System.Windows.Input.MouseWheelEventArgs e)
    {
        OverviewPanel.ScrollToVerticalOffset(OverviewPanel.VerticalOffset - e.Delta);
        e.Handled = true;
    }

    private int GetAttachmentDropIndex(object originalSource)
    {
        var item = FindAncestor<ListBoxItem>(originalSource as DependencyObject);
        if (item?.DataContext is AttachmentEditorViewModel target)
        {
            return _viewModel.Attachments.IndexOf(target);
        }

        return Math.Max(_viewModel.Attachments.Count - 1, 0);
    }

    private static T? FindAncestor<T>(DependencyObject? current) where T : DependencyObject
    {
        while (current is not null)
        {
            if (current is T typed)
            {
                return typed;
            }

            current = System.Windows.Media.VisualTreeHelper.GetParent(current);
        }

        return null;
    }

    private string? ShowRenameDialog(string currentName, string title = "Renomear arquivo", string label = "Novo nome do arquivo")
    {
        var dialog = new Window
        {
            Title = title,
            Owner = this,
            Width = 460,
            Height = 190,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            ResizeMode = ResizeMode.NoResize,
            Background = (System.Windows.Media.Brush)FindResource("PanelBackground")
        };
        var input = new System.Windows.Controls.TextBox
        {
            Text = currentName,
            FontSize = 16,
            Margin = new Thickness(0, 6, 0, 16),
            Padding = new Thickness(8, 6, 8, 6)
        };
        var result = currentName;
        var saveButton = new System.Windows.Controls.Button
        {
            Content = "Renomear",
            Style = (Style)FindResource("PrimaryButton"),
            MinWidth = 96
        };
        var cancelButton = new System.Windows.Controls.Button
        {
            Content = "Cancelar",
            Style = (Style)FindResource("SecondaryButton"),
            MinWidth = 88,
            Margin = new Thickness(0, 0, 10, 0)
        };

        saveButton.Click += (_, _) =>
        {
            result = input.Text;
            dialog.DialogResult = true;
        };
        cancelButton.Click += (_, _) => dialog.DialogResult = false;

        var actions = new StackPanel
        {
            Orientation = System.Windows.Controls.Orientation.Horizontal,
            HorizontalAlignment = System.Windows.HorizontalAlignment.Right
        };
        actions.Children.Add(cancelButton);
        actions.Children.Add(saveButton);

        var content = new StackPanel { Margin = new Thickness(18) };
        content.Children.Add(new TextBlock
        {
            Text = label,
            Foreground = (System.Windows.Media.Brush)FindResource("MutedTextBrush")
        });
        content.Children.Add(input);
        content.Children.Add(actions);
        dialog.Content = content;
        dialog.Loaded += (_, _) =>
        {
            input.Focus();
            var nameOnly = Path.GetFileNameWithoutExtension(currentName);
            input.Select(0, nameOnly.Length);
        };

        return dialog.ShowDialog() == true ? result : null;
    }

    private void OpenSelectedTip_Click(object sender, RoutedEventArgs e)
    {
        OpenTip(_viewModel.SelectedTip);
    }

    private void OpenTipLink_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: LinkEditorViewModel tip })
        {
            _viewModel.SelectedTip = tip;
            OpenTip(tip);
        }
    }

    private void EditTipCard_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: LinkEditorViewModel tip })
        {
            BeginTipEdit(tip);
        }
    }

    private void TipsList_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.F2 && _viewModel.SelectedTip is not null)
        {
            BeginTipEdit(_viewModel.SelectedTip);
            e.Handled = true;
        }
    }

    private void TipEditView_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: LinkEditorViewModel tip } editView ||
            e.NewFocus is DependencyObject newFocus && IsDescendantOf(newFocus, editView))
        {
            return;
        }

        CompleteTipEdit(tip);
    }

    private void TipEditView_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: LinkEditorViewModel tip })
        {
            return;
        }

        if (e.Key == Key.Enter)
        {
            CompleteTipEdit(tip);
            TipsList.Focus();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            CancelTipEdit(tip);
            TipsList.Focus();
            e.Handled = true;
        }
    }

    private void TipReadView_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount != 2 ||
            sender is not FrameworkElement { DataContext: LinkEditorViewModel tip } ||
            e.OriginalSource is not DependencyObject source ||
            FindVisualAncestor<System.Windows.Controls.Button>(source) is not null)
        {
            return;
        }

        BeginTipEdit(tip);
        e.Handled = true;
    }

    private void BeginTipEdit(LinkEditorViewModel tip)
    {
        _viewModel.SelectedTip = tip;
        if (!tip.IsEditing)
        {
            _tipEditSnapshots[tip] = (tip.Title, tip.Url);
        }

        tip.IsEditing = true;

        Dispatcher.BeginInvoke(() =>
        {
            if (TipsList.ItemContainerGenerator.ContainerFromItem(tip) is ListBoxItem item &&
                FindVisualChild<System.Windows.Controls.TextBox>(item) is { } input)
            {
                input.Focus();
                input.SelectAll();
            }
        });
    }

    private void CompleteTipEdit(LinkEditorViewModel tip)
    {
        if (!tip.IsEditing)
        {
            return;
        }

        tip.IsEditing = false;
        _tipEditSnapshots.Remove(tip);
        SaveTipsInternal("Dica atualizada e salva.");
    }

    private void CancelTipEdit(LinkEditorViewModel tip)
    {
        if (_tipEditSnapshots.Remove(tip, out var snapshot))
        {
            tip.Title = snapshot.Title;
            tip.Url = snapshot.Url;
        }

        tip.IsEditing = false;
        _viewModel.StatusMessage = "Edição cancelada.";
    }

    private void DeleteTipCard_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: LinkEditorViewModel tip })
        {
            _viewModel.SelectedTip = tip;
            _viewModel.DeleteSelectedTip();
            SaveTipsInternal("Dica removida e salva.");
        }
    }

    private void OpenTip(LinkEditorViewModel? tip)
    {
        OpenUrl(tip?.Url, "Selecione uma dica com link para abrir.");
    }

    private void OpenUrl(string? url, string emptyMessage)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            _viewModel.StatusMessage = emptyMessage;
            return;
        }

        if (!LinkEditorViewModel.IsHttpUrl(url) || !Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            _viewModel.StatusMessage = "O link informado não parece ser uma URL válida.";
            return;
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = uri.ToString(),
            UseShellExecute = true
        });
    }

    private void RefreshMapBrowsers()
    {
        var url = _viewModel.MyMapsUrl;
        var embedUrl = BuildMyMapsEmbedUrl(url);
        var hasMap = embedUrl is not null;
        OverviewMapPlaceholder.Visibility = hasMap ? Visibility.Collapsed : Visibility.Visible;
        MainMapPlaceholder.Visibility = hasMap ? Visibility.Collapsed : Visibility.Visible;

        if (embedUrl is null)
        {
            NavigateBrowserToBlank(OverviewMapBrowser);
            NavigateBrowserToBlank(MainMapBrowser);
            return;
        }

        NavigateMapBrowser(OverviewMapBrowser, embedUrl);
        NavigateMapBrowser(MainMapBrowser, embedUrl);
    }

    private static string? BuildMyMapsEmbedUrl(string url)
    {
        if (!LinkEditorViewModel.IsHttpUrl(url) || !Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return null;
        }

        var mid = GetQueryParameter(uri.Query, "mid");
        if (string.IsNullOrWhiteSpace(mid))
        {
            var match = Regex.Match(uri.AbsolutePath, @"/maps/d/(?:u/\d+/)?(?:viewer|edit|embed)/([^/?#]+)", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                mid = Uri.UnescapeDataString(match.Groups[1].Value);
            }
        }

        if (string.IsNullOrWhiteSpace(mid))
        {
            return uri.ToString();
        }

        return $"https://www.google.com/maps/d/embed?mid={Uri.EscapeDataString(mid)}";
    }

    private static void NavigateMapBrowser(Microsoft.Web.WebView2.Wpf.WebView2 browser, string embedUrl)
    {
        if (string.Equals(browser.Tag as string, embedUrl, StringComparison.Ordinal))
        {
            return;
        }

        browser.Tag = embedUrl;
        browser.Source = new Uri(embedUrl);
    }

    private static string? GetQueryParameter(string query, string name)
    {
        foreach (var part in query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var pieces = part.Split('=', 2);
            if (pieces.Length == 2 && string.Equals(Uri.UnescapeDataString(pieces[0]), name, StringComparison.OrdinalIgnoreCase))
            {
                return Uri.UnescapeDataString(pieces[1].Replace("+", " "));
            }
        }

        return null;
    }

    private static void NavigateBrowserToBlank(Microsoft.Web.WebView2.Wpf.WebView2 browser)
    {
        if (string.Equals(browser.Tag as string, "about:blank", StringComparison.Ordinal))
        {
            return;
        }

        browser.Tag = "about:blank";
        browser.Source = new Uri("about:blank");
    }

    private static bool IsDescendantOf(DependencyObject child, DependencyObject parent)
    {
        var current = child;
        while (current is not null)
        {
            if (ReferenceEquals(current, parent))
            {
                return true;
            }

            current = VisualTreeHelper.GetParent(current);
        }

        return false;
    }

    private static T? FindVisualChild<T>(DependencyObject parent)
        where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T typedChild)
            {
                return typedChild;
            }

            var descendant = FindVisualChild<T>(child);
            if (descendant is not null)
            {
                return descendant;
            }
        }

        return null;
    }

    private static T? FindVisualAncestor<T>(DependencyObject child)
        where T : DependencyObject
    {
        var current = child;
        while (current is not null)
        {
            if (current is T typedCurrent)
            {
                return typedCurrent;
            }

            current = VisualTreeHelper.GetParent(current);
        }

        return null;
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

    private void ViewModel_AttachmentsChanged(object? sender, EventArgs e)
    {
        if (_isLoadingTrip || _isSavingAttachments)
        {
            return;
        }

        SaveAttachmentsInternal($"Salvo automaticamente às {DateTime.Now:HH:mm}.");
    }

    private void ViewModel_ExpensesChanged(object? sender, EventArgs e)
    {
        if (_isLoadingTrip || _isSavingExpenses)
        {
            return;
        }

        _expensesSaveTimer.Stop();
        _expensesSaveTimer.Start();
    }

    private void ExpensesSaveTimer_Tick(object? sender, EventArgs e)
    {
        _expensesSaveTimer.Stop();
        SaveExpensesInternal($"Salvo automaticamente às {DateTime.Now:HH:mm}.");
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
        _viewModel.IsCurrentTripFavorite = trip is not null && config.FavoriteTrips.Contains(trip.Id);

        _viewModel.StatusMessage = trip is null
            ? $"Não foi possível abrir {tripId}."
            : $"Viagem {trip.Title} carregada.";
        RefreshMapBrowsers();

        if (TripDetailsPanel.Visibility == Visibility.Visible)
        {
            PopulateTripDetailsPanel();
        }
    }

    private void ShowOverview()
    {
        OverviewPanel.Visibility = Visibility.Visible;
        TripDetailsPanel.Visibility = Visibility.Collapsed;
        ItineraryPanel.Visibility = Visibility.Collapsed;
        BudgetPanel.Visibility = Visibility.Collapsed;
        TasksPanel.Visibility = Visibility.Collapsed;
        TipsPanel.Visibility = Visibility.Collapsed;
        MapPanel.Visibility = Visibility.Collapsed;
        FilesPanel.Visibility = Visibility.Collapsed;
        SetActiveNav(OverviewNavButton);
        RefreshMapBrowsers();
    }

    private void ShowItinerary()
    {
        OverviewPanel.Visibility = Visibility.Collapsed;
        TripDetailsPanel.Visibility = Visibility.Collapsed;
        ItineraryPanel.Visibility = Visibility.Visible;
        BudgetPanel.Visibility = Visibility.Collapsed;
        TasksPanel.Visibility = Visibility.Collapsed;
        TipsPanel.Visibility = Visibility.Collapsed;
        MapPanel.Visibility = Visibility.Collapsed;
        FilesPanel.Visibility = Visibility.Collapsed;
        SetActiveNav(ItineraryNavButton);
        // Aguarda o layout do pai ser concluído antes de recalcular os slots
        Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded,
            new Action(() => RecalcItinerarySlotWidth(ItineraryPanel.ActualWidth)));
    }

    private void ShowTasks()
    {
        OverviewPanel.Visibility = Visibility.Collapsed;
        TripDetailsPanel.Visibility = Visibility.Collapsed;
        ItineraryPanel.Visibility = Visibility.Collapsed;
        BudgetPanel.Visibility = Visibility.Collapsed;
        TasksPanel.Visibility = Visibility.Visible;
        TipsPanel.Visibility = Visibility.Collapsed;
        MapPanel.Visibility = Visibility.Collapsed;
        FilesPanel.Visibility = Visibility.Collapsed;
        SetActiveNav(TasksNavButton);
    }

    private void ShowBudget()
    {
        OverviewPanel.Visibility = Visibility.Collapsed;
        TripDetailsPanel.Visibility = Visibility.Collapsed;
        ItineraryPanel.Visibility = Visibility.Collapsed;
        BudgetPanel.Visibility = Visibility.Visible;
        TasksPanel.Visibility = Visibility.Collapsed;
        TipsPanel.Visibility = Visibility.Collapsed;
        MapPanel.Visibility = Visibility.Collapsed;
        FilesPanel.Visibility = Visibility.Collapsed;
        SetActiveNav(BudgetNavButton);
    }

    private void ShowTips()
    {
        OverviewPanel.Visibility = Visibility.Collapsed;
        TripDetailsPanel.Visibility = Visibility.Collapsed;
        ItineraryPanel.Visibility = Visibility.Collapsed;
        BudgetPanel.Visibility = Visibility.Collapsed;
        TasksPanel.Visibility = Visibility.Collapsed;
        TipsPanel.Visibility = Visibility.Visible;
        MapPanel.Visibility = Visibility.Collapsed;
        FilesPanel.Visibility = Visibility.Collapsed;
        SetActiveNav(TipsNavButton);
    }

    private void ShowMap()
    {
        OverviewPanel.Visibility = Visibility.Collapsed;
        TripDetailsPanel.Visibility = Visibility.Collapsed;
        ItineraryPanel.Visibility = Visibility.Collapsed;
        BudgetPanel.Visibility = Visibility.Collapsed;
        TasksPanel.Visibility = Visibility.Collapsed;
        TipsPanel.Visibility = Visibility.Collapsed;
        MapPanel.Visibility = Visibility.Visible;
        FilesPanel.Visibility = Visibility.Collapsed;
        SetActiveNav(MapNavButton);
        RefreshMapBrowsers();
    }

    private void ShowFiles()
    {
        OverviewPanel.Visibility = Visibility.Collapsed;
        TripDetailsPanel.Visibility = Visibility.Collapsed;
        ItineraryPanel.Visibility = Visibility.Collapsed;
        BudgetPanel.Visibility = Visibility.Collapsed;
        TasksPanel.Visibility = Visibility.Collapsed;
        TipsPanel.Visibility = Visibility.Collapsed;
        MapPanel.Visibility = Visibility.Collapsed;
        FilesPanel.Visibility = Visibility.Visible;
        SetActiveNav(FilesNavButton);
    }

    private void ShowTripDetails()
    {
        OverviewPanel.Visibility = Visibility.Collapsed;
        TripDetailsPanel.Visibility = Visibility.Visible;
        ItineraryPanel.Visibility = Visibility.Collapsed;
        BudgetPanel.Visibility = Visibility.Collapsed;
        TasksPanel.Visibility = Visibility.Collapsed;
        TipsPanel.Visibility = Visibility.Collapsed;
        MapPanel.Visibility = Visibility.Collapsed;
        FilesPanel.Visibility = Visibility.Collapsed;
        SetActiveNav(TripDetailsNavButton);
    }

    private void SetActiveNav(System.Windows.Controls.Button activeButton)
    {
        foreach (var button in new[] { OverviewNavButton, TripDetailsNavButton, ItineraryNavButton, BudgetNavButton, TasksNavButton, TipsNavButton, MapNavButton, FilesNavButton })
        {
            button.Tag = null;
        }
        activeButton.Tag = "Active";
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

    private void SaveItineraryInternal(string message)
    {
        if (_repository is null || _viewModel.CurrentTrip is null)
        {
            _viewModel.StatusMessage = "Nenhuma viagem carregada para salvar.";
            return;
        }

        _viewModel.ApplyItineraryToTrip();
        _repository.SaveTrip(_viewModel.CurrentTrip);
        _viewModel.StatusMessage = message;
    }

    private void ViewModel_ItineraryChanged(object? sender, EventArgs e)
    {
        // Auto-save is handled explicitly by user actions; no auto-save here.
    }

    private void AddItineraryDay_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.AddItineraryDay();
        SaveItineraryInternal($"Dia adicionado ao roteiro.");
    }

    private void RemoveItineraryDay_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: ItineraryDayViewModel day })
            return;

        var result = System.Windows.MessageBox.Show(
            $"Remover o dia \"{day.Title}\" e todas as suas atividades?",
            "Remover Dia",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result != MessageBoxResult.Yes)
            return;

        _viewModel.ClearAllActivityDims();
        _viewModel.ClearAllDayDims();
        _viewModel.RemoveItineraryDay(day);
        SaveItineraryInternal("Dia removido do roteiro.");
    }

    private void AddItineraryActivity_Click(object sender, RoutedEventArgs e)
    {
        var targetDay = _viewModel.SelectedActivity is not null
            ? _viewModel.FindDayForActivity(_viewModel.SelectedActivity)
            : _viewModel.Itinerary.FirstOrDefault();

        if (targetDay is null)
        {
            _viewModel.StatusMessage = "Adicione um dia antes de criar atividades.";
            return;
        }

        _viewModel.AddActivity(targetDay);
        SaveItineraryInternal("Nova atividade adicionada.");
    }

    private void DeleteSelectedActivity_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.RemoveSelectedActivity();
        SaveItineraryInternal("Atividade removida.");
    }

    private void ToggleBank_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.IsBankExpanded = !_viewModel.IsBankExpanded;
    }

    private void AddBankRow_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.AddBankRow();
        SaveItineraryInternal($"Linha adicionada ao banco.");
    }

    private void RemoveBankRow_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.RemoveBankRow();
        SaveItineraryInternal($"Linha removida do banco.");
    }

    private void AddBankActivity_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.AddBankActivity();
        SaveItineraryInternal("Nova atividade adicionada ao banco.");
    }

    // ── Inline edit handlers ────────────────────────────────────────────────

    private void DayDatePicker_CalendarOpened(object sender, RoutedEventArgs e)
    {
        if (sender is DatePicker dp && dp.SelectedDate.HasValue)
            dp.DisplayDate = dp.SelectedDate.Value;
    }

    private void EditTypeCombo_DropDownOpened(object sender, EventArgs e) => _typeComboOpen = true;
    private void EditTypeCombo_DropDownClosed(object sender, EventArgs e) =>
        Dispatcher.BeginInvoke(() => _typeComboOpen = false, System.Windows.Threading.DispatcherPriority.Input);

    private void DayLabel_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount != 2) return;
        if (sender is FrameworkElement fe && fe.DataContext is ItineraryDayViewModel day)
        {
            var wasEditing = day.IsEditingDay;
            foreach (var d in _viewModel.Itinerary) { d.RejectEdit(); d.RejectDayEdit(); }
            foreach (var r in _viewModel.BankRows) r.RejectEdit();
            _viewModel.ClearAllActivityDims();
            _viewModel.ClearAllDayDims();
            if (!wasEditing)
            {
                day.BeginDayEdit();
                _viewModel.SetDayFocus(day);
            }
            e.Handled = true;
        }
    }

    private void AcceptDayEdit_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.DataContext is ItineraryDayViewModel day)
        {
            day.AcceptDayEdit();
            _viewModel.ClearAllDayDims();
            SaveItineraryInternal("Dia atualizado.");
        }
    }

    private void RejectDayEdit_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.DataContext is ItineraryDayViewModel day)
        {
            day.RejectDayEdit();
            _viewModel.ClearAllDayDims();
        }
    }

    private void AcceptEdit_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement fe) return;
        if (fe.DataContext is ItineraryDayViewModel day)
            day.AcceptEdit();
        else if (fe.DataContext is BankRowViewModel bankRow)
            bankRow.AcceptEdit();
        else return;
        _viewModel.ClearAllActivityDims();
        _viewModel.ClearAllDayDims();
        SaveItineraryInternal("Atividade atualizada.");
    }

    private void RejectEdit_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement fe) return;
        if (fe.DataContext is ItineraryDayViewModel day)
            day.RejectEdit();
        else if (fe.DataContext is BankRowViewModel bankRow)
            bankRow.RejectEdit();
        else return;
        _viewModel.ClearAllActivityDims();
        _viewModel.ClearAllDayDims();
    }

    private void CopyEditActivity_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement fe) return;
        ItineraryActivityViewModel? target = null;
        if (fe.DataContext is ItineraryDayViewModel day && day.EditingActivity is not null)
        {
            target = day.EditingActivity;
            day.AcceptEdit();
        }
        else if (fe.DataContext is BankRowViewModel bankRow && bankRow.EditingActivity is not null)
        {
            target = bankRow.EditingActivity;
            bankRow.AcceptEdit();
        }
        if (target is null) return;
        _viewModel.ClearAllActivityDims();
        _viewModel.ClearAllDayDims();
        _viewModel.SelectedActivity = target;
        _viewModel.CopySelectedActivity();
        SaveItineraryInternal("Atividade copiada.");
    }

    private void DeleteEditActivity_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement fe) return;
        ItineraryActivityViewModel? target = null;
        if (fe.DataContext is ItineraryDayViewModel day && day.EditingActivity is not null)
        {
            target = day.EditingActivity;
            day.RejectEdit();
        }
        else if (fe.DataContext is BankRowViewModel bankRow && bankRow.EditingActivity is not null)
        {
            target = bankRow.EditingActivity;
            bankRow.RejectEdit();
        }
        if (target is null) return;
        _viewModel.ClearAllActivityDims();
        _viewModel.ClearAllDayDims();
        _viewModel.SelectedActivity = target;
        _viewModel.RemoveSelectedActivity();
        SaveItineraryInternal("Atividade removida.");
    }

    private void EditActivityIcon_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.Button btn || btn.Tag is not string icon) return;
        if (btn.DataContext is ItineraryDayViewModel day && day.EditingActivity is not null)
            day.EditingActivity.EditIcon = icon;
        else if (btn.DataContext is BankRowViewModel bankRow && bankRow.EditingActivity is not null)
            bankRow.EditingActivity.EditIcon = icon;
    }

    private static readonly int[] _activityPaletteOle =
    [
        HexToOle("#DBEAFE"), HexToOle("#E0F2FE"), HexToOle("#DCFCE7"), HexToOle("#FEF3C7"),
        HexToOle("#FCE7F3"), HexToOle("#FEE2E2"), HexToOle("#EDE9FE"), HexToOle("#CFFAFE"),
        HexToOle("#D1FAE5"), HexToOle("#FEF9C3"), HexToOle("#E2E8F0"), HexToOle("#FFE4E6"),
    ];

    private static int HexToOle(string hex)
    {
        var h = hex.TrimStart('#');
        int r = Convert.ToInt32(h[0..2], 16);
        int g = Convert.ToInt32(h[2..4], 16);
        int b = Convert.ToInt32(h[4..6], 16);
        return System.Drawing.ColorTranslator.ToOle(System.Drawing.Color.FromArgb(r, g, b));
    }

    private void EditActivityColor_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement fe) return;
        ItineraryActivityViewModel? editingActivity = null;
        if (fe.DataContext is ItineraryDayViewModel day) editingActivity = day.EditingActivity;
        else if (fe.DataContext is BankRowViewModel bankRow) editingActivity = bankRow.EditingActivity;
        if (editingActivity is null) return;

        var dlg = new System.Windows.Forms.ColorDialog { FullOpen = true, CustomColors = _activityPaletteOle };
        var hex = (editingActivity.EditColor ?? "#DBEAFE").TrimStart('#');
        if (hex.Length == 6
            && int.TryParse(hex[0..2], System.Globalization.NumberStyles.HexNumber, null, out int r)
            && int.TryParse(hex[2..4], System.Globalization.NumberStyles.HexNumber, null, out int g)
            && int.TryParse(hex[4..6], System.Globalization.NumberStyles.HexNumber, null, out int b))
            dlg.Color = System.Drawing.Color.FromArgb(r, g, b);
        if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            editingActivity.EditColor = $"#{dlg.Color.R:X2}{dlg.Color.G:X2}{dlg.Color.B:X2}";
    }

    private void ItineraryPanel_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        var mods = System.Windows.Input.Keyboard.Modifiers;
        var ctrl  = (mods & System.Windows.Input.ModifierKeys.Control) != 0;
        var shift = (mods & System.Windows.Input.ModifierKeys.Shift)   != 0;
        var step  = e.Delta > 0 ? 1 : -1;

        if (ctrl && !shift)
        {
            _viewModel.ItineraryBlockHeight += step * 4;
            SaveItineraryInternal($"Altura dos blocos: {_viewModel.ItineraryBlockHeight}px");
            e.Handled = true;
        }
        else if (shift && !ctrl)
        {
            _viewModel.ItineraryFontSize += step;
            SaveItineraryInternal($"Tamanho da fonte: {_viewModel.ItineraryFontSize}px");
            e.Handled = true;
        }
        else
        {
            // Os ScrollViewers internos dos canvases consomem o evento antes de chegar ao externo;
            // intercepta aqui no tunnel (PreviewMouseWheel) e rola diretamente o ScrollViewer externo.
            ItineraryScrollViewer.ScrollToVerticalOffset(
                ItineraryScrollViewer.VerticalOffset - e.Delta * 0.4);
            e.Handled = true;
        }
    }

    private void MultilineEditField_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == System.Windows.Input.Key.Enter
            && (System.Windows.Input.Keyboard.Modifiers & System.Windows.Input.ModifierKeys.Shift) != 0)
        {
            if (sender is System.Windows.Controls.TextBox tb)
            {
                int caret = tb.CaretIndex;
                tb.Text = tb.Text.Insert(caret, "\n");
                tb.CaretIndex = caret + 1;
            }
            e.Handled = true; // não borbulha para o painel
        }
        // Enter simples: não marca como handled → borbulha para ItineraryPanel_PreviewKeyDown
    }

    private void ItineraryPanel_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (_typeComboOpen) return;

        foreach (var day in _viewModel.Itinerary)
        {
            if (day.IsEditingDay)
            {
                if (e.Key == System.Windows.Input.Key.Enter
                    && (System.Windows.Input.Keyboard.Modifiers & System.Windows.Input.ModifierKeys.Shift) == 0)
                {
                    day.AcceptDayEdit();
                    _viewModel.ClearAllDayDims();
                    SaveItineraryInternal("Dia atualizado.");
                    e.Handled = true;
                }
                else if (e.Key == System.Windows.Input.Key.Escape)
                {
                    day.RejectDayEdit();
                    _viewModel.ClearAllDayDims();
                    e.Handled = true;
                }
                return;
            }

            if (!day.HasEditingBlock) continue;
            if (e.Key == System.Windows.Input.Key.Enter
                && (System.Windows.Input.Keyboard.Modifiers & System.Windows.Input.ModifierKeys.Shift) == 0)
            {
                day.AcceptEdit();
                _viewModel.ClearAllActivityDims();
                _viewModel.ClearAllDayDims();
                SaveItineraryInternal("Atividade atualizada.");
                e.Handled = true;
            }
            else if (e.Key == System.Windows.Input.Key.Escape)
            {
                day.RejectEdit();
                _viewModel.ClearAllActivityDims();
                _viewModel.ClearAllDayDims();
                e.Handled = true;
            }
            return;
        }

        // Check bank rows for editing state
        foreach (var row in _viewModel.BankRows)
        {
            if (!row.HasEditingBlock) continue;
            if (e.Key == System.Windows.Input.Key.Enter
                && (System.Windows.Input.Keyboard.Modifiers & System.Windows.Input.ModifierKeys.Shift) == 0)
            {
                row.AcceptEdit();
                _viewModel.ClearAllActivityDims();
                SaveItineraryInternal("Atividade atualizada.");
                e.Handled = true;
            }
            else if (e.Key == System.Windows.Input.Key.Escape)
            {
                row.RejectEdit();
                _viewModel.ClearAllActivityDims();
                e.Handled = true;
            }
            return;
        }

        // DEL: exclui atividade selecionada (nenhuma edição ativa)
        if (e.Key == System.Windows.Input.Key.Delete && _viewModel.SelectedActivity is not null)
        {
            var title = _viewModel.SelectedActivity.Title;
            var confirm = System.Windows.MessageBox.Show(
                $"Excluir a atividade \"{title}\"?",
                "Excluir atividade",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question,
                MessageBoxResult.No);
            if (confirm == MessageBoxResult.Yes)
            {
                _viewModel.RemoveSelectedActivity();
                SaveItineraryInternal("Atividade excluída.");
            }
            e.Handled = true;
        }
    }

    private void SaveItinerary_Click(object sender, RoutedEventArgs e)
    {
        SaveItineraryInternal($"Roteiro salvo às {DateTime.Now:HH:mm}.");
    }

    // ── Version tab handlers ─────────────────────────────────────────────────

    private void AddVersion_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.AddVersion(duplicateCurrent: false);
        SaveItineraryInternal($"Nova versão adicionada.");
    }

    private void DuplicateVersion_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.AddVersion(duplicateCurrent: true);
        SaveItineraryInternal($"Versão duplicada.");
    }

    private void VersionTab_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: ItineraryVersionTabViewModel tab }) return;
        e.Handled = true;

        if (e.ClickCount >= 2)
        {
            // Start inline rename
            tab.EditName = tab.Name;
            tab.IsRenaming = true;
            return;
        }

        // Single click: switch version (no-op if already active)
        if (!tab.IsActive && !tab.IsRenaming)
        {
            _viewModel.SwitchToVersion(tab.Id);
            SaveItineraryInternal($"Versão '{tab.Name}' ativada.");
        }
    }

    private void VersionTabRenameBox_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if ((bool)e.NewValue && sender is TextBox tb)
        {
            tb.Focus();
            tb.SelectAll();
        }
    }

    private void VersionTabRename_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (sender is not TextBox { DataContext: ItineraryVersionTabViewModel tab }) return;
        if (e.Key == Key.Enter)
        {
            CommitVersionRename(tab);
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            tab.IsRenaming = false;
            e.Handled = true;
        }
    }

    private void VersionTabRename_LostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox { DataContext: ItineraryVersionTabViewModel tab } && tab.IsRenaming)
            CommitVersionRename(tab);
    }

    private void CommitVersionRename(ItineraryVersionTabViewModel tab)
    {
        var name = tab.EditName?.Trim() ?? "";
        tab.IsRenaming = false;
        if (string.IsNullOrEmpty(name)) return;
        if (_viewModel.RenameVersion(tab.Id, name))
            SaveItineraryInternal($"Versão renomeada para '{name}'.");
    }

    private void DeleteVersion_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: ItineraryVersionTabViewModel tab }) return;

        if (_viewModel.VersionTabs.Count <= 1)
        {
            System.Windows.MessageBox.Show("Não é possível excluir a única versão do roteiro.",
                "Excluir versão", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var result = System.Windows.MessageBox.Show(
            $"Excluir a versão \"{tab.Name}\" e todas as suas atividades?",
            "Excluir versão", MessageBoxButton.YesNo, MessageBoxImage.Question);

        if (result != MessageBoxResult.Yes) return;

        if (_viewModel.DeleteVersion(tab.Id, out _))
            SaveItineraryInternal($"Versão excluída.");
    }

    private void ItineraryTimeline_SizeChanged(object sender, SizeChangedEventArgs e)
        => RecalcItinerarySlotWidth(e.NewSize.Width);

    private void RecalcItinerarySlotWidth(double panelWidth)
    {
        var availableWidth = panelWidth - 126; // 112 label column + ~2px border + 6px padding each side
        if (availableWidth > 0 && _viewModel.ItinerarySlotsPerDay > 0)
            _viewModel.ItinerarySlotWidth = availableWidth / _viewModel.ItinerarySlotsPerDay;
    }

    private void CopySelectedActivity_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.CopySelectedActivity();
        SaveItineraryInternal("Atividade copiada.");
    }

    private void ItineraryPanel_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (_typeComboOpen) return;

        // Deselecionar apenas se não clicou em um bloco (o handler do bloco reseleciona)
        if (e.OriginalSource is not FrameworkElement src || src.DataContext is not ItineraryActivityViewModel)
            _viewModel.SelectedActivity = null;
    }

    private void DayCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is Canvas)
            _viewModel.SelectedActivity = null;
    }

    private void ActivityBlock_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement grid || grid.DataContext is not ItineraryActivityViewModel activity)
            return;

        // Double-click: toggle inline editor
        if (e.ClickCount == 2)
        {
            var day = _viewModel.FindDayForActivity(activity);
            var bankRow = day is null ? _viewModel.FindBankRowForActivity(activity) : null;
            if (day is not null || bankRow is not null)
            {
                var wasEditing = activity.IsEditing;
                foreach (var d in _viewModel.Itinerary) d.RejectEdit();
                foreach (var r in _viewModel.BankRows) r.RejectEdit();
                _viewModel.ClearAllActivityDims();
                _viewModel.ClearAllDayDims();
                if (wasEditing) { e.Handled = true; return; }
                if (day is not null)
                {
                    day.BeginEdit(activity);
                    _viewModel.SetDayFocus(day);
                    foreach (var d in _viewModel.Itinerary)
                        if (d != day)
                            foreach (var a in d.Activities)
                                a.IsDimmed = true;
                }
                else
                {
                    bankRow!.BeginEdit(activity);
                }
                e.Handled = true;
                return;
            }
        }

        _viewModel.SelectedActivity = activity;

        var pos = e.GetPosition(grid);
        var isResizeRight = pos.X >= grid.ActualWidth - 10;
        var isResizeLeft  = pos.X <= 10;
        var isResize = isResizeRight || isResizeLeft;

        _draggingActivity = isResize ? null : activity;
        _resizingActivity = isResize ? activity : null;
        _resizingLeft = isResizeLeft;
        _activitySourceDay = _viewModel.FindDayForActivity(activity);
        _activitySourceBankRow = _activitySourceDay is null ? _viewModel.FindBankRowForActivity(activity) : null;
        _activityDragTargetDay = null;
        _activityDragTargetBankRow = null;
        _activityDragOriginPoint = e.GetPosition(null);
        _activityDragGrabOffset = pos.X; // onde dentro do bloco o usuário segurou
        _activityOriginSlot = activity.StartSlot;
        _activityOriginDuration = activity.DurationSlots;
        _activityDragMoved = false;
        _activityCurrentDay = _activitySourceDay;
        _activityCurrentBankRow = _activitySourceBankRow;

        grid.CaptureMouse();
        e.Handled = true;
    }

    private void ActivityBlock_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (_draggingActivity is null && _resizingActivity is null)
        {
            // Update cursor hint near edges
            if (sender is FrameworkElement grid)
            {
                var pos = e.GetPosition(grid);
                grid.Cursor = (pos.X >= grid.ActualWidth - 10 || pos.X <= 10)
                    ? System.Windows.Input.Cursors.SizeWE
                    : System.Windows.Input.Cursors.Arrow;
            }
            return;
        }

        if (e.LeftButton != MouseButtonState.Pressed)
            return;

        var currentPos = e.GetPosition(null);
        var delta = currentPos.X - _activityDragOriginPoint.X;
        if (Math.Abs(delta) > 3) _activityDragMoved = true;

        var slotWidth = _viewModel.ItinerarySlotWidth;
        var slotDelta = (int)Math.Round(delta / slotWidth);
        var slotsPerDay = _viewModel.ItinerarySlotsPerDay;

        if (_draggingActivity is not null)
        {
            var posInList = e.GetPosition(ItineraryDaysList);
            var targetDay = FindDayAtY(posInList.Y);
            var posInBank = e.GetPosition(BankRowsList);
            var targetBankRow = FindBankRowAtY(posInBank.Y);

            // Atualiza destaques de drop
            if (targetDay != _activityDragTargetDay)
            {
                if (_activityDragTargetDay != null) _activityDragTargetDay.IsDragTarget = false;
                _activityDragTargetDay = targetDay;
                if (_activityDragTargetDay != null && _activityDragTargetDay != _activitySourceDay)
                    _activityDragTargetDay.IsDragTarget = true;
            }
            if (targetBankRow != _activityDragTargetBankRow)
            {
                if (_activityDragTargetBankRow != null) _activityDragTargetBankRow.IsDragTarget = false;
                _activityDragTargetBankRow = targetBankRow;
                if (_activityDragTargetBankRow != null && _activityDragTargetBankRow != _activitySourceBankRow)
                    _activityDragTargetBankRow.IsDragTarget = true;
            }

            // Preview em tempo real: entra em novo dia ou nova linha de banco?
            var enteringDay = targetDay is not null && targetDay != _activityCurrentDay ? targetDay : null;
            var enteringBank = enteringDay is null && targetBankRow is not null && targetBankRow != _activityCurrentBankRow ? targetBankRow : null;

            if (enteringDay is not null)
            {
                // Move activity para o novo dia imediatamente
                _activityCurrentDay?.Activities.Remove(_draggingActivity);
                _activityCurrentBankRow?.Activities.Remove(_draggingActivity);

                var newSlot = Math.Clamp(
                    (int)Math.Round((posInList.X - 112 - _activityDragGrabOffset) / slotWidth),
                    0, slotsPerDay - _draggingActivity.DurationSlots);
                _draggingActivity.StartSlot = newSlot;
                enteringDay.Activities.Add(_draggingActivity);

                // Reinicia referência relativa para drags dentro do novo dia
                _activityOriginSlot = newSlot;
                _activityDragOriginPoint = currentPos;

                _activityCurrentDay = enteringDay;
                _activityCurrentBankRow = null;
            }
            else if (enteringBank is not null)
            {
                // Move activity para a nova linha de banco imediatamente
                _activityCurrentDay?.Activities.Remove(_draggingActivity);
                _activityCurrentBankRow?.Activities.Remove(_draggingActivity);

                var newSlot = Math.Clamp(
                    (int)Math.Round((posInBank.X - _activityDragGrabOffset) / slotWidth),
                    0, slotsPerDay - _draggingActivity.DurationSlots);
                _draggingActivity.StartSlot = newSlot;
                enteringBank.Activities.Add(_draggingActivity);

                // Reinicia referência relativa
                _activityOriginSlot = newSlot;
                _activityDragOriginPoint = currentPos;

                _activityCurrentDay = null;
                _activityCurrentBankRow = enteringBank;
            }
            else
            {
                // Mesmo container: atualiza slot por delta relativo (comportamento original)
                var newSlot = Math.Clamp(_activityOriginSlot + slotDelta, 0, slotsPerDay - _draggingActivity.DurationSlots);
                _draggingActivity.StartSlot = newSlot;
            }
        }
        else if (_resizingActivity is not null)
        {
            if (_resizingLeft)
            {
                // Borda esquerda: a borda direita fica fixa, StartSlot e DurationSlots mudam juntos
                var rightEdge = _activityOriginSlot + _activityOriginDuration;
                var newStart = Math.Clamp(_activityOriginSlot + slotDelta, 0, rightEdge - 1);
                _resizingActivity.StartSlot = newStart;
                _resizingActivity.DurationSlots = rightEdge - newStart;
            }
            else
            {
                // Borda direita: StartSlot fixo, apenas DurationSlots muda
                var newDuration = Math.Clamp(_activityOriginDuration + slotDelta, 1, slotsPerDay - _resizingActivity.StartSlot);
                _resizingActivity.DurationSlots = newDuration;
            }
        }

        e.Handled = true;
    }

    private void ActivityBlock_MouseUp(object sender, MouseButtonEventArgs e)
    {
        var moved = _activityDragMoved;

        if (_activityDragTargetDay != null) _activityDragTargetDay.IsDragTarget = false;
        if (_activityDragTargetBankRow != null) _activityDragTargetBankRow.IsDragTarget = false;
        _draggingActivity = null;
        _resizingActivity = null;
        _activityDragTargetDay = null;
        _activitySourceDay = null;
        _activityDragTargetBankRow = null;
        _activitySourceBankRow = null;
        _activityCurrentDay = null;
        _activityCurrentBankRow = null;
        _activityDragMoved = false;

        ((FrameworkElement)sender).ReleaseMouseCapture();

        // A movimentação cross-container já ocorreu em tempo real no MouseMove;
        // basta salvar se houve drag ou resize.
        if (moved)
            SaveItineraryInternal($"Roteiro atualizado às {DateTime.Now:HH:mm}.");

        e.Handled = true;
    }

    private ItineraryDayViewModel? FindDayAtY(double y)
    {
        for (int i = 0; i < ItineraryDaysList.Items.Count; i++)
        {
            if (ItineraryDaysList.ItemContainerGenerator.ContainerFromIndex(i) is not FrameworkElement container) continue;
            var topLeft = container.TranslatePoint(new System.Windows.Point(0, 0), ItineraryDaysList);
            if (y >= topLeft.Y && y < topLeft.Y + container.ActualHeight)
                return ItineraryDaysList.Items[i] as ItineraryDayViewModel;
        }
        return null;
    }

    private BankRowViewModel? FindBankRowAtY(double y)
    {
        for (int i = 0; i < BankRowsList.Items.Count; i++)
        {
            if (BankRowsList.ItemContainerGenerator.ContainerFromIndex(i) is not FrameworkElement container) continue;
            var topLeft = container.TranslatePoint(new System.Windows.Point(0, 0), BankRowsList);
            if (y >= topLeft.Y && y < topLeft.Y + container.ActualHeight)
                return BankRowsList.Items[i] as BankRowViewModel;
        }
        return null;
    }

    private void SaveExpensesInternal(string message)
    {
        if (_repository is null || _viewModel.CurrentTrip is null)
        {
            _viewModel.StatusMessage = "Nenhuma viagem carregada para salvar.";
            return;
        }

        _expensesSaveTimer.Stop();
        _isSavingExpenses = true;
        _viewModel.ApplyExpensesToTrip();
        _repository.SaveTrip(_viewModel.CurrentTrip);
        _viewModel.StatusMessage = message;
        _isSavingExpenses = false;
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

    private void SaveMapInternal(string message)
    {
        if (_repository is null || _viewModel.CurrentTrip is null)
        {
            _viewModel.StatusMessage = "Nenhuma viagem carregada para salvar.";
            return;
        }

        var url = (_viewModel.MyMapsUrl ?? string.Empty).Trim();
        if (!string.IsNullOrWhiteSpace(url))
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
                (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            {
                MapUrlError.Text = "URL inválida. Use um link que comece com http:// ou https://.";
                MapUrlError.Visibility = Visibility.Visible;
                return;
            }

            if (!uri.Host.Contains("google.com", StringComparison.OrdinalIgnoreCase) ||
                !uri.AbsolutePath.Contains("/maps", StringComparison.OrdinalIgnoreCase))
            {
                MapUrlError.Text = "O link não parece ser do Google My Maps (google.com/maps/d/…).";
                MapUrlError.Visibility = Visibility.Visible;
                return;
            }
        }

        MapUrlError.Visibility = Visibility.Collapsed;
        _repository.SaveTrip(_viewModel.CurrentTrip);
        RefreshMapBrowsers();
        _viewModel.StatusMessage = message;
    }

    private void SaveAttachmentsInternal(string message)
    {
        if (_repository is null || _viewModel.CurrentTrip is null)
        {
            _viewModel.StatusMessage = "Nenhuma viagem carregada para salvar.";
            return;
        }

        Directory.CreateDirectory(_viewModel.TripPath);
        _isSavingAttachments = true;
        if (!ApplyAttachmentRenames())
        {
            _isSavingAttachments = false;
            return;
        }

        _viewModel.ApplyAttachmentsToTrip();
        _repository.SaveTrip(_viewModel.CurrentTrip);
        _viewModel.StatusMessage = message;
        _isSavingAttachments = false;
    }

    private bool ApplyAttachmentRenames()
    {
        foreach (var attachment in _viewModel.Attachments)
        {
            var rawName = attachment.File.Trim();
            var fileName = Path.GetFileName(rawName);
            if (string.IsNullOrWhiteSpace(fileName))
            {
                _viewModel.StatusMessage = "Nome de arquivo inválido.";
                return false;
            }
            if (fileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            {
                _viewModel.StatusMessage = $"O nome \"{fileName}\" contém caracteres inválidos.";
                return false;
            }

            if (rawName.EndsWith('.') || rawName.EndsWith(' '))
            {
                _viewModel.StatusMessage = $"O nome \"{rawName}\" não pode terminar com ponto ou espaço.";
                return false;
            }

            if (_windowsReservedNames.Contains(Path.GetFileNameWithoutExtension(fileName)))
            {
                _viewModel.StatusMessage = $"\"{fileName}\" é um nome reservado pelo Windows e não pode ser usado.";
                return false;
            }

            if (fileName != attachment.File)
            {
                attachment.File = fileName;
            }

            if (string.IsNullOrWhiteSpace(attachment.OriginalFile) ||
                string.Equals(attachment.OriginalFile, fileName, StringComparison.OrdinalIgnoreCase))
            {
                attachment.OriginalFile = fileName;
                continue;
            }

            var newExt = Path.GetExtension(fileName);
            var origExt = Path.GetExtension(attachment.OriginalFile);
            if (!string.IsNullOrEmpty(origExt) && !string.Equals(newExt, origExt, StringComparison.OrdinalIgnoreCase))
            {
                var extMsg = string.IsNullOrEmpty(newExt)
                    ? $"O arquivo será renomeado para \"{fileName}\" sem extensão.\n\nDeseja continuar?"
                    : $"A extensão será alterada de \"{origExt}\" para \"{newExt}\".\n\nDeseja continuar?";
                var extTitle = string.IsNullOrEmpty(newExt) ? "Remover extensão" : "Alterar extensão";
                var confirm = System.Windows.MessageBox.Show(extMsg, extTitle, MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (confirm != MessageBoxResult.Yes)
                {
                    attachment.File = attachment.OriginalFile;
                    return false;
                }
            }

            var oldPath = Path.Combine(_viewModel.TripPath, attachment.OriginalFile);
            var newPath = Path.Combine(_viewModel.TripPath, fileName);
            if (!File.Exists(oldPath))
            {
                attachment.OriginalFile = fileName;
                continue;
            }

            if (File.Exists(newPath))
            {
                _viewModel.StatusMessage = $"Já existe um arquivo chamado {fileName}.";
                return false;
            }

            try
            {
                File.Move(oldPath, newPath);
            }
            catch (IOException ex)
            {
                _viewModel.StatusMessage = $"Não foi possível renomear {attachment.OriginalFile}: {ex.Message}";
                return false;
            }
            attachment.OriginalFile = fileName;
        }

        return true;
    }

    private void AddAttachmentFiles(IEnumerable<string> paths)
    {
        if (_repository is null || _viewModel.CurrentTrip is null)
        {
            _viewModel.StatusMessage = "Nenhuma viagem carregada para anexar arquivos.";
            return;
        }

        Directory.CreateDirectory(_viewModel.TripPath);
        var added = 0;
        _isSavingAttachments = true;
        try
        {
            foreach (var sourcePath in paths.Where(File.Exists))
            {
                var fileName = Path.GetFileName(sourcePath);
                var destinationName = CreateUniqueAttachmentFileName(fileName);
                var destinationPath = Path.Combine(_viewModel.TripPath, destinationName);

                if (!string.Equals(Path.GetFullPath(sourcePath), Path.GetFullPath(destinationPath), StringComparison.OrdinalIgnoreCase))
                {
                    File.Copy(sourcePath, destinationPath);
                }

                _viewModel.AddAttachment(destinationName);
                added++;
            }
        }
        finally
        {
            _isSavingAttachments = false;
        }

        if (added == 0)
        {
            _viewModel.StatusMessage = "Nenhum arquivo válido para anexar.";
            return;
        }

        SaveAttachmentsInternal(added == 1 ? "Arquivo anexado e salvo." : $"{added} arquivos anexados e salvos.");
    }

    private string CreateUniqueAttachmentFileName(string fileName)
    {
        var safeName = Path.GetFileName(fileName);
        var baseName = Path.GetFileNameWithoutExtension(safeName);
        var extension = Path.GetExtension(safeName);
        var candidate = safeName;
        var suffix = 2;

        while (File.Exists(Path.Combine(_viewModel.TripPath, candidate)) ||
               _viewModel.Attachments.Any(attachment => string.Equals(attachment.File, candidate, StringComparison.OrdinalIgnoreCase)))
        {
            candidate = $"{baseName}-{suffix}{extension}";
            suffix++;
        }

        return candidate;
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
        if (string.Equals(_viewModel.CurrentTrip?.Id, tripId, StringComparison.OrdinalIgnoreCase))
        {
            _viewModel.IsCurrentTripFavorite = isFavorite;
        }
    }

    private void ReplaceTripIdInConfig(string oldId, string newId)
    {
        if (_repository is null)
        {
            return;
        }

        var config = _repository.LoadOrCreateConfig();
        ReplaceAll(config.RecentTrips, oldId, newId);
        ReplaceAll(config.FavoriteTrips, oldId, newId);
        _repository.SaveConfig(config);
    }

    private static void ReplaceAll(List<string> values, string oldValue, string newValue)
    {
        for (var i = 0; i < values.Count; i++)
        {
            if (string.Equals(values[i], oldValue, StringComparison.OrdinalIgnoreCase))
            {
                values[i] = newValue;
            }
        }
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

    private string CreateUniqueTripId(string title, string startDate)
    {
        var baseId = BuildTripIdBase(title, startDate);
        var id = baseId;
        var suffix = 2;
        while (_repository is not null && Directory.Exists(Path.Combine(_repository.RootPath, id)))
        {
            id = $"{baseId}-{suffix}";
            suffix++;
        }

        return id;
    }

    private static string BuildTripIdBase(string title, string startDate)
    {
        var slug = Slugify(title);
        var date = DateOnly.TryParse(startDate, CultureInfo.InvariantCulture, out var parsedDate)
            ? parsedDate
            : DateOnly.FromDateTime(DateTime.Today);

        return $"{date:yyyy-MM}-{slug}";
    }

    private void PopulateTripDetailsPanel()
    {
        if (_viewModel.CurrentTrip is null)
        {
            return;
        }

        var draft = TripDetailsWindow.FromTrip(_viewModel.CurrentTrip);
        TripDetailsTitleBox.Text = draft.Title;
        TripDetailsStartDatePicker.SelectedDate = ParseDraftDate(draft.StartDate);
        TripDetailsEndDatePicker.SelectedDate = ParseDraftDate(draft.EndDate);
        TripDetailsPeopleBox.Text = draft.People.ToString(CultureInfo.InvariantCulture);
        TripDetailsCurrencyBox.Text = draft.BaseCurrency;
        TripDetailsRateDecimalDigitsBox.Text = (_viewModel.CurrentTrip?.RateDecimalDigits ?? 2).ToString(CultureInfo.InvariantCulture);
        TripDetailsSlotsPerDayBox.Text = (_viewModel.CurrentTrip?.ItinerarySlotsPerDay ?? 16).ToString(CultureInfo.InvariantCulture);
        TripDetailsGridCheckBox.IsChecked = _viewModel.CurrentTrip?.ShowItineraryGrid ?? false;
        TripDetailsPathBox.Text = _viewModel.TripPath;
        TripDetailsErrorText.Visibility = Visibility.Collapsed;
    }

    private TripDetailsDraft BuildTripDetailsDraft()
    {
        return new TripDetailsDraft
        {
            Id = _viewModel.CurrentTrip?.Id ?? "",
            Title = TripDetailsTitleBox.Text.Trim(),
            StartDate = FormatDraftDate(TripDetailsStartDatePicker.SelectedDate),
            EndDate = FormatDraftDate(TripDetailsEndDatePicker.SelectedDate),
            People = int.TryParse(TripDetailsPeopleBox.Text, CultureInfo.InvariantCulture, out var people) ? people : 0,
            BaseCurrency = string.IsNullOrWhiteSpace(TripDetailsCurrencyBox.Text) ? "BRL" : TripDetailsCurrencyBox.Text.Trim().ToUpperInvariant(),
            RateDecimalDigits = int.TryParse(TripDetailsRateDecimalDigitsBox.Text, CultureInfo.InvariantCulture, out var rdd) ? Math.Clamp(rdd, 0, 8) : 2,
            ItinerarySlotsPerDay = int.TryParse(TripDetailsSlotsPerDayBox.Text, CultureInfo.InvariantCulture, out var spd) ? Math.Clamp(spd, 4, 64) : 16,
            MyMapsUrl = _viewModel.CurrentTrip?.MyMapsUrl
        };
    }

    private bool ValidateTripDetailsDraft(TripDetailsDraft draft)
    {
        if (string.IsNullOrWhiteSpace(draft.Title))
        {
            ShowTripDetailsError("Informe o nome da viagem.");
            return false;
        }

        if (!ValidateDatePicker(TripDetailsStartDatePicker, "data inicial"))
        {
            return false;
        }

        if (!ValidateDatePicker(TripDetailsEndDatePicker, "data final"))
        {
            return false;
        }

        if (draft.People < 1)
        {
            ShowTripDetailsError("Informe uma quantidade válida de pessoas.");
            return false;
        }

        TripDetailsErrorText.Visibility = Visibility.Collapsed;
        return true;
    }

    private void ShowTripDetailsError(string message)
    {
        TripDetailsErrorText.Text = message;
        TripDetailsErrorText.Visibility = Visibility.Visible;
    }

    private bool ValidateTripFolderName(string folderName)
    {
        if (folderName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
            folderName is "." or ".." ||
            !string.Equals(folderName, Path.GetFileName(folderName), StringComparison.Ordinal))
        {
            ShowTripDetailsError("Informe apenas o nome da pasta, sem barras ou caracteres inválidos.");
            return false;
        }

        return true;
    }

    private static void ApplyDraftToTrip(TripDetailsDraft draft, Trip trip)
    {
        trip.Title = draft.Title;
        trip.StartDate = DateOnly.TryParse(draft.StartDate, CultureInfo.InvariantCulture, out var startDate) ? startDate : null;
        trip.EndDate = DateOnly.TryParse(draft.EndDate, CultureInfo.InvariantCulture, out var endDate) ? endDate : null;
        trip.People = draft.People;
        trip.BaseCurrency = draft.BaseCurrency;
        trip.RateDecimalDigits = draft.RateDecimalDigits;
        trip.ItinerarySlotsPerDay = draft.ItinerarySlotsPerDay;
        trip.MyMapsUrl = draft.MyMapsUrl;
    }

    private bool ValidateDatePicker(DatePicker picker, string label)
    {
        if (!string.IsNullOrWhiteSpace(picker.Text) && picker.SelectedDate is null)
        {
            ShowTripDetailsError($"Selecione uma {label} válida.");
            return false;
        }

        return true;
    }

    private static DateTime? ParseDraftDate(string value)
    {
        return DateOnly.TryParse(value, CultureInfo.InvariantCulture, out var date)
            ? date.ToDateTime(TimeOnly.MinValue)
            : null;
    }

    private static string FormatDraftDate(DateTime? value)
    {
        return value is null
            ? ""
            : DateOnly.FromDateTime(value.Value).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
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

public sealed class BudgetPieChart : FrameworkElement
{
    public static readonly DependencyProperty ItemsSourceProperty = DependencyProperty.Register(
        nameof(ItemsSource),
        typeof(IEnumerable),
        typeof(BudgetPieChart),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender, OnItemsSourceChanged));

    private INotifyCollectionChanged? _observableItemsSource;

    public IEnumerable? ItemsSource
    {
        get => (IEnumerable?)GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    private static void OnItemsSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var chart = (BudgetPieChart)d;
        if (chart._observableItemsSource is not null)
        {
            chart._observableItemsSource.CollectionChanged -= chart.ItemsSource_CollectionChanged;
        }

        chart._observableItemsSource = e.NewValue as INotifyCollectionChanged;
        if (chart._observableItemsSource is not null)
        {
            chart._observableItemsSource.CollectionChanged += chart.ItemsSource_CollectionChanged;
        }

        chart.InvalidateVisual();
    }

    private void ItemsSource_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);

        var categories = ItemsSource?.OfType<BudgetCategoryViewModel>().Where(category => category.Total > 0).ToList() ?? [];
        var size = Math.Min(ActualWidth, ActualHeight);
        if (size <= 0)
        {
            return;
        }

        var center = new System.Windows.Point(ActualWidth / 2, ActualHeight / 2);
        var radius = (size / 2) - 3;
        var total = categories.Sum(category => category.Total);
        if (total <= 0)
        {
            drawingContext.DrawEllipse(new SolidColorBrush(System.Windows.Media.Color.FromRgb(232, 226, 215)), null, center, radius, radius);
            return;
        }

        var startAngle = -90d;
        foreach (var category in categories)
        {
            var sweepAngle = Math.Max(0.2, (double)(category.Total / total) * 360d);
            DrawSlice(drawingContext, center, radius, startAngle, sweepAngle, ParseBrush(category.Color));
            startAngle += sweepAngle;
        }

        drawingContext.DrawEllipse(System.Windows.Media.Brushes.White, null, center, radius * 0.48, radius * 0.48);
    }

    private static void DrawSlice(DrawingContext drawingContext, System.Windows.Point center, double radius, double startAngle, double sweepAngle, System.Windows.Media.Brush brush)
    {
        var start = PointOnCircle(center, radius, startAngle);
        var end = PointOnCircle(center, radius, startAngle + sweepAngle);
        var figure = new PathFigure { StartPoint = center, IsClosed = true, IsFilled = true };
        figure.Segments.Add(new LineSegment(start, true));
        figure.Segments.Add(new ArcSegment(
            end,
            new System.Windows.Size(radius, radius),
            0,
            sweepAngle > 180,
            SweepDirection.Clockwise,
            true));

        var geometry = new PathGeometry();
        geometry.Figures.Add(figure);
        drawingContext.DrawGeometry(brush, new System.Windows.Media.Pen(System.Windows.Media.Brushes.White, 1.5), geometry);
    }

    private static System.Windows.Point PointOnCircle(System.Windows.Point center, double radius, double angle)
    {
        var radians = angle * Math.PI / 180d;
        return new System.Windows.Point(center.X + (Math.Cos(radians) * radius), center.Y + (Math.Sin(radians) * radius));
    }

    private static System.Windows.Media.Brush ParseBrush(string color)
    {
        try
        {
            return (System.Windows.Media.Brush)new BrushConverter().ConvertFromString(color)!;
        }
        catch (FormatException)
        {
            return new SolidColorBrush(System.Windows.Media.Color.FromRgb(15, 118, 110));
        }
    }
}

public sealed class LocalSettings
{
    public string? RepositoryPath { get; set; }
    public bool IsSidebarCollapsed { get; set; }
    public double? WindowLeft { get; set; }
    public double? WindowTop { get; set; }
    public double? WindowWidth { get; set; }
    public double? WindowHeight { get; set; }
    public string? WindowState { get; set; }

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
