using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Antlr4.Runtime;
using clmath.Antlr;
using CommandLine;
using comroid.csapi.common;
using static comroid.csapi.common.DebugUtil;
using Parser = CommandLine.Parser;

// ReSharper disable once ArrangeNamespaceBody
namespace clmath
{
    public static class Program
    {
        #region Fields
        private const double factorD2R = Math.PI / 180;
        private const double factorD2G = 1.111111111;
        public static readonly Random RNG = new();
        public static readonly MathContext BaseContext;
        public static readonly Stack<MathContext> Stack;
        public static bool SimplePrint = false;
        private static readonly string FuncExt = ".math";
        private static readonly string ConstExt = ".vars";
        internal static readonly string UnitExt = ".unit";
        internal static readonly string UnitPackExt = ".units";

        private static readonly string dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "comroid", "clmath");

        private static readonly string constantsFile = Path.Combine(dir, "constants" + ConstExt);
        private static readonly string configFile = Path.Combine(dir, "config.bin");

        public static Parser Parser;
        private static readonly int ConfigVersion;
        private static bool _exiting;
        private static bool _resetting;
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

        public static readonly ConcurrentDictionary<string, UnitPackage> unitPackages = new();
        private static readonly Stack<MathContext> stash = new();
        private static readonly MultiEntryTable results = new();

        private static UnitPackage selectedPkg;
        private static Unit selectedUnit;
        public static MathContext Current => Stack.Peek();

        public static CalcMode DRG { get; set; } = CalcMode.Deg;
        public static bool AutoEval { get; set; } = true;
        public static Dictionary<string, double> constants { get; private set; } = null!;
        #endregion

        #region Lifecycle Methods

        static Program()
        {
            BaseContext = new MathContext(null!);
            Stack = new Stack<MathContext>();
            Stack.Push(BaseContext);
            Parser = new Parser(cfg =>
            {
                cfg.AutoHelp = true;
                cfg.AutoVersion = true;
                cfg.CaseSensitive = false;
                cfg.ParsingCulture = CultureInfo.InvariantCulture;
            });
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
                                $"{match.Groups[2].Value}={match.Groups[3]};");
            var newFile = Path.Combine(f.DirectoryName!, uName + UnitExt);
            using var fs = File.OpenWrite(newFile);
            // file head
            fs.Write(new byte[] { 0, 0, 0, 0 });
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

        private static void Save()
        {
            SaveConfig();
            SaveConstants();
            foreach (var (_, pkg) in unitPackages)
                pkg.Save();
        }

        #endregion

        public static void Main(params string[] args)
        {
            Parser.ParseArguments<GraphCommand, SolveCommand>(args)
                .WithParsed(WithExceptionHandler<GraphCommand>(HandleException, HandleGraph))
                .WithParsed(WithExceptionHandler<SolveCommand>(HandleException, HandleSolve))
                .WithNotParsed(WithExceptionHandler<IEnumerable<Error>>(HandleException, _ =>
                {
                    if (args.Length == 0)
                    {
                        StdIoMode();
                    }
                    else
                    {
                        var arg = string.Join(" ", args);
                        EvalMode(new MathContext(BaseContext, ParseFunc(File.ReadAllText(arg))));
                        Console.WriteLine("Press any key to exit...");
                        Console.ReadLine();
                    }
                }));
            Save();
            _exiting = false;
        }

