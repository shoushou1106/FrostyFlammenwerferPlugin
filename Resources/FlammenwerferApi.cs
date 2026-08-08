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
    /// or as JSON text (<see cref="IdDatabase.ExportJson"/>, <see cref="ProjectIdDatabase.ExportJson"/>, <see cref="IdIndex.ImportJson"/>).
    /// The two <see cref="IdDatabase.EnumerateEntries"/> and <see cref="ProjectIdDatabase.EnumerateEntries"/> members yield our own <see cref="IdEntry"/> type,
    /// they still work through plain reflection, but cannot be bound to a delegate,
    /// so read a hash's parts individually (<see cref="ProjectIdDatabase.TryGetId"/>, <see cref="ProjectIdDatabase.GetReferences"/>)
    /// or take the JSON instead.
    /// </para>
    /// <para>
    /// Both databases speak the same document (see <see cref="IdDocument"/>):
    /// A version, a note, the game, then entries keyed by hash in hex,
    /// each holding an ID text and a list of asset paths, both of which can be empty.
    /// </para>
    /// <para>
    /// この未来は好都合に光ってる<br/>
    /// だから進むんだ
    /// </para>
    /// </remarks>
    public static class FlammenwerferApi
    {
        /// <summary>
        /// Bump on every change, for a caller to test version.
        /// v1: v0.4.1 ID Database release. Introduce IdIndex, IdDatabase, and ProjectIdDatabase.
        /// </summary>
        public const int Version = 1;

        /// <summary>Both ID databases read as one, plus the routing and importing the editor uses.</summary>
        public static Type IdIndexType => typeof(IdIndex);

        /// <summary>The per-game cached database, shared through one JSON file in Frosty's Caches folder.</summary>
        public static Type IdDatabaseType => typeof(IdDatabase);

        /// <summary>The project database, carried inside one added ebx asset.</summary>
        public static Type ProjectIdDatabaseType => typeof(ProjectIdDatabase);
    }
}
