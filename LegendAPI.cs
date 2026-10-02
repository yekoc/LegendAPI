using System;
using BepInEx;
using BepInEx.Logging;

namespace LegendAPI {
    [BepInPlugin("xyz.yekoc.wizardoflegend.LegendAPI", "Wizard of Legend API", "2.2.0")]
    public class LegendAPI : BaseUnityPlugin {
        internal new static ManualLogSource Logger { get; set; }
	public void Awake() {
            Logger = base.Logger;
            Logging.Awake();
            Items.Awake();
            Outfits.Awake();
            Elements.Awake();
            Utility.Hook();
            Music.Awake();
            Skills.Awake();
        }
    }
}
