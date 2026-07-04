using Frosty.Core;
using FrostySdk.IO;
using FrostySdk.Managers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

#if FROSTY_107
using FrostySdk.Managers.Entries;
#endif

namespace FsLocalizationPlugin
{
    /// <summary>
    /// Reads and writes the two Frostbite chunk types that back a game's localized text:
    /// the <b>histogram</b> chunk, a lookup table mapping single- and double-byte codes to
    /// Unicode code points, and the <b>strings binary</b> chunk, a table of string-ID
    /// hashes plus histogram-encoded, null-terminated string data.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Histogram chunk layout</b> (magic <c>0x00039001</c>): <c>uint magic</c>,
    /// <c>uint fileSize</c> (byte count following the two header fields),
    /// <c>uint dataOffSize</c> (see <see cref="AddCharsToHistogram"/>), followed by a flat
    /// table of <c>ushort</c> code points. The table's first 128 entries are the identity
    /// mapping for ASCII; everything from index <c>0x80</c> onward is populated (and grown)
    /// by <see cref="AddCharsToHistogram"/>.
    /// </para>
    /// <para>
    /// <b>Strings binary chunk layout</b> (magic <c>0x00039000</c>): <c>uint magic</c>,
    /// <c>uint fileSize</c>, <c>uint listSize</c> (string count), <c>uint dataOffset</c>
    /// (fixed at <see cref="StringDefaultDataOffset"/>), <c>uint stringsOffset</c>, a
    /// null-terminated section-name string, padding out to <c>dataOffset</c>, then
    /// <c>listSize</c> pairs of (<c>uint</c> string-ID hash, <c>uint</c> offset into the
    /// string data relative to <c>stringsOffset</c>), followed by the histogram-encoded,
    /// null-terminated string data itself.
    /// </para>
    /// </remarks>
    public class Flammen
    {
        // String chunk constants
        private const uint StringMagic = 0x00039000;
        private const uint StringDefaultDataOffset = 0x8C;
        private const uint StringHashPairSize = 8;

        // Histogram constants
        private const uint HistogramMagic = 0x00039001;
        private const int HistogramAsciiThreshold = 0x80;
        private const int HistogramMaxShiftValue = 0xFF;
        private const int HistogramInitialSectionSize = 0x80;
        private const int HistogramShiftNumStartIndex = 0x40;
        private const int HistogramShiftEndIndex = 0x1FE;

        /// <summary>
        /// Decodes a histogram-encoded binary string into its Unicode representation.
        /// </summary>
        /// <param name="binString">The raw, histogram-encoded bytes (as a string of chars 0-255).</param>
        /// <param name="section">The histogram code-point table to decode against.</param>
        /// <returns>The decoded string.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="binString"/> or <paramref name="section"/> is null.</exception>
        /// <remarks>
        /// Bytes below <c>0x80</c> pass through as ASCII. Bytes at or above <c>0x80</c> are
        /// looked up in the histogram: if the mapped value is itself &gt;= <c>0x80</c>, it's
        /// used directly as the code point; otherwise it's a shift number, and the *next*
        /// byte (which must also be &gt;= <c>0x80</c>) combines with it to index further into
        /// the table. This differs from the original Python <c>flammenwerfer</c>
        /// implementation, which guards the second byte with <c>&gt; 0x80</c> rather than
        /// <c>&gt;= 0x80</c> - that stricter check caused a "no proper shift" failure the
        /// moment a game launched with zero modified strings, so the looser guard here is a
        /// deliberate fix, not an oversight.
        /// </remarks>
        public static string DecodeString(string binString, List<ushort> section)
        {
            if (binString == null)
                throw new ArgumentNullException(nameof(binString));
            if (section == null)
                throw new ArgumentNullException(nameof(section));

            int index = 0;
            StringBuilder sb = new StringBuilder();

            while (index < binString.Length)
            {
                byte currentByte = (byte)binString[index];

                if (currentByte < HistogramAsciiThreshold)
                {
                    // ASCII character - decode directly
                    sb.Append((char)currentByte);
                }
                else
                {
                    // Non-ASCII character - decode using histogram
                    ushort mappedValue = section[currentByte];

                    if (mappedValue >= HistogramAsciiThreshold)
                    {
                        // Direct mapping
                        sb.Append((char)mappedValue);
                    }
                    else
                    {
                        // Shift-based mapping
                        index++;
                        if (index >= binString.Length)
                            break;

                        currentByte = (byte)binString[index];
                        if (currentByte >= HistogramAsciiThreshold)
                        {
                            int shiftedIndex = currentByte - HistogramAsciiThreshold + (mappedValue << 7);
                            sb.Append((char)section[shiftedIndex]);
                        }
                    }
                }
                index++;
            }
            return sb.ToString();
        }

