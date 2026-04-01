using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

foreach (var dll in new[] { "Brutal.Vulkan.Abstractions.dll", "Brutal.Vulkan.dll", "KSA.dll" })
{
    var path = Path.Combine(@"C:\Program Files\Kitten Space Agency", dll);
    using var pe = new PEReader(File.OpenRead(path));
    var r = pe.GetMetadataReader();
    var d = r.GetAssemblyDefinition();
    Console.WriteLine($"{dll} => Assembly: {r.GetString(d.Name)}");
    
    // List namespaces
    var namespaces = new HashSet<string>();
    foreach (var th in r.TypeDefinitions)
    {
        var td = r.GetTypeDefinition(th);
        var ns = r.GetString(td.Namespace);
        if (!string.IsNullOrEmpty(ns)) namespaces.Add(ns);
    }
    foreach (var ns in namespaces.OrderBy(n => n).Take(20))
        Console.WriteLine($"  ns: {ns}");
    Console.WriteLine();
}
