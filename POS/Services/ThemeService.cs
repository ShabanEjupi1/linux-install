using System;
using System.IO;
using System.Windows;
using System.Windows.Media;

namespace KosovaPOS.Services
{
    /// <summary>
    /// Service to manage application theme (Light/Dark mode)
    /// </summary>
    public class ThemeService
    {
        private static ThemeService? _instance;
        public static ThemeService Instance => _instance ??= new ThemeService();
        
        public event EventHandler<bool>? ThemeChanged;
        
        private bool _isDarkMode;
        private readonly string _settingsFilePath;
        
        public bool IsDarkMode
        {
            get => _isDarkMode;
            set
            {
                if (_isDarkMode != value)
                {
                    _isDarkMode = value;
                    ApplyTheme();
                    ThemeChanged?.Invoke(this, value);
                    SaveThemePreference();
                }
            }
        }
        
        private ThemeService()
        {
            _settingsFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "theme_settings.txt");
            LoadThemePreference();
        }
        
        private void LoadThemePreference()
        {
            try
            {
                // Try to load from settings file first
                if (File.Exists(_settingsFilePath))
                {
                    var content = File.ReadAllText(_settingsFilePath).Trim();
                    _isDarkMode = content.ToLower() == "dark" || content.ToLower() == "true";
                    return;
                }
                
                // Fallback to environment variable for backwards compatibility
                var savedTheme = Environment.GetEnvironmentVariable("POS_DARK_MODE");
                _isDarkMode = savedTheme?.ToLower() == "true";
            }
            catch
            {
                // Default to light mode if we can't read settings
                _isDarkMode = false;
            }
        }
        
        private void SaveThemePreference()
        {
            try
            {
                File.WriteAllText(_settingsFilePath, _isDarkMode ? "dark" : "light");
            }
            catch
            {
                // Silently fail if we can't write settings
            }
        }
        
        public void ToggleTheme()
        {
            IsDarkMode = !IsDarkMode;
        }
        
        public void ApplyTheme()
        {
            var app = Application.Current;
            if (app == null) return;
            
            if (_isDarkMode)
            {
                ApplyDarkTheme(app.Resources);
            }
            else
            {
                ApplyLightTheme(app.Resources);
            }
        }
        