        /// <summary>
        /// Encodes a string into histogram-encoded bytes, building a fresh character-to-index
        /// lookup from <paramref name="section"/> before encoding.
        /// </summary>
        /// <param name="str">The string to encode.</param>
        /// <param name="shifts">The histogram indices usable as two-byte shift prefixes, in the order they should be tried.</param>
        /// <param name="section">The histogram code-point table to encode against.</param>
        /// <returns>The encoded byte array with a trailing null terminator.</returns>
        /// <exception cref="ArgumentNullException">Thrown when any parameter is null.</exception>
        /// <exception cref="ArgumentException">Thrown when a character cannot be encoded.</exception>
        /// <remarks>
        /// This overload exists for API compatibility - it builds a lookup dictionary on
        /// every call. Encoding many strings against the same histogram (as
        /// <see cref="WriteAll"/> does) should build that dictionary once with
        /// <see cref="BuildCharIndex"/> and call the <see cref="EncodeString(string, List{int}, List{char}, Dictionary{char, int})"/>
        /// overload directly instead.
        /// </remarks>
        public static byte[] EncodeString(string str, List<int> shifts, List<char> section)
        {
            if (section == null)
                throw new ArgumentNullException(nameof(section));

            return EncodeString(str, shifts, section, BuildCharIndex(section));
        }

        /// <summary>
        /// Encodes a string into histogram-encoded bytes using a precomputed character-to-index
        /// lookup, as produced by <see cref="BuildCharIndex"/>.
        /// </summary>
        /// <param name="str">The string to encode.</param>
        /// <param name="shifts">The histogram indices usable as two-byte shift prefixes, in the order they should be tried.</param>
        /// <param name="section">The histogram code-point table to encode against.</param>
        /// <param name="charIndex">A map from character to its first occurrence's index in <paramref name="section"/>.</param>
        /// <returns>The encoded byte array with a trailing null terminator.</returns>
        /// <exception cref="ArgumentNullException">Thrown when any parameter is null.</exception>
        /// <exception cref="ArgumentException">Thrown when a character cannot be encoded.</exception>
        public static byte[] EncodeString(string str, List<int> shifts, List<char> section, Dictionary<char, int> charIndex)
        {
            if (str == null)
                throw new ArgumentNullException(nameof(str));
            if (shifts == null)
                throw new ArgumentNullException(nameof(shifts));
            if (section == null)
                throw new ArgumentNullException(nameof(section));
            if (charIndex == null)
                throw new ArgumentNullException(nameof(charIndex));

            List<byte> binString = new List<byte>();

            foreach (char c in str)
            {
                uint charValue = (uint)c;

                if (charValue < HistogramAsciiThreshold)
                {
                    // ASCII character - encode directly
                    binString.Add((byte)charValue);
                }
                else
                {
                    // Non-ASCII character - encode using histogram
                    if (!charIndex.TryGetValue(c, out int index))
                    {
                        // AddCharsToHistogram is always run before encoding in WriteAll, so a
                        // character missing from the histogram at this point means the
                        // histogram and the strings being encoded have fallen out of sync -
                        // that's a bug, not something to quietly paper over.
                        throw new ArgumentException(
                            $"Unable to encode character '{c}' (U+{(int)c:X4}) to bytes." + Environment.NewLine +
                            "This character is not present in the histogram at all." + Environment.NewLine +
                            "Expand the histogram with this character before encoding, or the game won't be able to display it either." + Environment.NewLine +
                            $"Full string: {str}",
                            nameof(str));
                    }

                    if (index <= HistogramMaxShiftValue)
                    {
                        // Single byte encoding
                        binString.Add((byte)index);
                    }
                    else
                    {
                        // Multi-byte encoding - find appropriate shift
                        bool shiftFound = false;

                        foreach (int shift in shifts)
                        {
                            int shiftByte = (int)section[shift] << 7;
                            int byteShifted = index - shiftByte;

                            if (byteShifted >= 0 && byteShifted < HistogramAsciiThreshold)
                            {
                                binString.Add((byte)shift);
                                binString.Add((byte)(byteShifted + HistogramAsciiThreshold));
                                shiftFound = true;
                                break;
                            }
                        }

                        if (!shiftFound)
                        {
                            throw new ArgumentException(
                                $"Unable to encode character '{c}' (U+{(int)c:X4}) to bytes." + Environment.NewLine +
                                "The histogram does not contain a valid shift mapping for this character." + Environment.NewLine +
                                "Consider expanding the histogram by adding more characters." + Environment.NewLine +
                                $"Full string: {str}",
                                nameof(str));
                        }
                    }
                }
            }

            // Add null terminator
            binString.Add(0x00);
            return binString.ToArray();
        }

