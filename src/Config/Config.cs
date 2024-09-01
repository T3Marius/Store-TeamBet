﻿using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Translations;
using System.Reflection;
using Tomlyn;
using Tomlyn.Model;

namespace TeamBet;
public static class Config_Config
{
    public static Cfg Config { get; set; } = new Cfg();

    public static void Load()
    {
        string assemblyName = Assembly.GetExecutingAssembly().GetName().Name ?? "";
        string cfgPath = $"{Server.GameDirectory}/csgo/addons/counterstrikesharp/configs/plugins/{assemblyName}";

        LoadConfig($"{cfgPath}/config.toml");
    }

    private static void LoadConfig(string configPath)
    {
        if (!File.Exists(configPath))
        {
            throw new FileNotFoundException($"Configuration file not found: {configPath}");
        }

        string configText = File.ReadAllText(configPath);
        TomlTable model = Toml.ToModel(configText);

        TomlTable tagTable = (TomlTable)model["Tag"];
        string config_tag = StringExtensions.ReplaceColorTags(tagTable["Tag"].ToString()!);

        TomlTable commandsTable = (TomlTable)model["Commands"];
        Config_Commands config_commands = new()
        {
            Bet = GetTomlArray(commandsTable, "Bet"),
        };
        TomlTable settingsTable = (TomlTable)model["Settings"];
        Config_Settings config_settings = new()
        {
            MaxBet = int.Parse(settingsTable["MaxBet"].ToString()!),
            MinBet = int.Parse(settingsTable["MinBet"].ToString()!),
            MinPlayers = int.Parse(settingsTable["MinPlayers"].ToString()!),
            RemoveBetIfPlayerChangedTeam = bool.Parse(settingsTable["RemovePlayerBetIfChangedTeam"].ToString()!),
            TMultiplier = int.Parse(settingsTable["TMultiplier"].ToString()!),
            CTMultiplier = int.Parse(settingsTable["CTMultiplier"].ToString()!),
        };

        Config = new Cfg
        {
            Tag = config_tag,
            Commands = config_commands,
            Settings = config_settings,         
        };
    }

    private static string[] GetTomlArray(TomlTable table, string key)
    {
        if (table.TryGetValue(key, out var value) && value is TomlArray array)
        {
            return array.OfType<string>().ToArray();
        }
        return Array.Empty<string>();
    }



    public class Cfg
    {
        public string Tag { get; set; } = string.Empty;
        public List<string> Permissions { get; set; } = new();
        public Config_Commands Commands { get; set; } = new();
        public Config_Settings Settings { get; set; } = new();
    }
    public class Config_Settings
    {
        public int MinBet { get; set; } = 1;
        public int MaxBet { get; set; } = 500;
        public int MinPlayers { get; set; } = 4;
        public bool RemoveBetIfPlayerChangedTeam { get; set; } = true;
        public int TMultiplier { get; set; } = 2;
        public int CTMultiplier { get; set; } = 2;
    }

    public class Config_Commands
    {
        public string[] Bet { get; set; } = Array.Empty<string>();

    }

}