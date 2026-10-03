using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using System.Runtime.Loader;

if (args.Length < 2) throw new ArgumentException("Usage: <SDK dll> <plugin dll> ...");
Assembly sdk = AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.GetFullPath(args[0]));
Console.WriteLine($"SDK: {sdk.Location}");
int failures = 0;
foreach (string pluginPath in args.Skip(1))
{
    string fullPath = Path.GetFullPath(pluginPath);
    Assembly plugin = AssemblyLoadContext.Default.LoadFromAssemblyPath(fullPath);
    using var stream = File.OpenRead(fullPath);
    using var pe = new PEReader(stream);
    MetadataReader reader = pe.GetMetadataReader();
    int checkedMembers = 0;
    foreach (MemberReferenceHandle handle in reader.MemberReferences)
    {
        MemberReference member = reader.GetMemberReference(handle);
        if (member.Parent.Kind != HandleKind.TypeReference) continue;
        TypeReference type = reader.GetTypeReference((TypeReferenceHandle)member.Parent);
        if (type.ResolutionScope.Kind != HandleKind.AssemblyReference) continue;
        AssemblyReference scope = reader.GetAssemblyReference((AssemblyReferenceHandle)type.ResolutionScope);
        if (reader.GetString(scope.Name) != "StarPie.Plugin.Abstractions") continue;
        checkedMembers++;
        try { plugin.ManifestModule.ResolveMember(MetadataTokens.GetToken(handle)); }
        catch (Exception ex)
        {
            failures++;
            Console.WriteLine($"FAIL {Path.GetFileName(fullPath)}: {reader.GetString(type.Namespace)}.{reader.GetString(type.Name)}.{reader.GetString(member.Name)}: {ex.Message}");
        }
    }
    Console.WriteLine($"{Path.GetFileName(fullPath)}: checked {checkedMembers} SDK member references");
}
Console.WriteLine($"Failures: {failures}");
return failures == 0 ? 0 : 1;
