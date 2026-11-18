// Lists every method in the plugin DLL that calls something Tattletail's old Mono lacks (Array.Empty, LINQ, ValueTuple, Func/Action).
using System;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;

class P
{
    static int Main(string[] a)
    {
        string path = a.Length > 0 ? a[0] : "../../bin/Release/net46/KaleidoscopeEndless.dll";
        using var fs = File.OpenRead(path);
        using var pe = new PEReader(fs);
        var md = pe.GetMetadataReader();
        var bad = new System.Collections.Generic.Dictionary<int, string>();
        foreach (var h in md.MemberReferences)
        {
            var m = md.GetMemberReference(h);
            string name = md.GetString(m.Name), type = "";
            if (m.Parent.Kind == HandleKind.TypeReference) { var t = md.GetTypeReference((TypeReferenceHandle)m.Parent); type = md.GetString(t.Namespace) + "." + md.GetString(t.Name); }
            else if (m.Parent.Kind == HandleKind.TypeSpecification) type = "spec";
            if ((type == "System.Array" && name == "Empty") || (type == "System.Type" && (name == "op_Equality" || name == "op_Inequality")) || type.StartsWith("System.Linq") || type.Contains("ValueTuple") || type == "System.Func`1" || type == "System.Action`1")
                bad[MetadataTokens.GetToken(h)] = type + "::" + name;
        }
        int found = 0;
        foreach (var th in md.TypeDefinitions)
        foreach (var mh in md.GetTypeDefinition(th).GetMethods())
        {
            var def = md.GetMethodDefinition(mh);
            if (def.RelativeVirtualAddress == 0) continue;
            var body = pe.GetMethodBody(def.RelativeVirtualAddress);
            var il = body.GetILBytes();
            for (int i = 0; i + 4 < il.Length; i++)
            {
                if (il[i] != 0x28 && il[i] != 0x6F && il[i] != 0x73) continue;
                int tok = BitConverter.ToInt32(il, i + 1);
                string what;
                if ((tok >> 24) == 0x0A && bad.TryGetValue(tok, out what) || (tok >> 24) == 0x2B && SpecBad(md, tok, out what))
                { Console.WriteLine(md.GetString(md.GetTypeDefinition(md.GetMethodDefinition(mh).GetDeclaringType()).Name) + "." + md.GetString(def.Name) + " -> " + what); found++; break; }
            }
        }
        Console.WriteLine(found == 0 ? "CLEAN" : found + " method(s) use types this Mono lacks");
        return found == 0 ? 0 : 1;
    }
    static bool SpecBad(MetadataReader md, int tok, out string what)
    {
        what = null;
        try { return SpecBad2(md, tok, out what); } catch (BadImageFormatException) { return false; }
    }
    static bool SpecBad2(MetadataReader md, int tok, out string what)
    {
        what = null;
        var ms = md.GetMethodSpecification((MethodSpecificationHandle)MetadataTokens.EntityHandle(tok));
        if (ms.Method.Kind != HandleKind.MemberReference) return false;
        var m = md.GetMemberReference((MemberReferenceHandle)ms.Method);
        string name = md.GetString(m.Name), type = "";
        if (m.Parent.Kind == HandleKind.TypeReference) { var t = md.GetTypeReference((TypeReferenceHandle)m.Parent); type = md.GetString(t.Namespace) + "." + md.GetString(t.Name); }
        if (type == "System.Array" && name == "Empty" || (type == "System.Type" && (name == "op_Equality" || name == "op_Inequality")) || type.StartsWith("System.Linq")) { what = type + "::" + name; return true; }
        return false;
    }
}
