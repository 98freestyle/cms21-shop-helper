using System.Collections.Generic;
using MelonLoader;
using UnityEngine;
using Il2Cpp;
using Il2CppCMS.UI;
using Il2CppCMS.Containers;

[assembly: MelonInfo(typeof(CMSShopHelper.Mod), "CMS Shop Helper", "0.4.0", "Ethan")]
[assembly: MelonGame]

namespace CMSShopHelper
{
    public class Mod : MelonMod
    {
        // Modes where F5 makes no sense (driving, photo mode, test track, dyno...)
        static readonly HashSet<gameMode> BlockedModes = new HashSet<gameMode>
        {
            gameMode.CarDrive, gameMode.PhotoMode, gameMode.PathTest, gameMode.Dyno,
            gameMode.Benchmark, gameMode.UI, gameMode.None
        };

        float lastPress;            // debounce
        bool modeNeedsRestore;      // we switched the game to UI mode and haven't switched back yet
        float askShownAt;

        public override void OnInitializeMelon() => LoggerInstance.Msg("Loaded. Look at a car and press F5.");

        public override void OnUpdate()
        {
            // Safety net: if the yes/no box closed without calling us back, give the player their controls back
            // (waits 1s so the box has definitely opened before checking)
            if (modeNeedsRestore && Time.unscaledTime - askShownAt > 1f && !IsAskWindowOpen())
            {
                LoggerInstance.Msg("Yes/no box closed without an answer - restoring controls");
                RestoreMode();
            }

            if (!Input.GetKeyDown(KeyCode.F5)) return;
            if (Time.unscaledTime - lastPress < 0.5f) return;   // ignore key spam
            lastPress = Time.unscaledTime;

            try { OnF5(); }
            catch (System.Exception e) { LoggerInstance.Error("F5 failed: " + e); RestoreMode(); }
        }

        void OnF5()
        {
            var game = GameScript.Get();
            var ui = UIManager.Get();
            var gm = GameMode.Get();
            if (game == null || ui == null || gm == null) { LoggerInstance.Msg("F5 ignored: not in the garage"); return; }
            if (BlockedModes.Contains(gm.currentMode)) { LoggerInstance.Msg($"F5 ignored: game mode is {gm.currentMode}"); return; }
            if (WindowManager.Instance != null && WindowManager.Instance.IsAnyWindowOpened()) { LoggerInstance.Msg("F5 ignored: a menu is open"); return; }

            var car = FindCar(game);
            if (car == null) { Popup(ui, "Look at a car first"); return; }
            string carName = SafeName(car);

            var needed = GetNeededParts(car, out int missing, out int worn);
            if (needed.Count == 0) { Popup(ui, $"{carName}: every part is at 100%"); return; }

            // Don't double up: only add what isn't already on the shopping list
            var toAdd = SubtractAlreadyOnList(ui, needed, out int alreadyThere);
            if (toAdd.Count == 0) { Popup(ui, $"{carName}: already on your shopping list"); return; }

            string desc = $"{carName}: add {toAdd.Count} parts?\n" +
                          $"Missing {missing}  |  Under 100% {worn}" +
                          (alreadyThere > 0 ? $"  |  {alreadyThere} already on list" : "");

            // The game only routes input to windows in UI mode
            gm.SetCurrentMode(gameMode.UI);
            modeNeedsRestore = true;
            askShownAt = Time.unscaledTime;

            System.Action<bool> onAnswer = yes =>
            {
                RestoreMode();
                if (yes) AddAll(ui, toAdd, carName);
            };
            ui.ShowAskWindow("Shopping list", desc, onAnswer, true);
        }

        void AddAll(UIManager ui, List<string> ids, string carName)
        {
            int ok = 0, failed = 0;
            var extra = new ShopListItemDataEx();
            foreach (var id in ids)
            {
                try { ui.AddToShopList(id, "", extra); ok++; }
                catch (System.Exception e) { failed++; LoggerInstance.Warning($"Couldn't add {id}: {e.Message}"); }
            }
            Popup(ui, failed == 0 ? $"Added {ok} parts" : $"Added {ok} parts ({failed} failed, see log)");
            LoggerInstance.Msg($"{carName}: added {ok}, failed {failed}");
        }

