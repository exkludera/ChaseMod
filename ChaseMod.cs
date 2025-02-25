﻿using ChaseMod.Utils;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Utils;
using Microsoft.Extensions.Logging;
using ChaseMod.Commands;
using CounterStrikeSharp.API.Core.Logging;

namespace ChaseMod;

[MinimumApiVersion(215)]
public class ChaseMod : BasePlugin, IPluginConfig<ChaseModConfig>
{
    public new static ILogger Logger = CoreLogging.Factory.CreateLogger("ChaseModCS#");

    public override string ModuleName => "HnS ChaseMod";
    public override string ModuleAuthor => "svn";
    public override string ModuleVersion => "2.2.1";

    public ChaseModConfig Config { get; set; } = new();
    public void OnConfigParsed(ChaseModConfig config) { Config = config; }

    private PlayerFreezeManager? _freezeManager;
    private RoundStartFreezeTimeManager? _roundStartFreezeTimeManager;
    private NadeManager? _nadeManager;
    private KnifeCooldownManager? _knifeCooldownManager;
    private TeamSwitchManager? _teamSwitchManager;

    public override void Load(bool hotReload)
    {
        _freezeManager = new PlayerFreezeManager(this);
        _roundStartFreezeTimeManager = new RoundStartFreezeTimeManager(this, _freezeManager);
        _nadeManager = new NadeManager(this, _freezeManager, _roundStartFreezeTimeManager);
        _knifeCooldownManager = new KnifeCooldownManager(this);
        _teamSwitchManager = new TeamSwitchManager(this);

        MiscCommands.AddCommands(this);

        _nadeManager.OnLoad();
        _knifeCooldownManager.OnLoad();
        _teamSwitchManager.OnLoad();
        _roundStartFreezeTimeManager.OnLoad();

        if (Config.DisableBoostTriggers)
        {
            TriggerWorkaround.HookTriggerOutput(this);
        }

        RegisterListener<Listeners.OnTick>(OnTick);
        RegisterListener<Listeners.OnServerPrecacheResources>(OnServerPrecacheResources);
    }

    public override void Unload(bool hotReload)
    {
        _freezeManager?.OnUnload();
        _nadeManager?.OnUnload();
        _knifeCooldownManager?.OnUnload();
    }

    [GameEventHandler]
    public HookResult OnEventRoundFreezeEnd(EventRoundFreezeEnd @event, GameEventInfo info)
    {
        Server.NextFrame(TriggerWorkaround.DisableWorkaroundTriggers);
        return HookResult.Continue;
    }

    [GameEventHandler]
    public HookResult OnEventRoundPrestart(EventRoundPrestart @event, GameEventInfo info)
    {
        _teamSwitchManager?.OnRoundPrestart();

        foreach (var controller in Utilities.GetPlayers())
        {
            controller.RemoveAllItemsOnNextRoundReset = true;
        }

        return HookResult.Continue;
    }

    private void OnServerPrecacheResources(ResourceManifest manifest)
    {
        if (Config.FreezeRingParticle.Enabled)
            manifest.AddResource(Config.FreezeRingParticle.VpcfFile);
    }

    private void OnTick()
    {
        foreach (var controller in ChaseModUtils.GetAllRealPlayers())
        {
            if (controller is not { IsBot: false, LifeState: (byte)LifeState_t.LIFE_ALIVE })
            {
                continue;
            }

            var pawn = controller.PlayerPawn.Value!;

            var weapons = pawn.WeaponServices?.MyWeapons;
            if (weapons == null)
            {
                continue;
            }

            // I'm not entirely sure why it's like this, but literally every other way
            // I tried is unreliable (it lets left clicks through sometimes...)
            var tickThreshold = Server.TickCount + (64 * 60);
            var tickNextAttack = Server.TickCount + (64 * 120);

            var freezeRemaining = _freezeManager?.GetPlayerFreezeRemaining(controller) ?? 0;
            var unfreezeTick = Server.TickCount + (int)(0.5f + (freezeRemaining / Server.TickInterval));

            foreach (var weaponHandle in weapons)
            {
                if (!weaponHandle.IsValid || weaponHandle.Value == null) continue;

                var weapon = weaponHandle.Value;

                if (freezeRemaining > 0)
                {
                    if (weapon.NextPrimaryAttackTick <= unfreezeTick
                        || weapon.NextSecondaryAttackTick <= unfreezeTick)
                    {
                        weapon.DisableUntil(unfreezeTick);
                    }
                }

                if (Config.EnableKnifeDisabling && weapon.DesignerName == "weapon_knife")
                {
                    if (controller.Team == CsTeam.CounterTerrorist)
                    {
                        if (weapon.NextPrimaryAttackTick <= tickThreshold)
                        {
                            weapon.DisableUntil(tickNextAttack, null);
                        }
                    }
                    else if (controller.Team == CsTeam.Terrorist)
                    {
                        var resetKnife = weapon.NextPrimaryAttackTick <= tickThreshold
                            || weapon.NextSecondaryAttackTick <= tickThreshold;
                        if (Config.AlwaysDisableTerroristKnife && resetKnife)
                        {
                            weapon.DisableUntil(tickNextAttack);
                        }
                    }
                }
            }
        }
    }
}
