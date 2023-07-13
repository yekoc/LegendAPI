using BepInEx;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using UnityEngine;
using MonoMod.RuntimeDetour;
using MonoMod.Cil;
using Mono.Cecil.Cil;

namespace LegendAPI {
    public static class Skills {
        internal static Dictionary<string, SkillInfo> SkillCatalog = new Dictionary<string, SkillInfo>();
	internal static bool enabled = true;
        internal static bool hooked = false;
        internal static bool init = false;
        internal static string fakeLocalForThatOneILHook = String.Empty;
        public static void Awake() {
            On.GameController.Awake += (orig, self) => {
                orig(self);
                if(!hooked){
                 On.LootManager.ResetAvailableSkills += CatalogToDict;
                 IL.RunHistoryEntryUI.Load += TrophyCase;
                 IL.Player.InitFSM += CatalogToFSM;
                 On.StatManager.LoadPlayerSkills += CatalogToStats;
                 On.CooldownEntry.SetSkillBGSprite += SetBGSprite;
                 On.SpellBookUI.AddSkillToInfoSkills += HiddenInfoSkill;
                 IL.SpellBookUI.LoadInfoPage += HiddenInfoIndex;
                 IL.SpellBookUI.LoadEleSkillDict += HiddenSpellPage;
                 IL.LoadoutUI.UpdateEntry += CameraBGSprite;
                 IL.LoadoutUI.UpdateCurrentLoadout += CameraBGSprite;
                 hooked = true;
                 init = true;
                }
            };
        }
        public static void LateInit() {
            enabled = SkillCatalog.Count != 0;
            if(hooked && !enabled){	
             On.LootManager.ResetAvailableSkills -= CatalogToDict;
             IL.RunHistoryEntryUI.Load -= TrophyCase;
             IL.Player.InitFSM -= CatalogToFSM;
             On.StatManager.LoadPlayerSkills -= CatalogToStats;
             On.CooldownEntry.SetSkillBGSprite -= SetBGSprite;
             On.SpellBookUI.AddSkillToInfoSkills -= HiddenInfoSkill;
             IL.SpellBookUI.LoadInfoPage -= HiddenInfoIndex;
             IL.SpellBookUI.LoadEleSkillDict -= HiddenSpellPage;
             IL.LoadoutUI.UpdateEntry += CameraBGSprite;
             IL.LoadoutUI.UpdateCurrentLoadout += CameraBGSprite;
             hooked = false;
            }
	}

        public static void Register(SkillInfo info) {
            if(!enabled && init){ 
             On.LootManager.ResetAvailableSkills += CatalogToDict;
             IL.RunHistoryEntryUI.Load += TrophyCase;
             IL.Player.InitFSM += CatalogToFSM;
             On.StatManager.LoadPlayerSkills += CatalogToStats;
             On.CooldownEntry.SetSkillBGSprite += SetBGSprite; 
             On.SpellBookUI.AddSkillToInfoSkills += HiddenInfoSkill;
             IL.SpellBookUI.LoadInfoPage += HiddenInfoIndex;
             IL.LoadoutUI.UpdateEntry += CameraBGSprite;
             IL.LoadoutUI.UpdateCurrentLoadout += CameraBGSprite;
            }
            SkillCatalog.Add(info.ID,info);
        }



        private static void CameraBGSprite(ILContext il){
            ILCursor c = new ILCursor(il);
            if(c.TryGotoNext(x => x.MatchCallOrCallvirt(typeof(LoadoutUI).GetMethod(nameof(LoadoutUI.GetArcanaBGIndex),(BindingFlags)(-1))),x => x.MatchLdelemRef())){
              c.EmitDelegate<Func<string,string>>((str) =>{ fakeLocalForThatOneILHook = str; return str;}); 
              c.Index+= 2;
              c.EmitDelegate<Func<Sprite,Sprite>>((orig) => (SkillCatalog.ContainsKey(fakeLocalForThatOneILHook) && SkillCatalog[fakeLocalForThatOneILHook].bgSpriteFull)? SkillCatalog[fakeLocalForThatOneILHook].bgSpriteFull : orig); 
            }
        }

