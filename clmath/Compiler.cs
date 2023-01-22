﻿using Antlr4.Runtime.Tree;
using clmath.Antlr;

// ReSharper disable once ArrangeNamespaceBody
namespace clmath
{
    public class MathCompiler : MathBaseVisitor<Component>
    {
        public new IEnumerable<Component> VisitUnitFile(MathParser.UnitFileContext unit)
        {
            return unit.equation().Select(Visit);
        }

        public override Component VisitEquation(MathParser.EquationContext context)
        {
            return new Component
            {
                type = Component.Type.Equation,
                x = Visit(context.lhs),
                y = Visit(context.rhs)
            };
        }

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

        public enum Operator : byte
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
            Unit,
            Equation
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

        public UnitResult Evaluate(MathContext? ctx)
        {
            var x = this.x?.Evaluate(ctx);
            var y = this.y?.Evaluate(ctx);
            switch (type, func, op)
            {
                case (Type.Num, _, _):
                    return new UnitResult((double)arg!);
                case (Type.Var, _, _):
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
                case (Type.Mem, _, _):
                    return ctx![(int?)x?.Value ?? 0];
                case (Type.FuncX, FuncX.Sin, _):
                    return new UnitResult(SiUnit.None, Math.Sin(Program.IntoDRG(x!.ValueNeutralized)));
                case (Type.FuncX, FuncX.Cos, _):
                    return new UnitResult(SiUnit.None, Math.Cos(Program.IntoDRG(x!.ValueNeutralized)));
                case (Type.FuncX, FuncX.Tan, _):
                    return new UnitResult(SiUnit.None, Math.Tan(Program.IntoDRG(x!.ValueNeutralized)));
                case (Type.FuncX, FuncX.Log, _):
                    return new UnitResult(SiUnit.None, Math.Log(x!.ValueNeutralized));
                case (Type.FuncX, FuncX.ArcSin, _):
                    return new UnitResult(SiUnit.None, Math.Asin(Program.IntoDRG(x!.ValueNeutralized)));
                case (Type.FuncX, FuncX.ArcCos, _):
                    return new UnitResult(SiUnit.None, Math.Acos(Program.IntoDRG(x!.ValueNeutralized)));
                case (Type.FuncX, FuncX.ArcTan, _):
                    return new UnitResult(SiUnit.None, Math.Atan(Program.IntoDRG(x!.ValueNeutralized)));
                case (Type.FuncX, _, _):
                    throw new NotImplementedException(func.ToString());
                case (Type.Factorial, _, _):
                    var yield = 1;
                    for (var rem = (int)x!.ValueNeutralized; rem > 0; rem--)
                        yield *= rem;
                    return new UnitResult(SiUnit.None, yield).Normalize();
                case (Type.Root, _, _):
                    // ReSharper disable once CompareOfFloatsByEqualityOperator
                    //return x!.Root(y);
                    return new UnitResult(SiUnit.None, Math.Pow(x!.ValueNeutralized, 1 / (y?.ValueNeutralized ?? 2d)))
                        .Normalize();
                case (Type.Abs, _, _):
                    return new UnitResult(x!.Unit, Math.Abs(x.ValueNeutralized)).Normalize();
                case (Type.Op, _, Operator.Add):
                    return new UnitResult(SiUnit.None, x!.ValueNeutralized + y!.ValueNeutralized).Normalize();
                case (Type.Op, _, Operator.Subtract):
                    return new UnitResult(SiUnit.None, x!.ValueNeutralized - y!.ValueNeutralized).Normalize();
                case (Type.Op, _, Operator.Multiply):
                    return x!.Multiply(ctx!, y!).Normalize();
                case (Type.Frac, _, _):
                case (Type.Op, _, Operator.Divide):
                    return x!.Divide(ctx!, y!).Normalize();
                case (Type.Op, _, Operator.Modulus):
                    return new UnitResult(x!.Unit, x!.ValueNeutralized % y!.ValueNeutralized).Normalize();
                case (Type.Op, _, Operator.Power):
                    return new UnitResult(x!.Unit, Math.Pow(x.ValueNeutralized, y!.ValueNeutralized)).Normalize();
                case (Type.Eval, _, _):
                    if (Program.LoadFunc(arg!.ToString()!) is not { } res)
                        return new UnitResult(SiUnit.None, double.NaN).Normalize();
                    var subCtx = new MathContext(res);
                    foreach (var (key, value) in ctx!.Vars())
                        subCtx[key] = value;
                    foreach (var var in args)
                        subCtx[var.arg!.ToString()!] = var.x!;
                    return res.function!.Evaluate(subCtx);
                case (Type.Parentheses, _, _):
                    return new UnitResult(x!.Unit, x.Value).Normalize();
                case (Type.Unit, _, _):
                    var unitResult =
                        new UnitResult(arg is not string str ? x!.Unit : new SiUnit(str ?? "", ctx!.GetUnitPackages()),
                            x!.Value);
                    if (op == Operator.Modulus)
                        return unitResult.Normalize(SiPrefix.None);
                    return unitResult.Normalize();
            }

            throw new NotSupportedException(ToString());
        }

        internal SiUnit? ToUnit(MathContext? ctx)
        {
            return ctx == null || arg == null ? null : new SiUnit((string)arg!, ctx.GetUnitPackages());
        }

        public enum OutputType
        {
            Text = default,
            LaTeX
        }

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
                    return (this.op, output) switch
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
                    return $"{x}{(arg != null ? output == OutputType.LaTeX ? $"\\text{{{arg}}}" : arg.ToString() : string.Empty)}{(this.op == Operator.Modulus ? "?" : string.Empty)}";
                case Type.Equation:
                    return $"{x} {(output == OutputType.LaTeX ? "&" : "")}= {y}";
                case Type.EvalVar:
                    return arg?.ToString() ?? "null";
                default:
                    throw new ArgumentOutOfRangeException(nameof(type), type, "Unsupported type");
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

        public UnitRef? FindOutputUnit()
        {
            if (type is Type.Unit or Type.Var)
                return new UnitRef((arg as string)!);
            return x != null ? x.FindOutputUnit() : y?.FindOutputUnit();
        }
    }
}