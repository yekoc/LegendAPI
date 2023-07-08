using BepInEx;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using MonoMod.RuntimeDetour;
using MonoMod.Cil;
using Mono.Cecil.Cil;

namespace LegendAPI {
    public static class Items {
        internal static Dictionary<string, ItemInfo> ItemCatalog = new Dictionary<string, ItemInfo>();
        internal static Dictionary<string, string[]> RecipeCatalog = new Dictionary<string, string[]>();
        internal static Dictionary<string, List<string>> GroupCatalog = new Dictionary<string, List<string>>();
	internal static bool enabled = true;
        internal static bool hooked = false;
        internal static bool init = false;
        public static void Awake() {
            On.GameController.Awake += (orig, self) => {
                orig(self);
                if(!hooked){
                On.TextManager.GetItemName += CustomItemText;
                On.LootManager.ResetAvailableItems += CatalogToDict;
                On.IconManager.GetItemIcon += CustomItemIcon;
                IL.RunHistoryEntryUI.Load += TrophyCaseUnknown;
                IL.LootManager.GetLockedItemID += CustomItemUnlock;
                IL.Inventory.AddItem_Item_bool_bool += GroupItemTrigger;
                hooked = true;
                init = true;
                }
            };
        }
        public static void LateInit() {
            if(ItemCatalog.Count == 0 && RecipeCatalog.Count == 0 && GroupCatalog.Count == 0){
                    enabled = false;
            }
            if(enabled){
               foreach (string Result in RecipeCatalog.Keys) {
                    if (!ItemRecipe.recipes.ContainsKey(Result)) {
                        ItemRecipe.recipes.Add(Result, RecipeCatalog[Result]);
                    }
                    else {
                        ItemRecipe.recipes[Result] = RecipeCatalog[Result];
                    }
                }
                foreach (string Group in GroupCatalog.Keys) {
                    if (!LootManager.completeItemDict.ContainsKey(Group)) {
                        LegendAPI.Logger.LogError($"Group: {Group} has no GroupItem associated,skipping.");
                        continue;
                    }
                    if (!GroupItemManager.groupsDict.ContainsKey(Group)) {
                        GroupItemManager.groupsDict.Add(Group, GroupCatalog[Group]);
                    }
                    else {
                        GroupItemManager.groupsDict[Group].AddRange(GroupCatalog[Group]);
                    }
                }
            }
            else if(hooked){	
             On.TextManager.GetItemName -= CustomItemText;
             On.LootManager.ResetAvailableItems -= CatalogToDict;
             On.IconManager.GetItemIcon -= CustomItemIcon;
             IL.LootManager.GetLockedItemID -= CustomItemUnlock;
             hooked = false;
            }
	}

        public static void Register(ItemInfo Info) {
            if(!enabled && init){ 
                if(!hooked){
                On.TextManager.GetItemName += CustomItemText;
                On.LootManager.ResetAvailableItems += CatalogToDict;
                On.IconManager.GetItemIcon += CustomItemIcon;
                IL.LootManager.GetLockedItemID += CustomItemUnlock;
                hooked = true;
                }
            }
            if (Info.text.Equals(default(global::TextManager.ItemInfo))) {
                Info.text = new global::TextManager.ItemInfo();
                Info.text.displayName = Info.name;
                Info.text.itemID = Info.item.ID;
                Info.text.description = Info.name;
            }
            if (!ItemCatalog.ContainsKey(Info.item.ID)) {
                ItemCatalog.Add(Info.item.ID, Info);
            }
            else {
                ItemCatalog[Info.item.ID] = Info;
            }
            if (Info.group != null) {
                if (!GroupCatalog.ContainsKey(Info.group)){
                    GroupCatalog.Add(Info.group, new List<string>());
                }
                GroupCatalog[Info.group].Add(Info.item.ID);
                if(Info.item.parentGroupID == null || Info.item.parentGroupID == String.Empty){
                    Info.item.parentGroupID = Info.group;
                }
            }
        }
        public static void Register(Item item) {
            ItemInfo Item = new ItemInfo();
            Item.item = item;
            Item.name = Item.item.ID;
            Item.text.displayName = Item.name;
            Item.text.itemID = Item.name;
            Item.text.description = Item.name;
            Register(Item);
        }
        public static void RegisterRecipe(string Result, string[] ingredients) {
            if (!RecipeCatalog.ContainsKey(Result)) {
                RecipeCatalog.Add(Result, ingredients);
            }
            else {
                RecipeCatalog[Result] = ingredients;
            }
        }

