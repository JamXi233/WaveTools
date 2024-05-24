// Copyright (c) 2021-2024, JamXi JSG-LLC.
// All rights reserved.

// This file is part of WaveTools.

// WaveTools is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.

// WaveTools is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.

// You should have received a copy of the GNU General Public License
// along with WaveTools.  If not, see <http://www.gnu.org/licenses/>.

// For more information, please refer to <https://www.gnu.org/licenses/gpl-3.0.html>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Net.Http;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using static WaveTools.App;
using System.IO;

namespace WaveTools.Depend
{
    public class GachaRecords
    {
        public string Uid { get; set; }
        public string GachaId { get; set; }
        public string GachaType { get; set; }
        public string ItemId { get; set; }
        public string Count { get; set; }
        public string Time { get; set; }
        public string Name { get; set; }
        public string Lang { get; set; }
        public string ItemType { get; set; }
        public string RankType { get; set; }
        public string Id { get; set; }

        public async Task<List<GachaRecords>> GetAllGachaRecordsAsync(String url, String localData = null , String gachaType = "11")
        {
            var records = new List<GachaRecords>();
            if (localData == null){
                var page = 1;
                var count = 0;
                var endId = "0";
                int urlindex = url.IndexOf("&gacha_type=");
                while (true)
                {
                    try
                    {
                        var client = new HttpClient();
                        await Task.Delay(TimeSpan.FromSeconds(0.08));
                        Logging.Write("Wait Timeout...", 0);
                        var newurl = url.Substring(0, urlindex) + "&gacha_type=" + gachaType + "&end_id=" + endId;
                        var response = await client.GetAsync(newurl);
                        if (!response.IsSuccessStatusCode) break;
                        var json = await response.Content.ReadAsStringAsync();
                        var data = System.Text.Json.JsonSerializer.Deserialize<JsonElement>(json).GetProperty("data");
                        Logging.Write(url.Substring(0, urlindex) + "&gacha_type=" + gachaType + "&end_id=" + endId, 0);
                        JObject jsonObj = JObject.Parse(json);
                        if (jsonObj["message"].ToString() == "authkey timeout")
                        {
                            records.Add(new GachaRecords
                            {
                                Uid = jsonObj["message"].ToString(),
                                GachaId = "",
                                GachaType = "",
                                ItemId = "",
                                Count = "",
                                Time = "",
                                Name = "",
                                Lang = "",
                                ItemType = "",
                                RankType = "",
                                Id = ""
                            });
                            return records;
                        }
                        else
                        {
                            if (data.GetProperty("list").GetArrayLength() == 0) break;
                            foreach (var item in data.GetProperty("list").EnumerateArray())
                            {
                                records.Add(new GachaRecords
                                {
                                    Uid = item.GetProperty("uid").GetString(),
                                    GachaId = item.GetProperty("gacha_id").GetString(),
                                    GachaType = item.GetProperty("gacha_type").GetString(),
                                    ItemId = item.GetProperty("item_id").GetString(),
                                    Count = item.GetProperty("count").GetString(),
                                    Time = item.GetProperty("time").GetString(),
                                    Name = item.GetProperty("name").GetString(),
                                    Lang = item.GetProperty("lang").GetString(),
                                    ItemType = item.GetProperty("item_type").GetString(),
                                    RankType = item.GetProperty("rank_type").GetString(),
                                    Id = item.GetProperty("id").GetString()
                                });
                                count++;
                                Logging.Write(item.GetProperty("uid").GetString() + "|" + item.GetProperty("time").GetString() + "|" + item.GetProperty("gacha_id").GetString() + "|" + item.GetProperty("name").GetString() + "|" + item.GetProperty("id").GetString(), 0);
                                WaitOverlayManager.RaiseWaitOverlay(true, true, 0, "正在获取API信息,请不要退出", "已获取"+count+"条记录"+item.GetProperty("uid").GetString() + "|" + item.GetProperty("time").GetString() + "|" + item.GetProperty("name").GetString());
                            }
                            endId = records.Last().Id;
                            page++;
                        }
                    }
                    catch (Exception ex)
                    {
                        records.Add(new GachaRecords
                        {
                            Uid = ex.Message,
                            GachaId = "",
                            GachaType = "",
                            ItemId = "",
                            Count = "",
                            Time = "",
                            Name = "",
                            Lang = "",
                            ItemType = "",
                            RankType = "",
                            Id = ""
                        });
                        return records;
                    }
                }
            }
            else {
                records = JsonConvert.DeserializeObject<List<GachaRecords>>(localData);
            }
            Logging.Write("Gacha Get Finished!", 0);
            return records;
        }

        private static readonly string userDocumentsFolderPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        private static readonly string WaveToolsBasePath = Path.Combine(userDocumentsFolderPath, "JSG-LLC", "WaveTools");

        public static async Task UpdateGachaRecordsAsync()
        {
            string[] gachaFiles = {
        "GachaRecords_Character.ini",
        "GachaRecords_LightCone.ini",
        "GachaRecords_Newbie.ini",
        "GachaRecords_Regular.ini"
    };

            foreach (var fileName in gachaFiles)
            {
                string filePath = Path.Combine(WaveToolsBasePath, fileName);
                if (File.Exists(filePath))
                {
                    try
                    {
                        string fileContent = await File.ReadAllTextAsync(filePath);
                        var newRecords = JsonConvert.DeserializeObject<GachaRecords[]>(fileContent);
                        if (newRecords != null && newRecords.Length > 0)
                        {
                            string uid = newRecords[0].Uid;
                            string targetDirectory = Path.Combine(WaveToolsBasePath, "GachaRecords", uid);
                            Directory.CreateDirectory(targetDirectory);
                            string targetFilePath = Path.Combine(targetDirectory, fileName);

                            // 检查目标文件是否存在以决定是合并还是创建新文件
                            if (File.Exists(targetFilePath))
                            {
                                // 读取现有的数据
                                string existingContent = await File.ReadAllTextAsync(targetFilePath);
                                var existingRecords = JsonConvert.DeserializeObject<GachaRecords[]>(existingContent);

                                // 创建一个查找集合以快速检查ID是否存在
                                var existingIds = new HashSet<string>(existingRecords.Select(rec => rec.Id));

                                // 合并新旧数据，只添加不存在的记录
                                var mergedRecords = existingRecords.ToList();
                                mergedRecords.AddRange(newRecords.Where(rec => !existingIds.Contains(rec.Id)));

                                // 对合并后的记录按时间降序排序
                                mergedRecords.Sort((a, b) => b.Time.CompareTo(a.Time));

                                // 序列化合并后的数据
                                string serializedContent = JsonConvert.SerializeObject(mergedRecords.ToArray());
                                // 写入合并后的数据到文件
                                await File.WriteAllTextAsync(targetFilePath, serializedContent);
                            }
                            else
                            {
                                // 如果目标文件不存在，直接按时间排序后写入新数据
                                Array.Sort(newRecords, (a, b) => b.Time.CompareTo(a.Time));
                                await File.WriteAllTextAsync(targetFilePath, JsonConvert.SerializeObject(newRecords));
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error processing file {fileName}: {ex.Message}");
                    }
                }
            }
        }


    }
}
