using Frosty.Core;
using FrostySdk.IO;
using FrostySdk.Managers;
using FsLocalizationPlugin.Helpers;
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
    /// Reads and writes the histogram chunk and strings-binary chunk that back a game's
    /// localized text.
    /// </summary>
    /// <remarks>
    /// Histogram chunk (magic <c>0x00039001</c>): magic, fileSize, dataOffSize, then a flat
    /// table of ushort code points (first 128 = ASCII identity, rest grown by
    /// <see cref="AddCharsToHistogram"/>).
    /// <para/>
    /// Strings binary chunk (magic <c>0x00039000</c>): magic, fileSize, listSize, dataOffset
    /// (fixed at <see cref="StringDefaultDataOffset"/>), stringsOffset, a null-terminated
    /// section name, padding to dataOffset, then listSize (hash, offset) pairs, then the
    /// histogram-encoded null-terminated string data.
    /// </remarks>
    public class Flammen
    {
        private const uint StringMagic = 0x00039000;
        private const uint StringDefaultDataOffset = 0x8C;
        private const uint StringHashPairSize = 8;

        private const uint HistogramMagic = 0x00039001;
        private const int HistogramAsciiThreshold = 0x80;
        private const int HistogramMaxShiftValue = 0xFF;
        private const int HistogramInitialSectionSize = 0x80;
        private const int HistogramShiftNumStartIndex = 0x40;
        private const int HistogramShiftEndIndex = 0x1FE;

        /// <summary>Decodes a histogram-encoded binary string into Unicode.</summary>
        /// <param name="binString">The raw, histogram-encoded bytes (as a string of chars 0-255).</param>
        /// <param name="section">The histogram code-point table to decode against.</param>
        /// <remarks>
        /// Bytes &lt; 0x80 pass through as ASCII. Bytes &gt;= 0x80 look up the histogram: if
        /// the mapped value is itself &gt;= 0x80 it's used directly, otherwise it's a shift
        /// number combined with the next byte.
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
                    // ASCII character: Decode directly
                    sb.Append((char)currentByte);
                }
                else
                {
                    // Non-ASCII character: Decode using histogram
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

        /// <summary>Encodes a string into histogram-encoded bytes, building a fresh char-to-index lookup first.</summary>
        /// <remarks>
        /// For encoding many strings against the same histogram (as <see cref="WriteAll"/>
        /// does), build the lookup once with <see cref="BuildCharIndex"/> and call the
        /// 4-argument overload directly instead.
        /// </remarks>
        public static byte[] EncodeString(string str, List<int> shifts, List<char> section)
        {
            if (section == null)
                throw new ArgumentNullException(nameof(section));

            return EncodeString(str, shifts, section, BuildCharIndex(section));
        }

        /// <summary>Encodes a string into histogram-encoded bytes using a precomputed char-to-index lookup.</summary>
        /// <param name="charIndex">From <see cref="BuildCharIndex"/>.</param>
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
                        // AddCharsToHistogram always runs first in WriteAll,
                        // so reaching here means the histogram and strings fell out of sync.
                        throw new ArgumentException(
                            $"Temporal dislocation! Unable to encode character '{c}' (U+{(int)c:X4}) to bytes." + Environment.NewLine +
                            "This character is not present in the histogram." + Environment.NewLine +
                            "Consider expanding the histogram by adding more characters, or report this bug on GitHub." + Environment.NewLine +
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
                        // Multi-byte encoding: Find appropriate shift
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
                                $"No kindling found! Unable to encode character '{c}' (U+{(int)c:X4}) to bytes." + Environment.NewLine +
                                "The histogram does not contain a valid shift mapping for this character." + Environment.NewLine +
                                "Consider expanding the histogram by adding more characters, or report this bug on GitHub." + Environment.NewLine +
                                $"Full string: {str}",
                                nameof(str));
                        }
                    }
                }
            }

            binString.Add(0x00);
            return binString.ToArray();
        }

        /// <summary>Builds a char-to-index lookup for a histogram section.</summary>
        /// <returns>Character to the index of its <i>first</i> occurrence in <paramref name="section"/>.</returns>
        /// <remarks>
        /// First-occurrence-wins matters: the histogram has repeated values (e.g. padding
        /// zeroes), and the old per-char <c>List.FindIndex</c> lookup always took the first
        /// match. Anything else here would change which byte gets emitted for a repeated
        /// character.
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

        /// <summary>Grows the histogram with any characters from <paramref name="strings"/> it doesn't already have, adding shifts as needed.</summary>
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
                        throw new ArgumentException("Crucible overloaded! Too many characters to add to histogram. Maximum capacity exceeded.");
                    }

                    shiftNumsSet.Add(shiftNum);
                }
                return shiftNumsSet.OrderBy(x => (int)x).ToList();
            }

            // Iterate until shift numbers stabilize
            while (true)
            {
                List<char> newShiftNums = CalculateShiftNumsAndMappings(CalculateBytePositions(newChars));

                // The number of shift_nums doesn't change. The algorithm ends
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

            DebugLogHelper.Log("Flammen.AddCharsToHistogram", "Added {0} new characters to histogram, original dataOffSize: {1}, new dataOffSize: {2}", newChars.Count, dataOffSize, dataOffSize + (uint)newChars.Count);

            section = updatedSection;
            dataOffSize += (uint)newChars.Count;
        }

        /// <summary>Reads and decodes all localized strings from a histogram and strings-binary chunk.</summary>
        public static Dictionary<uint, string> ReadStrings(ChunkAssetEntry histogramChunk, ChunkAssetEntry stringsBinaryChunk)
        {
            if (histogramChunk == null)
                throw new ArgumentNullException(nameof(histogramChunk));
            if (stringsBinaryChunk == null)
                throw new ArgumentNullException(nameof(stringsBinaryChunk));

            DebugLogHelper.Log("Flammen.ReadStrings", "Read histogram and strings from {0}(Histogram) and {1}(String Binary)", histogramChunk.Id, stringsBinaryChunk.Id);
            
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

        /// <summary>Reads and decodes all localized strings from a histogram and strings-binary chunk.</summary>
        public static Dictionary<uint, string> ReadStrings(Stream histogramChunk, Stream stringsBinaryChunk)
        {
            if (histogramChunk == null)
                throw new ArgumentNullException(nameof(histogramChunk));
            if (stringsBinaryChunk == null)
                throw new ArgumentNullException(nameof(stringsBinaryChunk));

            DebugLogHelper.Log("Flammen.ReadStrings", "Read histogram and strings from stream");

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
                    throw new InvalidDataException($"Spell fizzled, invalid histogram magic signature. Got {magic:X}.");

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
                    throw new InvalidDataException($"Spell fizzled! Invalid strings binary chunk magic signature. Got {magic:X}.");

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

        /// <summary>Merges modified/removed strings into the existing chunks and re-emits both.</summary>
        /// <param name="stringToRemove">Hashes to remove. Not supported by the original FsLocalizationPlugin.</param>
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
                    throw new InvalidDataException($"Spell fizzled! Invalid histogram magic signature. Got {histogramMagic:X}.");

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
                    throw new InvalidDataException($"Spell fizzled! Invalid strings binary magic signature. Got {stringMagic:X}.");

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

            DebugLogHelper.Log("Flammen.WriteAll", "Readed histogram and {0} strings", stringList.Count);

            // Merge modified strings with existing strings
            foreach (KeyValuePair<uint, string> data in modifiedData)
            {
                stringList[data.Key] = data.Value;
            }
            DebugLogHelper.Log("Flammen.WriteAll", "Modified {0} strings", modifiedData.Count);
            foreach (uint id in stringToRemove)
            {
                stringList.Remove(id);
            }
            DebugLogHelper.Log("Flammen.WriteAll", "Removed {0} strings", stringToRemove.Count());

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
                writer.Write(0xDEADBEEF); // Placeholder file size
                writer.Write(dataOffSize);

                foreach (char c in section)
                {
                    writer.Write((ushort)c);
                }

                // Update file size
                uint fileSize = (uint)(writer.Position - 8);

                DebugLogHelper.Log("Flammen.WriteHistogramChunk", "Finish writting histogram chunk, chunk size: {0}", writer.Position);

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
                writer.Write(0xDEADBEEF); // Placeholder file size
                writer.Write(listSize);
                writer.Write(dataOffset);
                writer.Write(stringsOffset);
                writer.WriteNullTerminatedString(section);

                // Write padding to reach data offset
                while (writer.Position < dataOffset + 8)
                    writer.Write((byte)0x00);

                // Get histogram shifts for encoding
                List<int> histogramShifts = new List<int>();
                // Use >= instead of > HistogramAsciiThreshold
                // This fixes the bug that adding too little characters
                // to the histogram or not adding any new characters will result:
                // "The histogram does not contain a valid shift mapping for this character."
                // Learn more: https://github.com/shoushou1106/FrostyFlammenwerferPlugin/pull/4
                for (int i = HistogramShiftEndIndex; i >= HistogramAsciiThreshold; i--)
                {
                    if (histogramSection[i] < HistogramAsciiThreshold)
                    {
                        histogramShifts.Add(i);
                    }
                }

                // Built once for the whole batch.
                // A per-char linear scan here used to dominate handler apply time.
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

                DebugLogHelper.Log("Flammen.WriteStringChunk", "Finish writting string chunk, chunk size: {0}", writer.Position);

                writer.Position = 4;
                writer.Write(fileSize);

                return writer.ToByteArray();
            }
        }
    }
}