        private static void GroupItemTrigger(ILContext il){
            ILCursor c = new ILCursor(il);
            if(c.TryGotoNext(MoveType.After,x => x.MatchCallOrCallvirt(typeof(Item).GetMethod("Activate")))){
              c.Emit(OpCodes.Ldarg_1);
              c.EmitDelegate<Action<Item>>((item) => item.GroupItemCreated(item.parentGroupID));
            }
        }
        private static void CatalogToDict(On.LootManager.orig_ResetAvailableItems orig) {
            foreach (ItemInfo Info in ItemCatalog.Values) {
                if (!LootManager.completeItemDict.ContainsKey(Info.item.ID)) {
                    LootManager.completeItemDict.Add(Info.item.ID, Info.item);
                }
                else {
                    LootManager.completeItemDict[Info.item.ID] = Info.item;
                }
                if (Info.tier >= LootManager.maxItemTier) {
                    for (int i = LootManager.maxItemTier; i <= Info.tier; i++)
                        LootManager.itemTierDict.Add(i, new List<string>());
                    LootManager.maxItemTier = Info.tier + 1;
                }
                foreach (List<string> Tier in LootManager.itemTierDict.Values) {
                    if (Tier.Contains(Info.item.ID)) {
                        Tier.Remove(Info.item.ID);
                    }
                }
                if(Info.priceMultiplier != 1)
                  LootManager.PriceDict[Info.item.ID] = Info.priceMultiplier != -1? Info.priceMultiplier : Info.item.isCursed? 0 : 1;
                if(!Info.item.isCursed)
                 LootManager.itemTierDict[Info.tier].Add(Info.item.ID);
            }
            LateInit();
            orig();
        }
        private static string CustomItemText(On.TextManager.orig_GetItemName orig, string givenID) {
            foreach (ItemInfo Info in ItemCatalog.Values) {
                if (!TextManager.itemInfoDict.ContainsKey(Info.item.ID)) {
                    TextManager.itemInfoDict.Add(Info.item.ID, Info.text);
                }
                else {
                    TextManager.itemInfoDict[Info.item.ID] = Info.text;
                }
            }
            return orig(givenID);
        }
        private static Sprite CustomItemIcon(On.IconManager.orig_GetItemIcon orig,string givenID) {
           if(ItemCatalog.ContainsKey(givenID)){
		Sprite icon = ItemCatalog[givenID].icon??IconManager.ItemIcons[IconManager.unavailableItemName];
                if (!IconManager.ItemIcons.ContainsKey(givenID)) {
                        IconManager.ItemIcons.Add(givenID,icon);
                }
		else if(IconManager.ItemIcons[givenID] != icon) {
                        IconManager.ItemIcons[givenID] = icon;
                }
	   }
	   return orig(givenID);
        }
        private static void CustomItemUnlock(ILContext il){
            ILCursor c = new ILCursor(il);
            ILLabel lab = c.DefineLabel(),targetLab = c.DefineLabel();
            c.MarkLabel(targetLab);
            if(c.TryGotoNext( x => x.MatchBle(out lab))){
                c.GotoLabel(lab);
                c.MoveBeforeLabels();
                c.Emit(OpCodes.Ldloc_0);
                c.EmitDelegate<Func<string,bool>>((id) => !ItemCatalog.ContainsKey(id) || ItemCatalog[id].unlockCondition());
                c.Emit(OpCodes.Brfalse,targetLab);
            }
            else{
                LegendAPI.Logger.LogError("ILHook for GetLockedItemID failed.");
            }
        }
        private static void TrophyCaseUnknown(ILContext il){
           ILCursor c = new ILCursor(il);
           ILLabel lab = c.DefineLabel();
           if(c.TryGotoNext( x=> x.MatchLdsfld(typeof(LootManager).GetField("completeItemDict",(BindingFlags)(-1)))) && c.TryFindNext(out _, x => x.MatchBrfalse(out lab)) ){
               c.Emit(OpCodes.Ldloc_2);
               c.Emit(OpCodes.Ldarg_1);
               c.EmitDelegate<Func<int,RunHistoryEntry,bool>>((orig,run) => LootManager.completeItemDict.ContainsKey(run.relicIDs[orig]));
               c.Emit(OpCodes.Brfalse,lab);
           }
           else{
             LegendAPI.Logger.LogError("ILHook for RunHistoryEntryUI failed.");
           }
        }
        /*	static public void Headgearify<T>(GameObject headgearPrefab){
              new Hook(typeof(T).GetMethod("Activate"),(Action orig,T self) =>{
                orig(self);
                if(headgearPrefab == null,self.parentEntity == null){
                 return;
                }
                ExtraGear[self.ID] = Globals.ChaosInst<Headgear>(headgearPrefab, (!self.parentEntity.headPosition) ? self.parentEntity.transform : self.parentEntity.headPosition.transform, null, null);
              });
              new Hook(typeof(T).GetMethod("Deactivate"),((Action orig,T self) =>{
                if(ExtraGear[self.ID] != null)
                 ExtraGear[self.ID].Hide(true,false);
                orig(self);
              } ));
            }
            static public void Headgearify<T>(string PrefabAssetPath){
              Headgearify<T>(ChaosBundle.Get(PrefabAssetPath));
            }
        */
    }

    public class ItemInfo {
        //public static int extra = 0;
        public Item item = new PlayerStartItem();
        public string name = "UNNAMED";
        public int tier = 0;
        public global::TextManager.ItemInfo text;
        public string group = null;
        public Sprite icon = null;
        public int priceMultiplier = -1;
        public Func<bool> unlockCondition = () => { return true; };
        /*
              public static ItemInfo GetInfo(string givenID){
                if(Items.ItemCatalog.ContainsKey(givenID))
                  return Items.ItemCatalog[givenID];
                ItemInfo request = new ItemInfo();
                if(!LootManager.completeItemDict.ContainsKey(givenID)){
                  LegendAPI.Logger.LogError("Someone Attempted to get a non-existent ItemInfo,returning Extra Museum Ticket");
                  request.item.ID= request.item.ID + ItemInfo.extra++;
                }
                else
                 request.item = LootManager.completeItemDict[givenID];
                 request.name = TextManager.GetItemName(givenID);
                 request.tier = LootManager.GetItemTier(givenID);
                 request.text = TextManager.itemInfoDict[givenID];
                 request.icon = IconManager.GetItemIcon(givenID);
                 request.group = null;
                 foreach (string group in GroupItemManager.groupsDict.Keys){
                  if (GroupItemManager.groupsDict[group].Contains(givenID))
                   request.group = group;
                 }
                return request;
              }*/
    }


}
