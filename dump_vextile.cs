using System;
using System.Reflection;

class Program {
    static void Main() {
        var asm = Assembly.LoadFrom(@"E:\Projects\GitHub\Mapsui48\packages\VexTile.0.1.0-alpha.5\lib\netstandard2.0\VexTile.dll");
        foreach(var t in asm.GetExportedTypes()) {
            if (t.Name == "ITile") {
                Console.WriteLine("ITile properties:");
                foreach(var p in t.GetProperties()) Console.WriteLine(p.PropertyType.Name + " " + p.Name);
            }
            if (t.Name == "IMetaData") {
                Console.WriteLine("IMetaData properties:");
                foreach(var p in t.GetProperties()) Console.WriteLine(p.PropertyType.Name + " " + p.Name);
            }
        }
    }
}
