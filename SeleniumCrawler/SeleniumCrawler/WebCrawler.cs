using Ivony.Html.Parser;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace SeleniumCrawler
{
    public class WebCrawler
    {
        protected static readonly IConfigurationRoot Config = new ConfigurationBuilder()
            .AddJsonFile("AppConfig.json", false, true).Build();

        protected static readonly JumonyParser JumonyParser = new JumonyParser();

        public virtual async Task StartAsync()
        {
            await Task.Delay(0);
        }

        protected virtual async Task ContinueCrawlerAsync(WebCrawler crawler, string lastCache, string basePath)
        {
            string lastArea = string.Empty;
            bool lastAreaExists = File.Exists(lastCache);
            if (lastAreaExists)
            {
                lastArea = File.ReadAllLines(lastCache)[0];
            }
            int count = 0;
            while (++count > 0)
            {
                LogHelper.Info($"{crawler.GetType()}城市:" + count.ToString());
                var path = Directory.GetCurrentDirectory() + string.Format(basePath, count.ToString());
                if (File.Exists(path))
                {
                    var areas = File.ReadAllLines(path);
                    await StartCrawlerAsync(crawler, areas, lastAreaExists, lastCache, lastArea);
                }
                else
                {
                    LogHelper.Info($"{crawler.GetType()}爬虫结束");
                    File.Delete(lastCache);
                    break;
                }
            }
        }

        public virtual async Task HaveAgentsAsync(Uri uri)
        {
            await Task.Delay(0);
        }

        public virtual async Task AgentCrawlerAsync(Uri cityUri, int currentPage = 1, int totalPage = 1)
        {
            try
            {
                while (currentPage <= totalPage)
                {
                    Uri uri = new Uri(string.Format(cityUri.ToString(), currentPage));
                    LogHelper.Info(uri.ToString());
                    await HaveAgentsAsync(uri);
                    currentPage++;
                }
            }
            catch (Exception exception)
            {
                LogHelper.Error(exception.Message);
            }
        }

        public virtual Task<List<string>> GetCityAreaUrisAsync(string cityArea)
        {
            return (Task<List<string>>)Task.CompletedTask;
        }

        public virtual Task<List<string>> GetAreasAsync(string area)
        {
            return (Task<List<string>>)Task.CompletedTask;
        }

        private int index;

        /// <summary>
        /// 板块分城市写入文件
        /// </summary>
        /// <param name="cityUrlConfig"></param>
        public virtual async Task WriteAreaToFileAsync(string configPath, string directory)
        {
            string[] cityUrlConfig = Config.GetSection(configPath).GetChildren().Select(key => key.Value).ToArray();
            string areaPath = Directory.GetCurrentDirectory() + directory;
            if (!Directory.Exists(areaPath))
            {
                Directory.CreateDirectory(areaPath);
            }

            DirectoryInfo directoryInfo = new DirectoryInfo(areaPath);
            int fileCount = directoryInfo.GetFiles().Length;
            index = fileCount + 1;
            var cities = cityUrlConfig.Skip(index);
            foreach (var city in cities)
            {
                ++index;
                await WriteAreaUriToFileAsync(this, city, areaPath);
            }
        }

        protected async Task WriteAreaUriToFileAsync(WebCrawler crawler, string city, string areaPath)
        {
            List<string> areaList = new List<string>();
            var cityAreaUris = await crawler.GetCityAreaUrisAsync(city);
            foreach (var cityAreaUri in cityAreaUris)
            {
                var areas = await crawler.GetAreasAsync(cityAreaUri);
                foreach (var area in areas)
                {
                    areaList.Add(area);
                }
            }
            if (areaList.Count > 0)
            {
                File.WriteAllLines(areaPath + $"/{index}.txt", areaList);
            }
            else
            {
                --index;
            }
        }

        protected async Task StartCrawlerAsync(WebCrawler crawler, string[] areas, bool lastAreaExists,
            string lastCache, string lastArea)
        {
            if (lastAreaExists)
            {
                areas = areas.SkipWhile(area => area != lastArea).ToArray();
            }
            LogHelper.Info($"剩余城市数量：{areas.Count()}");
            foreach (var area in areas)
            {
                File.WriteAllText(lastCache, area);
                await crawler.AgentCrawlerAsync(new Uri(area));
            }
        }
    }
}