        private void ApplyLightTheme(ResourceDictionary resources)
        {
            // Light theme colors - Enterprise Pro colors
            resources["BackgroundBrush"] = new SolidColorBrush(Color.FromRgb(248, 250, 252)); // #F8FAFC
            resources["SurfaceBrush"] = new SolidColorBrush(Color.FromRgb(255, 255, 255)); // #FFFFFF
            resources["SurfaceElevatedBrush"] = new SolidColorBrush(Color.FromRgb(255, 255, 255));
            resources["BorderBrush"] = new SolidColorBrush(Color.FromRgb(226, 232, 240)); // #E2E8F0
            resources["BorderLightBrush"] = new SolidColorBrush(Color.FromRgb(241, 245, 249)); // #F1F5F9
            resources["TextPrimaryBrush"] = new SolidColorBrush(Color.FromRgb(15, 23, 42)); // #0F172A
            resources["TextSecondaryBrush"] = new SolidColorBrush(Color.FromRgb(71, 85, 105)); // #475569
            resources["TextMutedBrush"] = new SolidColorBrush(Color.FromRgb(148, 163, 184)); // #94A3B8
            
            // Primary colors for light theme
            resources["PrimaryBrush"] = new SolidColorBrush(Color.FromRgb(79, 70, 229)); // #4F46E5
            resources["PrimaryHoverBrush"] = new SolidColorBrush(Color.FromRgb(67, 56, 202)); // #4338CA
            resources["PrimaryLightBrush"] = new SolidColorBrush(Color.FromRgb(238, 242, 255)); // #EEF2FF
            
            // Accent colors for light theme
            resources["AccentBrush"] = new SolidColorBrush(Color.FromRgb(14, 165, 233)); // #0EA5E9
            resources["SuccessBrush"] = new SolidColorBrush(Color.FromRgb(5, 150, 105)); // #059669
            resources["SuccessLightBrush"] = new SolidColorBrush(Color.FromRgb(209, 250, 229)); // #D1FAE5
            resources["WarningBrush"] = new SolidColorBrush(Color.FromRgb(217, 119, 6)); // #D97706
            resources["WarningLightBrush"] = new SolidColorBrush(Color.FromRgb(254, 243, 199)); // #FEF3C7
            resources["DangerBrush"] = new SolidColorBrush(Color.FromRgb(220, 38, 38)); // #DC2626
            resources["DangerLightBrush"] = new SolidColorBrush(Color.FromRgb(254, 226, 226)); // #FEE2E2
            resources["InfoBrush"] = new SolidColorBrush(Color.FromRgb(8, 145, 178)); // #0891B2
            
            // Glass effect for light theme
            resources["GlassBrush"] = new SolidColorBrush(Color.FromArgb(230, 255, 255, 255)); // #E6FFFFFF
            
            // Update gradient brushes for light theme
            resources["PrimaryGradient"] = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0),
                EndPoint = new Point(1, 1),
                GradientStops = new GradientStopCollection
                {
                    new GradientStop(Color.FromRgb(79, 70, 229), 0),
                    new GradientStop(Color.FromRgb(124, 58, 237), 1)
                }
            };
            
            resources["SuccessGradient"] = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0),
                EndPoint = new Point(1, 1),
                GradientStops = new GradientStopCollection
                {
                    new GradientStop(Color.FromRgb(5, 150, 105), 0),
                    new GradientStop(Color.FromRgb(16, 185, 129), 1)
                }
            };
            
            resources["DangerGradient"] = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0),
                EndPoint = new Point(1, 1),
                GradientStops = new GradientStopCollection
                {
                    new GradientStop(Color.FromRgb(220, 38, 38), 0),
                    new GradientStop(Color.FromRgb(239, 68, 68), 1)
                }
            };
            
            resources["WarningGradient"] = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0),
                EndPoint = new Point(1, 1),
                GradientStops = new GradientStopCollection
                {
                    new GradientStop(Color.FromRgb(217, 119, 6), 0),
                    new GradientStop(Color.FromRgb(245, 158, 11), 1)
                }
            };
            
            resources["HeaderGradient"] = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0),
                EndPoint = new Point(1, 0),
                GradientStops = new GradientStopCollection
                {
                    new GradientStop(Color.FromRgb(79, 70, 229), 0),
                    new GradientStop(Color.FromRgb(99, 102, 241), 0.5),
                    new GradientStop(Color.FromRgb(124, 58, 237), 1)
                }
            };
            
            resources["TotalDisplayGradient"] = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0),
                EndPoint = new Point(1, 1),
                GradientStops = new GradientStopCollection
                {
                    new GradientStop(Color.FromRgb(5, 150, 105), 0),
                    new GradientStop(Color.FromRgb(4, 120, 87), 1)
                }
            };
            
            // Update App.xaml colors
            resources["BackgroundColor"] = new SolidColorBrush(Color.FromRgb(245, 245, 245));
            resources["SurfaceColor"] = new SolidColorBrush(Color.FromRgb(255, 255, 255));
            resources["TextPrimary"] = new SolidColorBrush(Color.FromRgb(33, 33, 33));
            resources["TextSecondary"] = new SolidColorBrush(Color.FromRgb(117, 117, 117));
            resources["BorderColor"] = new SolidColorBrush(Color.FromRgb(224, 224, 224));
        }
        
        private void ApplyDarkTheme(ResourceDictionary resources)
        {
            // Dark theme colors - Enterprise Pro colors
            resources["BackgroundBrush"] = new SolidColorBrush(Color.FromRgb(15, 23, 42)); // #0F172A
            resources["SurfaceBrush"] = new SolidColorBrush(Color.FromRgb(30, 41, 59)); // #1E293B
            resources["SurfaceElevatedBrush"] = new SolidColorBrush(Color.FromRgb(51, 65, 85)); // #334155
            resources["BorderBrush"] = new SolidColorBrush(Color.FromRgb(51, 65, 85)); // #334155
            resources["BorderLightBrush"] = new SolidColorBrush(Color.FromRgb(71, 85, 105)); // #475569
            resources["TextPrimaryBrush"] = new SolidColorBrush(Color.FromRgb(248, 250, 252)); // #F8FAFC
            resources["TextSecondaryBrush"] = new SolidColorBrush(Color.FromRgb(203, 213, 225)); // #CBD5E1
            resources["TextMutedBrush"] = new SolidColorBrush(Color.FromRgb(100, 116, 139)); // #64748B
            
            // Primary colors for dark theme
            resources["PrimaryBrush"] = new SolidColorBrush(Color.FromRgb(129, 140, 248)); // #818CF8
            resources["PrimaryHoverBrush"] = new SolidColorBrush(Color.FromRgb(165, 180, 252)); // #A5B4FC
            resources["PrimaryLightBrush"] = new SolidColorBrush(Color.FromRgb(30, 27, 75)); // #1E1B4B
            
            // Accent colors for dark theme
            resources["AccentBrush"] = new SolidColorBrush(Color.FromRgb(56, 189, 248)); // #38BDF8
            resources["SuccessBrush"] = new SolidColorBrush(Color.FromRgb(52, 211, 153)); // #34D399
            resources["SuccessLightBrush"] = new SolidColorBrush(Color.FromRgb(6, 78, 59)); // #064E3B
            resources["WarningBrush"] = new SolidColorBrush(Color.FromRgb(251, 191, 36)); // #FBBF24
            resources["WarningLightBrush"] = new SolidColorBrush(Color.FromRgb(120, 53, 15)); // #78350F
            resources["DangerBrush"] = new SolidColorBrush(Color.FromRgb(248, 113, 113)); // #F87171
            resources["DangerLightBrush"] = new SolidColorBrush(Color.FromRgb(127, 29, 29)); // #7F1D1D
            resources["InfoBrush"] = new SolidColorBrush(Color.FromRgb(34, 211, 238)); // #22D3EE
            
            // Glass effect for dark theme
            resources["GlassBrush"] = new SolidColorBrush(Color.FromArgb(204, 30, 41, 59)); // #CC1E293B
            
            // Update gradient brushes for dark theme
            resources["PrimaryGradient"] = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0),
                EndPoint = new Point(1, 1),
                GradientStops = new GradientStopCollection
                {
                    new GradientStop(Color.FromRgb(129, 140, 248), 0),
                    new GradientStop(Color.FromRgb(167, 139, 250), 1)
                }
            };
            
            resources["SuccessGradient"] = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0),
                EndPoint = new Point(1, 1),
                GradientStops = new GradientStopCollection
                {
                    new GradientStop(Color.FromRgb(52, 211, 153), 0),
                    new GradientStop(Color.FromRgb(110, 231, 183), 1)
                }
            };
            
            resources["DangerGradient"] = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0),
                EndPoint = new Point(1, 1),
                GradientStops = new GradientStopCollection
                {
                    new GradientStop(Color.FromRgb(248, 113, 113), 0),
                    new GradientStop(Color.FromRgb(252, 165, 165), 1)
                }
            };
            
            resources["WarningGradient"] = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0),
                EndPoint = new Point(1, 1),
                GradientStops = new GradientStopCollection
                {
                    new GradientStop(Color.FromRgb(251, 191, 36), 0),
                    new GradientStop(Color.FromRgb(252, 211, 77), 1)
                }
            };
            
            resources["HeaderGradient"] = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0),
                EndPoint = new Point(1, 0),
                GradientStops = new GradientStopCollection
                {
                    new GradientStop(Color.FromRgb(49, 46, 129), 0),
                    new GradientStop(Color.FromRgb(55, 48, 163), 0.5),
                    new GradientStop(Color.FromRgb(67, 56, 202), 1)
                }
            };
            
            resources["TotalDisplayGradient"] = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0),
                EndPoint = new Point(1, 1),
                GradientStops = new GradientStopCollection
                {
                    new GradientStop(Color.FromRgb(6, 95, 70), 0),
                    new GradientStop(Color.FromRgb(4, 120, 87), 1)
                }
            };
            
            // Update App.xaml colors
            resources["BackgroundColor"] = new SolidColorBrush(Color.FromRgb(15, 23, 42));
            resources["SurfaceColor"] = new SolidColorBrush(Color.FromRgb(30, 41, 59));
            resources["TextPrimary"] = new SolidColorBrush(Color.FromRgb(248, 250, 252));
            resources["TextSecondary"] = new SolidColorBrush(Color.FromRgb(203, 213, 225));
            resources["BorderColor"] = new SolidColorBrush(Color.FromRgb(51, 65, 85));
        }
    }
}
