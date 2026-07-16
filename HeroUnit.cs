using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using BepInEx.Logging;
using HadeanTactics;
using UnityEngine;

namespace ExamplesMod;

public class HeroUnitManager
{
    private ManualLogSource _log = null!;
    private ConfigEntry<bool> _debug = null!;
    private ConfigEntry<UnitBuildMode> _buildMode = null!;
    private ConfigEntry<string> _visualDonorId = null!;

    private UnitManager _unitManager = null!;
    private bool _heroRegistered = false;

    private const string HeroUnitId = "my_hero";
    private const string HeroSkillId = "skill_my_hero";

    private enum UnitBuildMode
    {
        /// <summary>new Unit { ... } with your stats; borrows a prefab from VisualDonorId.</summary>
        Custom,
        /// <summary>Clone an existing unit id (VisualDonorId), then rename to my_hero.</summary>
        Clone,
    }

    public HeroUnitManager(ManualLogSource log, ConfigFile config)
    {
        _log = log;
        InitConfigEntries(config);
        // Don't spawn here — only try to register data if managers already exist.
        EnsureHeroUnitRegistered();
    }

    private void InitConfigEntries(ConfigFile config)
    {
        _debug = config.Bind("Hero Unit", "Debug", false, "Enable or disable debug logging");
        _buildMode = config.Bind(
            "Hero Unit",
            "Build Mode",
            UnitBuildMode.Clone,
            "Custom = build Unit from scratch. Clone = copy Visual Donor Id then rename.");
        _visualDonorId = config.Bind(
            "Hero Unit",
            "Visual Donor Id",
            "inquisitor",
            "Unit id for model (Custom) or full clone source (Clone). Use InfoBox ID.");

        config.Bind(
            "Hero Unit",
            "Add to bench",
            false,
            new ConfigDescription(
                "Register the hero unit (if needed) and spawn it as a hero on the bench/party.",
                null,
                new ConfigurationManagerAttributes
                {
                    CustomDrawer = _ =>
                    {
                        if (GUILayout.Button("Add to bench", GUILayout.ExpandWidth(false)))
                            AddHeroUnitToBench();
                    },
                    HideDefaultButton = true,
                }));
    }

    private static EffectContainer BuildHeroSkill()
    {
        return new EffectContainer
        {
            id = HeroSkillId,
            containerType = EffectContainerType.skill,
            targetType = TargetType.RandomEmptyTile,
            effects = new List<Effect>
            {
                new Effect(EffectType.SummonUnit) { args = HeroUnitId }
            }
        };
    }

    private Unit BuildCustomUnit(EffectContainer skill, string visualDonorId)
    {
        return new Unit
        {
            id = HeroUnitId,
            appearId = visualDonorId,
            assetRef = visualDonorId,
            title = "My Hero",
            pool = UnitPool.hero,
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

    /// <summary>
    /// Clone donor into a NEW Unit via the Unit(Unit) copy ctor so we don't mutate allUnits["inquisitor"].
    /// </summary>
    private Unit? BuildClonedUnit(UnitManager unitManager, EffectContainer skill, string templateId)
    {
        Unit template = unitManager.GetUnitById(templateId);
        if (template == null)
            return null;

        Unit unit = new Unit(template);
        unit.id = HeroUnitId;
        unit.appearId = templateId;
        unit.assetRef = templateId;
        unit.title = "My Hero";
        unit.pool = UnitPool.hero;
        unit.team = TeamType.Team1;
        unit.skillId = skill.id;
        unit.skills = new List<EffectContainer> { skill };
        return unit;
    }

    /// <summary>Spawn path — AddUnitToTeam + isHero. Call from the config button, not during register.</summary>
    private void AddHeroUnitToBench()
    {
        var unitManager = GetUnitManager();
        if (unitManager == null)
        {
            _log.LogError("Unit manager not found");
            return;
        }

        _heroRegistered = false;
        EnsureHeroUnitRegistered();
        if (!_heroRegistered)
        {
            _log.LogError("Hero unit failed to register. Check Visual Donor Id / that you are in a run.");
            return;
        }

        Unit unit = unitManager.GetUnitById(HeroUnitId);
        if (unit == null)
        {
            _log.LogError($"'{HeroUnitId}' not found after registration.");
            return;
        }

        if (_debug.Value)
            _log.LogInfo($"Spawning hero ({_buildMode.Value}, donor={_visualDonorId.Value})");

        UnitBehaviour behaviour = unitManager.AddUnitToTeam(unit, wanderer: false);
        if (behaviour == null)
        {
            _log.LogError($"AddUnitToTeam failed for '{HeroUnitId}'.");
            return;
        }

        behaviour.isHero = true;

        if (_debug.Value)
            _log.LogInfo($"Spawned '{HeroUnitId}' as hero.");
    }

    private UnitManager GetUnitManager()
    {
        if (_unitManager != null) return _unitManager;
        _unitManager = UnityEngine.Object.FindObjectOfType<UnitManager>();
        return _unitManager;
    }

    /// <summary>Data-only: build Unit, register skill + allUnits. Do not CreateUnit / AddUnitToTeam here.</summary>
    private bool RegisterHeroUnit()
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

        EffectContainer skill = BuildHeroSkill();
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

        GameObject model = PoolManager.GetUnitPrefab(donorId);
        if (model != null)
            PoolManager.AddOrReplaceUnitPrefab($"{HeroUnitId}_prefab", model);

        unitManager.AddUnitToAllUnits(unit);

        if (unitManager.GetUnitById(HeroUnitId) == null)
        {
            _log.LogError($"'{HeroUnitId}' missing after AddUnitToAllUnits.");
            return false;
        }

        if (_debug.Value)
            _log.LogInfo($"Registered '{HeroUnitId}' mode={_buildMode.Value} donor={donorId}.");

        return true;
    }

    private void EnsureHeroUnitRegistered()
    {
        if (_heroRegistered) return;
        if (RegisterHeroUnit()) _heroRegistered = true;
    }
}
