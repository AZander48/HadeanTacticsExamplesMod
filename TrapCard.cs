using System;
using System.Collections.Generic;
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
    private ConfigEntry<TrapBuildMode> _buildMode = null!;
    private ConfigEntry<string> _visualDonorId = null!;
    private ConfigEntry<int> _burnValue = null!;

    private CardManager _cardManager = null!;
    private bool _burnRegistered = false;

    private const string BurnTrapUnitId = "trap_burn";
    private const string BurnTrapSkillId = "skill_trapBurn";
    private const string BurnTrapCardId = "card_burnTrap";

    private enum TrapBuildMode
    {
        /// <summary>new Unit { ... } with trap fields; borrows prefab/VFX from Visual Donor Id.</summary>
        Custom,
        /// <summary>Clone an existing trap unit id (Visual Donor Id), then rename to trap_burn.</summary>
        Clone,
    }

    public TrapCardManager(ManualLogSource log, ConfigFile config)
    {
        new Harmony(PluginInfo.PLUGIN_GUID).PatchAll(typeof(TrapEffectBurnPatch).Assembly);

        _log = log;
        InitConfigEntries(config);
        EnsureBurnTrapRegistered();
    }

    private void InitConfigEntries(ConfigFile config)
    {
        _debug = config.Bind("TrapCard", "Debug", false, "Enable or disable debug logging");
        _buildMode = config.Bind(
            "TrapCard",
            "Build Mode",
            TrapBuildMode.Clone,
            "Custom = build trap Unit from scratch. Clone = copy Visual Donor Id then rename.");
        _visualDonorId = config.Bind(
            "TrapCard",
            "Visual Donor Id",
            "trap_poison",
            "Trap/unit id for model + VFX (Custom) or full clone source (Clone). Use InfoBox ID.");
        _burnValue = config.Bind(
            "TrapCard",
            "Burn Value",
            25,
            "TrapEffect value passed into the Harmony burn branch.");

        config.Bind(
            "TrapCard",
            "Add to hand",
            false,
            new ConfigDescription(
                "Register the burn trap (if needed) and add the place card to hand.",
                null,
                new ConfigurationManagerAttributes
                {
                    CustomDrawer = _ =>
                    {
                        if (GUILayout.Button("Add to hand", GUILayout.ExpandWidth(false)))
                            AddBurnTrapToHand();
                    },
                    HideDefaultButton = true,
                }));
    }

    private EffectContainer BuildBurnSkill()
    {
        return new EffectContainer
        {
            id = BurnTrapSkillId,
            containerType = EffectContainerType.skillExtra,
            targetType = TargetType.Source,
            effects = new List<Effect>
            {
                new Effect(EffectType.TrapEffect)
                {
                    value = _burnValue.Value,
                    args = "burn",
                    dMod = new DamageMod(),
                }
            }
        };
    }

    /// <summary>Fully custom trap unit — no GetUnitById clone. Still needs a real prefab via donor.</summary>
    private Unit BuildCustomTrap(EffectContainer skill, string visualDonorId)
    {
        return new Unit
        {
            id = BurnTrapUnitId,
            appearId = BurnTrapUnitId, // resolved via AddOrReplaceUnitPrefab("trap_burn_prefab", donorModel)
            assetRef = BurnTrapUnitId,
            title = "Burn Trap",
            pool = UnitPool.trap,
            team = TeamType.Team1,
            skillId = skill.id,
            skills = new List<EffectContainer> { skill },
        };
    }

    /// <summary>Clone a real trap/unit, then retarget id + burn skill.</summary>
    private Unit? BuildClonedTrap(UnitManager unitManager, EffectContainer skill, string templateId)
    {
        Unit unit = unitManager.GetUnitById(templateId);
        if (unit == null)
            return null;

        unit.id = BurnTrapUnitId;
        unit.appearId = BurnTrapUnitId;
        unit.assetRef = BurnTrapUnitId;
        unit.title = "Burn Trap";
        unit.pool = UnitPool.trap;
        unit.team = TeamType.Team1;
        unit.skillId = skill.id;
        unit.skills = new List<EffectContainer> { skill };
        return unit;
    }

    private static readonly Card BurnTrapCard = new Card
    {
        title = "Burn Trap",
        id = BurnTrapCardId,
        heroId = "any",
        cardType = CardType.artifice,
        cardTargetType = TargetType.EmptyTile,
        baseCost = 0,
        deplete = 1,
        repeat = 1,
        rarityIndex = 1,
        description = "Place a burn trap. Trigger to burn enemies in range.",
        IsMod = false,
        modId = "ExamplesMod",
        effects = new List<Effect>
        {
            new Effect(EffectType.Trap) { args = BurnTrapUnitId }
        }
    };

    private bool RegisterBurnTrap()
    {
        var unitManager = UnityEngine.Object.FindObjectOfType<UnitManager>();
        var relicManager = UnityEngine.Object.FindObjectOfType<RelicManager>();
        if (unitManager == null || relicManager == null)
            return false;

        string donorId = _visualDonorId.Value?.Trim() ?? "";
        if (string.IsNullOrEmpty(donorId))
        {
            _log.LogError("Visual Donor Id is empty.");
            return false;
        }

        EffectContainer skill = BuildBurnSkill();
        relicManager.AddOrReplaceEffectContainer(skill);

        Unit? unit = _buildMode.Value switch
        {
            TrapBuildMode.Clone => BuildClonedTrap(unitManager, skill, donorId),
            _ => BuildCustomTrap(skill, donorId),
        };

        if (unit == null)
        {
            _log.LogError($"Clone failed: GetUnitById('{donorId}') was null.");
            return false;
        }

        RegisterTrapVisuals(donorId);
        unitManager.AddUnitToAllUnits(unit);

        if (unitManager.GetUnitById(BurnTrapUnitId) == null)
        {
            _log.LogError($"'{BurnTrapUnitId}' missing after AddUnitToAllUnits.");
            return false;
        }

        if (_debug.Value)
            _log.LogInfo($"Registered '{BurnTrapUnitId}' mode={_buildMode.Value} donor={donorId} skill={skill.id}.");

        return true;
    }

    /// <summary>Borrow donor model / skill VFX / card art under trap_burn keys.</summary>
    private void RegisterTrapVisuals(string donorId)
    {
        GameObject model = PoolManager.GetUnitPrefab(donorId);
        if (model != null)
            PoolManager.AddOrReplaceUnitPrefab($"{BurnTrapUnitId}_prefab", model);

        // Prefer donor-named skill VFX; fall back to poison trap keys used by vanilla decay trap.
        GameObject skillVfx =
            PoolManager.GetUnitSkillEffectPrefab($"{donorId}_skill_prefab")
            ?? PoolManager.GetUnitSkillEffectPrefab("trap_poison_skill_prefab");
        if (skillVfx != null)
            PoolManager.AddOrReplaceVisualEffect($"{BurnTrapUnitId}_skill_prefab", skillVfx);

        // Card play VFX / art: try donor card keys, then poison trap card.
        string donorCardId = donorId.StartsWith("trap_", StringComparison.Ordinal)
            ? $"card_{donorId.Substring("trap_".Length)}Trap"
            : $"card_{donorId}";

        GameObject cardVfx =
            PoolManager.GetMiscPrefab($"{donorCardId}_prefab")
            ?? PoolManager.GetMiscPrefab("card_poisonTrap_prefab");
        if (cardVfx != null)
            PoolManager.AddOrReplaceVisualEffect($"{BurnTrapCardId}_prefab", cardVfx);

        Sprite cardArt =
            PoolManager.GetCardSprite(donorCardId)
            ?? PoolManager.GetCardSprite("card_poisonTrap");
        if (cardArt != null)
            PoolManager.AddOrReplaceSprite(BurnTrapCardId, cardArt);
    }

    private void EnsureBurnTrapRegistered()
    {
        if (_burnRegistered) return;
        if (RegisterBurnTrap()) _burnRegistered = true;
    }

    private void AddBurnTrapToHand()
    {
        // Allow re-register when switching Build Mode / donor between clicks.
        _burnRegistered = false;
        EnsureBurnTrapRegistered();
        if (!_burnRegistered)
        {
            _log.LogError("Burn trap failed to register. Check Visual Donor Id / that managers exist (in a run).");
            return;
        }

        var cardManager = GetCardManager();
        if (cardManager == null)
        {
            _log.LogError("Card manager not found");
            return;
        }

        if (_debug.Value)
            _log.LogInfo($"Adding burn trap ({_buildMode.Value}, donor={_visualDonorId.Value})");

        RegisterCardInCompendium(BurnTrapCard);
        cardManager.DrawCardSimple(BurnTrapCard);
    }

    private CardManager GetCardManager()
    {
        if (_cardManager != null) return _cardManager;
        _cardManager = UnityEngine.Object.FindObjectOfType<CardManager>();
        return _cardManager;
    }

    private void RegisterCardInCompendium(Card card)
    {
        var cardManager = GetCardManager();
        if (cardManager == null) return;
        if (string.IsNullOrEmpty(card.heroId))
            card.heroId = "any";
        cardManager.AddCardToAllCards(card);
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
