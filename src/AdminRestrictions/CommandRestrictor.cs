﻿using AdminRestrictions.Configuration;
using Network;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

namespace AdminRestrictions
{
    internal class CommandRestrictor : SingletonComponent<CommandRestrictor>
    {
        const string CONFIGURATION_PATH = "HarmonyMods_Data/AdminRestrictions/Configuration.json";
        const string LOGFILE_PATH = "HarmonyMods_Data/AdminRestrictions/Logs/Log-{0}.txt";
        readonly StringBuilder _stringBuilder = new StringBuilder();

        bool _ready = false;
        FileLogger _fileLogger;
        GlobalConfig _configuration;

        internal static void Initialize() => new GameObject().AddComponent<CommandRestrictor>();
        public override void Awake()
        {
            base.Awake();
            RegisterCommands();
            var logFilePath = string.Format(LOGFILE_PATH, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
            _fileLogger = new FileLogger(logFilePath);
            LoadConfiguration();
            if (ValidateConfiguration())
            {
                if (!_configuration.enabled)
                {
                    Debug.LogWarning("[AdminRestrictions]: All functionality has been disabled in the configuration");
                    return;
                }

                OnEnabled();
            }
        }

        /// <summary>
        /// This method is called once the rust console system has already determined that it is a player initiated command and they have authlevel >= 1
        /// Return true to permit execution, false to deny execution
        /// </summary>
        internal bool CommandHasPermission(ConsoleSystem.Arg arg)
        {
            if (!_ready)
                return true;

            // If this command can be ran as a normal server user we should allow it.
            // This check is required as Oxide Mod registers all commands with
            // ServerAdmin and ServerUser as true. Regardless, if a command has
            // ServerUser set to true and the arg has a valid Connection is will
            // be allowed to run by Rusts ConsoleSystem, we can already be sure
            // that a valid connection is present as an explicit test to check
            // whether this Arg was ran by Rcon has already been performed by Rust.
            if (arg.cmd.ServerUser)
                return true;

            var allowed = IsCommandPermitted(arg);
            LogCommand(arg, allowed);
            return allowed;
        }

        bool IsCommandPermitted(ConsoleSystem.Arg arg)
        {
            if (!_ready)
                return true;

            // Is this command globally allowed?
            for (int i = 0; i < _configuration.globallyAllowedCommands.Count; i++)
            {
                if (string.Equals(_configuration.globallyAllowedCommands[i], arg.cmd.FullName))
                {
                    return true;
                }
            }

            // Is this command globally blocked?
            for (int i = 0; i < _configuration.globallyBlockedCommands.Count; i++)
            {
                if (string.Equals(_configuration.globallyBlockedCommands[i], arg.cmd.FullName))
                {
                    SendClientReply(arg.Connection, "Permission Denied");
                    return false;
                }
            }

            // Allow Facepunch administrators to execute commands
            var developers = Facepunch.Application.Manifest?.Administrators;
            if (developers != null)
            {
                var steamIdString = arg.Connection.userid.ToString();
                for (int i = 0; i < developers.Length; i++)
                {
                    if (developers[i].UserId == steamIdString)
                        return true;
                }
            }

            var steamId = arg.Connection.userid;

            // Time to iterate over all the group configs in the configuration
            for (int i = 0; i < _configuration.groupConfigs.Length; i++)
            {
                // Check if groupConfig contains the Admins userid
                var groupConfig = _configuration.groupConfigs[i];
                var groupContainsPlayer = false;
                for (int j = 0; j < groupConfig.steamIds.Count; j++)
                {
                    if (groupConfig.steamIds[j] == steamId)
                    {
                        // We found a matching steamid in the group config,
                        // time to break out of this loop and test the permitted commands
                        groupContainsPlayer = true;
                        break;
                    }
                }
                // Admins userid was not found in the groupConfig, go to next group config
                if (!groupContainsPlayer)
                    continue;

                // Now that we know the admin is in this group configuration, lets see if the allow all flag is set for the group config
                // If so, lets return true
                if (groupConfig.allowAll)
                    return true;

                // Test if the group config permits the command
                for (int j = 0; j < groupConfig.allowedCommands.Length; j++)
                {
                    if (string.Equals(groupConfig.allowedCommands[j], arg.cmd.FullName))
                    {
                        // We found a matching command in the allowed commands list
                        // time to break out of this loop and allow the commands execution
                        return true;
                    }
                }
                // If we got here, we didn't find the command in the allowedCommands list
                // Lets test the rest of the group configs incase the admin is defined in multiple group configs
            }

            // Default to block all commands
            SendClientReply(arg.Connection, "Permission Denied");
            return false;
        }

        void LogCommand(ConsoleSystem.Arg arg, bool permitted)
        {
            if (!_configuration.logToFile) return;

            _stringBuilder.Clear();
            _stringBuilder.Append("["); _stringBuilder.Append(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()); _stringBuilder.Append("] ");
            _stringBuilder.Append("("); _stringBuilder.Append(arg.Connection.userid); _stringBuilder.Append(") \"");
            _stringBuilder.Append(arg.Connection.username);
            if (permitted) _stringBuilder.Append("\" executed command \"");
            else _stringBuilder.Append("\" attempted to execute a blocked command \"");
            _stringBuilder.Append(arg.cmd.FullName);
            _stringBuilder.Append("\"");
            if (arg.HasArgs())
            {
                _stringBuilder.Append(" Args: ");
                for (int i = 0; i < arg.Args.Length; i++)
                {
                    if (i != 0) _stringBuilder.Append(", ");
                    _stringBuilder.Append("\"");
                    _stringBuilder.Append(arg.Args[i]);
                    _stringBuilder.Append("\"");
                }
            }
            _fileLogger.Log(_stringBuilder.ToString());
        }

        void OnEnabled()
        {
            _ready = true;
        }

        void OnDisabled()
        {
            _ready = false;
        }

        void RegisterCommands()
        {
            const string commandPrefix = "adminrestrictions";
            ConsoleSystem.Command reloadCfgCommand = new ConsoleSystem.Command()
            {
                Name = "reloadcfg",
                Parent = commandPrefix,
                FullName = commandPrefix + "." + "reloadcfg",
                ServerAdmin = true,
                Variable = false,
                Call = new Action<ConsoleSystem.Arg>(ReloadCfgCommand)
            };
            ConsoleSystem.Index.Server.Dict[reloadCfgCommand.FullName] = reloadCfgCommand;

            ConsoleSystem.Command addGloballyAllowedCommand = new ConsoleSystem.Command()
            {
                Name = "addgloballyallowedcommand",
                Parent = commandPrefix,
                FullName = commandPrefix + "." + "addgloballyallowedcommand",
                ServerAdmin = true,
                Variable = false,
                Call = new Action<ConsoleSystem.Arg>(AddGloballyAllowedCommand)
            };
            ConsoleSystem.Index.Server.Dict[addGloballyAllowedCommand.FullName] = addGloballyAllowedCommand;

            ConsoleSystem.Command removeGloballyAllowedCommand = new ConsoleSystem.Command()
            {
                Name = "removegloballyallowedcommand",
                Parent = commandPrefix,
                FullName = commandPrefix + "." + "removegloballyallowedcommand",
                ServerAdmin = true,
                Variable = false,
                Call = new Action<ConsoleSystem.Arg>(RemoveGloballyAllowedCommand)
            };
            ConsoleSystem.Index.Server.Dict[removeGloballyAllowedCommand.FullName] = removeGloballyAllowedCommand;

            ConsoleSystem.Command addGloballyBlockedCommand = new ConsoleSystem.Command()
            {
                Name = "addgloballyblockedcommand",
                Parent = commandPrefix,
                FullName = commandPrefix + "." + "addgloballyblockedcommand",
                ServerAdmin = true,
                Variable = false,
                Call = new Action<ConsoleSystem.Arg>(AddGloballyBlockedCommand)
            };
            ConsoleSystem.Index.Server.Dict[addGloballyBlockedCommand.FullName] = addGloballyBlockedCommand;

            ConsoleSystem.Command removeGloballyBlockedCommand = new ConsoleSystem.Command()
            {
                Name = "removegloballyblockedcommand",
                Parent = commandPrefix,
                FullName = commandPrefix + "." + "removegloballyblockedcommand",
                ServerAdmin = true,
                Variable = false,
                Call = new Action<ConsoleSystem.Arg>(RemoveGloballyBlockedCommand)
            };
            ConsoleSystem.Index.Server.Dict[removeGloballyBlockedCommand.FullName] = removeGloballyBlockedCommand;

            ConsoleSystem.Command addAdminToGroupCommand = new ConsoleSystem.Command()
            {
                Name = "addadmintogroup",
                Parent = commandPrefix,
                FullName = commandPrefix + "." + "addadmintogroup",
                ServerAdmin = true,
                Variable = false,
                Call = new Action<ConsoleSystem.Arg>(AddAdminToGroupCommand)
            };
            ConsoleSystem.Index.Server.Dict[addAdminToGroupCommand.FullName] = addAdminToGroupCommand;

            ConsoleSystem.Command removeAdminFromGroupCommand = new ConsoleSystem.Command()
            {
                Name = "removeadminfromgroup",
                Parent = commandPrefix,
                FullName = commandPrefix + "." + "removeadminfromgroup",
                ServerAdmin = true,
                Variable = false,
                Call = new Action<ConsoleSystem.Arg>(RemoveAdminFromGroupCommand)
            };
            ConsoleSystem.Index.Server.Dict[removeAdminFromGroupCommand.FullName] = removeAdminFromGroupCommand;

            ConsoleSystem.Command[] allCommands = ConsoleSystem.Index.All.Concat(new ConsoleSystem.Command[] { reloadCfgCommand, addAdminToGroupCommand, removeAdminFromGroupCommand }).ToArray();
            // Would be nice if this had a public setter, or better yet, a register command helper
            typeof(ConsoleSystem.Index)
                .GetProperty(nameof(ConsoleSystem.Index.All), System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
                .SetValue(null, allCommands);
        }

        void AddGloballyAllowedCommand(ConsoleSystem.Arg arg)
        {
            if (!arg.HasArgs(1))
            {
                arg.ReplyWith("[AdminRestrictions]: Invalid syntax: adminrestrictions.addgloballyallowedcommand <command.name> [<command.name>...]");
                return;
            }

            int processed = 0;
            for (int i = 0; i < arg.Args.Length; i++)
            {
                var command = arg.Args[i];
                if (string.IsNullOrWhiteSpace(command)) continue;
                if (_configuration.globallyAllowedCommands.Contains(command)) continue;
                _configuration.globallyAllowedCommands.Add(command);
                processed++;
            }

            if (processed > 0)
            {
                SaveConfiguration();
            }

            arg.ReplyWith("[AdminRestrictions]: Added " + processed + " command(s) to the globally allowed list");
        }

        void RemoveGloballyAllowedCommand(ConsoleSystem.Arg arg)
        {
            if (!arg.HasArgs(1))
            {
                arg.ReplyWith("[AdminRestrictions]: Invalid syntax: adminrestrictions.removegloballyallowedcommand <command.name> [<command.name>...]");
                return;
            }

            int processed = 0;
            for (int i = 0; i < arg.Args.Length; i++)
            {
                var command = arg.Args[i];
                if (string.IsNullOrWhiteSpace(command)) continue;
                if (!_configuration.globallyAllowedCommands.Remove(command)) continue;
                processed++;
            }

            if (processed > 0)
            {
                SaveConfiguration();
            }

            arg.ReplyWith("[AdminRestrictions]: Removed " + processed + " command(s) from the globally allowed list");
        }

        void AddGloballyBlockedCommand(ConsoleSystem.Arg arg)
        {
            if (!arg.HasArgs(1))
            {
                arg.ReplyWith("[AdminRestrictions]: Invalid syntax: adminrestrictions.addgloballyblockedcommand <command.name> [<command.name>...]");
                return;
            }

            int processed = 0;
            for (int i = 0; i < arg.Args.Length; i++)
            {
                var command = arg.Args[i];
                if (string.IsNullOrWhiteSpace(command)) continue;
                if (_configuration.globallyBlockedCommands.Contains(command)) continue;
                _configuration.globallyAllowedCommands.Add(command);
                processed++;
            }

            if (processed > 0)
            {
                SaveConfiguration();
            }

            arg.ReplyWith("[AdminRestrictions]: Added " + processed + " command(s) to the globally blocked list");
        }

        void RemoveGloballyBlockedCommand(ConsoleSystem.Arg arg)
        {
            if (!arg.HasArgs(1))
            {
                arg.ReplyWith("[AdminRestrictions]: Invalid syntax: adminrestrictions.removegloballyblockedcommand <command.name> [<command.name>...]");
                return;
            }

            int processed = 0;
            for (int i = 0; i < arg.Args.Length; i++)
            {
                var command = arg.Args[i];
                if (string.IsNullOrWhiteSpace(command)) continue;
                if (!_configuration.globallyBlockedCommands.Remove(command)) continue;
                processed++;
            }

            if (processed > 0)
            {
                SaveConfiguration();
            }

            arg.ReplyWith("[AdminRestrictions]: Removed " + processed + " command(s) from the globally blocked list");
        }

        void AddAdminToGroupCommand(ConsoleSystem.Arg arg)
        {
            if (!arg.HasArgs(2))
            {
                arg.ReplyWith("[AdminRestrictions]: Invalid syntax: adminrestrictions.addadmintogroup <group name> <admin steam id>");
                return;
            }

            var groupName = arg.GetString(0, null);
            if (string.IsNullOrWhiteSpace(groupName))
            {
                arg.ReplyWith("[AdminRestrictions]: Invalid syntax: adminrestrictions.addadmintogroup <group name> <admin steam id>");
                return;
            }

            var userId = arg.GetUInt64(1, 0);
            if (userId == 0)
            {
                arg.ReplyWith("[AdminRestrictions]: Invalid syntax: adminrestrictions.addadmintogroup <group name> <admin steam id>");
                return;
            }

            for (int i = 0; i < _configuration.groupConfigs.Length; i++)
            {
                var configGroup = _configuration.groupConfigs[i];
                if (!string.Equals(configGroup.name, groupName))
                    continue;

                if (configGroup.steamIds.Contains(userId))
                {
                    arg.ReplyWith($"[AdminRestrictions]: Admin \"{userId}\" is already apart of the group \"{groupName}\"");
                    return;
                }

                configGroup.steamIds.Add(userId);
                arg.ReplyWith($"[AdminRestrictions]: Added admin \"{userId}\" to group \"{groupName}\"");
                SaveConfiguration();
                return;
            }

            arg.ReplyWith($"[AdminRestrictions]: Failed to find group with name \"{groupName}\"");
        }

        void RemoveAdminFromGroupCommand(ConsoleSystem.Arg arg)
        {
            if (!arg.HasArgs(2))
            {
                arg.ReplyWith("[AdminRestrictions]: Invalid syntax: adminrestrictions.removeadminfromgroup <group name> <admin steam id>");
                return;
            }

            var groupName = arg.GetString(0, null);
            if (string.IsNullOrWhiteSpace(groupName))
            {
                arg.ReplyWith("[AdminRestrictions]: Invalid syntax: adminrestrictions.removeadminfromgroup <group name> <admin steam id>");
                return;
            }

            var userId = arg.GetUInt64(1, 0);
            if (userId == 0)
            {
                arg.ReplyWith("[AdminRestrictions]: Invalid syntax: adminrestrictions.removeadminfromgroup <group name> <admin steam id>");
                return;
            }

            for (int i = 0; i < _configuration.groupConfigs.Length; i++)
            {
                var configGroup = _configuration.groupConfigs[i];
                if (!string.Equals(configGroup.name, groupName))
                    continue;

                if (configGroup.steamIds.RemoveAll(x => x == userId) < 1)
                {
                    arg.ReplyWith($"[AdminRestrictions]: Admin \"{userId}\" is not apart of the group \"{groupName}\"");
                    return;
                }

                arg.ReplyWith($"[AdminRestrictions]: Admin \"{userId}\" remove from group \"{groupName}\"");
                SaveConfiguration();
                return;
            }

            arg.ReplyWith($"[AdminRestrictions]: Failed to find group with name \"{groupName}\"");
        }

        void ReloadCfgCommand(ConsoleSystem.Arg arg)
        {
            LoadConfiguration();
            if (!ValidateConfiguration() || _configuration.enabled == false)
            {
                OnDisabled();

                if (!_configuration.enabled)
                {
                    arg.ReplyWith("[AdminRestrictions]: All functionality has been disabled in the configuration");
                    return;
                }
            }
            else if (!_ready)
            {
                OnEnabled();
            }
            arg.ReplyWith("[AdminRestrictions]: Configuration reloaded");
        }

        static void SendClientReply(Connection cn, string strCommand)
        {
            if (!Net.sv.IsConnected())
            {
                return;
            }
            var write = Net.sv.StartWrite();
            write.PacketID(Message.Type.ConsoleMessage);
            write.String(strCommand);
            write.Send(new SendInfo(cn));
        }

        bool ValidateConfiguration()
        {
            if (_configuration == null) return false;

            bool valid = true;

            // Validate Config Here
            if (_configuration.globallyBlockedCommands == null)
            {
                Debug.LogError("[AdminRestrictions]: Invalid configuration detected: The \"Globally Blocked Commands\" array in the configuration file was null");
                valid = false;
            }

            if (_configuration.groupConfigs == null)
            {
                Debug.LogError("[AdminRestrictions]: Invalid configuration detected: The \"Groups\" array in the configuration file was null");
                valid = false;
            }
            else
            {
                for (int i = 0; i < _configuration.groupConfigs.Length; i++)
                {
                    var groupConfig = _configuration.groupConfigs[i];
                    if (groupConfig == null)
                    {
                        Debug.LogError($"[AdminRestrictions]: Invalid configuration detected: The element at index {i} within the \"Groups\" array in the configuration file was null");
                        valid = false;
                    }
                    else
                    {
                        if (string.IsNullOrWhiteSpace(groupConfig.name))
                        {
                            Debug.LogError($"[AdminRestrictions]: Invalid configuration detected: The element at index {i} within the \"Groups\" array in the configuration file contained an empty or missing value for \"Group Name\"");
                            valid = false;
                        }

                        if (groupConfig.allowedCommands == null)
                        {
                            Debug.LogError($"[AdminRestrictions]: Invalid configuration detected: The element at index {i} within the \"Groups\" array in the configuration file contained a null \"Allowed Commands\" array");
                            valid = false;
                        }
                        else if (!groupConfig.allowAll && groupConfig.allowedCommands.Length < 1)
                        {
                            Debug.LogError($"[AdminRestrictions]: Detected non-performant configuration: The element at index {i} within the \"Groups\" array in the configuration file contained a empty \"Allowed Commands\" array with the \"Allow All\" option was set to false");
                        }

                        if (groupConfig.steamIds == null)
                        {
                            Debug.LogError($"[AdminRestrictions]: Invalid configuration detected: The element at index {i} within the \"Groups\" array in the configuration file contained a null \"Admin Steam Ids\" array");
                            valid = false;
                        }
                        else if (groupConfig.steamIds.Count < 1)
                        {
                            Debug.LogError($"[AdminRestrictions]: Detected non-performant configuration: The element at index {i} within the \"Groups\" array in the configuration file contained a empty \"Allowed Commands\" array");
                        }
                    }
                }
            }

            return valid;
        }

        void LoadConfiguration()
        {
            try
            {
                var configStr = File.ReadAllText(CONFIGURATION_PATH);
                _configuration = JsonConvert.DeserializeObject<GlobalConfig>(configStr);
                if (_configuration == null) _configuration = GenerateDefaultConfiguration();
            }
            catch
            {
                Debug.LogError("[AdminRestrictions]: The configuration seems to be missing or malformed. Defaults will be loaded.");
                _configuration = GenerateDefaultConfiguration();
                // We don't want to overwrite a broken config file, let just load defaults and wait for the user to fix their config file
                if (File.Exists(CONFIGURATION_PATH)) return;
            }
            SaveConfiguration();
        }

        GlobalConfig GenerateDefaultConfiguration()
        {
            return new GlobalConfig
            {
                enabled = false,
                globallyBlockedCommands = new List<string>
                {
                    "server.stop",
                    "spawn.fill_populations",
                    "spawn.fill_groups",
                    "global.quit",
                    "global.restart",
                    "demo.record",
                    "demo.recordlist",
                    "demo.splitmegabytes",
                    "demo.splitseconds",
                    "demo.stop"
                },
                globallyAllowedCommands = new List<string>
                {
                    "global.teleport2player",
                    "global.teleport2owneditem",
                    "global.teleport2marker",
                    "global.teleportlos",
                    "global.teleportpos",
                    "global.spectate",
                    "global.teaminfo",
                    "server.snapshot",
                    "server.combatlog",
                    "server.combatlog_outgoing",
                    "global.god",
                    "global.sleep"
                },
                logToFile = false,
                groupConfigs = new GroupConfig[]
                {
                    new GroupConfig
                    {
                        name = "allow-all",
                        allowAll = true,
                        allowedCommands = new string[0],
                        steamIds = new List<ulong>
                        {
                            1234567890
                        }
                    },
                    new GroupConfig
                    {
                        name = "admins",
                        allowAll = false,
                        allowedCommands = new string[]
                        {
                            "global.teleport2me",
                            "entity.spawn",
                            "inventory.give",
                            "inventory.giveall",
                            "inventory.givearm",
                            "inventory.giveid",
                            "inventory.giveto",
                            "global.entid"
                        },
                        steamIds = new List<ulong>
                        {
                            1234567890
                        }
                    }
                }
            };
        }

        void SaveConfiguration()
        {
            try
            {
                var configFileInfo = new FileInfo(CONFIGURATION_PATH);
                if (!configFileInfo.Directory.Exists) configFileInfo.Directory.Create();
                var serializedConfiguration = JsonConvert.SerializeObject(_configuration, Formatting.Indented);
                File.WriteAllText(CONFIGURATION_PATH, serializedConfiguration);
            }
            catch (Exception ex)
            {
                Debug.LogError("[AdminRestrictions]: Failed to write configuration file");
                Debug.LogException(ex);
            }
        }
    }
}
