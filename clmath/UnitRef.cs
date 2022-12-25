using System.Collections.Concurrent;
using System.Globalization;
using System.Text.RegularExpressions;

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

        public static UnitResult operator *(UnitResult left, UnitResult right)
        {
            var lhs = SiPrefix.None.Convert(left.Unit.Prefix, left.Value);
            var rhs = SiPrefix.None.Convert(right.Unit.Prefix, right.Value);
            var outputUnit = left.Unit.Multiply(right.Unit);
            return SiPrefix.Normalize(outputUnit, lhs * rhs);
        }

        public static UnitResult operator /(UnitResult left, UnitResult right)
        {
            var lhs = SiPrefix.None.Convert(left.Unit.Prefix, left.Value);
            var rhs = SiPrefix.None.Convert(right.Unit.Prefix, right.Value);
            var outputUnit = left.Unit.Divide(right.Unit);
            return SiPrefix.Normalize(outputUnit, lhs / rhs);
        }

        public UnitResult Normalize(SiPrefix? preferredPrefix = null)
        {
            return SiPrefix.Normalize(Unit.Unit, SiPrefix.None.Convert(Unit.Prefix, Value), preferredPrefix);
        }

        public override string ToString()
        {
            return Value.ToString(CultureInfo.InvariantCulture) +
                   (Unit.ToString() == string.Empty ? string.Empty : $":{Unit}");
        }
    }

    public abstract class AbstractUnit
    {
        protected internal abstract Unit AsUnit(MathContext? ctx = null);

        public AbstractUnit Multiply(AbstractUnit other, MathContext? ctx = null) => FindCommonUnit(other) ?? AsUnit(ctx).Products[other.AsUnit(ctx)];

        public AbstractUnit Divide(AbstractUnit other, MathContext? ctx = null) => FindCommonUnit(other) ?? AsUnit(ctx).Quotients[other.AsUnit(ctx)];

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

        protected internal override Unit AsUnit(MathContext? ctx = null) => Unit.AsUnit(ctx);
    }

    public sealed class Unit : AbstractUnit
    {
        public static readonly Unit None = new(UnitPackage.None, string.Empty);
        public readonly ConcurrentDictionary<Unit, AbstractUnit> Products = new();
        public readonly ConcurrentDictionary<Unit, AbstractUnit> Quotients = new();

        internal Unit(UnitPackage package, string name)
        {
            Name = name;
        }

        public string Name { get; }
        public string Repr { get; private set; } = null!;

        internal void SetRepr(string repr) => Repr ??= repr;

        public override string ToString()
        {
            return Repr;
        }

        public Unit Resolve(MathContext ctx) => this;

        protected internal override Unit AsUnit(MathContext? ctx = null) => this;

        public void AddProduct(Unit other, Unit result)
        {
            Products[other] = result;
            other.Products[this] = result;
            result.Quotients[this] = other;
            result.Quotients[other] = this;
        }

        public void AddQuotient(Unit other, Unit result)
        {
            Quotients[other] = result;
            Quotients[result] = other;
            other.Products[result] = this;
            result.Products[other] = this;
        }
    }

    public sealed class UnitRef : AbstractUnit
    {
        private readonly string _repr;

        public UnitRef(string repr) => _repr = repr;

        protected internal override Unit AsUnit(MathContext? ctx = null)
        {
            return (ctx ?? throw new ArgumentNullException(nameof(ctx), "Context may not be null for UnitRef"))
                   .GetUnitPackages()
                   .SelectMany(pkg => pkg.values)
                   .Where(unit => unit.Key == _repr)
                   .Select(entry => entry.Value)
                   .FirstOrDefault()
                   ?? throw new FileNotFoundException($"Unit {_repr} was not found");
        }

        public override string ToString() => _repr;
    }

    public sealed class UnitPackage
    {
        public static readonly UnitPackage None = new(string.Empty);
        private static readonly string DescriptorPattern = "[\\w\\d]+(\\/|\\*)(\\w+)=(\\w+)";
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

        public Unit CreateOrGet(string name, string? id)
        {
            Unit Factory(string? _ = null) => new(this, name);
            return id == null ? Factory() : values.GetOrAdd(id, Factory);
        }

        private struct UnitCandidate
        {
            internal Unit unit;
            internal readonly Dictionary<AbstractUnit, AbstractUnit> productCandidates = new();
            internal readonly Dictionary<AbstractUnit, AbstractUnit> quotientCandidates = new();

            public UnitCandidate(Unit unit)
            {
                this.unit = unit;
            }
        }

        private List<UnitCandidate> _candidates = new();

        public void Load(string file)
        {
            if (!File.Exists(file))
                throw new Exception($"Unit descriptor file {file} does not exist");
            var fName = new FileInfo(file).Name;
            var uName = fName.Substring(0, fName.IndexOf(Program.UnitExt, StringComparison.Ordinal));
            var unit = CreateOrGet(uName, null);
            var candidate = new UnitCandidate(unit);
            _candidates.Add(candidate);
            var i = 0;
            foreach (var line in File.ReadLines(file))
            {
                if (i++ == 0)
                {
                    unit.SetRepr(line);
                    continue;
                }

                if (Regex.Match(line, DescriptorPattern) is not { Success: true } match)
                    throw new Exception("Invalid descriptor: " + line);
                var other = Get(match.Groups[2].Value);
                var result = Get(match.Groups[3].Value);
                switch (match.Groups[1].Value)
                {
                    case "*":
                        candidate.productCandidates[other] = result;
                        break;
                    case "/":
                        candidate.quotientCandidates[other] = result;
                        break;
                    default: throw new Exception("Invalid descriptor: " + line);
                }
            }
        }

        public void Finalize(MathContext ctx)
        {
            if (_candidates == null)
                throw new Exception("Already finalized");
            foreach (var candidate in _candidates)
            {
                foreach (var (multiplier, product) in candidate.productCandidates)
                    candidate.unit.AddProduct(multiplier.AsUnit(ctx), product.AsUnit(ctx));
                foreach (var (dividend, quotient) in candidate.quotientCandidates)
                    candidate.unit.AddProduct(dividend.AsUnit(ctx), quotient.AsUnit(ctx));
                candidate.quotientCandidates.Clear();
                candidate.productCandidates.Clear();
            }
            _candidates.Clear();
            _candidates = null!;
        }

        public override string ToString()
        {
            return Name;
        }
    }
}