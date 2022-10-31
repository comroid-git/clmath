﻿using System;
using System.IO;
using NUnit.Framework;

namespace clmath.test;

public static class TestUtil
{
    public static string SolverTest(string input, string solveFor, string solveWith)
    {
        var bak = Console.In;
        var writer = new StringWriter();
        Console.SetIn(new StringReader("exit\n"));
        Console.SetOut(writer);
        Program.Main("solve", solveFor, solveWith, input);
        Program._exiting = false;
        Console.SetIn(bak);

        var written = writer.ToString();
        var cut = written.IndexOf(">", StringComparison.Ordinal);
        if (cut == -1) Assert.Fail("No output");
        var output = written.Substring(0, cut);
        return output;
    }
}