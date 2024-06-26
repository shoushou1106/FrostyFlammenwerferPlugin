> [!NOTE]
> This plugin is still under development.  
  
  
# Frosty Flammenwerfer Plugin
> 🔥 ***Flammenwerfer Plugin***, where ❄️ ***Frosty*** meets the dance of inferno.

`Flammenwerfer Plugin` is a [Frosty v1](https://github.com/CadeEvs/FrostyToolsuite) plugin adaptation of [flammenwerfer](https://github.com/BF1CHS/flammenwerfer/).

## About
- **FlammenwerferPlugin.Loader**: Modified from the `FsLocalizationPlugin`, designed to replace it. They are compatible with each other's projects and mods, but cannot be used at the same time. This plugin supports automatically modifying the histogram to allowing the game to display unsupported characters, and merging multiple mods that modify LocalizedString, even if they were exported with `FsLocalizationPlugin`.
- **FlammenwerferPlugin.Editor**: Modified from the `LocalizedStringPlugin`, designed to replace it. They can be used at the same time, but it is not recommended. This plugin supports import/export of JSON, CSV, and XLS(X) files.
- You can use only one of them. `FlammenwerferPlugin.Loader` supports `LocalizedStringPlugin`, and `FlammenwerferPlugin.Editor` supports `FsLocalizationPlugin`.
- Mod Manager only needs `FlammenwerferPlugin.Loader`. Feel free to include this plugin with your mod distribution, but it is better to indicate the GitHub link to this repo and [flammenwerfer](https://github.com/BF1CHS/flammenwerfer/), and remember to keep it updated.

## Install
### FlammenwerferPlugin.Loader
- Delete or disable `FsLocalizationPlugin.dll` from your `Plugins` folder under **Frosty**.
- Download `FlammenwerferPlugin.Loader.dll` from [GitHub Release](https://github.com/shoushou1106/FrostyFlammenwerferPlugin/releases).
- Place the downloaded file in the `Plugins` folder under **Frosty**.
### FlammenwerferPlugin.Editor
- **(Optional)** Delete or disable `LocalizedStringPlugin.dll` from your `Plugins` folder under **Frosty Editor**.
- Download `FlammenwerferPlugin.Editor.zip` from [GitHub Release](https://github.com/shoushou1106/FrostyFlammenwerferPlugin/releases).
- Extract **all** the files from the downloaded ZIP file.
- Place **all** the extracted files in the `Plugins` folder under **Frosty Editor**.

## Build
For easy installation, [ILRepack](https://github.com/gluck/il-repack) will be used after building. The following are sample steps (for myself):
- Put the two built DLLs, Frosty, and the ILRepack executable in the same directory.
- Run the following command:
```shell
ILRepack /out:FlammenwerferPluginLoader_ILRepack.dll FlammenwerferPlugin.Loader.dll FlammenwerferPlugin.Flammen.dll
```

## License
This repository contains code from two different repositories with different licenses, so it has multiple csproj and multiple licenses:
- [FlammenwerferPlugin.Loader License](/FlammenwerferPlugin.Loader/LICENSE.md) (Code edit from FsLocalizationPlugin). Originally created by GalaxyMan2015 & Mophead
- [FlammenwerferPlugin.Editor License](/FlammenwerferPlugin.Editor/LICENSE.md) (Code edit from LocalizedStringPlugin). Originally created by Mophead
- [FlammenwerferPlugin.Flammen License](/FlammenwerferPlugin.Flammen/LICENSE) (Code convert from flammenwerfer). Originally created by [zyf722 (Max Alex)](https://github.com/zyf722)

###### ~~this readme and this project sucks, plz help me if u like~~
##### If you think this project or the README useful and would like to help improve it, contributions are welcome!
