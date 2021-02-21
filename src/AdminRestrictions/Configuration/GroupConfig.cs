using Newtonsoft.Json;

namespace AdminRestrictions.Configuration
{
    internal class GroupConfig
    {
        [JsonProperty(PropertyName = "Allow All Commands")]
        public bool allowAll = false;

        [JsonProperty(PropertyName = "Allowed Commands")]
        public string[] allowedCommands;

        [JsonProperty(PropertyName = "Admin Steam Ids")]
        public ulong[] steamIds;

        public GroupConfig()
        {
            allowedCommands = allowedCommands ?? new string[0];
            steamIds = steamIds ?? new ulong[0];
        }
    }
}
