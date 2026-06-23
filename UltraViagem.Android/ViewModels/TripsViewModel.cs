using System.Collections.ObjectModel;
using System.Windows.Input;
using global::Android.Provider;
using UltraViagem.Android.Services;
using UltraViagem.Core;

namespace UltraViagem.Android.ViewModels;

public sealed class TripsViewModel : BindableObject
{
    private readonly TripFileService _service;
    private readonly FolderPickerService _folderPicker;
    private bool _isBusy;
    private string? _repoUri;
    private string? _repoLabel;

    public ObservableCollection<TripEntry> Trips { get; } = [];
    public Trip?     LoadedTrip          { get; private set; }
    public string?   LoadedTripUri       { get; private set; }
    public string?   LoadedTripFolderUri { get; private set; }
    public TripEntry? LastTrip     { get; private set; }

    public bool IsBusy
    {
        get => _isBusy;
        private set { _isBusy = value; OnPropertyChanged(); }
    }

    public bool   HasRepo          => _repoUri != null;
    public bool   HasLastTrip      => LastTrip != null;
    public bool   HasTrips         => Trips.Count > 0;
    public bool   HasPermissionError => _service.ScanPermissionDenied;
    public string RepoLabel        => _repoLabel ?? "Repositório";

    public ICommand SelectFolderCommand { get; }
    public ICommand OpenTripCommand     { get; }

    public TripsViewModel(TripFileService service, FolderPickerService folderPicker)
    {
        _service = service;
        _folderPicker = folderPicker;
        SelectFolderCommand = new Command(async () => await SelectFolderAsync());
        OpenTripCommand = new Command<TripEntry>(async e => await OpenTripAsync(e));
    }

    public async Task InitializeAsync()
    {
        var saved = _service.GetSavedRepoUri();
        var last  = _service.GetLastTrip();

        if (saved != null)
        {
            _repoUri = saved;
            _repoLabel = ExtractLabel(saved);
            OnPropertyChanged(nameof(HasRepo));
            OnPropertyChanged(nameof(RepoLabel));

            // 1) Exibe o cache imediatamente (se houver), evitando a espera do scan
            var cached  = _service.LoadTripsCache(saved);
            bool hasCache = cached is { Count: > 0 };
            if (hasCache)
            {
                ReplaceTrips(cached!);
                ResolveLastTrip(last);
            }

            // 2) Rescan: silencioso quando já há cache exibido; com spinner na primeira vez
            await ScanAsync(silent: hasCache);
        }

        // Resolve/atualiza a última viagem após o scan
        ResolveLastTrip(last);
    }

    private void ResolveLastTrip(TripEntry? last)
    {
        if (last == null) return;
        // Prefere o entry escaneado (título pode ter mudado)
        LastTrip = Trips.FirstOrDefault(t => t.UriString == last.UriString) ?? last;
        OnPropertyChanged(nameof(LastTrip));
        OnPropertyChanged(nameof(HasLastTrip));
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

    private async Task SelectFolderAsync()
    {
        if (IsBusy) return;
        var uri = await _folderPicker.PickAsync();
        if (uri == null) return;
        _service.SaveRepoUri(uri);
        _repoUri = uri.ToString();
        _repoLabel = ExtractLabel(_repoUri);
        OnPropertyChanged(nameof(HasRepo));
        OnPropertyChanged(nameof(RepoLabel));
        await ScanAsync();

        // Atualiza LastTrip com título atualizado após rescan
        if (LastTrip != null)
        {
            var refreshed = Trips.FirstOrDefault(t => t.UriString == LastTrip.UriString);
            if (refreshed != null)
            {
                LastTrip = refreshed;
                OnPropertyChanged(nameof(LastTrip));
            }
        }
    }

    private async Task ScanAsync(bool silent = false)
    {
        if (_repoUri == null) return;
        if (!silent)
        {
            if (IsBusy) return;
            IsBusy = true;
        }
        try
        {
            global::Android.Util.Log.Debug("UVDBG", $"ScanAsync: uri={_repoUri} silent={silent}");
            var entries = await _service.ScanRepositoryAsync(_repoUri);
            global::Android.Util.Log.Debug("UVDBG", $"ScanAsync: found {entries.Count} trips, permDenied={_service.ScanPermissionDenied}");
            OnPropertyChanged(nameof(HasPermissionError));

            if (_service.ScanPermissionDenied)
            {
                // Acesso perdido: num scan silencioso mantém o cache exibido;
                // num scan normal limpa a lista para mostrar o card de erro.
                if (!silent) ReplaceTrips([]);
                return;
            }

            // Atualiza a lista só se mudou (evita flicker no scan silencioso)
            if (!TripsMatch(entries))
                ReplaceTrips(entries);

            // Persiste o cache para a próxima abertura
            await _service.SaveTripsCacheAsync(_repoUri, entries);
        }
        finally { if (!silent) IsBusy = false; }
    }

    private async Task OpenTripAsync(TripEntry? entry)
    {
        if (entry == null || IsBusy) return;
        IsBusy = true;
        try
        {
            global::Android.Util.Log.Debug("UVDBG", $"OpenTripAsync: uri={entry.UriString}");
            var trip = await _service.LoadTripFromUriAsync(entry.UriString);
            global::Android.Util.Log.Debug("UVDBG", $"OpenTripAsync: trip={trip?.Title ?? "NULL"}");
            if (trip == null)
            {
                MainThread.BeginInvokeOnMainThread(async () =>
                    await Application.Current!.Windows[0].Page!.DisplayAlert(
                        "Não foi possível abrir",
                        "Não foi possível carregar a viagem. Se a pasta perdeu o acesso, use 'Trocar pasta' para reautorizar.",
                        "OK"));
                return;
            }
            if (trip != null)
            {
                _service.SaveLastTrip(entry);
                LastTrip             = entry;
                LoadedTrip           = trip;
                LoadedTripUri        = entry.UriString;
                LoadedTripFolderUri  = entry.FolderUri;
                OnPropertyChanged(nameof(LastTrip));
                OnPropertyChanged(nameof(HasLastTrip));
                OnPropertyChanged(nameof(LoadedTrip));
            }
        }
        finally { IsBusy = false; }
    }

    private static string ExtractLabel(string uriString)
    {
        try
        {
            var treeUri = global::Android.Net.Uri.Parse(uriString)!;
            var docId = DocumentsContract.GetTreeDocumentId(treeUri);
            if (docId != null)
            {
                // Local storage: "primary:Viagens" → "Viagens"
                var parts = docId.Split(new[] { ':', '/' }, StringSplitOptions.RemoveEmptyEntries);
                var last = parts.LastOrDefault() ?? "";
                if (!string.IsNullOrEmpty(last) && last.Length <= 30 && !last.Contains('='))
                    return last;

                // Google Drive e outros providers: consulta _display_name
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
