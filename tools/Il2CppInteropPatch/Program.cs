using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;

// Usage:
//   Il2CppInteropPatch dump <dll> <Type> <Method>   print a method's IL
//   Il2CppInteropPatch fix <in.dll> <out.dll>       apply the patch

var assembly = AssemblyDefinition.ReadAssembly(args[1]);
var type = assembly.MainModule.GetTypes().First(x =>
    x.Name == "MetadataCache_GetTypeInfoFromTypeDefinitionIndex_Hook" || x.Name == (args.Length > 2 ? args[2] : ""));

if (args[0] == "dump")
{
    foreach (var m in type.Methods.Where(m => m.Name == args[3]))
        foreach (var instruction in m.Body.Instructions)
            Console.WriteLine("  " + instruction);
    return;
}

var method = type.Methods.Single(m => m.Name == "FindGetTypeInfoFromTypeDefinitionIndex");
method.Body.SimplifyMacros();
var ins = method.Body.Instructions;

// Find: ldarg.0; ldc.i4.1; call FindGetTypeInfoFromTypeDefinitionIndex(bool); ret
// This is the "unaligned address" fallback that we replace.
int idx = -1;
for (int k = 0; k + 3 < ins.Count; k++)
{
    if (ins[k].OpCode == OpCodes.Ldarg && ins[k + 1].OpCode == OpCodes.Ldc_I4 && ins[k + 2].OpCode == OpCodes.Call
        && ((MethodReference)ins[k + 2].Operand).Name == "FindGetTypeInfoFromTypeDefinitionIndex" && ins[k + 3].OpCode == OpCodes.Ret)
    {
        if (idx >= 0) throw new Exception("pattern matched twice");
        idx = k;
    }
}
if (idx < 0) throw new Exception("pattern not found (already patched?)");

// The local that holds the Image::GetType target: the one stored right after
// JumpTargets(il2cpp_image_get_class export).First()
VariableDefinition imageGetType = null;
for (int k = 0; k + 2 < ins.Count; k++)
{
    if (ins[k].OpCode == OpCodes.Call && ((MethodReference)ins[k].Operand).Name == "GetIl2CppExport")
    {
        for (int j = k; j < k + 40; j++)
        {
            if (ins[j].OpCode == OpCodes.Call && ((MethodReference)ins[j].Operand).Name == "First")
            {
                imageGetType = (VariableDefinition)ins[j + 1].Operand;
                break;
            }
        }
        break;
    }
}

Console.WriteLine($"patch at {ins[idx]}  -> return local V_{imageGetType.Index} (Image::GetType target)");

// Replace the fallback call with "return imageGetType". The first instruction is reused
// so any branch that targets it stays valid.
var first = ins[idx];
first.OpCode = OpCodes.Ldloc;
first.Operand = imageGetType;
ins[idx + 1].OpCode = OpCodes.Nop;
ins[idx + 1].Operand = null;
ins[idx + 2].OpCode = OpCodes.Nop;
ins[idx + 2].Operand = null;

method.Body.OptimizeMacros();
assembly.Write(args[2]);
Console.WriteLine("written " + args[2]);
