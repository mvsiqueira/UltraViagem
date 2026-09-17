using System.Collections.ObjectModel;
using System.Windows.Input;
using global::Android.Provider;
using UltraViagem.Android.Services;
using UltraViagem.Core;

namespace UltraViagem.Android.ViewModels;

public sealed class TripsViewModel : BindableObject
{
    private readonly TripFileService    _saf;
    private readonly GoogleDriveStorage _gdrive;
    private readonly GoogleAuthService  _auth;
    private readonly FolderPickerService _folderPicker;

    private ITripStorage? _storage;      // backend ativo
    private bool   _isBusy;
    private string _repoKind = "saf";
    private string? _repoRef;
    private string? _repoLabel;

    public ObservableCollection<TripEntry> Trips { get; } = [];
    public Trip?     LoadedTrip          { get; private set; }
    public string?   LoadedTripUri       { get; private set; }
    public string?   LoadedTripFolderUri { get; private set; }
    public ITripStorage? LoadedTripStorage { get; private set; }
    public TripEntry? LastTrip           { get; private set; }

    public GoogleDriveStorage Drive => _gdrive;   // usado pela tela de escolha de pasta do Drive

    public bool IsBusy
    {
        get => _isBusy;
        private set { _isBusy = value; OnPropertyChanged(); }
    }

    public bool   HasRepo            => _repoRef != null;
    public bool   HasLastTrip        => LastTrip != null;
    public bool   HasTrips           => Trips.Count > 0;
    public bool   HasPermissionError => _storage?.AccessDenied ?? false;
    public string RepoLabel          => _repoLabel ?? "Repositório";

    public ICommand OpenTripCommand { get; }

    public TripsViewModel(TripFileService saf, GoogleDriveStorage gdrive,
                          GoogleAuthService auth, FolderPickerService folderPicker)
    {
        _saf          = saf;
        _gdrive       = gdrive;
        _auth         = auth;
        _folderPicker = folderPicker;
        OpenTripCommand = new Command<TripEntry>(async e => await OpenTripAsync(e));
    }

    private ITripStorage ResolveStorage(string kind) => kind == "gdrive" ? _gdrive : _saf;

    public async Task InitializeAsync()
    {
        _repoKind  = _saf.GetSavedRepoKind();
        _repoRef   = _saf.GetSavedRepoUri();
        _repoLabel = _saf.GetSavedRepoLabel();
        _storage   = ResolveStorage(_repoKind);

        var last = _saf.GetLastTrip(_repoRef);

        if (_repoRef != null)
        {
            OnPropertyChanged(nameof(HasRepo));
            OnPropertyChanged(nameof(RepoLabel));

            // 1) Exibe o cache imediatamente (se houver)
            var cached  = _saf.LoadTripsCache(_repoRef);
            bool hasCache = cached is { Count: > 0 };
            if (hasCache)
            {
                ReplaceTrips(cached!);
                ResolveLastTrip(last);
            }

            // 2) Rescan (silencioso quando já há cache exibido)
            await ScanAsync(silent: hasCache);
        }

        ResolveLastTrip(last);
    }

    // ── Seleção de provedor ──────────────────────────────────

    /// <summary>Seleciona uma pasta local via SAF (armazenamento interno).</summary>
    public async Task SelectSafFolderAsync()
    {
        if (IsBusy) return;
        var uri = await _folderPicker.PickAsync();
        if (uri == null) return;

        _saf.SaveRepoUri(uri);                       // pega permissão + salva kind "saf"
        _repoKind  = "saf";
        _repoRef   = uri.ToString();
        _repoLabel = ExtractLabel(_repoRef);
        _saf.SaveRepoRef("saf", _repoRef, _repoLabel);
        _storage   = _saf;
        ClearLastTrip();

        OnPropertyChanged(nameof(HasRepo));
        OnPropertyChanged(nameof(RepoLabel));
        await ScanAsync();
        RefreshLastTripTitle();
    }

    /// <summary>Faz login na conta Google (necessário antes de escolher a pasta do Drive).</summary>
    public Task<bool> SignInGoogleAsync() => _auth.SignInAsync();

    /// <summary>Conecta o repositório ao Google Drive, apontando para a pasta escolhida.</summary>
    public async Task ConnectGoogleAsync(string folderId, string folderName)
    {
        _repoKind  = "gdrive";
        _repoRef   = folderId;
        _repoLabel = folderName;
        _storage   = _gdrive;
        _saf.SaveRepoRef("gdrive", folderId, folderName);
        ClearLastTrip();

        OnPropertyChanged(nameof(HasRepo));
        OnPropertyChanged(nameof(RepoLabel));
        await ScanAsync();
        RefreshLastTripTitle();
    }

