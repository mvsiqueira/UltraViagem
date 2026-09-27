using System.Collections.ObjectModel;
using System.Windows.Input;
using global::Android.Provider;
using UltraViagem.Android.Services;
using UltraViagem.Core;

namespace UltraViagem.Android.ViewModels;

public sealed class TripsViewModel : BindableObject
{
    private readonly TripFileService     _saf;
    private readonly GoogleDriveStorage  _gdrive;
    private readonly GoogleAuthService   _googleAuth;
    private readonly OneDriveStorage     _onedrive;
    private readonly OneDriveAuthService _msAuth;
    private readonly FolderPickerService _folderPicker;

    private ITripStorage? _storage;      // backend ativo
    private bool   _isBusy;
    private bool   _scanning;            // guarda de scan concorrente (separada de IsBusy)
    private string _repoKind = "saf";
    private string? _repoRef;
    private string? _repoLabel;
    private TripEntry? _pendingLast;     // última viagem salva, re-resolvida após o scan

    public ObservableCollection<TripEntry> Trips { get; } = [];
    public Trip?     LoadedTrip          { get; private set; }
    public string?   LoadedTripUri       { get; private set; }
    public string?   LoadedTripFolderUri { get; private set; }
    public ITripStorage? LoadedTripStorage { get; private set; }
    public TripEntry? LastTrip           { get; private set; }

    /// <summary>Navegador de pastas do provedor de nuvem ("gdrive" ou "onedrive"), para a tela de escolha de pasta.</summary>
    public ICloudFolderBrowser GetFolderBrowser(string kind) => kind == "onedrive" ? _onedrive : _gdrive;

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

    public TripsViewModel(TripFileService saf,
                          GoogleDriveStorage gdrive, GoogleAuthService googleAuth,
                          OneDriveStorage onedrive, OneDriveAuthService msAuth,
                          FolderPickerService folderPicker)
    {
        _saf          = saf;
        _gdrive       = gdrive;
        _googleAuth   = googleAuth;
        _onedrive     = onedrive;
        _msAuth       = msAuth;
        _folderPicker = folderPicker;
        OpenTripCommand = new Command<TripEntry>(async e => await OpenTripAsync(e));
    }

    private ITripStorage ResolveStorage(string kind) => kind switch
    {
        "gdrive"   => _gdrive,
        "onedrive" => _onedrive,
        _          => _saf,
    };

    /// <summary>
    /// Prepara o repositório de forma rápida (sem varrer a nuvem): resolve o backend,
    /// exibe o cache e a última viagem. Assim a abertura direta na última viagem não
    /// precisa esperar a varredura completa (que roda depois em <see cref="RescanAsync"/>).
    /// </summary>
    public void PrepareRepo()
    {
        _repoKind  = _saf.GetSavedRepoKind();
        _repoRef   = _saf.GetSavedRepoUri();
        _repoLabel = _saf.GetSavedRepoLabel();
        _storage   = ResolveStorage(_repoKind);
        _pendingLast = _saf.GetLastTrip(_repoRef);

        if (_repoRef != null)
        {
            OnPropertyChanged(nameof(HasRepo));
            OnPropertyChanged(nameof(RepoLabel));
            var cached = _saf.LoadTripsCache(_repoRef);
            if (cached is { Count: > 0 }) ReplaceTrips(cached);
        }
        ResolveLastTrip(_pendingLast);
    }

    /// <summary>Varre o repositório e atualiza a lista. Pode rodar em segundo plano.</summary>
    public async Task RescanAsync()
    {
        if (_repoRef == null) return;
        await ScanAsync(silent: Trips.Count > 0);   // silencioso se o cache já preenche a lista
        ResolveLastTrip(_pendingLast);
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

    /// <summary>
    /// Garante a sessão no provedor ("gdrive" ou "onedrive") antes de escolher a pasta:
    /// reaproveita o login salvo e só pede login de novo se não houver sessão válida.
    /// </summary>
    public Task<bool> EnsureCloudSignInAsync(string kind)
        => kind == "onedrive" ? _msAuth.EnsureSignedInAsync() : _googleAuth.EnsureSignedInAsync();

    /// <summary>Conecta o repositório a um provedor de nuvem, apontando para a pasta escolhida.</summary>
    public async Task ConnectCloudAsync(string kind, string folderId, string folderName)
    {
        _repoKind  = kind;
        _repoRef   = folderId;
        _repoLabel = folderName;
        _storage   = ResolveStorage(kind);
        _saf.SaveRepoRef(kind, folderId, folderName);
        ClearLastTrip();

        OnPropertyChanged(nameof(HasRepo));
        OnPropertyChanged(nameof(RepoLabel));
        await ScanAsync();
        RefreshLastTripTitle();
    }

    // ── Scan / abrir ─────────────────────────────────────────

    private async Task ScanAsync(bool silent = false)
    {
        if (_repoRef == null || _storage == null || _scanning) return;
        _scanning = true;
        if (!silent) IsBusy = true;
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
        finally { _scanning = false; if (!silent) IsBusy = false; }
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
