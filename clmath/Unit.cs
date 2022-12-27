using System.Collections;
using System.Collections.Concurrent;
using System.ComponentModel.Design.Serialization;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using System.Transactions;
using Antlr4.Runtime;
using clmath.Antlr;
// ReSharper disable ArrangeObjectCreationWhenTypeEvident

// ReSharper disable once ArrangeNamespaceBody
namespace clmath
{
    public sealed class SiPrefix
    {
        internal static readonly List<SiPrefix> values = new();

        public static readonly SiPrefix Yocto = new("y", -24);
        public static readonly SiPrefix Zepto = new("z", -21);
        public static readonly SiPrefix Atto = new("a", -18);
        public static readonly SiPrefix Femto = new("f", -15);
        public static readonly SiPrefix Pico = new("p", -12);
        public static readonly SiPrefix Nano = new("n", -9);
        public static readonly SiPrefix Micro = new("\u00B5", -6);
        public static readonly SiPrefix Milli = new("m", -3);
        //public static readonly SiPrefix Centi = new("c", -2);
        //public static readonly SiPrefix Deci = new("d", -1);

        public static readonly SiPrefix None = new(string.Empty, 0);

        //public static readonly SiPrefix Deca = new("da", 1);
        //public static readonly SiPrefix Hecto = new("h", 2);
        public static readonly SiPrefix Kilo = new("k", 3);
        public static readonly SiPrefix Mega = new("M", 6);
        public static readonly SiPrefix Giga = new("G", 9);
        public static readonly SiPrefix Tera = new("T", 12);
        public static readonly SiPrefix Peta = new("P", 15);
        public static readonly SiPrefix Exa = new("E", 18);
        public static readonly SiPrefix Zetta = new("Z", 21);
        public static readonly SiPrefix Yotta = new("Y", 24);

        private SiPrefix(string id, int exp)
        {
            values.Add(this);
            Id = id;
            Factor = Math.Pow(10, exp);
        }

        public string Id { get; }
        public double Factor { get; }

        public double Convert(SiPrefix from, double value)
        {
            return value / Factor * from.Factor;
        }

        public override string ToString()
        {
            return Id;
        }

        public static UnitResult Normalize(AbstractUnit unit, double value, SiPrefix? preferredPrefix = null)
        {
            if (preferredPrefix != null)
                return new UnitResult(new SiUnit(preferredPrefix, unit), preferredPrefix.Convert(None, value));
            for (var i = 0; i < values.Count; i++)
            {
                var si = values[i];
                var next = i + 1 < values.Count ? values[i + 1] : null;

                if (next != null && value >= si.Factor && value < next.Factor)
                    return new UnitResult(new SiUnit(si, unit), si.Convert(None, value));
            }

            return new UnitResult(new SiUnit(None, unit), value);
        }
    }

    public sealed class UnitResult
    {
        public static readonly UnitResult Zero = new(0);
        public readonly SiUnit Unit;
        public readonly double Value;

        public UnitResult(double value) : this(SiUnit.None, value)
        {
        }

        public UnitResult(SiUnit? unit, double value)
        {
            Unit = unit ?? SiUnit.None;
            Value = value;
        }

        public UnitResult Multiply(MathContext ctx, UnitResult right)
        {
            var lhs = SiPrefix.None.Convert(Unit.Prefix, Value);
            var rhs = SiPrefix.None.Convert(right.Unit.Prefix, right.Value);
            var evl = Unit.AsUnit(ctx)[right.Unit, Component.Operator.Multiply];
            var res = evl.Evaluate(lhs, rhs);
            return new UnitResult(new SiUnit(SiPrefix.None, evl.output), res);
        }

        public UnitResult Divide(MathContext ctx, UnitResult right)
        {
            var lhs = SiPrefix.None.Convert(Unit.Prefix, Value);
            var rhs = SiPrefix.None.Convert(right.Unit.Prefix, right.Value);
            var evl = Unit.AsUnit(ctx)[right.Unit, Component.Operator.Divide];
            var res = evl.Evaluate(lhs, rhs);
            return new UnitResult(new SiUnit(SiPrefix.None, evl.output), res);
        }

        public UnitResult Root(UnitResult? right)
        {
            right ??= new UnitResult(2);
            var lhs = SiPrefix.None.Convert(Unit.Prefix, Value);
            var rhs = SiPrefix.None.Convert(right.Unit.Prefix, right.Value);
            var evl = Unit.AsUnit()[right.Unit, Component.Operator.Modulus];
            var res = evl.Evaluate(lhs, rhs);
            return new UnitResult(new SiUnit(SiPrefix.None, evl.output), res);
        }

        public UnitResult Normalize(SiPrefix? preferredPrefix = null)
        {
            return SiPrefix.Normalize(Unit.Unit, SiPrefix.None.Convert(Unit.Prefix, Value), preferredPrefix);
        }

        public double As(SiPrefix? prefix = null) => Normalize(prefix ?? SiPrefix.None).Value;

