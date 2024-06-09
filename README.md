### This plugin is still under development.  
  
  
# Frosty Flammenwerfer Plugin
> 🔥 ***Flammenwerfer Plugin***, where ❄️ ***Frosty*** meets the dance of inferno.

`Flammenwerfer Plugin` is a [Frosty v1](https://github.com/CadeEvs/FrostyToolsuite) plugin adaptation of [flammenwerfer](https://github.com/BF1CHS/flammenwerfer/). It is modified from the `FsLocalizationPlugin` and designed to automatically modify the histogram, allowing the game to display unsupported characters.  
This plugin supports projects that use `FsLocalizationPlugin`, and `FsLocalizationPlugin` can also support projects that use this plugin. But they cannot be used at the same time.

## Install
- Delete or disable `FsLocalizationPlugin.dll` from your `Plugins` folder under **Frosty**.
- Download the plugin from [GitHub Release](https://github.com/shoushou1106/FrostyFlammenwerferPlugin/releases) and place `all` the .dll files in the `Plugins` folder under **Frosty**.

## Build
For easy installation, [ILRepack](https://github.com/gluck/il-repack) will be used after building. The following are sample steps (for myself)
- Put the two built dlls and Frosty and ILRepack exe in the same directory
- Run this code
```shell
ILRepack /out:FlammenwerferPlugin_ILRepack.dll FlammenwerferPlugin.dll FlammenwerferPlugin.Flammen.dll
```

## License
This repository contains code from two different repositories with different licenses, so it has two csproj files and two licenses:
- [FlammenwerferPlugin](/FlammenwerferPlugin/LICENSE.md) (Code edit from FrostyToolsuite)
- [FlammenwerferPlugin.Flammen](/FlammenwerferPlugin.Flammen/LICENSE) (Code convert from flammenwerfer)
