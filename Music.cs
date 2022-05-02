using System;
using System.Collections.Generic;
using UnityEngine;

namespace LegendAPI {
    public static class Music {
	internal static Dictionary<BGMTrackType,BGMInfo> BGMCatalog = new Dictionary<BGMTrackType,BGMInfo>(); 
	public static void Awake(){

	}
	public static BGMTrackType Register(BGMInfo info){	
		BGMTrackType id = (BGMTrackType)info.name.GetHashCode();
		int failsafe = 1;
		while((id >= BGMTrackType.None && id <= BGMTrackType.Piano) || BGMCatalog.ContainsKey(id)){
		  id = (BGMTrackType)((int)id + (failsafe % 2) *info.message.GetHashCode() + (failsafe % 2 +1) * info.name.GetHashCode());
		  if(failsafe++ > 10){
		    LegendAPI.Logger.LogError("BGM Registration failed due to hash collision,this really shouldn't be happening unless you are adding the same thing repeatedly.");
		    return (BGMTrackType)(-1);
		  }
		}
		BGMCatalog.Add(id,info);
		return id;
	}

    }

    public class BGMInfo{
	public string name;
	public BGMTrackType fallback;
	public Dictionary<string,AudioClip> soundtrack;
	public string message;
        public Func<bool> unlockCondition = () => { return true; };
    }
}
