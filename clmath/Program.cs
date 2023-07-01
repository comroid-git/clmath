using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Antlr4.Runtime;
using Antlr4.Runtime.Tree;
using clmath.Antlr;
using CommandLine;
using CommandLine.Text;
using comroid.common;
using JetBrains.Annotations;
using static comroid.common.DebugUtil;
using Parser = CommandLine.Parser;

// ReSharper disable once ArrangeNamespaceBody
namespace clmath;

public static class Program
{
    public static void Main(params string[] args)
    {
        ParseInput(string.Join(" ", args), new Dictionary<Type, Action<ICmd>>
        {
            { typeof(ListCommand), cmd => HandleList((ListCommand)cmd) },
            { typeof(EditCommand), cmd => HandleEditConfig((EditCommand)cmd) },
            { typeof(SetCommand), cmd => HandleSetConst((SetCommand)cmd) },
            { typeof(UnsetCommand), cmd => HandleUnsetConst((UnsetCommand)cmd) },
            { typeof(CopyCommand), cmd => HandleCopy((CopyCommand)cmd) },
            { typeof(LoadCommand), cmd => HandleLoad((LoadCommand)cmd) },
            { typeof(RenameCommand), cmd => HandleRename((RenameCommand)cmd) },
            { typeof(DeleteCommand), cmd => HandleDelete((DeleteCommand)cmd) },
            { typeof(RestoreCommand), cmd => HandleRestore((RestoreCommand)cmd) },
            { typeof(ClearCommand), cmd => HandleClear((ClearCommand)cmd) },
            { typeof(ModeCommand), cmd => HandleMode((ModeCommand)cmd) },
            { typeof(SolveCommand), cmd => HandleSolve((SolveCommand)cmd) },
            { typeof(GraphCommand), cmd => HandleGraph((GraphCommand)cmd) }
        }, input =>
        {
            var component = Parse(input, p => p.input());
            switch (component)
            {
                case {type: Component.Type.Equation}:
                    log.Error("not implemented");
                    break;
                case {type: Component.Type.Declaration}:
                    BaseContext.Vars(component.arg!.ToString()!, component);
                    break;
                case {type: Component.Type.Target}:
                    BaseContext.Target = component.arg!.ToString();
                    break;
                default:
                    var missing = FindMissingVariables(component, BaseContext);
                    if (missing.Count > 0)
                    {
                        log.Error("Missing variables: " + string.Join(", ", missing));
                        break;
                    }
                
                    var result = component.Evaluate(BaseContext);
                    PrintResult(component, result, BaseContext);
                    break;
            }
        });
    }

    #region Commands
    
    private static void HandleCopy(CopyCommand cmd)
    {
        // todo: support for non Windows OS
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
        var process = new Process { StartInfo = startInfo };
        process.Start();
        process.WaitForExit();
    }

    private static void HandleDrop(DropCommand cmd)
    {
        if (Current.Root)
            throw new Exception("Stack is empty");
        switch (cmd.Target ?? "one")
        {
            case "one":
                Stack.Pop();
                break;
            case "all":
                Stack.Clear();
                break;
            case not null:
                if (!Regex.IsMatch(cmd.Target!, "\\d+"))
                    break;
                var n = int.Parse(cmd.Target!);
                for (var i = n; i > 0; i--)
                    if (Current.Root)
                        break;
                    else Stack.Pop();
                break;
        }
        if (Stack.Count == 0)
            Stack.Push(BaseContext);
    }

    private static void HandleSetConst(SetCommand cmd)
    {
        var name = cmd.Variable;
        if (globalConstants.ContainsKey(name))
            throw new Exception("Cannot modify constant: " + name);
        constants[name] = double.Parse(cmd.Value);
        SaveConstants();
        Console.WriteLine($"Constant {name} was set");
    }