        public override string ToString()
        {
            return Value.ToString("0." + new string('#', 14), CultureInfo.InvariantCulture) +
                   (Unit.ToString() == string.Empty ? string.Empty : $"{Unit}");
        }
    }

    public abstract class AbstractUnit
    {
        public abstract string Repr { get; }
        
        protected internal abstract Unit AsUnit(MathContext? ctx = null);

        private AbstractUnit? FindCommonUnit(AbstractUnit other, MathContext? ctx = null)
        {
            var it = AsUnit(ctx);
            var ot = other.AsUnit(ctx);
            return (it.Repr == string.Empty, ot.Repr == string.Empty) switch
            {
                (true, true) => Unit.None,
                (false, true) => it,
                (true, false) => ot,
                (_, _) => null
            };
        }

        public override int GetHashCode() => Repr.GetHashCode();

        public override bool Equals(object? obj) => obj is AbstractUnit other && Repr == other.Repr;
        
        public override string ToString() => Repr;
    }

    public sealed class SiUnit : AbstractUnit
    {
        public static readonly SiUnit None = new(SiPrefix.None, clmath.Unit.None);
        public readonly SiPrefix Prefix;
        public readonly AbstractUnit Unit;

        public SiUnit(string str, params UnitPackage[] packages)
        {
            var unit = packages.SelectMany(pkg => pkg.values.Values).FirstOrDefault(unit => unit.Repr == str);
            SiPrefix? si = null;
            if (unit == null)
            {
                si = SiPrefix.values.FirstOrDefault(prefix => prefix.Id.Length > 0 && str.StartsWith(prefix.Id));
                if (si != null)
                {
                    var offset = str.IndexOf(si.Id, StringComparison.Ordinal) + si.Id.Length;
                    str = str.Substring(offset, str.Length - offset);
                }
            }

            Prefix = si ?? SiPrefix.None;
            Unit = unit
                   ?? packages.SelectMany(pkg => pkg.values.Values).FirstOrDefault(unit => unit.Repr == str)
                   ?? clmath.Unit.None;
        }

        internal SiUnit(SiPrefix prefix, AbstractUnit unit)
        {
            Prefix = prefix;
            Unit = unit;
        }

        public override string ToString()
        {
            return $"{Prefix}{Unit}";
        }

        public override string Repr => Unit.Repr;
        protected internal override Unit AsUnit(MathContext? ctx = null) => Unit.AsUnit(ctx);
    }

    public sealed class Unit : AbstractUnit
    {
        internal bool Finalized = false;
        public readonly UnitPackage Package;
        public static readonly Unit None = new(UnitPackage.None, string.Empty, string.Empty);
        private readonly ConcurrentDictionary<(string repr, Component.Operator op), UnitEvaluator> Evaluators = new();

        internal Unit(UnitPackage package, string name, string repr)
        {
            Package = package;
            Name = name;
            _repr = repr;
        }

        internal UnitEvaluator this[AbstractUnit other, Component.Operator op]
        {
            get => Evaluators.GetValueOrDefault((other.Repr, op)) ?? new UnitEvaluator(new UnitRef(other.Repr), op);
            set => Evaluators[(other.Repr, op)] = value;
        }

        public string Name { get; internal set; }
        public override string Repr => _repr;
        internal string _repr;

        protected internal override Unit AsUnit(MathContext? ctx = null) => this;

        public IEnumerable<(UnitRef other, Component.Operator op, UnitEvaluator eval)> GetEvaluators() => Evaluators
            .Select((entry) => (new UnitRef(entry.Key.repr), entry.Key.op, entry.Value));
    }

    public sealed class UnitRef : AbstractUnit
    {
        private static readonly HashSet<UnitRef> cache = new HashSet<UnitRef>();
        public UnitRef(string repr)
        {
            _repr = repr;
            cache.Add(this);
        }

        public override string Repr => _repr;
        private string _repr;

        protected internal override Unit AsUnit(MathContext? ctx = null)
        {
            if (string.IsNullOrEmpty(Repr))
                return Unit.None;
            var result = (ctx ?? throw new ArgumentNullException(nameof(ctx), "Context may not be null for UnitRef"))
                .GetUnitPackages()
                .SelectMany(pkg => pkg.values)
                .Where(unit => unit.Key == Repr)
                .Select(entry => entry.Value)
                .FirstOrDefault();
            if (result == null)
                throw new FileNotFoundException($"Unit {Repr} was not found");
            return result;
        }

        public override string ToString() => Repr;

        internal static void NotifyNameChange(string old, string id)
        {
            foreach (var unit in cache.Where(x => x.Repr == old))
                unit._repr = id;
        }
    }

    public sealed class UnitEvaluator
    {
        public readonly AbstractUnit output;
        public readonly Component.Operator? op;
        public readonly double? overrideY;

        internal UnitEvaluator(AbstractUnit output, Component.Operator? op = null, double? overrideY = null)
        {
            this.output = output;
            this.op = op;
            this.overrideY = overrideY;
        }

        public double Evaluate(double x, double y) => op!.Value.Evaluate(x, overrideY ?? y);
    }

