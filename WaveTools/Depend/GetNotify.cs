using Newtonsoft.Json.Linq;
using System.Linq;
using System.IO;
using Windows.Storage;
using System;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;

namespace WaveTools.Depend
{
    public class GetNotify
    {
        public string content { get; set; }
        public string jumpUrl { get; set; }
        public string time { get; set; }

        public async Task Get()
        {
            string apiAddress = "https://pcdownload-wangsu.aki-game.com/pcstarter/prod/starter/10003_Y8xXrXk65DqFHEDgApn3cpK5lfczpFx5/G152/guidance/zh-Hans.json";

            using (HttpClient client = new HttpClient())
            {
                try
                {
                    // 使用 HttpClient 发送请求并获取响应
                    string jsonResponse = await client.GetStringAsync(apiAddress);

                    // 将API响应转换为JSON对象并筛选特定类型的帖子
                    var jsonObject = JObject.Parse(jsonResponse);

                    var activityPosts = jsonObject["guidance"]["activity"]["contents"].Children().ToList();
                    var newsPosts = jsonObject["guidance"]["news"]["contents"].Children().ToList();
                    var noticePosts = jsonObject["guidance"]["notice"]["contents"].Children().ToList();

                    // 将结果保存到文件中
                    var folder = KnownFolders.DocumentsLibrary;
                    var WaveToolsFolder = await folder.CreateFolderAsync("JSG-LLC\\WaveTools", CreationCollisionOption.OpenIfExists);
                    var activityFile = await WaveToolsFolder.CreateFileAsync("Posts\\activity.json", CreationCollisionOption.OpenIfExists);
                    var newsFile = await WaveToolsFolder.CreateFileAsync("Posts\\news.json", CreationCollisionOption.OpenIfExists);
                    var noticeFile = await WaveToolsFolder.CreateFileAsync("Posts\\notice.json", CreationCollisionOption.OpenIfExists);

                    await File.WriteAllTextAsync(activityFile.Path, JArray.FromObject(activityPosts).ToString());
                    await File.WriteAllTextAsync(newsFile.Path, JArray.FromObject(newsPosts).ToString());
                    await File.WriteAllTextAsync(noticeFile.Path, JArray.FromObject(noticePosts).ToString());
                }
                catch (Exception ex)
                {
                    // 打印错误信息
                    Console.WriteLine("Error: " + ex.Message);
                }
            }
        }

        public List<GetNotify> GetData(string localData)
        {
            var records = JsonConvert.DeserializeObject<List<GetNotify>>(localData);
            return records;
        }
    }
}