        // Every mechanical part that's missing or not at 100%
        List<string> GetNeededParts(CarLoader car, out int missing, out int worn)
        {
            missing = worn = 0;
            var result = new List<string>();

            var parts = car.GetPartScriptCache();
            if (parts == null) { car.CachePartScripts(); parts = car.GetPartScriptCache(); }
            if (parts == null) return result;

            foreach (var p in parts)
            {
                try
                {
                    if (p == null) continue;
                    string id = p.GetIDWithTuned();
                    if (string.IsNullOrEmpty(id)) continue;

                    if (p.IsUnmounted) missing++;
                    else if (p.Condition < 1f) worn++;
                    else continue;

                    result.Add(id);
                }
                catch (System.Exception e) { LoggerInstance.Warning("Skipped a part: " + e.Message); }
            }
            return result;
        }

        // Counts what's already on the shopping list and removes that many from what we'd add.
        // If the list can't be read, falls back to adding everything.
        List<string> SubtractAlreadyOnList(UIManager ui, List<string> needed, out int alreadyThere)
        {
            alreadyThere = 0;
            var onList = new Dictionary<string, int>();
            try
            {
                var items = ui.ShopListWindow?.items;
                if (items != null)
                    for (int i = 0; i < items.Count; i++)
                    {
                        var it = items[i];
                        if (it == null || string.IsNullOrEmpty(it.ID)) continue;
                        onList[it.ID] = (onList.TryGetValue(it.ID, out int n) ? n : 0) + it.Amount;
                    }
            }
            catch (System.Exception e)
            {
                LoggerInstance.Warning("Couldn't read shopping list, adding everything: " + e.Message);
                return needed;
            }

            var result = new List<string>();
            foreach (var id in needed)
            {
                if (onList.TryGetValue(id, out int n) && n > 0) { onList[id] = n - 1; alreadyThere++; }
                else result.Add(id);
            }
            return result;
        }

        // Car under the mouse, else the game's last car, else the closest loaded car
        CarLoader FindCar(GameScript game)
        {
            CarLoader car = null;
            try { car = game.GetIOMouseOverCarLoader2(); } catch { }
            if (car != null) return car;
            try { car = game.GetLastCarLoader(); } catch { }
            if (car != null) return car;

            try
            {
                var places = CarLoaderPlaces.Get();
                if (places == null) return null;
                var cam = Camera.main;
                Vector3 camPos = cam != null ? cam.transform.position : Vector3.zero;
                float best = float.MaxValue;
                for (int i = 0; i < places.GetCarLoadersCount(); i++)
                {
                    var c = places.GetCarLoaderByIndex(i);
                    if (c == null || !c.IsCarLoaded()) continue;
                    float d = Vector3.Distance(camPos, c.transform.position);
                    if (d < best) { best = d; car = c; }
                }
            }
            catch (System.Exception e) { LoggerInstance.Warning("Car search failed: " + e.Message); }
            return car;
        }

        bool IsAskWindowOpen()
        {
            try { return WindowManager.Instance != null && WindowManager.Instance.IsWindowActive(WindowID.AskWindow); }
            catch { return false; }
        }

        void RestoreMode()
        {
            if (!modeNeedsRestore) return;
            modeNeedsRestore = false;
            try { GameMode.Get()?.SetPreviousMode(); }
            catch (System.Exception e) { LoggerInstance.Error("Couldn't restore game mode: " + e); }
        }

        void Popup(UIManager ui, string text)
        {
            try { ui.ShowPopup("Shopping list", text, PopupType.Normal); }
            catch { LoggerInstance.Msg(text); }
        }

        static string SafeName(CarLoader car)
        {
            try { var n = car.GetName(); return string.IsNullOrEmpty(n) ? "Car" : n; } catch { return "Car"; }
        }
    }
}
