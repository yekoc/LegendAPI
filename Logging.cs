using System;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using System.IO;
using MonoMod.RuntimeDetour;
using MonoMod.RuntimeDetour.HookGen;

namespace LegendAPI {
    public static class Logging {
        internal static DetourModManager manager;
	public static void Awake(){ 
           manager = new DetourModManager();
           manager.OnDetour += (owner,orig,a) => HookLog(orig,Path.GetFileName(owner.Location),true,a.Name);
           manager.OnHook += (owner,orig,a,b) =>HookLog(orig,Path.GetFileName(owner.Location),true,a.Name);
           manager.OnILHook += (owner,orig,a) =>HookLog(orig,Path.GetFileName(owner.Location),true,a.Method.Name);
           manager.OnNativeDetour += (owner,orig,a,b) => HookLog(orig,Path.GetFileName(owner.Location),true);
           HookEndpointManager.OnAdd += (orig,hook) => HookLog(orig,Path.GetFileName(hook.Method.Module.Assembly.Location),true,hook.Method.Name);
           HookEndpointManager.OnRemove += (orig,hook) =>HookLog(orig,Path.GetFileName(hook.Method.Module.Assembly.Location),false,hook.Method.Name);
	}

        public static bool HookLog(MemberInfo orig,string ownerName,bool addremove,string hookName = null){
            LegendAPI.Logger.LogDebug((addremove? "Added" : "Removed") + $" hook {(hookName != null ? hookName : String.Empty)} by {ownerName} for {orig.DeclaringType.Name + "." + orig.Name}");
            return true;
        }
    }
}
