# CMS 2021 Shop Helper

A [MelonLoader](https://melonwiki.xyz) mod for Car Mechanic Simulator 2021 that adds every part a car needs to your shopping list in one go. No more going through the car layer by layer to work out what's missing.

## What it does

1. Look at a car in your garage.
2. Press **F5**.
3. The game's own yes/no box shows what the mod found, for example `Hammerdal Strale S: add 213 parts?`
4. Say yes and the parts go on your shopping list. You still do the buying at the shop.

**Added:** every mechanical part that's missing or below 100% condition.
**Not included yet:** body parts (panels, glass, seats, lights).

### Safety checks

- Parts already on your shopping list are skipped, so nothing gets doubled up.
- F5 is ignored while driving, in photo mode, on the dyno or test track, or with a menu open.
- If the yes/no box closes without an answer, your controls are handed back.
- If one part fails, the rest still get added. Errors go to the MelonLoader log and never crash the game.

## Installation

1. Install **MelonLoader 0.6.6**. CMS 2021 needs this version. 0.5.7 crashes and 0.7.x is untested.
2. Launch the game once, then close it. This lets MelonLoader set itself up.
3. Download `CMSShopHelper.dll` from the [Releases page](../../releases) and drop it in `Car Mechanic Simulator 2021\Mods\`.
4. Start the game, load into your garage, look at a car and press F5.

If the MelonLoader log shows `No Support Module Loaded!`, see [tools/Il2CppInteropPatch](tools/Il2CppInteropPatch) for a fix.

## Building from source

You'll need:

- The [.NET 6 SDK](https://dotnet.microsoft.com/download/dotnet/6.0)
- Car Mechanic Simulator 2021 with MelonLoader 0.6.6 installed and run at least once

```
cd src
dotnet build -c Release
```

The project compiles against the game's own files from your install, so nothing from the game is stored in this repo. A successful build also copies the mod into your `Mods` folder.

If the game lives somewhere other than the default Steam path:

```
dotnet build -c Release -p:GameDir="D:\Games\Car Mechanic Simulator 2021"
```

## How it hooks into the game

| What | Game code used |
|---|---|
| The car you're looking at | `GameScript.GetIOMouseOverCarLoader2()`, then `GetLastCarLoader()`, then the nearest loaded car |
| Its parts | `CarLoader.GetPartScriptCache()`, then `PartScript.IsUnmounted`, `.Condition` and `.GetIDWithTuned()` |
| Shopping list | `UIManager.AddToShopList(id, "", new ShopListItemDataEx())`, with the current list read from `ShopListWindow.items` |
| Yes/no box | `UIManager.ShowAskWindow(...)`, with the game switched to `gameMode.UI` while it's open |

## Built with

- C# on .NET 6
- [MelonLoader](https://melonwiki.xyz) 0.6.6 (Il2Cpp)
- Il2CppInterop, using the game's generated interop assemblies
- Unity (legacy input, for the F5 key)

## Repo layout

| Path | Contents |
|---|---|
| `src/` | The mod itself |
| `tools/Il2CppInteropPatch/` | A small Mono.Cecil tool that fixes a MelonLoader 0.6.6 startup crash on some installs |

## Disclaimer

This is an unofficial fan mod. It isn't affiliated with or endorsed by Red Dot Games or PlayWay. No game files are included in this repo.
