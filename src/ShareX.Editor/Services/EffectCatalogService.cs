using ShareX.Editor.Helpers;
using ShareX.Editor.ImageEffects;
using ShareX.Editor.ViewModels;

namespace ShareX.Editor.Services
{
    /// <summary>
    /// Service for discovering and cataloging all available ImageEffects
    /// </summary>
    public static class EffectCatalogService
    {
        /// <summary>
        /// Get all available effects from the ImageEffects assembly
        /// </summary>
        public static List<EffectViewModel> GetAllEffects()
        {
            var assembly = typeof(ImageEffect).Assembly;
            var effectTypes = assembly.GetTypes()
                .Where(t => t.IsSubclassOf(typeof(ImageEffect)) && !t.IsAbstract)
                .ToList();

            var effectViewModels = new List<EffectViewModel>();

            foreach (var type in effectTypes)
            {
                try
                {
                    var instance = (ImageEffect?)Activator.CreateInstance(type);
                    if (instance != null)
                    {
                        effectViewModels.Add(new EffectViewModel(instance));
                    }
                }
                catch (Exception ex)
                {
                    // Log or ignore effects that fail to instantiate
                    DebugHelper.WriteException(ex, $"Failed to instantiate effect: {type.Name}");
                }
            }

            return effectViewModels;
        }

        /// <summary>
        /// Get effects filtered by category
        /// </summary>
        public static List<EffectViewModel> GetEffectsByCategory(string category)
        {
            return GetAllEffects()
                .Where(e => e.Category.Equals(category, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        /// <summary>
        /// Get all unique effect categories
        /// </summary>
        public static List<string> GetCategories()
        {
            return GetAllEffects()
                .Select(e => e.Category)
                .Distinct()
                .OrderBy(c => c)
                .ToList();
        }
    }
}
