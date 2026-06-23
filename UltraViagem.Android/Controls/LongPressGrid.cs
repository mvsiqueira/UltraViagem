namespace UltraViagem.Android.Controls;

/// <summary>
/// Grid que expõe o evento LongPressed via LongClick nativo do Android.
/// </summary>
public sealed class LongPressGrid : Grid
{
    public event EventHandler? LongPressed;

    protected override void OnHandlerChanged()
    {
        base.OnHandlerChanged();
        if (Handler?.PlatformView is global::Android.Views.View nv)
        {
            nv.LongClickable = true;
            nv.LongClick -= OnNativeLongClick;
            nv.LongClick += OnNativeLongClick;
        }
    }

    private void OnNativeLongClick(object? sender, global::Android.Views.View.LongClickEventArgs e)
    {
        MainThread.BeginInvokeOnMainThread(() => LongPressed?.Invoke(this, EventArgs.Empty));
        e.Handled = true;
    }
}
