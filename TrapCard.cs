using System;
using System.Configuration;
using System.Collections.Generic;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using HadeanTactics;
using UnityEngine;

namespace ExamplesMod;

public class TrapCardManager
{
    private ManualLogSource _log = null!;

    private ConfigEntry<bool> _debug = null!;
    private ConfigEntry<TrapCardId> _currentTrapCard = null!;
    private bool _burnRegistered = false;
    
    private CardManager _cardManager = null!;

    public TrapCardManager(ManualLogSource log, ConfigFile config)
    {
        new Harmony(PluginInfo.PLUGIN_GUID).PatchAll(typeof(TrapEffectBurnPatch).Assembly);

        _log = log;
        InitConfigEntries(config);
        RegisterBurnTrap();
    }
    
    private enum TrapCardId
    {
        BurnTrap,
    }

    private void InitConfigEntries(ConfigFile config)
    {
        _debug = config.Bind("TrapCard", "Debug", false, "Enable or disable debug logging");
        

        _currentTrapCard = config.Bind(
        "TrapCard",
        "Trap",
        TrapCardId.BurnTrap,
        new ConfigDescription(
            "Select a trap, then add it to hand.",
            null,
            new ConfigurationManagerAttributes
            {
                CustomDrawer = entry =>
                {
                    var e = (ConfigEntry<TrapCardId>)entry;
                    // dropdown
                    if (GUILayout.Button(e.Value.ToString(), GUILayout.ExpandWidth(true)))
                    {
                        // simple cycle; or use a real popup if you want
                        var values = (TrapCardId[])Enum.GetValues(typeof(TrapCardId));
                        int i = Array.IndexOf(values, e.Value);
                        e.Value = values[(i + 1) % values.Length];
                    }
                    // button
                    if (GUILayout.Button("Add to hand", GUILayout.ExpandWidth(false)))
                        AddSelectedTrapToHand(e.Value);
                },
                HideDefaultButton = true,
            })
        );
    }

    private static readonly Effect burnEffect = new Effect(EffectType.TrapEffect) 
    { 
        value = 25, 
        args = "burn",
        dMod = new DamageMod(),

    };

    private static readonly EffectContainer burnSkill = new EffectContainer 
    {
        id = "skill_trapBurn",
        containerType = EffectContainerType.skillExtra,
        targetType = TargetType.Source,
        effects = new List<Effect> { burnEffect }
    };

    private static readonly Unit burnTrapUnit = new Unit
    {
        id = "trap_burn",
        skillId = "skill_trapBurn",
        pool = UnitPool.trap,
        skills = new List<EffectContainer> { burnSkill },
    };

    private static readonly Card burnTrapCard = new Card
    {
        title = "Burn Trap",
        id = "card_burnTrap",
        cardType = CardType.artifice,
        cardTargetType = TargetType.EmptyTile,
        baseCost = 0,
        deplete = 1,
        repeat = 1,
        IsMod = false,
        modId = "ExamplesMod",
        effects = new List<Effect>
        {
            new Effect(EffectType.Trap) { args = "trap_burn" }
        }
    };

