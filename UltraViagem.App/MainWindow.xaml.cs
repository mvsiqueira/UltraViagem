using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using UltraViagem.Core;
using Forms = System.Windows.Forms;

namespace UltraViagem.App;

public partial class MainWindow : Window
{
    private readonly AppViewModel _viewModel = new();
    private TripRepository? _repository;
    private readonly Dictionary<LinkEditorViewModel, (string Title, string Url)> _tipEditSnapshots = [];
    private readonly Dictionary<AttachmentEditorViewModel, string> _attachmentEditSnapshots = [];
    private System.Windows.Point _attachmentDragStartPoint;
    private bool _isLoadingTrip;
    private bool _isSavingTasks;
    private bool _isSavingTips;
    private bool _isSavingAttachments;

    public MainWindow()
    {
        InitializeComponent();
        DataContext = _viewModel;
        _viewModel.TasksChanged += ViewModel_TasksChanged;
        _viewModel.TipsChanged += ViewModel_TipsChanged;
        _viewModel.AttachmentsChanged += ViewModel_AttachmentsChanged;

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
        if (_viewModel.CurrentTrip is null)
        {
            return;
        }

        PopulateTripDetailsPanel();
        ShowTripDetails();
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
        _repository.SaveTrip(_viewModel.CurrentTrip);
        LoadTrip(_viewModel.CurrentTrip.Id);
        PopulateTripDetailsPanel();
        ShowTripDetails();
        _viewModel.StatusMessage = $"Viagem {draft.Title} atualizada.";
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

    private void OpenAttachment_Click(object sender, RoutedEventArgs e)
    {
        SelectAttachmentFromSender(sender);
        OpenSelectedAttachment();
    }

    private void AttachmentFileLink_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount != 2)
        {
            return;
        }

        if (SelectAttachmentFromSender(sender) is { } attachment)
        {
            BeginAttachmentEdit(attachment);
        }
        e.Handled = true;
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
                input.SelectAll();
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

        if (FindAncestor<ListBoxItem>(e.OriginalSource as DependencyObject) is not { DataContext: AttachmentEditorViewModel attachment })
        {
            return;
        }

        BeginAttachmentEdit(attachment);
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

    private string? ShowRenameDialog(string currentName)
    {
        var dialog = new Window
        {
            Title = "Renomear arquivo",
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
            Text = "Novo nome do arquivo",
            Foreground = (System.Windows.Media.Brush)FindResource("MutedTextBrush")
        });
        content.Children.Add(input);
        content.Children.Add(actions);
        dialog.Content = content;
        input.SelectAll();
        input.Focus();

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
    }

    private void ShowOverview()
    {
        OverviewPanel.Visibility = Visibility.Visible;
        TripDetailsPanel.Visibility = Visibility.Collapsed;
        TasksPanel.Visibility = Visibility.Collapsed;
        TipsPanel.Visibility = Visibility.Collapsed;
        MapPanel.Visibility = Visibility.Collapsed;
        FilesPanel.Visibility = Visibility.Collapsed;
        SetActiveNav(OverviewNavButton);
        RefreshMapBrowsers();
    }

    private void ShowTasks()
    {
        OverviewPanel.Visibility = Visibility.Collapsed;
        TripDetailsPanel.Visibility = Visibility.Collapsed;
        TasksPanel.Visibility = Visibility.Visible;
        TipsPanel.Visibility = Visibility.Collapsed;
        MapPanel.Visibility = Visibility.Collapsed;
        FilesPanel.Visibility = Visibility.Collapsed;
        SetActiveNav(TasksNavButton);
    }

    private void ShowTips()
    {
        OverviewPanel.Visibility = Visibility.Collapsed;
        TripDetailsPanel.Visibility = Visibility.Collapsed;
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
        TasksPanel.Visibility = Visibility.Collapsed;
        TipsPanel.Visibility = Visibility.Collapsed;
        MapPanel.Visibility = Visibility.Collapsed;
        FilesPanel.Visibility = Visibility.Collapsed;
        SetActiveNav(TripDetailsNavButton);
    }

    private void SetActiveNav(System.Windows.Controls.Button activeButton)
    {
        var accentBrush = (System.Windows.Media.Brush)FindResource("AccentBrush");
        foreach (var button in new[] { OverviewNavButton, TripDetailsNavButton, TasksNavButton, TipsNavButton, MapNavButton, FilesNavButton })
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

    private void SaveMapInternal(string message)
    {
        if (_repository is null || _viewModel.CurrentTrip is null)
        {
            _viewModel.StatusMessage = "Nenhuma viagem carregada para salvar.";
            return;
        }

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
            var fileName = Path.GetFileName(attachment.File.Trim());
            if (string.IsNullOrWhiteSpace(fileName))
            {
                _viewModel.StatusMessage = "Nome de arquivo inválido.";
                return false;
            }
            if (fileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            {
                _viewModel.StatusMessage = $"O nome {fileName} contém caracteres inválidos.";
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

    private void PopulateTripDetailsPanel()
    {
        if (_viewModel.CurrentTrip is null)
        {
            return;
        }

        var draft = TripDetailsWindow.FromTrip(_viewModel.CurrentTrip);
        TripDetailsTitleBox.Text = draft.Title;
        TripDetailsStartDateBox.Text = draft.StartDate;
        TripDetailsEndDateBox.Text = draft.EndDate;
        TripDetailsPeopleBox.Text = draft.People.ToString(CultureInfo.InvariantCulture);
        TripDetailsCurrencyBox.Text = draft.BaseCurrency;
        TripDetailsErrorText.Visibility = Visibility.Collapsed;
    }

    private TripDetailsDraft BuildTripDetailsDraft()
    {
        return new TripDetailsDraft
        {
            Id = _viewModel.CurrentTrip?.Id ?? "",
            Title = TripDetailsTitleBox.Text.Trim(),
            StartDate = TripDetailsStartDateBox.Text.Trim(),
            EndDate = TripDetailsEndDateBox.Text.Trim(),
            People = int.TryParse(TripDetailsPeopleBox.Text, CultureInfo.InvariantCulture, out var people) ? people : 0,
            BaseCurrency = string.IsNullOrWhiteSpace(TripDetailsCurrencyBox.Text) ? "BRL" : TripDetailsCurrencyBox.Text.Trim().ToUpperInvariant(),
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

        if (!string.IsNullOrWhiteSpace(draft.StartDate) && !DateOnly.TryParse(draft.StartDate, CultureInfo.InvariantCulture, out _))
        {
            ShowTripDetailsError("A data inicial precisa estar no formato aaaa-mm-dd.");
            return false;
        }

        if (!string.IsNullOrWhiteSpace(draft.EndDate) && !DateOnly.TryParse(draft.EndDate, CultureInfo.InvariantCulture, out _))
        {
            ShowTripDetailsError("A data final precisa estar no formato aaaa-mm-dd.");
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
