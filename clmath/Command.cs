using CommandLine;

#pragma warning disable CS8618

namespace clmath;

public interface ICmd
{
    [Option('v', "verbose", Required = false, Default = false, HelpText = "Print verbose information, if applicable")]
    public bool Verbose { get; set; }
}

public interface IVariableCmd<T> : ICmd
{
    [Value(0, MetaName = "Variable", Required = false, Default = default, HelpText = "The variable to work with")]
    public T? Variable { get; set; }
}

public interface ITargetCmd<T> : ICmd
{
    [Value(0, MetaName = "Target", Required = false, Default = default, HelpText = "The target to use")]
    public T? Target { get; set; }
}

public interface IAcceptValueCmd<T> : ICmd
{
    [Value(1, MetaName = "Value", Required = false, Default = default, HelpText = "The new value")]
    public T? Value { get; set; }
}

#region Generic

[Verb("exit", HelpText = "Closes the program")]
public class ExitCommand : ICmd
{
    public bool Verbose { get; set; }
}

[Verb("copy", HelpText = "Copies the last result into the clipboard (Windows only)")]
public class CopyCommand : ICmd
{
    public bool Verbose { get; set; }
}

// configuration related
[Verb("list", HelpText = "List things by category")]
public class ListCommand : ITargetCmd<ListCommand.TargetType>
{
    public enum TargetType
    {
        vars = default,
        results,
        func,
        constant,
        stack,
        mem,
        stash
    }

    public bool Verbose { get; set; }
    public TargetType Target { get; set; }
}

[Verb("mode", HelpText = "DRG Mode to use for calculation")]
public class ModeCommand : ITargetCmd<CalcMode>
{
    public bool Verbose { get; set; }
    public CalcMode Target { get; set; }
}

#endregion

#region Editor Related

[Verb("load", HelpText = "Load a function from disk")]
public class LoadCommand : ITargetCmd<string>
{
    public bool Verbose { get; set; }
    public string Target { get; set; }
}

[Verb("save", HelpText = "Save a function to disk")]
public class SaveCommand : ITargetCmd<string>
{
    [Option('d', "detailed", HelpText = "Save with all current variables")]
    public bool Detailed { get; set; }

    public bool Verbose { get; set; }
    public string Target { get; set; }
}

[Verb("rename", HelpText = "Rename a function")]
public class RenameCommand : ITargetCmd<string>, IAcceptValueCmd<string>
{
    public string Value { get; set; }
    public bool Verbose { get; set; }
    public string Target { get; set; }
}

[Verb("delete", HelpText = "Delete a function")]
public class DeleteCommand : ITargetCmd<string>
{
    public bool Verbose { get; set; }
    public string Target { get; set; }
}

[Verb("clear", HelpText = "Clear things by category")]
public class ClearCommand : ITargetCmd<ClearCommand.TargetType>
{
    [Flags]
    public enum TargetType : byte
    {
        screen = 1 << 1,
        vars = 1 << 2,
        mem = 1 << 3,
        stash = 1 << 4,
        stack = 1 << 5,
        results = 1 << 6,
        all = 255
    }

    public bool Verbose { get; set; }
    public TargetType Target { get; set; }
}

[Verb("drop", HelpText = "Drop the current context")]
public class DropCommand : ITargetCmd<string>
{
    public bool Verbose { get; set; }
    public string Target { get; set; }
}

[Verb("stash", HelpText = "Store a function in stash")]
public class StashCommand : ITargetCmd<string>
{
    public bool Verbose { get; set; }
    public string Target { get; set; }
}

[Verb("restore", HelpText = "Restore a function from stash")]
public class RestoreCommand : ITargetCmd<string>
{
    public bool Verbose { get; set; }
    public string Target { get; set; }
}

[Verb("eval", HelpText = "Re-run calculation")]
public class EvalCommand : ICmd
{
    public bool Verbose { get; set; }
}

#endregion

#region Config Related

[Verb("edit", aliases: new[] { "config" }, HelpText = "Edit configuration")]
public class EditCommand : ITargetCmd<EditCommand.TargetType>, IAcceptValueCmd<string>
{
    public enum TargetType
    {
        config
    }

    public string? Value { get; set; }

    public bool Verbose { get; set; }
    public TargetType Target { get; set; }
}

[Verb("get", HelpText = "Get a value")]
public class GetCommand : IVariableCmd<string>, IAcceptValueCmd<string>
{
    public string? Value { get; set; }
    public bool Verbose { get; set; }
    public string? Variable { get; set; }
}

[Verb("set", HelpText = "Set a value")]
public class SetCommand : IVariableCmd<string>, IAcceptValueCmd<string>
{
    public string? Value { get; set; }
    public bool Verbose { get; set; }
    public string? Variable { get; set; }
}

[Verb("unset", HelpText = "Unset a value")]
public class UnsetCommand : IVariableCmd<string>
{
    public bool Verbose { get; set; }
    public string? Variable { get; set; }
}

[Verb("add", HelpText = "Add a value to a target")]
public class AddCommand : ITargetCmd<string>, IAcceptValueCmd<string>
{
    public string Value { get; set; }
    public bool Verbose { get; set; }
    public string Target { get; set; }
}

[Verb("remove", HelpText = "Remove a value to a target")]
public class RemoveCommand : ITargetCmd<string>, IAcceptValueCmd<string>
{
    public string Value { get; set; }
    public bool Verbose { get; set; }
    public string Target { get; set; }
}

#endregion

#region Tools

[Verb("graph", HelpText = "Display functions on a 2D Graph")]
public class GraphCommand : ICmd
{
    [Option('n', "variable", HelpText = "The variable name to use when unsure")]
    public string Variable { get; set; }

    [Value(0, MetaName = "Function1", Required = false, HelpText = "The first function to display")]
    public string Function { get; set; }

    [Value(1, MetaName = "Function2", Required = false, HelpText = "The second function to display")]
    public string Function2 { get; set; }

    [Value(2, MetaName = "Function3", Required = false, Hidden = true, HelpText = "The third function to display")]
    public string Function3 { get; set; }

    [Value(3, MetaName = "Function4", Required = false, Hidden = true, HelpText = "The fourth function to display")]
    public string Function4 { get; set; }

    [Value(4, MetaName = "Function5", Required = false, Hidden = true, HelpText = "The fifth function to display")]
    public string Function5 { get; set; }

    [Value(5, MetaName = "Function6", Required = false, Hidden = true, HelpText = "The sixth function to display")]
    public string Function6 { get; set; }

    [Value(6, MetaName = "Function7", Required = false, HelpText = "The seventh function to display")]
    public string Function7 { get; set; }

    public bool Verbose { get; set; }
}

[Verb("solve", HelpText = "Solve an equation for a variable")]
public class SolveCommand : ICmd
{
    [Value(0, MetaName = "For", Required = true, HelpText = "The variable to solve for")]
    public string For { get; set; }

    [Value(1, MetaName = "LHS", Required = true, HelpText = "The left hand side of the equation; must be a singular variable")]
    public string LHS { get; set; }

    [Value(2, MetaName = "Function", Required = false, HelpText = "The function to solve")]
    public string Function { get; set; }

    public bool Verbose { get; set; }
}

#endregion