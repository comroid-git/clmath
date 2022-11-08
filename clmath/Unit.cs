using System.Collections.Concurrent;
using System.Globalization;
using System.Text.RegularExpressions;

namespace clmath;

public sealed class SiPrefix
{
    internal static readonly HashSet<SiPrefix> values = new();

    public static readonly SiPrefix Yocto = new("y", -24);
    public static readonly SiPrefix Zepto = new("z", -21);
    public static readonly SiPrefix Atto = new("a", -18);
    public static readonly SiPrefix Femto = new("f", -15);
    public static readonly SiPrefix Pico = new("p", -12);
    public static readonly SiPrefix Nano = new("n", -9);
    public static readonly SiPrefix Micro = new("μ", -6);
    public static readonly SiPrefix Milli = new("m", -3);
    public static readonly SiPrefix Centi = new("c", -2);
    public static readonly SiPrefix Deci = new("d", -1);
    
    public static readonly SiPrefix None = new(string.Empty, 0);
    
    public static readonly SiPrefix Deca = new("da", 1);
    public static readonly SiPrefix Hecto = new("h", 2);
    public static readonly SiPrefix Kilo = new("k", 3);
    public static readonly SiPrefix Mega = new("M", 6);
    public static readonly SiPrefix Giga = new("G", 9);
    public static readonly SiPrefix Tera = new("T", 12);
    public static readonly SiPrefix Peta = new("P", 15);
    public static readonly SiPrefix Exa = new("E", 18);
    public static readonly SiPrefix Zetta = new("Z", 21);
    public static readonly SiPrefix Yotta = new("Y", 24);

    public string Id { get; }
    public double Factor { get; }

    private SiPrefix(string id, int exp)
    {
        if (id != string.Empty)
            values.Add(this);
        Id = id;
        Factor = Math.Pow(10, exp);
    }

    public double Convert(SiPrefix target, double value) => value / Factor * target.Factor;

    public override string ToString() => Id;
}

public sealed class UnitResult
{
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
        var si1 = right.Unit.Prefix;
        var value = left.Unit.Prefix.Convert(si1, left.Value);
        value *= right.Unit.Prefix.Convert(si1, right.Value);
        var unit = new SiUnit(si1, left.Unit.Multiply(right.Unit));
        //todo find good common unit prefix and convert to it
        var si2 = si1;
        return new UnitResult(unit, si1.Convert(si2, value));
    }

    public static UnitResult operator /(UnitResult left, UnitResult right)
    {
        var si1 = right.Unit.Prefix;
        var value = left.Unit.Prefix.Convert(si1, left.Value);
        value /= right.Unit.Prefix.Convert(si1, right.Value);
        var unit = new SiUnit(si1, left.Unit.Divide(right.Unit));
        //todo find good common unit prefix and convert to it
        var si2 = si1;
        return new UnitResult(unit, si1.Convert(si2, value));
    }

    public override string ToString() => Value.ToString(CultureInfo.InvariantCulture) +
                                         (Unit.ToString() == string.Empty ? string.Empty : $"[{Unit}]");
}

public abstract class AbstractUnit
{
    protected abstract Unit AsUnit();
    
    public Unit Multiply(AbstractUnit other) => AsUnit() == Unit.None ? Unit.None : AsUnit().Products[other.AsUnit()];
    public Unit Divide(AbstractUnit other) => AsUnit() == Unit.None ? Unit.None : AsUnit().Quotients[other.AsUnit()];
}

public sealed class SiUnit : AbstractUnit
{
    public static readonly SiUnit None = new(SiPrefix.None, Unit.None);
    public readonly SiPrefix Prefix;
    public readonly Unit Unit;

    public SiUnit(string str, params UnitPackage[] packages)
    {
        var si = SiPrefix.values.FirstOrDefault(si => str.StartsWith(si.Id));
        if (si != null)
        {
            var offset = str.IndexOf(si.Id, StringComparison.Ordinal) + si.Id.Length;
            str = str.Substring(offset, str.Length - offset);
        }

        Prefix = si ?? SiPrefix.None;
        Unit = packages.SelectMany(pkg => pkg.values.Values).FirstOrDefault(unit => unit.Id == str)
               ?? throw new Exception("No unit found with identifier " + str);
    }

    internal SiUnit(SiPrefix prefix, Unit unit)
    {
        Prefix = prefix;
        Unit = unit;
    }

    public override string ToString() => $"{Prefix}{Unit}";
    protected override Unit AsUnit() => Unit;
}

public sealed class Unit : AbstractUnit
{
    public static readonly Unit None = new(UnitPackage.None, string.Empty);
    public readonly ConcurrentDictionary<Unit, Unit> Products = new();
    public readonly ConcurrentDictionary<Unit, Unit> Quotients = new();
    public string Id { get; }
    public string DisplayName { get; internal set; }

    internal Unit(UnitPackage package, string id) => package.values[Id = id] = this;

    public override string ToString() => Id;
    protected override Unit AsUnit() => this;

    public void AddProduct(Unit other, Unit result)
    {
        this.Products[other] = result;
        other.Products[this] = result;
        result.Quotients[this] = other;
        result.Quotients[other] = this;
    }

    public void AddQuotient(Unit other, Unit result)
    {
        this.Quotients[other] = result;
        this.Quotients[result] = other;
        other.Products[result] = this;
        result.Products[other] = this;
    }
}

public sealed class UnitPackage
{
    public static readonly UnitPackage None = new(string.Empty);
    public string Name { get; }
    private static readonly string DescriptorPattern = "([pq]):\\s(\\w+),(\\w+)";
    public readonly ConcurrentDictionary<string, Unit> values = new();

    public UnitPackage(string name)
    {
        Name = name;
    }

    public Unit Get(string id) => values.GetOrAdd(id, id => new Unit(this, id));

    public void Load(string file)
    {
        if (!File.Exists(file))
            throw new Exception($"Unit descriptor file {file} does not exist");
        var fName = new FileInfo(file).Name;
        var uName = fName.Substring(0, fName.IndexOf(Program.UnitExt, StringComparison.Ordinal));
        var unit = Get(uName);
        var i = 0;
        foreach (var line in File.ReadLines(file))
        {
            if (i++ == 0)
            {
                unit.DisplayName = line;
                continue;
            }
            if (Regex.Match(line, DescriptorPattern) is not { Success: true } match)
                throw new Exception("Invalid descriptor: " + line);
            var other = Get(match.Groups[2].Value);
            var result = Get(match.Groups[3].Value);
            switch (match.Groups[1].Value)
            {
                case "p":
                    unit.AddProduct(other, result);
                    break;
                case "q":
                    unit.AddQuotient(other, result);
                    break;
                default: throw new Exception("Invalid descriptor: " + line);
            }
        }
    }

    public override string ToString() => Name;
}