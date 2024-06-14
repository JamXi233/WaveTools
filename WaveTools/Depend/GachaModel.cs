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
