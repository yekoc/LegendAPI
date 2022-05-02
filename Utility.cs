using System;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;

namespace LegendAPI {
    public static class Utility {
	internal static Dictionary<WeakReference,Dictionary<string,object>> extraInfo = new Dictionary<WeakReference, Dictionary<string,object>>();
	internal static List<string> defaultStatFields = typeof(StatData).GetFields((BindingFlags)(-1)).Where((field) => field.FieldType == typeof(string) && field.IsStatic).Select((field) => (string)field.GetValue(null)).ToList(); 
	public static void Hook(){ 
	   On.AttackInfo.ctor_AttackInfo += (orig,self,info) =>{
		orig(self,info);
		if(extraInfo.ContainsKey(new WeakReference(info)))
		  extraInfo[new WeakReference(self)] = new Dictionary<string,object>(extraInfo[new WeakReference(info)]);
	   };
	   On.AttackInfo.GetInfoFromData += (orig,ent,stat,lvl,ult) =>{
		AttackInfo result = orig(ent,stat,lvl,ult);
		foreach(var item in stat.statDict.Keys.Where((key) => !defaultStatFields.Contains(key))){
		  LegendAPI.Logger.LogError(item);  
		  SetExtraAttackInfo<object>(result,item,stat.GetValue<object>(item,lvl)); 
		}
		return result;
	   };
	   
	}
	public static T GetExtraAttackInfo<T>(AttackInfo info,string staticID){
		FieldInfo fiel;
		if(extraInfo.ContainsKey(new WeakReference(info)) && extraInfo[new WeakReference(info)].ContainsKey(staticID)){
		  return (T)extraInfo[new WeakReference(info)][staticID];
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
		  extraInfo[new WeakReference(info)][staticID] = value;
	}
	public static void CleanExtraAttackInfo(){
		foreach(var item in extraInfo.ToList()){
		  if(!item.Key.IsAlive)
		    extraInfo.Remove(item.Key);
		}
	}
    }
}
