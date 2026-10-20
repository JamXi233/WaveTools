using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WaveTools.Depend
{
    public class GachaModel
    {
        private static readonly HashSet<string> PermanentFiveStarNames = new HashSet<string>(StringComparer.Ordinal)
        {
            "维里奈", "安可", "卡卡罗", "凌阳", "鉴心"
        };

        public enum FiveStarUpRule
        {
            None,
            Guaranteed,
            CanLose
        }

        public class GroupedRecord
        {
            public string Name { get; set; }
            public int Count { get; set; }
        }

        public class GachaData
        {
            public GachaInfo Info { get; set; }
            public List<GachaPool> List { get; set; }
        }

        public class GachaInfo
        {
            public string Uid { get; set; }
        }

        public class GachaPool
        {
            public int CardPoolId { get; set; }
            public string CardPoolType { get; set; }
            public List<GachaRecord> Records { get; set; }
        }

        public class GachaRecord
        {
            public string ResourceId { get; set; }
            public string Name { get; set; }
            public int QualityLevel { get; set; }
            public string ResourceType { get; set; }
            public string Time { get; set; }
            public string Id { get; set; }
        }

        public class CardPool
        {
            public int CardPoolId { get; set; }
            public string CardPoolType { get; set; }
            public int? FiveStarPity { get; set; }
            public int? FourStarPity { get; set; }
            public bool? isPityEnable { get; set; }

            [JsonProperty("fiveStarUpMode")]
            public string FiveStarUpMode { get; set; }

            [JsonProperty("fiveStarLoseNames")]
            public List<string> FiveStarLoseNames { get; set; }

            private IReadOnlyCollection<string> EffectiveFiveStarLoseNames => FiveStarLoseNames != null
                ? FiveStarLoseNames
                : CardPoolId == 1 ? PermanentFiveStarNames : Array.Empty<string>();

            [JsonIgnore]
            public FiveStarUpRule UpRule
            {
                get
                {
                    if (FiveStarUpMode == null)
                    {
                        return CardPoolId switch
                        {
                            1 => FiveStarUpRule.CanLose,
                            2 or 11 => FiveStarUpRule.Guaranteed,
                            _ => FiveStarUpRule.None
                        };
                    }

                    return FiveStarUpMode.Trim().ToLowerInvariant() switch
                    {
                        "canlose" => FiveStarUpRule.CanLose,
                        "guaranteed" => FiveStarUpRule.Guaranteed,
                        _ => FiveStarUpRule.None
                    };
                }
            }

            [JsonIgnore]
            public bool HasFiveStarUp => UpRule == FiveStarUpRule.Guaranteed ||
                (UpRule == FiveStarUpRule.CanLose && EffectiveFiveStarLoseNames.Count > 0);

            [JsonIgnore]
            public bool HasFiveStarPity => isPityEnable != false && FiveStarPity.GetValueOrDefault() > 0;

            [JsonIgnore]
            public bool HasFourStarPity => isPityEnable != false && FourStarPity.GetValueOrDefault() > 0;

            public bool IsFiveStarUp(GachaRecord record)
            {
                if (record == null || record.QualityLevel != 5 || string.IsNullOrWhiteSpace(record.Name))
                {
                    return false;
                }

                return UpRule switch
                {
                    FiveStarUpRule.Guaranteed => true,
                    FiveStarUpRule.CanLose => HasFiveStarUp && !IsFiveStarLost(record),
                    _ => false
                };
            }

            public bool IsFiveStarLost(GachaRecord record)
            {
                return record != null && record.QualityLevel == 5 &&
                    !string.IsNullOrWhiteSpace(record.Name) &&
                    UpRule == FiveStarUpRule.CanLose &&
                    EffectiveFiveStarLoseNames.Contains(record.Name, StringComparer.Ordinal);
            }

            public string GetFiveStarUpLabel(GachaRecord record)
            {
                return IsFiveStarLost(record) ? "歪了" : string.Empty;
            }
        }

        public class CardPoolInfo
        {
            public List<CardPool> CardPools { get; set; }
        }

        public class GachaUrl
        {
            [JsonProperty("gachaLink")]
            public string GachaLink { get; set; }

            [JsonProperty("playerId")]
            public string PlayerId { get; set; }

            [JsonProperty("cardPoolType")]
            public string CardPoolType { get; set; }

            [JsonProperty("serverId")]
            public string ServerId { get; set; }

            [JsonProperty("languageCode")]
            public string LanguageCode { get; set; }

            [JsonProperty("recordId")]
            public string RecordId { get; set; }
        }
    }
}
