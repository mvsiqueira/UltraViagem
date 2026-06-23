namespace UltraViagem.Android.Controls;

/// <summary>
/// Border que usa GestureDetector nativo para expor Tapped e LongPressed sem conflito com
/// TapGestureRecognizer do MAUI. Necessário quando toque e segura devem cobrir a mesma área.
/// </summary>
public sealed class LongPressBorder : Border
{
    public event EventHandler? Tapped;
    public event EventHandler? LongPressed;

    private global::Android.Views.GestureDetector? _detector;

    protected override void OnHandlerChanged()
    {
        base.OnHandlerChanged();
        if (Handler?.PlatformView is global::Android.Views.View nv)
        {
            _detector = new global::Android.Views.GestureDetector(
                Platform.AppContext, new GestureListener(this));
            nv.Touch -= OnNativeTouch;
            nv.Touch += OnNativeTouch;
            nv.Clickable = true;
        }
    }

    // setOnTouchListener intercepta antes dos filhos — GestureDetector recebe todos os eventos
    private void OnNativeTouch(object? sender, global::Android.Views.View.TouchEventArgs e)
    {
        if (e.Event != null)
            _detector?.OnTouchEvent(e.Event);
        e.Handled = true;
    }

    private sealed class GestureListener : global::Android.Views.GestureDetector.SimpleOnGestureListener
    {
        private readonly LongPressBorder _owner;
        public GestureListener(LongPressBorder owner) => _owner = owner;

        // OnDown deve retornar true para o GestureDetector processar eventos subsequentes
        public override bool OnDown(global::Android.Views.MotionEvent? e) => true;

        public override bool OnSingleTapUp(global::Android.Views.MotionEvent? e)
        {
            MainThread.BeginInvokeOnMainThread(() => _owner.Tapped?.Invoke(_owner, EventArgs.Empty));
            return true;
        }

        public override void OnLongPress(global::Android.Views.MotionEvent? e)
            => MainThread.BeginInvokeOnMainThread(() => _owner.LongPressed?.Invoke(_owner, EventArgs.Empty));
    }
}
