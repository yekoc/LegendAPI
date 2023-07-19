
using System;
using BepInEx;
using System.Collections.Generic;
using On;
using IL;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using UnityEngine;

namespace LegendAPI {
    public static class Outfits {

        public static void Register(OutfitInfo info) {
            foreach (OutfitModStat Mod in info.outfit.modList) {
                if (!Mod.modType.Equals(CustomModType))
                    continue;
                Mod.modifierID = info.outfit.outfitID;
            }
            if (!OutfitCatalog.ContainsKey(info.outfit.outfitID)) {
                OutfitCatalog.Add(info.outfit.outfitID, info);
            }
            else {
                OutfitCatalog[info.outfit.outfitID] = info;
            }
        }
        public static void Register(Outfit outfit) {
            OutfitInfo info = new OutfitInfo();
            info.outfit = outfit;
            info.name = outfit.outfitID;
            Register(info);
        }


        public static Dictionary<string, OutfitInfo> OutfitCatalog = new Dictionary<string, OutfitInfo>();
        internal static List<string> Aisle = new List<string>();
	public static OutfitModStat.OutfitModType CustomModType = (OutfitModStat.OutfitModType)20;
        public static bool init = false;
        static public void Awake() {
            On.Outfit.UpdateOutfitDictData += CatalogToDict;
            On.OutfitMerchantNpc.CreateOutfitStoreItem += OutfitForSale;
            On.OutfitMerchantNpc.ConditionalRequirementMet += CustomCondition;
            On.OutfitMerchantNpc.LoadOutfitItems += (orig, self) => { orig(self); Aisle.Clear(); };
            On.GameController.Awake += (orig, self) => {
                orig(self);
                if(!init){
                 On.TextManager.GetOutfitName += CustomOutfitText;
                 IL.TailorNpc.UpgradePlayerOutfit += EarlyUpgrade;
                 init = true;
                }
            };
            On.OutfitModStat.GetDescription += CustomModDescription;
            On.OutfitModStat.SetModStatus += SetCustomStatus;
            On.Outfit.HandleNOutfit += CustomShadowShade;
        }
        internal static void OutfitForSale(On.OutfitMerchantNpc.orig_CreateOutfitStoreItem orig, OutfitMerchantNpc self, Vector2 pos, string givenID) {
            if (givenID == String.Empty) {
                foreach (OutfitInfo Info in OutfitCatalog.Values) {
                    if (Info.outfit.unlocked || !Info.unlockCondition() || Aisle.Contains(Info.outfit.outfitID))
                        continue;
                    Aisle.Add(Info.outfit.outfitID);
                    self.CreateOutfitStoreItem(pos, Info.outfit.outfitID);
                    return;
                }
            }
            else if (OutfitCatalog.ContainsKey(givenID)) {
                if (OutfitCatalog[givenID].outfit.unlocked || !OutfitCatalog[givenID].unlockCondition()) {
                    orig(self, pos, String.Empty);
                    return;
                }
            }
            orig(self, pos, givenID);
        }
        internal static void SetCustomStatus(On.OutfitModStat.orig_SetModStatus orig, OutfitModStat self, Player givenPlayer, bool givenStatus, bool allowUpdate) {
            if (self.modType == CustomModType && OutfitCatalog.ContainsKey(self.modifierID)){
                OutfitCatalog[self.modifierID].customMod(givenPlayer, givenStatus, allowUpdate);
            }
            orig(self, givenPlayer, givenStatus, allowUpdate);
        }
        internal static void EarlyUpgrade(ILContext il){
            var c = new ILCursor(il);
            if(c.TryGotoNext(MoveType.After,x => x.MatchStfld(typeof(TailorNpc).GetField("currentMod",(System.Reflection.BindingFlags)(-1))), x=> x.MatchLdarg(out _),x=> x.MatchLdfld(out _))){
              c.Emit(OpCodes.Ldarg_0);
              c.Emit(OpCodes.Ldfld,typeof(TailorNpc).GetField("currentOutfit",(System.Reflection.BindingFlags)(-1)));
              c.EmitDelegate<Func<OutfitModStat,Outfit,OutfitModStat>>((mod,outfit) =>{
                if(mod.modType == CustomModType && (mod.modifierID == null || mod.modifierID == String.Empty)){
                   LegendAPI.Logger.LogDebug("Fixing broken modifierID");
                   mod.modifierID = outfit.outfitID;
                }
                return mod;
              });
            }
            if(c.TryGotoNext(x => x.MatchLdcI4(1),x => x.MatchCallOrCallvirt(typeof(Outfit).GetMethod("SetEquipStatus")))){
               c.EmitDelegate<Func<Player,Player>>((p) => {p.outfitEnhanced = true; return p;});
            }
        }
        internal static string CustomModDescription(On.OutfitModStat.orig_GetDescription orig, OutfitModStat self, bool addExtra) {
            if (self.modType == CustomModType && OutfitCatalog.ContainsKey(self.modifierID))
                return OutfitCatalog[self.modifierID].customDesc(addExtra);
            return orig(self, addExtra);

        }
        internal static bool CustomCondition(On.OutfitMerchantNpc.orig_ConditionalRequirementMet orig, OutfitMerchantNpc self, string givenName) {
            if (OutfitCatalog.ContainsKey(givenName))
                return OutfitCatalog[givenName].unlockCondition();
            return orig(self, givenName);
        }
        internal static void CatalogToDict(On.Outfit.orig_UpdateOutfitDictData orig) {
            orig();
            foreach (OutfitInfo Info in OutfitCatalog.Values) {
                if (!Outfit.OutfitDict.ContainsKey(Info.outfit.outfitID)) {
                    Outfit.OutfitDict.Add(Info.outfit.outfitID, Info.outfit);
                }
                else {
                    Outfit.OutfitDict[Info.outfit.outfitID] = Info.outfit;
                }
            }
            GameDataManager.gameData.PullOutfitData();

        }
        internal static string CustomOutfitText(On.TextManager.orig_GetOutfitName orig, string givenID) {
            if (!OutfitCatalog.ContainsKey(givenID))
                return orig(givenID);
            else
                return OutfitCatalog[givenID].name;
        }
        internal static void CustomShadowShade(On.Outfit.orig_HandleNOutfit orig,string actualOutfit){
            orig(actualOutfit);
            foreach (OutfitModStat Mod in Outfit.normalOutfit.modList) {
                if (!Mod.modType.Equals(CustomModType))
                    continue;
                Mod.modifierID = actualOutfit;
            }
        }
    }
    public class OutfitInfo {
        //public static int extra = 0;
        public Outfit outfit = new Outfit("newHope", Outfit.baseHope.outfitColorIndex, new List<OutfitModStat>());
        public string name = "UNNAMED";
        public Func<bool> unlockCondition = () => { return true; };
        public Action<Player, bool, bool> customMod = (p, b1, b2) => { return; };
        public Func<bool, string> customDesc = (b) => { return ""; };
    }
}
