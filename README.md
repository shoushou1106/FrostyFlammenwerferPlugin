> [!NOTE]
> This plugin is still under development.  
  
  
# Frosty Flammenwerfer Plugin
> 🔥 ***Flammenwerfer Plugin***, where ❄️ ***Frosty*** meets the dance of inferno.

`Flammenwerfer Plugin` is a [Frosty v1](https://github.com/CadeEvs/FrostyToolsuite) plugin adaptation of [flammenwerfer](https://github.com/BF1CHS/flammenwerfer/).

## About
- **FlammenwerferPlugin** Is modified from the `FsLocalizationPlugin` (Sorry for GalaxyMan2015 and Mophead), and designed to replace it. They are compatible with each other's projects and mods, but cannot be used at the same time. This plugin supports automatically modifying the histogram to allowing the game to display unsupported characters, and merging multiple mods that modify LocalizedString, even if they were exported with `FsLocalizationPlugin`.
- Feel free to include this plugin with your mod distribution, but it is better to indicate the GitHub link to this repo and [flammenwerfer](https://github.com/BF1CHS/flammenwerfer/), also remember to keep it updated.

## Install
- Delete or disable `FsLocalizationPlugin.dll` from your `Plugins` folder under **Frosty**.
  - It's recommanded to disable it by rename to `FsLocalizationPlugin.dll.disable` or other suffix that is not `.dll`
- Download `FlammenwerferPlugin.dll` from [GitHub Release](https://github.com/shoushou1106/FrostyFlammenwerferPlugin/releases).
- Place the downloaded file in the `Plugins` folder under **Frosty**.

## Todo
- [x] Migrate all features
- [ ] Show diff
- [ ] Export chunk

## Build
> [!TIP]
> Due to Frosty have a lot of community fork, you may need to build your own version of plugin.

- A modern Windows and Visual Studio 2022 or newer.
1. [Clone](https://docs.github.com/repositories/creating-and-managing-repositories/cloning-a-repository) FrostyToolsuite and this repo in a same directory. Frosty can be [official](https://github.com/CadeEvs/FrostyToolsuite) or other fork.<br><img width="338" height="161" alt="Screenshot 2025-10-29 165404" src="https://github.com/user-attachments/assets/e14bcc7e-78be-458b-84ca-dfb9f951928d" />

> [!TIP]
> Release build are based on [official 1.0.6.3](https://github.com/CadeEvs/FrostyToolsuite/tree/1.0.6.3) and [official 1.0.7](https://github.com/CadeEvs/FrostyToolsuite/tree/1.0.7).

2. Open `FrostyFlammenwerferPlugin.sln`

> [!NOTE]
> The solution requires FrostyCore, FrostySdk, FrostyControl, and FrostyHash.

3. Open `FlammenwerferPlugin/Properties/AssemblyInfo.cs`<br><img width="284" height="121" alt="image" src="https://github.com/user-attachments/assets/fe3a83a0-18cd-40e7-b1d9-c9c876ef8ded" />

4. Change plugin name<br><img width="843" height="97" alt="image" src="https://github.com/user-attachments/assets/768471c6-d9f7-495a-aa92-1c7539b7e22d" />

> [!IMPORTANT]
> Add some marks to distinguish your version from my release. This will help avoid confusion, especially when you're distributing(post to internet).

5. Change configuration from the toolbar(at the top). Use `Release - Final` for normal release, `Developer - Debug` for pre-release. And make sure platform is `x64`<br><img width="291" height="223" alt="image" src="https://github.com/user-attachments/assets/225875ef-d5c2-469d-b84e-aca02befafb6" />

6. Right click FlammenwerferPlugin in the Solution Explorer, and click **Rebuild** <br><img width="421" height="278" alt="image" src="https://github.com/user-attachments/assets/a549fba5-2ff7-49ac-96b2-4f75c9aa449d" />

7. Plugin is located in `FrostyFlammenwerferPlugin\FlammenwerferPlugin\bin\`. Ignore other files e.g. FrostyHash.dll<br><img width="531" height="230" alt="image" src="https://github.com/user-attachments/assets/3c286c81-6036-4576-95fe-f30a4f9f8440" />



