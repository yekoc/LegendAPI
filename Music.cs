using System;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using UnityEngine;
using MonoMod.Cil;
using Mono.Cecil.Cil;

namespace LegendAPI {
    public static class Music {
	internal static Dictionary<BGMTrackType,BGMInfo> BGMCatalog = new Dictionary<BGMTrackType,BGMInfo>();
        internal static BGMTrackType lastTrack = BGMTrackType.None;
	public static void Awake(){

           On.SoundManager.PlayBGM += (orig,track) =>{
               if(BGMCatalog.ContainsKey(SoundManager.bgmTrack)){
                SoundManager.instance.StartCoroutine(SoundManager.BGMTransition(track,BGMCatalog[SoundManager.bgmTrack].volumeMultiplier));
               }
               else{
                   orig(track);
               }
           };
           On.DialogManager.InitDialogDictionary += (orig,self,str) =>{
               orig(self,str);
               var jukeMessages = DialogManager.dialogDict["JukeboxNpc-PlayerRoom"].messages.ToList();
               var hereWeGO = jukeMessages.Last();
               jukeMessages.Remove(hereWeGO);
               foreach(var ost in BGMCatalog.Values){
                DialogMessage message = new DialogMessage(jukeMessages[1]);
                message.message = ost.message;
                message.confirmText = ost.messageConfirm;
                message.cancelText = ost.messageCancel;
                jukeMessages.Add(message);
               }
               jukeMessages.Add(hereWeGO);
               DialogManager.dialogDict["JukeboxNpc-PlayerRoom"].messages = jukeMessages.ToArray();
           };
           IL.JukeboxNpc.HandleConditionalInteraction += (il) => {
               ILCursor c = new ILCursor(il);
               if(c.TryGotoNext(x => x.MatchLdcI4(0),x => x.MatchRet())){
                  c.MoveAfterLabels();
                  c.Emit(OpCodes.Ldloc_0);
                  c.Emit(OpCodes.Ldarg_0);
                  c.EmitDelegate<Action<int,JukeboxNpc>>((index,self) => {
                    if(index > 4 && index <= (BGMCatalog.Count + 4)){
                      self.SelectAlbum(BGMCatalog.Keys.ElementAt(index - 5));
                    }
                  });
               }
               else{
                LegendAPI.Logger.LogError("JukeboxNpc.HandleConditionalInteraction Hook Failed,switching to custom soundtracks might not work");
               }
           };
           IL.JukeboxNpc.SelectAlbum += (il) => {
               ILCursor c = new ILCursor(il);
               if(c.TryGotoNext(x => x.MatchLdcI4(out _),x => x.MatchBle(out _))){
                  c.Index++;
                  c.EmitDelegate<Func<int,int>>((orig) => orig + BGMCatalog.Count);
                  c.GotoNext(x => x.MatchCallOrCallvirt(typeof(Npc).GetProperty(nameof(Npc.CurrentDialogIndex),(BindingFlags)(-1)).GetSetMethod(true)));
                  c.EmitDelegate<Func<int,int>>((orig) => BGMCatalog.Count + 4);
               }
               else{
                LegendAPI.Logger.LogError("JukeboxNpc.SelectAlbum Hook Failed,switching to custom soundtracks might not work");
               }
           };
           On.JukeboxNpc.SetAlbumIndicator += (orig,self,ost) => {
               orig(self,ost);
               if(BGMCatalog.ContainsKey(ost)){
                   self.albumIndicatorSprite.sprite = BGMCatalog[ost].albumIndicatorSprite;
                   self.albumIndicatorSprite.enabled = self.albumIndicatorSprite.sprite;
               }
           };
           new MonoMod.RuntimeDetour.ILHook(typeof(SoundManager).GetNestedType("<BGMTransition>c__Iterator1",(BindingFlags)(-1)).GetMethod("MoveNext"),new ILContext.Manipulator((il) => {
               ILCursor c = new ILCursor(il);
               if(c.TryGotoNext(x => x.MatchBrfalse(out _),x => x.MatchLdcI4(0),x => x.MatchStsfld(typeof(SoundManager).GetField("bgmTransInProgress",(BindingFlags)(-1))))){
                   c.EmitDelegate<Func<bool,bool>>((orig) => orig && SoundManager.bgmTrack == lastTrack);
                   if(c.TryGotoNext(MoveType.Before,x => x.MatchLdsfld(typeof(SoundManager).GetField("bgmSet",(BindingFlags)(-1))))){
                       c.MoveAfterLabels();
                       c.Emit(OpCodes.Ldarg_0);
                       c.Emit(OpCodes.Ldarg_0);
                       c.Emit(OpCodes.Ldfld,typeof(SoundManager).GetNestedType("<BGMTransition>c__Iterator1",(BindingFlags)(-1)).GetField("givenTrackName",(BindingFlags)(-1)));
                       c.EmitDelegate<Func<string,string>> ((track) => {
                           lastTrack = SoundManager.bgmTrack;
                           if(BGMCatalog.ContainsKey(SoundManager.bgmTrack) && !BGMCatalog[SoundManager.bgmTrack].soundtrack.ContainsKey(track)){
                              BGMTrackType fallback = BGMCatalog[SoundManager.bgmTrack].fallback;
                              if(BGMCatalog.ContainsKey(fallback)){
                                BGMCatalog[SoundManager.bgmTrack].soundtrack.Add(track,BGMCatalog[fallback].soundtrack[track]);
                              }
                              return track + (fallback == BGMTrackType.Piano? SoundManager.pianoPostStr : ((fallback == BGMTrackType.Jazz) ? SoundManager.jazzPostStr : SoundManager.oriPostStr));
                           }
                           return track;
                       });
                       c.Emit(OpCodes.Stfld,typeof(SoundManager).GetNestedType("<BGMTransition>c__Iterator1",(BindingFlags)(-1)).GetField("givenTrackName",(BindingFlags)(-1)));
                       if(c.TryGotoNext(MoveType.After, x => x.MatchCallOrCallvirt(typeof(ChaosBundle).GetMethod(nameof(ChaosBundle.GetBGM))))){
                           c.Emit(OpCodes.Ldarg_0);
                           c.Emit(OpCodes.Ldfld,typeof(SoundManager).GetNestedType("<BGMTransition>c__Iterator1",(BindingFlags)(-1)).GetField("givenTrackName",(BindingFlags)(-1)));
                           c.EmitDelegate<Func<AudioClip,string,AudioClip>>((orig,track) => (BGMCatalog.ContainsKey(SoundManager.bgmTrack) && BGMCatalog[SoundManager.bgmTrack].soundtrack.ContainsKey(track)) ? BGMCatalog[SoundManager.bgmTrack].soundtrack[track] : orig);
                       }
                       else{
                           LegendAPI.Logger.LogError("BGMTransition Hook (3) Failed,custom songs might not play");
                       }
                    }
                   else{
                    LegendAPI.Logger.LogError("BGMTransition Hook (2) Failed,custom songs might not play");
                   }
               }
               else{
                LegendAPI.Logger.LogError("BGMTransition Hook (1) Failed,custom songs might not play");
               }
           }));
           new MonoMod.RuntimeDetour.Hook(typeof(Enum).GetMethod("ToString",System.Type.EmptyTypes),typeof(Music).GetMethod("ToStringRedir",(BindingFlags)(-1)));
        }


        public static string ToStringRedir(Func<Enum,string> orig,Enum self){
            return ((self.GetType() == typeof(BGMTrackType)) && BGMCatalog.ContainsKey((BGMTrackType)self))? BGMCatalog[(BGMTrackType)self].name : orig(self);
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
	public string name = string.Empty;
	public BGMTrackType fallback = BGMTrackType.None;
	public Dictionary<string,AudioClip> soundtrack = new Dictionary<string, AudioClip>();
	public string message = string.Empty;
        public string messageConfirm = string.Empty;
        public string messageCancel = string.Empty;
        public Sprite albumIndicatorSprite = null;
        public float volumeMultiplier = 1f;
    }
}
