using Antlr4.Runtime.Tree;
using clmath.Antlr;
using comroid.common;

// ReSharper disable once ArrangeNamespaceBody
namespace clmath;

public class MathCompiler : MathBaseVisitor<Component>
{
    public new IEnumerable<Component> VisitUnitFile(MathParser.UnitFileContext unit) => unit.equation().Select(Visit);

    public override Component VisitNormalEquation(MathParser.NormalEquationContext context) => new()
    {
        type = Component.Type.Equation,
        x = Visit(context.lhs),
        y = Visit(context.rhs)
    };

    public override Component VisitVariableDeclaration(MathParser.VariableDeclarationContext context) => new()
    {
        type = Component.Type.Declaration,
        arg = context.lhs.GetText(),
        x = Visit(context.rhs)
    };

    public override Component VisitVariableTarget(MathParser.VariableTargetContext context) => new()
    {
        type = Component.Type.Target,
        arg = context.lhs.GetText()
    };

    public override Component VisitExprUnit(MathParser.ExprUnitContext context) => new()
    {
        type = Component.Type.Unit,
        op = Component.Operator.Multiply,
        arg = context.unit?.GetText(),
        x = Visit(context.expr())
    };

    public override Component VisitExprUnitCast(MathParser.ExprUnitCastContext context) => new()
    {
        type = Component.Type.Unit,
        op = Component.Operator.Modulus,
        arg = context.unit?.GetText(),
        x = Visit(context.expr())
    };

    public override Component VisitExprUnitNormalize(MathParser.ExprUnitNormalizeContext context) => new()
    {
        type = Component.Type.Unit,
        op = Component.Operator.Divide,
        x = Visit(context.expr())
    };

    public override Component VisitExprNum(MathParser.ExprNumContext context) => new()
    {
        type = Component.Type.Num,
        arg = double.Parse(context.num().GetText())
    };

    public override Component VisitWord(MathParser.WordContext context) => new() { type = Component.Type.Var, arg = context.GetText() };

    public override Component VisitExprMem(MathParser.ExprMemContext context) => new() { type = Component.Type.Mem, x = context.n != null ? Visit(context.n) : null };

    public override Component VisitExprFrac(MathParser.ExprFracContext context) => new()
    {
        type = Component.Type.Frac,
        x = Visit(context.frac().x),
        y = Visit(context.frac().y)
    };

