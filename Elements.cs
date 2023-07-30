
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using UnityEngine;


namespace LegendAPI {
    public static class Elements {
	public static Dictionary<ElementType,ElementInfo> eleDict = new Dictionary<ElementType, ElementInfo>();

	public static void Awake(){
	   On.Health.ctor += (orig,self) => {
	      orig(self);
	      foreach(ElementType ele in eleDict.Keys){
		if(!eleDict[ele].isSubElement)
	          self.elemModDict.Add(ele,new SyncedNumVarStat(1f));
		if(eleDict[ele].statusEffectType != null){
		  self.statusResDict.Add(ele,new SyncedNumVarStat(1f));
		}
	      }
	   };
	   IL.Globals.IntToElement += DefaultToCustomElementSwitchCase;
	   IL.Globals.ElementToInt += DefaultToCustomElementSwitchCase;
	   On.Globals.StrToElement += (orig,name) =>{
		foreach(KeyValuePair<ElementType,ElementInfo> pair in eleDict){
		   if(pair.Value.name == name)
			return pair.Key;
		}
		return orig(name);
	   };
	   On.ElementTypeMethods.IsPrimaryElement += (orig,element) =>{
		return eleDict.ContainsKey(element)? !(eleDict[element].isSubElement) : orig(element);
	   };
	   On.ElementTypeMethods.IsSubElement += (orig,element) =>{
		return eleDict.ContainsKey(element)? (eleDict[element].isSubElement) : orig(element);
	   };
	   IL.Player.SkillState.SetSkillData += (il) =>{
		ILCursor c = new ILCursor(il);
		if(c.TryGotoNext(x=>x.MatchLdstr("elementType"))){
		  c.Index -= 2;
		  c.Emit(OpCodes.Pop);
		  c.GotoNext(x=>x.MatchCallOrCallvirt(out _),x=>x.MatchCallOrCallvirt(out _));
		  c.Index++;
		  c.RemoveRange(2);
		  c.EmitDelegate<Func<string,ElementType>>(Globals.StrToElement);
		}
		else
		  LegendAPI.Logger.LogError("SetSkillData hook failed,skills will not associate with custom elements.");
	   };
           On.NewSpellHandler.ctor += (orig,self) => {
               orig(self);
               foreach(var element in eleDict.Keys.Where((key) => !eleDict[key].isSubElement && !self.elementUnlockCount.ContainsKey(key))){
                   self.elementUnlockCount.Add(element,0);
               }
           };
           IL.SpellBookUI.LoadEleSkillDict += (il) =>{ 
		ILCursor c = new ILCursor(il);
                if(c.TryGotoNext(x=>x.MatchLdarg(1),x=>x.MatchLdfld(typeof(Player).GetField("skillsDict")))){
                    c.Emit(OpCodes.Ldarg_0);
                    c.EmitDelegate<Action<SpellBookUI>>((self) =>{
                      int indexCount = self.eleIndexDict.Count;
                      self.eleIndexDict.Add(indexCount++,ElementType.Neutral);
                      self.eleSpellDict.Add(ElementType.Neutral,new List<Player.SkillState>());
                      self.eleOverdriveDict.Add(ElementType.Neutral,new List<Player.SkillState>());
                      self.infoEleSkillTupleList.Add(new Tuple<ElementType,List<Player.SkillState>>(ElementType.Neutral,new List<Player.SkillState>()));
                      foreach(var element in eleDict.Keys.Where((key) => !eleDict[key].isSubElement)){
                        self.eleIndexDict.Add(indexCount++,element);
                        self.eleSpellDict.Add(element,new List<Player.SkillState>());
                        self.eleOverdriveDict.Add(element,new List<Player.SkillState>());
                        self.infoEleSkillTupleList.Add(new Tuple<ElementType, List<Player.SkillState>>(element, new List<Player.SkillState>()));
                      }
                    });
                }
                else
                 LegendAPI.Logger.LogError("LoadEleSkillDict hook failed,spellbook won't be able to show spells with custom elements");
           };
           On.SBCardInfoPageUI.InitMiniElementIndicator += (orig,self) =>{
               orig(self);
               foreach(var element in eleDict.Keys.Where((key) => !eleDict[key].isSubElement)){
                   self.miniEleIconDict[element] = UnityEngine.Object.Instantiate(self.miniEleIconDict[ElementType.Fire],self.miniElementIcons);
                   self.miniEleIconDict[element].GetComponent<UnityEngine.UI.Image>().sprite = eleDict[element].icon ?? IconManager.ItemIcons[IconManager.unavailableItemName]; 
               }
           };
           On.SpellBookUI.SetObjectReferences += (orig,self) => {
               self.maxOverdriveCount = (self.eleOverdriveDict?.Values.Aggregate(6,(longest,next) => next.Count > longest? next.Count : longest))??6;
               orig(self); 
               foreach(var element in eleDict.Keys.Where((key) => !eleDict[key].isSubElement)){
                   self.sbRef.eleInkIconDict[element] = UnityEngine.Object.Instantiate(self.sbRef.eleInkIconDict[ElementType.Fire],self.sbRef.inkElementsObj.transform);
                   self.sbRef.eleInkIconDict[element].GetComponent<UnityEngine.UI.Image>().sprite = eleDict[element].iconInk ?? IconManager.ItemIcons[IconManager.unavailableItemName]; 
               }
           };
           On.ElementSelectionUI.Start += (orig,self) =>{
               orig(self);
               foreach(ElementInfo info in eleDict.Values.Where((info) => !self.elementSprites.ContainsKey(info.name))){
                  self.elementSprites[info.name] = info.icon;
               }
           };
	   On.ElementalResistanceUpOnHurt.ctor += (orig,self) => {
	      orig(self);
	      foreach(ElementType ele in eleDict.Keys){
	          self.elementList.Add(ele);
		}
	   };
	   On.ElementalResistanceUpOnHurt.SetModStatus += (orig,self,flag) => {
	      orig(self,flag);
	      for(int i = 5; i < self.elementList.Count();i++){
		ElementType e = self.elementList[i];
		if(!eleDict.ContainsKey(e))
		  continue;
		if(!eleDict[e].isSubElement)
		  self.parentEntity.health.elemModDict[e].Modify(self.resistMod, flag);
		if(eleDict[e].statusEffectType != null)
		  self.parentEntity.health.statusResDict[e].Modify(self.resistMod, flag);
	      }
	   };
	   On.ElementalResistanceUpOnHurt.OnTakeDamageActual += (orig,self,atk,ent) => {
	      orig(self,atk,ent);
	      for(int i = 5; i < self.elementList.Count();i++){
		ElementType e = self.elementList[i];
		if(!eleDict.ContainsKey(e))
		  continue;
		if(!eleDict[e].isSubElement)
		  self.parentEntity.health.elemModDict[e].Modify(self.resistMod, true);
		if(eleDict[e].statusEffectType != null)
		  self.parentEntity.health.statusResDict[e].Modify(self.resistMod, true);
	      }
	   };
	   IL.ElementalResistanceUp.SetModStatus += (il) =>{
		ILCursor c =new ILCursor(il);
		ILLabel lab = c.DefineLabel();
		if(c.TryGotoNext(x => x.MatchLdfld(typeof(Health),nameof(Health.statusResDict))) && c.TryGotoPrev(x=>x.MatchBrfalse(out lab)) && c.TryGotoNext(MoveType.After,x => x.MatchCallOrCallvirt(typeof(VarStat<float>).GetMethod("Modify")))){
		  c.Emit(OpCodes.Ldarg_0);
		  c.EmitDelegate<Func<ElementalResistanceUp,bool>>((self) => {return eleDict.ContainsKey(self.element)? eleDict[self.element].statusEffectType != null : true;});
		  c.Emit(OpCodes.Brfalse,lab);
		}
	   };
	   IL.BurstOnDotItem.OnDotApply += (il) =>{
		ILCursor c = new ILCursor(il);
		c.Index = -1;
		c.MoveAfterLabels();
		c.Emit(OpCodes.Ldarg_0);
		c.Emit(OpCodes.Ldarg_1);
		c.EmitDelegate<Action<BurstOnDotItem,ElementType>>((self,element) => {
		  if(eleDict.ContainsKey(element) && eleDict[element].elementalBurstType != null){
		    eleDict[element].elementalBurstType.GetMethod("CreateBurst").Invoke(null,new object[] {self.parentPlayer.hurtBoxTransform.position,self.parentPlayer.skillCategory,self.ID,1,BurstOnDotItem.burstRadius});
		  }
		});
	   };
	   IL.AoEStatusEffects.OnDoTEvent += (il) => {
		ILCursor c = new ILCursor (il);
		c.Index = -1;
		c.MoveAfterLabels();
		c.Emit(OpCodes.Ldloc,0);
		c.Emit(OpCodes.Ldloc,1);
		c.Emit(OpCodes.Ldarg,2);
		c.EmitDelegate<Action<AttackInfo,Vector2,ElementType>>((info,vec,element)=>{
		  if(eleDict.ContainsKey(element) && eleDict[element].elementalBurstType != null){
		    if(eleDict[element].statusEffectType != null)
		      
		    eleDict[element].elementalBurstType.GetMethod("CreateBurst").Invoke(null,new object[] {vec,info,AoEStatusEffects.burstRadius});
		  }
		});
	   };
	   On.RandomElementalEnemies.SetEventHandlers += (orig,self,active) =>{
		orig(self,active);
		foreach(KeyValuePair<ElementType,ElementInfo> pair in eleDict){
		  if(!pair.Value.isSubElement)
		    self.parentPlayer.health.elemModDict[pair.Key].Modify(self.resistMod,active);
		  if(pair.Value.statusEffectType != null)
		    self.parentPlayer.health.statusResDict[pair.Key].Modify(self.resistMod,active);
		}
	   };
	   IL.Health.TakeDamage += HookTakeDamage;
	   Health.globalTakeDamageActualHandlers += (AttackInfo givenInfo,Entity atkEnt,Entity caller) => {
                if(givenInfo == null || !caller){
                   return;
                }
		if(eleDict.ContainsKey(givenInfo.elementType) && HandleCustomStatus(caller.health,givenInfo,givenInfo.elementType,atkEnt))
		  caller.health.statusElementType = givenInfo.elementType;
		foreach(ElementType element in eleDict.Keys){
		  if(element != givenInfo.elementType && HandleCustomStatus(caller.health,givenInfo,element,atkEnt)){
		    caller.health.statusElementType = element;
		    return;
		  }
		}
	   };
	   On.StatData.LoadStatDict += (orig,self,givenSS) => {
		orig(self,givenSS);
		float[] template = new float[givenSS.burnChance.Count()];
		foreach(var item in eleDict.Values.Where((item) => item.statusEffectType != null)){
		 List<float> tempList = new List<float>(template);
		 self.statDict.Add(item.statusEffectChanceString,tempList);
		}
	   };
	   On.ElementalStatusUp.ElementToStatusEffect += (orig,self,element) => {
		return eleDict.ContainsKey(element)? eleDict[element].statusEffectChanceString : orig(self,element);
	   };

           IL.RandomDisasters.SpawnDisaster += (il) => {
               ILCursor c = new ILCursor(il);
               int rand = -1;
               ILLabel lab = c.DefineLabel();
               if(c.TryGotoNext(x=>x.MatchLdcI4(0),x=>x.MatchLdcI4(5),x=>x.MatchCallOrCallvirt(out _),x => x.MatchStloc(out rand))){
                   c.Index+=2;
                   c.EmitDelegate<Func<int,int>>((coun) => coun + eleDict.Values.Count((el) => el.spawnDisaster != null));
                   c.GotoNext(MoveType.After,x=>x.MatchSwitch(out _),x=>x.MatchBr(out _));
                   c.GotoLabel((ILLabel)(c.Prev.Operand));
                   lab = (ILLabel)c.Prev.Operand;
                   c.Emit(OpCodes.Ldloc,rand);
                   c.Emit(OpCodes.Ldarg_0);
                   c.EmitDelegate<Func<int,RandomDisasters,bool>>((ran,self) => {
                        if(ran == 4){
                          return false;
                        }
                        self.parentEntity.StartCoroutine(eleDict.Values.Where((el) => el.spawnDisaster != null).ElementAt(ran-4).spawnDisaster?.Invoke(self.parentPosition));
                        return true;
                   });
                   c.Emit(OpCodes.Brtrue,lab);
               }
           };

           IL.Health.GetElementalDamageModifier += ElementalDamageModifier;
           IL.HealthProxy.GetElementalDamageModifier += ElementalDamageModifierProxy;
           new MonoMod.RuntimeDetour.Hook(typeof(Enum).GetMethod("ToString",System.Type.EmptyTypes),typeof(Elements).GetMethod("ToStringRedir",(BindingFlags)(-1)));
           new MonoMod.RuntimeDetour.Hook(typeof(Enum).GetMethod("Parse",new Type[]{typeof(Type),typeof(string)}),typeof(Elements).GetMethod("ParseRedir",(BindingFlags)(-1)));
           new MonoMod.RuntimeDetour.Hook(typeof(SpellBookUI).GetProperty("MaxSpellPageCount",(BindingFlags)(-1)).GetGetMethod(true),typeof(Elements).GetMethod("SBPageCount"));
        }

