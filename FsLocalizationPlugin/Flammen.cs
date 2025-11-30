using Frosty.Core;
using FrostySdk.IO;
using FrostySdk.Managers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

//using FrostySdk.Managers.Entries; // Uncomment this line [For 1.0.7]

namespace FsLocalizationPlugin.Flammen
{
    /// <summary>
    /// Provides functionality for encoding and decoding localized strings using histogram-based compression.
    /// </summary>
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
        /// Decodes binary string using histogram
        /// </summary>
        /// <param name="binString">String to decode</param>
        /// <param name="section">Histogram</param>
        /// <returns>The decoded string.</returns>
        /// <exception cref="ArgumentNullException">Thrown when binString or section is null</exception>
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
        /// Encodes binary string using histogram
        /// </summary>
        /// <param name="str">String to encode</param>
        /// <param name="shifts">The list of shift indices for multi-byte character encoding.</param>
        /// <param name="section">Histogram</param>
        /// <returns>The encoded byte array with null terminator.</returns>
        /// <exception cref="ArgumentNullException">Thrown when any parameter is null.</exception>
        /// <exception cref="ArgumentException">Thrown when a character cannot be encoded.</exception>
        public static byte[] EncodeString(string str, List<int> shifts, List<char> section)
        {
            if (str == null)
                throw new ArgumentNullException(nameof(str));
            if (shifts == null)
                throw new ArgumentNullException(nameof(shifts));
            if (section == null)
                throw new ArgumentNullException(nameof(section));

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
                    int index = section.FindIndex(a => a.Equals(c));
                    if (index == -1)
                        continue;

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
                                $"Full String: {str}",
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
                    throw new InvalidDataException($"Magic failed, invalid histogram chunk. Got {magic.ToString("X")}.");

                uint fileSize = reader.ReadUInt();
                uint dataOffSize = reader.ReadUInt();

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
                    throw new InvalidDataException($"Magic failed, invalid strings binary chunk. Got {magic.ToString("X")}.");

                uint fileSize = reader.ReadUInt();
                uint listSize = reader.ReadUInt();
                uint dataOffset = reader.ReadUInt();
                uint stringsOffset = reader.ReadUInt();

                string section = reader.ReadNullTerminatedString();

                // Read hash-offset pairs
                List<Tuple<uint, uint>> hashPairList = new List<Tuple<uint, uint>>();
                reader.Position = dataOffset + 8;

                while (reader.Position != stringsOffset + 8)
                {
                    uint hash = reader.ReadUInt();
                    uint offset = reader.ReadUInt();
                    hashPairList.Add(new Tuple<uint, uint>(hash, offset));
                }

                // Decode strings using histogram
                foreach (Tuple<uint, uint> hashPair in hashPairList)
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
        /// <param name="histogramChunk">The original histogram chunk.</param>
        /// <param name="stringsBinaryChunk">The original strings binary chunk.</param>
        /// <param name="modifiedData">Dictionary of modified strings to merge.</param>
        /// <param name="newHistogramData">Output parameter containing the new histogram chunk data.</param>
        /// <param name="newStringData">Output parameter containing the new strings binary chunk data.</param>
        /// <exception cref="ArgumentNullException">Thrown when any parameter is null.</exception>
        /// <exception cref="InvalidDataException">Thrown when chunk format is invalid.</exception>
        public static void WriteAll(AssetManager am, ChunkAssetEntry histogramChunk, ChunkAssetEntry stringsBinaryChunk, Dictionary<uint, string> modifiedData, List<uint> stringToRemove, out byte[] newHistogramData, out byte[] newStringData)
        {
            if (histogramChunk == null)
                throw new ArgumentNullException(nameof(histogramChunk));
            if (stringsBinaryChunk == null)
                throw new ArgumentNullException(nameof(stringsBinaryChunk));
            if (modifiedData == null)
                throw new ArgumentNullException(nameof(modifiedData));

            // Read histogram chunk
            uint histogramMagic;
            uint histogramFileSize;
            uint histogramDataOffSize;
            List<char> histogramSection = new List<char>();

            using (NativeReader reader = new NativeReader(am.GetChunk(histogramChunk)))
            {
                histogramMagic = reader.ReadUInt();
                if (histogramMagic != HistogramMagic)
                    throw new InvalidDataException($"Magic failed, invalid histogram chunk. Got {histogramMagic.ToString("X")}.");

                histogramFileSize = reader.ReadUInt();
                histogramDataOffSize = reader.ReadUInt();

                long sizeToRead = (histogramFileSize + 8 - reader.Position) / 2;
                for (int i = 0; i < sizeToRead; i++)
                {
                    histogramSection.Add((char)reader.ReadUShort());
                }
            }

            // Read strings binary chunk
            uint stringMagic;
            uint stringFileSize;
            uint stringListSize;
            uint stringDataOffset;
            uint stringStringsOffset;
            string stringSection;
            Dictionary<uint, string> stringList = new Dictionary<uint, string>();

            using (NativeReader reader = new NativeReader(am.GetChunk(stringsBinaryChunk)))
            {
                // Read and validate header
                stringMagic = reader.ReadUInt();
                if (stringMagic != StringMagic)
                    throw new InvalidDataException($"Magic failed, invalid strings binary chunk. Got {stringMagic.ToString("X")}.");

                stringFileSize = reader.ReadUInt();
                stringListSize = reader.ReadUInt();
                stringDataOffset = reader.ReadUInt();
                stringStringsOffset = reader.ReadUInt();

                stringSection = reader.ReadNullTerminatedString();

                // Read hash-offset pairs
                List<Tuple<uint, uint>> hashPairList = new List<Tuple<uint, uint>>();
                reader.Position = stringDataOffset + 8;

                while (reader.Position != stringStringsOffset + 8)
                {
                    hashPairList.Add(new Tuple<uint, uint>(reader.ReadUInt(), reader.ReadUInt()));
                }

                // Decode existing strings
                foreach (Tuple<uint, uint> hashPair in hashPairList)
                {
                    reader.Position = stringStringsOffset + hashPair.Item2 + 8;
                    string binString = reader.ReadNullTerminatedString();

                    if (!stringList.ContainsKey(hashPair.Item1))
                    {
                        stringList.Add(hashPair.Item1, DecodeString(binString, histogramSection.Select(c => (ushort)c).ToList()));
                    }
                }
            }

            // Add new characters to histogram
            AddCharsToHistogram(modifiedData.Values, ref histogramDataOffSize, ref histogramSection);

            // Merge modified strings with existing strings
            foreach (KeyValuePair<uint, string> data in modifiedData)
            {
                stringList[data.Key] = data.Value;
            }
            foreach (uint id in stringToRemove)
            {
                stringList.Remove(id);
            }
            stringList.OrderBy(pair => pair.Key);

            // Write histogram chunk
            newHistogramData = WriteHistogramChunk(histogramDataOffSize, histogramSection, out histogramFileSize);

            // Write string chunk
            newStringData = WriteStringChunk(stringSection, stringList, histogramSection);
        }

        /// <summary>
        /// Writes the histogram chunk data.
        /// </summary>
        private static byte[] WriteHistogramChunk(uint dataOffSize, List<char> section, out uint fileSize)
        {
            using (NativeWriter writer = new NativeWriter(new MemoryStream()))
            {
                writer.Write(HistogramMagic);
                writer.Write(0xDEADBEEF); // Placeholder for file size
                writer.Write(dataOffSize);

                foreach (char c in section)
                {
                    ushort charCode = (ushort)c;
                    writer.Write(charCode);
                }

                // Update file size
                fileSize = (uint)(writer.Position - 8);
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

                // Write hash pairs and encode strings
                using (NativeWriter stringBuffer = new NativeWriter(new MemoryStream()))
                {
                    foreach (KeyValuePair<uint, string> keyValuePair in stringList)
                    {
                        writer.Write(keyValuePair.Key);
                        writer.Write((uint)stringBuffer.Position);
                        stringBuffer.Write(EncodeString(keyValuePair.Value, histogramShifts, histogramSection));
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
