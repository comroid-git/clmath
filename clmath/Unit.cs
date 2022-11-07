using System.Collections.Concurrent;
using System.Text.RegularExpressions;

namespace clmath;

public abstract class AbstractUnit
{
    protected abstract Unit AsUnit();
    
    public Unit Multiply(AbstractUnit other) => AsUnit().Products[other.AsUnit()];
    public Unit Divide(AbstractUnit other) => AsUnit().Quotients[other.AsUnit()];
}

public sealed class SiUnit : AbstractUnit
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

    public override string ToString() => $"{Prefix}{Unit}";
    protected override Unit AsUnit() => Unit;
}

public sealed class Unit : AbstractUnit
{
    private static readonly string DescriptorPattern = "([pq]):\\s(\\w+),(\\w+)"; 
    public static readonly ConcurrentDictionary<string, Unit> values = new();
    public readonly ConcurrentDictionary<Unit, Unit> Products = new();
    public readonly ConcurrentDictionary<Unit, Unit> Quotients = new();
    public string Id { get; }
    public string DisplayName { get; private set; }

    private Unit(string id) => values[Id = id] = this;

    public static Unit Get(string id) => values.GetOrAdd(id, id => new Unit(id));

    public override string ToString() => Id;
    protected override Unit AsUnit() => this;

    public static void Load(string file)
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
                    unit.Products[other] = result;
                    other.Products[unit] = result;
                    result.Quotients[other] = unit;
                    result.Quotients[unit] = other;
                    break;
                case "q":
                    unit.Quotients[other] = result;
                    unit.Quotients[result] = other;
                    other.Products[result] = unit;
                    result.Products[other] = unit;
                    break;
                default: throw new Exception("Invalid descriptor: " + line);
            }
        }
    }
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