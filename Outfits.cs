
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
                if (Mod.modType.Equals(CustomModType)){
                    Mod.modifierID = info.outfit.outfitID;
                }
                else if(Mod.modType.Equals(CopyCatModType)){
                    Mod.modifierID = info.outfit.outfitID;
                    CopycatCatalog.Add(info.outfit.outfitID,info.outfit);
                }
            }
            if (!OutfitCatalog.ContainsKey(info.outfit.outfitID)) {
                OutfitCatalog.Add(info.outfit.outfitID,info);
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
        private static Dictionary<string, Outfit> CopycatCatalog = new Dictionary<string, Outfit>();
        internal static List<string> Aisle = new List<string>();
	public static OutfitModStat.OutfitModType CustomModType = (OutfitModStat.OutfitModType)20;
        public static OutfitModStat.OutfitModType CopyCatModType = (OutfitModStat.OutfitModType)21;
        public static bool init = false;
        public static string shadowSource = String.Empty;
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
            IL.OutfitModStat.SetModStatus += SetCustomStatus;
            On.Outfit.HandleNOutfit += CustomShadowShade;
            On.Player.OnDestroy += (orig,self) =>{
                foreach(var mod in Outfit.OutfitDict[self.outfitID].modList){
                    if(mod.modType == CustomModType && OutfitCatalog.ContainsKey(mod.modifierID)){
                        OutfitCatalog[mod.modifierID].customMod(self,false,true,mod);
                    }
                } 
                orig(self);
            };
            On.Outfit.GetDescription += (orig,self,bol) =>{
                var res = orig(self,bol);
                return System.Text.RegularExpressions.Regex.Replace(res,"^(?:[\t ]*(?:\r?\n|\r))+",String.Empty);
            };
            IL.Player.EquipOutfit += HandleCopycat;
        }
        internal static void HandleCopycat(ILContext il){
            ILCursor c = new ILCursor(il);
            if(c.TryGotoNext(MoveType.After,x=>x.MatchLdcI4(0),x=>x.MatchRet())){
                c.MoveAfterLabels();
                c.Emit(OpCodes.Ldarg_0);
                c.Emit(OpCodes.Ldarg_1);
                c.EmitDelegate<Action<Player,string>>((player,outfitID) =>{
                    if(CopycatCatalog.ContainsKey(outfitID) && player.outfitID != outfitID){
                        var outf = new Outfit(CopycatCatalog[outfitID]);
                        for(int i = 0 ; i < outf.modList.Count;i++){
                            outf.modList[i].modifierID = CopycatCatalog[outfitID].modList[i].modifierID;
                        }
                        OutfitCatalog[outfitID].outfit = outf;
                        Outfit.outfitDict[outfitID] = outf;
                    }
                    if(CopycatCatalog.ContainsKey(player.outfitID) && player.outfitID != outfitID){
                        var outf = new Outfit(CopycatCatalog[player.outfitID]);
                        for(int i = 0 ; i < outf.modList.Count;i++){
                            outf.modList[i].modifierID = CopycatCatalog[player.outfitID].modList[i].modifierID;
                        }
                        OutfitCatalog[player.outfitID].outfit = outf;
                        Outfit.outfitDict[player.outfitID] = outf;
                    }
                });
            }
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
        internal static void SetCustomStatus(ILContext il) {
            ILCursor c = new ILCursor(il);
            if(c.TryGotoNext(MoveType.After,x => x.MatchCallOrCallvirt(typeof(OutfitModStat).GetMethod(nameof(OutfitModStat.SetTargetVarStatList),(System.Reflection.BindingFlags)(-1))))){
              c.Emit(OpCodes.Ldarg_0);
              c.Emit(OpCodes.Ldarg_1);
              c.Emit(OpCodes.Ldarg_2);
              c.Emit(OpCodes.Ldarg_3);
              c.EmitDelegate<Action<OutfitModStat,Player,bool,bool>>((modifier,player,status,update) => {
                 if(modifier.modType == CustomModType && OutfitCatalog.ContainsKey(modifier.modifierID)){
                     OutfitCatalog[modifier.modifierID].customMod(player,status,update,modifier);
                 }
                 else if(modifier.modType == CopyCatModType && Outfit.OutfitDict.ContainsKey(modifier.modifierID) && modifier.modifierID != player.outfitID){ 
                        var curout = Outfit.OutfitDict[modifier.modifierID];
                        var exout = Outfit.GetAvailableOutfit(player.outfitID);
                        if(exout == null || exout == Outfit.normalOutfit){
                          return;
                        }
                        if(CopycatCatalog.ContainsKey(exout.outfitID)){
                            exout = CopycatCatalog[exout.outfitID];
                        }
                        var allowUp = curout.modList.Find(x=>x.modType == OutfitModStat.OutfitModType.AllowUpgrade);
                        if(exout.useLeveling && !curout.useLeveling){
                            curout.lvlModList.Clear();
                            curout.levelModStat.Reset();
                            player.absEnemyKillEventHandlers -= curout.OnEnemyDefeat;
                            player.absEnemyKillEventHandlers += curout.OnEnemyDefeat;
                        }
                        foreach(var mod in exout.modList){
                            if(mod.modType == CopyCatModType){
                                continue;
                            }
                            if(allowUp?.boolModifier != null && mod.modType == OutfitModStat.OutfitModType.AllowUpgrade){
                                allowUp.boolModifier.modValue &= mod.boolModifier.modValue;
                                continue;
                            }
                            var nuMod = new OutfitModStat(mod); 
                            nuMod.modifierID = mod.modifierID; 
                            curout.modList.Add(nuMod);
                            if(exout.useLeveling){
                                if(nuMod.hasAddValue){
                                    nuMod.addModifier.modValue = 0f - curout.levelModStat.CurrentValue;
                                    curout.lvlModList.Add(nuMod.addModifier);
                                }
                                if(nuMod.hasMultiValue){
                                    nuMod.multiModifier.modValue = 0f - curout.levelModStat.CurrentValue;
                                    curout.lvlModList.Add(nuMod.multiModifier);
                                }
                            }
                        }
                 }
              });
            }
        }
        internal static void EarlyUpgrade(ILContext il){
            var c = new ILCursor(il);
            if(c.TryGotoNext(MoveType.After,x => x.MatchStfld(typeof(TailorNpc).GetField("currentMod",(System.Reflection.BindingFlags)(-1))), x=> x.MatchLdarg(out _),x=> x.MatchLdfld(out _))){
              c.Emit(OpCodes.Ldarg_0);
              c.Emit(OpCodes.Ldfld,typeof(TailorNpc).GetField("currentOutfit",(System.Reflection.BindingFlags)(-1)));
              c.EmitDelegate<Func<OutfitModStat,Outfit,OutfitModStat>>((mod,outfit) =>{
                if(mod.modType == CustomModType && (mod.modifierID == null || mod.modifierID == String.Empty)){
                   LegendAPI.Logger.LogDebug("Fixing broken modifierID");
                   mod.modifierID = (outfit.outfitID == Outfit.normalID) ? shadowSource : outfit.outfitID;
                }
                return mod;
              });
            }
            if(c.TryGotoNext(x => x.MatchLdcI4(1),x => x.MatchCallOrCallvirt(typeof(Outfit).GetMethod("SetEquipStatus")))){
               c.EmitDelegate<Func<Player,Player>>((p) => {p.outfitEnhanced = true; return p;});
            }
        }
        internal static string CustomModDescription(On.OutfitModStat.orig_GetDescription orig, OutfitModStat self, bool addExtra) {
            var result = orig(self,addExtra);
            if(self.modType == CustomModType && OutfitCatalog.ContainsKey(self.modifierID))
                result = OutfitCatalog[self.modifierID].customDesc(addExtra,self);// + (((!addExtra) || !(self.hasAddValue || self.hasMultiValue || self.hasOverrideValue) )? string.Empty : (" <color=#009999>( </color><color=#00dddd>" + (self.hasAddValue ? Globals.PercentToStr(self.addModifier, (!self.isIncrease) ? "-" : "+") : (self.hasMultiValue ? Globals.PercentToStr(self.multiModifier, (!self.isIncrease) ? "-" : "+") : ((!self.hasOverrideValue) ? string.Empty : ((int)self.overrideModifier.modValue).ToString()))) + "</color><color=#009999> )</color>"));
            if(self.modType == CopyCatModType)
                result = String.Empty;
            return result;

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
            if(actualOutfit != Outfit.normalID && actualOutfit != "default"){
             shadowSource = actualOutfit;
             if(OutfitCatalog.ContainsKey(actualOutfit)){
                 Outfit.normalOutfit.useLeveling = OutfitCatalog[actualOutfit].outfit.useLeveling;
             }
             foreach (OutfitModStat Mod in Outfit.normalOutfit.modList) {
                 if (!Mod.modType.Equals(CustomModType))
                     continue;
                 Mod.modifierID = actualOutfit;
             }
            }
        }
    }
    public class OutfitInfo {
        //public static int extra = 0;
        public Outfit outfit = new Outfit("newHope", Outfit.baseHope.outfitColorIndex, new List<OutfitModStat>());
        public string name = "UNNAMED";
        public Func<bool> unlockCondition = () => { return true; };
        public Action<Player, bool, bool,OutfitModStat> customMod = (p, b1, b2,modifier) => { return; };
        public Func<bool,OutfitModStat,string> customDesc = (addExtra,modifier) => { return ""; };
    }
}
