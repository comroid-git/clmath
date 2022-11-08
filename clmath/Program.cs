using System.Collections.Concurrent;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Antlr4.Runtime;
using clmath.Antlr;

namespace clmath;

public static class Program
{
    private const double factorD2R = Math.PI / 180;
    private const double factorD2G = 1.111111111;
    private static readonly string FuncExt = ".math";
    private static readonly string ConstExt = ".vars";
    internal static readonly string UnitExt = ".unit";
    internal static readonly string UnitPackExt = ".units";

    private static readonly string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
        "comroid", "clmath");

    private static readonly string constantsFile = Path.Combine(dir, "constants" + ConstExt);
    private static readonly string configFile = Path.Combine(dir, "config.bin");

    private static bool _exiting;
    private static bool _dropAll;
    private static Graph? _graph;

    private static readonly Dictionary<string, double> globalConstants = new()
    {
        { "pi", Math.PI },
        { "e", Math.E },
        { "tau", Math.Tau },
        { "rng_i", double.NaN },
        { "rng_d", double.NaN }
    };
    internal static Dictionary<string, double> constants { get; private set; } = null!;
    private static readonly Stack<(Component func, MathContext ctx)> stash = new();
    internal static readonly ConcurrentDictionary<string, UnitPackage> unitPackages = new();
    private static readonly List<string> enabledUnitPacks = new();

    static Program()
    {
        SetUp();
        if (!Directory.Exists(dir))
            Directory.CreateDirectory(dir);
        if (!File.Exists(configFile))
            SaveConfig();
        if (!File.Exists(constantsFile))
            SaveConstants(new Dictionary<string, double>());
        if (!LoadConfig())
        {
            File.Delete(configFile);
            SaveConfig();
        } 
        LoadConstants();
        LoadUnits();
    }

    public static CalcMode DRG { get; set; } = CalcMode.Deg;
    internal static bool AutoEval { get; set; } = true;

    public static void SetUp() => CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;

    private static void SaveConstants(Dictionary<string, double>? values = null)
    {
        values ??= constants;
        File.WriteAllText(constantsFile, ConvertValuesToString(values, globalConstants.ContainsKey));
    }

    private static void LoadConstants()
    {
        // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
        if (constants == null)
            constants = new Dictionary<string, double>();
        else constants.Clear();
        foreach (var (key, value) in globalConstants)
            constants[key] = value;
        foreach (var (key, value) in ConvertValuesFromString(File.ReadAllText(constantsFile)))
            constants[key] = value.Evaluate(null).Value; // todo: use UnitResult in constants?
    }

    private static void LoadUnits()
    {
        foreach (var pkg in Directory.EnumerateDirectories(dir, $"*{UnitPackExt}"))
        {
            var packageName = new DirectoryInfo(pkg).Name.StripExtension(UnitPackExt);
            var package = new UnitPackage(packageName);
            foreach (var unitFile in Directory.EnumerateFiles(pkg, $"*{UnitExt}"))
                package.Load(unitFile);
            unitPackages[packageName] = package;
        }
    }
    
    private const int ConfigVersion = 10;

    private static void SaveConfig()
    {
        using var fs = File.OpenWrite(configFile);
        fs.Write(BitConverter.GetBytes(ConfigVersion));
        fs.Write(new[] { (byte)DRG });
        fs.Write(BitConverter.GetBytes(AutoEval));
        fs.Write(BitConverter.GetBytes(enabledUnitPacks.Count));
        foreach (var pack in enabledUnitPacks)
        {
            var buffer = Encoding.ASCII.GetBytes(pack);
            fs.Write(BitConverter.GetBytes(buffer.Length));
            fs.Write(buffer);
        }
    }

    private static bool LoadConfig()
    {
        using var fs = File.OpenRead(configFile);
        if (ConfigVersion != BitConverter.ToInt32(Read(fs, sizeof(int))))
            return false;
        DRG = (CalcMode)Read(fs, 1)[0];
        AutoEval = BitConverter.ToBoolean(Read(fs, sizeof(bool)));
        var enabledPackCount = BitConverter.ToInt32(Read(fs, sizeof(int)));
        for (; enabledPackCount > 0; enabledPackCount--)
        {
            var len = BitConverter.ToInt32(Read(fs, sizeof(int)));
            enabledUnitPacks.Add(Encoding.ASCII.GetString(Read(fs, len)));
        }
        return true;
    }

    private static byte[] Read(Stream s, int len)
    {
        var buf = new byte[len];
        if (len != s.Read(buf, 0, len))
            return new byte[len];
        return buf;
    }

    private static string ConvertValuesToString(Dictionary<string, Component> values, Func<string, bool>? skip = null)
    {
        skip ??= _ => false;
        var txt = string.Empty;
        foreach (var (key, value) in values)
            if (!skip(key))
                txt += $"{key} = {value}\n";
        return txt;
    }

    private static string ConvertValuesToString(Dictionary<string, double> values, Func<string, bool>? skip = null)
    {
        skip ??= _ => false;
        var txt = string.Empty;
        foreach (var (key, value) in values)
            if (!skip(key))
                txt += $"{key} = {value}\n";
        return txt;
    }

    private static Dictionary<string, Component> ConvertValuesFromString(string data)
    {
        Dictionary<string, Component> vars = new();
        foreach (var (key, value) in data.Replace("\r\n", "\n").Split("\n")
                     .Select(ConvertValueFromString)
                     .Where(e => e.HasValue)
                     .Select(e => e!.Value))
            vars[key] = value;
        return vars;
    }

    private static (string key, Component value)? ConvertValueFromString(string data)
    {
        if (Regex.Match(data, "([\\w]+)\\s*=\\s*(.+)") is not { Success: true } matcher)
            return null;
        var key = matcher.Groups[1].Value;
        var value = ParseFunc(matcher.Groups[2].Value);
        return (key, value);
    }

    public static void Main(params string[] args)
    {
        if (args.Length == 0)
        {
            StdIoMode();
        }
        else
        {
            if (args[0] == "graph")
            {
                StartGraph(CreateArgsFuncs(1, args));
            }
            else if (args[0] == "solve")
            {
                var func = CreateArgsFuncs(3, args)[0];
                CmdSolve(new[] { "solve", args[1], args[2] /*, "-v"*/ },
                    new Component { type = Component.Type.Var, arg = args[2] }, func.fx, func.ctx);
            }
            else
            {
                var arg = string.Join(" ", args);
                if (File.Exists(arg))
                    EvalFunc(File.ReadAllText(arg));
                else EvalFunc(arg);
                Console.WriteLine("Press any key to exit...");
                Console.ReadLine();
            }
        }

        SaveConfig();
        _exiting = false;
    }

    private static string CleanupString(string str)
    {
        var leadingSpaces = 0;
        for (var i = 0; i < str.Length && str[i] == ' '; i++)
            leadingSpaces++;
        return str.Substring(leadingSpaces, str.Length - leadingSpaces);
    }

    private static void StdIoMode()
    {
        while (!_exiting)
        {
            _dropAll = false;
            Console.Title = $"[{DRG}] clmath";
            Console.Write("math> ");
            var func = Console.ReadLine()!;
            func = CleanupString(func);
            var cmds = func.Split(" ");

            switch (cmds[0])
            {
                case "": break;
                case "exit": return;
                case "help":
                    Console.WriteLine($"clmath v{typeof(Program).Assembly.GetName().Version} by comroid\n");
                    Console.WriteLine("Available commands:");
                    Console.WriteLine("\thelp\t\t\t\tShows this text");
                    Console.WriteLine("\texit\t\t\t\tCloses the program");
                    Console.WriteLine("\tset <const>\t\t\tDefines a constant");
                    Console.WriteLine("\tunset <const>\t\t\tRemoves a constant");
                    Console.WriteLine("\tlist <target>\t\t\tLists things");
                    Console.WriteLine("\tenable <target>\t\t\tEnables the specified unit package");
                    Console.WriteLine("\tdisable <target>\t\t\tDisables the specified unit package");
                    Console.WriteLine("\tload <name>\t\t\tLoads function with the given name");
                    Console.WriteLine("\tmv <n0> <n1>\t\t\tRename function with the given name");
                    Console.WriteLine("\tdelete <name>\t\t\tDeletes function with the given name");
                    Console.WriteLine("\trestore <trace>\t\t\tRestores a function from stash");
                    Console.WriteLine("\tclear <target>\t\t\tClears the desired target");
                    Console.WriteLine("\tmode <D/R/G>\t\t\tSets the mode to Deg/Rad/Grad");
                    Console.WriteLine("\tsolve <var> <lhs> <func>\tSets the mode to Deg/Rad/Grad");
                    Console.WriteLine("\tgraph <func..>\t\t\tDisplays function/s in a 2D graph");
                    Console.WriteLine("\nEnter a function to start evaluating");
                    break;
                case "set":
                    CmdSet(func, cmds);
                    break;
                case "unset":
                    CmdUnset(cmds);
                    break;
                case "list":
                    CmdList(cmds);
                    break;
                case "enable" or "disable":
                    CmdToggleState(cmds, cmds[0] == "enable");
                    break;
                case "load":
                    CmdLoad(cmds);
                    break;
                case "mv" or "rename":
                    CmdMove(cmds);
                    break;
                case "rm" or "delete":
                    CmdDelete(cmds);
                    break;
                case "restore":
                    CmdRestore(cmds);
                    break;
                case "clear":
                    CmdClearTarget(cmds);
                    break;
                case "mode":
                    CmdMode(cmds);
                    break;
                case "solve":
                    var f = CreateArgsFuncs(3, cmds)[0];
                    CmdSolve(cmds, null, f.fx, f.ctx);
                    break;
                case "graph":
                    StartGraph(cmds.Length == 1 ? stash.ToArray() : CreateArgsFuncs(1, cmds));
                    break;
                default:
                    EvalFunc(func);
                    break;
            }
        }
    }

    private static (Component fx, MathContext ctx)[] CreateArgsFuncs(int start, params string[] args)
    {
        return args.ToList()
            .GetRange(start, args.Length - start)
            .Select(ParseFunc)
            .Select(fx => (fx, new MathContext(){enabledUnitPacks = enabledUnitPacks}))
            .ToArray();
    }

    internal static (Component func, MathContext ctx)? LoadFunc(string name)
    {
        var path = Path.Combine(dir, name + FuncExt);
        if (!File.Exists(path))
        {
            Console.WriteLine($"Function with name {name} not found");
            return null;
        }

        var data = File.ReadAllText(path);
        var lnb = data.IndexOf("\n", StringComparison.Ordinal);
        MathContext ctx;
        if (lnb != -1)
        {
            var vars = ConvertValuesFromString(data.Substring(lnb + 1, data.Length - lnb - 2));
            ctx = new MathContext(vars, enabledUnitPacks);
        }
        else
        {
            ctx = new MathContext(){enabledUnitPacks = enabledUnitPacks};
        }

        return (ParseFunc(lnb == -1 ? data : data.Substring(0, lnb)), ctx);
    }

    private static bool IsInvalidArgumentCount(string[] arr, int min)
    {
        if (arr.Length < min)
        {
            Console.WriteLine(
                $"Error: Not enough arguments; '{arr[0]}' requires at least {min - 1} argument{(min == 2 ? string.Empty : "s")}");
            return true;
        }

        return false;
    }

    private static Component ParseFunc(string f)
    {
        var input = new AntlrInputStream(f);
        var lexer = new MathLexer(input);
        var tokens = new CommonTokenStream(lexer);
        var parser = new MathParser(tokens);
        return new MathCompiler().Visit(parser.expr());
    }

    private static void EvalFunc(string f)
    {
        var fx = ParseFunc(f);
        EvalFunc(fx, fx.ToString());
    }

    private static void EvalFunc(Component func, string? f = null, MathContext? ctx = null)
    {
        if (func.EnumerateVars().Distinct().All(constants.ContainsKey))
        {
            var res = func.Evaluate(new MathContext(){enabledUnitPacks = enabledUnitPacks});
            PrintResult(func, res);
        }
        else
        {
            // enter editor mode
            ctx ??= new MathContext(){enabledUnitPacks = enabledUnitPacks};
            while (true)
            {
                if (_exiting || _dropAll)
                    return;
                Console.Title = $"[{DRG}] {func}";
                Console.Write($"{func}> ");
                var cmd = Console.ReadLine()!;
                cmd = CleanupString(cmd);

                if (ConvertValueFromString(cmd) is { } result)
                {
                    var key = result.key;
                    var value = result.value;
                    if (value.EnumerateVars().Contains(key))
                        Console.WriteLine($"Error: Variable {key} cannot use itself");
                    else if (constants.ContainsKey(key))
                        Console.WriteLine($"Error: Cannot redefine {key}");
                    else ctx.var[key] = value;

                    if (AutoEval && FindMissingVariables(func, ctx).Count == 0)
                        PrintResult(func, func.Evaluate(ctx));
                }
                else
                {
                    var cmds = cmd.Split(" ");
                    switch (cmds[0])
                    {
                        case "drop":
                            _dropAll = cmds[^1] == "all";
                            return;
                        case "exit":
                            _exiting = true;
                            return;
                        case "help":
                            Console.WriteLine($"clmath v{typeof(Program).Assembly.GetName().Version} by comroid\n");
                            Console.WriteLine("Available commands:");
                            Console.WriteLine("\thelp\t\t\t\tShows this text");
                            Console.WriteLine("\texit\t\t\t\tCloses the program");
                            Console.WriteLine("\tdrop\t\t\t\tDrops the current function");
                            Console.WriteLine("\tclear [var]\t\t\tClears all variables or just one from the cache");
                            Console.WriteLine("\tdump\t\t\t\tPrints all variables in the cache");
                            Console.WriteLine("\tlist <target>\t\t\tLists things");
                            Console.WriteLine(
                                "\tload <func>\t\t\tLoads a function from disk; the current function is kept in a lower execution context");
                            Console.WriteLine(
                                "\tsave <name>\t\t\tSaves the current function with the given name; append '-y' to store current variables as well");
                            Console.WriteLine("\tstash\t\t\t\tStores the function in stash");
                            Console.WriteLine(
                                "\trestore <id>\t\t\tRestore another function; the current function is kept in a lower execution context");
                            Console.WriteLine("\tmode <D/R/G>\t\t\tChanges calculation mode to Deg/Rad/Grad");
                            Console.WriteLine("\tsolve <var> <lhs>\t\tSolves the function after the given variable");
                            Console.WriteLine("\tgraph\t\t\t\tDisplays the function in a 2D graph");
                            Console.WriteLine(
                                "\teval\t\t\t\tEvaluates the function, also achieved by just pressing return");
                            Console.WriteLine("\nSet variables with an equation (example: 'x = 5' or 'y = x * 2')");
                            break;
                        case "dump":
                            DumpVariables(ctx, func.ToString().Length / 8 + 1);
                            break;
                        case "list":
                            CmdList(cmds);
                            break;
                        case "load":
                            CmdLoad(cmds);
                            break;
                        case "save":
                            CmdSave(cmds, f, func, ctx);
                            break;
                        case "clear":
                            CmdClearVar(cmds, ctx);
                            break;
                        case "stash":
                            stash.Push((func, ctx));
                            return;
                        case "restore":
                            CmdRestore(cmds);
                            break;
                        case "mode":
                            CmdMode(cmds);
                            break;
                        case "solve":
                            CmdSolve(cmds, null, func, ctx);
                            break;
                        case "graph":
                            stash.Push((func, ctx));
                            StartGraph(stash.ToArray());
                            stash.Pop();
                            break;
                        case "eval":
                            CmdEval(func, ctx);
                            break;
                        default:
                            Console.WriteLine("Error: Unknown command; type 'help' for a list of commands");
                            break;
                    }
                }
            }
        }
    }

    private static void CmdSolve(string[] cmds, Component? lhs, Component func, MathContext ctx)
    {
        if (IsInvalidArgumentCount(cmds, 3))
            return;
        lhs ??= new Component { type = Component.Type.Var, arg = cmds[2] };
        var target = cmds[1];
        var count = func.EnumerateVars().Count(x => x == target);
        if (count == 0)
        {
            Console.WriteLine($"Error: Variable {target} was not found in function");
            return;
        }

        if (count > 1)
        {
            Console.WriteLine($"Error: Variable {target} was found more than once");
            return;
        }

        var result = new Solver(cmds[^1] == "-v").Solve(func, lhs, target);
        EvalFunc(result);
    }

    private static void CmdSave(string[] cmds, string? f, Component func, MathContext ctx)
    {
        if (IsInvalidArgumentCount(cmds, 2))
            return;
        if (cmds[1] == "unit")
        { // save as unit
            if (IsInvalidArgumentCount(cmds, 4))
                return;
            if (func.type != Component.Type.Frac 
                || (func.type == Component.Type.Op &&
                    func.op is not Component.Operator.Multiply or Component.Operator.Divide)
                || func.x?.type != Component.Type.Num || func.y?.type != Component.Type.Num)
            {
                Console.WriteLine($"Error: Cannot convert operation {func} to a unit");
                return;
            }
            var pkg = unitPackages.GetOrAdd(cmds[2], id => new UnitPackage(id));
            var result = pkg.Get(cmds[3]);
            var unitA = func.x?.unitX?.ToUnit(ctx)?.Unit ?? Unit.None;
            var unitB = func.y?.unitX?.ToUnit(ctx)?.Unit ?? Unit.None;
            
            if (func.type == Component.Type.Frac || (func.type == Component.Type.Op && func.op == Component.Operator.Divide))
            {
                unitA.AddQuotient(unitB, result);
                unitB.AddQuotient(unitA, result);
            }
            else if (func.type == Component.Type.Op && func.op == Component.Operator.Multiply)
            {
                unitA.AddProduct(unitB, result);
                unitB.AddProduct(unitA, result);
            }
            else throw new Exception("Assertion failure");
        } else
        {
            var data = f ?? func.ToString();
            if (cmds.Length > 2 && cmds[2] == "-y")
                data += $"\n{ConvertValuesToString(ctx.var, globalConstants.ContainsKey)}";
            var path = Path.Combine(dir, cmds[1] + FuncExt);
            File.WriteAllText(path, data);
            Console.WriteLine($"Function saved as {cmds[1]}");
        }
    }

    private static void CmdClearVar(string[] cmds, MathContext ctx)
    {
        if (cmds.Length > 1)
        {
            if (!ctx.var.ContainsKey(cmds[1]))
            {
                Console.WriteLine($"Error: Variable {cmds[1]} not found");
                return;
            }

            ctx.var.Remove(cmds[1]);
            Console.WriteLine($"Variable {cmds[1]} deleted");
        }
        else
        {
            ctx.var.Clear();
        }
    }

    private static void CmdEval(Component func, MathContext ctx)
    {
        var missing = FindMissingVariables(func, ctx);
        if (missing.Count > 0)
        {
            DumpVariables(ctx, func.ToString().Length / 8 + 1);
            Console.WriteLine(
                $"Error: Missing variable{(missing.Count != 1 ? "s" : "")} {string.Join(", ", missing)}");
        }
        else
        {
            PrintResult(func, func.Evaluate(ctx), ctx);
        }
    }

    private static void CmdSet(string func, string[] cmds)
    {
        var setConstN = ConvertValueFromString(func.Substring("set ".Length, func.Length - "set ".Length));
        if (setConstN is not { } setConst)
        {
            Console.WriteLine("Error: Invalid declaration of constant variable; try 'x = 5'");
            return;
        }

        if (globalConstants.ContainsKey(setConst.key))
        {
            Console.WriteLine($"Error: Cannot redefine {setConst.key}");
            return;
        }

        constants[setConst.key] = setConst.value.Evaluate(null).Value; // todo use UnitResult in constants?
        SaveConstants();
    }

    private static void CmdUnset(string[] cmds)
    {
        if (IsInvalidArgumentCount(cmds, 2))
            return;
        if (globalConstants.ContainsKey(cmds[1]))
        {
            Console.WriteLine($"Error: Cannot unset {cmds[1]}");
            return;
        }

        if (!constants.ContainsKey(cmds[1]))
        {
            Console.WriteLine($"Error: Unknown constant {cmds[1]}");
            return;
        }

        constants.Remove(cmds[1]);
        SaveConstants();
    }

    private static void CmdList(string[] cmds)
    {
        const string options = "'funcs', 'constants', 'stash', 'enabled', 'packs' and 'units <pack>'";
        if (cmds.Length == 1)
        {
            Console.WriteLine("Error: Listing target unspecified; options are " + options);
            return;
        }

        switch (cmds[1])
        {
            case "funcs" or "fx":
                var funcs = Directory.EnumerateFiles(dir, "*.math").Select(p => new FileInfo(p)).ToArray();
                if (funcs.Length == 0)
                {
                    Console.WriteLine("No saved functions");
                    break;
                }
                Console.WriteLine("Available functions:");
                foreach (var file in funcs)
                    Console.WriteLine(
                        $"\t- {file.Name.Substring(0, file.Name.Length - FuncExt.Length)}");

                break;
            case "constants" or "const":
                if (constants.Count == 0)
                {
                    Console.WriteLine("No available constants");
                    break;
                }
                Console.WriteLine("Available constants:");
                foreach (var (key, value) in constants)
                    Console.WriteLine($"\t{key}\t= {value}");

                break;
            case "stash":
                if (stash.Count == 0)
                {
                    Console.WriteLine("No functions in stash");
                    break;
                }
                Console.WriteLine("Stashed Functions:");
                var i = 0;
                foreach (var (fx, ctx) in stash)
                {
                    Console.WriteLine($"\tstash[{i++}]\t= {fx}");
                    ctx.DumpVariables("stash[#]".Length / 8 + 1, false);
                }

                break;
            case "enabled":
                if (enabledUnitPacks.Count == 0)
                {
                    Console.WriteLine("No unit packs are enabled");
                    break;
                }
                Console.WriteLine("Enabled unit packs:");
                foreach (var pack in enabledUnitPacks) 
                    Console.WriteLine($"\t- {pack}");
                break;
            case "packs":
                var directories = Directory.GetDirectories(dir, $"*{UnitPackExt}");
                if (directories.Length == 0)
                {
                    Console.WriteLine("No unit packages defined");
                    break;
                }
                Console.WriteLine("Available unit packages:");
                foreach (var pack in directories)
                {
                    var packName = new DirectoryInfo(pack).Name.StripExtension(UnitPackExt);
                    Console.WriteLine($"\t- {packName}");
                }
                break;
            case "units":
                if (IsInvalidArgumentCount(cmds, 3))
                    break;
                var package = unitPackages[cmds[2]];
                if (package.values.IsEmpty)
                {
                    Console.WriteLine("No units loaded");
                    break;
                }
                Console.WriteLine($"Units in package '{package.Name}':");
                foreach (var unit in package.values.Values)
                {
                    Console.WriteLine($"\t{unit.DisplayName}");
                    foreach (var (factor, result) in unit.Products)
                        Console.WriteLine($"\t\t{unit} = {result} / {factor}");
                    foreach (var (dividend, result) in unit.Quotients)
                        Console.WriteLine($"\t\t{unit} = {result} * {dividend}");
                }
                break;
            default:
                Console.WriteLine(
                    $"Error: Unknown listing target '{cmds[1]}';  options are " + options);
                break;
        }
    }

    private static void CmdToggleState(string[] cmds, bool newState)
    {
        if (IsInvalidArgumentCount(cmds, 2))
            return;
        var desiredPack = cmds[1];
        if (!unitPackages.ContainsKey(desiredPack))
        {
            Console.WriteLine($"Unit pack {desiredPack} does not exist");
            return;
        }
        if (newState == enabledUnitPacks.Contains(desiredPack))
        {
            Console.WriteLine($"Unit pack {desiredPack} is already " + (newState ? "enabled" : "disabled"));
            return;
        }
        if (newState)
            enabledUnitPacks.Add(desiredPack);
        else enabledUnitPacks.Remove(desiredPack);
        Console.WriteLine($"Unit pack {desiredPack} now " + (newState ? "enabled" : "disabled"));
    }

    private static void CmdLoad(string[] cmds)
    {
        if (!IsInvalidArgumentCount(cmds, 2))
        {
            var load = LoadFunc(cmds[1]);
            if (load is { } res)
                EvalFunc(res.func, ctx: res.ctx);
        }
    }

    private static void CmdMove(string[] cmds)
    {
        if (IsInvalidArgumentCount(cmds, 3))
            return;
        var path1 = Path.Combine(dir, cmds[1] + FuncExt);
        var path2 = Path.Combine(dir, cmds[2] + FuncExt);
        if (!File.Exists(path1))
            Console.WriteLine($"Function with name {cmds[1]} not found");
        else File.Move(path1, path2);
    }

    private static void CmdDelete(string[] cmds)
    {
        if (IsInvalidArgumentCount(cmds, 2))
            return;
        var path0 = Path.Combine(dir, cmds[1] + FuncExt);
        if (File.Exists(path0))
        {
            File.Delete(path0);
            Console.WriteLine($"Function with name {cmds[1]} deleted");
        }
        else
        {
            Console.WriteLine($"Function with name {cmds[1]} not found");
        }
    }

    private static void CmdRestore(string[] cmds)
    {
        (Component func, MathContext ctx) entry;
        if (cmds.Length == 1)
        {
            entry = stash.Pop();
        }
        else
        {
            if (Regex.Match(cmds[1], "\\d+") is { Success: true })
            {
                var index = int.Parse(cmds[1]);
                if (index > stash.Count)
                {
                    Console.WriteLine($"Error: Backtrace index {index} too large");
                    return;
                }

                entry = stash.ToArray()[index];
                var bak = stash.ToList();
                bak.Remove(entry);
                stash.Clear();
                bak.Reverse();
                bak.ForEach(stash.Push);
            }
            else
            {
                Console.WriteLine($"Error: Invalid backtrace {cmds[1]}");
                return;
            }
        }

        EvalFunc(entry.func, ctx: entry.ctx);
    }

    private static void CmdClearTarget(string[] cmds)
    {
        if (IsInvalidArgumentCount(cmds, 2))
            return;
        switch (cmds[1])
        {
            case "stash":
                stash.Clear();
                Console.WriteLine("Stash cleared");
                break;
            default:
                Console.WriteLine($"Error: Invalid clear target '{cmds[1]}'; options are 'stash'");
                break;
        }
    }

    private static void CmdMode(string[] cmds)
    {
        if (cmds.Length > 1)
            switch (cmds[1].ToLower())
            {
                case "d" or "deg" or "degree":
                    DRG = CalcMode.Deg;
                    break;
                case "r" or "rad" or "radians":
                    DRG = CalcMode.Rad;
                    break;
                case "g" or "grad" or "grade":
                    DRG = CalcMode.Grad;
                    break;
                default:
                    Console.WriteLine($"Error: Invalid calculation mode '{cmds[1]}'");
                    break;
            }

        Console.WriteLine($"Calculation mode is {DRG}");
    }

    private static List<string> FindMissingVariables(Component func, MathContext ctx)
    {
        var missing = new List<string>();
        foreach (var var in ctx.var.Values.Append(func).SelectMany(it => it.EnumerateVars())
                     .Distinct())
            if (!ctx.var.ContainsKey(var))
                missing.Add(var);
        missing.RemoveAll(constants.ContainsKey);
        return missing;
    }

    private static void StartGraph(params (Component fx, MathContext ctx)[] funcs)
    {
        _graph?.Dispose();
        _graph = new Graph(funcs);
    }

    private static int DumpVariables(this MathContext ctx, int alignBase = 1, bool shouldError = true)
    {
        if (ctx.var.Count == 0)
        {
            if (shouldError)
                Console.WriteLine("Error: No variables are set");
            return 1;
        }

        var maxAlign = ctx.var.Keys.Max(key => key.Length) / 8;
        foreach (var (key, val) in ctx.var)
        {
            var align = Math.Max(maxAlign > 0 ? maxAlign - alignBase : alignBase,
                maxAlign - (key.Length / 8 + 1) + alignBase);
            var spacer = Enumerable.Range(0, align).Aggregate(string.Empty, (str, _) => str + '\t');
            Console.WriteLine($"\t{key}{spacer}= {val}");
        }

        return maxAlign;
    }

    private static void PrintResult(Component func, UnitResult result, MathContext? ctx = null)
    {
        var funcAlign = func.ToString().Length / 8 + 1;
        var align = Math.Max(1, (ctx?.DumpVariables(funcAlign) ?? 1) - funcAlign);
        var spacer = Enumerable.Range(0, align).Aggregate(string.Empty, (str, _) => str + '\t');
        Console.WriteLine($"\t{func}{spacer}= {result}");
    }

    internal static double IntoDRG(double value)
    {
        return DRG switch
        {
            CalcMode.Deg => value,
            CalcMode.Rad => value * factorD2R,
            CalcMode.Grad => value * factorD2G,
            _ => throw new ArgumentOutOfRangeException(nameof(value), value, "Invalid Calculation Mode")
        };
    }

    internal static double FromDRG(double value)
    {
        return DRG switch
        {
            CalcMode.Deg => value,
            CalcMode.Rad => value / factorD2R,
            CalcMode.Grad => value / factorD2G,
            _ => throw new ArgumentOutOfRangeException(nameof(value), value, "Invalid Calculation Mode")
        };
    }

    internal static string StripExtension(this string str, string ext)
    {
        if (str.EndsWith(ext))
            str = str.Substring(0, str.IndexOf(ext, StringComparison.Ordinal));
        return str;
    }
}

public enum CalcMode : byte
{
    Deg = 0x1,
    Rad = 0x2,
    Grad = 0x4
}

public sealed class MathContext
{
    public readonly Dictionary<string, Component> var = new();
    public List<string> enabledUnitPacks { get; init; }= new();

    public MathContext() : this(null)
    {
    }

    public MathContext(MathContext? copy) : this(copy?.var, copy?.enabledUnitPacks)
    {
    }

    public MathContext(Dictionary<string, Component>? copy, List<string>? enabledUnitPacks)
    {
        if (copy != null)
            foreach (var (key, value) in copy)
                var[key] = value;
        if (enabledUnitPacks != null)
            foreach (var pack in enabledUnitPacks)
                this.enabledUnitPacks.Add(pack);
    }

    public UnitPackage[] GetUnitPackages() => Program.unitPackages
        .Where(pkg => enabledUnitPacks.Contains(pkg.Key))
        .Select(pkg => pkg.Value)
        .ToArray();
}