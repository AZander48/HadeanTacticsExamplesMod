using System;
using System.Collections.Generic;
using System.Reflection;
using System.IO;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using HadeanTactics;
using UnityEngine;

namespace ExamplesMod;

[BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
public class ExamplesMod : BaseUnityPlugin
{
    private TrapCardManager _trapCardManager = null!;
    private DamageSpellCardManager _damageSpellCardManager = null!;
    private EnchantCardManager _enchantCardManager = null!;
    private SummonCardManager _summonCardManager = null!;
    private ExampleUnitManager _exampleUnitManager = null!;
    private HeroUnitManager _heroUnitManager = null!;
    
    private void Awake()
    {
        // Put your initialization logic here
        Logger.LogInfo($"Plugin {PluginInfo.PLUGIN_GUID} has loaded!");

        _trapCardManager = new TrapCardManager(Logger, Config);
        _damageSpellCardManager = new DamageSpellCardManager(Logger, Config);
        _enchantCardManager = new EnchantCardManager(Logger, Config);
        _summonCardManager = new SummonCardManager(Logger, Config);
        _exampleUnitManager = new ExampleUnitManager(Logger, Config);
        _heroUnitManager = new HeroUnitManager(Logger, Config);
    }
}