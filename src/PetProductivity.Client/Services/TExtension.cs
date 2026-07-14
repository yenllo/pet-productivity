namespace PetProductivity.Client.Services;

// #26: uso en XAML — Text="{loc:T 'Registrar progreso'}" con xmlns:loc="clr-namespace:PetProductivity.Client.Services".
// Se evalúa al inflar la página; el cambio de idioma reconstruye el Shell (como el tema).
[ContentProperty(nameof(K))]
public class TExtension : IMarkupExtension<string>
{
    public string K { get; set; } = string.Empty;

    public string ProvideValue(IServiceProvider serviceProvider) => L.T(K);
    object IMarkupExtension.ProvideValue(IServiceProvider serviceProvider) => ProvideValue(serviceProvider);
}

// #26: para StringFormat con texto ('🔥 {0} racha'): Converter={StaticResource LocFmt}, ConverterParameter='🔥 {0} racha'.
public class LocFormatConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture) =>
        L.F(parameter?.ToString() ?? "{0}", value ?? string.Empty);

    public object ConvertBack(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture) =>
        throw new NotSupportedException();
}
