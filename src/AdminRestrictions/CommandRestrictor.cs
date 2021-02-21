using AdminRestrictions.Configuration;
using Newtonsoft.Json;
using System;
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
        protected override void Awake()
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

            // Is this command globally blocked?
            for (int i = 0; i < _configuration.globallyBlockedCommands.Length; i++)
            {
                if (string.Equals(_configuration.globallyBlockedCommands[i], arg.cmd.FullName))
                {
                    LogBlockedCommand(arg);
                    return false;
                }
            }

            var steamId = arg.Connection.userid;

            // Time to iterate over all the group configs in the configuration
            for (int i = 0; i < _configuration.groupConfigs.Length; i++)
            {
                // Check if groupConfig contains the Admins userid
                var groupConfig = _configuration.groupConfigs[i];
                var groupContainsPlayer = false;
                for (int j = 0; j < groupConfig.steamIds.Length; j++)
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
                    if (string.Equals(groupConfig.allowedCommands[i], arg.cmd.FullName))
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
            LogBlockedCommand(arg);
            return false;
        }

        void LogBlockedCommand(ConsoleSystem.Arg arg)
        {
            _stringBuilder.Clear();
            _stringBuilder.Append("["); _stringBuilder.Append(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()); _stringBuilder.Append("] ");
            _stringBuilder.Append("("); _stringBuilder.Append(arg.Connection.userid); _stringBuilder.Append(") \"");
            _stringBuilder.Append(arg.Connection.username);
            _stringBuilder.Append("\" attempted to execute blocked command \"");
            _stringBuilder.Append(arg.cmd.FullName);
            _stringBuilder.Append("\"");
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
            ConsoleSystem.Index.Server.Dict[commandPrefix + "." + "reloadcfg"] = reloadCfgCommand;

            ConsoleSystem.Command[] allCommands = ConsoleSystem.Index.All.Concat(new ConsoleSystem.Command[] { reloadCfgCommand }).ToArray();
            // Would be nice if this had a public setter, or better yet, a register command helper
            typeof(ConsoleSystem.Index)
                .GetProperty(nameof(ConsoleSystem.Index.All), System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
                .SetValue(null, allCommands);
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
                        else if (groupConfig.steamIds.Length < 1)
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
                globallyBlockedCommands = new string[]
                {
                    "global.quit",
                    "global.restart",
                    "demo.record",
                    "demo.recordlist",
                    "demo.splitmegabytes",
                    "demo.splitseconds",
                    "demo.stop"
                },
                logToFile = true,
                groupConfigs = new GroupConfig[]
                {
                    new GroupConfig
                    {
                        name = "demo-group-1",
                        allowAll = false,
                        allowedCommands = new string[]
                        {
                            "global.entid"
                        },
                        steamIds = new ulong[]
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
