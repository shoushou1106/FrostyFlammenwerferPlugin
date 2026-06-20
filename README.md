> [!NOTE]
> This plugin is still under development.  
  
  
# Frosty Flammenwerfer Plugin
**English (United States)** | [简体中文（中国）](./README_zh-Hans-CN.md)
> 🔥 ***Flammenwerfer Plugin***, where ❄️ ***Frosty*** meets the dance of inferno.

`Flammenwerfer Plugin` is a [Frosty v1](https://github.com/CadeEvs/FrostyToolsuite) plugin adaptation of [flammenwerfer](https://github.com/BF1CHS/flammenwerfer/). The vision for this plugin is to build the next generation of Frosty Modding experiences, making it an essential plugin for everyone. *No more language barriers.*

## About
- **FlammenwerferPlugin** is modified from the `FsLocalizationPlugin` (Sorry for GalaxyMan2015 and Mophead), and designed to replace it. They are compatible with each other's projects and mods, but cannot be used at the same time.
- This plugin supports automatically modifying histogram to allowing the game to display unsupported characters。Even the mod were exported by `FsLocalizationPlugin`, or project is saved by `FsLocalizationPlugin`, vice versa, but `FsLocalizationPlugin` do not support modifying histogram.
- Feel free to include this plugin with your mod distribution, but it is better to indicate the GitHub link to this repo and keep it updated. Remember to follow GPL-3.0.
- Support export chunks to files.
- Support import strings from chunk files.
- Support delete strings (Experimental).

## Install
1. Download plugin from [GitHub Releases](https://github.com/shoushou1106/FrostyFlammenwerferPlugin/releases).
    - Download `pdb` if you need to debug. Place it with `dll`.
2. Delete or disable `FsLocalizationPlugin.dll` from your `Plugins` folder under **Frosty**.
    - It's recommanded to disable it by add `.disable` or other suffix that is not `.dll`. Example: `FsLocalizationPlugin.dll.disable`
3. Place the downloaded file in the `Plugins` folder under **Frosty**.
4. (Optional) If you encountered any problems by install this, try delete `Caches` folder under **Frosty**

## 1.0.7 Support
- The provided 1.0.7 release is based on official 1.0.7, which DOES NOT work with Mod Manager.
- You might want to (or already using) another community fork, like [this one from HarGabt](https://github.com/HarGabt/FrostyToolsuite). The plugin you downloaded here have a high possibility will not work with these forks. But I cannot build it for every of them, Frosty have a lot of forks. If you find out this plugin not working with a fork, try build it yourself or ask the author politely to integrate this plugin and throw them the GitHub link of this repo.
- The projects and mods you created use the structure of `FsLocalizationPlugin`, so your projects and mods will work properly doesn't matter who build it.

## Build and Development Workflow
- Frosty have a lot of community fork, you may need to build your own version of plugin. Thats why this repo provides two workflow to build and develop.
- Integrated Workflow: Use git submodule to include this plugin remotely inside `FrostyToolsuite\Plugins` folder.
- Separate Workflow: Put this repo and the Frosty repo in a same folder. This is also more beginner-friendly.

### Integrated Workflow
1. Submodule this repo in Frosty repo's Plugins directory. If you don't know what is Git Submodule please Google it first. Run something like this in the root folder
```sh
git submodule add https://github.com/shoushou1106/FrostyFlammenwerferPlugin Plugins/FrostyFlammenwerferPlugin
```
2. Ignore the `FrostyFlammenwerferPlugin.sln`, delete vanilla FsLocalizationPlugin project from your solution and add `FrostyFlammenwerferPlugin/FsLocalizationPlugin.csproj` inside Plugins folder<br/><img width="369" height="103" alt="image" src="https://github.com/user-attachments/assets/104b27f7-c97f-4ba6-a15a-551eb887ee72" />
3. Open Configuration Manager to manage version of this plugin. Don't forgot it's named `FsLocalizationPlugin`<br/><img width="128" height="142" alt="image" src="https://github.com/user-attachments/assets/e325d226-7bee-4bd6-aa3e-5b5487b4c33d" /><br/><img width="502" height="352" alt="image" src="https://github.com/user-attachments/assets/5191bc34-a5fe-43a6-9a97-a3de68c90957" />
4. Change plugin name, if you think it's necessary.<br><img width="777" height="252" alt="image" src="https://github.com/user-attachments/assets/773f4f86-f18d-4ab2-97d5-25258d4ba2f6" />

> [!IMPORTANT]
> Add some marks to distinguish your version from my release. This will help avoid confusion, especially when you're distributing.


### Separate Workflow
> [!NOTE]
> This tutorial is written for beginners.

1. [Clone](https://docs.github.com/repositories/creating-and-managing-repositories/cloning-a-repository) FrostyToolsuite and this repo in a same directory. Frosty can be [official](https://github.com/CadeEvs/FrostyToolsuite) or other fork.<br><img width="338" height="161" alt="Screenshot 2025-10-29 165404" src="https://github.com/user-attachments/assets/e14bcc7e-78be-458b-84ca-dfb9f951928d" />

2. Open `FrostyFlammenwerferPlugin.sln`

> [!NOTE]
> The solution requires FrostyCore, FrostySdk, FrostyControl, and FrostyHash.

3. Open `FsLocalizationPlugin/Properties/AssemblyInfo.cs`<br><img width="183" height="93" alt="image" src="https://github.com/user-attachments/assets/fe823a46-5995-4651-94bc-f2da1542b1b9" />

4. Change plugin name<br><img width="777" height="252" alt="image" src="https://github.com/user-attachments/assets/773f4f86-f18d-4ab2-97d5-25258d4ba2f6" />

> [!IMPORTANT]
> Add some marks to distinguish your version from my release. This will help avoid confusion, especially when you're distributing.

5. Change configuration from the toolbar(at the top). Make sure to choose correct Frosty version you are building with.<br><img width="178" height="212" alt="image" src="https://github.com/user-attachments/assets/9b157cd2-d18c-4bb7-9eb2-35eb78cd623c" />

    - You can switch Frosty version at the bottom right corner quickly<br/><img width="347" height="347" alt="image" src="https://github.com/user-attachments/assets/5e8ffb3d-ae8f-4a1b-97ab-a4ff5e4c3f4e" />

6. Right click FsLocalizationPlugin in the Solution Explorer, and click **Build**. If you changed Frosty version, branch, or something Frostyy, please click **Rebuild** to avoid any issues. <br><img width="439.5" height="180.75" alt="image" src="https://github.com/user-attachments/assets/2833c7c4-bd4a-4b71-806d-8397fcfba32a" />

7. Plugin is located in `bin\`.


## Special Thanks
- [NFSLYY](https://space.bilibili.com/14734025) for testing
- [HarGabt](https://github.com/HarGabt)'s [fork](https://github.com/HarGabt/FrostyFlammenwerferPlugin)
- [Max Alex](https://github.com/zyf722) and their [BF1CHS](https://github.com/BF1CHS)
- [NFSToolHB](https://github.com/Punpude/NFSToolHB)
