# Il2CppInterop patch (MelonLoader 0.6.6)

On some CMS 2021 installs MelonLoader fails at startup with:

```
SceneManager.sceneLoaded override failed: System.InvalidOperationException: Sequence contains no elements
  at ...MetadataCache_GetTypeInfoFromTypeDefinitionIndex_Hook.FindGetTypeInfoFromTypeDefinitionIndex
No Support Module Loaded!
```

No mod runs when that happens.

## Cause

Il2CppInterop has to find the engine function `MetadataCache::GetTypeInfoFromTypeDefinitionIndex` inside
`GameAssembly.dll`. It starts at the exported `il2cpp_image_get_class` and follows the jumps.

In this build of the game, `il2cpp_image_get_class` tail-jumps **straight into** the target function
(`Image::GetType` is inlined). Il2CppInterop follows one jump too many, lands on an unaligned
epilogue address, rejects it, falls back to a second search method, and that one finds nothing.
Related upstream report: https://github.com/LavaGang/MelonLoader/issues/688

## Fix

When that unaligned case happens, return the function it just jumped from instead of falling back.
That's a 3-instruction IL change in `Il2CppInterop.Runtime.dll`; nothing else in the file changes.

## Use

Game closed, MelonLoader 0.6.6 installed:

```
dotnet run -- fix "<game>\MelonLoader\net6\Il2CppInterop.Runtime.dll" Il2CppInterop.Runtime.patched.dll
copy Il2CppInterop.Runtime.patched.dll "<game>\MelonLoader\net6\Il2CppInterop.Runtime.dll"
```

Keep a backup of the original. Reinstalling MelonLoader undoes the patch.
