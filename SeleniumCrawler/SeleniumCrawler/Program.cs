using System;
using System.Threading.Tasks;

namespace SeleniumCrawler
{
    internal static class Program
    {
        private static async Task Main(string[] args)
        {
            LogHelper.Info($"开始:{DateTime.Now}");
            var crawler = new QccCrawler();
            await crawler.WriteAreaToFileAsync(null, null);//将省、市、区县uri存入文件
            await crawler.StartAsync();//抓取公司
            LogHelper.Info($"结束:{ DateTime.Now}");
        }
    }
}