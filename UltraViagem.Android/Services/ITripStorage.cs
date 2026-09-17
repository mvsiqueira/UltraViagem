using UltraViagem.Core;

namespace UltraViagem.Android.Services;

/// <summary>
/// Abstração do armazenamento de viagens, independente do provedor.
/// Implementações: <see cref="TripFileService"/> (SAF/armazenamento local) e,
/// futuramente, backends de nuvem via API (Google Drive, OneDrive).
///
/// As "refs" (repoRef, tripRef, folderRef) são identificadores opacos de cada
/// backend: no SAF são URIs de documento; na nuvem serão ids de arquivo/pasta.
/// O restante do app não interpreta esses valores — só os repassa.
/// </summary>
public interface ITripStorage
{
    /// <summary>Identificador do backend ("saf", "gdrive", ...). Usado para saber a origem do repositório salvo.</summary>
    string Kind { get; }

    /// <summary>True se o último <see cref="ScanTripsAsync"/> falhou por falta de acesso (permissão revogada / não autorizado).</summary>
    bool AccessDenied { get; }

    /// <summary>Varre o repositório procurando subpastas com <c>trip.json</c> e retorna uma entrada por viagem.</summary>
    Task<List<TripEntry>> ScanTripsAsync(string repoRef);

    /// <summary>Carrega a viagem apontada por <paramref name="tripRef"/> (o <c>trip.json</c>).</summary>
    Task<Trip?> LoadTripAsync(string tripRef);

    /// <summary>Grava a viagem no <c>trip.json</c> apontado por <paramref name="tripRef"/>.</summary>
    Task<bool> SaveTripAsync(string tripRef, Trip trip);

    /// <summary>Abre um anexo (arquivo irmão do <c>trip.json</c>) para leitura. Retorna null se não encontrado.</summary>
    Task<Stream?> OpenAttachmentAsync(string tripRef, string? folderRef, string filename);

    /// <summary>Exclui um anexo da pasta da viagem.</summary>
    Task<bool> DeleteAttachmentAsync(string tripRef, string? folderRef, string filename);
}