    public sealed class UnitPackage
    {
        public static readonly UnitPackage None = new(string.Empty);
        public readonly ConcurrentDictionary<string, Unit> values = new();

        public UnitPackage(string name)
        {
            Name = name;
        }

        public string Name { get; }

        public AbstractUnit Get(string id)
        {
            return (AbstractUnit?) values!.GetValueOrDefault(id, null) ?? new UnitRef(id);
        }

        public Unit CreateOrGet(string name, string id) => values.GetOrAdd(id, (_) => new(this, name, id));

        internal class UnitLoadException : Exception {}
        
        public void Load(string file)
        {
            if (!File.Exists(file))
                throw new Exception($"Unit descriptor file {file} does not exist");
            var fName = new FileInfo(file).Name;
            var uName = fName.Substring(0, fName.IndexOf(Program.UnitExt, StringComparison.Ordinal));
            Unit unit = null!;
            using var fs = File.OpenRead(file);
            int o = 0;
            byte[] buf = new byte[4];
            while (o <= 3 && fs.Read(buf, 0, buf.Length) != -1) switch (o++)
            {
                case 0:
                    // reserved 4 bytes
                    if (buf.Any(b => b != 0))
                    { // unit file needs migration
                        values.TryRemove(uName, out _);
                        throw new UnitLoadException();
                    }
                    break;
                case 1:
                    // repr len
                    var len = BitConverter.ToInt32(buf);
                    buf = new byte[len];
                    break;
                case 2:
                    // repr
                    unit = CreateOrGet(uName, Encoding.ASCII.GetString(buf));
                    buf = new byte[1];
                    break;
                case 3:
                    if (buf[0] == '\r' && fs.Read(buf, 0, 1) != 1)
                        throw new Exception("Invalid End of Line");
                    if (buf[0] != '\n') 
                        throw new Exception("Linefeed expected");
                    break;
            }

            if (unit == null)
                throw new Exception("Assertion failure");
            ParseEquations(new AntlrInputStream(fs), unit);
        }

        internal void ParseEquations(AntlrInputStream ais, Unit unit)
        {
            var lex = new MathLexer(ais);
            var ats = new CommonTokenStream(lex);
            var par = new MathParser(ats);

            foreach (var equation in new MathCompiler().VisitUnitFile(par.unitFile()))
            {
                if (equation is not { type: Component.Type.Equation }
                    || equation.x is not { type: Component.Type.Op or Component.Type.Root or Component.Type.FuncX } lhs
                    || equation.y is not { type: Component.Type.Var } rhs)
                    throw new Exception("Malformed equation: equation must be implicitly 'x(..) = y'");
                var result = rhs.FindOutputUnit()!;
                var srcIsX = lhs.x is { type: Component.Type.Var } && (string)lhs.x.arg! == unit.Repr;
                var input = srcIsX ? lhs.y : lhs.x;
                var other = input?.FindOutputUnit();
                if (input?.type == Component.Type.Num)
                    unit[Unit.None, Component.Operator.Multiply] = 
                        new UnitEvaluator(result, Component.Operator.Multiply, overrideY: (double)input.arg!);
                else if (other != null && lhs.op is Component.Operator.Multiply or Component.Operator.Divide)
                    unit[other, lhs.op!.Value] = new UnitEvaluator(result, lhs.op);
                else throw new Exception("Malformed equation: Only Multiply or Divide operators are allowed");
            }
        }

        public static void AddEval(Unit inputA, Unit inputB, Unit output, Component.Operator op, double? overrideY = null) 
            => inputA[inputB, op] = new UnitEvaluator(output, op, overrideY);
        public void Finalize(MathContext ctx)
        {
            foreach (var inputA in values.Values)
            {
                if (inputA.Finalized)
                    continue;
                foreach (var (other, op, eval) in inputA.GetEvaluators())
                {
                    var inputB = other?.AsUnit(ctx);
                    var output = eval.output.AsUnit(ctx);
                    switch (op)
                    {
                        case Component.Operator.Multiply:
                            if (inputB is {Repr: not ""})
                            {
                                AddEval(inputB, inputA, output, Component.Operator.Multiply);
                                AddEval(output, inputA, inputB, Component.Operator.Divide);
                                AddEval(output, inputB, inputA, Component.Operator.Divide);
                            }
                            else AddEval(output, Unit.None, inputA, Component.Operator.Divide, eval.overrideY);
                            break;
                        case Component.Operator.Divide:
                            if (inputB is {Repr: not ""})
                            {
                                AddEval(inputA, output, inputB, Component.Operator.Divide);
                                AddEval(inputB, output, inputA, Component.Operator.Multiply);
                                AddEval(output, inputB, inputA, Component.Operator.Multiply);
                            }
                            else AddEval(output, Unit.None, inputA, Component.Operator.Multiply, eval.overrideY);
                            break;
                        default:
                            throw new ArgumentOutOfRangeException(nameof(eval.op));
                    }
                }
                inputA.Finalized = true;
            }
        }

        public override string ToString()
        {
            return Name;
        }
    }
}