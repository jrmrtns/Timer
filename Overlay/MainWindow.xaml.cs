using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace Overlay;

public partial class MainWindow : Window
{
    private IntPtr _hwnd = IntPtr.Zero;
    private bool _clickThroughEnabled;
    private DispatcherTimer _timer;
    private TimeSpan _timeRemaining;
    private TimeSpan _totalTime;
    private bool _isRunning;

    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_LAYERED = 0x00080000;
    private const int WS_EX_TRANSPARENT = 0x00000020;

    public MainWindow()
    {
        InitializeComponent();

        _timeRemaining = TimeSpan.FromMinutes(10);
        _totalTime = TimeSpan.FromMinutes(10);
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

    private void CenterTimeText_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (!_isRunning)
        {
            e.Handled = true;
            TimeInputPopup.IsOpen = true;
        }
    }

    private void TimeInputTextBox_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox textBox)
        {
            textBox.Focus();
            textBox.SelectAll();
        }
    }

    private void TimeInputTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            if (sender is TextBox textBox && int.TryParse(textBox.Text, out int minutes))
            {
                if (minutes > 0 && minutes <= 999)
                {
                    _timeRemaining = TimeSpan.FromMinutes(minutes);
                    _totalTime = TimeSpan.FromMinutes(minutes);
                    UpdateTimerDisplay();
                    TimeInputPopup.IsOpen = false;
                    textBox.Text = string.Empty;
                }
                else
                {
                    MessageBox.Show("Bitte geben Sie eine Zahl zwischen 1 und 999 ein.", "Ungültige Eingabe", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            else
            {
                MessageBox.Show("Bitte geben Sie eine gültige Zahl ein.", "Ungültige Eingabe", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            TimeInputPopup.IsOpen = false;
            if (sender is TextBox textBox)
            {
                textBox.Text = string.Empty;
            }
            e.Handled = true;
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
                _totalTime = TimeSpan.FromMinutes(minutes);
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
            _isRunning = false;
            var textBlock = FindButtonTextBlock(StartPauseButton);
            if (textBlock != null)
            {
                textBlock.Text = "Beendet";
            }
        }
    }

    private void UpdateTimerDisplay()
    {
        double percentage = _totalTime.TotalSeconds > 0 
            ? (_timeRemaining.TotalSeconds / _totalTime.TotalSeconds) * 100 
            : 0;

        UpdatePieChart(percentage);

        CenterTimeText.Text = $"{(int)_timeRemaining.TotalMinutes:D2}:{_timeRemaining.Seconds:D2}";
    }

    private void UpdatePieChart(double percentage)
    {
        double radius = 70;
        double centerX = 70;
        double centerY = 70;

        if (percentage <= 0)
        {
            ProgressPie.Data = Geometry.Empty;
            return;
        }

        if (percentage >= 100)
        {
            ProgressPie.Data = new EllipseGeometry(new Point(centerX, centerY), radius, radius);
            return;
        }

        double missingAngle = ((100 - percentage) / 100.0) * 360.0;
        double startRadians = (-90 + missingAngle) * Math.PI / 180.0;

        double startX = centerX + radius * Math.Cos(startRadians);
        double startY = centerY + radius * Math.Sin(startRadians);

        bool isLargeArc = percentage > 50;

        var figure = new PathFigure
        {
            StartPoint = new Point(centerX, centerY),
            IsClosed = true
        };

        figure.Segments.Add(new LineSegment(new Point(startX, startY), true));
        figure.Segments.Add(new ArcSegment(
            new Point(centerX, centerY - radius),
            new Size(radius, radius),
            0,
            isLargeArc,
            SweepDirection.Clockwise,
            true));

        var geometry = new PathGeometry();
        geometry.Figures.Add(figure);

        ProgressPie.Data = geometry;
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
