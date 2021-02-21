using Newtonsoft.Json;
using System.Collections.Generic;

namespace AdminRestrictions.Configuration
{
    internal class GroupConfig
    {
        [JsonProperty(PropertyName = "Group Name")]
        public string name;

        [JsonProperty(PropertyName = "Allow All Commands")]
        public bool allowAll = false;

        [JsonProperty(PropertyName = "Allowed Commands")]
        public string[] allowedCommands;

        [JsonProperty(PropertyName = "Admin Steam Ids")]
        public List<ulong> steamIds;

        public GroupConfig()
        {
            allowedCommands = allowedCommands ?? new string[0];
            steamIds = steamIds ?? new List<ulong>();
        }
    }
}