    private bool RegisterBurnTrap()
    {
        var unitManager = UnityEngine.Object.FindObjectOfType<UnitManager>();
        var relicManager = UnityEngine.Object.FindObjectOfType<RelicManager>();
        if (relicManager == null || unitManager == null) return false;
        
        Unit burnUnit = unitManager.GetUnitById("trap_poison");

        if (burnUnit == null) return false;

        relicManager.AddOrReplaceEffectContainer(burnSkill);


        /*  This is the code for replacing the trap_poison with custom assets
        * place assets in the plugin directory as an asset bundle
        string pluginDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!;
        string path = Path.Combine(pluginDir, "burnTrap"); // still wrong if burnTrap is a folder of raw assets
        var bundle = AssetBundle.LoadFromFile(path);       // needs a bundle FILE

        GameObject customModel = bundle.LoadAsset<GameObject>("asset_name_model");       // Unity asset name
        GameObject customSkillVfx = bundle.LoadAsset<GameObject>("asset_name_skill");
        GameObject cardVfx = bundle.LoadAsset<GameObject>("asset_name_card");
        Sprite cardArt = bundle.LoadAsset<Sprite>("asset_name_card_art");

        */
    
        // this uses poisen assets
        GameObject customModel = PoolManager.GetUnitPrefab("trap_poison");
        GameObject customSkillVfx = PoolManager.GetUnitSkillEffectPrefab("trap_poison_skill_prefab");
        GameObject cardVfx = PoolManager.GetMiscPrefab("card_poisonTrap_prefab"); // if that key exists
        Sprite cardArt = PoolManager.GetCardSprite("card_poisonTrap");

        // adds the custom assets to the pool
        PoolManager.AddOrReplaceUnitPrefab("trap_burn_prefab", customModel);
        PoolManager.AddOrReplaceVisualEffect("trap_burn_skill_prefab", customSkillVfx);
        PoolManager.AddOrReplaceVisualEffect("card_burnTrap_prefab", cardVfx);
        PoolManager.AddOrReplaceSprite("card_burnTrap", cardArt);

        burnUnit.id = "trap_burn";
        burnUnit.appearId = "trap_burn";
        burnUnit.assetRef = "trap_burn";
        burnUnit.title = "Burn Trap";
        burnUnit.skillId = "skill_trapBurn";
        burnUnit.skills = new List<EffectContainer> { burnSkill };
        unitManager.AddUnitToAllUnits(burnUnit);
        return true;
    }

    private void AddSelectedTrapToHand()
    {
        AddSelectedTrapToHand(_currentTrapCard.Value);
    }

    private void EnsureBurnTrapRegistered()
    {
        if (_burnRegistered) return;
        if (RegisterBurnTrap()) _burnRegistered = true;
    }

    private void AddSelectedTrapToHand(TrapCardId trapCardId)
    {
        if (trapCardId == TrapCardId.BurnTrap)
        {
            EnsureBurnTrapRegistered();
        }

        Card trapCard = trapCardId switch
        {
            TrapCardId.BurnTrap => burnTrapCard,
            _ => null!,
        };

        if (_debug.Value)
        {
            _log.LogInfo($"Adding trap card {trapCardId} to hand");
        }

        if (trapCard == null) 
        {
            _log.LogError($"Trap card {trapCardId} not found");
            return;
        }

        var cardManager = GetCardManager();

        if (_debug.Value)
        {
            _log.LogInfo($"Card manager: {cardManager}");
        }

        if (cardManager == null)
        {
            _log.LogError("Card manager not found");
            return;
        }

        cardManager.DrawCardSimple(trapCard);
    }

    private CardManager GetCardManager()
    {
        _cardManager = GameObject.FindObjectOfType<CardManager>();

        if (_cardManager == null) throw new Exception("CardManager not found");

        return _cardManager;
    }
}

[HarmonyPatch(typeof(Effect), nameof(Effect.Execute), new[]
{
    typeof(GameManager),
    typeof(TileBehaviour),
    typeof(UnitBehaviour),
    typeof(UnitBehaviour),
    typeof(EffectAttr),
})]
static class TrapEffectBurnPatch
{
    static bool Prefix(
        Effect __instance,
        GameManager manager,
        UnitBehaviour source)
    {
        if (__instance.effectType != EffectType.TrapEffect)
            return true;
        if (__instance.args != "burn")
            return true;
        if (source == null || manager?._tileManager == null)
            return false;
        int range = source.unit.persistentStats.trapRange + 1;
        manager._cardManager?.OnTrapTriggered();
        var tiles = manager._tileManager.GetTilesInRange(source.currentTile, range, true);
        foreach (var tile in tiles)
        {
            UnitBehaviour unit = tile.CurrentUnit;
            if (unit == null || unit.Team == source.Team)
                continue;
            if (__instance.dMod != null)
            {
                __instance.dMod.sourceType = SourceType.Card;
                __instance.dMod.sourceId = "card_trigger";
                __instance.dMod.sourceName = LocalizationManager.GetLocaPure("card_trigger");
            }
            var burn = new Effect(EffectType.Burn, __instance.value)
            {
                dMod = __instance.dMod,
            };
            unit.AddStatus(burn);
        }
        return false; // skip vanilla TrapEffect (no "burn" branch)
    }
}
