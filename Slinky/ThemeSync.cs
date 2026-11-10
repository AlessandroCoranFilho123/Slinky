namespace Slinky.Services
{
    /// <summary>
    /// Pure decision for cross-process theme sync: ApplicationData.LocalSettings is
    /// shared by every process of the same package, so a second instance only needs
    /// to re-read the stored name when it becomes active. Returns the theme to switch
    /// to, or null when there is nothing to change (missing/invalid name or no change).
    /// </summary>
    public static class ThemeSync
    {
        public static AppTheme? Resolve(string? storedName, AppTheme current)
        {
            if (string.IsNullOrEmpty(storedName)
                || !System.Enum.TryParse<AppTheme>(storedName, out var stored))
            {
                return null;
            }

            return stored == current ? null : stored;
        }
    }
}
