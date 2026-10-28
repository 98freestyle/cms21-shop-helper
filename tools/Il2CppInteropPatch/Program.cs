using Mono.Cecil; using Mono.Cecil.Cil; using Mono.Cecil.Rocks;
// usage: ilpatch dump <dll> <Type> <Method>  |  ilpatch fix <in.dll> <out.dll>
var a=AssemblyDefinition.ReadAssembly(args[1]);
var t=a.MainModule.GetTypes().First(x=>x.Name=="MetadataCache_GetTypeInfoFromTypeDefinitionIndex_Hook" || x.Name==(args.Length>2?args[2]:""));
if (args[0]=="dump") {
  foreach (var m in t.Methods.Where(m=>m.Name==args[3])) foreach (var i in m.Body.Instructions) Console.WriteLine("  "+i);
  return;
}
var meth=t.Methods.Single(m=>m.Name=="FindGetTypeInfoFromTypeDefinitionIndex"); meth.Body.SimplifyMacros();
var ins=meth.Body.Instructions;
// Find: ldarg.0; ldc.i4.1; call FindGetTypeInfoFromTypeDefinitionIndex(bool); ret   (the unaligned -> icall fallback)
int idx=-1;
for (int k=0;k+3<ins.Count;k++)
  if (ins[k].OpCode==OpCodes.Ldarg && ins[k+1].OpCode==OpCodes.Ldc_I4 && ins[k+2].OpCode==OpCodes.Call
      && ((MethodReference)ins[k+2].Operand).Name=="FindGetTypeInfoFromTypeDefinitionIndex" && ins[k+3].OpCode==OpCodes.Ret) { if (idx>=0) throw new Exception("pattern matched twice"); idx=k; }
if (idx<0) throw new Exception("pattern not found (already patched?)");
// imageGetType local: the local stored right after JumpTargets(il2cpp_image_get_class export).First()
VariableDefinition imageGetType=null;
for (int k=0;k+2<ins.Count;k++)
  if (ins[k].OpCode==OpCodes.Call && ((MethodReference)ins[k].Operand).Name=="GetIl2CppExport") {
    // ...stloc V_5 ... ldloc V_5; ldc.i4.0; call JumpTargets; call First; stloc V_6
    for (int j=k;j<k+40;j++) if (ins[j].OpCode==OpCodes.Call && ((MethodReference)ins[j].Operand).Name=="First" ) { imageGetType=(VariableDefinition)ins[j+1].Operand; break; }
    break;
  }
Console.WriteLine($"patch at {ins[idx]}  -> return local V_{imageGetType.Index} (Image::GetType target)");
var il=meth.Body.GetILProcessor();
var oldFirst=ins[idx];
oldFirst.OpCode=OpCodes.Ldloc; oldFirst.Operand=imageGetType;   // keeps any branch pointing here valid
ins[idx+1].OpCode=OpCodes.Nop; ins[idx+1].Operand=null;
ins[idx+2].OpCode=OpCodes.Nop; ins[idx+2].Operand=null;
meth.Body.OptimizeMacros();
a.Write(args[2]);
Console.WriteLine("written "+args[2]);
