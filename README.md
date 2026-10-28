# CMS 2021 Shop Helper

A MelonLoader mod for Car Mechanic Simulator 2021 that adds every part a car needs to your
shopping list in one go - no more going layer by layer.

## How it works

1. Look at a car in the garage
2. Press **F5**
3. The game's own yes/no box shows what it found, e.g. `Hammerdal Strale S: add 213 parts?`
4. Accept - the parts are on your shopping list. Buying is still up to you at the shop.

**Added:** every mechanical part that is missing or below 100%.
**Not yet:** body parts (panels, glass, seats, lights).

### Safety

- Won't add parts that are already on your shopping list
- Ignored while driving, in photo mode, on the dyno/test track, or with a menu open
- If the yes/no box closes without an answer, your controls are restored
- One bad part can't stop the rest; errors go to the MelonLoader log, never crash the game

## Install

1. Install **MelonLoader 0.6.6** (CMS 2021 needs this version - 0.5.7 crashes, 0.7.x is untested)
2. Launch the game once, then close it
3. Put `CMSShopHelper.dll` in `Car Mechanic Simulator 2021\Mods\`

If the MelonLoader log shows `No Support Module Loaded!`, see [tools/Il2CppInteropPatch](tools/Il2CppInteropPatch).

## Build

Needs the .NET 6 SDK and the game with MelonLoader 0.6.6 installed (run once).

```
cd src
dotnet build -c Release
```

The build references the game's own files from your install and copies the mod into `Mods\` automatically.
Different install path: `dotnet build -c Release -p:GameDir="D:\Games\Car Mechanic Simulator 2021"`

## How it finds things in the game

| What | Game code |
|---|---|
| Car you're looking at | `GameScript.GetIOMouseOverCarLoader2()`, then `GetLastCarLoader()`, then nearest loaded car |
| Its parts | `CarLoader.GetPartScriptCache()` -> `PartScript.IsUnmounted`, `.Condition`, `.GetIDWithTuned()` |
| Shopping list | `UIManager.AddToShopList(id, "", new ShopListItemDataEx())`, current list in `ShopListWindow.items` |
| Yes/no box | `UIManager.ShowAskWindow(...)` with the game switched to `gameMode.UI` while it's open |
