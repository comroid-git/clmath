using System.Collections.Concurrent;

namespace clmath;

public interface IUnit
{
    static Unit Multiply(Unit a, Unit b) => a.Products[b];
    static Unit Divide(Unit a, Unit b) => a.Quotients[b];
}

public interface IUnit<TUnit> : IUnit where TUnit : IUnit
{
    TUnit Multiply(TUnit other);
    TUnit Divide(TUnit other);
}

public sealed class SiUnit : IUnit<SiUnit>
{
    public readonly SiPrefix Prefix;
    public readonly Unit Unit;

    public SiUnit(string str)
    {
        var si = SiPrefix.values.FirstOrDefault(si => str.StartsWith(si.Id));
        if (si != null)
        {
            var offset = str.IndexOf(si.Id, StringComparison.Ordinal) + si.Id.Length;
            str = str.Substring(offset, str.Length - offset);
        }

        Prefix = si ?? SiPrefix.None;
        Unit = Unit.Get(str);
    }

    private SiUnit(SiPrefix prefix, Unit unit)
    {
        Prefix = prefix;
        Unit = unit;
    }

    public SiUnit Multiply(SiUnit other)
    {
        if (Prefix != other.Prefix)
            throw new Exception("Unit Prefixes have to be equal");
        var output = IUnit<SiUnit>.Multiply(Unit, other.Unit);
        return new SiUnit(Prefix, output);
    }

    public SiUnit Divide(SiUnit other)
    {
        if (Prefix != other.Prefix)
            throw new Exception("Unit Prefixes have to be equal");
        var output = IUnit<SiUnit>.Divide(Unit, other.Unit);
        return new SiUnit(Prefix, output);
    }

    public override string ToString() => $"{Prefix}{Unit}";
}

public sealed class Unit : IUnit<Unit>
{
    public static readonly ConcurrentDictionary<string, Unit> values = new();
    public readonly ConcurrentDictionary<Unit, Unit> Products = new();
    public readonly ConcurrentDictionary<Unit, Unit> Quotients = new();
    public readonly string Id;

    private Unit(string id) => values[Id = id] = this;

    public static Unit Get(string id) => values.GetOrAdd(id, id => new Unit(id));
    
    public Unit Multiply(Unit other) => IUnit<Unit>.Multiply(this, other);
    public Unit Divide(Unit other) => IUnit<Unit>.Divide(this, other);

    public override string ToString() => Id;
}

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