        private static void StdIoMode()
        {
            while (!_exiting)
            {
                if (_resetting)
                {
                    _resetting = false;
                    HandleClear(new ClearCommand { Target = ClearCommand.TargetType.all });
                }

                _dropAll = false;
                Console.Title = $"[{DRG}] clmath";
                Console.Write("math> ");
                var input = Console.ReadLine()!;
                ParseInput(input, new Dictionary<Type, Action<ICmd>>()
                {
                    { typeof(SetCommand), cmd => HandleSet((SetCommand)cmd) },
                    { typeof(UnsetCommand), cmd => HandleUnset((UnsetCommand)cmd) },
                    { typeof(ListCommand), cmd => HandleList((ListCommand)cmd) },
                    { typeof(EditCommand), cmd => HandleEdit((EditCommand)cmd) },
                    { typeof(CopyCommand), cmd => HandleCopy((CopyCommand)cmd) },
                    { typeof(LoadCommand), cmd => HandleLoad((LoadCommand)cmd) },
                    { typeof(RenameCommand), cmd => HandleRename((RenameCommand)cmd) },
                    { typeof(DeleteCommand), cmd => HandleDelete((DeleteCommand)cmd) },
                    { typeof(RestoreCommand), cmd => HandleRestore((RestoreCommand)cmd) },
                    { typeof(ClearCommand), cmd => HandleClear((ClearCommand)cmd) },
                    { typeof(ModeCommand), cmd => HandleMode((ModeCommand)cmd) },
                    { typeof(SolveCommand), cmd => HandleSolve((SolveCommand)cmd) },
                    { typeof(GraphCommand), cmd => HandleGraph((GraphCommand)cmd) }
                }, func =>
                {
                    var ctx = new MathContext(Current, ParseFunc(func));
                    Stack.Push(ctx);
                    EvalMode(ctx);
                });
            }
        }

        private static void EvalMode(MathContext ctx)
        {
            var func = ctx.Function;
            if (func.GetVars().Distinct().All(constants.ContainsKey))
            {
                var res = func.Evaluate(new MathContext(BaseContext));
                PrintResult(func, res, BaseContext, false);
            }
            else
            {
                var cc = 0;
                // enter editor mode
                while (!(Current.Root || _exiting || _dropAll))
                {
                    Console.Title = $"[{DRG}] {func}";
                    Console.Write($"{func}> ");
                    var input = Console.ReadLine()!;
                    if (ConvertValueFromString(input) is { } equ)
                    {
                        var key = equ.key;
                        var value = equ.value;
                        if (value.GetVars().Contains(key))
                            Console.WriteLine($"Error: Variable {key} cannot use itself");
                        else if (constants.ContainsKey(key))
                            Console.WriteLine($"Error: Cannot redefine {key}");
                        else ctx[key] = value;

                        if (AutoEval && FindMissingVariables(func, ctx).Count == 0)
                            HandleEval();
                    }
                    else
                    {
                        ParseInput(input, new Dictionary<Type, Action<ICmd>>()
                        {
                            { typeof(CopyCommand), cmd => HandleCopy((CopyCommand)cmd) },
                            { typeof(DropCommand), cmd => HandleDrop((DropCommand)cmd) },
                            { typeof(ClearCommand), cmd => HandleClear((ClearCommand)cmd) },
                            { typeof(SetCommand), cmd => HandleSet((SetCommand)cmd) },
                            { typeof(UnsetCommand), cmd => HandleUnset((UnsetCommand)cmd) },
                            { typeof(ListCommand), cmd => HandleList((ListCommand)cmd) },
                            { typeof(LoadCommand), cmd => HandleLoad((LoadCommand)cmd) },
                            { typeof(SaveCommand), cmd => HandleSave((SaveCommand)cmd) },
                            { typeof(StashCommand), cmd => HandleStash((StashCommand)cmd) },
                            { typeof(RestoreCommand), cmd => HandleRestore((RestoreCommand)cmd) },
                            { typeof(ModeCommand), cmd => HandleMode((ModeCommand)cmd) },
                            { typeof(SolveCommand), cmd => HandleSolve((SolveCommand)cmd) },
                            { typeof(GraphCommand), cmd => HandleGraph((GraphCommand)cmd) },
                            { typeof(EvalCommand), cmd => HandleEval((EvalCommand)cmd) }
                        });
                    }
                }
            }
        }

        #region Utilities

