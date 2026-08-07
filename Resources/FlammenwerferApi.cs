using System;

namespace FsLocalizationPlugin.Resources
{
    /// <summary>
    /// The contract API other plugins reach.
    /// <para>
    /// Read the static <see cref="FsLocalizationStringDatabase.FlammenwerferApiVersion"/> property off the type of <see cref="Frosty.Core.LocalizedStringDatabase.Current"/>,
    /// then resolve <see cref="FsLocalizationPlugin.Resources.FlammenwerferApi"/> from that type's assembly.
    /// Anchoring on the live string database proves Flammenwerfer is not merely installed but is the active string database.
    /// Vanilla FsLocalizationPlugin ships a class with the same name and no such property, so a missing property means not Flammenwerfer.
    /// </para>
    /// </summary>
    /// <remarks>
    /// <para>
    /// Callers cannot name our types, so anything they need must cross the boundary as framework types (uint, string, List, IEnumerable, KeyValuePair)
    /// or as JSON text (<see cref="IdDatabase.ExportJson"/>, <see cref="ProjectIdDatabase.ExportJson"/>).
    /// The two <see cref="IdDatabase.EnumerateEntries"/> and <see cref="ProjectIdDatabase.EnumerateEntries"/> members yield our own entry types,
    /// they still work through plain reflection, but cannot be bound to a delegate,
    /// so read a hash's parts individually (<see cref="ProjectIdDatabase.TryGet"/>, <see cref="ProjectIdDatabase.GetComment"/>, <see cref="ProjectIdDatabase.GetReferences"/>)
    /// or take the JSON instead.
    /// </para>
    /// </remarks>
    public static class FlammenwerferApi
    {
        /// <summary>
        /// Bump on every change, for a caller to test version.
        /// v1: 0.4.1, IdDatabase and ProjectIdDatabase as of the ID Database release.
        /// </summary>
        public const int Version = 1;

        /// <summary>Both ID stores read as one, plus the routing the editor uses when strings change.</summary>
        public static Type IdIndexType => typeof(IdIndex);

        /// <summary>The per-game cached database, shared through one JSON file in Frosty's Caches folder.</summary>
        public static Type IdDatabaseType => typeof(IdDatabase);

        /// <summary>The project store, carried inside one added ebx asset.</summary>
        public static Type ProjectIdDatabaseType => typeof(ProjectIdDatabase);
    }
}
