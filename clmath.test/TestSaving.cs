using System;
using System.IO;
using comroid.csapi.common;
using NUnit.Framework;

namespace clmath.test;

public class TestSaving
{
    private string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "test.bin");
    [Test]
    public void save()
    {
        Console.WriteLine("saving to " + path);
        var func = Program.ParseFunc("x*y^2");
        (func as IByteContainer).Save(new FileStream(path, FileMode.Create));
    }
    [Test]
    public void load()
    {
        Console.WriteLine("loaded from " + path);
        var func = new Component();
        (func as IByteContainer).Load(new FileStream(path, FileMode.Open));
        Console.WriteLine(func);
    }
}