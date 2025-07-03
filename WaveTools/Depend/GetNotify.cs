using Newtonsoft.Json.Linq;
using System.Linq;
using System.IO;
using Windows.Storage;
using System;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using System.IO.Compression;

namespace WaveTools.Depend
{
    public class GetNotify
    {
        public string content { get; set; }
        public string jumpUrl { get; set; }
        public string time { get; set; }

        public async Task Get()
        {
            string apiAddress = "https://prod-cn-alicdn-gamestarter.kurogame.com/launcher/10003_Y8xXrXk65DqFHEDgApn3cpK5lfczpFx5/G152/information/zh-Hans.json";

            using (HttpClient client = new HttpClient())
            {
                try
                {
                    client.DefaultRequestHeaders.AcceptEncoding.Add(new System.Net.Http.Headers.StringWithQualityHeaderValue("gzip"));

                    var response = await client.GetAsync(apiAddress);
                    if (!response.IsSuccessStatusCode)
                    {
                        Console.WriteLine("获取失败，状态码：" + response.StatusCode);
                        return;
                    }

                    string jsonResponse;

                    if (response.Content.Headers.ContentEncoding.Contains("gzip"))
                    {
                        using var stream = await response.Content.ReadAsStreamAsync();
                        using var gzip = new GZipStream(stream, CompressionMode.Decompress);
                        using var reader = new StreamReader(gzip);
                        jsonResponse = await reader.ReadToEndAsync();
                    }
                    else
                    {
                        jsonResponse = await response.Content.ReadAsStringAsync();
                    }

                    Console.WriteLine("内容预览：" + jsonResponse.Substring(0, Math.Min(100, jsonResponse.Length)));

                    var jsonObject = JObject.Parse(jsonResponse);

                    var activityPosts = jsonObject["guidance"]?["activity"]?["contents"] is JToken activityToken
                        ? activityToken.Children().ToList()
                        : new List<JToken>();

                    var newsPosts = jsonObject["guidance"]?["news"]?["contents"] is JToken newsToken
                        ? newsToken.Children().ToList()
                        : new List<JToken>();

                    var noticePosts = jsonObject["guidance"]?["notice"]?["contents"] is JToken noticeToken
                        ? noticeToken.Children().ToList()
                        : new List<JToken>();

                    string documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                    string waveToolsFolderPath = Path.Combine(documentsPath, "JSG-LLC", "WaveTools", "Posts");

                    Directory.CreateDirectory(waveToolsFolderPath);

                    string activityFilePath = Path.Combine(waveToolsFolderPath, "activity.json");
                    string newsFilePath = Path.Combine(waveToolsFolderPath, "news.json");
                    string noticeFilePath = Path.Combine(waveToolsFolderPath, "notice.json");

                    await File.WriteAllTextAsync(activityFilePath, JArray.FromObject(activityPosts).ToString());
                    await File.WriteAllTextAsync(newsFilePath, JArray.FromObject(newsPosts).ToString());
                    await File.WriteAllTextAsync(noticeFilePath, JArray.FromObject(noticePosts).ToString());
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Error: " + ex.Message);
                }
            }
        }


        public List<GetNotify> GetData(string localData)
        {
            try
            {
                return JsonConvert.DeserializeObject<List<GetNotify>>(localData) ?? new List<GetNotify>();
            }
            catch
            {
                return new List<GetNotify>();
            }
        }
    }
}