        private static void HiddenInfoSkill(On.SpellBookUI.orig_AddSkillToInfoSkills orig,SpellBookUI self,Player.SkillState state,ElementType ele){
            var skill = SkillCatalog.Values.Where((i) => i.stateType == state.GetType());
            if(!skill.Any() || !skill.First().hidden){
              orig(self,state,ele);
            }
        }
        private static void HiddenInfoIndex(ILContext il){
            ILCursor c = new ILCursor(il);
            if(c.TryGotoNext(MoveType.After,x => x.MatchCallOrCallvirt(typeof(List<Player.SkillState>).GetMethod("IndexOf",new Type[]{typeof(Player.SkillState)})))){
               c.EmitDelegate<Func<int,int>>((orig) => orig < 0 ? 0 : orig);
            }
        }
        private static void HiddenSpellPage(ILContext il){
            ILCursor c = new ILCursor(il);
            var indexindex = -1;
            if(c.TryGotoNext(MoveType.After,x => x.MatchLdloc(out indexindex),x=>x.MatchLdfld(typeof(Player.SkillState).GetField("isUnlocked")))){
                c.Emit(OpCodes.Ldloc,indexindex);
                c.EmitDelegate<Func<Player.SkillState,bool>>((state) => !SkillCatalog.ContainsKey(state.skillID) || !SkillCatalog[state.skillID].hidden);
                c.Emit(OpCodes.And);
            }
        }
        private static void CatalogToDict(On.LootManager.orig_ResetAvailableSkills orig) {
            foreach(var skill in SkillCatalog.Values){
               if(!LootManager.completeSkillList.Contains(skill.ID)){
                 LootManager.completeSkillList.Add(skill.ID);
               }
               if(skill.tier >= LootManager.maxSkillTier){
                 for(int i = LootManager.maxSkillTier; i <= skill.tier;i++){
                    LootManager.skillTierDict.Add(i,new List<string>());
                 }
                 LootManager.maxSkillTier = skill.tier + 1;
               }
               else{
                foreach(var list in LootManager.skillTierDict.Values){
                   if(list.Contains(skill.ID)){
                       list.Remove(skill.ID);
                   }
                }
               }
               LootManager.skillTierDict[skill.tier].Add(skill.ID);
               LootManager.PriceDict[skill.ID] = skill.priceMultiplier != -1? skill.priceMultiplier : 1;
               var icon = skill.icon ? skill.icon : IconManager.SkillIcons[IconManager.unavailableSkillName];
               if(!IconManager.SkillIcons.ContainsKey(skill.ID)){
                   IconManager.skillIcons.Add(skill.ID,icon);
               }
               else if(IconManager.skillIcons[skill.ID] != icon){
                   IconManager.skillIcons[skill.ID] = icon;
               }
               TextManager.SkillInfo text = new TextManager.SkillInfo{
                   skillID = skill.ID,
                   description = skill.description,
                   displayName = skill.displayName,
                   empowered = skill.enhancedDescription
               };
               if(!TextManager.skillInfoDict.ContainsKey(skill.ID)){
                   TextManager.skillInfoDict.Add(skill.ID,text);
               }
               else{
                   TextManager.skillInfoDict[skill.ID] = text;
               }

            }
            LateInit();
            orig();
        }
        private static void CatalogToFSM(ILContext il){
           ILCursor c = new ILCursor(il);
           if(c.TryGotoNext(x => x.MatchLdsfld(typeof(GameDataManager).GetField("gameData",(BindingFlags)(-1))) )){
             c.Emit(OpCodes.Ldarg_0);
             c.EmitDelegate<Action<Player>>((player) =>{foreach(var info in SkillCatalog.Values){
                Player.SkillState stat = (Player.SkillState)Activator.CreateInstance(info.stateType,player.fsm,player);
                if(!player.fsm.states.ContainsKey(stat.name)){
                  player.fsm.AddState(stat);
                }
                else{
                  player.fsm.states[stat.name] = stat;
                }
               if(stat.element == ElementType.Chaos && !info.hidden){
                   if(!LootManager.chaosSkillList.Contains(info.ID)){
                     LootManager.chaosSkillList.Add(info.ID);
                   }
               }
             }});
           }
        }
        private static void TrophyCase(ILContext il){
           ILCursor c = new ILCursor(il);
           var indexindex = -1;
           if(c.TryGotoNext(MoveType.After, x=> x.MatchLdfld(typeof(RunHistoryEntry).GetField("arcanaTypes",(BindingFlags)(-1))),x => x.MatchLdloc(out indexindex),x => x.MatchLdelemI4(),x => x.MatchLdelemRef())){
              c.Emit(OpCodes.Ldarg_1);
              c.Emit(OpCodes.Ldloc,indexindex);
              c.EmitDelegate<Func<UnityEngine.Sprite,RunHistoryEntry,int,UnityEngine.Sprite>>((orig,run,index) =>{
                return (SkillCatalog.ContainsKey(run.arcanaIDs[index]) && SkillCatalog[run.arcanaIDs[index]].bgSpriteFull)?  SkillCatalog[run.arcanaIDs[index]].bgSpriteFull : orig;
              });
           }
           else{
             LegendAPI.Logger.LogError("ILHook for RunHistoryEntryUI failed.");
           }
        }
        private static void SetBGSprite(On.CooldownEntry.orig_SetSkillBGSprite orig,CooldownEntry self,bool sig,bool emp){
            orig(self,sig,emp);
            if(self.skillState != null && SkillCatalog.ContainsKey(self.skillState.skillID)){
              var info = SkillCatalog[self.skillState.skillID];
              if( info.bgSpriteIcon){
                self.skillBorder.sprite = info.bgSpriteIcon;
              }
            }
        }
        private static void CatalogToStats(On.StatManager.orig_LoadPlayerSkills orig,string categoryModifier){
            orig(categoryModifier);
            LoadSkillData();
        }
        private static string SkillUnlockCondition(On.LootManager.orig_GetSkillID orig,bool locked,bool sig){
            var text = orig(locked,sig);
            while( SkillCatalog.ContainsKey(text) &&  !SkillCatalog[text].unlockCondition() ){
              if(sig){
                 LootManager.lockedSigList.Add(text);
              }
              if(locked){
                 LootManager.lockedSkillList.Add(text);
              }
              text = orig(locked,sig);
            }
            return text;
        }
        private static void LoadSkillData(string givenID = null){
          if(givenID == null || givenID == string.Empty){
             foreach(var skill in SkillCatalog.Keys){
               LoadSkillData(skill); 
             }
          }
          else if(SkillCatalog.ContainsKey(givenID)){
             SkillStats stat = SkillCatalog[givenID].skillStats;
             stat.Initialize();
             foreach(var cat in StatManager.data[StatManager.statFieldStr].Where((kvp) => kvp.Key.StartsWith(StatManager.playerBaseCategory))){
                StatData data = new StatData(stat,cat.Key);
                List<string> value = data.GetValue<List<string>>("targetNames");
                if ((value.Contains(Globals.allyHBStr) || value.Contains(Globals.enemyHBStr) ) && !value.Contains(Globals.ffaHBStr)){
                        value.Add(Globals.ffaHBStr);
                }
                if ((value.Contains(Globals.allyFCStr) || value.Contains(Globals.enemyFCStr)) && !value.Contains(Globals.ffaFCStr)){
                        value.Add(Globals.ffaFCStr);
                }
                cat.Value[givenID] = data;
                StatManager.globalSkillData[givenID] = data;
             }
          }
        }
    }

