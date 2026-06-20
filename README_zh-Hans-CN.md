> [!NOTE] 
> 此插件正在开发中
  
  
# Frosty Flammenwerfer Plugin
[English (United States)](./README.md) | **简体中文（中国）**
> 🔥 ***Flammenwerfer Plugin***，当 ❄️ ***Frosty*** 遇见炽火之舞。

`Flammenwerfer Plugin` 是 [flammenwerfer](https://github.com/BF1CHS/flammenwerfer/) 的 [Frosty v1](https://github.com/CadeEvs/FrostyToolsuite) 插件移植版。此插件的愿景是构建下一代的 Frosty Modding 体验，作为每个人的必装基础插件存在。从此再无语言障碍。

## 关于
- 此插件从 `FsLocalizationPlugin` 修改而来（对不起，GalaxyMan2015 和 Mophead），并旨在代替它。它们兼容对方的工程和 Mod，但不能同时使用。
- 此插件支持自动修改码表（Histogram）从而让游戏显示不支持的字符。即使 Mod 是用 `FsLocalizationPlugin` 导出的，或者工程是用 `FsLocalizationPlugin` 保存的。反之亦然，但 `FsLocalizationPlugin` 不支持修改码表。
- 欢迎将此插件随附您的 Mod 一起分发，但最好标注此 GitHub 仓库链接，并保持更新。记得遵守 GPL-3.0。
- 支持将字符串导出成 Chunk。
- 支持从 Chunk 导入字符串。
- 支持移除字符串（实验性）。

## 安装
1. 从 [GitHub 发行版](https://github.com/shoushou1106/FrostyFlammenwerferPlugin/releases)下载插件。
  - 如果需要除错，也可以下载 `pdb`，并和 `dll` 放在一起。
2. 从 **Frosty** 的 `Plugins` 文件夹删除或禁用 `FsLocalizationPlugin.dll`。
  - 建议给插件添加 `.disable` 或其他不是 `.dll` 的**后缀**以禁用。示例：`FsLocalizationPlugin.dll.disable`。
3. 把下载的文件放在 **Frosty** 的 `Plugins` 文件夹。
4. （可选）如果您因为安装插件遇到了错误，可尝试删除 **Frosty** 的 `Caches` 文件夹。

## 1.0.7 支持
- 这里提供的 1.0.7 发行版是基于官方 1.0.7 构建的，无法在官方版 1.0.7 Mod Manager 中正常工作（因为官方版没做完，有 Bug）
- 您可能想要（大概率已经在）使用一个社区分支(比如这个来自 HarGabt 的)[https://github.com/HarGabt/FrostyToolsuite]。这个插件很有可能无法在这些分支里正常工作。我不可能为每一个分支都单独编译一个版本，Frosty 的分支太多了。如果您发现这个插件无法在你使用的分支里工作，可以先检查一下那个分支是否有更新，作者说不定已经帮你集成好了这个插件。如果没有，您可以尝试自行构建或礼貌的请求那个分支的开发者将这个插件集成进去，不要忘了附上这个 GitHub 仓库的链接 `github.com/shoushou1106/FrostyFlammenwerferPlugin`
- 您创建的工程和 Mod 都是基于 `FsLocalizationPlugin` 的结构保存的，也就是无论是谁构建的版本都应该能正常打开同一份工程或 Mod。

## 生成与开发工作流
- Frosty 有很多社区分支，您可能需要自行编译此插件。这也是为什么这个仓库提供两种工作流来帮助您生成与开发。
- 集成工作流：使用 Git Submodule 来将此仓库远程集成到 `FrostyToolsuite\Plugins` 文件夹
- 独立工作流：将这个仓库和 Frosty 仓库放在同一个目录下。对新手更友好。

### 集成工作流
1. 将这个仓库 Submodule 到 Frosty 仓库的 Plugins 目录。如果你不知道 Submodule 请自行查询“Git Submodule”。在根目录运行类似这样的命令：
```sh
git submodule add https://github.com/shoushou1106/FrostyFlammenwerferPlugin Plugins/FrostyFlammenwerferPlugin
```
2. 无视 `FrostyFlammenwerferPlugin.sln`，从您的解决方案移除原版 FsLocalizationPlugin 工程，并将 `FrostyFlammenwerferPlugin/FsLocalizationPlugin.csproj` 作为工程添加到 Plugins 目录<br/><img width="369" height="103" alt="image" src="https://github.com/user-attachments/assets/104b27f7-c97f-4ba6-a15a-551eb887ee72" />
3. 打开配置管理器来管理这个插件的适配版本。别忘了显示的名字是`FsLocalizationPlugin`<br/><img width="128" height="142" alt="image" src="https://github.com/user-attachments/assets/e325d226-7bee-4bd6-aa3e-5b5487b4c33d" /><br/><img width="502" height="352" alt="image" src="https://github.com/user-attachments/assets/5191bc34-a5fe-43a6-9a97-a3de68c90957" />
4. 更改插件名称，如果您觉得有必要<br><img width="777" height="252" alt="image" src="https://github.com/user-attachments/assets/773f4f86-f18d-4ab2-97d5-25258d4ba2f6" />

> [!IMPORTANT]
> 增加一些记号可以区分您自己的版本和我的发行版，这样可以避免混淆，尤其当您在分发时。


### 独立工作流
> [!NOTE]
> 这个教程是为新手编写的

1. 在同一个文件夹里[克隆](https://docs.github.com/repositories/creating-and-managing-repositories/cloning-a-repository) FrostyToolsuite 和此仓库。Frosty 可以是[官方版](https://github.com/CadeEvs/FrostyToolsuite)或其他分支。<br><img width="338" height="161" alt="Screenshot 2025-10-29 165404" src="https://github.com/user-attachments/assets/e14bcc7e-78be-458b-84ca-dfb9f951928d" />

2. 打开 `FrostyFlammenwerferPlugin.sln`。

> [!NOTE]
> 此解决方案需要 `FrostyCore`，`FrostySdk`，`FrostyControl`，和 `FrostyHash`.

3. 打开 `FsLocalizationPlugin/Properties/AssemblyInfo.cs`。<br><img width="183" height="93" alt="image" src="https://github.com/user-attachments/assets/fe823a46-5995-4651-94bc-f2da1542b1b9" />

4. 更改插件名<br><img width="777" height="252" alt="image" src="https://github.com/user-attachments/assets/773f4f86-f18d-4ab2-97d5-25258d4ba2f6" />

> [!IMPORTANT]
> 请务必增加一些记号来区分您自己的版本和我的发行版，这样可以避免混淆，尤其当您在分发时。

5. 在工具栏更改生成配置。请务必选择您正在用于构建的 Frosty 版本。<br><img width="178" height="212" alt="image" src="https://github.com/user-attachments/assets/9b157cd2-d18c-4bb7-9eb2-35eb78cd623c" />

    - 您可以在右下角快速切换 Frosty 分支版本<br/><img width="347" height="347" alt="image" src="https://github.com/user-attachments/assets/5e8ffb3d-ae8f-4a1b-97ab-a4ff5e4c3f4e" />

6. 在解决方案资源管理器中右键 `FsLocalizationPlugin`，并点击**生成**。如果您更改了 Frosty 版本、分支或任何与 Frosty 相关的内容，请点击**重新生成**以避免出现问题。<br><img width="439.5" height="180.75" alt="image" src="https://github.com/user-attachments/assets/2833c7c4-bd4a-4b71-806d-8397fcfba32a" />

7. 插件会生成到 `bin\`


## 特别鸣谢
- [NFSLYY](https://space.bilibili.com/14734025) 帮助测试
- [HarGabt](https://github.com/HarGabt) 的[分支](https://github.com/HarGabt/FrostyFlammenwerferPlugin)
- [Max Alex](https://github.com/zyf722) 和其的 [BF1CHS](https://github.com/BF1CHS)
- [NFSToolHB](https://github.com/Punpude/NFSToolHB)
