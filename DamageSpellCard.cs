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

public class DamageSpellCardManager
{
    private ManualLogSource _log = null!;

    private ConfigEntry<bool> _debug = null!;
    private ConfigEntry<int> _damage = null!;

    private CardManager _cardManager = null!;       

    public DamageSpellCardManager(ManualLogSource log, ConfigFile config)
    {
        //new Harmony(PluginInfo.PLUGIN_GUID).PatchAll(typeof(DamageSpellCardEffectPatch).Assembly);

        _log = log;
        InitConfigEntries(config);
        registerCard(DamageSpellCard);
    }

    private void InitConfigEntries(ConfigFile config)
    {
        _debug = config.Bind("DamageSpellCard", "Debug", false, "Enable or disable debug logging");
        _damage = config.Bind("DamageSpellCard", "Damage", 10, "The amount of damage to deal");

        config.Bind(
            "DamageSpellCard",
            "Add to hand",
            false,
            new ConfigDescription(
                "Add the damage spell card to the hand.",
                null,
                new ConfigurationManagerAttributes
                {
                    CustomDrawer = entry =>
                    {
                        // button
                        if (GUILayout.Button("Add to hand", GUILayout.ExpandWidth(false)))
                            AddDamageSpellToHand(DamageSpellCard);
                    },
                    HideDefaultButton = true,
                })
            );
    }

    // if you want to create a card dynamically, you can use this method
    private Card CreateDamageSpellCard()
    {
        return new Card
        {
            title = "Damage Spell",
            id = "card_damageSpell",
            cardType = CardType.spell,
            cardTargetType = TargetType.EnemyOnly,
            baseCost = 0,
            deplete = 1,
            repeat = 1,
            IsMod = false,
            modId = "ExamplesMod",
            effects = new List<Effect>
            {
                new Effect(EffectType.DealDamage)
                {
                    value = _damage.Value,
                    dMod = new DamageMod(),
                }
            }
        };
    }
    
    // if you want to use a static card, you can use this method
    private static readonly Card DamageSpellCard = new Card
    {
        title = "Damage Burn Spell",
        id = "card_damageBurnSpell",
        cardType = CardType.spell,
        cardTargetType = TargetType.EnemyOnly,
        baseCost = 0,
        deplete = 1,
        repeat = 1,
        IsMod = false,
        modId = "ExamplesMod",
        effects = new List<Effect>
        {
            new Effect(EffectType.DealDamage)
            {
                value = 10,
                dMod = new DamageMod(),
            },
            new Effect(EffectType.Burn)
            {
                value = 1,
                dMod = new DamageMod(),
            }
        }
    };

    private void AddDamageSpellToHand(Card card)
    {
        var cardManager = GetCardManager();

        if (_debug.Value)
        {
            _log.LogInfo($"Adding damage spell card to hand");
        }

        if (cardManager == null) 
        {
            _log.LogError($"Card manager not found");
            return;
        }

        if (_debug.Value)
        {
            _log.LogInfo($"Card manager: {cardManager}");
        }

        cardManager.DrawCardSimple(card);
    }

    private CardManager GetCardManager()
    {
        if (_cardManager != null) return _cardManager;
        _cardManager = UnityEngine.Object.FindObjectOfType<CardManager>();
        return _cardManager;
    }

    private void registerCard(Card card)
    {
        var cardManager = GetCardManager();
        cardManager.AddCardToAllCards(card);
    }
}