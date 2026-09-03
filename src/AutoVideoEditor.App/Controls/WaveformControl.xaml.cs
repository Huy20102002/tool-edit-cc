using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using AutoVideoEditor.Core.Models;

namespace AutoVideoEditor.App.Controls;

public partial class WaveformControl : UserControl
{
    public static readonly DependencyProperty AnalysisResultProperty =
        DependencyProperty.Register(
            nameof(AnalysisResult),
            typeof(AudioAnalysisResult),
            typeof(WaveformControl),
            new PropertyMetadata(null, OnAnalysisResultChanged));

    public AudioAnalysisResult? AnalysisResult
    {
        get => (AudioAnalysisResult?)GetValue(AnalysisResultProperty);
        set => SetValue(AnalysisResultProperty, value);
    }

    private static readonly Brush SpeechBrush = new SolidColorBrush(Color.FromRgb(16, 185, 129)); // Emerald 500
    private static readonly Brush SilenceBrush = new SolidColorBrush(Color.FromArgb(180, 239, 68, 68)); // Translucent Red 500
    private static readonly Brush CenterLineBrush = new SolidColorBrush(Color.FromRgb(39, 39, 42));

    public WaveformControl()
    {
        InitializeComponent();
    }

    private static void OnAnalysisResultChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is WaveformControl control)
        {
            control.RedrawWaveform();
        }
    }

    private void WaveformCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        RedrawWaveform();
    }

    public void RedrawWaveform()
    {
        WaveformCanvas.Children.Clear();

        var result = AnalysisResult;
        if (result == null || result.WaveformPoints == null || result.WaveformPoints.Length == 0 || result.OriginalDurationSeconds <= 0)
        {
            PlaceholderText.Visibility = Visibility.Visible;
            return;
        }

        PlaceholderText.Visibility = Visibility.Collapsed;

        var width = WaveformCanvas.ActualWidth;
        var height = WaveformCanvas.ActualHeight;

        if (width <= 10 || height <= 10) return;

        var centerY = height / 2.0;

        // Draw center baseline
        var centerLine = new Line
        {
            X1 = 0,
            Y1 = centerY,
            X2 = width,
            Y2 = centerY,
            Stroke = CenterLineBrush,
            StrokeThickness = 1
        };
        WaveformCanvas.Children.Add(centerLine);

        var points = result.WaveformPoints;
        var pointCount = points.Length;
        var totalDur = result.OriginalDurationSeconds;
        var barWidth = Math.Max(1.0, (width / pointCount) - 1.0);

        for (int i = 0; i < pointCount; i++)
        {
            var x = (i / (double)pointCount) * width;
            var currentTime = (i / (double)pointCount) * totalDur;

            // Check if this point falls inside any speech segment
            var isSpeech = result.SpeechSegments.Any(s => currentTime >= s.StartSeconds && currentTime <= s.EndSeconds);
            var brush = isSpeech ? SpeechBrush : SilenceBrush;

            var amp = points[i];
            var barHeight = Math.Max(3.0, amp * (height * 0.9));
            var y = centerY - (barHeight / 2.0);

            var rect = new Rectangle
            {
                Width = barWidth,
                Height = barHeight,
                Fill = brush,
                RadiusX = 1,
                RadiusY = 1
            };

            Canvas.SetLeft(rect, x);
            Canvas.SetTop(rect, y);
            WaveformCanvas.Children.Add(rect);
        }
    }
}
