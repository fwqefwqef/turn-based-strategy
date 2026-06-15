using System.ComponentModel;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace StatGainLab;

internal sealed class GrowthBarControl : Control
{
    private const int Step = 5;
    private const int MaxValue = 100;
    private const int LeftLabelWidth = 92;
    private const int RightValueWidth = 40;
    private const int HorizontalGap = 10;
    private const int BarHeight = 24;
    private readonly StringFormat _leftTextFormat = new() { LineAlignment = StringAlignment.Center };
    private readonly StringFormat _rightTextFormat = new() { Alignment = StringAlignment.Far, LineAlignment = StringAlignment.Center };
    private bool _dragging;
    private int _value;
    private string _statName = string.Empty;
    private Color _fillColor = Color.FromArgb(80, 140, 230);

    public event Action<int>? DesiredValueChanged;

    public GrowthBarControl()
    {
        SetStyle(ControlStyles.SupportsTransparentBackColor, true);
        DoubleBuffered = true;
        Height = 46;
        Margin = new Padding(0, 0, 0, 8);
        Cursor = Cursors.SizeWE;
        BackColor = Color.Transparent;
    }

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string StatName
    {
        get => _statName;
        set
        {
            _statName = value ?? string.Empty;
            Invalidate();
        }
    }

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int Value
    {
        get => _value;
        set
        {
            int clamped = Math.Clamp(value, 0, MaxValue);
            if (_value == clamped)
            {
                return;
            }

            _value = clamped;
            Invalidate();
        }
    }

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color FillColor
    {
        get => _fillColor;
        set
        {
            _fillColor = value;
            Invalidate();
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);

        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        e.Graphics.Clear(Parent?.BackColor ?? BackColor);

        Rectangle labelBounds = new(0, 0, LeftLabelWidth, Height);
        Rectangle valueBounds = new(Width - RightValueWidth, 0, RightValueWidth, Height);
        Rectangle barBounds = GetBarBounds();

        using var labelBrush = new SolidBrush(ForeColor);
        using var valueBrush = new SolidBrush(ForeColor);
        using var backgroundBrush = new SolidBrush(Color.FromArgb(42, 45, 54));
        using var fillBrush = new SolidBrush(_fillColor);
        using var borderPen = new Pen(Color.FromArgb(90, 95, 110));

        e.Graphics.DrawString(StatName, Font, labelBrush, labelBounds, _leftTextFormat);
        e.Graphics.DrawString($"{Value}", Font, valueBrush, valueBounds, _rightTextFormat);

        DrawRoundedRectangle(e.Graphics, backgroundBrush, borderPen, barBounds, 6);

        int fillWidth = (int)Math.Round(barBounds.Width * (Value / 100f));
        if (fillWidth > 0)
        {
            Rectangle fillBounds = new(barBounds.X, barBounds.Y, fillWidth, barBounds.Height);
            DrawRoundedFill(e.Graphics, fillBrush, fillBounds, 6);
        }
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button != MouseButtons.Left)
        {
            return;
        }

        _dragging = true;
        Capture = true;
        RequestValueFromPosition(e.X);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (!_dragging)
        {
            return;
        }

        RequestValueFromPosition(e.X);
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (e.Button != MouseButtons.Left)
        {
            return;
        }

        _dragging = false;
        Capture = false;
    }

    private void RequestValueFromPosition(int mouseX)
    {
        Rectangle barBounds = GetBarBounds();
        float ratio = (mouseX - barBounds.Left) / (float)Math.Max(1, barBounds.Width);
        int desiredValue = (int)Math.Round(Math.Clamp(ratio, 0f, 1f) * MaxValue / Step) * Step;
        desiredValue = Math.Clamp(desiredValue, 0, MaxValue);
        DesiredValueChanged?.Invoke(desiredValue);
    }

    private Rectangle GetBarBounds()
    {
        int x = LeftLabelWidth + HorizontalGap;
        int width = Math.Max(10, Width - LeftLabelWidth - RightValueWidth - (HorizontalGap * 2));
        int y = (Height - BarHeight) / 2;
        return new Rectangle(x, y, width, BarHeight);
    }

    private static void DrawRoundedRectangle(Graphics graphics, Brush fillBrush, Pen borderPen, Rectangle bounds, int radius)
    {
        using GraphicsPath path = BuildRoundedPath(bounds, radius);
        graphics.FillPath(fillBrush, path);
        graphics.DrawPath(borderPen, path);
    }

    private static void DrawRoundedFill(Graphics graphics, Brush fillBrush, Rectangle bounds, int radius)
    {
        using GraphicsPath path = BuildRoundedPath(bounds, Math.Min(radius, bounds.Width / 2));
        graphics.FillPath(fillBrush, path);
    }

    private static GraphicsPath BuildRoundedPath(Rectangle bounds, int radius)
    {
        int diameter = radius * 2;
        GraphicsPath path = new();

        if (diameter <= 0 || bounds.Width <= diameter || bounds.Height <= diameter)
        {
            path.AddRectangle(bounds);
            path.CloseFigure();
            return path;
        }

        Rectangle arc = new(bounds.Location, new Size(diameter, diameter));
        path.AddArc(arc, 180, 90);
        arc.X = bounds.Right - diameter;
        path.AddArc(arc, 270, 90);
        arc.Y = bounds.Bottom - diameter;
        path.AddArc(arc, 0, 90);
        arc.X = bounds.Left;
        path.AddArc(arc, 90, 90);
        path.CloseFigure();
        return path;
    }
}
