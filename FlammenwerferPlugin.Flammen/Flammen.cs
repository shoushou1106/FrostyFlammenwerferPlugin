
using Frosty.Controls;
using Frosty.Core;
using Frosty.Core.Controls;
using Frosty.Core.Sdk.AnthemDemo;
using FrostySdk.IO;
using FrostySdk.Managers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.ConstrainedExecution;
using System.Text;
using System.Windows.Input;

namespace FlammenwerferPlugin.Flammen
{
    public class Flammen
    {
        /// <summary>
        /// Decode a binary string using the histogram.
        /// </summary>
        /// <param name="binString">The string to decode.</param>
        /// <param name="section">The histogram section.</param>
        /// <returns>The decoded string.</returns>
        public static string DecodeString(string binString, List<ushort> section)
        {
            int index = 0;
            StringBuilder sb = new StringBuilder();

            while (index < binString.Length)
            {
                byte _byte = (byte)binString[index];
                if (_byte < 0x80)
                {
                    // ASCII
                    sb.Append((char)_byte);
                }
                else
                {
                    // Not ASCII
                    ushort tmp = section[_byte];
                    if (tmp >= 0x80)
                    {
                        sb.Append((char)tmp);
                    }
                    else
                    {
                        ++index;
                        _byte = (byte)binString[index];
                        if (_byte >= 0x80)
                        {
                            sb.Append((char)section[(_byte - 0x80) + (tmp << 7)]);
                        }
                    }
                }
                index++;
            }
            return sb.ToString();
        }

        public static byte[] EncodeString(string str, List<int> shifts, List<char> section)
        {
            List<byte> binString = new List<byte>();

            foreach (char c in str)
            {
                bool checkShift = false;
                uint ordTmp = (uint)c;
                
                if (ordTmp < 0x80)
                {
                    // ASCII
                    binString.Add((byte)ordTmp); // unsigned char
                }
                else
                {
                    // Not ASCII
                    int index = section.FindIndex((char a) => { return a.Equals(c); });
                    if (index == -1)
                        continue;

                    if (index <= 0xFF)
                    {
                        binString.Add((byte)index);
                    }
                    else
                    {
                        // Try to find a proper shift to fit the byte into range
                        foreach (int shift in shifts)
                        {
                            int shiftByte = ((int)section[shift]) << 7;
                            int byteShifted = index - shiftByte;
                            if (byteShifted < 0x80 && byteShifted >= 0)
                            {
                                binString.Add((byte)shift);
                                binString.Add((byte)(byteShifted + 0x80));
                                checkShift = true;
                                break;
                            }
                        }

                        if (!checkShift) 
                        {
                            // If this happens, it means that there is no proper shift to fit the character into range.
                            // You may want to expand the shift range by calling `Histogram.expand_shift_range` method first.
                            //
                            // _btw idk what is `Histogram.expand_shift_range`, its not exists in the original python code._

                            throw new ArgumentException($"Unable to encode character {c} to bytes.");
                        }
                    }
                }
            }

            binString.Add(0x00);
            return binString.ToArray();
        }

        public static void addCharsToHistogram(List<char> chars, ref uint dataOffSize, ref List<ushort> section)
        {
            int shiftNumsIndex = 0x40;
            while (shiftNumsIndex < 0xFF)
            {
                if (section[shiftNumsIndex] != 0x00)
                    break;
                shiftNumsIndex++;
            }

            uint insertedStart = dataOffSize - 1;

            List<char> shiftNums = Enumerable
                .Range(2, (int)section[shiftNumsIndex] + 1)
                .Select(num => ((char)num))
                .ToList();
            int shiftNumsCount = shiftNums.Count;

            while (true)
            {
                // calculate_byte_positions
                List<int> bytePositions = new List<int>();
                for (int i = 0; i < chars.Count; i++)
                {
                    bytePositions.Add((int)insertedStart + shiftNumsCount + (shiftNumsIndex - 0x80) + i);
                }

                // calculate_shift_nums_and_mappings
                HashSet<char> shiftNumsSet = new HashSet<char>();
                foreach (int @byte in bytePositions)
                {
                    char shiftNum = (char)(@byte / 0x80);

                    if ((int)shiftNum >= 0x80)
                    {
                        throw new ArgumentException("Too many characters");
                    }
                    if (!shiftNumsSet.Contains(shiftNum))
                    {
                        shiftNumsSet.Add(shiftNum);
                    }
                }
                List<char> newShiftNums = shiftNumsSet.OrderBy(x => (int)x).ToList();

                if (newShiftNums.Count == shiftNumsCount)
                {
                    shiftNums = newShiftNums;
                    break;
                }

                shiftNumsCount = newShiftNums.Count;
            }


            // Create a new list to hold the updated section
            List<ushort> newSection = new List<ushort>();

            // Add the first 0x80 elements from the original section
            newSection.AddRange(section.Take(0x80));

            // Add the shift_nums elements
            newSection.AddRange(shiftNums.Select(c => (ushort)c));

            // Add the elements from shiftNumsIndex to insertedStart
            newSection.AddRange(section.Skip(shiftNumsIndex).Take((int)insertedStart - shiftNumsIndex));

            // Add the chars elements
            newSection.AddRange(chars.Select(c => (ushort)c));

            // Add the remaining elements from insertedStart to the end
            newSection.AddRange(section.Skip((int)insertedStart));

            // Update the section with the new list
            section = newSection;

            // Update the dataOffSize
            dataOffSize += (uint)chars.Count;
        }

