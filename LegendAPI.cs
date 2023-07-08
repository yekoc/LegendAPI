using System;
using BepInEx;
using BepInEx.Logging;
using On;

namespace LegendAPI {
    [BepInPlugin("xyz.yekoc.wizardoflegend.LegendAPI", "Wizard of Legend API", "1.4.0")]
    public class LegendAPI : BaseUnityPlugin {
        internal new static ManualLogSource Logger { get; set; }
	public void Awake() {
            Logger = base.Logger;
            Items.Awake();
            Outfits.Awake();
	    Elements.Awake();
	    Utility.Hook();
            Music.Awake();
            Skills.Awake();
        }
	public void FixedUpdate(){
	}
    }
}
