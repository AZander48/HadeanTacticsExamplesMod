using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using BepInEx.Logging;
using HadeanTactics;
using UnityEngine;

namespace ExamplesMod;

public class ExampleUnitManager
{
    private ManualLogSource _log = null!;
    private ConfigEntry<bool> _debug = null!;
    private ConfigEntry<UnitBuildMode> _buildMode = null!;
    private ConfigEntry<string> _visualDonorId = null!;

    private UnitManager _unitManager = null!;
    private bool _exampleRegistered = false;

    private const string ExampleUnitId = "example";
    private const string ExampleSkillId = "skill_example";
    private const string ExampleCardId = "card_exampleSummonSpell";

    private enum UnitBuildMode
    {
        /// <summary>new Unit { ... } with your stats; borrows a prefab from VisualDonorId.</summary>
        Custom,
        /// <summary>Clone an existing unit id (VisualDonorId), then rename to "example".</summary>
        Clone,
    }

    public ExampleUnitManager(ManualLogSource log, ConfigFile config)
    {
        _log = log;
        InitConfigEntries(config);
        EnsureExampleUnitRegistered();
    }

    private void InitConfigEntries(ConfigFile config)
    {
        _debug = config.Bind("ExampleUnit", "Debug", false, "Enable or disable debug logging");
        _buildMode = config.Bind(
            "ExampleUnit",
            "Build Mode",
            UnitBuildMode.Custom,
            "Custom = build Unit from scratch. Clone = copy VisualDonorId then rename.");
        _visualDonorId = config.Bind(
            "ExampleUnit",
            "Visual Donor Id",
            "lunarWolf",
            "Unit id for model (Custom) or full clone source (Clone). Use InfoBox ID.");

        config.Bind(
            "ExampleUnit",
            "Add to bench",
            false,
            new ConfigDescription(
                "Register the example unit (if needed) and add the example unit to bench.",
                null,
                new ConfigurationManagerAttributes
                {
                    CustomDrawer = _ =>
                    {
                        if (GUILayout.Button("Add to bench", GUILayout.ExpandWidth(false)))
                            AddExampleUnitToBench();
                    },
                    HideDefaultButton = true,
                }));
    }

    /// <summary>Combat skill: when the unit casts, summon another copy of itself.</summary>
    private static EffectContainer BuildSelfSummonSkill()
    {
        // Tile targets (RandomEmptyTile) only execute effects when containerType is skill.
        // skillExtra works for unit targets (Source) but silently skips tile-target effects.
        return new EffectContainer
        {
            id = ExampleSkillId,
            containerType = EffectContainerType.skill,
            targetType = TargetType.RandomEmptyTile,
            effects = new List<Effect>
            {
                new Effect(EffectType.SummonUnit)
                {
                    args = ExampleUnitId,
                }
            }
        };
    }

    /// <summary>Fully custom unit — no GetUnitById clone. Must still point appearId at a real prefab.</summary>
    private Unit BuildCustomUnit(EffectContainer skill, string visualDonorId)
    {
        return new Unit
        {
            id = ExampleUnitId,
            appearId = visualDonorId,   // GetUnitPrefab(appearId) — donor's model
            assetRef = visualDonorId,
            title = "Example Unit",
            pool = UnitPool.summon,
            team = TeamType.Team1,
            MaxHP = 150,
            currentHp = 150,
            BaseDamage = 25,
            BaseAttackRange = 1,
            baseAttackSpeed = 1f,
            movementSpeed = 3f,
            MaxMana = 100f,
            ManaRegen = 40f,
            currentMana = 0f,
            skillId = skill.id,
            skillLevel = 1,
            skills = new List<EffectContainer> { skill },
        };
    }

    /// <summary>Clone a real unit, then retarget id + skill to self-summon.</summary>
    private Unit? BuildClonedUnit(UnitManager unitManager, EffectContainer skill, string templateId)
    {
        Unit unit = unitManager.GetUnitById(templateId);
        if (unit == null)
            return null;

        unit.id = ExampleUnitId;
        unit.appearId = templateId;
        unit.assetRef = templateId;
        unit.title = "Example Unit";
        unit.pool = UnitPool.summon;
        unit.team = TeamType.Team1;
        unit.MaxMana = 100f;
        unit.ManaRegen = 40f;
        unit.currentMana = 0f;
        unit.skillId = skill.id;
        unit.skills = new List<EffectContainer> { skill };
        return unit;
    }

