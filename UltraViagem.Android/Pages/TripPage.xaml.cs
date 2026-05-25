using UltraViagem.Android.ViewModels;

namespace UltraViagem.Android.Pages;

public partial class TripPage : TabbedPage
{
    public TripPage()
    {
        InitializeComponent();

        var vm = TripViewModel.Current;
        if (vm is not null)
        {
            // Propaga o VM para todas as tabs filhas
            foreach (var page in Children)
                page.BindingContext = vm;

            Title = vm.Trip.Title;
        }
    }
}
