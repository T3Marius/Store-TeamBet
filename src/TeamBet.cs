﻿using static TeamBet.Config_Config;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Commands;
using StoreApi;
using WASDSharedAPI;
using CounterStrikeSharp.API.Modules.Utils;
using CounterStrikeSharp.API.Core.Capabilities;

namespace TeamBet;

public class TeamBet : BasePlugin
{
    public override string ModuleName => "Store Module [TeamBet]";
    public override string ModuleVersion => "1.0.0";
    public override string ModuleAuthor => "T3Marius";
    private IStoreApi? StoreApi { get; set; }

    private bool hasProcessedRoundEnd = false;
    public Dictionary<string, Dictionary<CCSPlayerController, int>> GlobalBet { get; set; } = new Dictionary<string, Dictionary<CCSPlayerController, int>>();

    public List<string> Options = new List<string> { "Terrorist", "CounterTerrorist" };

    public static IWasdMenuManager? MenuManager;
    public IWasdMenuManager? GetMenuManager()
    {
        if (MenuManager == null)
            MenuManager = new PluginCapability<IWasdMenuManager>("wasdmenu:manager").Get();

        return MenuManager;
    }

    public override void OnAllPluginsLoaded(bool hotReload)
    {
        Config_Config.Load();
        RegisterCommands();
        StoreApi = IStoreApi.Capability.Get() ?? throw new Exception("StoreApi could not be located.");
        RegisterEventHandler<EventRoundEnd>(OnRoundEnd);
        RegisterEventHandler<EventRoundStart>(OnRoundStart);
        AddCommandListener("jointeam", OnCommand_jointeam, HookMode.Pre);

        foreach (string option in Options)
        {
            GlobalBet[option] = new Dictionary<CCSPlayerController, int>();
        }
    }

    public void BetTeam(CCSPlayerController player, CommandInfo info, int credits, string option)
    {
        if (StoreApi == null)
        {
            throw new Exception("StoreApi could not be located.");
        }

        if (StoreApi.GetPlayerCredits(player) < credits)
        {
            info.ReplyToCommand(Config.Tag + Localizer["No Credits"]);
            return;
        }

        if (!GlobalBet.ContainsKey(option))
        {
            GlobalBet[option] = new Dictionary<CCSPlayerController, int>();
        }

        // Add or update the bet
        if (GlobalBet[option].ContainsKey(player))
        {
            GlobalBet[option][player] += credits;
        }
        else
        {
            GlobalBet[option].Add(player, credits);
        }

        StoreApi.GivePlayerCredits(player, -credits);

        Server.PrintToChatAll(Config.Tag + Localizer["Join bet", player.PlayerName, credits, Localizer[option]]);
    }

    public void RegisterCommands()
    {
        foreach (string cmd in Config.Commands.Bet)
        {
            AddCommand($"css_{cmd}", "Bet Command", Command_Bet);
        }
    }

    [CommandHelper(minArgs: 1, "<amount>")]
    public void Command_Bet(CCSPlayerController? player, CommandInfo info)
    {
        if (player == null)
            return;

        if (StoreApi == null)
        {
            throw new Exception("StoreApi could not be located.");
        }

        if (Utilities.FindAllEntitiesByDesignerName<CCSGameRulesProxy>("cs_gamerules").First().GameRules!.WarmupPeriod)
        {
            info.ReplyToCommand(Config.Tag + Localizer["Cannot bet during warmup"]);
            return;
        }

        if (!int.TryParse(info.GetArg(1), out int credits))
        {
            info.ReplyToCommand(Config.Tag + Localizer["Must Be Integer"]);
            return;
        }

        if (credits > Config.Settings.MaxBet)
        {
            info.ReplyToCommand(Config.Tag + Localizer["Max Bet", credits]);
            return;
        }

        if (credits < Config.Settings.MinBet)
        {
            info.ReplyToCommand(Config.Tag + Localizer["Min Bet", credits]);
            return;
        }

        int playerCount = Utilities.GetPlayers().Count(p => p.IsValid && !p.IsBot && p.TeamNum > 1);

        if (playerCount < Config.Settings.MinPlayers)
        {
            player.PrintToChat(string.Format(Config.Tag + Localizer["MinPlayers"], Config.Settings.MinPlayers));
            return;
        }


        var manager = GetMenuManager();
        if (manager == null)
        {
            info.ReplyToCommand(Config.Tag + "Menu manager is not available.");
            return;
        }

        IWasdMenu menu = manager.CreateMenu(Localizer["menu_title", credits]);

        menu.Add("Terrorist", (p, option) =>
        {
            ProcessBet(p, info, credits, "Terrorist");
            manager.CloseMenu(p);
        });

        menu.Add("CounterTerrorist", (p, option) =>
        {
            ProcessBet(p, info, credits, "CounterTerrorist");
            manager.CloseMenu(p);
        });

        menu.Add(Localizer["Cancel Bet Menu"], (p, option) =>
        {
            manager.CloseMenu(p);
        });

        manager.OpenMainMenu(player, menu);
    }

