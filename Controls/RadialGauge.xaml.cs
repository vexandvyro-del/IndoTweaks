using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace IndoTweaks.Controls;

/// <summary>
/// A circular arc gauge (270° sweep) used for CPU/GPU load, temperature, and RAM
/// usage on the Dashboard. Renders as two overlaid Path arcs (track + value) plus
/// a centered value/label stack. Pure XAML+code shapes, no external charting
/// dependency needed for something this simple.
/// </summary>
public partial class RadialGauge : UserControl
{
    public RadialGauge()
    {
        InitializeComponent();
        Loaded += (_, _) => Redraw();
    }

    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(
        nameof(Value), typeof(double), typeof(RadialGauge),
        new PropertyMetadata(0.0, OnAnyPropertyChanged));

    public static readonly DependencyProperty MaximumProperty = DependencyProperty.Register(
        nameof(Maximum), typeof(double), typeof(RadialGauge),
        new PropertyMetadata(100.0, OnAnyPropertyChanged));

    public static readonly DependencyProperty LabelProperty = DependencyProperty.Register(
        nameof(Label), typeof(string), typeof(RadialGauge),
        new PropertyMetadata("", OnAnyPropertyChanged));

    public static readonly DependencyProperty UnitProperty = DependencyProperty.Register(
        nameof(Unit), typeof(string), typeof(RadialGauge),
        new PropertyMetadata("", OnAnyPropertyChanged));

    /// <summary>Value fraction (0-1) above which the arc turns amber/warn colored.</summary>
    public static readonly DependencyProperty WarnThresholdProperty = DependencyProperty.Register(
        nameof(WarnThreshold), typeof(double), typeof(RadialGauge),
        new PropertyMetadata(0.7, OnAnyPropertyChanged));

    /// <summary>Value fraction (0-1) above which the arc turns red/critical colored.</summary>
    public static readonly DependencyProperty CriticalThresholdProperty = DependencyProperty.Register(
        nameof(CriticalThreshold), typeof(double), typeof(RadialGauge),
        new PropertyMetadata(0.9, OnAnyPropertyChanged));

    public double Value { get => (double)GetValue(ValueProperty); set => SetValue(ValueProperty, value); }
    public double Maximum { get => (double)GetValue(MaximumProperty); set => SetValue(MaximumProperty, value); }
    public string Label { get => (string)GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
    public string Unit { get => (string)GetValue(UnitProperty); set => SetValue(UnitProperty, value); }
    public double WarnThreshold { get => (double)GetValue(WarnThresholdProperty); set => SetValue(WarnThresholdProperty, value); }
    public double CriticalThreshold { get => (double)GetValue(CriticalThresholdProperty); set => SetValue(CriticalThresholdProperty, value); }

    private static void OnAnyPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
        (d as RadialGauge)?.Redraw();

    private const double StartAngleDeg = 135; // sweep starts at bottom-left...
    private const double SweepDeg = 270;      // ...and covers 270° clockwise, leaving a bottom gap (classic gauge look)

    private void Redraw()
    {
        if (ValueArcPath is null || TrackArcPath is null) return;

        double fraction = Maximum <= 0 ? 0 : Math.Clamp(Value / Maximum, 0, 1);

        TrackArcPath.Data = BuildArcGeometry(StartAngleDeg, SweepDeg);
        ValueArcPath.Data = BuildArcGeometry(StartAngleDeg, SweepDeg * fraction);

        ValueArcPath.Stroke = fraction >= CriticalThreshold
            ? (Brush)FindResource("StatusBadBrush")
            : fraction >= WarnThreshold
                ? (Brush)FindResource("StatusWarnBrush")
                : (Brush)FindResource("AccentBrush");

        ValueText.Text = Unit == "%" ? $"{Value:0}" : $"{Value:0}";
        UnitText.Text = Unit;
        LabelText.Text = Label;
    }

    private Geometry BuildArcGeometry(double startAngleDeg, double sweepDeg)
    {
        const double radius = 42;
        var center = new Point(50, 50);

        if (sweepDeg <= 0.01)
            return Geometry.Empty;

        Point PointOnCircle(double angleDeg)
        {
            double rad = angleDeg * Math.PI / 180.0;
            return new Point(center.X + radius * Math.Cos(rad), center.Y + radius * Math.Sin(rad));
        }

        var startPoint = PointOnCircle(startAngleDeg);
        var endPoint = PointOnCircle(startAngleDeg + sweepDeg);
        bool isLargeArc = sweepDeg > 180;

        var figure = new PathFigure { StartPoint = startPoint, IsClosed = false };
        figure.Segments.Add(new ArcSegment(
            endPoint, new Size(radius, radius), 0, isLargeArc, SweepDirection.Clockwise, isStroked: true));

        var geometry = new PathGeometry();
        geometry.Figures.Add(figure);
        return geometry;
    }
}
