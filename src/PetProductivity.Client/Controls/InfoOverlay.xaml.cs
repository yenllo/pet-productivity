namespace PetProductivity.Client.Controls;

/// <summary>
/// T31: overlay explicativo ("tap = explicación") con el estilo de la app. IsOpen es TwoWay:
/// cerrar (fondo o botón) escribe false de vuelta al VM. La tarjeta entra con pop.
/// </summary>
public partial class InfoOverlay : ContentView
{
    public static readonly BindableProperty IsOpenProperty =
        BindableProperty.Create(nameof(IsOpen), typeof(bool), typeof(InfoOverlay), false,
            BindingMode.TwoWay, propertyChanged: OnIsOpenChanged);
    // "HeaderText" y no "Title" para no chocar con nombres reservados de Page/Shell.
    public static readonly BindableProperty HeaderTextProperty =
        BindableProperty.Create(nameof(HeaderText), typeof(string), typeof(InfoOverlay), string.Empty,
            propertyChanged: (b, _, n) => ((InfoOverlay)b).TitleLabel.Text = (string)n);
    public static readonly BindableProperty BodyProperty =
        BindableProperty.Create(nameof(Body), typeof(string), typeof(InfoOverlay), string.Empty,
            propertyChanged: (b, _, n) => ((InfoOverlay)b).BodyLabel.Text = (string)n);

    public bool IsOpen { get => (bool)GetValue(IsOpenProperty); set => SetValue(IsOpenProperty, value); }
    public string HeaderText { get => (string)GetValue(HeaderTextProperty); set => SetValue(HeaderTextProperty, value); }
    public string Body { get => (string)GetValue(BodyProperty); set => SetValue(BodyProperty, value); }

    public InfoOverlay() => InitializeComponent();

    private static void OnIsOpenChanged(BindableObject b, object o, object n)
    {
        var self = (InfoOverlay)b;
        self.IsVisible = (bool)n;
        if ((bool)n) _ = Services.Anim.PopAsync(self.Card);
    }

    private void OnDismiss(object? sender, EventArgs e) => IsOpen = false;
}
