# **Rust Admin Restrictions Mod**
A HarmonyMod for Rust that is capable of globally blocking admin commands or restricting admins to only being able to execute specific commands

> **NOTE**: This will only attempt to block commands that require admin permissions to run, you cannot use this to block server commands that are accesible to all players

# Setup
1. Stop your Rust server, **see note below**
2. Download the latest version of [AdminRestrictions.dll](https://github.com/Pinkstink-Rust/AdminRestrictions/releases/latest/download/AdminRestrictions.dll) from this projects [latest release](https://github.com/Pinkstink-Rust/AdminRestrictions/releases/latest)
3. Copy `AdminRestrictions.dll` to the `HarmonyMods` folder in your rust server directory
4. Start your server, **see note below**
5. Once the server has started and the mod has loaded, setup your groups and globally blocked commands in configuration file located `HarmonyMods_Data/AdminRestrictions/Configuration.json`

> **NOTE**: Never update or delete a HarmonyMod DLL file when the rust server is running, this can lead to your server throwing random Invalid IL exceptions and eventually crash.

 # Configuration
 ## Quick Start
 `HarmonyMods_Data/AdminRestrictions/Configuration.json`
```json
{
  "Enabled": true,
  "Globally Blocked Commands": [
    "server.stop",
    "spawn.fill_populations",
    "spawn.fill_groups",
    "global.quit",
    "global.restart"
  ],
  "Log to file": false,
  "Groups": [
    {
      "Group Name": "allow-all",
      "Allow All Commands": true,
      "Allowed Commands": [],
      "Steam Ids": [
        76561198044364727
      ]
    },
    {
      "Group Name": "admins",
      "Allow All Commands": false,
      "Allowed Commands": [
        "global.teleport",
        "global.teleport2marker",
        "global.teleport2player",
        "global.spectate",
        "global.teaminfo",
        "global.teleport2owneditem",
        "server.snapshot"
      ],
      "Steam Ids": [
        76561198288126363
      ]
    }
  ]
}
```

This is a boilerplate configuration that can be used to get started, it will block all admins on the server from running any command that requires admin permissions unless they are matched in the one of the 2 groups defined, the first group will allow `76561198044364727` to run any command they wish with the exception of the commands listed in `Globally Blocked Commands`, the second group allows `76561198288126363` to run commands necessary for teleporting and locally demo recording (`server.snapshot` is sent by the client automatically when starting a demo record).
## Global Explanation
### Enabled
If set to true, allow the Mod from imposing the configured command restrictions.

### Globally Blocked Commands
An array of commands that should always be blocked regardless, this is always process first and will ignore any group specific configuration.

### Log to file
Logs a message to file everytime an admin attempts to run a command that was blocked, this is useful for finding out what you need to allow.

> E.g. An admin needs to be able to run `server.snapshot` to start a local demo record.
> 
> When `server.snapshot` is not allowed and an admin runs `demo.record`, a message will show up in the log file detailing that the admin ran `server.snapshot`, this is because as part of Rust starting a demo recording, it requests an update to date "snapshot" of all entities from the server to ensure it is 100% in sync with the server.

### Groups
An array of restrictions to apply to a group of players, these are processed in the order that they are found in the configuration file, so for this example, if an admin tries to run a command and it's not in `Globally Blocked Commands`, these groups will then next be consulted to determine if the admin can run the command.

The Mod will continue to search all groups until it finds a group that allows the admin to run the command, if it never finds a group that allows the admin to run the command it will block the command.

## Groups Explanation
```json
{
  "Group Name": "admins",
  "Allow All Commands": false,
  "Allowed Commands": [
    "global.teleport",
    "global.teleport2marker",
    "global.teleport2player",
    "global.spectate",
    "global.teaminfo",
    "global.teleport2owneditem",
    "server.snapshot"
  ],
  "Steam Ids": [
    76561198288126363
  ]
}
```

### Group Name
A name to reference the group by in the console commands.

> **NOTE**: This name should be unique and have no spaces, otherwise you will not be able use the console commands to manage it.

### Allow All Commands
If this is set to true, all commands will be permitted, unless they are on the `Globally Blocked Commands` list.

### Allowed Commands
An array of commands that admins in this group are permitted to run.

### Steam Ids
The Steam Ids of admins that this group should apply to.

> **TIP**: An admin can be a part of multiple groups

# Commands
### adminrestrictions.reloadcfg
If you have made changes to the config file directly, you can run this console command and it will load the changes.

### adminrestrictions.addadmintogroup <group name> <admin steam id>
Use this command to add an admins `Steam Id` to a group.

`adminrestrictions.addadmintogroup allow-all 76561198044364727`
> **NOTE**: You cannot use this command to add an admin to a group with spaces in it's `Group Name`

### adminrestrictions.removeadminfromgroup <group name> <admin steam id>
Use this command to remove an admins `Steam Id` from a group.

`adminrestrictions.removeadminfromgroup allow-all 76561198044364727`
> **NOTE**: You cannot use this command to add an admin to a group with spaces in it's `Group Name`

# Final Remarks
### The less Groups, the better
Having less groups removes the overal amount of "loops" or checks the server has to perform to find a group that a user is a part of, whilst this is true, it's a micro optimisation and should not be ignored **IF** you need more groups than what the example config has.