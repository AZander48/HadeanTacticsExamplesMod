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

public class EnchantCardManager
{
    private ManualLogSource _log = null!;

    private ConfigEntry<bool> _debug = null!;
    private ConfigEntry<int> _shield = null!;

    private CardManager _cardManager = null!;       

    public EnchantCardManager(ManualLogSource log, ConfigFile config)
    {
        //new Harmony(PluginInfo.PLUGIN_GUID).PatchAll(typeof(DamageSpellCardEffectPatch).Assembly);

        _log = log;
        InitConfigEntries(config);
    }

    private void InitConfigEntries(ConfigFile config)
    {
        _debug = config.Bind("EnchantCard", "Debug", false, "Enable or disable debug logging");
        _shield = config.Bind("EnchantCard", "Enchant", 1, "The amount of enchant to add");

        config.Bind(
            "EnchantCard",
            "Add to hand",
            false,
            new ConfigDescription(
                "Add the enchant card to the hand.",
                null,
                new ConfigurationManagerAttributes
                {
                    CustomDrawer = entry =>
                    {
                        // button
                        if (GUILayout.Button("Add to hand", GUILayout.ExpandWidth(false)))
                            AddEnchantCardToHand(EnchantCard);
                    },
                    HideDefaultButton = true,
                })
            );
    }

    // if you want to create a card dynamically, you can use this method
    private Card CreateEnchantCard()
    {
        return new Card
        {
            title = "Enchant Spell",
            id = "card_enchantSpell",
            cardType = CardType.spell,
            cardTargetType = TargetType.AllyOnly,
            baseCost = 0,
            deplete = 1,
            repeat = 1,
            IsMod = false,
            modId = "ExamplesMod",
            effects = new List<Effect>
            {
                new Effect(EffectType.GainShield)
                {
                    value = _shield.Value,
                }
            }
        };
    }
    
    // if you want to use a static card, you can use this method
    private static readonly Card EnchantCard = new Card
    {
        title = "Enchant Spell",
        id = "card_enchantSpell",
        heroId = "any",
        cardType = CardType.spell,
        cardTargetType = TargetType.AllyOnly,
        baseCost = 0,
        deplete = 1,
        repeat = 1,
        IsMod = false,
        modId = "ExamplesMod",
        effects = new List<Effect>
        {
            new Effect(EffectType.GainShield)
            {
                value = 1,
            },
            new Effect(EffectType.Heal)
            {
                value = 1,
            }
        }
    };

    private void AddEnchantCardToHand(Card card)
    {
        var cardManager = GetCardManager();

        if (_debug.Value)
        {
            _log.LogInfo($"Adding enchant card to hand");
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
        registerCard(card);
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
        if (cardManager == null) return;
        if (string.IsNullOrEmpty(card.heroId))
            card.heroId = "any";
        cardManager.AddCardToAllCards(card);
    }
}