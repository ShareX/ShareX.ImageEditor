using CommunityToolkit.Mvvm.ComponentModel;
using ShareX.Editor.ImageEffects;
using SkiaSharp;
using System.Reflection;

namespace ShareX.Editor.ViewModels;

/// <summary>
/// ViewModel wrapper for ImageEffect instances with observable properties.
/// </summary>
public partial class EffectViewModel : ObservableObject
{
    private readonly ImageEffect _effectInstance;

    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string _description = string.Empty;

    [ObservableProperty]
    private string _category = string.Empty;

    public ImageEffect EffectInstance => _effectInstance;

    public EffectViewModel(ImageEffect effect)
    {
        _effectInstance = effect ?? throw new ArgumentNullException(nameof(effect));

        var type = effect.GetType();
        _name = type.Name;
        _category = DetermineCategory(type);
        _description = GetDescriptionFromAttribute(type);
    }

    public SKBitmap Apply(SKBitmap bitmap) => _effectInstance.Apply(bitmap);

    public void ResetParameters()
    {
        var type = _effectInstance.GetType();
        var newInstance = Activator.CreateInstance(type);

        foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (prop.CanWrite && prop.CanRead)
            {
                var value = prop.GetValue(newInstance);
                prop.SetValue(_effectInstance, value);
            }
        }
    }

    private static string DetermineCategory(Type effectType)
    {
        var ns = effectType.Namespace ?? string.Empty;

        if (ns.Contains("Filters")) return "Filters";
        if (ns.Contains("Adjustments")) return "Adjustments";
        if (ns.Contains("Manipulations")) return "Manipulations";
        if (ns.Contains("Drawings")) return "Drawings";

        return "Other";
    }

    private static string GetDescriptionFromAttribute(Type effectType)
    {
        var attr = effectType.GetCustomAttribute<System.ComponentModel.DescriptionAttribute>();
        return attr?.Description ?? "No description available";
    }
}
