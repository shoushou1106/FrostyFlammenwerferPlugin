using Frosty.Core;
using Frosty.Core.Attributes;
using FrostySdk;
using FsLocalizationPlugin;
using FsLocalizationPlugin.Extensions;
using System.Runtime.InteropServices;
using System.Windows;

// Setting ComVisible to false makes the types in this assembly not visible
// to COM components.  If you need to access a type in this assembly from
// COM, set the ComVisible attribute to true on that type.
[assembly: ComVisible(false)]

[assembly: ThemeInfo(
    ResourceDictionaryLocation.None, //where theme specific resource dictionaries are located
                                     //(used if a resource is not found in the page, 
                                     // or application resource dictionaries)
    ResourceDictionaryLocation.SourceAssembly //where the generic resource dictionary is located
                                              //(used if a resource is not found in the page, 
                                              // app, or any theme specific resource dictionaries)
)]

// The following GUID is for the ID of the typelib if this project is exposed to COM
[assembly: Guid("4b612468-9b6a-4304-88a5-055c3575eb3d")]

[assembly: PluginDisplayName("Flammenwerfer")]
[assembly: PluginAuthor("shoushou1106")]

#if FROSTY_107
[assembly: PluginVersion("0.3.1 (for 1.0.7)")]
#elif FROSTY_1063_LATER
[assembly: PluginVersion("0.3.1 (for 1.0.6.3 or Later)")]
#elif FROSTY_1062_EARLIER
[assembly: PluginVersion("0.3.1 (for 1.0.6.2 or Earlier)")]
#else
[assembly: PluginVersion("0.3.1 (Unknown)")]
#endif

[assembly: PluginNotValidForProfile((int)ProfileVersion.DragonAgeInquisition)]
[assembly: PluginNotValidForProfile((int)ProfileVersion.MassEffectAndromeda)]
[assembly: PluginNotValidForProfile((int)ProfileVersion.Anthem)]
[assembly: PluginNotValidForProfile((int)ProfileVersion.Fifa17)]
[assembly: PluginNotValidForProfile((int)ProfileVersion.Fifa18)]
[assembly: PluginNotValidForProfile((int)ProfileVersion.Fifa19)]
[assembly: PluginNotValidForProfile((int)ProfileVersion.Fifa20)]
#if FROSTY_107
[assembly: PluginNotValidForProfile((int)ProfileVersion.DeadSpace)]
#endif

[assembly: RegisterCustomHandler(CustomHandlerType.Ebx, typeof(FsLocalizationCustomActionHandler), ebxType: "UITextDatabase")]
[assembly: RegisterLocalizedStringDatabase(typeof(FsLocalizationStringDatabase))]

// https://github.com/CadeEvs/FrostyToolsuite/pull/311
// Starting Frosty 1.0.6.3, Dyvinia added PluginManagerType to RegisterMenuExtension attribute.
// On Frosty 1.0.7 and 1.0.6.2 or earlier, the PluginManagerType does not exist, writing nothing equals to PluginManagerType.Editor
#if FROSTY_1063_LATER
[assembly: RegisterMenuExtension(typeof(ModifyStringMenuExtension), PluginManagerType.Editor)]
[assembly: RegisterMenuExtension(typeof(ModifyMultipleStringsMenuExtension), PluginManagerType.Editor)]
[assembly: RegisterMenuExtension(typeof(ImportChunksMenuExtension), PluginManagerType.Editor)]
[assembly: RegisterMenuExtension(typeof(ExportChunksMenuExtension), PluginManagerType.Editor)]
[assembly: RegisterMenuExtension(typeof(CheckCompatibilityMenuExtension), PluginManagerType.Editor)]
#else
[assembly: RegisterMenuExtension(typeof(ModifyStringMenuExtension))]
[assembly: RegisterMenuExtension(typeof(ModifyMultipleStringsMenuExtension))]
[assembly: RegisterMenuExtension(typeof(ImportChunksMenuExtension))]
[assembly: RegisterMenuExtension(typeof(ExportChunksMenuExtension))]
[assembly: RegisterMenuExtension(typeof(CheckCompatibilityMenuExtension))]
#endif
