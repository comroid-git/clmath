using Antlr4.Runtime.Tree;
using clmath.Antlr;

namespace clmath;

public class MathCompiler : MathBaseVisitor<Component>
{
    public override Component VisitNum(MathParser.NumContext context)
    {
        return new Component { type = Component.Type.Num, arg = double.Parse(context.GetText()) };
    }

    public override Component VisitWord(MathParser.WordContext context)
    {
        return new Component { type = Component.Type.Var, arg = context.GetText() };
    }

    public override Component VisitFrac(MathParser.FracContext context)
    {
        return new Component { type = Component.Type.Frac, x = Visit(context.x), y = Visit(context.y) };
    }

    public override Component VisitFx(MathParser.FxContext context)
    {
        return new Component
        {
            type = Component.Type.FuncX,
            func = context.func().Start.Type switch
            {
                MathLexer.SIN => Component.FuncX.Sin,
                MathLexer.COS => Component.FuncX.Cos,
                MathLexer.TAN => Component.FuncX.Tan,
                MathLexer.LOG => Component.FuncX.Log,
                MathLexer.SEC => Component.FuncX.Sec,
                MathLexer.CSC => Component.FuncX.Csc,
                MathLexer.COT => Component.FuncX.Cot,
                MathLexer.HYP => Component.FuncX.Hyp,
                MathLexer.ARCSIN => Component.FuncX.ArcSin,
                MathLexer.ARCCOS => Component.FuncX.ArcCos,
                MathLexer.ARCTAN => Component.FuncX.ArcTan,
                _ => throw new NotSupportedException(context.func().GetText())
            },
            x = Visit(context.x)
        };
    }

    public override Component VisitExprFact(MathParser.ExprFactContext context)
    {
        return new Component { type = Component.Type.Factorial, x = Visit(context.x) };
    }

    public override Component VisitRoot(MathParser.RootContext context)
    {
        return new Component { type = Component.Type.Root, x = Visit(context.x), y = Visit(context.i) };
    }

    public override Component VisitExprOp1(MathParser.ExprOp1Context context)
    {
        return new Component
        {
            type = Component.Type.Op,
            op = context.op_1().Start.Type switch
            {
                MathLexer.POW => Component.Operator.Power,
                _ => throw new NotSupportedException(context.op_1().GetText())
            },
            x = Visit(context.l),
            y = Visit(context.r)
        };
    }

    public override Component VisitExprOp2(MathParser.ExprOp2Context context)
    {
        return new Component
        {
            type = Component.Type.Op,
            op = context.op_2().Start.Type switch
            {
                MathLexer.OP_MUL => Component.Operator.Multiply,
                MathLexer.OP_DIV => Component.Operator.Divide,
                MathLexer.OP_MOD => Component.Operator.Modulus,
                _ => throw new NotSupportedException(context.op_2().GetText())
            },
            x = Visit(context.l),
            y = Visit(context.r)
        };
    }

    public override Component VisitExprOp3(MathParser.ExprOp3Context context)
    {
        return new Component
        {
            type = Component.Type.Op,
            op = context.op_3().Start.Type switch
            {
                MathLexer.OP_ADD => Component.Operator.Add,
                MathLexer.OP_SUB => Component.Operator.Subtract,
                _ => throw new NotSupportedException(context.op_3().GetText())
            },
            x = Visit(context.l),
            y = Visit(context.r)
        };
    }

    public override Component VisitEval(MathParser.EvalContext context)
    {
        return new Component
        {
            type = Component.Type.Eval,
            arg = context.name.GetText(),
            args = VisitVars(context.evalVar())
        };
    }

    public override Component VisitExprPar(MathParser.ExprParContext context)
    {
        return new Component
        {
            type = Component.Type.Parentheses,
            x = Visit(context.n)
        };
    }

    private Component[] VisitVars(MathParser.EvalVarContext[] evalVar)
    {
        return evalVar.Select(context => new Component
        {
            type = Component.Type.EvalVar,
            arg = context.name.GetText(),
            x = Visit(context.expr())
        }).ToArray();
    }

    public override Component Visit(IParseTree? tree)
    {
        return (tree == null ? null : base.Visit(tree))!;
    }

    protected override bool ShouldVisitNextChild(IRuleNode node, Component? currentResult)
    {
        return currentResult == null;
    }
}

public sealed class Component
{
    public enum FuncX
    {
        Sin,
        Cos,
        Tan,
        Log,
        Sec,
        Csc,
        Cot,
        Hyp,
        ArcSin,
        ArcCos,
        ArcTan
    }

    public enum Operator
    {
        Add,
        Subtract,
        Multiply,
        Divide,
        Modulus,
        Power
    }

    public enum Type
    {
        Num,
        Var,
        FuncX,
        Factorial,
        Root,
        Frac,
        Eval,
        EvalVar,
        Op,
        Parentheses
    }

    public Type type { get; init; }
    public FuncX? func { get; init; }
    public Operator? op { get; init; }
    public Component? x { get; set; }
    public Component? y { get; set; }
    public object? arg { get; set; }
    public Component[] args { get; init; }

