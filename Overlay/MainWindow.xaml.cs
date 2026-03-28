using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;

namespace Overlay;

public partial class MainWindow : Window
{
    private IntPtr _hwnd = IntPtr.Zero;
    private bool _clickThroughEnabled;
    private DispatcherTimer _timer;
    private TimeSpan _timeRemaining;
    private bool _isRunning;

    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_LAYERED = 0x00080000;
    private const int WS_EX_TRANSPARENT = 0x00000020;

    public MainWindow()
    {
        InitializeComponent();

        _timeRemaining = TimeSpan.FromMinutes(10);
        _isRunning = false;
        _timer = new DispatcherTimer();
        _timer.Interval = TimeSpan.FromSeconds(1);
        _timer.Tick += Timer_Tick;

        UpdateTimerDisplay();
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        _hwnd = new WindowInteropHelper(this).Handle;
        ApplyExtendedStyles();

        if (Properties.Settings.Default.WindowLeft >= 0 && Properties.Settings.Default.WindowTop >= 0)
        {
            Left = Properties.Settings.Default.WindowLeft;
            Top = Properties.Settings.Default.WindowTop;
        }
        else
        {
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
        }

        if (!string.IsNullOrEmpty(Properties.Settings.Default.SelectedTheme))
        {
            LoadTheme(Properties.Settings.Default.SelectedTheme);
        }

        Closing += Window_Closing;
    }

    private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
    {
        Properties.Settings.Default.WindowLeft = Left;
        Properties.Settings.Default.WindowTop = Top;
        Properties.Settings.Default.Save();
    }

    private void ToggleClickThrough_Click(object sender, RoutedEventArgs e)
    {
        _clickThroughEnabled = !_clickThroughEnabled;
        ApplyExtendedStyles();
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void StartPause_Click(object sender, RoutedEventArgs e)
    {
        if (_isRunning)
        {
            _timer.Stop();
            _isRunning = false;
            UpdateStartPauseButton();
        }
        else
        {
            _timer.Start();
            _isRunning = true;
            UpdateStartPauseButton();
        }
    }

    private void TimeRemainingText_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (!_isRunning)
        {
            e.Handled = true;
            TimeContextMenu.PlacementTarget = TimeRemainingText;
            TimeContextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
            TimeContextMenu.IsOpen = true;
        }
    }

    private void TimeContextMenu_Opened(object sender, RoutedEventArgs e)
    {
        if (TimeContextMenu != null)
        {
            TimeContextMenu.Focus();
        }
    }

    private void Theme_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.ContextMenu != null)
        {
            button.ContextMenu.PlacementTarget = button;
            button.ContextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
            button.ContextMenu.IsOpen = true;
        }
    }

    private void SetTheme_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem menuItem && menuItem.Tag is string theme)
        {
            LoadTheme(theme);
            Properties.Settings.Default.SelectedTheme = theme;
            Properties.Settings.Default.Save();
        }
    }

    private void LoadTheme(string themeName)
    {
        var themeUri = new Uri($"Themes/{themeName}Theme.xaml", UriKind.Relative);

        try
        {
            var newTheme = new ResourceDictionary { Source = themeUri };

            if (Application.Current.Resources.MergedDictionaries.Count > 0)
            {
                Application.Current.Resources.MergedDictionaries[0] = newTheme;
            }
            else
            {
                Application.Current.Resources.MergedDictionaries.Add(newTheme);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Fehler beim Laden des Themes: {ex.Message}", "Theme-Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void SetTime_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem menuItem && menuItem.Tag is string tagString)
        {
            if (int.TryParse(tagString, out int minutes))
            {
                _timeRemaining = TimeSpan.FromMinutes(minutes);
                UpdateTimerDisplay();
            }
        }
    }

    private void UpdateStartPauseButton()
    {
        if (_isRunning)
        {
            var textBlock = FindButtonTextBlock(StartPauseButton);
            if (textBlock != null)
            {
                textBlock.Text = "⏸ Pause";
            }
        }
        else
        {
            var textBlock = FindButtonTextBlock(StartPauseButton);
            if (textBlock != null)
            {
                textBlock.Text = "▶ Start";
            }

        }
    }

    private TextBlock FindButtonTextBlock(Button button)
    {
        if (button.Template != null)
        {
            return button.Template.FindName("ButtonText", button) as TextBlock;
        }
        return null;
    }

    private void RootBorder_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed && !_clickThroughEnabled)
        {
            DragMove();
        }
    }

    private void ApplyExtendedStyles()
    {
        if (_hwnd == IntPtr.Zero)
        {
            return;
        }

        var exStyle = GetWindowLongPtr(_hwnd, GWL_EXSTYLE).ToInt64();

        // Layered ist fuer transparente Darstellung notwendig.
        exStyle |= WS_EX_LAYERED;

        if (_clickThroughEnabled)
        {
            exStyle |= WS_EX_TRANSPARENT;
        }
        else
        {
            exStyle &= ~WS_EX_TRANSPARENT;
        }

        SetWindowLongPtr(_hwnd, GWL_EXSTYLE, new IntPtr(exStyle));
    }

    private void Timer_Tick(object sender, EventArgs e)
    {
        if (_timeRemaining.TotalSeconds > 0)
        {
            _timeRemaining = _timeRemaining.Subtract(TimeSpan.FromSeconds(1));
            UpdateTimerDisplay();
        }
        else
        {
            _timer.Stop();
        }
    }

    private void UpdateTimerDisplay()
    {
        double totalSeconds = _timeRemaining.TotalSeconds;
        double totalMinutes = totalSeconds / 60.0;

        double minuteAngle = (10 - totalMinutes) * 36.0;
        double secondAngle = (60 - _timeRemaining.Seconds) * 6.0;

        MinuteRotation.Angle = minuteAngle;
        SecondRotation.Angle = secondAngle;

        TimeRemainingText.Text = $"{(int)_timeRemaining.TotalMinutes:D2}:{_timeRemaining.Seconds:D2}";
    }

    private static IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex)
    {
        if (IntPtr.Size == 8)
        {
            return GetWindowLongPtr64(hWnd, nIndex);
        }

        return new IntPtr(GetWindowLong32(hWnd, nIndex));
    }

    private static IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong)
    {
        if (IntPtr.Size == 8)
        {
            return SetWindowLongPtr64(hWnd, nIndex, dwNewLong);
        }

        return new IntPtr(SetWindowLong32(hWnd, nIndex, dwNewLong.ToInt32()));
    }

    [DllImport("user32.dll", EntryPoint = "GetWindowLong")]
    private static extern int GetWindowLong32(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtr")]
    private static extern IntPtr GetWindowLongPtr64(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "SetWindowLong")]
    private static extern int SetWindowLong32(IntPtr hWnd, int nIndex, int dwNewLong);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr")]
    private static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);
}
