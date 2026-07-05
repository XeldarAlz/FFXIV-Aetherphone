using System.Globalization;

namespace Aetherphone.Apps.Calculator;

internal enum CalcOp : byte
{
    None,
    Add,
    Subtract,
    Multiply,
    Divide,
}

internal readonly record struct CalcHistoryEntry(string Expression, string Result);

internal sealed class CalculatorEngine
{
    private const int MaxDigits = 9;
    private const int MaxHistory = 50;

    private readonly List<CalcHistoryEntry> history = new();
    private double accumulator;
    private double lastOperand;
    private CalcOp pending;
    private bool freshEntry = true;
    private bool justEvaluated;
    private bool error;
    private string exprPrefix = string.Empty;
    private string lastExpression = string.Empty;

    public string Display { get; private set; } = "0";

    public IReadOnlyList<CalcHistoryEntry> History => history;

    public CalcOp ActiveOperator => pending != CalcOp.None && freshEntry && !justEvaluated ? pending : CalcOp.None;

    public bool ShowAllClear => Display == "0" && !error;

    public string Expression
    {
        get
        {
            if (justEvaluated)
            {
                return lastExpression.Length > 0 ? lastExpression + " =" : string.Empty;
            }

            if (exprPrefix.Length == 0 && freshEntry)
            {
                return string.Empty;
            }

            return (exprPrefix + (freshEntry ? string.Empty : Display)).TrimEnd();
        }
    }

    public void InputDigit(int digit)
    {
        if (error)
        {
            Reset();
        }

        StartFreshAfterEvaluation();

        if (freshEntry)
        {
            Display = digit.ToString(CultureInfo.InvariantCulture);
            freshEntry = false;
            return;
        }

        if (Display == "0")
        {
            Display = digit.ToString(CultureInfo.InvariantCulture);
            return;
        }

        if (Display == "-0")
        {
            Display = "-" + digit.ToString(CultureInfo.InvariantCulture);
            return;
        }

        if (SignificantDigits(Display) >= MaxDigits)
        {
            return;
        }

        Display += digit.ToString(CultureInfo.InvariantCulture);
    }

    public void InputDecimal()
    {
        if (error)
        {
            Reset();
        }

        StartFreshAfterEvaluation();

        if (freshEntry)
        {
            Display = "0.";
            freshEntry = false;
            return;
        }

        if (!Display.Contains('.'))
        {
            Display += ".";
        }
    }

    public void SetOperator(CalcOp op)
    {
        if (error)
        {
            return;
        }

        if (justEvaluated)
        {
            exprPrefix = Display + " " + Symbol(op) + " ";
            accumulator = Parse(Display);
            pending = op;
            freshEntry = true;
            justEvaluated = false;
            lastExpression = string.Empty;
            return;
        }

        if (freshEntry && pending != CalcOp.None)
        {
            pending = op;
            exprPrefix = ReplaceTrailingOperator(exprPrefix, op);
            return;
        }

        var entryText = Display;
        if (pending != CalcOp.None)
        {
            accumulator = Apply(accumulator, pending, Parse(Display));
            Display = Format(accumulator);
            if (error)
            {
                return;
            }
        }
        else
        {
            accumulator = Parse(Display);
        }

        exprPrefix += entryText + " " + Symbol(op) + " ";
        pending = op;
        freshEntry = true;
    }

    public void Equals()
    {
        if (error || pending == CalcOp.None)
        {
            justEvaluated = true;
            freshEntry = true;
            return;
        }

        var entryText = freshEntry ? Format(lastOperand) : Display;
        var operand = freshEntry ? lastOperand : Parse(Display);
        lastOperand = operand;
        var fullExpression = exprPrefix + entryText;
        accumulator = Apply(accumulator, pending, operand);
        Display = Format(accumulator);
        lastExpression = fullExpression;
        if (!error)
        {
            PushHistory(fullExpression, Display);
        }

        exprPrefix = string.Empty;
        pending = CalcOp.None;
        freshEntry = true;
        justEvaluated = true;
    }

    public void Negate()
    {
        if (error)
        {
            return;
        }

        if (Display.StartsWith('-'))
        {
            Display = Display.Substring(1);
        }
        else if (Display != "0")
        {
            Display = "-" + Display;
        }
    }

    public void Percent()
    {
        if (error)
        {
            return;
        }

        Display = Format(Parse(Display) / 100.0);
        justEvaluated = false;
    }

    public void Clear()
    {
        if (Display != "0")
        {
            Display = "0";
            freshEntry = true;
            justEvaluated = false;
            return;
        }

        Reset();
    }

    public void Recall(string result)
    {
        Display = result;
        accumulator = Parse(result);
        exprPrefix = string.Empty;
        lastExpression = string.Empty;
        pending = CalcOp.None;
        freshEntry = true;
        justEvaluated = false;
        error = false;
    }

    public void ClearHistory() => history.Clear();

    private void StartFreshAfterEvaluation()
    {
        if (!justEvaluated)
        {
            return;
        }

        accumulator = 0;
        pending = CalcOp.None;
        exprPrefix = string.Empty;
        lastExpression = string.Empty;
        justEvaluated = false;
        freshEntry = true;
    }

    private void PushHistory(string expression, string result)
    {
        history.Insert(0, new CalcHistoryEntry(expression, result));
        if (history.Count > MaxHistory)
        {
            history.RemoveAt(history.Count - 1);
        }
    }

    private void Reset()
    {
        accumulator = 0;
        lastOperand = 0;
        pending = CalcOp.None;
        Display = "0";
        exprPrefix = string.Empty;
        lastExpression = string.Empty;
        freshEntry = true;
        justEvaluated = false;
        error = false;
    }

    private double Apply(double left, CalcOp op, double right)
    {
        switch (op)
        {
            case CalcOp.Add:
                return left + right;
            case CalcOp.Subtract:
                return left - right;
            case CalcOp.Multiply:
                return left * right;
            case CalcOp.Divide:
                if (right == 0.0)
                {
                    error = true;
                    return 0.0;
                }

                return left / right;
            default:
                return right;
        }
    }

    private string Format(double value)
    {
        if (error || double.IsNaN(value) || double.IsInfinity(value))
        {
            error = true;
            return "Error";
        }

        var rounded = Math.Round(value, 8, MidpointRounding.AwayFromZero);
        if (rounded == 0.0)
        {
            return "0";
        }

        return rounded.ToString("0.########", CultureInfo.InvariantCulture);
    }

    private static string Symbol(CalcOp op)
    {
        return op switch
        {
            CalcOp.Add => "+",
            CalcOp.Subtract => "-",
            CalcOp.Multiply => "×",
            CalcOp.Divide => "÷",
            _ => string.Empty,
        };
    }

    private static string ReplaceTrailingOperator(string prefix, CalcOp op)
    {
        var trimmed = prefix.TrimEnd();
        var lastSpace = trimmed.LastIndexOf(' ');
        var head = lastSpace >= 0 ? trimmed.Substring(0, lastSpace) : trimmed;
        return head + " " + Symbol(op) + " ";
    }

    private static double Parse(string text)
    {
        return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : 0.0;
    }

    private static int SignificantDigits(string text)
    {
        var count = 0;
        for (var index = 0; index < text.Length; index++)
        {
            if (char.IsDigit(text[index]))
            {
                count++;
            }
        }

        return count;
    }
}
