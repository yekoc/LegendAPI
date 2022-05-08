using System;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;

namespace LegendAPI {
    public static class Utility {
	internal static Dictionary<AttackInfoHandle,Dictionary<string,object>> extraInfo = new Dictionary<AttackInfoHandle, Dictionary<string,object>>();
	internal static List<string> defaultStatFields = typeof(StatData).GetFields((BindingFlags)(-1)).Where((field) => field.FieldType == typeof(string) && field.IsStatic).Select((field) => (string)field.GetValue(null)).ToList(); 
	public static void Hook(){ 
	   On.AttackInfo.ctor_AttackInfo += (orig,self,info) =>{
		orig(self,info);
		if(extraInfo.TryGetValue(new AttackInfoHandle(info),out var other))
		  extraInfo[new AttackInfoHandle(self)] = new Dictionary<string,object>(other);
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
	   
	}
	public static T GetExtraAttackInfo<T>(AttackInfo info,string staticID){
		FieldInfo fiel;
		if(extraInfo.TryGetValue(new AttackInfoHandle(info),out var result) && result.ContainsKey(staticID)){
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
		else if(value != default(T))
		 if(extraInfo.TryGetValue(new AttackInfoHandle(info),out var result)){
                     result[staticID] = value;
                 }
                 else{
                     extraInfo.Add(new AttackInfoHandle(info),new Dictionary<string, object>());
                     extraInfo[new AttackInfoHandle(info)][staticID] = value;
                 }
	}
	public static void CleanExtraAttackInfo(){
		foreach(var item in extraInfo.ToList()){
		  if(!item.Key.IsAlive)
		    extraInfo.Remove(item.Key);
		}
	}

        internal class AttackInfoHandle{
            //We mourn the non-existance of ConditionalWeakTable in net 3.5
            internal WeakReference info;
            internal bool IsAlive => info.IsAlive;

            internal AttackInfoHandle(AttackInfo attack){
                info = new WeakReference(attack);
            }
            public override bool Equals(object other){
                if(other.GetType() == typeof(AttackInfoHandle)){
                    return (IsAlive && ((AttackInfoHandle)other).IsAlive)?Equals(other as AttackInfoHandle):false;
                }
                return false;
            }
            public bool Equals(AttackInfoHandle other){
                return info.Target.Equals(other.info.Target);
            }

            public override int GetHashCode(){
                return IsAlive?info.Target.GetHashCode():(-1);
            }

        }
    }
}
