using CommandLine;

#pragma warning disable CS8618

namespace clmath
{
    public interface ICmd
    {
        [Option('v', "verbose", Required = false, Default = false, HelpText = "Print verbose information, if applicable")]
        public bool Verbose { get; set; }
    }

    public interface IVariableCmd<T> : ICmd
    {
        [Value(0, Required = true, HelpText = "The variable to work with")]
        public T Variable { get; set; }
    }

    public interface ITargetCmd<T> : ICmd
    {
        [Value(0, Required = true, HelpText = "The target to use")]
        public T Target { get; set; }
    }

    public interface IAcceptValueCmd<T> : ICmd
    {
        [Value(1, Required = true, HelpText = "The new value")]
        public T Value { get; set; }
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
            func,
            constant,
            stack,
            mem,
            stash,
            enabled,
            packs,
            units
        }

        public bool Verbose { get; set; }
        public TargetType Target { get; set; }
    }

    [Verb("set", HelpText = "Define a constant value")]
    public class SetCommand : IVariableCmd<string>, IAcceptValueCmd<string>
    {
        public string Value { get; set; }
        public bool Verbose { get; set; }
        public string Variable { get; set; }
    }

    [Verb("unset", HelpText = "Remove a constant value")]
    public class UnsetCommand : IVariableCmd<string>
    {
        public bool Verbose { get; set; }
        public string Variable { get; set; }
    }

    [Verb("mode", HelpText = "DRG Mode to use for calculation")]
    public class ModeCommand : ITargetCmd<CalcMode>
    {
        public bool Verbose { get; set; }
        public CalcMode Target { get; set; }
    }

    [Verb("edit", HelpText = "Edit configuration")]
    public class EditCommand : ITargetCmd<EditCommand.TargetType>
    {
        public enum TargetType
        {
            unit
        }

        public bool Verbose { get; set; }
        public TargetType Target { get; set; }
    }

    [Verb("enable", HelpText = "Enable a feature")]
    public class EnableCommand : ITargetCmd<string>
    {
        public bool Verbose { get; set; }
        public string Target { get; set; }
    }

    [Verb("disable", HelpText = "Disable a feature")]
    public class DisableCommand : ITargetCmd<string>
    {
        public bool Verbose { get; set; }
        public string Target { get; set; }
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

    [Verb("delete", HelpText = "Rename a function")]
    public class DeleteCommand : ITargetCmd<string>
    {
        public bool Verbose { get; set; }
        public string Target { get; set; }
    }

    [Verb("clear", HelpText = "Clear things by category")]
    public class ClearCommand : ITargetCmd<ClearCommand.TargetType>
    {
        public enum TargetType
        {
            vars = default,
            mem,
            stash,
            stack,
            all
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

    #region Tools

    [Verb("graph", HelpText = "Display functions on a 2D Graph")]
    public class GraphCommand : ICmd
    {
        [Option('n', "variable", HelpText = "The variable name to use when unsure")]
        public string Variable { get; set; }

        [Value(0, Required = true, HelpText = "The first function to display")]
        public string Function { get; set; }

        [Value(1, Required = false, HelpText = "The second function to display")]
        public string Function2 { get; set; }

        [Value(2, Required = false, Hidden = true, HelpText = "The third function to display")]
        public string Function3 { get; set; }

        [Value(3, Required = false, Hidden = true, HelpText = "The fourth function to display")]
        public string Function4 { get; set; }

        [Value(4, Required = false, Hidden = true, HelpText = "The fifth function to display")]
        public string Function5 { get; set; }

        [Value(5, Required = false, Hidden = true, HelpText = "The sixth function to display")]
        public string Function6 { get; set; }

        [Value(6, Required = false, HelpText = "The seventh function to display")]
        public string Function7 { get; set; }

        public bool Verbose { get; set; }
    }

    [Verb("solve", HelpText = "Solve an equation for a variable")]
    public class SolveCommand : ICmd
    {
        [Value(0, Required = true, HelpText = "The variable to solve for")]
        public string For { get; set; }

        [Value(1, Required = true, HelpText = "The left hand side of the equation; must be a singular variable")]
        public string LHS { get; set; }

        [Value(2, Required = false, HelpText = "The function to solve")]
        public string Function { get; set; }

        public bool Verbose { get; set; }
    }

    #endregion
}