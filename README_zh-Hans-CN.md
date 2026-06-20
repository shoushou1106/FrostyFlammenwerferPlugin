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

## 单独生成与开发
> [!TIP]
> Frosty 有很多社区分支，您可能需要自行编译此插件

- 你需要一个现代 Windows 和 Visual Studio 2022 或更新。（推荐 Visual Studio 2026）
1. 在同一个文件夹里[克隆](https://docs.github.com/repositories/creating-and-managing-repositories/cloning-a-repository) FrostyToolsuite 和此仓库。Frosty 可以是[官方版](https://github.com/CadeEvs/FrostyToolsuite)或其他分支。<br><img width="338" height="161" alt="Screenshot 2025-10-29 165404" src="https://github.com/user-attachments/assets/e14bcc7e-78be-458b-84ca-dfb9f951928d" />

> [!TIP]
> 我的发行版使用[官方版 1.0.6.2](https://github.com/CadeEvs/FrostyToolsuite/tree/1.0.6.2) 、[官方版 1.0.6.4](https://github.com/CadeEvs/FrostyToolsuite/tree/1.0.6.4) 、和[官方版 1.0.7](https://github.com/CadeEvs/FrostyToolsuite/tree/1.0.7)

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