    public List<string> EnumerateVars()
    {
        if (type == Type.Var)
            return new List<string> { (arg as string)! };
        List<string> vars = new();
        if (type == Type.Eval)
        {
            Program.LoadFunc(arg!.ToString()!)?.func.EnumerateVars().ForEach(vars.Add);
            foreach (var arg in args)
                vars.Add(arg.arg!.ToString()!);
        }

        x?.EnumerateVars().ForEach(vars.Add);
        y?.EnumerateVars().ForEach(vars.Add);
        return vars;
    }

    public double Evaluate(MathContext? ctx)
    {
        var x = this.x?.Evaluate(ctx);
        var y = this.y?.Evaluate(ctx);
        switch (type)
        {
            case Type.Num:
                return (double)arg!;
            case Type.Var:
                if (Program.constants.TryGetValue((string)arg!, out var val))
                    return val;
                return ctx!.var[(string)arg!].Evaluate(ctx);
            case Type.FuncX:
                switch (func)
                {
                    case FuncX.Sin:
                        return Math.Sin(Program.IntoDRG(x!.Value));
                    case FuncX.Cos:
                        return Math.Cos(Program.IntoDRG(x!.Value));
                    case FuncX.Tan:
                        return Math.Tan(Program.IntoDRG(x!.Value));
                    case FuncX.Log:
                        return Math.Log(x!.Value);
                    case FuncX.Sec:
                    case FuncX.Csc:
                    case FuncX.Cot:
                    case FuncX.Hyp:
                        throw new NotImplementedException(func.ToString());
                    case FuncX.ArcSin:
                        return Program.FromDRG(Math.Asin(x!.Value));
                    case FuncX.ArcCos:
                        return Program.FromDRG(Math.Acos(x!.Value));
                    case FuncX.ArcTan:
                        return Program.FromDRG(Math.Atan(x!.Value));
                    case null: throw new Exception("invalid state");
                }

                break;
            case Type.Factorial:
                var yield = 1;
                for (var rem = (int)x!.Value; rem > 0; rem--)
                    yield *= rem;
                return yield;
            case Type.Root:
                // ReSharper disable once CompareOfFloatsByEqualityOperator
                return Math.Pow(x!.Value, 1 / (y ?? 2d));
            case Type.Frac:
                return x!.Value / y!.Value;
            case Type.Op:
                switch (op)
                {
                    case Operator.Add:
                        return x!.Value + y!.Value;
                    case Operator.Subtract:
                        return x!.Value - y!.Value;
                    case Operator.Multiply:
                        return x!.Value * y!.Value;
                    case Operator.Divide:
                        return x!.Value / y!.Value;
                    case Operator.Modulus:
                        return x!.Value % y!.Value;
                    case Operator.Power:
                        return Math.Pow(x!.Value, y!.Value);
                    case null: throw new Exception("invalid state");
                }

                break;
            case Type.Eval:
                if (Program.LoadFunc(arg!.ToString()!) is not { } res)
                    return double.NaN;
                var subCtx = new MathContext(res.ctx);
                foreach (var (key, value) in ctx!.var)
                    subCtx.var[key] = value;
                foreach (var var in args)
                    subCtx.var[var.arg!.ToString()!] = var.x!;
                return res.func.Evaluate(subCtx);
            case Type.Parentheses:
                return x!.Value;
        }

        throw new NotSupportedException(ToString());
    }

    public override string ToString()
    {
        switch (type)
        {
            case Type.Num:
            case Type.Var:
                return arg!.ToString()!;
            case Type.FuncX:
                return $"{func.ToString()!.ToLower()}({x})";
            case Type.Factorial:
                return $"{x}!";
            case Type.Root:
                var n = y?.ToString() ?? "2";
                return $"{(n == "2" ? "sqrt" : $"root[{n}]")}({x})";
            case Type.Frac:
                return $"frac({x})({y})";
            case Type.Op:
                var op = this.op switch
                {
                    Operator.Add => '+',
                    Operator.Subtract => '-',
                    Operator.Multiply => '*',
                    Operator.Divide => '/',
                    Operator.Modulus => '%',
                    Operator.Power => '^',
                    _ => throw new ArgumentOutOfRangeException()
                };
                return $"{x}{op}{y}";
            case Type.Eval:
                return $"${arg}" + (args.Length == 0
                    ? string.Empty
                    : $"{{{string.Join("; ", args.Select(var => $"{var.arg}={var.x}"))}}}");
            case Type.Parentheses:
                return $"({x})";
            default:
                throw new ArgumentOutOfRangeException(nameof(type));
        }
    }

    public bool HasChildren()
    {
        return x != null || y != null;
    }

    public Component Copy(bool? deepCopy = null)
    {
        var _deepCopy = deepCopy ?? HasChildren();
        var argsCopy = args != null ? new Component[args.Length] : Array.Empty<Component>();
        for (var i = 0; i < argsCopy.Length; i++)
            argsCopy[i] = args![i].Copy(deepCopy);
        var copy = new Component
        {
            type = type,
            func = func,
            op = op,
            arg = arg,
            args = argsCopy
        };
        if (_deepCopy)
        {
            copy.x = x?.Copy(deepCopy);
            copy.y = y?.Copy(deepCopy);
        }

        return copy;
    }
}