        /// <summary>
        /// Builds a character-to-index lookup for a histogram section, for use with
        /// <see cref="EncodeString(string, List{int}, List{char}, Dictionary{char, int})"/>.
        /// </summary>
        /// <param name="section">The histogram code-point table to index.</param>
        /// <returns>A map from character to the index of its <i>first</i> occurrence in <paramref name="section"/>.</returns>
        /// <remarks>
        /// First-occurrence-wins matters: the histogram legitimately contains repeated
        /// values (e.g. padding zeroes), and the original character-by-character
        /// <c>List.FindIndex</c> lookup this replaces always returned the first match. Using
        /// anything other than first-wins here would change which byte gets emitted for a
        /// repeated character and silently produce a different (though still technically
        /// valid) encoded chunk.
        /// </remarks>
        public static Dictionary<char, int> BuildCharIndex(List<char> section)
        {
            if (section == null)
                throw new ArgumentNullException(nameof(section));

            Dictionary<char, int> charIndex = new Dictionary<char, int>(section.Count);
            for (int i = 0; i < section.Count; i++)
            {
                if (!charIndex.ContainsKey(section[i]))
                    charIndex[section[i]] = i;
            }
            return charIndex;
        }

        /// <summary>
        /// Adds new characters to the histogram section, automatically calculating and adding necessary shifts.
        /// </summary>
        /// <param name="strings">The strings containing characters to add to the histogram.</param>
        /// <param name="dataOffSize">Reference to the histogram data offset size, updated with new character count.</param>
        /// <param name="section">Reference to the histogram, updated with new characters.</param>
        /// <exception cref="ArgumentNullException">Thrown when strings or section is null.</exception>
        /// <exception cref="ArgumentException">Thrown when too many characters are added.</exception>
        public static void AddCharsToHistogram(IEnumerable<string> strings, ref uint dataOffSize, ref List<char> section)
        {
            if (strings == null)
                throw new ArgumentNullException(nameof(strings));
            if (section == null)
                throw new ArgumentNullException(nameof(section));

            // Collect unique characters from all strings
            HashSet<char> charSet = new HashSet<char>();
            foreach (string str in strings)
                charSet.UnionWith(str);

            // Find characters not already in section
            List<char> newChars = charSet.Except(section).ToList();

            if (!newChars.Any())
                return; // No new characters to add

            // Find the shift numbers start index
            int shiftNumsIndex = HistogramShiftNumStartIndex;
            while (shiftNumsIndex < HistogramMaxShiftValue)
            {
                if (section[shiftNumsIndex] != 0x00)
                    break;
                shiftNumsIndex++;
            }

            // Calculate insertion point and initial shift numbers
            int insertedStart = (int)dataOffSize - 1;
            List<char> shiftNums = Enumerable
                .Range(2, (int)section[shiftNumsIndex] + 1)
                .Select(num => (char)num)
                .ToList();
            int shiftNumsCount = shiftNums.Count;

            // Calculate byte positions for new characters
            List<char> CalculateBytePositions(List<char> chars)
            {
                List<char> bytePositions = new List<char>();
                for (int i = 0; i < chars.Count; i++)
                {
                    bytePositions.Add((char)(insertedStart + shiftNumsCount + (shiftNumsIndex - HistogramAsciiThreshold) + i));
                }
                return bytePositions;
            }

            // Calculate required shift numbers based on byte positions
            List<char> CalculateShiftNumsAndMappings(List<char> bytePositions)
            {
                HashSet<char> shiftNumsSet = new HashSet<char>();
                foreach (int bytePos in bytePositions)
                {
                    char shiftNum = (char)(bytePos / HistogramAsciiThreshold);

                    if ((int)shiftNum >= HistogramAsciiThreshold)
                    {
                        throw new ArgumentException("Too many characters to add to histogram. Maximum capacity exceeded.");
                    }

                    shiftNumsSet.Add(shiftNum);
                }
                return shiftNumsSet.OrderBy(x => (int)x).ToList();
            }

            // Iterate until shift numbers stabilize
            while (true)
            {
                List<char> newShiftNums = CalculateShiftNumsAndMappings(CalculateBytePositions(newChars));

                // The number of shift_nums doesn't change - the algorithm ends
                if (newShiftNums.Count == shiftNumsCount)
                {
                    shiftNums = newShiftNums;
                    break;
                }

                // Otherwise, update the number of shift_nums and repeat
                shiftNumsCount = newShiftNums.Count;
            }

            // Reconstruct section with new characters and shift numbers
            List<char> updatedSection = new List<char>();

            // Add original ASCII section
            updatedSection.AddRange(section.Take(HistogramInitialSectionSize));

            // Add calculated shift_nums
            updatedSection.AddRange(shiftNums);

            // Add section from shiftNumsIndex to insertedStart
            updatedSection.AddRange(section.Skip(shiftNumsIndex).Take(insertedStart - shiftNumsIndex));

            // Add new chars
            updatedSection.AddRange(newChars);

            // Add remaining section
            updatedSection.AddRange(section.Skip(insertedStart));

            section = updatedSection;
            dataOffSize += (uint)newChars.Count;
        }

