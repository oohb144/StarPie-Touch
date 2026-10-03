using System.IO;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using StarPie.Plugin;

namespace WinPieGestures.Plugins;

/// <summary>在执行插件代码前解析其 SDK 引用，兼容按需回移的接口并给出具体缺失项。</summary>
internal static class PluginContractCompatibility
{
    public static bool Check(Assembly assembly, string dllPath, out string error)
    {
        using var stream = File.OpenRead(dllPath);
        using var pe = new PEReader(stream);
        MetadataReader reader = pe.GetMetadataReader();
        bool IsSdkType(TypeReference type) => type.ResolutionScope.Kind == HandleKind.AssemblyReference &&
            reader.GetString(reader.GetAssemblyReference((AssemblyReferenceHandle)type.ResolutionScope).Name) == PluginApi.AbstractionsAssemblyName;

        foreach (TypeReferenceHandle handle in reader.TypeReferences)
        {
            TypeReference type = reader.GetTypeReference(handle);
            if (!IsSdkType(type)) continue;
            try { assembly.ManifestModule.ResolveType(MetadataTokens.GetToken(handle)); }
            catch (TypeLoadException)
            {
                error = Describe(reader.GetString(type.Name));
                return false;
            }
        }
        foreach (MemberReferenceHandle handle in reader.MemberReferences)
        {
            MemberReference member = reader.GetMemberReference(handle);
            if (member.Parent.Kind != HandleKind.TypeReference) continue;
            TypeReference type = reader.GetTypeReference((TypeReferenceHandle)member.Parent);
            if (!IsSdkType(type)) continue;
            try { assembly.ManifestModule.ResolveMember(MetadataTokens.GetToken(handle)); }
            catch (Exception ex) when (ex is MissingMemberException or TypeLoadException)
            {
                error = Describe(reader.GetString(type.Name) + "." + reader.GetString(member.Name));
                return false;
            }
        }
        error = "";
        return true;
    }

    private static string Describe(string member) =>
        $"插件与当前 StarPie 的 SDK 不兼容，缺少接口 {member}。请使用匹配的插件版本或升级 StarPie。";
}
