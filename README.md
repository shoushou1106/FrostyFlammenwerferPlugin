> [!WARNING]
> This plugin is still under development. Not production-ready. Do not release a mod or Frosty fork with this yet.

# Frosty Flammenwerfer Plugin

**English (United States)** | [简体中文（中国）](./README_zh-Hans-CN.md)

> 🔥 ***Flammenwerfer Plugin***, where ❄️ ***Frosty*** meets the dance of inferno.

Flammenwerfer Plugin is a [Frosty Toolsuite v1](https://github.com/CadeEvs/FrostyToolsuite) plugin, ported from [flammenwerfer](https://github.com/BF1CHS/flammenwerfer/), a tool for editing Frostbite's FsLocalization data. The goal: the next-gen of Frosty modding experiences, an essential plugin for everyone, and there will be no more language barriers.

## What it is

Flammenwerfer Plugin is a drop-in replacement for `FsLocalizationPlugin` (by GalaxyMan2015 and Mophead, with my thanks and respect). But it allows you to mod with almost any language. It reads and writes the same project and mod format, completely safe to upgrade.

## Features

- **Automatic histogram expansion:** Every Frostbite game with FsLocalization uses a histogram: a small table mapping bytes to the characters it knows how to render. Type a character that isn't in that table and vanilla `FsLocalizationPlugin` errors out. Flammenwerfer Plugin **grows the histogram** for you, so the game can display it.
- **Work as usual:** It has all the features of vanilla `FsLocalizationPlugin`, plus performance and stability upgrades.
- **Even without another plugin**: You can add/modify a string without another plugin from Frosty Editor's menu bar. Just in case you need it.
- **Bulk find & replace:** Now featuring regular-expression matching.
- **Export/import chunks to and from files:** You can hand off modified assets as static binary files. Like the original flammenwerfer does.

### Extended Features

- **String removal:** Because why not. Removing a string makes the game display the original `ID_`.

## Compatibility

Two-way compatibility with the original `FsLocalizationPlugin` is Flammenwerfer Plugin's core feature and promise, and it's why the two plugins share a project/mod format:

| Project or mod saved by... | ...opened in Flammenwerfer Plugin | ...opened in FsLocalizationPlugin |
| --- | --- | --- |
| FsLocalizationPlugin | Loads normally | Loads normally (obviously) |
| Flammenwerfer Plugin | Loads normally | Loads normally |
| Flammenwerfer Plugin (with Extended Features) | Loads normally | Loads normally, ignores any extended features |

A few things worth knowing:

- You can't run both plugins at once. Pick one in `Plugins` folder.
- Auto-grown histograms are a Flammenwerfer-only feature. `FsLocalizationPlugin` won't crash but will log an error, and the game shows a blank when a character isn't in the histogram.
- This plugin isn't valid for every game, some Frostbite games use another format, like Dragon Age: Inquisition, Mass Effect Andromeda, Anthem, FIFA series, or Dead Space.
- Open a Flammenwerfer project with extended features using `FsLocalizationPlugin`, those extended features will get ignored. If you save at this point, you will **lose** all saved extended features. Be careful!
- FsLocalization format have limits, it can't fit characters bigger than `0xFFFF`. That means no **emojis** 😭😭 (also no some **rare CJK characters**, **historical characters**, and **ancient scripts**).

## Install

1. Download the plugin from [GitHub Releases](https://github.com/shoushou1106/FrostyFlammenwerferPlugin/releases).
   - Also grab the `.pdb` if you want to debug, rename both the `.dll` and `.pdb` to `FsLocalizationPlugin`.
2. Delete or disable `FsLocalizationPlugin.dll` in your Frosty `Plugins` folder.
   - Disabling is safer than deleting: rename it to something that isn't `.dll`, e.g. `FsLocalizationPlugin.dll.disable`.
3. Place the downloaded file in the `Plugins` folder.
4. (Optional) If you run into problems after installing, try deleting the `Caches` folder under Frosty.

## Frosty 1.0.7 and community forks

The 1.0.7 release here targets the official 1.0.7 build, which doesn't work with Mod Manager. If you're using a community fork instead, such as [HarGabt's](https://github.com/HarGabt/FrostyToolsuite). There's a good chance this exact build won't load. Frosty has a lot of forks, and building a release for every one of them isn't realistic.

If the plugin doesn't work with your fork:

- Check whether the fork has an update, the author may have already integrated it for you.
- If not, ask the fork's author to integrate it for you. Or build it yourself (see below, it's easy).
- Please keep the link to this repository attached when you contact the author. `https://github.com/shoushou1106/FrostyFlammenwerferPlugin`

None of this affects your data: projects and mods use the same format no matter which build made them, so they keep working no matter who built your copy of the plugin.

## Quick Build

Use Quick Build via GitHub Actions, you can build your own Flammenwerfer Plugin with your Frosty fork in a few clicks. This is suggested as a temporary solution before the developer integrates it for you.

> [!NOTE]
> You will need a GitHub account to use this. It's free to create and only needs a email.

1. **Fork** this repo on GitHub.

2. Open **Actions** tab on GitHub

3. Choose **Quick Build** action. On the left if Desktop, top if Mobile.

4. Click **Run workflow**, follow the instructions.

5. Wait for it to finish, and your files is avaliable in **Artifacts** section.

## Build and Development Workflow

Two workflows are supported. The project file switches between them automatically:

- **Separate Workflow:** this repo and your Frosty checkout live side by side in the same folder. More beginner-friendly.
- **Integrated Workflow:** this repo is a git submodule inside `FrostyToolsuite\Plugins`.

### Separate Workflow

> [!NOTE]
> This walkthrough is written for beginners, but I highly recommend that beginners use the Quick Build above.

1. [Clone](https://docs.github.com/repositories/creating-and-managing-repositories/cloning-a-repository) FrostyToolsuite and this repo into the same parent directory. Frosty can be the [official build](https://github.com/CadeEvs/FrostyToolsuite) or a fork.<br><img width="338" height="161" alt="Screenshot 2025-10-29 165404" src="https://github.com/user-attachments/assets/e14bcc7e-78be-458b-84ca-dfb9f951928d" />

2. Open `FrostyFlammenwerferPlugin.sln` with Visual Studio 2022 or newer, on Windows 10 or newer.

> [!NOTE]
> The solution requires FrostyCore, FrostySdk, FrostyControls, and FrostyHash to be present in the sibling Frosty checkout.

3. Open `FsLocalizationPlugin/Properties/AssemblyInfo.cs`.<br><img width="183" height="93" alt="image" src="https://github.com/user-attachments/assets/fe823a46-5995-4651-94bc-f2da1542b1b9" />

4. Change the plugin name.<br><img width="777" height="252" alt="image" src="https://github.com/user-attachments/assets/773f4f86-f18d-4ab2-97d5-25258d4ba2f6" />

> [!TIP]
> Mark your build to distinguish it from an official release. This avoids confusion, especially if you're distributing it.

5. Select the configuration from the toolbar at the top, matching the Frosty version you're building against.<br><img width="178" height="212" alt="image" src="https://github.com/user-attachments/assets/9b157cd2-d18c-4bb7-9eb2-35eb78cd623c" />

   - You can also switch the Frosty version quickly from the bottom-right corner.<br/><img width="347" height="347" alt="image" src="https://github.com/user-attachments/assets/5e8ffb3d-ae8f-4a1b-97ab-a4ff5e4c3f4e" />

6. Right-click the project in Solution Explorer and choose **Build**. If you changed the Frosty version, switched branches, or anything else Frosty-related, use **Rebuild** instead. An incremental build won't pick up a Frosty version switch correctly.<br><img width="439.5" height="180.75" alt="image" src="https://github.com/user-attachments/assets/2833c7c4-bd4a-4b71-806d-8397fcfba32a" />

7. The built plugin is in `bin\`.

### Integrated Workflow

1. Add this repo as a submodule inside your Frosty checkout's `Plugins` directory (if you're not familiar with git submodules, please Google it first):
   ```sh
   git submodule add https://github.com/shoushou1106/FrostyFlammenwerferPlugin Plugins/FrostyFlammenwerferPlugin
   ```
2. Ignore `FrostyFlammenwerferPlugin.sln`. Remove the vanilla FsLocalizationPlugin project from your solution and add `FrostyFlammenwerferPlugin/FsLocalizationPlugin.csproj` from the Plugins folder instead.<br/><img width="369" height="103" alt="image" src="https://github.com/user-attachments/assets/104b27f7-c97f-4ba6-a15a-551eb887ee72" />
3. Open Configuration Manager to manage this plugin's build configuration, note it's still named `FsLocalizationPlugin` there. Visual Studio saves this in the `.sln` file, so it's less hassle than it sounds.<br/><img width="128" height="142" alt="image" src="https://github.com/user-attachments/assets/e325d226-7bee-4bd6-aa3e-5b5487b4c33d" /><br/><img width="502" height="352" alt="image" src="https://github.com/user-attachments/assets/5191bc34-a5fe-43a6-9a97-a3de68c90957" />
4. Change the plugin name if you'd like.<br><img width="777" height="252" alt="image" src="https://github.com/user-attachments/assets/773f4f86-f18d-4ab2-97d5-25258d4ba2f6" />

> [!TIP]
> Mark your build to distinguish it from an official release. This avoids confusion, especially if you're distributing it.

The assembly is intentionally still named `FsLocalizationPlugin.dll` in the codebase (for backwards compatibility).

- On Release config, integrated workflow **renames** the output to `FlammenwerferPlugin.dll` when copying it into your Frosty `bin\` folder.
- On Debug config, integrated workflow does **not** rename the output, but **also copies the pdb** into your Frosty `bin\` folder.

## Special Thanks

- [NFSLYY](https://space.bilibili.com/14734025) for testing
- [HarGabt](https://github.com/HarGabt)'s [fork](https://github.com/HarGabt/FrostyFlammenwerferPlugin)
- [Max Alex](https://github.com/zyf722) and their [BF1CHS](https://github.com/BF1CHS)
- [NFSToolHB](https://github.com/Punpude/NFSToolHB)
- Claude for writing code, Comments, and README
- Gemini for instilling the pyromancy lore
