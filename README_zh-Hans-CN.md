> [!WARNING]
> 此插件正在开发中。未达到生产可用标准。请勿发布基于此插件的模组或 Frosty 分支。

# Frosty 喷火器插件

[English (United States)](./README.md) | **简体中文（中国）**

> 🔥 ***喷火器插件***，当 ❄️ ***Frosty*** 遇见炽火之舞。

喷火器插件是一个 [Frosty v1](https://github.com/CadeEvs/FrostyToolsuite) 插件，从 [flammenwerfer](https://github.com/BF1CHS/flammenwerfer/)，一个用于修改寒霜引擎 FsLocalization 数据的工具移植而来。愿景是：构建下一代 Frosty 模组体验，作为每个人的必装基础插件存在。从此再无语言障碍。

## 需要测试者
我需要模组(Mod)创作者/用户和火系法师协助测试此插件，只有几个人无法保证此插件的稳定性。您可以在 [Project](https://github.com/users/shoushou1106/projects/3) 查看路线图和未来计划。要贡献测试，您可以通过测试被标注为“In review”的项目，并在对应的 Issue 中报告测试结果，告知我是否有效或存在任何问题。非常感谢！
如果您想参与测试但遇到问题或不清楚测试流程，请联系我：    
Discord: `shoushou1106`
QQ: `2420308592`

## 此插件是什么

喷火器插件是 `FsLocalizationPlugin` 的直接替代品（由 GalaxyMan2015 和 Mophead 开发，向他们致谢）。但喷火器插件允许您使用几乎所有字符（例如中文字符）进行修改。读写相同的工程和模组格式，升级完全安全。

## 功能

- **自动扩展直方图**：所有使用 FsLocalization 的寒霜游戏都会使用直方图（Histogram）：一个码表，将字节映射到游戏能够渲染的字符。如果输入的字符不在码表中，原版 `FsLocalizationPlugin` 将会报错。喷火器插件会**自动扩展直方图**，让游戏正确显示字符。

- **与往常一样使用**：它拥有原版 `FsLocalizationPlugin` 的所有功能，并附赠性能和稳定性提升。

- **即使没有其他插件也能使用**：无需其他插件即可通过 Frosty 编辑器的菜单栏添加/修改字符串。说不定哪天就用上了。

- **批量查找和替换**：现已支持正则表达式。

- **块（Chunk）文件的导入导出：**：您可以将修改后的资源作为静态二进制文件导出。就像原版 flammenwerfer 一样。

- **检查兼容性：** 在发行 Mod 前检查存在的问题，获取与 `FsLocalizationPlugin` 的兼容报告。

### 扩展功能

- **字符串移除**：有总比没有好。移除一个字符串将使游戏显示原始 `ID_`。

## 兼容性

与原版 `FsLocalizationPlugin` 的双向兼容是喷火器插件的核心特性和目标，这也是为什么这两个插件共享相同工程/模组格式的原因：

| 工程或模组由……保存 | 用 喷火器插件 打开 | 用 FsLocalizationPlugin 打开 |
| --- | --- | --- |
| FsLocalizationPlugin | 正常加载 | 正常加载（不然呢） |
| 喷火器插件 | 正常加载 | 正常加载 |
| 喷火器插件（带扩展功能） | 正常加载 | 正常加载，忽略任何扩展功能 |

一些注意事项：

- 不能同时运行这两个插件，请在 `Plugins` 文件夹中选择一个。

- 自动扩展直方图是喷火器插件独有的功能，也是核心所在。`FsLocalizationPlugin` 如果遇到了，虽然不会崩溃，但会在日志中报告错误，并当游戏在直方图中找不到某个字符时，会显示空白。

- 此插件并非适用于所有游戏。部分寒霜引擎游戏使用其他格式，例如《龙腾世纪：审判》、《质量效应：仙女座》、《圣歌》、《FIFA》系列或《死亡空间》。

- 如果您使用 `FsLocalizationPlugin` 打开一个包含扩展功能的喷火器插件工程，所有扩展功能都将被忽略。如果您此时保存项目，将会**丢失**所有已保存的扩展功能。请务必小心！

- 由于 FsLocalization 格式的限制，**无法支持**大于 `0xFFFF` 的字符。也就是说不能打**表情符号**😭😭。也不能打𰻝𰻝面（**生僻汉字**）。（也不支持某些**历史与古文字**、**特殊符号与字母**）

## 安装

1. 从 [GitHub 发行版](https://github.com/shoushou1106/FrostyFlammenwerferPlugin/releases) 下载插件。

   - 如果需要调试，请一同下载 `.pdb` 文件，并将 dll 文件和 pdb 文件都重命名为 `FsLocalizationPlugin`。

2. 删除或禁用 **Frosty** 的 `Plugins` 文件夹中的 `FsLocalizationPlugin.dll`。

   - 禁用比删除更安全：可以将其重命名为任何不是 `.dll` 的**后缀**，例如 `FsLocalizationPlugin.dll.disable`。

3. 将下载的文件放入 `Plugins` 文件夹。

4. （可选）如果安装后遇到问题，请尝试删除 **Frosty** 目录下的 `Caches` 文件夹。

## Frosty 1.0.7 及社区分支

此处发布的 1.0.7 版本针对的是官方 1.0.7 版本，该版本与模组管理器不兼容。如果您使用的是一个社区分支（例如 [HarGabt 的](https://github.com/HarGabt/FrostyToolsuite)），则此版本很可能无法加载。Frosty 有许多分支，为每个分支都单独生成一个版本不太现实。

如果插件无法在您使用的分支上运行：

- 检查该分支有没有新版，作者是否已经集成此插件。

- 如果没有，请联系分支的作者进行集成，或者自行生成（见下文，很简单的）

- 请务必在联系时附上此仓库的链接 `github.com/shoushou1106/FrostyFlammenwerferPlugin`

不过这无论怎样都不会影响您的数据：无论哪个版本、哪个生成，工程和模组都使用相同的格式，因此无论谁生成了您的插件副本，都会正常工作。

## 快速生成

通过 GitHub Actions 使用快速构建，只需几步即可基于您使用的 Frosty 分支构建您自己的喷火器插件。建议在您的开发者为您集成前使用此方法作为临时替代。

> [!NOTE]
> 使用此方法需要一个 GitHub 账户。创建是免费的，并且只需要电子邮件。

1. 在 GitHub 上 **[分叉](https://docs.github.com/pull-requests/collaborating-with-pull-requests/working-with-forks/fork-a-repo)** 此仓库。<br/><img width="537" height="247" alt="Screenshot 2026-07-06 at 9 12 11 PM" src="https://github.com/user-attachments/assets/0b16e070-c265-4e0f-8647-70b080352a2c" />

2. 在您自己的分支里，导航到 **Actions**<br/><img width="722" height="287" alt="Screenshot 2026-07-06 at 9 15 07 PM" src="https://github.com/user-attachments/assets/d46c56c1-d1a5-4aa3-9e13-a819ea80cc4a" />

3. 点击 **Quick Build** 工作流。桌面端是左侧，移动端是顶部。

4. 点击 **[Run workflow](https://docs.github.com/actions/how-tos/manage-workflow-runs/manually-run-a-workflow)**，并跟从指示<br/><img width="830" height="429" alt="Screenshot 2026-07-06 at 9 09 42 PM" src="https://github.com/user-attachments/assets/835c25e0-0a67-4e7f-b9c7-22c171eb2a14" />

   - 配置为使用 HarGabt 的分支构建喷火器插件 v0.4.0 的示例：<br/><img width="305" height="406" alt="image" src="https://github.com/user-attachments/assets/ec2b7ac4-c14b-4f5c-9d12-5f7680f08946" /><img width="226" height="247" alt="image" src="https://github.com/user-attachments/assets/7dd0a864-4463-427f-b120-aebd0b45ef89" />

5. 等待运行结束，您的文件会出现在 **Artifacts** 区域。<br/><img width="1472" height="734" alt="Screenshot 2026-07-06 at 9 18 47 PM" src="https://github.com/user-attachments/assets/8648111c-3ddd-4212-b9b6-7aa9903d90fc" />

## 生成和开发工作流

我们支持两种面向开发者的工作流，项目文件会自动识别并进行切换：

- **独立工作流**：此仓库和您的 Frosty 仓库位于同一文件夹中。更适合新手。

- **集成工作流**：此仓库是 `FrostyToolsuite\Plugins` 目录下的 Git Submodule。

### 独立工作流

> [!NOTE]
> 本教程是面向新手编写的，但我十分建议新手使用上方的“快速生成”。

1. 在同一个父文件夹里[克隆](https://docs.github.com/repositories/creating-and-managing-repositories/cloning-a-repository) FrostyToolsuite 和此仓库。Frosty 可以是[官方版](https://github.com/CadeEvs/FrostyToolsuite)或其他分支。<br><img width="338" height="161" alt="Screenshot 2025-10-29 165404" src="https://github.com/user-attachments/assets/e14bcc7e-78be-458b-84ca-dfb9f951928d" />

2. 打开 `FrostyFlammenwerferPlugin.sln`。请在运行 Windows 10 或更高版本的现代 PC 上使用 Visual Studio 2022 或更高版本打开。

> [!NOTE]
> 此解决方案需要 Frosty 仓库中包含 FrostyCore、FrostySdk、FrostyControls 和 FrostyHash。

3. 打开 `FsLocalizationPlugin/Properties/AssemblyInfo.cs`。<br><img width="183" height="93" alt="image" src="https://github.com/user-attachments/assets/fe823a46-5995-4651-94bc-f2da1542b1b9" />

4. 更改插件名。<br><img width="777" height="252" alt="image" src="https://github.com/user-attachments/assets/773f4f86-f18d-4ab2-97d5-25258d4ba2f6" />

> [!TIP]
> 在你的构建版本中增加一些记号来区分官方版本。这样可以避免混淆，尤其是在分发时。

5. 在顶部的工具栏更改生成配置，请确保与您正在用于生成的 Frosty 版本相匹配。<br><img width="178" height="212" alt="image" src="https://github.com/user-attachments/assets/9b157cd2-d18c-4bb7-9eb2-35eb78cd623c" />

   - 您也可以在右下角快速切换 Frosty 版本。<br/><img width="347" height="347" alt="image" src="https://github.com/user-attachments/assets/5e8ffb3d-ae8f-4a1b-97ab-a4ff5e4c3f4e" />

6. 在解决方案资源管理器中右键此项目并点击**生成**。如果您更改了 Frosty 版本、分支或任何与 Frosty 相关的内容，请点击**重新生成**以避免出现问题。增量构建无法正确识别 Frosty 版本变更。<br><img width="439.5" height="180.75" alt="image" src="https://github.com/user-attachments/assets/2833c7c4-bd4a-4b71-806d-8397fcfba32a" />

7. 插件会生成到 `bin\`。

### 集成工作流

1. 将这个仓库作为 Submodule 添加到 Frosty 仓库的 `Plugins` 目录。（如果你不熟悉 Git Submodule 请自行查询）：
   ```sh
   git submodule add https://github.com/shoushou1106/FrostyFlammenwerferPlugin Plugins/FrostyFlammenwerferPlugin
   ```
2. 无视 `FrostyFlammenwerferPlugin.sln`。从您的解决方案移除原版 FsLocalizationPlugin 工程并从 Plugins 目录添加 `FrostyFlammenwerferPlugin/FsLocalizationPlugin.csproj`。<br/><img width="369" height="103" alt="image" src="https://github.com/user-attachments/assets/104b27f7-c97f-4ba6-a15a-551eb887ee72" />
3. 打开配置管理器来管理这个插件的生成配置。请注意显示的名字是`FsLocalizationPlugin`。Visual Studio 会把您的配置保存在 .sln 文件中，这可能比你想的要方便。<br/><img width="128" height="142" alt="image" src="https://github.com/user-attachments/assets/e325d226-7bee-4bd6-aa3e-5b5487b4c33d" /><br/><img width="502" height="352" alt="image" src="https://github.com/user-attachments/assets/5191bc34-a5fe-43a6-9a97-a3de68c90957" />
4. 更改插件名称，如果您觉得有必要。<br><img width="777" height="252" alt="image" src="https://github.com/user-attachments/assets/773f4f86-f18d-4ab2-97d5-25258d4ba2f6" />

> [!TIP]
> 在你的构建版本中增加一些记号来区分官方版本。这样可以避免混淆，尤其是在分发时。

在代码库中，程序集被有意命名为“FsLocalizationPlugin.dll”（为了向后兼容）。

- 在 Release 配置下。集成工作流在将输出复制到 Frosty 的 `bin\` 文件夹时，会自动将输出**重命名**为 “FlammenwerferPlugin.dll”。

- 在 Debug 配置下。集成工作流在将输出复制到 Frosty 的 `bin\` 文件夹时，**不会**重命名，但是会顺便带上 pdb 文件。

## 特别鸣谢

- [NFSLYY](https://space.bilibili.com/14734025) 帮助测试
- [HarGabt](https://github.com/HarGabt) 的[分支](https://github.com/HarGabt/FrostyFlammenwerferPlugin)
- [Max Alex](https://github.com/zyf722) 和其的 [BF1CHS](https://github.com/BF1CHS)
- [NFSToolHB](https://github.com/Punpude/NFSToolHB)
- Claude 写了代码，注释，和 README
- Gemini 注入了火焰魔法
