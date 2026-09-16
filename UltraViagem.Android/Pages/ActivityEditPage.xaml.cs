using UltraViagem.Core;

namespace UltraViagem.Android.Pages;

public sealed record ActivityEditResult(string Title, string Type, string Color, string? Details);

public partial class ActivityEditPage : ContentPage
{
    // Paleta de cores para atividades (pastéis, texto escuro legível por cima)
    private static readonly string[] Palette =
    {
        "#DBEAFE", "#CCFBF1", "#DCFCE7", "#D9F99D",
        "#FEF3C7", "#FED7AA", "#FEE2E2", "#FCE7F3",
        "#EDE9FE", "#E5E7EB",
    };

    private readonly TaskCompletionSource<ActivityEditResult?> _tcs = new();
    private readonly List<Border> _swatches = new();
    private string _selectedColor;

    public Task<ActivityEditResult?> Result => _tcs.Task;

    public ActivityEditPage(ItineraryActivity activity)
    {
        InitializeComponent();

        TitleEntry.Text   = activity.Title;
        TypeEntry.Text    = activity.Type;
        DetailsEntry.Text = activity.Details;
        _selectedColor    = string.IsNullOrWhiteSpace(activity.Color) ? Palette[0] : activity.Color;

        BuildPalette();
    }

    private void BuildPalette()
    {
        // Garante que a cor atual apareça na paleta mesmo se for fora do conjunto
        var colors = Palette.ToList();
        if (!colors.Any(c => string.Equals(c, _selectedColor, StringComparison.OrdinalIgnoreCase)))
            colors.Insert(0, _selectedColor);

        foreach (var hex in colors)
        {
            var swatch = new Border
            {
                WidthRequest = 40,
                HeightRequest = 40,
                Margin = new Thickness(0, 0, 10, 10),
                BackgroundColor = Color.FromArgb(hex),
                StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 8 },
                StrokeThickness = 3,
                Stroke = Colors.Transparent,
            };
            var tap = new TapGestureRecognizer();
            tap.Tapped += (_, _) => Select(hex);
            swatch.GestureRecognizers.Add(tap);
            _swatches.Add(swatch);
            ColorPalette.Children.Add(swatch);
        }
        UpdateSelectionRing();
    }

    private void Select(string hex)
    {
        _selectedColor = hex;
        UpdateSelectionRing();
    }

    private void UpdateSelectionRing()
    {
        var accent = (Color)Application.Current!.Resources["Accent"];
        for (int i = 0; i < _swatches.Count; i++)
        {
            var sw = _swatches[i];
            bool selected = sw.BackgroundColor is { } bg &&
                            string.Equals(bg.ToArgbHex().Substring(0, 7), _selectedColor, StringComparison.OrdinalIgnoreCase);
            sw.Stroke = selected ? accent : Colors.Transparent;
        }
    }

    private void OnCancelClicked(object? sender, EventArgs e)
    {
        _tcs.TrySetResult(null);
        _ = Navigation.PopModalAsync();
    }

    private void OnSaveClicked(object? sender, EventArgs e)
    {
        var title = TitleEntry.Text?.Trim() ?? "";
        if (string.IsNullOrEmpty(title))
        {
            TitleEntry.Focus();
            return;
        }

        var result = new ActivityEditResult(
            title,
            (TypeEntry.Text ?? "").Trim(),
            _selectedColor,
            string.IsNullOrWhiteSpace(DetailsEntry.Text) ? null : DetailsEntry.Text.Trim());

        _tcs.TrySetResult(result);
        _ = Navigation.PopModalAsync();
    }

    protected override bool OnBackButtonPressed()
    {
        _tcs.TrySetResult(null);
        return base.OnBackButtonPressed();
    }
}
