namespace UltraViagem.Android.Services;

/// <summary>
/// Navegação de pastas de um provedor de nuvem, usada pela tela de escolha da pasta de
/// viagens. O id "root" representa a raiz do provedor.
/// </summary>
public interface ICloudFolderBrowser
{
    /// <summary>Nome exibido para a raiz (ex.: "Meu Drive", "OneDrive").</summary>
    string RootName { get; }

    /// <summary>Lista as subpastas de <paramref name="parentId"/> ("root" para a raiz). Null em caso de erro.</summary>
    Task<List<DriveFolder>?> ListFoldersAsync(string parentId);
}

public sealed record DriveFolder(string Id, string Name);
