using System.Collections;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Antlr4.Runtime;
using clmath.Antlr;
using comroid.csapi.common;

// ReSharper disable once ArrangeNamespaceBody
namespace clmath
{
    public static class Program
    {
        public static readonly Random RNG = new();
        public static readonly MathContext BaseContext = new(null);
        
        private const double factorD2R = Math.PI / 180;
        private const double factorD2G = 1.111111111;
        private static readonly string FuncExt = ".math";
        private static readonly string ConstExt = ".vars";
        internal static readonly string UnitExt = ".unit";
        internal static readonly string UnitPackExt = ".units";

        private static readonly string dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "comroid", "clmath");

        private static readonly string constantsFile = Path.Combine(dir, "constants" + ConstExt);
        private static readonly string configFile = Path.Combine(dir, "config.bin");

        private static readonly int ConfigVersion;
        private static bool _exiting;
        private static bool _dropAll;
        private static Graph? _graph;

        private static readonly Dictionary<string, double> globalConstants = new()
        {
            { "pi", Math.PI },
            { "e", Math.E },
            { "tau", Math.PI * 2 },
            { "rng_i", double.NaN },
            { "rng_d", double.NaN }
        };

        public static readonly Stack<(Component func, MathContext ctx)> stash = new();
        public static readonly ConcurrentDictionary<string, UnitPackage> unitPackages = new();

        static Program()
        {
            ConfigVersion = FileVersionInfo.GetVersionInfo(typeof(Program).Assembly.Location).FileMajorPart;
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
            LoadUnits(BaseContext);
        }

        public static Dictionary<string, double> constants { get; private set; } = null!;

        public static CalcMode DRG { get; set; } = CalcMode.Deg;
        public static bool AutoEval { get; set; } = true;

        public static void SetUp()
        {
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
        }

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

        private static void LoadUnits(MathContext ctx)
        {
            foreach (var pkg in Directory.EnumerateDirectories(dir, $"*{UnitPackExt}"))
            {
                var packageName = new DirectoryInfo(pkg).Name.StripExtension(UnitPackExt);
                var package = new UnitPackage(packageName);
                foreach (var unitFile in Directory.EnumerateFiles(pkg, $"*{UnitExt}"))
                    try
                    {
                        package.Load(unitFile);
                    }
                    catch (UnitPackage.UnitLoadException)
                    {
                        package.Load(MigrateUnitFile(unitFile));
                    }
                unitPackages[packageName] = package;
                package.Finalize(ctx);
                package.Save();
            }
        }

        private static readonly string OldDescriptorPattern = "([pq]):\\s(\\w+),(\\w+)";
        private static string MigrateUnitFile(string unitFile)
        {
            Console.WriteLine("Migrating outdated unit file " + unitFile);
            var f = new FileInfo(unitFile);
            var repr = f.Name;
            repr = repr.Substring(0, repr.IndexOf(UnitExt, StringComparison.Ordinal));
            var lines = File.ReadAllLines(unitFile);
            var convert = new List<string>();
            var uName = lines[0];
            for (var i = 1; i < lines.Length; i++)
                if (Regex.Match(lines[i], OldDescriptorPattern) is { Success: true } match)
                    convert.Add($"{repr}{(match.Groups[1].Value == "p" ? "*" : "/")}" +
                                $"{match.Groups[2].Value}={match.Groups[3]}");
            var newFile = Path.Combine(f.DirectoryName!, uName + UnitExt);
            using var fs = File.OpenWrite(newFile);
            // file head
            fs.Write(new byte[]{0,0,0,0});
            var buf = Encoding.ASCII.GetBytes(repr);
            fs.Write(BitConverter.GetBytes(buf.Length));
            fs.Write(buf);
            fs.Write(new[] { (byte)'\n' });
                
            // equations
            foreach (var equation in convert) 
                fs.Write(Encoding.ASCII.GetBytes(equation + '\n'));
            fs.Flush();
            File.Delete(unitFile);
            return newFile;
        }

