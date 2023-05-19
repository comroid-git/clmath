using System;
using System.IO;
using comroid.common;
using NUnit.Framework;

namespace clmath.test;

public static class TestUtil
{
    public const int Repeat = 1000;
    public const double Delta = 0.000_000_000_1d;
    public static readonly Random RNG = new();
    
    static TestUtil()
    {
        Program.log.Level = LogLevel.None;
    }

    public static UnitValue CalcTest(string input)
    {
        Console.SetIn(new StringReader("\n"));
        Console.SetOut(new StringWriter());
        lock (RNG) Program.Main(input);
        return Program.BaseContext[0];
    }

    public static string SolverTest(string input, string solveFor, string solveWith)
    {
        var bak = Console.In;
        var writer = new StringWriter();
        Console.SetIn(new StringReader("exit\n"));
        Console.SetOut(writer);
        lock (RNG) Program.Main("solve", solveFor, solveWith, input);
        Console.SetIn(bak);

        var written = writer.ToString();
        var cut = written.IndexOf(">", StringComparison.Ordinal);
        if (cut == -1) Assert.Fail("No output");
        var output = written.Substring(0, cut);
        return output;
    }

    public static int RandomI() => RNG.Next();
    public static double RandomD() => RNG.Next();
    public static double Random() => RandomD() + RandomI();
}