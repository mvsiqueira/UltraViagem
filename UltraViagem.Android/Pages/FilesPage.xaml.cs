using System.Collections.ObjectModel;
using UltraViagem.Android.ViewModels;
using UltraViagem.Core;

namespace UltraViagem.Android.Pages;

// ── Wrapper observável para modo de seleção ───────────────────────────────────
public sealed class SelectableFile : BindableObject
{
    public AttachmentItem Attachment { get; }
    public string File => Attachment.File;

    private bool _isSelected;
    public bool IsSelected
    {
        get => _isSelected;
        set { if (_isSelected != value) { _isSelected = value; OnPropertyChanged(); } }
    }

    private bool _isSelectionMode;
    public bool IsSelectionMode
    {
        get => _isSelectionMode;
        set { if (_isSelectionMode != value) { _isSelectionMode = value; OnPropertyChanged(); OnPropertyChanged(nameof(ShowArrow)); } }
    }

    public bool ShowArrow => !_isSelectionMode;

    public SelectableFile(AttachmentItem a) => Attachment = a;
}

// ── FilesPage ─────────────────────────────────────────────────────────────────
public partial class FilesPage : ContentPage
{
    public ObservableCollection<SelectableFile> Files { get; } = [];

    private bool _isSelectionMode;

    public FilesPage()
    {
        InitializeComponent();
        if (TripViewModel.Current?.Trip.Attachments is { } list)
            foreach (var a in list) Files.Add(new SelectableFile(a));
        FilesList.ItemsSource = Files;
    }

    // ── Gestos ────────────────────────────────────────────────────────────────

    private void OnFileTapped(object? sender, EventArgs e)
    {
        if (sender is not BindableObject bo || bo.BindingContext is not SelectableFile sf) return;
        if (_isSelectionMode)
        {
            sf.IsSelected = !sf.IsSelected;
            UpdateActionBar();
            if (!Files.Any(f => f.IsSelected))
                ExitSelectionMode();
        }
        else
        {
            _ = TripViewModel.Current!.OpenAttachmentAsync(sf.Attachment);
        }
    }

    private void OnFileLongPressed(object? sender, EventArgs e)
    {
        if (sender is not BindableObject bo || bo.BindingContext is not SelectableFile sf) return;
        if (!_isSelectionMode) EnterSelectionMode(sf);
    }

    // ── Modo seleção ──────────────────────────────────────────────────────────

    private void EnterSelectionMode(SelectableFile initial)
    {
        _isSelectionMode = true;
        foreach (var f in Files) f.IsSelectionMode = true;
        initial.IsSelected = true;
        ActionBar.IsVisible = true;
        UpdateActionBar();
    }

    private void ExitSelectionMode()
    {
        _isSelectionMode = false;
        foreach (var f in Files) { f.IsSelectionMode = false; f.IsSelected = false; }
        ActionBar.IsVisible = false;
    }

    private void UpdateActionBar()
    {
        var count = Files.Count(f => f.IsSelected);
        SelectionCountLabel.Text = count == 1 ? "1 selecionado" : $"{count} selecionados";
    }

    private void OnCancelSelectionClicked(object? sender, EventArgs e) => ExitSelectionMode();

    // ── Ações ─────────────────────────────────────────────────────────────────

    private async void OnDeleteClicked(object? sender, EventArgs e)
    {
        var selected = Files.Where(f => f.IsSelected).ToList();
        if (selected.Count == 0) return;

        var page = GetModalPage();
        var confirm = await page.DisplayAlert(
            "Excluir arquivos",
            $"Excluir {selected.Count} arquivo{(selected.Count > 1 ? "s" : "")} permanentemente?",
            "Excluir", "Cancelar");
        if (!confirm) return;

        foreach (var sf in selected)
        {
            await TripViewModel.Current!.DeleteAttachmentAsync(sf.Attachment);
            Files.Remove(sf);
        }
        ExitSelectionMode();
    }

    private async void OnDownloadClicked(object? sender, EventArgs e)
    {
        var selected = Files.Where(f => f.IsSelected).ToList();
        if (selected.Count == 0) return;

        int ok = 0;
        foreach (var sf in selected)
            if (await TripViewModel.Current!.DownloadAttachmentAsync(sf.Attachment)) ok++;

        ExitSelectionMode();
        var page = GetModalPage();
        if (ok > 0)
            await page.DisplayAlert("Download concluído",
                $"{ok} arquivo{(ok > 1 ? "s salvos" : " salvo")} em Downloads/UltraViagem.",
                "OK");
        else
            await page.DisplayAlert("Erro", "Não foi possível baixar os arquivos.", "OK");
    }

    private static Page GetModalPage()
    {
        var root = Application.Current!.Windows[0].Page!;
        return root.Navigation.ModalStack.LastOrDefault() ?? root;
    }
}
