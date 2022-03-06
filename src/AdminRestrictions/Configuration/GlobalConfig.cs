using Newtonsoft.Json;
using System.Collections.Generic;

namespace AdminRestrictions.Configuration
{
    internal class GlobalConfig
    {
        [JsonProperty(PropertyName = "Enabled")]
        public bool enabled = false;

        [JsonProperty(PropertyName = "Globally Blocked Commands")]
        public List<string> globallyBlockedCommands;

        [JsonProperty(PropertyName = "Globally Allowed Commands")]
        public List<string> globallyAllowedCommands;

        [JsonProperty(PropertyName = "Log to file")]
        public bool logToFile = true;

        [JsonProperty(PropertyName = "Groups")]
        public GroupConfig[] groupConfigs;

        public GlobalConfig()
        {
            globallyBlockedCommands = globallyBlockedCommands ?? new List<string>();
            globallyAllowedCommands = globallyAllowedCommands ?? new List<string>();
            groupConfigs = groupConfigs ?? new GroupConfig[0];
        }
    }
}