    // ── Scan / abrir ─────────────────────────────────────────

    private async Task ScanAsync(bool silent = false)
    {
        if (_repoRef == null || _storage == null) return;
        if (!silent)
        {
            if (IsBusy) return;
            IsBusy = true;
        }
        try
        {
            var entries = await _storage.ScanTripsAsync(_repoRef);
            OnPropertyChanged(nameof(HasPermissionError));

            if (_storage.AccessDenied)
            {
                if (!silent) ReplaceTrips([]);
                return;
            }

            if (!TripsMatch(entries))
                ReplaceTrips(entries);

            await _saf.SaveTripsCacheAsync(_repoRef, entries);
        }
        finally { if (!silent) IsBusy = false; }
    }

    private async Task OpenTripAsync(TripEntry? entry)
    {
        if (entry == null || IsBusy || _storage == null) return;
        IsBusy = true;
        try
        {
            var trip = await _storage.LoadTripAsync(entry.UriString);
            if (trip == null)
            {
                MainThread.BeginInvokeOnMainThread(async () =>
                    await Application.Current!.Windows[0].Page!.DisplayAlert(
                        "Não foi possível abrir",
                        "Não foi possível carregar a viagem. Se o acesso foi perdido, use 'Trocar pasta' para reautorizar.",
                        "OK"));
                return;
            }

            _saf.SaveLastTrip(entry, _repoRef);
            LastTrip            = entry;
            LoadedTrip          = trip;
            LoadedTripUri       = entry.UriString;
            LoadedTripFolderUri = entry.FolderUri;
            LoadedTripStorage   = _storage;
            OnPropertyChanged(nameof(LastTrip));
            OnPropertyChanged(nameof(HasLastTrip));
            OnPropertyChanged(nameof(LoadedTrip));
        }
        finally { IsBusy = false; }
    }

    // ── Helpers ──────────────────────────────────────────────

    private void ResolveLastTrip(TripEntry? last)
    {
        if (last == null) return;
        LastTrip = Trips.FirstOrDefault(t => t.UriString == last.UriString) ?? last;
        OnPropertyChanged(nameof(LastTrip));
        OnPropertyChanged(nameof(HasLastTrip));
    }

    private void ClearLastTrip()
    {
        LastTrip = null;
        OnPropertyChanged(nameof(LastTrip));
        OnPropertyChanged(nameof(HasLastTrip));
    }

    private void RefreshLastTripTitle()
    {
        if (LastTrip == null) return;
        var refreshed = Trips.FirstOrDefault(t => t.UriString == LastTrip.UriString);
        if (refreshed != null)
        {
            LastTrip = refreshed;
            OnPropertyChanged(nameof(LastTrip));
        }
    }

    private void ReplaceTrips(IEnumerable<TripEntry> entries)
    {
        Trips.Clear();
        foreach (var e in entries) Trips.Add(e);
        OnPropertyChanged(nameof(HasTrips));
    }

    private bool TripsMatch(List<TripEntry> entries)
    {
        if (entries.Count != Trips.Count) return false;
        for (int i = 0; i < entries.Count; i++)
            if (entries[i].UriString != Trips[i].UriString) return false;
        return true;
    }

    private static string ExtractLabel(string uriString)
    {
        try
        {
            var treeUri = global::Android.Net.Uri.Parse(uriString)!;
            var docId = DocumentsContract.GetTreeDocumentId(treeUri);
            if (docId != null)
            {
                var parts = docId.Split(new[] { ':', '/' }, StringSplitOptions.RemoveEmptyEntries);
                var last = parts.LastOrDefault() ?? "";
                if (!string.IsNullOrEmpty(last) && last.Length <= 30 && !last.Contains('='))
                    return last;

                var docUri = DocumentsContract.BuildDocumentUriUsingTree(treeUri, docId)!;
                using var cursor = Platform.AppContext.ContentResolver!.Query(
                    docUri, new[] { "_display_name" }, null, null, null);
                if (cursor != null && cursor.MoveToFirst())
                {
                    var name = cursor.GetString(0);
                    if (!string.IsNullOrEmpty(name)) return name;
                }
            }
        }
        catch { }
        return "Repositório";
    }
}
