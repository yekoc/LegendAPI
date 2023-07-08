using System;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;

namespace LegendAPI {
    public static class Utility {
	internal static Dictionary<WeakRefHandle<AttackInfo>,Dictionary<string,object>> extraInfoATK = new Dictionary<WeakRefHandle<AttackInfo>, Dictionary<string,object>>();
	internal static Dictionary<WeakRefHandle<SkillStats>,Dictionary<string,List<object>>> extraInfoSKL = new Dictionary<WeakRefHandle<SkillStats>, Dictionary<string,List<object>>>();
	internal static List<string> defaultStatFields = typeof(StatData).GetFields((BindingFlags)(-1)).Where((field) => field.FieldType == typeof(string) && field.IsStatic).Select((field) => (string)field.GetValue(null)).ToList(); 
	public static void Hook(){ 
	   On.AttackInfo.ctor_AttackInfo += (orig,self,info) =>{
		orig(self,info);
		if(extraInfoATK.TryGetValue(new WeakRefHandle<AttackInfo>(info),out var other))
		  extraInfoATK[new WeakRefHandle<AttackInfo>(self)] = new Dictionary<string,object>(other);
	   };
	   On.AttackInfo.GetInfoFromData += (orig,ent,stat,lvl,ult) =>{
		AttackInfo result = orig(ent,stat,lvl,ult);
                var miT = typeof(Utility).GetMethod("SetExtraAttackInfo");
                var miS = typeof(StatData).GetMethod("GetValue");
		foreach(var item in stat.statDict.Keys.Where((key) => !defaultStatFields.Contains(key))){
                  Type type = stat.statDict[item].GetType();
		  miT.MakeGenericMethod(new Type[] {type}).Invoke(null,new object[] {result,item,miS.MakeGenericMethod(new Type[] {type}).Invoke(stat,new object[] {item,lvl})}); 
		}
		return result;
	   };
           On.StatData.LoadStatDict += (orig,self,ss) =>{
               orig(self,ss);
               Dictionary<string,List<object>> info;
               if(extraInfoSKL.TryGetValue(new WeakRefHandle<SkillStats>(ss),out info)){
                foreach(var extrainfo in info){
                  self.statDict.Add(extrainfo.Key,extrainfo.Value);
                }
               }
           };
           GameController.levelLoadEventHandlers += (a,b) =>{
              CleanExtraAttackInfo();
              CleanExtraSkillStats();
           };
	   
	}
	public static T GetExtraAttackInfo<T>(AttackInfo info,string staticID){
		FieldInfo fiel;
		if(extraInfoATK.TryGetValue(new WeakRefHandle<AttackInfo>(info),out var result) && result.ContainsKey(staticID)){
		  return (T)result[staticID];
		}
		else if((fiel = typeof(AttackInfo).GetField(staticID)) != null)
		  return (T)fiel.GetValue(info);
		else
		 return default(T);
	}
	public static void SetExtraAttackInfo<T>(AttackInfo info,string staticID,T value) where T : class{
		FieldInfo fiel;
		if((fiel = typeof(AttackInfo).GetField(staticID)) != null)
		  fiel.SetValue(info,value);
		else if(value != default(T)){
                 var key = new WeakRefHandle<AttackInfo>(info);
		 if(extraInfoATK.TryGetValue(key,out var result)){
                     result[staticID] = value;
                 }
                 else{
                     extraInfoATK.Add(key,new Dictionary<string, object>());
                     extraInfoATK[key][staticID] = value;
                 }
                }
	}
	public static void CleanExtraAttackInfo(){
                extraInfoATK.Clear();
	}

	public static T GetExtraSkillStat<T>(SkillStats info,string staticID,int level){
		FieldInfo fiel;
		if(extraInfoSKL.TryGetValue(new WeakRefHandle<SkillStats>(info),out var result) && result.ContainsKey(staticID)){
		  return (T)result[staticID][level];
		}
		else if((fiel = typeof(SkillStats).GetField(staticID)) != null)
		  return (T)(fiel.GetValue(info) as T[])[level];
		else
		 return default(T);
	}
	public static void SetExtraSkillStat<T>(SkillStats info,string staticID,T value,int level) where T : class{
		FieldInfo fiel;
		if((fiel = typeof(SkillStats).GetField(staticID)) != null){
		  T[] val = (T[])fiel.GetValue(info);
                  if(level >= val.Length){
                   var tmpval = val.ToList();
                   tmpval.AddRange(new T[level +1 - val.Length]);
                   val = tmpval.ToArray();
                  }
                   val[level] = value;
                   fiel.SetValue(info,val);
                }
		else if(value != default(T)){
		 var key = new WeakRefHandle<SkillStats>(info);
                 if(extraInfoSKL.TryGetValue(key,out var result)){
                     if(result[staticID].Count <= level){
                        result[staticID].AddRange(new T[level +1 -result[staticID].Count]);
                     }
                     result[staticID][level] = value;
                 }
                 else{
                     extraInfoSKL.Add(key,new Dictionary<string, List<object>>());
                     extraInfoSKL[key][staticID] = new List<object>(new T[Math.Max(level + 1 ,SkillStats.maxSkillLevel)]);
                     extraInfoSKL[key][staticID][level] = value;
                 }
                }
	}
	public static void CleanExtraSkillStats(){
		foreach(var item in extraInfoSKL.ToList()){
		  if(!item.Key.IsAlive)
		    extraInfoSKL.Remove(item.Key);
		}
	}

        internal class WeakRefHandle<T>{
            //We mourn the non-existance of ConditionalWeakTable in net 3.5
            internal WeakReference info;
            internal bool IsAlive => info.IsAlive;

            internal WeakRefHandle(T attack){
                info = new WeakReference(attack);
            }
            public override bool Equals(object other){
                if(other.GetType() == typeof(WeakRefHandle<T>)){
                    return (IsAlive && ((WeakRefHandle<T>)other).IsAlive)?Equals(other as WeakRefHandle<T>):false;
                }
                return false;
            }
            public bool Equals(WeakRefHandle<T> other){
                return info.Target.Equals(other.info.Target);
            }

            public override int GetHashCode(){
                return IsAlive?info.Target.GetHashCode():(-1);
            }

        }
    }
}