        public static Dictionary<uint, string> ReadStrings(ChunkAssetEntry histogramChunk, ChunkAssetEntry stringsBinaryChunk)
        {
            // Read histogram chunk
            List<ushort> histogramSection = new List<ushort>();
            using (NativeReader reader = new NativeReader(App.AssetManager.GetChunk(histogramChunk)))
            {
                uint magic = reader.ReadUInt();
                if (magic != 0x00039001)
                    throw new InvalidDataException("Invalid histogram chunk.");

                uint fileSize = reader.ReadUInt();
                uint dataOffSize = reader.ReadUInt();

                long sizeToRead = ((fileSize + 8 - reader.Position) / 2); // Calculate first
                for (int i = 0; i < sizeToRead; i++)
                {
                    histogramSection.Add(reader.ReadUShort());
                }
            }

            // Read strings binary chunk
            Dictionary<uint, string> stringList = new Dictionary<uint, string>();
            using (NativeReader reader = new NativeReader(App.AssetManager.GetChunk(stringsBinaryChunk)))
            {
                // Header
                uint magic = reader.ReadUInt();
                if (magic != 0x00039000)
                    throw new InvalidDataException("Invalid strings binary chunk.");

                uint fileSize = reader.ReadUInt();
                uint listSize = reader.ReadUInt();
                uint dataOffset = reader.ReadUInt();
                uint stringsOffset = reader.ReadUInt();

                string section = reader.ReadNullTerminatedString();

                // Read hash pairs
                List<Tuple<uint, uint>> hashPairList = new List<Tuple<uint, uint>>();
                reader.Position = dataOffset + 8;
                while (reader.Position != stringsOffset + 8)
                {
                    hashPairList.Add(new Tuple<uint, uint>(reader.ReadUInt(), reader.ReadUInt()));
                }

                // Read strings by locating offsets
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

        public static void WriteAll(ChunkAssetEntry histogramChunk, ChunkAssetEntry stringsBinaryChunk, Dictionary<uint, string> modifiedData, out byte[] newHistogramData, out byte[] newStringData)
        {
            // Read histogram chunk
            uint histogramMagic;
            uint histogramFileSize;
            uint histogramDataOffSize;
            List<ushort> histogramSection = new List<ushort>();
            using (NativeReader reader = new NativeReader(App.AssetManager.GetChunk(histogramChunk)))
            {
                histogramMagic = reader.ReadUInt();
                if (histogramMagic != 0x00039001)
                    throw new InvalidDataException("Invalid histogram chunk.");

                histogramFileSize = reader.ReadUInt();
                histogramDataOffSize = reader.ReadUInt();

                long sizeToRead = ((histogramFileSize + 8 - reader.Position) / 2); // Calculate first
                
                for (int i = 0; i < sizeToRead; i++)
                {
                    histogramSection.Add(reader.ReadUShort());
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
            using (NativeReader reader = new NativeReader(App.AssetManager.GetChunk(stringsBinaryChunk)))
            {
                // Header
                stringMagic = reader.ReadUInt();
                if (stringMagic != 0x00039000)
                    throw new InvalidDataException("Invalid strings binary chunk.");

                stringFileSize = reader.ReadUInt();
                stringListSize = reader.ReadUInt();
                stringDataOffset = reader.ReadUInt();
                stringStringsOffset = reader.ReadUInt();

                stringSection = reader.ReadNullTerminatedString();

                // Read hash pairs
                List<Tuple<uint, uint>> hashPairList = new List<Tuple<uint, uint>>();
                reader.Position = stringDataOffset + 8;
                while (reader.Position != stringStringsOffset + 8)
                {
                    hashPairList.Add(new Tuple<uint, uint>(reader.ReadUInt(), reader.ReadUInt()));
                }

                // Read strings by locating offsets
                foreach (Tuple<uint, uint> hashPair in hashPairList)
                {
                    reader.Position = stringStringsOffset + hashPair.Item2 + 8;
                    string binString = reader.ReadNullTerminatedString();
                    if (!stringList.ContainsKey(hashPair.Item1))
                    {
                        stringList.Add(hashPair.Item1, DecodeString(binString, histogramSection));
                    }
                }
            }

            // Merge new strings
            foreach (KeyValuePair<uint, string> data in modifiedData)
            {
                if (stringList.ContainsKey(data.Key))
                {
                    stringList[data.Key] = data.Value;
                }
                else
                {
                    stringList.Append(data);
                }
            }
            stringList.OrderBy(pair => pair.Key);

            // Find chars to add
            List<char> unsupportedChars = new List<char>();
            foreach (KeyValuePair<uint, string> str in stringList)
            {
                foreach (char c in str.Value.ToCharArray())
                {
                    if (!(c < 0x80))
                    {
                        if (c > 0xFFFF)
                        {
                            App.Logger.LogError("Character not supported: \"" + c + "\" from string " + str.Key.ToString("X"));
                        }
                        else
                        {
                            int index = histogramSection.FindIndex(a => { return ((char)a).Equals(c); });
                            if (index == -1)
                            {
                                unsupportedChars.Add(c);
                                continue;
                            }
                        }
                    }
                }
            }

            // Add chars to histogram
            addCharsToHistogram(unsupportedChars, ref histogramDataOffSize, ref histogramSection);

            // Write histogram chunk
            using (NativeWriter writer = new NativeWriter(new MemoryStream()))
            {
                writer.Write(histogramMagic);
                writer.Write(0xDEADBEEF);
                writer.Write(histogramDataOffSize);

                foreach (ushort c in histogramSection)
                {
                    if (c <= 0xFFFF)
                    {
                        writer.Write(c);
                    }
                    else
                    {
                        histogramSection.Remove(c);
                    }
                }

                histogramFileSize = (uint)(writer.Position - 8);
                writer.Position = 4;
                writer.Write(histogramFileSize);

                newHistogramData = writer.ToByteArray();
            }

            // Write string chunk
            using (NativeWriter writer = new NativeWriter(new MemoryStream()))
            {
                // Header
                stringListSize = (uint)stringList.Count();
                stringDataOffset = 0x8C;
                stringStringsOffset = 0x8C + (stringListSize * 8);

                writer.Write(stringMagic);
                writer.Write(0xDEADBEEF); // fileSize
                writer.Write(stringListSize);
                writer.Write(stringDataOffset);
                writer.Write(stringStringsOffset);
                writer.WriteNullTerminatedString(stringSection);

                // Padding stuff
                while (writer.Position < stringDataOffset + 8)
                    writer.Write((byte)0x00);

                // Get the shifts of the histogram
                List<int> histogramShifts = new List<int>();
                for (int i = 0x1FE; i > 0x80; i--)
                {
                    if (histogramSection[i] < 0x80)
                    {
                        histogramShifts.Add(i);
                    }
                }

                // Update hash pair offsets
                byte[] stringBytes = null;
                using (NativeWriter stringBuffer = new NativeWriter(new MemoryStream()))
                {
                    foreach (KeyValuePair<uint, string> keyValuePair in stringList)
                    {
                        writer.Write(keyValuePair.Key);
                        writer.Write((uint)stringBuffer.Position);
                        stringBuffer.Write(EncodeString(keyValuePair.Value, histogramShifts, histogramSection.Select(c => (char)c).ToList()));
                    }
                    // Write strings
                    writer.Write(stringBuffer.ToByteArray());
                }

                // Update fileSize
                stringFileSize = (uint)(writer.Position - 8);
                writer.Position = 4;
                writer.Write(stringFileSize);

                newStringData = writer.ToByteArray();
            }
        }
    }
}