    private void AddExampleUnitToBench()
    {
        var unitManager = GetUnitManager();
        if (unitManager == null)
        {
            _log.LogError("Unit manager not found");
            return;
        }

        // Allow re-register when switching Build Mode / donor between clicks.
        _exampleRegistered = false;
        EnsureExampleUnitRegistered();
        if (!_exampleRegistered)
        {
            _log.LogError("Example unit failed to register. Check Visual Donor Id / that you are in a run.");
            return;
        }

        Unit unit = unitManager.GetUnitById(ExampleUnitId);
        if (unit == null)
        {
            _log.LogError($"'{ExampleUnitId}' not found after registration.");
            return;
        }

        if (_debug.Value)
            _log.LogInfo($"Adding example unit ({_buildMode.Value}, donor={_visualDonorId.Value})");

        RegisterUnitInCompendium(unit);
        UnitBehaviour behaviour = unitManager.AddUnitToTeam(unit, wanderer: false);
        if (behaviour == null)
        {
            _log.LogError($"AddUnitToTeam failed for '{ExampleUnitId}'.");
            return;
        }

        if (_debug.Value)
            _log.LogInfo($"Added '{ExampleUnitId}' to bench.");
    }

    /// <summary>
    /// Ensures the unit is in AllUnits. Compendium unit portraits also need the unit in a unitPool
    /// (see RegisterExampleUnit); reopen the compendium after registering.
    /// </summary>
    private void RegisterUnitInCompendium(Unit unit)
    {
        var unitManager = GetUnitManager();
        if (unitManager == null) return;

        try
        {
            unitManager.AddUnitToAllUnits(unit);
            if (_debug.Value)
                _log.LogInfo($"Registered '{unit.id}' in AllUnits for compendium.");
        }
        catch (Exception e)
        {
            _log.LogError($"AddUnitToAllUnits failed: {e.Message}");
        }
    }

    private UnitManager GetUnitManager()
    {
        if (_unitManager != null) return _unitManager;
        _unitManager = UnityEngine.Object.FindObjectOfType<UnitManager>();
        return _unitManager;
    }

    private bool RegisterExampleUnit()
    {
        var unitManager = UnityEngine.Object.FindObjectOfType<UnitManager>();
        var relicManager = UnityEngine.Object.FindObjectOfType<RelicManager>();
        if (unitManager == null) return false;

        string donorId = _visualDonorId.Value?.Trim() ?? "";
        if (string.IsNullOrEmpty(donorId))
        {
            _log.LogError("Visual Donor Id is empty.");
            return false;
        }

        EffectContainer skill = BuildSelfSummonSkill();
        if (relicManager != null)
            relicManager.AddOrReplaceEffectContainer(skill);

        Unit? unit = _buildMode.Value switch
        {
            UnitBuildMode.Clone => BuildClonedUnit(unitManager, skill, donorId),
            _ => BuildCustomUnit(skill, donorId),
        };

        if (unit == null)
        {
            _log.LogError($"Clone failed: GetUnitById('{donorId}') was null.");
            return false;
        }

        // Ensure the model key exists (donor appearId). Optional alias under example_prefab.
        GameObject model = PoolManager.GetUnitPrefab(donorId);
        if (model != null)
            PoolManager.AddOrReplaceUnitPrefab($"{ExampleUnitId}_prefab", model);

        unitManager.AddUnitToAllUnits(unit);

        if (unitManager.GetUnitById(ExampleUnitId) == null)
        {
            _log.LogError($"'{ExampleUnitId}' missing after AddUnitToAllUnits.");
            return false;
        }

        if (_debug.Value)
            _log.LogInfo($"Registered '{ExampleUnitId}' mode={_buildMode.Value} donor={donorId} MaxHP={unit.MaxHP} skill={skill.id} (self-summon).");

        return true;
    }

    private void EnsureExampleUnitRegistered()
    {
        if (_exampleRegistered) return;
        if (RegisterExampleUnit()) _exampleRegistered = true;
    }
}
