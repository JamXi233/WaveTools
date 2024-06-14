using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Windows.ApplicationModel;

namespace WaveTools.Depend
{
    public class GachaCommon
    {
        public class Info
        {
            public string uid { get; set; }
        }

        public class GachaRecord
        {
            public string gacha_id { get; set; }
            public string gacha_type { get; set; }
            public string item_id { get; set; }
            public string count { get; set; }
            public string time { get; set; }
            public string name { get; set; }
            public string item_type { get; set; }
            public string rank_type { get; set; }
            public string id { get; set; }
        }

        public class SourceInfo
        {
            public string uid { get; set; }
        }

        public class SourceGachaRecord
        {
            public string cardPoolType { get; set; }
            public int resourceId { get; set; }
            public int qualityLevel { get; set; }
            public string resourceType { get; set; }
            public string name { get; set; }
            public int count { get; set; }
            public string time { get; set; }
            public string id { get; set; }
        }

        public class SourceRecord
        {
            public int cardPoolId { get; set; }
            public string cardPoolType { get; set; }
            public List<SourceGachaRecord> records { get; set; }
        }

        public class SourceData
        {
            public SourceInfo info { get; set; }
            public List<SourceRecord> list { get; set; }
        }

        public static string GenerateUniqueId(long timestamp, int cardPoolId, int drawNumber)
        {
            return $"{timestamp:D10}{cardPoolId:D4}000{drawNumber:D2}";
        }


    }

    public class ImportGacha
    {
        public class ImportData
        {
            public GachaCommon.Info info { get; set; }
            public List<GachaCommon.GachaRecord> list { get; set; }
        }

        public static async Task Import(string importFilePath)
        {
            // 读取导入文件
            var importJson = await File.ReadAllTextAsync(importFilePath);
            var importData = JsonConvert.DeserializeObject<ImportData>(importJson);

            if (importData?.info?.uid == null)
            {
                throw new InvalidOperationException("Invalid import file: missing uid.");
            }

            string uid = importData.info.uid;
            string recordsBasePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), @"JSG-LLC\WaveTools\GachaRecords");
            string targetFilePath = Path.Combine(recordsBasePath, $"{uid}.json");
            string tempExportFilePath = Path.Combine(recordsBasePath, "tmp", $"{uid}.json");

            // 确保临时目录存在
            Directory.CreateDirectory(Path.Combine(recordsBasePath, "tmp"));

            // 如果文件存在，读取现有数据并导出到临时文件
            if (File.Exists(targetFilePath))
            {
                var existingJson = await File.ReadAllTextAsync(targetFilePath);
                var sourceData = JsonConvert.DeserializeObject<GachaCommon.SourceData>(existingJson) ?? new GachaCommon.SourceData { info = new GachaCommon.SourceInfo { uid = uid }, list = new List<GachaCommon.SourceRecord>() };

                ExportGacha.Export(targetFilePath, tempExportFilePath);

                // 读取导出的临时文件
                var tempExportJson = await File.ReadAllTextAsync(tempExportFilePath);
                var tempExportData = JsonConvert.DeserializeObject<ImportData>(tempExportJson);

                if (tempExportData.info.uid != importData.info.uid)
                {
                    throw new InvalidOperationException("UID mismatch between existing records and import file.");
                }

                // 合并记录
                var existingRecords = tempExportData.list.ToDictionary(record => record.id, record => record);
                foreach (var importRecord in importData.list)
                {
                    existingRecords[importRecord.id] = importRecord;
                }

                importData.list = existingRecords.Values.ToList();
            }

            // 准备合并后的源数据
            var finalSourceData = new GachaCommon.SourceData
            {
                info = new GachaCommon.SourceInfo { uid = uid },
                list = new List<GachaCommon.SourceRecord>()
            };

            var existingSourceRecords = finalSourceData.list.SelectMany(record => record.records).ToDictionary(record => record.id, record => record);
            foreach (var importRecord in importData.list)
            {
                if (!existingSourceRecords.ContainsKey(importRecord.id))
                {
                    int cardPoolId = int.Parse(importRecord.gacha_id);
                    var sourceRecord = new GachaCommon.SourceGachaRecord
                    {
                        resourceId = int.Parse(importRecord.item_id),
                        qualityLevel = int.Parse(importRecord.rank_type),
                        resourceType = importRecord.item_type,
                        name = importRecord.name,
                        count = int.Parse(importRecord.count),
                        time = importRecord.time,
                        id = importRecord.id
                    };

                    if (!finalSourceData.list.Any(r => r.cardPoolId == cardPoolId))
                    {
                        finalSourceData.list.Add(new GachaCommon.SourceRecord
                        {
                            cardPoolId = cardPoolId,
                            cardPoolType = importRecord.gacha_type,
                            records = new List<GachaCommon.SourceGachaRecord> { sourceRecord }
                        });
                    }
                    else
                    {
                        finalSourceData.list.First(r => r.cardPoolId == cardPoolId).records.Add(sourceRecord);
                    }

                    existingSourceRecords[importRecord.id] = sourceRecord;
                }
            }

