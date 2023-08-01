# LegendAPI - A Modding API for Wizard of Legend

## About
LegendAPI is a framework for other mods to help integrate their content with the game in a way compatible with both other mods and with vanilla systems other than the one being directly modified.

## Manual Installation
The contents of the release should be extracted into the `plugins` folder under your BepInEx installation,\
This project requires a valid installation of both [BepInEx](https://github.com/BepInEx/BepInEx) (version 4.5.\* or later) and also of [HookGenPatcher](https://github.com/harbingerofme/Bepinex.Monomod.HookGenPatcher/)

## Building
After cloning the repo,place the necessary library files in a folder named `libs` one level above the repo,afterwards run `dotnet build`.

## Basic Usage (by Module)
The core idea behind using LegendAPI as a mod developer is that you write your content as if it was a part of the main game and then provide LegendAPI with info regarding it so that it can do the necessary modifications for the game to accept it.\
There are various modules that facilitate this.\
Unless stated otherwise all Info fields can be expected to have sane default values.

### Items
An Item is implemented by first writing a class inheriting from either any other item or the vanilla `Item` class,after doing so the following details can be provided to LegendAPI:
```csharp
ItemInfo {
 Item item; //An instance of your item
 string name; //The name of your item.
 int tier; //The 'tier' that your item is in,this determines internal values such as rarity and base price.
 global::TextManager.ItemInfo text; //The user facing text your item has,made up of it's internal-id,a display name and a description. 
 string group; //The internal-id of any group/bundle items that this item might belong to.
 Sprite icon; // The inventory & world sprite for your item.
 int priceMultiplier; //The direct price multiplier of your item,set to 0 to make it free.
 Func<bool> unlockCondition; //A function that takes nothing and returns a bool,this delegate will be called to decide whether the item is eligible for sale at the Plaza's Item Shop. It can either check another condition or return false for fully custom unlocks.
}
```
This data can then be passed to the API by calling `LegendAPI.Items.Register()`,any items that this one can combine into should be registered using `LegendAPI.Items.RegisterRecipe()`.\
This module is compatible with Replace Mode Registering,which means that providing an ItemInfo matching to an existing (vanilla or modded) item will allow you to change it's values.

### Outfits
An Outfit is made by creating a new instance of the vanilla `Outfit` class. However,to make use of LegendAPI's outfit related functionality requires that the outfit's `modList` field contains an entry of `LegendAPI.Outfits.CustomModType`,it can also include other OutfitStatMods without issue.\
Afterwards,the following info can be filled out:
```csharp
OutfitInfo {
 Outfit outfit; //The outfit instance you created,note the requirement of a CustomModType entry mentioned above.
 string name; //The display name of the outfit.
 Func<bool> unlockCondition; //A function that takes nothing and returns a bool,provided that the outfit isn't unlocked by default(a field in the Outfit class),this delegate will be called to decide whether the player has earned the outfit.
 Action<Player, bool, bool, OutfitModStat> customMod; //The Custom Modifier,this is the workhorse that weaves your custom outfit effects into being,this delegate takes,in order,the player in question,whether the effect is being turned on or off ,if it's being called due to a change in outfits or otherwise and the outfitmodstat instace being activated.
 Func<bool, OutfitModStat, string> customDesc; //The display description of the outfit's abilities,it is provided as a delegate to take advantage of live shifts in values,the input of the delegate refers to whether it's being displayed in a context where providing stat numbers is appropriate as well as the outfitmodstat instance used.
}
```
This data can then be passed to the API by calling `LegendAPI.Outfits.Register()`.\
This module is compatible with Replace Mode Registering,which means that providing and OutfitInfo matching to an existing (vanilla or modded) outfit will allow you to change it's values.

### Skills/Spells/Arcana
An arcana is implemented by writing a class inheriting from `Player.SkillState`,after doing so the following structs can be provided to LegendAPI:
```csharp
public class SkillInfo{
 public string ID; //The id of your skill,should be the same across all classes relating to the skill in question.
 public string displayName"; //The skill name shown to the user.
 public string description; //The description text of the skill.
 public string enhancedDescription = String.Empty; //The piece of text added to the description when the skill is enhanced.
 public Sprite icon; //The icon representing the skill.
 public int tier; //The 'tier' that your skill is in,this determines internal values such as rarity and base price.
 public Type stateType; //The type inheriting from Player.SkillState that you defined for this skill.
 public SkillStats skillStats; // The entry holding the stat data for this skill,values of type SkillStatsInfo can be provided for implicit conversion instead of a SkillStats instance.
 public Func<bool> unlockCondition; // A function that takes nothing and returns a bool,this delegate will be called to decide whether the skill is eligible for sale at the Plaza's Skill Shop. It can either check another condition or return false for fully custom unlocks.
 public bool hidden; //Set to true to hide the skill from Tomi.Can be used to implement skills that require special conditions to acquire.
 public int priceMultiplier; //The direct price multiplier of your skill,set to 0 to make it free.
 public Sprite bgSpriteIcon; //The square background and border of your skill in the hud. 
 public Sprite bgSpriteFull; //The rectangular card background and border of your skill in relevant UIs.
}
```
This data can then be passed to the API by calling `LegendAPI.Skills.Register()`.\
This module is compatible with Replace Mode Registering,which means that providing a SkillInfo matching to and existing (vanilla or modded) skill will allow you to change it's values.\
In order to make it easier to create skills with complex stat data,the types `SkillStatsInfo` and `SkillStatsLevel` can be used instead of SkillStats,as SkillStatsInfo has an implicit conversion to SkillStats it can be used directly to set `SkillInfo.skillStats`.
```csharp
public class SkillStatsInfo{
 public string ID; //The id of your skill,should be the same across all classes relating to the skill in question.
 public string[] targetNames; //The hitbox categories your skill is capable of targeting, 'commonly accepted behavior' would use "EnemyHurtBox" and "DestructibleHurtBox".
 public SkillStatsLevel[] levelInfos; //SkillStatsLevel instances providing stat blocks for different 'levels' of the skill,these represent different stats that your skill can access for various effects.
}
```

### Elements
This module allows you to provide the following data to slot in new element types into the games systems:
```csharp
ElementInfo {
 string name; //The Internal *and* External name of the element.
 Color color; //The color that represent the element
 List<ElementType> weakTo; //Elements that this one performs weakly against.
 bool isSubElement; //Whether this element is a subset of another,primary,element. It should be noted that the game expects all damage to be dealt in the form of a primary element.
 string impactAudioID; //The internal id for this element's sound effect.
 Type statusEffectType; //The C# Type of a status effect that can be inflicted by this element,leave empty if no such exists.
 string statusEffectChanceString; //Provided that a statusEffectType exists,this will be the key that will be added to attacks to represent it,can be left empty to use a default value.
 Type elementalBurstType; //The C# Type of this element's 'burst'.This is an explosion or blast that will be used by many effects in the game,can be left empty if such compatibility is not desired.
 Func<Vector2,System.Collections.IEnumerator> spawnDisaster; //The Delegate that gets called to represent this element in the Catastrophic Codex,it is treated as a coroutine that takes a position to spawn the effect at,can be left empty if such compatibility is not desired.
 Sprite icon; //The sprite used to represent your element.
 Sprite iconInk; //The sprite used to represent your element when not selected.
}
```
After providing this data to the API by calling `LegendAPI.Elements.Register()`,it will return an ElementType representing the new element,\
this value should be saved by the mod plugin and can later be used in anywhere a vanilla ElementType can.\
This module is NOT compatible with Replace Mode Registering,trying to register the same element twice will give you copies.

### Music
This module allows you to provide the following data to add new soundtrack replacements to the game:
```csharp
BGMInfo{
 string name; //The Internal name of the Soundtrack
 BGMTrackType fallback; //Another Soundtrack,vanilla or modded,for this replacer to fallback to when it is missing a requested song.
 Dictionary<string,AudioClip> soundtrack; //The actual audio that will be played for a given song name/id.
 string message; //The line of dialogue Melody will provide the player in regards to this soundtack.
 string messageConfirm; //The confirmation option for the above.
 string messageCancel; //The rejection option for the above.
 Sprite albumIndicatorSprite; //The replacement sprite for Melody's indicator ribbon.
 float volumeMultiplier; //A flat multiplier on the volume of all songs played from this soundtrack
}
```
After providing this data to the API by calling `LegendAPI.Music.Register()` it will return a BGMTrackType representing the new soundtrack,\
however,keeping this value around is not necessary unless one intends to manually queue up music,as all soundtracks will be automatically added to Melody's rotation ingame.\
This module is NOT compatible with Replace Mode Registering,trying to register the same soundtrack twice will give you copies.

### Utility
This is the generic module for any extra functionality that LegendAPI might make use of or expose,check here for stuff that might be useful to you!\
Current Functionality Includes(but may not be limited to):\
+ `SetExtraAttackInfo()` which will let you attach arbitrary pieces of data to attacks.
+ `GetExtraAttackInfo()` for making use of the above.
+ `SetExtraSkillStat()` for attaching arbitrary pieces of data to skill stats.These will be passed to the attacks generated by that skill for use of the above.
+ `GetExtraSkillStats()` for advanced usage of the above.

### Credit
 * Credit to TheTimesweeper and only_going_up_fr0m_here for investigating how the game handles skills and discovering the requirements to add new ones.


## Changelog

  **2.1.0**
   * Yet Another Vanilla Secret Cloak Fix.
   * You can now access the source cloak for the Vanilla Secret Cloak through `LegendAPI.Outfits.shadowSource`

  **2.0.0**
   * BREAKING CHANGE
   * Outfit custom mods and their descriptions now have access to their OutfitModStat instance.
     - This allows them to refer to the modifiers stored in the OutfitModStat to describe,display,or modify stats.
     - `OutfitModStat.statDictKey` can be set WHILE CREATING THE OUTFIT,to modify the given skill stat,including extra stats from the Skills module.
     - `OutfitModStat.targetNumStatList` and `OutfitModStat.targetBoolStatList` can be used INSIDE THE CUSTOM MOD DELEGATE to specify stats that the modifiers apply.
     - Old custom mods should still work without issue once their method signature is corrected to fit the new delegate types.
   * Misc bugfixes (including upgraded custom mods while wearing Shadow).

  **1.4.3**
   * Improved Hook Logging
   * Fixed Erroneous Re-application of logs.
   * Fixed Upgrading of custom outfit effects.

  **1.4.2**
   * Added hook logging
   * Fixed Element Status NRE on partially-specified attacks.
   * Fixed Custom Arcana Unlocks not working at all.(Whoops)
   * Non-hidden Chaos Arcana are now automatically added to the vanilla unlock mechanism.
   * Fixed hidden arcana showing up in (and being equipable from) the full spell list.

  **Manifest Update/1.4.1**
   * Updated thunderstore manifest for dependency packages.
   * Package contents are the same as 1.4.0

  **1.4.0**
   * Added Skills module.
   * Added Utility functions for extra skill data.
   * Improved behavior regarding modules going semi-active when not used.
   * Improved handling of group items.
   * Misc. Bug Fixes.

  **1.3.0**
   * Fixed Trophy Case Error when trying to display items that no longer exist.
   * Added conditional unlocks for items.
  
  **1.2.1**
   * Fixed the vanilla secret cloak not inheriting custom outfit modifications

  **1.2.0**
   * Added Custom Element Support to the spellbook
   * Added Custom Soundtrack Support 
   * Added Utility functions to attach custom data to attacks
   * Added values to modify Item Price