        private static void SaveConfig()
        {
            using var fs = File.OpenWrite(configFile);
            fs.Write(BitConverter.GetBytes(ConfigVersion));
            fs.Write(new[] { (byte)DRG });
            fs.Write(BitConverter.GetBytes(AutoEval));
            fs.Write(BitConverter.GetBytes(BaseContext.EnabledUnitPacks.Count));
            foreach (var pack in BaseContext.EnabledUnitPacks)
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
                BaseContext.EnabledUnitPacks.Add(Encoding.ASCII.GetString(Read(fs, len)));
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

        private static string ConvertValuesToString(IEnumerable<KeyValuePair<string, Component>> values, Func<string, bool>? skip = null)
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

        public static (string key, Component value)? ConvertValueFromString(string data)
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

                try
                {
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
                            Console.WriteLine("\tunit <verb> [args]\t\tModify Units");
                            Console.WriteLine("\tenable <target>\t\t\tEnables the specified unit package");
                            Console.WriteLine("\tdisable <target>\t\tDisables the specified unit package");
                            Console.WriteLine("\tload <name>\t\t\tLoads function with the given name");
                            Console.WriteLine("\tmv <n0> <n1>\t\t\tRename function with the given name");
                            Console.WriteLine("\tdelete <name>\t\t\tDeletes function with the given name");
                            Console.WriteLine("\trestore <trace>\t\t\tRestores a function from stash");
                            Console.WriteLine("\tclear <target>\t\t\tClears the desired target");
                            Console.WriteLine("\tmode <D/R/G>\t\t\tSets the mode to Deg/Rad/Grad");
                            Console.WriteLine("\tsolve <var> <lhs> <func>\tSolves a function after var");
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
                            CmdList(BaseContext, cmds);
                            break;
                        case "unit":
                            CmdUnit(BaseContext, cmds);
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
                            CmdClearTarget(BaseContext, cmds);
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
                catch (Exception e)
                {
                    Handle(e);
                }
            }
        }

        private static void Handle(Exception e)
        {
            Console.WriteLine("Error: " + e.Message);
            Debug.WriteLine(e);
        }

        private static (Component fx, MathContext ctx)[] CreateArgsFuncs(int start, params string[] args)
        {
            return args.ToList()
                .GetRange(start, args.Length - start)
                .Select(ParseFunc)
                .Select(fx => (fx, new MathContext(BaseContext)))
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
                ctx = new MathContext(BaseContext);
                foreach (var (key, value) in vars)
                    ctx[key] = value;
            }
            else
            {
                ctx = new MathContext(BaseContext);
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

        public static Component ParseFunc(string f)
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
            EvalFunc(fx, BaseContext, fx.ToString());
        }