        /// <summary>
        /// Reads and decodes all localized strings from histogram and strings binary chunks.
        /// </summary>
        /// <param name="histogramChunk">The histogram chunk containing character mappings.</param>
        /// <param name="stringsBinaryChunk">The strings binary chunk containing encoded strings.</param>
        /// <returns>Dictionary mapping string hash to decoded strings.</returns>
        /// <exception cref="ArgumentNullException">Thrown when any parameter is null.</exception>
        /// <exception cref="InvalidDataException">Thrown when chunk format is invalid.</exception>
        public static Dictionary<uint, string> ReadStrings(ChunkAssetEntry histogramChunk, ChunkAssetEntry stringsBinaryChunk)
        {
            if (histogramChunk == null)
                throw new ArgumentNullException(nameof(histogramChunk));
            if (stringsBinaryChunk == null)
                throw new ArgumentNullException(nameof(stringsBinaryChunk));

            List<ushort> histogramSection;
            // Read histogram chunk
            using (var stream = App.AssetManager.GetChunk(histogramChunk))
            {
                histogramSection = ReadHistogram(stream);
            }

            // Read strings binary chunk
            using (var stream = App.AssetManager.GetChunk(stringsBinaryChunk))
            {
                return ReadStringsBinary(stream, histogramSection);
            }
        }

        /// <summary>
        /// Reads and decodes all localized strings from histogram and strings binary chunks.
        /// </summary>
        /// <param name="histogramChunk">The histogram chunk containing character mappings.</param>
        /// <param name="stringsBinaryChunk">The strings binary chunk containing encoded strings.</param>
        /// <returns>Dictionary mapping string hash to decoded strings.</returns>
        /// <exception cref="ArgumentNullException">Thrown when any parameter is null.</exception>
        /// <exception cref="InvalidDataException">Thrown when chunk format is invalid.</exception>
        public static Dictionary<uint, string> ReadStrings(Stream histogramChunk, Stream stringsBinaryChunk)
        {
            if (histogramChunk == null)
                throw new ArgumentNullException(nameof(histogramChunk));
            if (stringsBinaryChunk == null)
                throw new ArgumentNullException(nameof(stringsBinaryChunk));

            // Read histogram chunk
            List<ushort> histogramSection = ReadHistogram(histogramChunk);

            // Read strings binary chunk
            return ReadStringsBinary(stringsBinaryChunk, histogramSection);
        }

        /// <summary>
        /// Reads the histogram section from a chunk.
        /// </summary>
        private static List<ushort> ReadHistogram(Stream histogramChunk)
        {
            List<ushort> histogramSection = new List<ushort>();

            using (NativeReader reader = new NativeReader(histogramChunk))
            {
                uint magic = reader.ReadUInt();
                if (magic != HistogramMagic)
                    throw new InvalidDataException($"Magic failed, invalid histogram chunk. Got {magic:X}.");

                uint fileSize = reader.ReadUInt();
                reader.ReadUInt(); // dataOffSize

                long sizeToRead = (fileSize + 8 - reader.Position) / 2;
                for (int i = 0; i < sizeToRead; i++)
                {
                    histogramSection.Add(reader.ReadUShort());
                }
            }

            return histogramSection;
        }

