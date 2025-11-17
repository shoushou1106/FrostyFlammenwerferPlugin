> [!NOTE] 
> 此插件正在开发中
  
  
# Frosty Flammenwerfer Plugin
[English (United States)](./README.md) | **简体中文（中国）**
> 🔥 ***Flammenwerfer Plugin***，当 ❄️ ***Frosty*** 遇见炽火之舞。

`Flammenwerfer Plugin` 是 [flammenwerfer](https://github.com/BF1CHS/flammenwerfer/) 的 [Frosty v1](https://github.com/CadeEvs/FrostyToolsuite) 插件移植版。

## 关于
- 此插件从 `FsLocalizationPlugin` 修改而来（对不起，GalaxyMan2015 和 Mophead），并旨在代替它。它们不能同时使用，但是兼容对方的工程和 Mod。
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

## 生成
> [!WARNING]
> 此项目的结构已被更改，但我没有时间去更新截图。请以文字为准，**不要**相信截图。

> [!TIP]
> 由于 Frosty 有很多社区分支，您可能需要自行编译此插件

- 一个现代 Windows 和 Visual Studio 2022 或更新。
1. 在同一个文件夹里[克隆](https://docs.github.com/repositories/creating-and-managing-repositories/cloning-a-repository) FrostyToolsuite 和此仓库。Frosty 可以是[官方版](https://github.com/CadeEvs/FrostyToolsuite)或其他分支。<br><img width="338" height="161" alt="Screenshot 2025-10-29 165404" src="https://github.com/user-attachments/assets/e14bcc7e-78be-458b-84ca-dfb9f951928d" />

> [!TIP]
> 发行版使用[官方版 1.0.6.4](https://github.com/CadeEvs/FrostyToolsuite/tree/1.0.6.4) 和[官方版 1.0.7](https://github.com/CadeEvs/FrostyToolsuite/tree/1.0.7)。

2. 打开 `FrostyFlammenwerferPlugin.sln`。

> [!NOTE]
> 此解决方案需要 `FrostyCore`，`FrostySdk`，`FrostyControl`，和 `FrostyHash`.

3. 打开 `FsLocalizationPlugin/Properties/AssemblyInfo.cs`。<br><img width="284" height="121" alt="image" src="https://github.com/user-attachments/assets/fe3a83a0-18cd-40e7-b1d9-c9c876ef8ded" />

4. 更改插件名。<br><img width="843" height="97" alt="image" src="https://github.com/user-attachments/assets/768471c6-d9f7-495a-aa92-1c7539b7e22d" />

> [!IMPORTANT]
> 请务必增加一些记号来区分您自己的版本和我的发行版以避免混淆，尤其是在分发时。

5. 在工具栏更改生成配置。正常发行使用 `Release - Final`，预发行或需要除错使用 `Developer - Debug`。并保证平台是 `x64`。<br><img width="291" height="223" alt="image" src="https://github.com/user-attachments/assets/225875ef-d5c2-469d-b84e-aca02befafb6" />

6. 在解决方案资源管理器中右键 `FsLocalizationPlugin`，并点击**重新生成**。<br><img width="421" height="278" alt="image" src="https://github.com/user-attachments/assets/a549fba5-2ff7-49ac-96b2-4f75c9aa449d" />

7. 插件会生成到 `FrostyFlammenwerferPlugin\FsLocalizationPlugin\bin\`。无视其他文件，比如 `FrostyHash.dll`。<br><img width="531" height="230" alt="image" src="https://github.com/user-attachments/assets/3c286c81-6036-4576-95fe-f30a4f9f8440" />


## 特别鸣谢
- [NFSLYY](https://space.bilibili.com/14734025) 帮助测试
