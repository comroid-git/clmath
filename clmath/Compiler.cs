using Antlr4.Runtime.Tree;
using clmath.Antlr;

// ReSharper disable once ArrangeNamespaceBody
namespace clmath
{
    public class MathCompiler : MathBaseVisitor<Component>
    {
        public override Component VisitExprUnit(MathParser.ExprUnitContext context)
        {
            return new Component
            {
                type = Component.Type.Unit,
                arg = context.unit?.GetText(),
                x = Visit(context.expr()),
                op = context.h != null || context.unit == null ? Component.Operator.Modulus : null
            };
        }

        public override Component VisitExprNum(MathParser.ExprNumContext context)
        {
            return new Component
            {
                type = Component.Type.Num,
                arg = double.Parse(context.num().GetText())
            };
        }

        public override Component VisitWord(MathParser.WordContext context)
        {
            return new Component { type = Component.Type.Var, arg = context.GetText() };
        }

        public override Component VisitExprMem(MathParser.ExprMemContext context)
        {
            return new Component { type = Component.Type.Mem, x = context.n != null ? Visit(context.n) : null };
        }

        public override Component VisitExprFrac(MathParser.ExprFracContext context)
        {
            return new Component
            {
                type = Component.Type.Frac,
                x = Visit(context.frac().x),
                y = Visit(context.frac().y)
            };
        }

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

        public override Component VisitExprFact(MathParser.ExprFactContext context)
        {
            return new Component
            {
                type = Component.Type.Factorial,
                x = Visit(context.x)
            };
        }

        public override Component VisitExprRoot(MathParser.ExprRootContext context)
        {
            return new Component
            {
                type = Component.Type.Root,
                x = Visit(context.root().x),
                y = Visit(context.root().i)
            };
        }

        public override Component VisitExprAbs(MathParser.ExprAbsContext context)
        {
            return new Component
            {
                type = Component.Type.Abs,
                x = Visit(context.abs().x)
            };
        }

        public override Component VisitExprPow(MathParser.ExprPowContext context)
        {
            return new Component
            {
                type = Component.Type.Op,
                op = Component.Operator.Power,
                x = Visit(context.x),
                y = Visit(context.y)
            };
        }

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
            Abs,
            Frac,
            Eval,
            EvalVar,
            Op,
            Mem,
            Parentheses,
            Unit
        }

