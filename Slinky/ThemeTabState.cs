using System;
using System.Collections.Generic;
using System.Linq;

namespace Slinky.Services
{
    /// <summary>
    /// WinRT-free visual contract shared by the theme service and the tab chrome.
    /// Keeping the complete state in one immutable value prevents an existing tab
    /// from accidentally retaining only the Light/Dark system mode while missing a
    /// same-group palette change such as Dracula -> Bluish.
    /// </summary>
    public readonly record struct ThemeTabState(
        AppTheme Theme,
        bool IsDark,
        (byte R, byte G, byte B) Background,
        (byte R, byte G, byte B) Foreground,
        (byte R, byte G, byte B) Surface,
        (byte R, byte G, byte B) Accent)
    {
        public static ThemeTabState For(AppTheme theme) => new(
            theme,
            theme is not AppTheme.LightDefault and not AppTheme.LightYellowish,
            ThemePalette.Background(theme),
            ThemePalette.Foreground(theme),
            ThemePalette.Surface(theme),
            ThemePalette.Accent(theme));
    }

    /// <summary>
    /// Small UI-independent coordinator used by MainWindow to update every materialized
    /// tab. It is intentionally keyed by object identity so a closed tab can be removed
    /// without keeping its visual tree alive.
    /// </summary>
    public sealed class ThemeTabStateHub
    {
        private readonly Dictionary<object, Action<ThemeTabState>> subscribers = new();

        public ThemeTabState Current { get; private set; }

        public ThemeTabStateHub(AppTheme initialTheme)
        {
            Current = ThemeTabState.For(initialTheme);
        }

        public void Register(object key, Action<ThemeTabState> apply)
        {
            ArgumentNullException.ThrowIfNull(key);
            ArgumentNullException.ThrowIfNull(apply);

            subscribers[key] = apply;
            apply(Current);
        }

        public bool Unregister(object key) => subscribers.Remove(key);

        public void Apply(AppTheme theme)
        {
            Current = ThemeTabState.For(theme);

            // Snapshot the callbacks so a callback can safely unregister its tab.
            foreach (var apply in subscribers.Values.ToArray())
            {
                apply(Current);
            }
        }
    }
}
