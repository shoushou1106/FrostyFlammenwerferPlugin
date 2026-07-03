> [!NOTE]
> This plugin is still under development.

> [!WARNING]
> This plugin is not production-ready. Do not release a mod or fork with this yet.

# Frosty Flammenwerfer Plugin

**English (United States)** | [简体中文（中国）](./README_zh-Hans-CN.md)

> 🔥 ***Flammenwerfer Plugin***, where ❄️ ***Frosty*** meets the dance of inferno.

Flammenwerfer Plugin is a [Frosty Toolsuite v1](https://github.com/CadeEvs/FrostyToolsuite) plugin, ported from [flammenwerfer](https://github.com/BF1CHS/flammenwerfer/), a tool for editing Frostbite's FsLocalization data. The goal is: build the next-gen of Frosty modding experiences, becoming an essential plugin for everyone, and there will be no more language barriers.

## What it is

Flammenwerfer Plugin is a drop-in replacement for `FsLocalizationPlugin` (by GalaxyMan2015 and Mophead, with my thanks and respect). But it allows you to mod with more characters. It reads and writes the same project and mod format, completely safe to upgrade.

## Features

- **Automatic histogram expansion:** Every Frostbite game with FsLocalization use a histogram: a small table mapping bytes to the characters it knows how to render. Type a character that isn't in that table and vanilla `FsLocalizationPlugin` will errors out. Flammenwerfer Plugin **grows the histogram** for you, so the game can display it.
- **Work as usual:** It has all the features of vanilla `FsLocalizationPlugin`, plus performance and stability upgrades.
- **Even without another plugin**: You can add/modify a string without another plugin from Frosty Editor's menu bar. Just in case you need it.
- **Bulk find & replace:** Now featuring regular-expression matching.
- **Export/import chunks to and from files:** You can hand off modified assets as static binary files. Like the original flammenwerfer does.

### Extended Features

- **String removal:** Because why not. Removing a string will result the game to display the original ID_.

## Compatibility

Two-way compatibility with the original `FsLocalizationPlugin` is Flammenwerfer Plugin's core feature and promise, and it's why the two plugins share a project/mod format:

| Project or mod saved by... | ...opened in Flammenwerfer Plugin | ...opened in `FsLocalizationPlugin` |
| --- | --- | --- |
| FsLocalizationPlugin | Loads normally | Loads normally (obviously) |
| Flammenwerfer Plugin | Loads normally | Loads normally |
| Flammenwerfer Plugin (with Extended Features) | Loads normally | Loads normally, ignores any extended features |

A few things worth knowing:

- You can't run both plugins at once — pick one in `Plugins` folder.
- Auto-grown histograms are a Flammenwerfer-only feature; `FsLocalizationPlugin` won't crash but report error is Logs, and the game will display a blank when a character is not found in histogram.
- This plugin isn't valid for every game — Some Frostbite games use another format, like Dragon Age: Inquisition, Mass Effect Andromeda, Anthem, FIFA series, or Dead Space.
- If you open a Flammenwerfer project with extended features using `FsLocalizationPlugin`, all extended features will be ignored. If you save the project at this point, you will **lose** all saved extended features. Be careful!

## Install

1. Download the plugin from [GitHub Releases](https://github.com/shoushou1106/FrostyFlammenwerferPlugin/releases).
   - Also grab the `.pdb` if you want to debug, rename both the dll and pdb to `FsLocalizationPlugin`, and place it next to the `.dll`.
2. Delete or disable `FsLocalizationPlugin.dll` in your Frosty `Plugins` folder.
   - Disabling is safer than deleting: rename it to something that isn't `.dll`, e.g. `FsLocalizationPlugin.dll.disable`.
3. Place the downloaded file in the `Plugins` folder.
4. (Optional) If you run into problems after installing, try deleting the `Caches` folder under Frosty.

## Frosty 1.0.7 and community forks

The 1.0.7 release here targets the official 1.0.7 build, which doesn't work with Mod Manager. If you're using a community fork instead — such as [HarGabt's](https://github.com/HarGabt/FrostyToolsuite) — there's a good chance this exact build won't load. Frosty has a lot of forks, and building a release for every one of them isn't realistic.

If the plugin doesn't work with your fork:

- Check whether the fork has already integrated it for you.
- If not, build it yourself (see below) or ask the fork's author to integrate it — please keep the link to this repository (`github.com/shoushou1106/FrostyFlammenwerferPlugin`) attached when you do.

None of this affects your data: projects and mods use the same structure regardless of which build produced them, so they'll keep working no matter who built your copy of the plugin.

## Build and Development Workflow

Because of the fork situation above, you may need to build your own copy of this plugin. Two workflows are supported:

- **Separate Workflow** — this repo and your Frosty checkout live side by side in the same folder. More beginner-friendly.
- **Integrated Workflow** — this repo is a git submodule inside `FrostyToolsuite\Plugins`.

### Separate Workflow

> [!NOTE]
> This walkthrough is written for beginners.

1. [Clone](https://docs.github.com/repositories/creating-and-managing-repositories/cloning-a-repository) FrostyToolsuite and this repo into the same parent directory. Frosty can be the [official build](https://github.com/CadeEvs/FrostyToolsuite) or a fork.<br><img width="338" height="161" alt="Screenshot 2025-10-29 165404" src="https://github.com/user-attachments/assets/e14bcc7e-78be-458b-84ca-dfb9f951928d" />

2. Open `FrostyFlammenwerferPlugin.sln`. With Visual Studio 2022 or newer on a modern PC running Windows 10 or newer.

> [!NOTE]
> The solution requires FrostyCore, FrostySdk, FrostyControls, and FrostyHash to be present in the sibling Frosty checkout.

3. Open `FsLocalizationPlugin/Properties/AssemblyInfo.cs`.<br><img width="183" height="93" alt="image" src="https://github.com/user-attachments/assets/fe823a46-5995-4651-94bc-f2da1542b1b9" />

4. Change the plugin name.<br><img width="777" height="252" alt="image" src="https://github.com/user-attachments/assets/773f4f86-f18d-4ab2-97d5-25258d4ba2f6" />

> [!IMPORTANT]
> Mark your build to distinguish it from an official release. This avoids confusion, especially if you're distributing it.

5. Select the configuration from the toolbar at the top, matching the Frosty version you're building against.<br><img width="178" height="212" alt="image" src="https://github.com/user-attachments/assets/9b157cd2-d18c-4bb7-9eb2-35eb78cd623c" />

   - You can also switch the Frosty version quickly from the bottom-right corner.<br/><img width="347" height="347" alt="image" src="https://github.com/user-attachments/assets/5e8ffb3d-ae8f-4a1b-97ab-a4ff5e4c3f4e" />

6. Right-click the project in Solution Explorer and choose **Build**. If you changed the Frosty version, switched branches, or anything else Frosty-related, use **Rebuild** instead — the project uses version-conditional compilation, and an incremental build won't pick up a config switch correctly.<br><img width="439.5" height="180.75" alt="image" src="https://github.com/user-attachments/assets/2833c7c4-bd4a-4b71-806d-8397fcfba32a" />

7. The built plugin is in `bin\`.

### Integrated Workflow

1. Add this repo as a submodule inside your Frosty checkout's `Plugins` directory (if you're not familiar with git submodules, please Google it first):
   ```sh
   git submodule add https://github.com/shoushou1106/FrostyFlammenwerferPlugin Plugins/FrostyFlammenwerferPlugin
   ```
2. Ignore `FrostyFlammenwerferPlugin.sln`. Remove the vanilla FsLocalizationPlugin project from your solution and add `FrostyFlammenwerferPlugin/FsLocalizationPlugin.csproj` from the Plugins folder instead.<br/><img width="369" height="103" alt="image" src="https://github.com/user-attachments/assets/104b27f7-c97f-4ba6-a15a-551eb887ee72" />
3. Open Configuration Manager to manage this plugin's build configuration — note it's still named `FsLocalizationPlugin` there. Visual Studio saves this in the `.sln` file, so it's less hassle than it sounds.<br/><img width="128" height="142" alt="image" src="https://github.com/user-attachments/assets/e325d226-7bee-4bd6-aa3e-5b5487b4c33d" /><br/><img width="502" height="352" alt="image" src="https://github.com/user-attachments/assets/5191bc34-a5fe-43a6-9a97-a3de68c90957" />
4. Change the plugin name if you'd like.<br><img width="777" height="252" alt="image" src="https://github.com/user-attachments/assets/773f4f86-f18d-4ab2-97d5-25258d4ba2f6" />

> [!IMPORTANT]
> Mark your build to distinguish it from an official release. This avoids confusion, especially if you're distributing it.

The assembly is intentionally still named `FsLocalizationPlugin.dll` even in this repo (for backwards compatibility with the plugin it replaces) — a post-build step **renames** the Release output to `FlammenwerferPlugin.dll` when copying it into your Frosty install.

## Special Thanks

- [NFSLYY](https://space.bilibili.com/14734025) for testing
- [HarGabt](https://github.com/HarGabt)'s [fork](https://github.com/HarGabt/FrostyFlammenwerferPlugin)
- [Max Alex](https://github.com/zyf722) and their [BF1CHS](https://github.com/BF1CHS)
- [NFSToolHB](https://github.com/Punpude/NFSToolHB)