        private static void EvalFunc(Component func, MathContext ctx, string? f = null)
        {
            if (func.GetVars().Distinct().All(constants.ContainsKey))
            {
                var res = func.Evaluate(new MathContext(BaseContext));
                PrintResult(func, res, BaseContext, false);
            }
            else
            {
                // enter editor mode
                while (true)
                {
                    if (_exiting || _dropAll)
                        return;
                    Console.Title = $"[{DRG}] {func}";
                    Console.Write($"{func}> ");
                    var cmd = Console.ReadLine()!;
                    cmd = CleanupString(cmd);

                    try
                    {
                        if (ConvertValueFromString(cmd) is { } result)
                        {
                            var key = result.key;
                            var value = result.value;
                            if (value.GetVars().Contains(key))
                                Console.WriteLine($"Error: Variable {key} cannot use itself");
                            else if (constants.ContainsKey(key))
                                Console.WriteLine($"Error: Cannot redefine {key}");
                            else ctx[key] = value;

                            if (AutoEval && FindMissingVariables(func, ctx).Count == 0)
                                PrintResult(func, func.Evaluate(ctx), ctx);
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
                                    Console.WriteLine(
                                        $"clmath v{typeof(Program).Assembly.GetName().Version} by comroid\n");
                                    Console.WriteLine("Available commands:");
                                    Console.WriteLine("\thelp\t\t\t\tShows this text");
                                    Console.WriteLine("\texit\t\t\t\tCloses the program");
                                    Console.WriteLine("\tdrop\t\t\t\tDrops the current function");
                                    Console.WriteLine(
                                        "\tclear [var]\t\t\tClears all variables or just one from the cache");
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
                                    Console.WriteLine(
                                        "\tsolve <var> <lhs>\t\tSolves the function after the given variable");
                                    Console.WriteLine("\tgraph\t\t\t\tDisplays the function in a 2D graph");
                                    Console.WriteLine(
                                        "\teval\t\t\t\tEvaluates the function, also achieved by just pressing return");
                                    Console.WriteLine(
                                        "\nSet variables with an equation (example: 'x = 5' or 'y = x * 2')");
                                    break;
                                case "dump":
                                    DumpVariables(ctx);
                                    break;
                                case "list":
                                    CmdList(ctx, cmds);
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
                    catch (Exception e)
                    {
                        Handle(e);
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
            var count = func.GetVars().Count(x => x == target);
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
            EvalFunc(result, ctx);
        }

        private static void CmdSave(string[] cmds, string? f, Component func, MathContext ctx)
        {
            if (IsInvalidArgumentCount(cmds, 2))
                return;
            /*
            if (cmds[1] == "unit")
            {
                // save as unit
                if (IsInvalidArgumentCount(cmds, 4))
                    return;
                if (func.type != Component.Type.Frac
                    || (func.type == Component.Type.Op &&
                        func.op is not Component.Operator.Multiply or Component.Operator.Divide)
                    || func.x?.type != Component.Type.Var || func.y?.type != Component.Type.Var)
                {
                    Console.WriteLine($"Error: Cannot convert operation {func} to a unit");
                    return;
                }

                var pkg = unitPackages.GetOrAdd(cmds[2], id => new UnitPackage(id));
                var result = pkg.Get(cmds[3]);
                var unitA = func.x?.unitX?.ToUnit(ctx)?.Unit ?? Unit.None;
                var unitB = func.y?.unitX?.ToUnit(ctx)?.Unit ?? Unit.None;

                if (func.type == Component.Type.Frac ||
                    (func.type == Component.Type.Op && func.op == Component.Operator.Divide))
                {
                    unitA.AddQuotient(unitB, result);
                    unitB.AddQuotient(unitA, result);
                }
                else if (func.type == Component.Type.Op && func.op == Component.Operator.Multiply)
                {
                    unitA.AddProduct(unitB, result);
                    unitB.AddProduct(unitA, result);
                }
                else
                {
                    throw new Exception("Assertion failure");
                }
            }
            else
            {
                var data = f ?? func.ToString();
                if (cmds.Length > 2 && cmds[2] == "-y")
                    data += $"\n{ConvertValuesToString(ctx.Vars(), globalConstants.ContainsKey)}";
                var path = Path.Combine(dir, cmds[1] + FuncExt);
                File.WriteAllText(path, data);
                Console.WriteLine($"Function saved as {cmds[1]}");
            }*/
        }

        private static void CmdClearVar(string[] cmds, MathContext ctx)
        {
            if (cmds.Length > 1)
            {
                if (ctx.Vars().All(pair => pair.Key != cmds[1]))
                {
                    Console.WriteLine($"Error: Variable {cmds[1]} not found");
                    return;
                }

                ctx[cmds[1]] = null;
                Console.WriteLine($"Variable {cmds[1]} deleted");
            }
            else
            {
                ctx.ClearVars();
            }
        }

        private static void CmdEval(Component func, MathContext ctx)
        {
            var missing = FindMissingVariables(func, ctx);
            if (missing.Count > 0)
            {
                DumpVariables(ctx);
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

        private static void CmdList(MathContext ctx, string[] cmds)
        {
            const string options = "'funcs', 'constants', 'mem', 'stash', 'enabled', 'packs' and 'units <pack>'";
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
                    foreach (var (key, value) in constants.Where(c => !double.IsNaN(c.Value)))
                        Console.WriteLine($"\t{key}\t= {value}");
                    Console.WriteLine("Available semiconstants:");
                    Console.WriteLine("\tmem\t= Previous computation result");
                    Console.WriteLine("\trng_i\t= Random integer");
                    Console.WriteLine("\trng_d\t= Random decimal");

                    break;
                case "mem":
                    if (!ctx.Mem().Any())
                    {
                        Console.WriteLine("No values in memory");
                        break;
                    }

                    Console.WriteLine("Variables in Memory:");
                    var i = 0;
                    foreach (var value in ctx.Mem()) 
                        Console.WriteLine($"\tmem[{i++}]\t= {value}");

                    break;
                case "stash":
                    if (stash.Count == 0)
                    {
                        Console.WriteLine("No functions in stash");
                        break;
                    }

                    Console.WriteLine("Stashed Functions:");
                    var i0 = 0;
                    foreach (var (fx, ctx0) in stash)
                    {
                        Console.WriteLine($"\tstash[{i0++}]\t= {fx}");
                        ctx0.DumpVariables(shouldError: false);
                    }

                    break;
                case "enabled":
                    if (BaseContext.EnabledUnitPacks.Count == 0)
                    {
                        Console.WriteLine("No unit packs are enabled");
                        break;
                    }

                    Console.WriteLine("Enabled unit packs:");
                    foreach (var pack in BaseContext.EnabledUnitPacks)
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
                    if (!unitPackages.ContainsKey(cmds[2]))
                    {
                        Console.WriteLine($"Error: Unit pack with name {cmds[2]} was not found");
                        break;
                    }

                    var package = unitPackages[cmds[2]];
                    if (package.values.IsEmpty)
                    {
                        Console.WriteLine("No units loaded");
                        break;
                    }

                    Console.WriteLine($"Units in package '{package.Name}':");
                    foreach (var unit in package.values.Values)
                    {
                        Console.WriteLine($"\t{unit.Name}");
                        foreach (var (other, op, eval) in unit.GetEvaluators())
                            Console.WriteLine($"{unit}{eval.op switch {
                                Component.Operator.Multiply => "*",
                                Component.Operator.Divide => "/",
                                _ => throw new ArgumentOutOfRangeException() }}{eval.overrideY?.ToString()
                                ?? other.Repr} = {eval.output}");
                    }

                    break;
                default:
                    Console.WriteLine(
                        $"Error: Unknown listing target '{cmds[1]}';  options are " + options);
                    break;
            }
        }

        private static UnitPackage selectedPkg;
        private static Unit selectedUnit;
        private static void CmdUnit(MathContext ctx, string[] cmds)
        {
            if (IsInvalidArgumentCount(cmds, 2))
                return;

            void Selection(int m, string? detail = null)
            {
                if (m >= 0 && selectedPkg == null)
                    throw new Exception("No unit pack selected" + (detail == null ? string.Empty : "; " + detail));
                if (m >= 1 && selectedUnit == null)
                    throw new Exception("No unit selected" + (detail == null ? string.Empty : "; " + detail));
            }

            var table = new TextTable(true, TextTable.LineMode.Unicode);
            switch (cmds[1])
            {
                case "list"/* */:
                    Selection(0);
                    var unitName = table.AddColumn("Unit");
                    var unitRepr = table.AddColumn("ID");
                    foreach (var unit in selectedPkg.values.Values)
                        table.AddRow()
                            .SetData(unitName, unit.Name)
                            .SetData(unitRepr, unit.Repr);
                    Console.Write(table);
                    break;
                case "sel"/* <name> */:
                    if (IsInvalidArgumentCount(cmds, 3))
                        return;
                    selectedUnit = selectedPkg.values.GetValueOrDefault(cmds[2])
                                   ?? selectedPkg.values.Values.FirstOrDefault(x => x.Name == cmds[2])!;
                    Selection(1, "unknown unit: " + cmds[2]);
                    break;
                case "add"/* <name> <repr> */:
                    if (IsInvalidArgumentCount(cmds, 4))
                        return;
                    Selection(0);
                    selectedPkg.values[cmds[3]] = selectedUnit = new Unit(selectedPkg, cmds[2], cmds[3]);
                    Selection(1, "unable to add unit: " + cmds[2]);
                    break;
                case "rename"/* <new_name> */:
                    if (IsInvalidArgumentCount(cmds, 3))
                        return;
                    Selection(1);
                    selectedUnit.Name = cmds[2];
                    File.Move(Path.Combine(dir, selectedPkg.Name + UnitPackExt, selectedUnit.Name + UnitExt),
                        Path.Combine(dir, selectedPkg.Name + UnitPackExt, cmds[2] + UnitExt));
                    break;
                case "id"/* <new_id> */:
                    if (IsInvalidArgumentCount(cmds, 3))
                        return;
                    Selection(1);
                    if (selectedPkg.values.Values.Any(x => x.Repr == cmds[2]))
                        throw new Exception("ID already taken: " + cmds[2]);
                    var old = selectedUnit.Repr;
                    selectedPkg.values[selectedUnit._repr = cmds[2]] = selectedPkg.values[old];
                    selectedPkg.values.Remove(old, out _);
                    UnitRef.NotifyNameChange(old, selectedUnit.Repr);
                    break;
                case "del"/* */:
                    Selection(1);
                    File.Delete(Path.Combine(dir, selectedPkg.Name + UnitPackExt, selectedUnit.Name + UnitExt));
                    selectedPkg.values.Remove(selectedUnit.Repr, out _);
                    selectedUnit = null!;
                    break;
                case "addequ"/* <equation> */:
                    if (IsInvalidArgumentCount(cmds, 3))
                        return;
                    Selection(1);
                    selectedPkg.ParseEquations(new AntlrInputStream(string.Join(" ", cmds[2..])), selectedUnit);
                    selectedPkg.Finalize(ctx);
                    break;
                case "listpkg"/* */:
                    var pkgName = table.AddColumn("Name");
                    var unitCount = table.AddColumn("Units");
                    foreach (var (name, pkg) in unitPackages)
                        table.AddRow()
                            .SetData(pkgName, name)
                            .SetData(unitCount, pkg.values.Count);
                    Console.Write(table);
                    break;
                case "selpkg"/* <name> */:
                    if (IsInvalidArgumentCount(cmds, 3))
                        return;
                    selectedUnit = null!;
                    selectedPkg = unitPackages.GetValueOrDefault(cmds[2])!;
                    Selection(0, "unknown package: " + cmds[2]);
                    break;
                case "addpkg"/* <name> */:
                    if (IsInvalidArgumentCount(cmds, 3))
                        return;
                    selectedUnit = null!;
                    selectedPkg = unitPackages[cmds[2]] = new UnitPackage(cmds[2]);
                    Selection(0, "failed to create package: " + cmds[2]);
                    break;
                case "delpkg"/* */:
                    Selection(0);
                    unitPackages.Remove(selectedPkg.Name, out _);
                    Directory.Delete(Path.Combine(dir, selectedPkg.Name + UnitPackExt));
                    selectedPkg = null!;
                    selectedUnit = null!;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(cmds), "Invalid arguments");
            }
            selectedPkg?.Save();
            Console.WriteLine("OK");
        }

        private static void Save(this UnitPackage pkg)
        {
            foreach (var unit in pkg.values.Values)
                unit.Save();
            Directory.CreateDirectory(Path.Combine(dir, pkg.Name + UnitPackExt));
        }

        private static void Save(this Unit unit)
        {
            var path = Path.Combine(dir, unit.Package.Name + UnitPackExt, unit.Name + UnitExt);
            using var fs = File.OpenWrite(path);
            // file head
            fs.Write(new byte[]{0,0,0,0});
            var buf = Encoding.ASCII.GetBytes(unit.Repr);
            fs.Write(BitConverter.GetBytes(buf.Length));
            fs.Write(buf);
            fs.Write(new[] { (byte)'\n' });
                
            // equations
            string EvalToString(Unit unit, UnitRef other, UnitEvaluator eval) =>
                $"{unit.Repr}{eval.op switch {
                    Component.Operator.Multiply => '*',
                    Component.Operator.Divide => '/',
                    _ => throw new ArgumentOutOfRangeException() }}{eval.overrideY?.ToString() ?? other.Repr}={eval.output.Repr}";
            foreach (var (other, _, eval) in unit.GetEvaluators()) 
                fs.Write(Encoding.ASCII.GetBytes(EvalToString(unit, other, eval) + '\n'));
            fs.Flush();
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

            if (newState == BaseContext.EnabledUnitPacks.Contains(desiredPack))
            {
                Console.WriteLine($"Unit pack {desiredPack} is already " + (newState ? "enabled" : "disabled"));
                return;
            }

            if (newState)
                BaseContext.EnabledUnitPacks.Add(desiredPack);
            else BaseContext.EnabledUnitPacks.Remove(desiredPack);
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

        private static void CmdClearTarget(MathContext ctx, string[] cmds)
        {
            if (IsInvalidArgumentCount(cmds, 2))
                return;
            switch (cmds[1])
            {
                case "vars":
                    ctx.ClearVars();
                    Console.WriteLine("Variables cleared");
                    break;
                case "stash":
                    stash.Clear();
                    Console.WriteLine("Stash cleared");
                    break;
                case "mem":
                    ctx.ClearMem();
                    Console.WriteLine("Memory cleared");
                    break;
                default:
                    Console.WriteLine($"Error: Invalid clear target '{cmds[1]}'; options are 'vars', 'stash' and 'mem'");
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
            foreach (var var in ctx.Vars()
                         .Select(x => x.Value)
                         .Append(func)
                         .SelectMany(it => it.GetVars())
                         .Distinct())
                if (ctx.Vars().All(x => x.Key != var))
                    missing.Add(var);
            missing.RemoveAll(constants.ContainsKey);
            return missing;
        }

        private static void StartGraph(params (Component fx, MathContext ctx)[] funcs)
        {
            _graph?.Dispose();
            _graph = new Graph(funcs);
        }

        private static void DumpVariables(this MathContext ctx, TextTable? table = null, bool shouldError = true)
        {
            TextTable.Column term, expr;
            bool newTable = table == null;
            if (table == null)
            {
                table = new TextTable(true, TextTable.LineMode.Unicode);
                term = table.AddColumn("Variable");
                expr = table.AddColumn("Value", true);
            }
            else
            {
                term = table.Columns[0];
                expr = table.Columns[1];
            }
            if (!ctx.Vars().Any())
            {
                if (shouldError)
                    Console.WriteLine("Error: No variables are set");
                return;
            }
            foreach (var (key, val) in ctx.Vars())
            {
                table.AddRow()
                    .SetData(term, key)
                    .SetData(expr, val);
            }
            if (newTable) 
                Console.Write(table);
        }

        private static void PrintResult(Component func, UnitResult result, MathContext ctx, bool shouldError = true)
        {
            ctx[0] = result; // push result to mem
            var table = new TextTable(true, TextTable.LineMode.Unicode);
            var term = table.AddColumn("Term");
            var expr = table.AddColumn("Value", true);
            ctx.DumpVariables(table, shouldError);
            table.AddSeparator();
            table.AddRow()
                .SetData(term, func)
                .SetData(expr, result);
            Console.Write(table);
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

        internal static double Evaluate(this Component.Operator op, double x, double y) => op switch
        {
            Component.Operator.Add => x + y,
            Component.Operator.Subtract => x - y,
            Component.Operator.Multiply => x * y,
            Component.Operator.Divide => x / y,
            Component.Operator.Modulus => x % y,
            Component.Operator.Power => Math.Pow(x, y),
            _ => throw new ArgumentOutOfRangeException(nameof(op), op, "Invalid Operator")
        };
    }

    public enum CalcMode : byte
    {
        Deg = 0x1,
        Rad = 0x2,
        Grad = 0x4
    }

    public sealed class MathContext
    {
        private readonly MathContext? _parent;
        private readonly Dictionary<string, Component> var = new();
        private readonly Stack<UnitResult> mem = new();
        public readonly HashSet<string> EnabledUnitPacks = new();

        public MathContext(MathContext? parent)
        {
            foreach (var enabled in (_parent = parent)?.EnabledUnitPacks ?? ArraySegment<string>.Empty.AsEnumerable())
                EnabledUnitPacks.Add(enabled);
        }

        public Component? this[string key]
        {
            get => var.ContainsKey(key) ? var[key] : _parent?[key] ?? throw new Exception($"Variable {key} not found");
            set
            {
                if (value == null)
                {
                    var.Remove(key);
                    if (_parent != null)
                        _parent[key] = null;
                }
                else var[key] = value;
            }
        }

        public UnitResult this[int i]
        {
            get => i < mem.Count ? mem.ToArray()[i] : _parent?[i - mem.Count] ?? UnitResult.Zero;
            set
            {
                if (mem.Count == 0 || mem.Peek() != value)
                    mem.Push(value);
            }
        }

        public IEnumerable<KeyValuePair<string, Component>> Vars() => var.Concat(_parent?.Vars() ?? ArraySegment<KeyValuePair<string, Component>>.Empty);

        public void ClearVars() => var.Clear();

        public IEnumerable<UnitResult> Mem() => mem.Concat(_parent?.Mem() ?? ArraySegment<UnitResult>.Empty);

        public void ClearMem() => mem.Clear();

        public UnitPackage[] GetUnitPackages()
        {
            return Program.unitPackages
                .Where(pkg => EnabledUnitPacks.Contains(pkg.Key))
                .Select(pkg => pkg.Value)
                .ToArray();
        }

        public Unit? FindUnit(string id) =>
            GetUnitPackages().SelectMany(x => x.values.Values).FirstOrDefault(x => x.Repr == id);
    }
}