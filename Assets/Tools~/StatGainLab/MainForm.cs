using System.Drawing;
using System.Windows.Forms;
using StatGainTools;

namespace StatGainLab;

internal sealed class MainForm : Form
{
    private static readonly Color[] BarColors =
    [
        Color.FromArgb(206, 80, 74),
        Color.FromArgb(108, 96, 219),
        Color.FromArgb(66, 146, 215),
        Color.FromArgb(52, 161, 118),
        Color.FromArgb(231, 171, 47),
        Color.FromArgb(182, 102, 201)
    ];

    private readonly string[] _statNames = StatGainCalculator.DefaultStatNames.ToArray();
    private readonly int[] _growthRates = [25, 20, 15, 15, 15, 10];
    private readonly GrowthBarControl[] _barControls = new GrowthBarControl[StatGainCalculator.ExpectedStatCount];

    private readonly Label _growthSummaryLabel;
    private readonly Label _totalLabel;
    private readonly Button _baseFormulaButton;
    private readonly Button _biasedFormulaButton;
    private readonly Button _bothFormulaButton;
    private readonly TextBox _outputTextBox;

    private RequestedFormula _requestedFormula = RequestedFormula.Base;

    public MainForm()
    {
        Text = "Stat Gain Lab";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(960, 720);
        BackColor = Color.FromArgb(27, 29, 35);
        ForeColor = Color.WhiteSmoke;

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Padding = new Padding(18),
            BackColor = BackColor
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 360f));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        Controls.Add(root);

        var leftPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 5,
            BackColor = Color.FromArgb(35, 38, 46),
            Padding = new Padding(14)
        };
        leftPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        leftPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        leftPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        leftPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        leftPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        root.Controls.Add(leftPanel, 0, 0);

        var titleLabel = new Label
        {
            AutoSize = true,
            Text = "Growth Tuning",
            Font = new Font(Font.FontFamily, 16f, FontStyle.Bold),
            Margin = new Padding(0, 0, 0, 10)
        };
        leftPanel.Controls.Add(titleLabel);

        _growthSummaryLabel = new Label
        {
            AutoSize = true,
            MaximumSize = new Size(320, 0),
            Margin = new Padding(0, 0, 0, 8)
        };
        leftPanel.Controls.Add(_growthSummaryLabel);

        _totalLabel = new Label
        {
            AutoSize = true,
            Font = new Font(Font.FontFamily, 10f, FontStyle.Bold),
            Margin = new Padding(0, 0, 0, 14)
        };
        leftPanel.Controls.Add(_totalLabel);

        FlowLayoutPanel formulaButtonPanel = new()
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 14),
            BackColor = Color.Transparent
        };

        _baseFormulaButton = CreateFormulaButton("Base", RequestedFormula.Base);
        _biasedFormulaButton = CreateFormulaButton("Biased", RequestedFormula.Biased);
        _bothFormulaButton = CreateFormulaButton("Both", RequestedFormula.Both);
        formulaButtonPanel.Controls.AddRange([_baseFormulaButton, _biasedFormulaButton, _bothFormulaButton]);
        leftPanel.Controls.Add(formulaButtonPanel);

        var barHost = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.Transparent,
            Padding = new Padding(0, 4, 0, 0)
        };
        leftPanel.Controls.Add(barHost);

        FlowLayoutPanel barsFlow = new()
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = true,
            BackColor = Color.Transparent
        };
        barHost.Controls.Add(barsFlow);

        for (int i = 0; i < _barControls.Length; i++)
        {
            int statIndex = i;
            GrowthBarControl bar = new()
            {
                Width = 310,
                StatName = _statNames[i],
                Value = _growthRates[i],
                FillColor = BarColors[i % BarColors.Length]
            };
            bar.DesiredValueChanged += desiredValue => TrySetGrowth(statIndex, desiredValue);
            _barControls[i] = bar;
            barsFlow.Controls.Add(bar);
        }

        var rightPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            BackColor = Color.FromArgb(35, 38, 46),
            Padding = new Padding(14)
        };
        rightPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        rightPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        root.Controls.Add(rightPanel, 1, 0);

        var outputHeader = new Label
        {
            AutoSize = true,
            Text = "Preview Output",
            Font = new Font(Font.FontFamily, 16f, FontStyle.Bold),
            Margin = new Padding(0, 0, 0, 10)
        };
        rightPanel.Controls.Add(outputHeader);

        _outputTextBox = new TextBox
        {
            Multiline = true,
            Dock = DockStyle.Fill,
            ScrollBars = ScrollBars.Vertical,
            ReadOnly = true,
            Font = new Font(FontFamily.GenericMonospace, 10.5f),
            BackColor = Color.FromArgb(22, 24, 30),
            ForeColor = Color.Gainsboro,
            BorderStyle = BorderStyle.FixedSingle
        };
        rightPanel.Controls.Add(_outputTextBox);

        UpdateFormulaButtonState();
        RefreshUi();
    }

    private Button CreateFormulaButton(string label, RequestedFormula formula)
    {
        Button button = new()
        {
            AutoSize = true,
            Text = label,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(58, 64, 78),
            ForeColor = Color.WhiteSmoke,
            Margin = new Padding(0, 0, 8, 0),
            Padding = new Padding(10, 6, 10, 6)
        };
        button.FlatAppearance.BorderSize = 0;
        button.Click += (_, _) =>
        {
            _requestedFormula = formula;
            UpdateFormulaButtonState();
            RefreshUi();
        };

        return button;
    }

    private void TrySetGrowth(int statIndex, int desiredValue)
    {
        int currentValue = _growthRates[statIndex];
        int delta = desiredValue - currentValue;
        if (delta == 0)
        {
            return;
        }

        int total = _growthRates.Sum();
        int appliedDelta = delta;
        if (delta > 0)
        {
            int remaining = StatGainCalculator.ExpectedGrowthTotal - total;
            appliedDelta = Math.Min(delta, remaining);
        }

        if (appliedDelta == 0)
        {
            return;
        }

        _growthRates[statIndex] = Math.Clamp(currentValue + appliedDelta, 0, StatGainCalculator.ExpectedGrowthTotal);
        _barControls[statIndex].Value = _growthRates[statIndex];
        RefreshUi();
    }

    private void RefreshUi()
    {
        _growthSummaryLabel.Text = string.Join(" | ", _statNames.Select((name, index) => $"{name}: {_growthRates[index]}"));

        int total = _growthRates.Sum();
        _totalLabel.Text = $"Total: {total}/{StatGainCalculator.ExpectedGrowthTotal}";
        _totalLabel.ForeColor = total == StatGainCalculator.ExpectedGrowthTotal
            ? Color.FromArgb(144, 220, 122)
            : Color.FromArgb(244, 193, 92);

        if (total != StatGainCalculator.ExpectedGrowthTotal)
        {
            _outputTextBox.Text =
                $"Allocate {StatGainCalculator.ExpectedGrowthTotal - total} more growth points to reach 100." +
                Environment.NewLine +
                Environment.NewLine +
                "Drag bars left or right in 5-point steps. Increasing stops automatically when the total reaches 100.";
            return;
        }

        List<string> reports = new();
        foreach (FormulaKind formula in GetFormulasToRun(_requestedFormula))
        {
            SequenceResult result = StatGainCalculator.GenerateSequence(_growthRates, 1, 20, formula);
            reports.Add(StatGainCalculator.BuildReport(result, includeHeader: _requestedFormula == RequestedFormula.Both));
        }

        _outputTextBox.Text = string.Join(Environment.NewLine + Environment.NewLine, reports);
    }

    private void UpdateFormulaButtonState()
    {
        ApplyFormulaButtonState(_baseFormulaButton, _requestedFormula == RequestedFormula.Base);
        ApplyFormulaButtonState(_biasedFormulaButton, _requestedFormula == RequestedFormula.Biased);
        ApplyFormulaButtonState(_bothFormulaButton, _requestedFormula == RequestedFormula.Both);
    }

    private static void ApplyFormulaButtonState(Button button, bool isActive)
    {
        button.BackColor = isActive ? Color.FromArgb(91, 122, 241) : Color.FromArgb(58, 64, 78);
        button.ForeColor = isActive ? Color.White : Color.WhiteSmoke;
    }

    private static IReadOnlyList<FormulaKind> GetFormulasToRun(RequestedFormula formula)
    {
        return formula switch
        {
            RequestedFormula.Base => [FormulaKind.Base],
            RequestedFormula.Biased => [FormulaKind.Biased],
            RequestedFormula.Both => [FormulaKind.Base, FormulaKind.Biased],
            _ => throw new ArgumentOutOfRangeException(nameof(formula))
        };
    }

    private enum RequestedFormula
    {
        Base,
        Biased,
        Both
    }
}