        private static void ParseInput(string input, Dictionary<Type, Action<ICmd>> bindings, Action<string>? fallback = null)
        {
            var types = bindings.Keys.ToArray();
            var result = Parser.ParseArguments(input.Split(" "), types);
            result
                .WithNotParsed(WithExceptionHandler<IEnumerable<Error>>(HandleException,
                    errors => HandleParseErrors(errors, result, types, fallback != null)))
                .WithParsed(WithExceptionHandler<ExitCommand>(HandleException, HandleExit));
            if (bindings.FirstOrDefault((entry) => entry.Key.IsInstanceOfType(result.Value)) is {Key: not null, Value: not null} pair)
                WithExceptionHandler<ICmd>(HandleException, (cmd) => pair.Value(cmd))((result.Value as ICmd)!);
            else if (fallback != null && result.Errors.All(e => e.Tag == ErrorType.BadVerbSelectedError))
                fallback(input);
        }

        private static void HandleParseErrors(IEnumerable<Error> obj, ParserResult<object> parserResult, Type[] types, bool skipOnBadVerb)
        {
            var errorText = string.Empty;
            foreach (var error in obj)
            {
                switch (error)
                {
                    case VersionRequestedError:
                        Console.WriteLine(VersionText);
                        return;
                    case HelpRequestedError:
                        ShowHelp(parserResult, types, null);
                        return;
                    case HelpVerbRequestedError hvre:
                        ShowHelp(parserResult, types, hvre.Matched ? hvre.Verb : null);
                        return;
                    case BadVerbSelectedError bvse:
                        if (skipOnBadVerb)
                            return;
                        errorText += $"\nInvalid command '{bvse.Token}'; type 'help' for a list of commands";
                        break;
                    case MissingRequiredOptionError mroe:
                        errorText += $"\nMissing required argument {mroe.NameInfo.NameText}";
                        break;
                    case BadFormatConversionError bfce:
                        errorText += $"\nUnable to parse argument {bfce.NameInfo.NameText}";
                        break;
                    default:
                        errorText += $"\nInternal {error}";
                        break;
                }
            }

            if (!string.IsNullOrEmpty(errorText))
                throw new Exception(errorText.Substring(1));
        }

        private static byte[] Read(Stream s, int len)
        {
            var buf = new byte[len];
            if (len != s.Read(buf, 0, len))
                return new byte[len];
            return buf;
        }

        private static string ConvertValuesToString(IEnumerable<KeyValuePair<string, Component>> values,
            Func<string, bool>? skip = null)
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

        private static MathContext[] CreateArgsFuncs(int start, params string[] args)
        {
            return args.ToList()
                .GetRange(start, args.Length - start)
                .Select(ParseFunc)
                .Select(fx => new MathContext(Current, fx))
                .ToArray();
        }

        internal static MathContext? LoadFunc(string name)
        {
            var path = Path.Combine(dir, name + FuncExt);
            if (!File.Exists(path))
            {
                Console.WriteLine($"Function with name {name} not found");
                return null;
            }

            var data = File.ReadAllText(path);
            var lnb = data.IndexOf("\n", StringComparison.Ordinal);
            var func = ParseFunc(lnb == -1 ? data : data.Substring(0, lnb));
            MathContext ctx;
            if (lnb != -1)
            {
                var vars = ConvertValuesFromString(data.Substring(lnb + 1, data.Length - lnb - 2));
                ctx = new MathContext(BaseContext, func);
                foreach (var (key, value) in vars)
                    ctx[key] = value;
            }
            else
            {
                ctx = new MathContext(BaseContext, func);
            }

            return ctx;
        }