        /// <summary>
        /// Reads and decodes strings from the strings binary chunk.
        /// </summary>
        private static Dictionary<uint, string> ReadStringsBinary(Stream stringsBinaryChunk, List<ushort> histogramSection)
        {
            Dictionary<uint, string> stringList = new Dictionary<uint, string>();

            using (NativeReader reader = new NativeReader(stringsBinaryChunk))
            {
                // Read and validate header
                uint magic = reader.ReadUInt();
                if (magic != StringMagic)
                    throw new InvalidDataException($"Magic failed, invalid strings binary chunk. Got {magic:X}.");

                reader.ReadUInt(); // fileSize
                reader.ReadUInt(); // listSize
                uint dataOffset = reader.ReadUInt();
                uint stringsOffset = reader.ReadUInt();

                reader.ReadNullTerminatedString(); // section name

                // Read hash-offset pairs
                List<ValueTuple<uint, uint>> hashPairList = new List<ValueTuple<uint, uint>>();
                reader.Position = dataOffset + 8;

                while (reader.Position != stringsOffset + 8)
                {
                    uint hash = reader.ReadUInt();
                    uint offset = reader.ReadUInt();
                    hashPairList.Add(new ValueTuple<uint, uint>(hash, offset));
                }

                // Decode strings using histogram
                foreach (ValueTuple<uint, uint> hashPair in hashPairList)
                {
                    reader.Position = stringsOffset + hashPair.Item2 + 8;
                    string binString = reader.ReadNullTerminatedString();

                    if (!stringList.ContainsKey(hashPair.Item1))
                    {
                        stringList.Add(hashPair.Item1, DecodeString(binString, histogramSection));
                    }
                }
            }

            return stringList;
        }

        /// <summary>
        /// Writes updated histogram and strings binary data, merging modified strings with existing data.
        /// </summary>
        /// <param name="am">The AssetManager to use.</param>
        /// <param name="histogramChunk">The original histogram chunk.</param>
        /// <param name="stringsBinaryChunk">The original strings binary chunk.</param>
        /// <param name="modifiedData">Dictionary of modified strings to merge.</param>
        /// <param name="stringToRemove">Hashes of strings to remove. Not supported by the original FsLocalizationPlugin.</param>
        /// <param name="newHistogramData">Output parameter containing the new histogram chunk data.</param>
        /// <param name="newStringData">Output parameter containing the new strings binary chunk data.</param>
        /// <exception cref="ArgumentNullException">Thrown when any parameter is null.</exception>
        /// <exception cref="InvalidDataException">Thrown when chunk format is invalid.</exception>
        public static void WriteAll(AssetManager am, ChunkAssetEntry histogramChunk, ChunkAssetEntry stringsBinaryChunk, Dictionary<uint, string> modifiedData, IEnumerable<uint> stringToRemove, out byte[] newHistogramData, out byte[] newStringData)
        {
            if (histogramChunk == null)
                throw new ArgumentNullException(nameof(histogramChunk));
            if (stringsBinaryChunk == null)
                throw new ArgumentNullException(nameof(stringsBinaryChunk));
            if (modifiedData == null)
                throw new ArgumentNullException(nameof(modifiedData));
            if (stringToRemove == null)
                throw new ArgumentNullException(nameof(stringToRemove));

            // Read histogram chunk
            uint histogramDataOffSize;
            List<char> histogramSection = new List<char>();

            using (NativeReader reader = new NativeReader(am.GetChunk(histogramChunk)))
            {
                uint histogramMagic = reader.ReadUInt();
                if (histogramMagic != HistogramMagic)
                    throw new InvalidDataException($"Magic failed, invalid histogram chunk. Got {histogramMagic:X}.");

                uint histogramFileSize = reader.ReadUInt();
                histogramDataOffSize = reader.ReadUInt();

                long sizeToRead = (histogramFileSize + 8 - reader.Position) / 2;
                for (int i = 0; i < sizeToRead; i++)
                {
                    histogramSection.Add((char)reader.ReadUShort());
                }
            }

            // Read strings binary chunk
            string stringSection;
            Dictionary<uint, string> stringList = new Dictionary<uint, string>();

            using (NativeReader reader = new NativeReader(am.GetChunk(stringsBinaryChunk)))
            {
                // Read and validate header
                uint stringMagic = reader.ReadUInt();
                if (stringMagic != StringMagic)
                    throw new InvalidDataException($"Magic failed, invalid strings binary chunk. Got {stringMagic:X}.");

                reader.ReadUInt(); // fileSize
                reader.ReadUInt(); // listSize
                uint stringDataOffset = reader.ReadUInt();
                uint stringStringsOffset = reader.ReadUInt();

                stringSection = reader.ReadNullTerminatedString();

                // Read hash-offset pairs
                List<ValueTuple<uint, uint>> hashPairList = new List<ValueTuple<uint, uint>>();
                reader.Position = stringDataOffset + 8;

                while (reader.Position != stringStringsOffset + 8)
                {
                    hashPairList.Add(new ValueTuple<uint, uint>(reader.ReadUInt(), reader.ReadUInt()));
                }

                // Decode existing strings
                foreach (ValueTuple<uint, uint> hashPair in hashPairList)
                {
                    reader.Position = stringStringsOffset + hashPair.Item2 + 8;
                    string binString = reader.ReadNullTerminatedString();

                    if (!stringList.ContainsKey(hashPair.Item1))
                    {
                        stringList.Add(hashPair.Item1, DecodeString(binString, histogramSection.Select(c => (ushort)c).ToList()));
                    }
                }
            }

            // Merge modified strings with existing strings
            foreach (KeyValuePair<uint, string> data in modifiedData)
            {
                stringList[data.Key] = data.Value;
            }
            foreach (uint id in stringToRemove)
            {
                stringList.Remove(id);
            }

            // Add new characters to histogram
            AddCharsToHistogram(stringList.Values, ref histogramDataOffSize, ref histogramSection);

            // Write histogram chunk
            newHistogramData = WriteHistogramChunk(histogramDataOffSize, histogramSection);

            // Write string chunk
            newStringData = WriteStringChunk(stringSection, stringList, histogramSection);
        }

