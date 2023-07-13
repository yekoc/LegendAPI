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
           manager.OnDetour += (owner,orig,a) => HookLog(orig,Path.GetFileName(owner.Location),true);
           manager.OnHook += (owner,orig,a,b) =>HookLog(orig,Path.GetFileName(owner.Location),true);
           manager.OnILHook += (owner,orig,a) =>HookLog(orig,Path.GetFileName(owner.Location),true);
           manager.OnNativeDetour += (owner,orig,a,b) => HookLog(orig,Path.GetFileName(owner.Location),true);
           HookEndpointManager.OnAdd += (orig,hook) => HookLog(orig,Path.GetFileName(hook.Method.Module.Assembly.Location),true);
           HookEndpointManager.OnRemove += (orig,hook) =>HookLog(orig,Path.GetFileName(hook.Method.Module.Assembly.Location),false);
	}

        public static bool HookLog(MemberInfo orig,string ownerName,bool addremove){
            LegendAPI.Logger.LogDebug((addremove? "Added" : "Removed") + $" hook by {ownerName} for {orig.DeclaringType.Name}");
            return true;
        }
    }
}