        public static int SBPageCount(Func<SpellBookUI,int> orig,SpellBookUI self){
          _ = orig(self);
          return (Player.elementSkillDict[self.currentElement].Count / SBSpellPageUI.MaxSpellCount);
        }
        public static string ToStringRedir(Func<Enum,string> orig,Enum self){
           return ((self.GetType() == typeof(ElementType)) && eleDict.ContainsKey((ElementType)self))? eleDict[(ElementType)self].name : orig(self);
        }
        public static Enum ParseRedir(Func<Type,string,Enum> orig,Type type,string data){
           if(type == typeof(ElementType)){
               foreach(var elePair in eleDict){
                   if(elePair.Value.name == data || elePair.Key.ToString() == data)
                       return elePair.Key;
               }
           }
           return orig(type,data);
        }

	public static ElementType Register(ElementInfo info){
		ElementType id = (ElementType)info.name.GetHashCode();
		int failsafe = 1;
		while((id >= ElementType.Fire && id <= ElementType.Poison) || eleDict.ContainsKey(id)){
		  id = (ElementType)((int)id + (failsafe % 2) *info.color.GetHashCode() + (failsafe % 2 +1) * info.name.GetHashCode());
		  if(failsafe++ > 10){
		    LegendAPI.Logger.LogError("Element Registration failed due to hash collision,this really shouldn't be happening unless you are adding the same element repeatedly.");
		    return (ElementType)0;
		  }
		}
                if(info.icon && !info.iconInk){
                    info.iconInk = info.icon;
                }
		if(info.statusEffectType != null && info.statusEffectChanceString == null){
		   info.statusEffectChanceString = info.name + "StatusChance";
		}
		eleDict.Add(id,info);
		if(!info.isSubElement){
		  Player.elementSkillDict.Add(id,new List<string>());
		}
		if(info.weakTo.Count() != 0){
		  Health.eleWheelDict[id] = info.weakTo.First();
		  Ward.addedWeakenessDict[id] = info.weakTo.First();
		}
		ElementalStatusEffect.elementColorDictionary.Add(id,info.color);
		return id;
	}