        /// <summary>
        /// Writes the histogram chunk data.
        /// </summary>
        private static byte[] WriteHistogramChunk(uint dataOffSize, List<char> section)
        {
            using (NativeWriter writer = new NativeWriter(new MemoryStream()))
            {
                writer.Write(HistogramMagic);
                writer.Write(0xDEADBEEF); // Placeholder for file size
                writer.Write(dataOffSize);

                foreach (char c in section)
                {
                    writer.Write((ushort)c);
                }

                // Update file size
                uint fileSize = (uint)(writer.Position - 8);
                writer.Position = 4;
                writer.Write(fileSize);

                return writer.ToByteArray();
            }
        }

        /// <summary>
        /// Writes the strings binary chunk data.
        /// </summary>
        private static byte[] WriteStringChunk(string section, Dictionary<uint, string> stringList, List<char> histogramSection)
        {
            using (NativeWriter writer = new NativeWriter(new MemoryStream()))
            {
                // Calculate offsets
                uint listSize = (uint)stringList.Count;
                uint dataOffset = StringDefaultDataOffset;
                uint stringsOffset = dataOffset + (listSize * StringHashPairSize);

                // Write header
                writer.Write(StringMagic);
                writer.Write(0xDEADBEEF); // Placeholder for file size
                writer.Write(listSize);
                writer.Write(dataOffset);
                writer.Write(stringsOffset);
                writer.WriteNullTerminatedString(section);

                // Write padding to reach data offset
                while (writer.Position < dataOffset + 8)
                    writer.Write((byte)0x00);

                // Get histogram shifts for encoding
                List<int> histogramShifts = new List<int>();
                for (int i = HistogramShiftEndIndex; i >= HistogramAsciiThreshold; i--)
                {
                    if (histogramSection[i] < HistogramAsciiThreshold)
                    {
                        histogramShifts.Add(i);
                    }
                }

                // Build the character lookup once for this whole batch of strings, rather than
                // linearly scanning the histogram section for every non-ASCII character of
                // every string - with a large database and a grown histogram, that scan used
                // to dominate mod-bake time.
                Dictionary<char, int> charIndex = BuildCharIndex(histogramSection);

                // Write hash pairs and encode strings
                using (NativeWriter stringBuffer = new NativeWriter(new MemoryStream()))
                {
                    foreach (KeyValuePair<uint, string> keyValuePair in stringList)
                    {
                        writer.Write(keyValuePair.Key);
                        writer.Write((uint)stringBuffer.Position);
                        stringBuffer.Write(EncodeString(keyValuePair.Value, histogramShifts, histogramSection, charIndex));
                    }

                    // Write all encoded strings
                    writer.Write(stringBuffer.ToByteArray());
                }

                // Update file size
                uint fileSize = (uint)(writer.Position - 8);
                writer.Position = 4;
                writer.Write(fileSize);

                return writer.ToByteArray();
            }
        }
    }
}