        public Type type { get; set; }
        public FuncX? func { get; set; }
        public Operator? op { get; set; }
        public Component? x { get; set; }
        public Component? y { get; set; }
        public object? arg { get; set; }
        public Component[] args { get; set; }

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
            return vars.Distinct().ToList();
        }

        public UnitResult Evaluate(MathContext? ctx)
        {
            var x = this.x?.Evaluate(ctx);
            var y = this.y?.Evaluate(ctx);
            switch (type)
            {
                case Type.Num:
                    return new UnitResult((double) arg!);
                case Type.Var:
                    if (arg is not string name)
                        throw new Exception("Invalid arg: " + arg);
                    if (name.StartsWith("rng"))
                        if (name.EndsWith("i"))
                            return new UnitResult(Program.RNG.Next()).Normalize();
                        else if (name.EndsWith("d"))
                            return new UnitResult(Program.RNG.NextDouble()).Normalize();
                        else throw new Exception("Invalid random: " + arg);
                    if (Program.constants.TryGetValue(name, out var val))
                        return new UnitResult(val).Normalize();
                    return ctx![name]!.Evaluate(ctx);
                case Type.Mem:
                    return ctx![(int?)x?.Value ?? 0];
                case Type.FuncX:
                    double result;
                    switch (func)
                    {
                        case FuncX.Sin:
                            result = Math.Sin(Program.IntoDRG(x!.Value));
                            break;
                        case FuncX.Cos:
                            result = Math.Cos(Program.IntoDRG(x!.Value));
                            break;
                        case FuncX.Tan:
                            result = Math.Tan(Program.IntoDRG(x!.Value));
                            break;
                        case FuncX.Log:
                            result = Math.Log(x!.Value);
                            break;
                        case FuncX.Sec:
                        case FuncX.Csc:
                        case FuncX.Cot:
                        case FuncX.Hyp:
                            throw new NotImplementedException(func.ToString());
                        case FuncX.ArcSin:
                            result = Program.FromDRG(Math.Asin(x!.Value));
                            break;
                        case FuncX.ArcCos:
                            result = Program.FromDRG(Math.Acos(x!.Value));
                            break;
                        case FuncX.ArcTan:
                            result = Program.FromDRG(Math.Atan(x!.Value));
                            break;
                        default: throw new Exception("invalid state");
                    }

                    return new UnitResult(SiUnit.None, result);
                case Type.Factorial:
                    var yield = 1;
                    for (var rem = (int)x!.Value; rem > 0; rem--)
                        yield *= rem;
                    return new UnitResult(x.Unit, yield).Normalize();
                case Type.Root:
                    // ReSharper disable once CompareOfFloatsByEqualityOperator
                    return new UnitResult(SiUnit.None, Math.Pow(x!.Value, 1 / (y?.Value ?? 2d))).Normalize();
                case Type.Abs:
                    return new UnitResult(x!.Unit, Math.Abs(x.Value)).Normalize();
                case Type.Frac:
                    return x! / y!;
                case Type.Op:
                    switch (op)
                    {
                        case Operator.Add:
                            return new UnitResult(x!.Unit, x.Unit.Prefix.Convert(x.Unit.Prefix, x.Value) + y!.Unit.Prefix.Convert(x.Unit.Prefix, y.Value)).Normalize();
                        case Operator.Subtract:
                            return new UnitResult(x!.Unit, x.Unit.Prefix.Convert(x.Unit.Prefix, x.Value) - y!.Unit.Prefix.Convert(x.Unit.Prefix, y.Value)).Normalize();
                        case Operator.Multiply:
                            return (x! * y!).Normalize();
                        case Operator.Divide:
                            return (x! / y!).Normalize();
                        case Operator.Modulus:
                            return new UnitResult(x!.Unit, x.Unit.Prefix.Convert(x.Unit.Prefix, x.Value) % y!.Unit.Prefix.Convert(x.Unit.Prefix, y.Value)).Normalize();
                        case Operator.Power:
                            return new UnitResult(x!.Unit, Math.Pow(x.Unit.Prefix.Convert(x.Unit.Prefix, x.Value), y!.Unit.Prefix.Convert(x.Unit.Prefix, y.Value))).Normalize();
                        case null: throw new Exception("invalid state");
                    }

                    break;
                case Type.Eval:
                    if (Program.LoadFunc(arg!.ToString()!) is not { } res)
                        return new UnitResult(SiUnit.None, double.NaN).Normalize();
                    var subCtx = new MathContext(res.ctx);
                    foreach (var (key, value) in ctx!.Vars())
                        subCtx[key] = value;
                    foreach (var var in args)
                        subCtx[var.arg!.ToString()!] = var.x!;
                    return res.func.Evaluate(subCtx);
                case Type.Parentheses:
                    return new UnitResult(x!.Unit, x.Value).Normalize();
                case Type.Unit:
                    var unitResult = new UnitResult(arg is not string str ? x!.Unit : new SiUnit(str ?? "", ctx!.GetUnitPackages()), x!.Value);
                    if (op == Operator.Modulus)
                        return unitResult.Normalize(SiPrefix.None);
                    else return unitResult.Normalize();
            }

            throw new NotSupportedException(ToString());
        }

        internal SiUnit? ToUnit(MathContext? ctx)
        {
            return ctx == null || arg == null ? null : new SiUnit((string)arg!, ctx.GetUnitPackages());
        }

        public override string ToString()
        {
            switch (type)
            {
                case Type.Num:
                case Type.Var:
                    return arg?.ToString()!;
                case Type.Mem:
                    return "mem" + (x == null ? string.Empty : $"[{x}]");
                case Type.FuncX:
                    return $"{func.ToString()!.ToLower()}({x})";
                case Type.Factorial:
                    return $"{x}!";
                case Type.Root:
                    var n = y?.ToString() ?? "2";
                    return $"{(n == "2" ? "sqrt" : $"root[{n}]")}({x})";
                case Type.Abs:
                    return $"|{x}|";
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
                case Type.Unit:
                    return $"{x}:{arg ?? string.Empty}{(arg != null && this.op == Operator.Modulus ? ":" : string.Empty)}";
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
}