        public static Component ParseFunc(string f)
        {
            var input = new AntlrInputStream(f);
            var lexer = new MathLexer(input);
            var tokens = new CommonTokenStream(lexer);
            var parser = new MathParser(tokens);
            return new MathCompiler().Visit(parser.expr());
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
            fs.Write(new byte[] { 0, 0, 0, 0 });
            var buf = Encoding.ASCII.GetBytes(unit.Repr);
            fs.Write(BitConverter.GetBytes(buf.Length));
            fs.Write(buf);
            fs.Write(new[] { (byte)'\n' });

            // equations
            string EvalToString(Unit unit, UnitRef other, UnitEvaluator eval)
            {
                return $"{unit.Repr}{eval.op switch {
                    Component.Operator.Multiply => '*',
                    Component.Operator.Divide => '/',
                    _ => throw new ArgumentOutOfRangeException() }}{eval.overrideY?.ToString() ?? other.Repr}={eval.output.Repr};";
            }

            foreach (var (other, _, eval) in unit.GetEvaluators())
                fs.Write(Encoding.ASCII.GetBytes(EvalToString(unit, other, eval) + '\n'));
            fs.Flush();
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

        private static void StartGraph(params MathContext[] funcs)
        {
            _graph?.Dispose();
            _graph = new Graph(funcs);
        }

        private static void DumpVariables(this MathContext ctx, MultiEntryTable.Entry? entry = null,
            bool shouldError = true, Func<string, bool>? includeVar = null)
        {
            includeVar ??= _ => true;
            Action<object, object> AddEntryData;
            TextTable table = null!;
            if (entry == null)
            {
                table = new TextTable(true, TextTable.LineMode.Unicode);
                var colTerm = table.AddColumn("Variable");
                var colExpr = table.AddColumn("Value", true);
                AddEntryData = (term, value) => table.AddRow().SetData(colTerm, term).SetData(colExpr, value);
            }
            else
            {
                AddEntryData = (term, value) => entry.Values.Add((term, value));
            }

            if (!ctx.Vars().Any())
            {
                if (shouldError)
                    Console.WriteLine("Error: No variables are set");
                return;
            }

            foreach (var (key, val) in ctx.Vars())
                if (includeVar(key))
                    AddEntryData(key, val);
            if (table != null != !SimplePrint)
                Console.Write(table);
        }

        private static void PrintResult(Component func, UnitResult result, MathContext ctx, bool shouldError = true,
            bool amend = false, bool printOnly = false)
        {
            if (!printOnly) ctx[0] = result; // push result to mem
            var entry = amend ? results.Values[^1] : results.AddEntry();
            if (!printOnly)
            {
                if (amend) entry.Values.Clear();
                ctx.DumpVariables(entry, shouldError, key => func.EnumerateVars().Contains(key));
                entry.Values.Add((func, result));
            }

            if (SimplePrint)
            {
                if (!printOnly)
                    Console.WriteLine($"\t{func}\t= {result}");
            }
            else
            {
                Console.Clear();
                Console.Write(results);
            }
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

        internal static double Evaluate(this Component.Operator op, double x, double y)
        {
            return op switch
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

        #endregion

        #region Command Handlers

        private static string VersionText = $"clmath @ {GetAssemblyVersion<MathContext>()}";

        private static void ShowHelp<T>(ParserResult<T> result, Type[] types, string? verb)
        {
            var helpText = CommandLine.Text.HelpText.AutoBuild(
                result, text => text, text => text, maxDisplayWidth: 120);
            helpText.MaximumDisplayWidth = Console.WindowWidth;
            helpText.Heading = VersionText;
            helpText.Copyright = "comroid";
            helpText.AddDashesToOption = false;
            helpText.AdditionalNewLineAfterOption = false;
            helpText.AddEnumValuesToHelpText = true;
            var verbs = verb == null ? types
                : types.Where(t => t.Name.StartsWith(verb, true, CultureInfo.InvariantCulture));
            helpText.AddOptions(result);
            helpText.AddVerbs(verbs.ToArray());
            Console.WriteLine(helpText);
        }

        private static void HandleExit(ExitCommand _)
        {
            _exiting = true;
        }

        private static void HandleCopy(CopyCommand cmd)
        { // todo: support for non Windows OS
            if (Environment.OSVersion.Platform != PlatformID.Win32NT
                && Environment.OSVersion.Platform != PlatformID.Win32Windows
                && Environment.OSVersion.Platform != PlatformID.Win32S
                && Environment.OSVersion.Platform != PlatformID.WinCE)
                throw new Exception("This command is only available on Windows machines");
            if (Current.Mem().Count() == 0)
                throw new Exception("No value in memory");
            var data = Current[0];
            var startInfo = new ProcessStartInfo("C:\\Windows\\System32\\cmd.exe")
                { Arguments = $"/k \"(echo {data}| clip) & exit\"" };
            var process = new Process() { StartInfo = startInfo };
            process.Start();
            process.WaitForExit();
        }

        private static void HandleDrop(DropCommand cmd)
        {
            Stack.Pop();
        }

        private static void HandleSet(SetCommand cmd)
        {
            var name = cmd.Variable;
            if (globalConstants.ContainsKey(name))
                throw new Exception("Cannot modify constant: " + name);
            constants[name] = double.Parse(cmd.Value);
            SaveConstants();
            Console.WriteLine($"Constant {name} was set");
        }

        private static void HandleUnset(UnsetCommand cmd)
        {
            var name = cmd.Variable;
            if (globalConstants.ContainsKey(name))
                throw new Exception("Cannot remove constant: " + name);
            if (!constants.Remove(name))
                throw new Exception("Could not remove constant: " + name);
            SaveConstants();
            Console.WriteLine($"Constant {name} was unset");
        }

        private static void HandleList(ListCommand cmd)
        {
            var table = new TextTable(lineMode: TextTable.LineMode.Unicode);
            string colIdText = "Name", colDataText = "Value";
            IEnumerable<(object id, object obj)> data;
            var c = 0;
            switch (cmd.Target)
            {
                case ListCommand.TargetType.vars:
                    data = Current.Vars().Select(entry => ((object)entry.Key, (object)entry.Value));
                    break;
                case ListCommand.TargetType.func:
                    data = Directory.EnumerateFiles(dir, '*' + FuncExt)
                        .Select(path => new FileInfo(path).Name)
                        .Select(name => name.Substring(0, name.IndexOf(FuncExt, StringComparison.Ordinal)))
                        .Select(name => ((object)name, (object)LoadFunc(name)?.function!));
                    break;
                case ListCommand.TargetType.constant:
                    data = constants.Select(entry => ((object)entry.Key, (object)entry.Value));
                    break;
                case ListCommand.TargetType.stack:
                    data = Stack.Where(it => !it.Root).Select(ctx => ((object)++c, (object)ctx.function!));
                    break;
                case ListCommand.TargetType.mem:
                    c = Current.Mem().Count();
                    data = Current.Mem().Select(comp => ((object)--c, (object)comp));
                    break;
                case ListCommand.TargetType.stash:
                    data = stash.Select(comp => ((object)++c, (object)comp));
                    break;
                case ListCommand.TargetType.enabled:
                    colDataText = "Unit ID";
                    data = Current.GetUnitPackages()
                        .SelectMany(pkg => pkg.values.Values)
                        .Select(unit => ((object)unit.Name, (object)unit));
                    break;
                case ListCommand.TargetType.packs:
                    colDataText = "Unit Count";
                    data = unitPackages.Values.Select(pkg => ((object)pkg.Name, (object)pkg.values.Count));
                    break;
                case ListCommand.TargetType.units:
                    colDataText = "Unit ID";
                    data = unitPackages.Values
                        .SelectMany(pkg => pkg.values.Values)
                        .Select(unit => ((object)unit.Name, (object)unit));
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(cmd.Target), cmd.Target, "Unknown list Target");
            }

            var colId = table.AddColumn(colIdText);
            var colData = table.AddColumn(colDataText);

            foreach (var (id, obj) in data)
                table.AddRow()
                    .SetData(colId, id)
                    .SetData(colData, obj);
            if (table.Rows.Count == 0)
                Console.WriteLine("Nothing to display");
            else Console.Write(table);
        }

        private static void HandleEdit(EditCommand cmd)
        {
            throw new NotImplementedException();
        }

        private static void HandleClear(ClearCommand cmd)
        {
            var ClearVars = Current.ClearVars;
            var ClearMemory = Current.ClearMem;
            var ClearStash = stash.Clear;
            var ClearStack = Stack.Clear;
            switch (cmd.Target)
            {
                case ClearCommand.TargetType.vars:
                    ClearVars();
                    break;
                case ClearCommand.TargetType.mem:
                    ClearMemory();
                    break;
                case ClearCommand.TargetType.stash:
                    ClearStash();
                    break;
                case ClearCommand.TargetType.stack:
                    ClearStack();
                    break;
                case ClearCommand.TargetType.all:
                    ClearVars();
                    ClearMemory();
                    ClearStash();
                    ClearStack();
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(cmd.Target), cmd.Target, "Unknown clear Target");
            }
        }

        private static void HandleLoad(LoadCommand cmd)
        {
            var ctx = LoadFunc(cmd.Target)!;
            Stack.Push(ctx);
            EvalMode(ctx);
        }

        private static void HandleSave(SaveCommand cmd)
        {
            // save function to file
            var name = cmd.Target;
            var func = Current.Function;
            var data = func.ToString();
            if (cmd.Detailed)
                data += $"\n{ConvertValuesToString(Current.Vars(), globalConstants.ContainsKey)}";
            var path = Path.Combine(dir, name + FuncExt);
            File.WriteAllText(path, data);
            Console.WriteLine($"Function saved as {name}");
        }

        private static void HandleRename(RenameCommand cmd)
        {
            var name = cmd.Target;
            var dest = cmd.Value;
            File.Move(Path.Combine(dir, name + FuncExt), Path.Combine(dir, dest + FuncExt));
            Console.WriteLine($"Function {name} renamed to {dest}");
        }

        private static void HandleDelete(DeleteCommand cmd)
        {
            var name = cmd.Target;
            File.Delete(Path.Combine(dir, name + FuncExt));
            Console.WriteLine($"Function {name} deleted");
        }

        private static void HandleEnable(EnableCommand cmd)
        {
            if (Current.EnabledUnitPacks.Add(cmd.Target))
                Console.WriteLine("Enabled unit pack " + cmd.Target);
        }

        private static void HandleDisable(DisableCommand cmd)
        {
            if (Current.EnabledUnitPacks.Remove(cmd.Target))
                Console.WriteLine("Disabled unit pack " + cmd.Target);
        }

        private static void HandleMode(ModeCommand cmd)
        {
            DRG = cmd.Target;
        }

        private static void HandleStash(StashCommand cmd)
        {
            stash.Push(Stack.Pop());
        }

        private static void HandleRestore(RestoreCommand cmd)
        {
            Stack.Push(stash.Pop());
        }

        private static void HandleEval(EvalCommand? _ = null)
        {
            var ctx = Current;
            var func = ctx.Function;
            var missing = FindMissingVariables(func, ctx);
            if (missing.Count > 0)
            {
                DumpVariables(ctx, includeVar: key => func.EnumerateVars().Contains(key));
                Console.WriteLine(
                    $"Error: Missing variable{(missing.Count != 1 ? "s" : "")} {string.Join(", ", missing)}");
            }
            else
            {
                PrintResult(func, func.Evaluate(ctx), ctx);
            }
        }

        private static void HandleSolve(SolveCommand cmd)
        {
            var ctx = cmd.Function == null ? Current : CreateArgsFuncs(0, cmd.Function)[0];
            var lhs = new Component { type = Component.Type.Var, arg = cmd.LHS };
            var target = cmd.For;
            var count = ctx.Function.GetVars().Count(x => x == target);
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

            var result = new Solver(cmd.Verbose).Solve(ctx.Function, lhs, target);
            EvalMode(new MathContext(ctx, result));
        }

        private static void HandleGraph(GraphCommand cmd)
        {
            StartGraph(CreateArgsFuncs(0,
                new[]
                {
                    cmd.Function, cmd.Function2, cmd.Function3, cmd.Function4,
                    cmd.Function5, cmd.Function6, cmd.Function7
                }.Where(it => it != null).ToArray()));
        }

        private static void HandleException(Exception e)
        {
            Console.WriteLine("Error: " + (string.IsNullOrEmpty(e.Message) ? e.GetType().Name : e.Message));
            Debug.WriteLine(e);
        }

        #endregion
    }

    internal class MultiEntryTable : TextTable
    {
        private readonly Column ColTerm;
        private readonly Column ColValue;
        public readonly List<Entry> Values = new();

        public MultiEntryTable() : base(true, LineMode.Unicode)
        {
            ColTerm = AddColumn("Term");
            ColValue = AddColumn("Value");
        }

        public override List<Row> Rows
        {
            get
            {
                IEnumerable<Row> yield = ArraySegment<Row>.Empty;
                foreach (var entry in Values)
                    yield = yield.Concat(entry.Rows).Append(new SeparatorRow { Detail = LineType.Bold });
                var list = yield.ToList();
                return list.GetRange(0, list.Count - 1);
            }
        }

        public Entry AddEntry()
        {
            var entry = new Entry(this);
            Values.Add(entry);
            return entry;
        }

        public class Entry
        {
            private readonly MultiEntryTable _table;
            public readonly List<(object term, object value)> Values = new();

            internal Entry(MultiEntryTable table)
            {
                _table = table;
            }

            public IEnumerable<Row> Rows
            {
                get
                {
                    var rows = new List<Row>();

                    for (var i = 0; i < Math.Max(Values.Count - 1, 1); i++)
                    {
                        var val = Values[i];
                        rows.Add(new Row()
                            .SetData(_table.ColTerm, val.term)
                            .SetData(_table.ColValue, val.value));
                    }

                    if (Values.Count > 1)
                    {
                        var val = Values[^1];
                        rows.Add(new SeparatorRow());
                        rows.Add(new Row()
                            .SetData(_table.ColTerm, val.term)
                            .SetData(_table.ColValue, val.value));
                    }

                    return rows;
                }
            }
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
        private readonly MathContext _parent;
        public readonly HashSet<string> EnabledUnitPacks = new();
        public readonly Component? function;
        private readonly Stack<UnitResult> mem = new();
        private readonly Dictionary<string, Component> var = new();

        public MathContext(MathContext parent, Component? function = null)
        {
            _parent = parent;
            this.function = function ?? parent?.function;
            foreach (var enabled in parent?.EnabledUnitPacks ?? ArraySegment<string>.Empty.AsEnumerable())
                EnabledUnitPacks.Add(enabled);
        }

        public bool Root => _parent == null && function == null;
        public Component Function => function ?? throw new Exception("No Function in context");

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
                else
                {
                    var[key] = value;
                }
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

        public IEnumerable<KeyValuePair<string, Component>> Vars()
        {
            return var.Concat(_parent?.Vars() ?? ArraySegment<KeyValuePair<string, Component>>.Empty);
        }

        public void ClearVars()
        {
            var.Clear();
        }

        public IEnumerable<UnitResult> Mem()
        {
            return mem.Concat(_parent?.Mem() ?? ArraySegment<UnitResult>.Empty);
        }

        public void ClearMem()
        {
            mem.Clear();
        }

        public UnitPackage[] GetUnitPackages()
        {
            return Program.unitPackages
                .Where(pkg => EnabledUnitPacks.Contains(pkg.Key))
                .Select(pkg => pkg.Value)
                .ToArray();
        }

        public Unit? FindUnit(string id)
        {
            return GetUnitPackages().SelectMany(x => x.values.Values).FirstOrDefault(x => x.Repr == id);
        }
    }
}