            // 确保目录存在
            Directory.CreateDirectory(recordsBasePath);

            // 对记录先按 cardPoolId 排序，然后按 id 倒序排序
            finalSourceData.list = finalSourceData.list.OrderBy(record => record.cardPoolId).ToList();
            foreach (var sourceRecord in finalSourceData.list)
            {
                sourceRecord.records = sourceRecord.records.OrderByDescending(record => record.id).ToList();
            }

            // 序列化时移除 records 中的 cardPoolType 字段
            var settings = new JsonSerializerSettings
            {
                Formatting = Formatting.Indented,
                NullValueHandling = NullValueHandling.Ignore
            };
            var finalSourceJson = JsonConvert.SerializeObject(finalSourceData, settings);
            await File.WriteAllTextAsync(targetFilePath, finalSourceJson);
            Directory.Delete(Path.Combine(recordsBasePath, "tmp"), true);
        }
    }



    public class ExportGacha
    {
        public class ExportInfo : GachaCommon.Info
        {
            public string lang { get; set; }
            public int region_time_zone { get; set; }
            public long export_timestamp { get; set; }
            public string export_app { get; set; }
            public string export_app_version { get; set; }
            public string wwgf_version { get; set; }
        }

        public class ExportData
        {
            public ExportInfo info { get; set; }
            public List<GachaCommon.GachaRecord> list { get; set; }
        }

        public static void Export(string sourceFilePath, string exportFilePath)
        {
            // 读取源文件
            var sourceJson = File.ReadAllText(sourceFilePath);
            var sourceData = JsonConvert.DeserializeObject<GachaCommon.SourceData>(sourceJson);
            PackageVersion packageVersion = Package.Current.Id.Version;
            string currentVersion = $"{packageVersion.Major}.{packageVersion.Minor}.{packageVersion.Build}.{packageVersion.Revision}";

            // 准备导出数据
            var exportData = new ExportData
            {
                info = new ExportInfo
                {
                    uid = sourceData.info.uid,
                    lang = "zh-cn",
                    region_time_zone = 8,
                    export_timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                    export_app = "WaveTools",
                    export_app_version = currentVersion,
                    wwgf_version = "v0.1b"
                },
                list = new List<GachaCommon.GachaRecord>()
            };

            var timestampCounter = new Dictionary<long, int>();

            foreach (var sourceRecord in sourceData.list)
            {
                // 对 sourceRecord.records 按时间排序
                var sortedRecords = sourceRecord.records.OrderBy(record => record.time).ToList();
                foreach (var gachaRecord in sortedRecords)
                {
                    // 获取时间戳（秒级）
                    long timestamp = DateTimeOffset.Parse(gachaRecord.time).ToUnixTimeSeconds();

                    // 计算一秒内的抽数，初始化为10
                    if (!timestampCounter.ContainsKey(timestamp))
                    {
                        timestampCounter[timestamp] = Math.Min(sortedRecords.Count(record => DateTimeOffset.Parse(record.time).ToUnixTimeSeconds() == timestamp), 10);  // 初始化为当秒记录的总数或10
                    }
                    int drawNumber = timestampCounter[timestamp];
                    timestampCounter[timestamp]--;  // 递减

                    // 生成唯一的id
                    string uniqueId = GachaCommon.GenerateUniqueId(timestamp, sourceRecord.cardPoolId, drawNumber);

                    // 删除 records 中的 cardPoolType 字段
                    gachaRecord.cardPoolType = null;

                    exportData.list.Add(new GachaCommon.GachaRecord
                    {
                        gacha_id = sourceRecord.cardPoolId.ToString("D4"),
                        gacha_type = sourceRecord.cardPoolType,
                        item_id = gachaRecord.resourceId.ToString(),
                        count = gachaRecord.count.ToString(),
                        time = gachaRecord.time,
                        name = gachaRecord.name,
                        item_type = gachaRecord.resourceType,
                        rank_type = gachaRecord.qualityLevel.ToString(),
                        id = uniqueId
                    });
                }
            }

            // 对记录先按 cardPoolId 排序，然后按 id 倒序排序
            exportData.list = exportData.list.OrderBy(record => record.gacha_id).ThenByDescending(record => record.id).ToList();

            // 序列化并写入导出文件
            var exportJson = JsonConvert.SerializeObject(exportData, Formatting.Indented);
            File.WriteAllText(exportFilePath, exportJson);
        }
    }

}
