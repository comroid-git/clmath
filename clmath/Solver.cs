// ReSharper disable once ArrangeNamespaceBody
namespace clmath
{
    public sealed class Solver
    {
        private readonly bool _verbose;

        public Solver(bool verbose)
        {
            _verbose = verbose;
        }

        public Component Solve(Component rhs, Component lhs, string target)
        {
            WalkComponent_Rec(ref rhs, ref lhs, target);
            if (_verbose)
                Console.WriteLine($"{lhs} = {rhs}");
            return lhs;
        }

        private void WalkComponent_Rec(ref Component rhs, ref Component lhs, string target)
        {
            if (rhs.x == null && rhs.y == null)
            {
                // reverse lhs and rhs
                (rhs, lhs) = (lhs, rhs);
                WalkComponent_Rec(ref rhs, ref lhs, target);
            }
            else
            {
                if (_verbose)
                    Console.Write($"{lhs} = {rhs}\t");
                var xCopy = rhs.x?.Copy();
                var yCopy = rhs.y?.Copy();
                // must reverse by commutativity?
                var reverse = yCopy?.GetVars().Contains(target) ?? false;
                switch (rhs.type)
                {
                    case Component.Type.Op:
                        switch (rhs.op)
                        {
                            case Component.Operator.Add:
                                lhs = new Component
                                {
                                    type = Component.Type.Op,
                                    op = Component.Operator.Subtract,
                                    x = lhs,
                                    y = reverse ? xCopy : yCopy
                                };
                                if (_verbose)
                                    Console.Write(" | -");
                                break;
                            case Component.Operator.Subtract:
                                lhs = new Component
                                {
                                    type = Component.Type.Op,
                                    op = Component.Operator.Add,
                                    x = lhs,
                                    y = reverse ? xCopy : yCopy
                                };
                                if (_verbose)
                                    Console.Write(" | +");
                                break;
                            case Component.Operator.Multiply:
                                lhs = new Component
                                {
                                    type = Component.Type.Op,
                                    op = Component.Operator.Divide,
                                    x = lhs,
                                    y = reverse ? xCopy : yCopy
                                };
                                if (_verbose)
                                    Console.Write(" | /");
                                break;
                            case Component.Operator.Divide:
                                lhs = new Component
                                {
                                    type = Component.Type.Op,
                                    op = Component.Operator.Multiply,
                                    x = lhs,
                                    y = yCopy
                                };
                                if (_verbose)
                                    Console.Write(" | *");
                                break;
                            case Component.Operator.Power:
                                lhs = new Component
                                {
                                    type = Component.Type.Root,
                                    x = lhs,
                                    y = rhs.y?.Copy() ?? new Component { type = Component.Type.Num, arg = 2d }
                                };
                                if (_verbose)
                                    Console.WriteLine(Math.Abs((double)lhs.y.arg! - 2d) < 0.001
                                        ? " | sqrt()"
                                        : $" | root[{lhs.y}]()");
                                break;
                            default:
                                throw new NotSupportedException($"It is impossible to reduce by operator {rhs.op}");
                        }

                        if (_verbose && rhs.op != Component.Operator.Power)
                            Console.WriteLine(reverse ? xCopy : yCopy);
                        rhs = reverse && rhs.op != Component.Operator.Divide ? rhs.y! : rhs.x!;
                        break;
                    case Component.Type.FuncX:
                        switch (rhs.func)
                        {
                            case Component.FuncX.Sin:
                                lhs = new Component
                                {
                                    type = Component.Type.FuncX,
                                    func = Component.FuncX.ArcSin,
                                    x = lhs
                                };
                                if (_verbose)
                                    Console.WriteLine(" | arcsin()");
                                break;
                            case Component.FuncX.Cos:
                                lhs = new Component
                                {
                                    type = Component.Type.FuncX,
                                    func = Component.FuncX.ArcCos,
                                    x = lhs
                                };
                                if (_verbose)
                                    Console.WriteLine(" | arccos()");
                                break;
                            case Component.FuncX.Tan:
                                lhs = new Component
                                {
                                    type = Component.Type.FuncX,
                                    func = Component.FuncX.ArcTan,
                                    x = lhs
                                };
                                if (_verbose)
                                    Console.WriteLine(" | arctan()");
                                break;
                            case Component.FuncX.ArcSin:
                                lhs = new Component
                                {
                                    type = Component.Type.FuncX,
                                    func = Component.FuncX.Sin,
                                    x = lhs
                                };
                                if (_verbose)
                                    Console.WriteLine(" | sin()");
                                break;
                            case Component.FuncX.ArcCos:
                                lhs = new Component
                                {
                                    type = Component.Type.FuncX,
                                    func = Component.FuncX.Cos,
                                    x = lhs
                                };
                                if (_verbose)
                                    Console.WriteLine(" | cos()");
                                break;
                            case Component.FuncX.ArcTan:
                                lhs = new Component
                                {
                                    type = Component.Type.FuncX,
                                    func = Component.FuncX.Tan,
                                    x = lhs
                                };
                                if (_verbose)
                                    Console.WriteLine(" | tan()");
                                break;
                            default:
                                throw new NotImplementedException("Function not implemented: " + rhs.func);
                        }

                        rhs = rhs.x!;
                        break;
                    case Component.Type.Root:
                        yCopy ??= new Component { type = Component.Type.Num, arg = 2d };
                        lhs = new Component
                        {
                            type = Component.Type.Op,
                            op = Component.Operator.Power,
                            x = lhs,
                            y = yCopy
                        };
                        rhs = rhs.x!;
                        if (_verbose)
                            Console.WriteLine($" | ^{yCopy}");
                        break;
                    case Component.Type.Frac:
                        lhs = new Component
                        {
                            type = Component.Type.Op,
                            op = Component.Operator.Multiply,
                            x = lhs,
                            y = yCopy
                        };
                        if (_verbose)
                            Console.Write(" | *");
                        if (_verbose && rhs.op != Component.Operator.Power)
                            Console.WriteLine(yCopy);
                        rhs = rhs.x!;
                        break;
                    case Component.Type.Parentheses:
                        rhs = rhs.x!;
                        WalkComponent_Rec(ref rhs, ref lhs, target);
                        if (_verbose)
                            Console.WriteLine(" | remove parens (WIP)");
                        break;
                    default:
                        throw new NotSupportedException($"It is impossible to reduce by operation {rhs.type}");
                }
            }

            if (!(rhs.type == Component.Type.Parentheses && rhs.x!.type == Component.Type.Var &&
                  (string)rhs.x.arg! == target)
                && !(rhs.type == Component.Type.Var && (string)rhs.arg! == target))
                WalkComponent_Rec(ref rhs, ref lhs, target);
        }
    }
}