    private static void HandleUnsetConst(UnsetCommand cmd)
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
        if (cmd.Target == ListCommand.TargetType.results)
        {
            if (AutoClear)
                Console.Clear();
            Console.Write(results);
            return;
        }
        var table = new TextTable { Lines = LineMode };
        string colIdText = "Name", colDataText = "Value";
        IEnumerable<(object id, object obj)> data;
        var c = 0;
        switch (cmd.Target)
        {
            case ListCommand.TargetType.vars:
                data = Current.Vars().Concat((Current.function?.Vars() ?? ArraySegment<string>.Empty)
                        .SelectMany(name =>
                        {
                            if (Current.Vars().FirstOrDefault(var => var.Key == name).Value is { } comp)
                                return comp.Vars();
                            return new[] { name };
                        })
                        .Select(name => new KeyValuePair<string, Component>(name,
                            new Component { type = Component.Type.EvalVar, arg = "unset" })))
                    .DistinctBy(entry => entry.Key)
                    .Where(entry => !constants.ContainsKey(entry.Key))
                    .Select(entry => ((object)entry.Key, (object)entry.Value));
                break;
            case ListCommand.TargetType.func:
                colDataText = "Term";
                data = Directory.EnumerateFiles(dir, '*' + FuncExt)
                    .Select(path => new FileInfo(path).Name)
                    .Select(name => name.StripExtension(FuncExt))
                    .Select(name => ((object)name, (object)LoadFunc(name)?.function!));
                break;
            case ListCommand.TargetType.constant:
                data = constants.Select(entry => ((object)entry.Key, (object)entry.Value));
                break;
            case ListCommand.TargetType.stack:
                colIdText = "Index";
                colDataText = "Term";
                data = Stack.Where(it => !it.Root).Select(ctx => ((object)c++, (object)ctx.function!));
                break;
            case ListCommand.TargetType.mem:
                colIdText = "Index";
                colDataText = "Value";
                data = Current.Mem().Reverse().Select(comp => ((object)c++, (object)comp));
                break;
            case ListCommand.TargetType.stash:
                colIdText = "Index";
                colDataText = "Term";
                data = stash.Select(comp => ((object)c++, (object)comp.function!));
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(cmd.Target), cmd.Target, "Unknown list Target");
        }
        data = data.OrderBy(entry => entry.id.ToString()!, Comparer<string>.Default);

        var colId = table.AddColumn(colIdText);
        var colData = table.AddColumn(colDataText);

        foreach (var (id, obj) in data)
            table.AddRow()
                .SetData(colId, id)
                .SetData(colData, obj);
        if (table.Rows.Count == 0)
            Console.WriteLine("Nothing to display");
        else
        {
            if (AutoClear)
                Console.Clear();
            Console.Write(table);
        }
    }

    private static void HandleEditConfig(EditCommand cmd)
    {
        editTarget = cmd.Target switch
        {
            EditCommand.TargetType.config => cmd.Value,
            _ => string.Empty
        };
        //EditMode();
    }

    private static void HandleGetConfig(GetCommand cmd)
    {
        var table = new TextTable { Lines = LineMode, Title = "Configuration Values" };
        var colName = table.AddColumn("Name");
        var colValue = table.AddColumn("Value");
        TextTable.Column? colExtra = null;

        //if (editTarget is not string && cmd.Variable is null)
        //    throw new Exception("'get' not supported by target " + editTarget?.GetType().Name);
        IEnumerable<(string name, string value, string extra)> data;
        switch (editTarget)
        {
            default:
                colExtra = table.AddColumn("Type");
                data = Config.Entries.Select(entry => (entry.Key, entry.Value.ConvertOutput(), entry.Value.Type.FullName!));
                break;
        }
        data = data.OrderBy(entry => entry.name.ToString()!, Comparer<string>.Default);
        if ((cmd.Variable ?? editTarget as string) is { } ovr)
            data = data.Where(entry => entry.name == ovr);

        foreach (var (key, value, extra) in data)
        {
            var row = table.AddRow()
                .SetData(colName, key)
                .SetData(colValue, value);
            if (colExtra != null && !string.IsNullOrEmpty(extra))
                row.SetData(colExtra, extra);
        }

        Console.Write(table);
    }

    private static void HandleSetConfig(SetCommand cmd)
    {
        if (editTarget is not string && cmd.Variable is null)
            throw new Exception("'get' not supported by target " + editTarget);

        var key = cmd.Variable ?? editTarget as string ?? throw new Exception("No key defined");
        var value = (editTarget as string == key ? cmd.Variable : cmd.Value) ??
                    throw new Exception("No value found");
        Config.Entries[key].Set(value);
        SaveConfig();

        Console.WriteLine($"Config variable '{key}' was set to '{value}'");
    }

    private static void HandleUnsetConfig(UnsetCommand cmd)
    {
        if (editTarget is not string && cmd.Variable is null)
            throw new Exception("'get' not supported by target " + editTarget);

        var key = cmd.Variable ?? editTarget as string ?? throw new Exception("No key defined");
        var entry = Config.Entries[key];
        entry.Value = entry.DefaultValue;
        SaveConfig();

        Console.WriteLine($"Config variable '{key}' was reset to '{entry.DefaultValue}'");
    }

    private static void HandleAddConfig(AddCommand cmd)
    {
        switch (editTarget)
        {
            default:
                if (cmd.Target is not { } name3)
                    throw new Exception("No array name provided");
                if (cmd.Value is not { } value3)
                    throw new Exception("No value provided");
                var entry = Config.Entries[name3];
                if (!entry.Type.IsArray || !typeof(string).IsAssignableFrom(entry.Type.GetElementType()))
                    throw new Exception("Variable is not string[]");
                //Current.EnabledUnitPacks.Add(value3);
                SaveConfig();
                break;
        }
    }

    private static void HandleRemoveConfig(RemoveCommand cmd)
    {
        bool NotConfirmed(string task)
        {
            Console.Write($"Warning: Do you really want to {task}? This action cannot be undone. [y/n] ");
            return Console.Read() != 'y';
        }

        switch (editTarget)
        {
            default:
                if (cmd.Target is not { } name3)
                    throw new Exception("No array name provided");
                if (cmd.Value is not { } value3)
                    throw new Exception("No value provided");
                var entry = Config.Entries[name3];
                if (!entry.Type.IsArray || typeof(string).IsAssignableFrom(entry.Type.GetElementType()))
                    throw new Exception("Variable is not string[]");
                //Current.EnabledUnitPacks.Remove(value3);
                SaveConfig();
                break;
        }
    }

    private static void HandleClear(ClearCommand cmd)
    {
        if ((cmd.Target & ClearCommand.TargetType.screen) != 0)
            Console.Clear();
        if ((cmd.Target & ClearCommand.TargetType.vars) != 0)
            Current.ClearVars();
        if ((cmd.Target & ClearCommand.TargetType.mem) != 0)
            Current.ClearMem();
        if ((cmd.Target & ClearCommand.TargetType.stash) != 0)
            stash.Clear();
        if ((cmd.Target & ClearCommand.TargetType.results) != 0)
            results.Values.Clear();
        if ((cmd.Target & ClearCommand.TargetType.stack) != 0)
        {
            Stack.Clear();
            Stack.Push(BaseContext);
        }
    }

    private static void HandleLoad(LoadCommand cmd)
    {
        var ctx = LoadFunc(cmd.Target)!;
        BaseContext.function = ctx.function;
        //EvalMode();
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

    private static void HandleMode(ModeCommand cmd)
    {
        DRG = cmd.Target;
    }

    private static void HandleStash(StashCommand cmd)
    {
        if (Current.Root)
            throw new Exception("Stack is empty");
        stash.Push(Stack.Pop());
    }

    private static void HandleRestore(RestoreCommand cmd)
    {
        Stack.Push(stash.Pop());
    }

    private static void HandleSolve(SolveCommand cmd)
    {
        var ctx = cmd.Function == null ? Current : CreateArgsFuncs(0, cmd.Function)[0];
        var lhs = new Component { type = Component.Type.Var, arg = cmd.LHS };
        var target = cmd.For;
        var count = ctx.Function.Vars().Count(x => x == target);
        if (count == 0)
        {
            log.Error($"Variable {target} was not found in function");
            return;
        }

        if (count > 1)
        {
            log.Error($"Variable {target} was found more than once");
            return;
        }

        var result = new Solver(cmd.Verbose).Solve(ctx.Function, lhs, target);
        Stack.Push(new MathContext(ctx, result));
    }

    private static void HandleGraph(GraphCommand cmd)
    {
        _graph?.Dispose();
        _graph = new Graph(Stack
            .Where(ctx => ctx.function != null
                          && ctx.Function.Vars().Count(var => !constants.ContainsKey(var)) == 1
                          && ctx.Vars().All(pair => globalConstants.ContainsKey(pair.Key)))
            .Concat(CreateArgsFuncs(0, new[]
            {
                cmd.Function, cmd.Function2, cmd.Function3, cmd.Function4,
                cmd.Function5, cmd.Function6, cmd.Function7
            }.Where(it => it != null).ToArray()))
            .ToArray());
    }

    #endregion
    
    #region Fields
    public static readonly Log log = new("clmath");
    private const double factorD2R = Math.PI / 180;
    private const double factorD2G = 1.111111111;
    public static readonly Random RNG = new();
    public static readonly MathContext BaseContext;
    public static readonly Stack<MathContext> Stack;
    public static bool SimplePrint = false;
    private static readonly string FuncExt = ".math";
    private static readonly string ConstExt = ".vars";

    internal static readonly string dir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
        "comroid", "clmath");

    private static readonly string constantsFile = Path.Combine(dir, "constants" + ConstExt);
    private static readonly string configFile = Path.Combine(dir, "config.json");

    public static Parser Parser;
    private static readonly int ConfigVersion;
    private static bool _exiting;
    private static bool _resetting;
    private static Graph? _graph;

    private static readonly Dictionary<string, double> globalConstants = new()
    {
        { "pi", Math.PI },
        { "e", Math.E },
        { "tau", Math.PI * 2 },
        { "rng_i", double.NaN },
        { "rng_d", double.NaN }
    };

    private static readonly Stack<MathContext> stash = new();
    private static readonly MultiEntryTable results;

    private static object? editTarget;

    public static MathContext Current
    {
        get => Stack.Peek();
    }

    public static readonly ConfigBase Config = new()
    {
        { "autoEval", Convert.ToBoolean, () => true },
        { "autoClear", Convert.ToBoolean, () => true },
        {
            "drg", key =>
            {
                Enum.TryParse<CalcMode>(key, out var result);
                return result;
            },
            () => CalcMode.Deg
        },
        {
            "lineMode", key =>
            {
                Enum.TryParse<TextTable.LineMode>(key, out var result);
                return result;
            },
            () => TextTable.LineMode.Unicode
        },
        {
            "output", key =>
            {
                Enum.TryParse<Component.OutputType>(key, out var result);
                return result;
            },
            () => Component.OutputType.Text
        }
    };

    public static CalcMode DRG
    {
        get => Config.Get<CalcMode>("drg");
        set => Config["drg"] = value;
    }
    public static bool AutoEval
    {
        get => Config.Get<bool>("autoEval");
        set => Config["autoEval"] = value;
    }
    public static bool AutoClear
    {
        get => Config.Get<bool>("autoClear");
        set => Config["autoClear"] = value;
    }
    public static TextTable.LineMode LineMode
    {
        get => Config.Get<TextTable.LineMode>("lineMode");
        set => Config["lineMode"] = value;
    }

    public static Component.OutputType Output
    {
        get => Config.Get<Component.OutputType>("output");
        set => Config["output"] = value;
    }

    public static Dictionary<string, double> constants { get; private set; } = null!;

    #endregion

    #region Lifecycle Methods

    static Program()
    {
        BaseContext = new MathContext(null!, null);
        Stack = new Stack<MathContext>();
        Stack.Push(BaseContext);
        Parser = new Parser(cfg =>
        {
            cfg.CaseSensitive = false;
            cfg.CaseInsensitiveEnumValues = true;
            cfg.HelpWriter = Console.Out;
            cfg.IgnoreUnknownArguments = true;
            cfg.AutoHelp = true;
            cfg.AutoVersion = true;
            cfg.ParsingCulture = CultureInfo.InvariantCulture;
            cfg.EnableDashDash = false;
            cfg.MaximumDisplayWidth = log.RunWithExceptionLogger(() => Console.WindowWidth, "Could not get Console Width", _ => 1024, LogLevel.Debug);
        });
        ConfigVersion = FileVersionInfo.GetVersionInfo(AppContext.BaseDirectory + "clmath" + (OperatingSystem.IsWindows() ? ".exe" : string.Empty)).FileMajorPart;
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
        results = new() { Lines = LineMode };
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
            constants[key] = value.Evaluate(null).Value; // todo: use UnitValue in constants?
    }

    [RegexPattern] private static readonly string OldDescriptorPattern = "([pq]):\\s(\\w+),(\\w+)";

    private static void SaveConfig()
    {
        Config.Save(configFile);
    }

    private static bool LoadConfig()
    {
        try
        {
            Config.Load(configFile);
        }
        catch
        {
            log.Config("Configuration could not be loaded; it was reset to defaults.");
            return false;
        }

        return true;
    }

    private static void Save()
    {
        SaveConfig();
        SaveConstants();
    }

    #endregion

    #region Utilities

    private static bool ParseInput(string input, Dictionary<Type, Action<ICmd>> bindings, Action<string>? fallback = null)
    {
        var types = bindings.Keys.Append(typeof(ExitCommand)).ToArray();
        var result = Parser.ParseArguments(input.Split(" "), types);
        var exitRequested = false;
        result
            .WithNotParsed(WithExceptionHandler<IEnumerable<Error>>(HandleException,
                errors => HandleParseErrors(errors, result, types, fallback != null)))
            .WithParsed(WithExceptionHandler<ExitCommand>(HandleException, _ => exitRequested = true));
        if (bindings.FirstOrDefault(entry => entry.Key.IsInstanceOfType(result.Value)) is { Key: not null, Value: not null } pair)
            WithExceptionHandler<ICmd>(HandleException, cmd => pair.Value(cmd))((result.Value as ICmd)!);
        else if (fallback != null && result.Errors.All(e => e.Tag == ErrorType.BadVerbSelectedError))
            WithExceptionHandler(HandleException, fallback)(input);
        return exitRequested;
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

    private static readonly string VersionText = $"clmath @ {GetAssemblyVersion<MathContext>()}";

    private static void ShowHelp<T>(ParserResult<T> result, Type[] types, string? verb)
    {
        var helpText = HelpText.AutoBuild(
            result, helpText =>
            {
                helpText.MaximumDisplayWidth = Console.WindowWidth;
                helpText.Heading = VersionText;
                helpText.Copyright = "comroid";
                helpText.AddDashesToOption = false;
                helpText.AdditionalNewLineAfterOption = false;
                helpText.AddEnumValuesToHelpText = true;
                helpText.AutoVersion = false;
                return HelpText.DefaultParsingErrorsHandler(result, helpText);
            }, e => e, maxDisplayWidth: 120);
        var verbs = verb == null
            ? types
            : types.Where(t => t.Name.StartsWith(verb, true, CultureInfo.InvariantCulture));
        helpText.AddOptions(result);
        helpText.AddVerbs(verbs.ToArray());
        Console.WriteLine(helpText);
    }
    
    private static void HandleException(Exception e)
    {
        log.Error(string.IsNullOrEmpty(e.Message) ? e.GetType().Name : e.Message, e);
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
        var value = Parse(matcher.Groups[2].Value, p=>p.expr());
        return (key, value);
    }

    private static MathContext[] CreateArgsFuncs(int start, params string[] args)
    {
        return args.ToList()
            .GetRange(start, args.Length - start)
            .Select(txt=>Parse(txt,p=>p.expr()))
            .Select(fx => new MathContext(Current, fx))
            .ToArray();
    }

    internal static MathContext? LoadFunc(string name)
    {
        var path = Path.Combine(dir, name + FuncExt);
        if (!File.Exists(path))
        {
            log.Error($"Function with name {name} not found");
            return null;
        }

        var data = File.ReadAllText(path);
        var lnb = data.IndexOf("\n", StringComparison.Ordinal);
        var func = Parse(lnb == -1 ? data : data.Substring(0, lnb),p=>p.expr());
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

    public static Component Parse(string expr, Func<MathParser, IParseTree> output)
    {
        var input = new AntlrInputStream(expr);
        var lexer = new MathLexer(input);
        var tokens = new CommonTokenStream(lexer);
        var parser = new MathParser(tokens) { ErrorHandler = new BailErrorStrategy() };
        return new MathCompiler().Visit(output(parser));
    }

    private static List<string> FindMissingVariables(Component func, MathContext ctx)
    {
        var missing = new List<string>();
        foreach (var var in ctx.Vars()
                     .Select(x => x.Value)
                     .Append(func)
                     .SelectMany(it => it.Vars())
                     .Distinct())
            if (ctx.Vars().All(x => x.Key != var))
                missing.Add(var);
        missing.RemoveAll(constants.ContainsKey);
        return missing;
    }

    private static void DumpVariables(this MathContext ctx, MultiEntryTable.Entry? entry = null,
        bool shouldError = true, Func<string, bool>? includeVar = null)
    {
        includeVar ??= _ => true;
        Action<object, object> AddEntryData;
        TextTable table = null!;
        if (entry == null)
        {
            table = new TextTable { Lines = LineMode };
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
                log.Error("No variables are set");
            return;
        }

        foreach (var (key, val) in ctx.Vars())
            if (includeVar(key))
                AddEntryData(key, val);
        if (table != null != !SimplePrint)
            Console.Write(table);
    }

    private static void PrintResult(Component func, UnitValue result, MathContext ctx, bool shouldError = true,
        bool printOnly = false)
    {
        if (!printOnly) ctx[0] = result; // push result to mem
        var amend = results.Values.Count > 0 && results.Values[^1].Values[^1].term.ToString() == func.ToString();
        var entry = amend ? results.Values[^1] : results.AddEntry();
        if (!printOnly)
        {
            if (amend) entry.Values.Clear();
            ctx.DumpVariables(entry, shouldError, key => func.Vars()
                .SelectMany(name =>
                {
                    if (Current.Vars().FirstOrDefault(var => var.Key == name).Value is { } comp)
                        return comp.Vars().Append(name);
                    return new[] { name };
                }).Contains(key));
            entry.Values.Add((func, result));
        }

        if (SimplePrint)
        {
            if (!printOnly)
                Console.WriteLine($"\t{func}\t= {result}");
        }
        else
        {
            if (AutoClear)
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

    private static string StripExtension(this string str, string ext)
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
}

internal class MultiEntryTable : TextTable
{
    private readonly Column ColTerm;
    private readonly Column ColValue;
    public readonly List<Entry> Values = new();

    public MultiEntryTable()
    {
        Lines = Program.LineMode;
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
    public readonly Component? function;
    private readonly Stack<UnitValue> mem = new();
    private readonly Dictionary<string, Component> var = new();
    public string? Target { get; set; }
    
    public MathContext(MathContext parent, Component? function)
    {
        _parent = parent;
        this.function = function;
    }

    public bool Root
    {
        get => _parent == null && function == null;
    }
    public Component Function
    {
        get => function ?? throw new Exception("No Function in context");
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
            else
            {
                var[key] = value;
            }
        }
    }

    public UnitValue this[int i]
    {
        get => i < mem.Count ? mem.ToArray()[i] : _parent?[i - mem.Count] ?? Units.EmptyValue;
        set
        {
            if (mem.Count == 0 || mem.Peek() != value)
                mem.Push(value);
        }
    }

    public IEnumerable<KeyValuePair<string, Component>> Vars() => var.Concat(_parent?.Vars() ?? ArraySegment<KeyValuePair<string, Component>>.Empty);
    public void Vars(string name, Component value) => var[name] = value;

    public void ClearVars()
    {
        var.Clear();
    }

    public IEnumerable<UnitValue> Mem() => mem.Concat(_parent?.Mem() ?? ArraySegment<UnitValue>.Empty);

    public void ClearMem()
    {
        mem.Clear();
    }
}