    public class SkillInfo{
        public string ID = "unnamedarcanaCHANGETHIS";
        public string displayName = "Unnamed";
        public string description = String.Empty;
        public string enhancedDescription = String.Empty;
        public Sprite icon = null;
        public int tier = 0;
        public Type stateType;
        public SkillStats skillStats;
        public Func<bool> unlockCondition = () => true;
        public bool hidden = false;
        public int priceMultiplier = -1;
        public Sprite bgSpriteIcon = null;
        public Sprite bgSpriteFull = null;
    }
    public class SkillStatsInfo{
       public string ID = "unnamedarcanaCHANGETHIS";
       public string[] targetNames;
       public SkillStatsLevel[] levelInfos = new SkillStatsLevel[]{};

       public static implicit operator SkillStats(SkillStatsInfo info){
           var stats = new SkillStats();
           var maxLevelCache = SkillStats.maxSkillLevel;
           if(info.levelInfos.Length > maxLevelCache)
              SkillStats.maxSkillLevel = info.levelInfos.Length;
           var miS = typeof(Utility).GetMethod(nameof(Utility.SetExtraSkillStat));
           foreach(string id in Utility.defaultStatFields){
             var field = typeof(SkillStats).GetField(id);
             var field2 = typeof(SkillStatsLevel).GetField(id);
             if(field != null && field2 != null){
             var arr = Array.CreateInstance(field.FieldType.GetElementType(),SkillStats.maxSkillLevel);
             for(int i = 0; i < info.levelInfos.Length;i++){
                if(id.Contains("lementType")){
                  arr.SetValue(((ElementType)field2.GetValue(info.levelInfos[i])).ToString(),i);
                }
                else{
                  arr.SetValue(field2.GetValue(info.levelInfos[i]),i);
                }
             }
             field.SetValue(stats,arr);
             }
           }
           
           stats.ID = new string[SkillStats.maxSkillLevel];
           stats.ID[0] = info.ID;
           stats.targetNames = info.targetNames;
           for(int i = 0;i < info.levelInfos.Length;i++){
             foreach(var extra in info.levelInfos[i].extraStats){
                Utility.SetExtraSkillStat(stats,extra.Key,extra.Value,i);
             }
             if(info.levelInfos[i].subElementType == ElementType.Neutral){
               stats.subElementType[i] = stats.elementType[i]; 
             }
           }
           stats.Initialize();
           SkillStats.maxSkillLevel = maxLevelCache;
           return stats;
       }
    }
    public class SkillStatsLevel{ 
	public ElementType elementType = ElementType.Neutral;
	public ElementType subElementType = ElementType.Neutral;
	public bool showDamageNumber = true;
	public bool showDamageEffect = true;
	public bool shakeCamera = true;
	public float sameTargetImmunityTime = 0f;
	public float sameAttackImmunityTime = 0f;
	public bool canHitStun = true;
	public float hitStunDurationModifier = 1f;
	public float knockbackMultiplier = 0f;
	public bool knockbackOverwrite = false;
	public int damage = 0;
	public float cooldown = 0f;
	public float duration = 0f;
	public int hitCount = 0;
	public float damageInterval = 0f;
	public int spawnCount = 0;
	public int baseHealth = 0;
	public float criticalHitChance = 0.05f;
	public float criticalDamageModifier = 1.5f;
	public bool isStatusEffect = false;
	public float rootChance = 0f;
	public float rootDuration = 2f;
	public float chaosChance = 0f;
	public int chaosLevel = 1;
	public float burnChance = 0f;
	public int burnLevel = 1;
	public float slowChance  = 0f;
	public int slowLevel = 1;
	public float poisonChance  = 0f;
	public int poisonLevel = 1;
	public float shockChance  = 0f;
	public int shockLevel = 1;
	public float freezeChance  = 0f;
	public float freezeDuration = 1.5f;
	public float overdriveDamageMultiplier = 1f;
	public float overdriveProgressMultiplier = 1f;
	public bool overdriveSingleIncrease = false;
        public Dictionary<string,object> extraStats = new Dictionary<string, object>();
    }

}