	internal static void DefaultToCustomElementSwitchCase(ILContext il){
		ILCursor c = new ILCursor(il);
		c.Index = -1;
		if(c.TryGotoPrev(x=>x.MatchLdcI4(0),x=>x.MatchRet())){
		   c.Index++;
		   c.Emit(OpCodes.Pop);
		   c.Emit(OpCodes.Ldarg_0);
		   c.EmitDelegate<Func<int,int>>((input) =>{ return eleDict.ContainsKey((ElementType)input)? input : 0;});
		}
		else
		   LegendAPI.Logger.LogError("Element <=> Int conversion hook failed,relevant functions will break");
	}
	internal static void ElementalDamageModifierProxy(ILContext il){
		ILCursor c = new ILCursor(il);
		ILLabel lab = c.DefineLabel();
		if(c.TryGotoNext(MoveType.After,x=>x.MatchStfld(typeof(HealthProxy).GetField(nameof(HealthProxy.eleDmgMulti),(BindingFlags)(-1))),x=>x.MatchBr(out lab))){
                   c.MoveAfterLabels();
		   c.Emit(OpCodes.Ldarg_0);
		   c.Emit(OpCodes.Ldarg_1);
		   c.EmitDelegate<Func<HealthProxy,ElementType,bool>>((self,element) =>{
		     if(eleDict.ContainsKey(self.entityElement) && eleDict[self.entityElement].weakTo.Contains(element)){
		       self.eleDmgMulti = 2;
		       return true;
		     }
		     else if (eleDict.ContainsKey(element) && eleDict[element].weakTo.Contains(self.entityElement)){
		       self.eleDmgMulti = -1;
		       return true;
		     }
		     return false;
		   });
		   c.Emit(OpCodes.Brtrue,lab);
		}
		else
		  LegendAPI.Logger.LogError("Element weakness hook failed,custom elements may ignore weaknessess");
	}
	internal static void ElementalDamageModifier(ILContext il){
		ILCursor c = new ILCursor(il);
		ILLabel lab = c.DefineLabel();
		if(c.TryGotoNext(MoveType.After,x=>x.MatchStfld(typeof(Health).GetField(nameof(Health.eleDmgModVal),(BindingFlags)(-1))),x=>x.MatchBr(out lab))){
                   c.MoveAfterLabels();
		   c.Emit(OpCodes.Ldarg_0);
		   c.Emit(OpCodes.Ldarg_1);
		   c.EmitDelegate<Func<Health,ElementType,bool>>((self,element) =>{
		     if(eleDict.ContainsKey(self.tempEleType) && eleDict[self.entityElement].weakTo.Contains(element)){
		       self.eleDmgModVal = 2;
		       return true;
		     }
		     else if (eleDict.ContainsKey(element) && eleDict[element].weakTo.Contains(self.tempEleType)){
		       self.eleDmgModVal = -1;
		       return true;
		     }
		     return false;
		   });
		   c.Emit(OpCodes.Brtrue,lab);
		}
		else
		  LegendAPI.Logger.LogError("Element weakness hook failed,custom elements may ignore weaknessess");
	}
	internal static void HookTakeDamage(ILContext il){
		ILCursor c = new ILCursor(il);
		if(c.TryGotoNext(x => x.MatchLdfld(typeof(AttackInfo).GetField("subElementType",(BindingFlags)(-1)))) && c.TryGotoNext(x => x.MatchSwitch(out _),x=> x.MatchBr(out _))){
		  c.Emit(OpCodes.Ldarg_0);
		  c.EmitDelegate<Action<Health>>((self) => {
		    if(eleDict.ContainsKey(self.currentAtkInfo.subElementType))
		      self.impactAudioID = eleDict[self.currentAtkInfo.subElementType].impactAudioID ?? self.impactAudioID;
		  });
		}
	}
	internal static bool HandleCustomStatus(Health caller,AttackInfo givenInfo,ElementType element,Entity atkEnt = null){
		if(givenInfo.skillLevel >= 0 && eleDict.ContainsKey(element) && eleDict[element].statusEffectType != null){
		  ElementInfo eInfo = eleDict[element];
		  var extinfo = Utility.GetExtraAttackInfo<List<float>>(givenInfo,eInfo.statusEffectChanceString);
                  if(extinfo == null || extinfo.Count <= givenInfo.skillLevel){
                    return false;
                  }
                  float chance = extinfo.ElementAt(givenInfo.skillLevel);
		  if( chance > 0f && chance * caller.statusResDict[element].CurrentValue > UnityEngine.Random.value){
		    caller.entityScript.dotManager.AddDot((DoTEffect)Activator.CreateInstance(eInfo.statusEffectType, new object[] {givenInfo.skillCategory,givenInfo.skillLevel,caller.entityScript,atkEnt}));
		    return true;
		  }
		}
		return false;
	}
    }
    public class ElementInfo {
	public string name = "";
	public Color color = Color.cyan;
	public List<ElementType> weakTo = new List<ElementType>();
	public bool isSubElement = false;
	public string impactAudioID = null;
	public Type statusEffectType = null;
	public string statusEffectChanceString = null;
	public Type elementalBurstType = null;
        public Func<Vector2,System.Collections.IEnumerator> spawnDisaster = null;
        public Sprite icon = null;
        public Sprite iconInk = null;
        /*	public Type finalBossAttackStateType = null;
	internal BossSkillState finalBossAttackState = null;
	internal BossSkillState finalBossSuperState = null;
	public Type finalBossSuperStateType = null;
*/ // :^) Secret
    }
}
