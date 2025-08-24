using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static ChatClient.Models.EmojiModel;

namespace ChatClient.Services
{
    internal class EmojiParsing
    {
        public static List<EmojiGroup> ParseEmojiFile(string path)
        {
            // ovde izdvajamo emojije iz fajlova i pravimo grupe i subgrupe
            var groups = new List<EmojiGroup>();
            EmojiGroup? currentGroup = null;
            EmojiSubgroup? currentSubgroup = null;

            foreach (var line in File.ReadLines(path))
            {
                if (line.StartsWith("# group:"))
                {
                    currentGroup = new EmojiGroup { GroupName = line.Substring(8).Trim() };
                    groups.Add(currentGroup);
                }
                else if (line.StartsWith("# subgroup:"))
                {
                    currentSubgroup = new EmojiSubgroup { SubgroupName = line.Substring(11).Trim() };
                    currentGroup?.Subgroups.Add(currentSubgroup);
                }
               
                else if (line.Contains("; fully-qualified") && line.Contains("#"))
                {
                    var codePoints = line.Split(';')[0].Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);

                    var sb = new StringBuilder();
                    foreach (var cp in codePoints)
                    {
                        int value = Convert.ToInt32(cp, 16);
                        sb.Append(char.ConvertFromUtf32(value));
                    }

                    var emojiChar = sb.ToString();
                    currentSubgroup?.Emojis.Add(emojiChar);
                }

            }

            return groups;
        }
    }
}
