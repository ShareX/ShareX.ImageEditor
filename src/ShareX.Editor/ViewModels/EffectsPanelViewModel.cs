using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShareX.Editor.Services;
using System.Collections.ObjectModel;

namespace ShareX.Editor.ViewModels;

/// <summary>
/// ViewModel for the Effects Panel.
/// </summary>
public partial class EffectsPanelViewModel : ObservableObject
{
    [ObservableProperty]
    private ObservableCollection<EffectViewModel> _availableEffects = new();

    [ObservableProperty]
    private ObservableCollection<string> _categories = new();

    [ObservableProperty]
    private string? _selectedCategory;

    [ObservableProperty]
    private EffectViewModel? _selectedEffect;

    public EffectsPanelViewModel()
    {
        LoadEffects();
        LoadCategories();
    }

    private void LoadEffects()
    {
        var effects = EffectCatalogService.GetAllEffects();
        AvailableEffects = new ObservableCollection<EffectViewModel>(effects);
    }

    private void LoadCategories()
    {
        var categories = EffectCatalogService.GetCategories();
        Categories = new ObservableCollection<string>(categories);

        if (Categories.Any())
        {
            SelectedCategory = Categories.First();
        }
    }

    partial void OnSelectedCategoryChanged(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            LoadEffects();
            return;
        }

        var filtered = EffectCatalogService.GetAllEffects()
            .Where(e => e.Category.Equals(value, System.StringComparison.OrdinalIgnoreCase))
            .ToList();

        AvailableEffects.Clear();
        foreach (var effect in filtered)
        {
            AvailableEffects.Add(effect);
        }
    }

    [RelayCommand]
    private void ResetEffect()
    {
        SelectedEffect?.ResetParameters();
    }

    [RelayCommand]
    private void SelectCategory(string category)
    {
        SelectedCategory = category;
    }
}
