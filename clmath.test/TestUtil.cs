using System;
using System.IO;
using NUnit.Framework;

namespace clmath.test
{
    public static class TestUtil
    {
        public static string CalcTest(string input)
        {
            var bak = Console.In;
            var writer = new StringWriter();
            Console.SetIn(new StringReader("\n"));
            Console.SetOut(writer);
            Program.Main(input);
            Console.SetIn(bak);

            var written = writer.ToString();
            var start = written.IndexOf("=", StringComparison.Ordinal);
            var end = written.IndexOf(Environment.NewLine, StringComparison.Ordinal) - 2;
            if (start == -1) Assert.Fail("No output");
            var output = written.Substring(start + 2, end - start);
            return output;
        }

        public static string SolverTest(string input, string solveFor, string solveWith)
        {
            var bak = Console.In;
            var writer = new StringWriter();
            Console.SetIn(new StringReader("exit\n"));
            Console.SetOut(writer);
            Program.Main("solve", solveFor, solveWith, input);
            Console.SetIn(bak);

            var written = writer.ToString();
            var cut = written.IndexOf(">", StringComparison.Ordinal);
            if (cut == -1) Assert.Fail("No output");
            var output = written.Substring(0, cut);
            return output;
        }
    }
}