    public override Component VisitExprFunc(MathParser.ExprFuncContext context)
    {
        return new Component
        {
            type = Component.Type.FuncX,
            func = context.fx().func().Start.Type switch
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
                _ => throw new NotSupportedException(context.fx().func().GetText())
            },
            x = Visit(context.fx().x)
        };
    }

    public override Component VisitExprFact(MathParser.ExprFactContext context) => new()
    {
        type = Component.Type.Factorial,
        x = Visit(context.x)
    };

    public override Component VisitExprRoot(MathParser.ExprRootContext context) => new()
    {
        type = Component.Type.Root,
        x = Visit(context.root().x),
        y = Visit(context.root().i)
    };

    public override Component VisitExprAbs(MathParser.ExprAbsContext context) => new()
    {
        type = Component.Type.Abs,
        x = Visit(context.abs().x)
    };

    public override Component VisitExprPow(MathParser.ExprPowContext context) => new()
    {
        type = Component.Type.Op,
        op = Component.Operator.Power,
        x = Visit(context.x),
        y = Visit(context.y)
    };

    public override Component VisitExprOp1(MathParser.ExprOp1Context context)
    {
        return new Component
        {
            type = Component.Type.Op,
            op = context.op_1().Start.Type switch
            {
                MathLexer.OP_MUL => Component.Operator.Multiply,
                MathLexer.OP_DIV => Component.Operator.Divide,
                MathLexer.OP_MOD => Component.Operator.Modulus,
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
                MathLexer.OP_ADD => Component.Operator.Add,
                MathLexer.OP_SUB => Component.Operator.Subtract,
                _ => throw new NotSupportedException(context.op_2().GetText())
            },
            x = Visit(context.l),
            y = Visit(context.r)
        };
    }

    public override Component VisitEval(MathParser.EvalContext context) => new()
    {
        type = Component.Type.Eval,
        arg = context.name.GetText(),
        args = VisitVars(context.evalVar())
    };

    public override Component VisitExprPar(MathParser.ExprParContext context) => new()
    {
        type = Component.Type.Parentheses,
        x = Visit(context.n)
    };

    private Component[] VisitVars(MathParser.EvalVarContext[] evalVar)
    {
        return evalVar.Select(context => new Component
        {
            type = Component.Type.EvalVar,
            arg = context.name.GetText(),
            x = Visit(context.expr())
        }).ToArray();
    }

    public override Component Visit(IParseTree? tree) => (tree == null ? null : base.Visit(tree))!;

    protected override bool ShouldVisitNextChild(IRuleNode node, Component? currentResult) => currentResult == null;
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

    public enum Operator : byte
    {
        Add,
        Subtract,
        Multiply,
        Divide,
        Modulus,
        Power
    }

    public enum OutputType
    {
        Text = default,
        LaTeX
    }

    public enum Type
    {
        Num,
        Var,
        FuncX,
        Factorial,
        Root,
        Abs,
        Frac,
        Eval,
        EvalVar,
        Op,
        Mem,
        Parentheses,
        Unit,
        Equation,
        Declaration,
        Target
    }

    public Type type { get; set; }
    public FuncX? func { get; set; }
    public Operator? op { get; set; }
    public Component? x { get; set; }
    public Component? y { get; set; }
    public object? arg { get; set; }
    public Component[] args { get; set; }

    public IEnumerable<string> Vars()
    {
        if (type == Type.Var)
            return new List<string> { (arg as string)! };
        IEnumerable<string> vars = Array.Empty<string>();
        if (type == Type.Eval)
        {
            vars = vars.Concat(Program.LoadFunc(arg!.ToString()!)?.function?.Vars()
                               ?? Array.Empty<string>());
            foreach (var arg in args)
                vars = vars.Append(arg.arg!.ToString()!);
        }

        return vars.Concat(x?.Vars() ?? Array.Empty<string>())
            .Concat(y?.Vars() ?? Array.Empty<string>());
    }

    public UnitValue Evaluate(MathContext? ctx)
    {
        UnitValue _()
        {
            var x = this.x?.Evaluate(ctx);
            var y = this.y?.Evaluate(ctx);
            switch (type, func, op)
            {
                case (Type.Num, _, _):
                    return (double)arg!;
                case (Type.Var, _, _):
                    if (arg is not string name)
                        throw new Exception("Invalid arg: " + arg);
                    if (name.StartsWith("rng"))
                        if (name.EndsWith("i"))
                            return Program.RNG.Next();
                        else if (name.EndsWith("d"))
                            return Program.RNG.NextDouble();
                        else throw new Exception("Invalid random: " + arg);
                    if (Program.constants.TryGetValue(name, out var val))
                        return val;
                    return ctx![name]!.Evaluate(ctx);
                case (Type.Mem, _, _):
                    return ctx![(int?)x?.Value ?? 0];
                case (Type.FuncX, FuncX.Sin, _): return (x as Unit)! * Math.Sin(Program.IntoDRG(x!));
                case (Type.FuncX, FuncX.Cos, _): return (x as Unit)! * Math.Cos(Program.IntoDRG(x!));
                case (Type.FuncX, FuncX.Tan, _): return (x as Unit)! * Math.Tan(Program.IntoDRG(x!));
                case (Type.FuncX, FuncX.Log, _): return (x as Unit)! * Math.Log(x!);
                case (Type.FuncX, FuncX.ArcSin, _): return (x as Unit)! * Math.Asin(Program.IntoDRG(x!));
                case (Type.FuncX, FuncX.ArcCos, _): return (x as Unit)! * Math.Acos(Program.IntoDRG(x!));
                case (Type.FuncX, FuncX.ArcTan, _): return (x as Unit)! * Math.Atan(Program.IntoDRG(x!));
                case (Type.FuncX, _, _):
                    throw new NotImplementedException(func.ToString());
                case (Type.Factorial, _, _):
                    var yield = 1;
                    for (var rem = (int)x!; rem > 0; rem--)
                        yield *= rem;
                    return (x as Unit)! * yield;
                case (Type.Root, _, _):
                    // ReSharper disable once CompareOfFloatsByEqualityOperator
                    //return x!.Root(y);
                    return (x as Unit)! * Math.Pow(x!, 1 / ((double?)y ?? 2d));
                case (Type.Abs, _, _):
                    return (x as Unit)! * Math.Abs(x!);
                case (Type.Op, _, Operator.Add):
                    return x! + y!;
                case (Type.Op, _, Operator.Subtract):
                    return x! - y!;
                case (Type.Op, _, Operator.Multiply):
                    return x! * y!;
                case (Type.Frac, _, _):
                case (Type.Op, _, Operator.Divide):
                    return x! / y!;
                case (Type.Op, _, Operator.Modulus):
                    return (x as Unit)! * (x! % y!);
                case (Type.Op, _, Operator.Power):
                    return (x as Unit)! * Math.Pow(x!, y!);
                case (Type.Eval, _, _):
                    if (Program.LoadFunc(arg!.ToString()!) is not { } res)
                        return double.NaN;
                    var subCtx = new MathContext(res, null);
                    foreach (var (key, value) in ctx!.Vars())
                        subCtx[key] = value;
                    foreach (var var in args)
                        subCtx[var.arg!.ToString()!] = var.x!;
                    return res.function!.Evaluate(subCtx);
                case (Type.Parentheses, _, _):
                    return x!;
                case (Type.Unit, _, Operator.Multiply): // apply unit
                    return (double)x! * Units.ParseUnit(arg?.ToString()!);
                case (Type.Unit, _, Operator.Modulus): // cast unit
                    return x! | Units.ParseUnit(arg?.ToString()!);
                case (Type.Unit, _, Operator.Divide): // normalize unit
                    return x!.Normalize();
                default: throw new NotSupportedException(ToString());
            }
        }

        return _();
    }

    internal UnitValue? ToUnit(MathContext? ctx) => ctx == null || arg == null ? null : Units.Parse((string)arg!);

    public override string ToString() => ToString(Program.Output);
    public string ToString(OutputType output)
    {
        switch (type)
        {
            case Type.Num:
                return (arg as double?)?.ToString("0." + new string('#', 15))!;
            case Type.Var:
                return output switch
                {
                    OutputType.LaTeX => $"\\text{{{arg}}}",
                    _ => arg?.ToString()!
                };
            case Type.Mem:
                return "mem" + (x == null ? string.Empty : $"[{x}]");
            case Type.FuncX:
                return (output == OutputType.LaTeX ? '\\' : string.Empty) + $"{func.ToString()!.ToLower()}({x})";
            case Type.Factorial:
                return $"{x}!";
            case Type.Root:
                var n = y?.ToString() ?? "2";
                switch (output)
                {
                    case OutputType.LaTeX:
                        return "\\sqrt" + (n == "2" ? string.Empty : $"[{n}]") + $"{{{x}}}";
                    default:
                        return $"{(n == "2" ? "sqrt" : $"root[{n}]")}({x})";
                }
            case Type.Abs:
                return $"|{x}|";
            case Type.Frac:
                return output switch
                {
                    OutputType.LaTeX => $"\\frac{{{x}}}{{{y}}}",
                    _ => $"frac({x})({y})"
                };
            case Type.Op:
                return (op, output) switch
                {
                    (Operator.Add, _) => $"{x}+{y}",
                    (Operator.Subtract, _) => $"{x}-{y}",
                    (Operator.Multiply, OutputType.LaTeX) => $"{x}\\cdot{y}",
                    (Operator.Multiply, _) => $"{x}*{y}",
                    (Operator.Divide, OutputType.LaTeX) => $"\\frac{{{x}}}{{{y}}}",
                    (Operator.Divide, _) => $"{x}/{y}",
                    (Operator.Modulus, _) => $"{x}%{y}",
                    (Operator.Power, OutputType.LaTeX) => $"{x}^{{{y}}}",
                    (Operator.Power, _) => $"{x}^{y}",
                    _ => throw new ArgumentOutOfRangeException()
                };
            case Type.Eval:
                return $"${arg}" + (args.Length == 0
                    ? string.Empty
                    : $"{{{string.Join("; ", args.Select(var => $"{var.arg}={var.x}"))}}}");
            case Type.Parentheses:
                return $"({x})";
            case Type.Unit:
                return op switch
                {
                    Operator.Multiply => $"{x}{(arg != null ? output == OutputType.LaTeX ? $"\\text{{{arg}}}" : arg.ToString() : string.Empty)}",
                    Operator.Modulus => $"{x} as {arg}",
                    Operator.Divide => $"{x}?",
                    _ => throw new ArgumentOutOfRangeException()
                };
            case Type.Equation:
                return $"{x} {(output == OutputType.LaTeX ? "&" : "")}= {y}";
            case Type.EvalVar:
                return arg?.ToString() ?? "null";
            default:
                throw new ArgumentOutOfRangeException(nameof(type), type, "Unsupported type");
        }
    }

    public bool HasChildren() => x != null || y != null;

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

    public Unit? FindOutputUnit()
    {
        if (type is Type.Unit or Type.Var)
            return Units.ParseUnit((arg as string)!);
        return x != null ? x.FindOutputUnit() : y?.FindOutputUnit();
    }
}