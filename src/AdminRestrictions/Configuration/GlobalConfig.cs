using Newtonsoft.Json;

namespace AdminRestrictions.Configuration
{
    internal class GlobalConfig
    {
        [JsonProperty(PropertyName = "Enabled")]
        public bool enabled = false;

        [JsonProperty(PropertyName = "Globally Blocked Commands")]
        public string[] globallyBlockedCommands;

        [JsonProperty(PropertyName = "Globally Allowed Commands")]
        public string[] globallyAllowedCommands;

        [JsonProperty(PropertyName = "Log to file")]
        public bool logToFile = true;

        [JsonProperty(PropertyName = "Groups")]
        public GroupConfig[] groupConfigs;

        public GlobalConfig()
        {
            globallyBlockedCommands = globallyBlockedCommands ?? new string[0];
            globallyAllowedCommands = globallyAllowedCommands ?? new string[0];
            groupConfigs = groupConfigs ?? new GroupConfig[0];
        }
    }
}
