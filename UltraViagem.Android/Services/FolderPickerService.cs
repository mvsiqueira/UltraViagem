using Android.Content;

namespace UltraViagem.Android.Services;

public sealed class FolderPickerService
{
    private static TaskCompletionSource<global::Android.Net.Uri?>? _tcs;
    public const int RequestCode = 1001;

    public Task<global::Android.Net.Uri?> PickAsync()
    {
        _tcs = new TaskCompletionSource<global::Android.Net.Uri?>();
        var intent = new Intent(Intent.ActionOpenDocumentTree);
        intent.AddFlags(ActivityFlags.GrantReadUriPermission | ActivityFlags.GrantPersistableUriPermission);
        Platform.CurrentActivity!.StartActivityForResult(intent, RequestCode);
        return _tcs.Task;
    }

    public static void HandleResult(int requestCode, global::Android.App.Result resultCode, Intent? data)
    {
        if (requestCode != RequestCode) return;
        _tcs?.TrySetResult(resultCode == global::Android.App.Result.Ok ? data?.Data : null);
    }
}
