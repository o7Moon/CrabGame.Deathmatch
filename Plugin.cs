using BepInEx;
using BepInEx.IL2CPP;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.Events;
using System.Collections.Generic;
using System.IO;

namespace Deathmatch
{
    [BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
    public class Plugin : BasePlugin
    {
        public static ConfigEntry<bool> toggled;
        public static ConfigEntry<int> weapon;
        public static ConfigEntry<int> bonusTime;
        public static bool roundEndedYet = false;
        public static bool addedBonusTime = false;
        public override void Load()
        {
            Harmony.CreateAndPatchAll(typeof(Plugin));
            toggled = Config.Bind<bool>("Deathmatch","Enabled",true,"If true, deathmatch replaces tag. this reloads every time you switch maps");
            weapon = Config.Bind<int>("Deathmatch","Weapon", 6, "The item ID of the weapon to give every player. Default is katana");
            bonusTime = Config.Bind<int>("Deathmatch","Bonus Time", 180, "This number is added onto the round timer");
            SceneManager.sceneLoaded += (UnityAction<Scene,LoadSceneMode>) OnSceneLoad;

            Log.LogInfo($"Plugin {PluginInfo.PLUGIN_GUID} is loaded!");
        }

        public void OnSceneLoad(Scene scene, LoadSceneMode mode){
            Config.Reload();
        }

        [HarmonyPatch(typeof(GameModeTag), nameof(GameModeTag.OnFreezeOver))]
        [HarmonyPrefix]
        public static bool OnFreezeOver(GameModeTag __instance) {
            if (!toggled.Value) return true;
            GameServer.ForceGiveAllWeapon(weapon.Value);
            addedBonusTime = false;
            return false;
        }

        [HarmonyPatch(typeof(GameModeTag), nameof(GameModeTag.OnFreezeOverAlert))]
        [HarmonyPrefix]
        public static bool FreezeOverAlert(GameModeTag __instance) {
            return !toggled.Value; // skip alert if toggled
        }

        [HarmonyPatch(typeof(GameModeTag), nameof(GameModeTag.CheckGameOver))]
        [HarmonyPrefix]
        public static bool CheckGameOver(GameModeTag __instance) {
            if (!toggled.Value) return true;
            if (!SteamManager.Instance.IsLobbyOwner()) return false;
            if (__instance.modeState == GameMode.EnumNPublicSealedvaFrPlEnGa5vUnique.Freeze) return false;
            if (roundEndedYet) return false;

            if (!addedBonusTime){
                addedBonusTime = true;
                __instance.freezeTimer.field_Private_Single_0 += bonusTime.Value;
            }

            List<PlayerManager> alivePlayers = new List<PlayerManager>();

            foreach (PlayerManager player in GameManager.Instance.activePlayers.values){
                if (!player.dead) alivePlayers.Add(player);
            }

            if (__instance.freezeTimer.field_Private_Single_0 < 1) {
                foreach (var p in alivePlayers) {
                    ServerSend.PlayerDied((ulong)p.steamProfile, (ulong)p.steamProfile,Vector3.up * 1000f);
                }
                alivePlayers.RemoveAll((p)=>{return true;});
            }

            // there can only be one
            if (alivePlayers.Count < 2) {
                if (alivePlayers.Count < 1) {
                    ServerSend.SendChatMessage(1,"nobody wins, L");
                    SetWinner(__instance, 0uL);
                } else {
                    ServerSend.PlayerDied((ulong) alivePlayers[0].steamProfile, (ulong) alivePlayers[0].steamProfile, Vector3.zero);
                    ServerSend.SendChatMessage(1, alivePlayers[0].username + " wins, W");
                    SetWinner(__instance, (ulong) alivePlayers[0].steamProfile);
                }
                roundEndedYet = true;
            }
            
            return false;
        }

        [HarmonyPatch(typeof(GameModeTag),nameof(GameModeTag.InitMode))]
        [HarmonyPostfix]
        public static void InitMode(GameModeTag __instance) {
            roundEndedYet = false;
        }

        public static void SetWinner(GameModeTag __instance, ulong winner) {
            ServerSend.GameOver(winner);
            __instance.modeState = GameMode.EnumNPublicSealedvaFrPlEnGa5vUnique.GameOver;
            __instance.freezeTimer.field_Private_Single_0 = 3;
        }
    }
}
