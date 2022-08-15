using System;
using BepInEx;
using BepInEx.Logging;
using On;

namespace LegendAPI {
    [BepInPlugin("xyz.yekoc.wizardoflegend.LegendAPI", "Wizard of Legend API", "1.2.1")]
    public class LegendAPI : BaseUnityPlugin {
        internal new static ManualLogSource Logger { get; set; }
	public void Awake() {
            Logger = base.Logger;
            Items.Awake();
            Outfits.Awake();
	    Elements.Awake();
	    Utility.Hook();
            Music.Awake();
        }
	public void Start(){
	  if(Outfits.OutfitCatalog.Count == 0){
		Outfits.enabled = false;	
	  }
	  if(Items.ItemCatalog.Count == 0 && Items.RecipeCatalog.Count == 0 && Items.GroupCatalog.Count == 0){
		Items.enabled = false;
	  }
	}
	public void FixedUpdate(){
	  Utility.CleanExtraAttackInfo();
	}
    }
}