    private void ProcessBet(CCSPlayerController player, CommandInfo info, int credits, string team)
    {
        if (StoreApi == null)
        {
            throw new Exception("StoreApi could not be located.");
        }

        if (!GlobalBet.ContainsKey(team))
        {
            GlobalBet[team] = new Dictionary<CCSPlayerController, int>();
        }

        int playerCredits = StoreApi.GetPlayerCredits(player);

        if (playerCredits < credits)
        {
            info.ReplyToCommand(Config.Tag + Localizer["No Credits"]);
            return;
        }

        StoreApi.GivePlayerCredits(player, -credits);

        if (GlobalBet[team].ContainsKey(player))
        {
            GlobalBet[team][player] += credits;
        }
        else
        {
            GlobalBet[team].Add(player, credits);
        }

        Server.PrintToChatAll(Config.Tag + Localizer["Join bet", player.PlayerName, credits, Localizer[team]]);
    }

    [GameEventHandler(HookMode.Pre)]
    public HookResult OnRoundEnd(EventRoundEnd @event, GameEventInfo info)
    {
        if (hasProcessedRoundEnd)
        {
            return HookResult.Continue;
        }
        if (StoreApi == null)
            return HookResult.Continue;

        hasProcessedRoundEnd = true;

        CsTeam winnerTeam = (CsTeam)@event.Winner;
        string winnerOption = winnerTeam == CsTeam.Terrorist ? "Terrorist" : "CounterTerrorist";
        string loserOption = winnerTeam == CsTeam.Terrorist ? "CounterTerrorist" : "Terrorist";

        if (GlobalBet.TryGetValue(winnerOption, out var betEntries))
        {
            int multiplier = winnerTeam == CsTeam.Terrorist ? Config.Settings.TMultiplier : Config.Settings.CTMultiplier;

            foreach (var entry in betEntries)
            {
                var betPlayer = entry.Key;
                var betAmount = entry.Value;

                int reward = betAmount * multiplier;

                StoreApi.GivePlayerCredits(betPlayer, reward);
                betPlayer.PrintToChat(Config.Tag + Localizer["YouWonMessage", reward]);
                GlobalBet.Clear();
                
            }
        }

        if (GlobalBet.TryGetValue(loserOption, out var losingBetEntries))
        {
            foreach (var entry in losingBetEntries)
            {
                var betPlayer = entry.Key;
                var betAmount = entry.Value;

                betPlayer.PrintToChat(Config.Tag + Localizer["YouLostMessage", betAmount]);
            }
        }

        return HookResult.Continue;
    }

    [GameEventHandler(HookMode.Pre)]
    public HookResult OnRoundStart(EventRoundStart @event, GameEventInfo info)
    {
        GlobalBet.Clear();
        hasProcessedRoundEnd = false;
        return HookResult.Continue;
    }
    public HookResult OnCommand_jointeam(CCSPlayerController? player, CommandInfo info)
    {
        if (player == null || !player.IsValid || player.IsBot)
            return HookResult.Continue;

        if (StoreApi == null)
        {
            return HookResult.Continue;
        }

        foreach (var option in GlobalBet.Keys)
        {
            if (Config.Settings.RemoveBetIfPlayerChangedTeam)
            {
                if (GlobalBet[option].ContainsKey(player))
                {
                    int betAmount = GlobalBet[option][player];

                    StoreApi.GivePlayerCredits(player, betAmount);

                    GlobalBet[option].Remove(player);

                    player.PrintToChat(Config.Tag + Localizer["Bet Removed", betAmount]);
                }
            }
        }
        return HookResult.Continue;
    }
}