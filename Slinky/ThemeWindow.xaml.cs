using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using Slinky.Services;

namespace Slinky
{
    public sealed partial class ThemeWindow : Window
    {
        public ThemeService Theme => ThemeService.Instance;

        public ThemeWindow()
        {
            this.InitializeComponent();
            WindowIconService.Apply(this);

            SelectRadioForCurrentTheme();

            ThemeService.Instance.ThemeChanged += OnThemeChanged;
            ApplyElementTheme();

            // Unsubscribe when this window closes, so short-lived ThemeWindow
            // instances don't pile up as listeners on the long-lived singleton.
            this.Closed += (s, e) => ThemeService.Instance.ThemeChanged -= OnThemeChanged;
        }

        private void SelectRadioForCurrentTheme()
        {
            RadioButton radio = ThemeService.Instance.Current switch
            {
                AppTheme.LightDefault => LightDefaultRadio,
                AppTheme.LightYellowish => LightYellowishRadio,
                AppTheme.DarkDefault => DarkDefaultRadio,
                AppTheme.DarkBluish => DarkBluishRadio,
                AppTheme.DarkDracula => DarkDraculaRadio,
                _ => LightDefaultRadio
            };
            radio.IsChecked = true;
        }

        private void ThemeRadio_Checked(object sender, RoutedEventArgs e)
        {
            if (sender is RadioButton { Tag: string tagName } && Enum.TryParse<AppTheme>(tagName, out var theme))
            {
                ThemeService.Instance.Apply(theme);
            }
        }

        private void OnThemeChanged(object? sender, EventArgs e) => ApplyElementTheme();

        private void ApplyElementTheme()
        {
            if (this.Content is FrameworkElement root)
            {
                root.RequestedTheme = ThemeService.Instance.ElementTheme;
            }
        }
    }
}
