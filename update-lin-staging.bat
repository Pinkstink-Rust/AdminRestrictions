pwsh scripts\SteamDownloader.ps1 -steam_appid 258550 -steam_branch staging -platform linux -deps_dir "../deps"
dotnet ./scripts/RadiumPublicizer/RadiumPublicizer.dll deps/linux/RustDedicated_Data/Managed
PAUSE