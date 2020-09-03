using Ivony.Html;
using OpenQA.Selenium;
using OpenQA.Selenium.Interactions;
using OpenQA.Selenium.Support.Extensions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace SeleniumCrawler
{
    internal class QccCrawler : WebCrawler
    {
        private const int MaxPage = 250;
        private const int PageSize = 20;

        public override async Task StartAsync()
        {
            try
            {
                const string lastCache = "lastCityUri.txt";
                string lastArea = string.Empty;
                bool lastAreaExists = File.Exists(lastCache);
                if (lastAreaExists)
                {
                    lastArea = File.ReadAllLines(lastCache)[0];
                }
                const string basePath = "企查查.txt";
                if (File.Exists(basePath))
                {
                    var areas = await File.ReadAllLinesAsync(basePath);
                    await StartCrawlerAsync(this, areas, lastAreaExists, lastCache, lastArea);
                }
                LogHelper.Info("企查查爬虫结束");
                File.Delete(lastCache);
            }
            catch (Exception exception)
            {
                LogHelper.Info(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType.FullName);
                LogHelper.Error(exception.Message);
            }
        }

        public override async Task AgentCrawlerAsync(Uri cityUri, int currentPage = 1, int totalPage = 1)
        {
            const string Page = "p={0}";
            while (true)
            {
                StringBuilder.Clear();
                StringBuilder.Append(cityUri.ToString());
                if (currentPage > MaxPage ||
                    !await GetAgentsAsync(new Uri(StringBuilder.AppendFormat(Page, currentPage).ToString())))
                {
                    break;
                }
                currentPage++;
            }
        }

        private async Task<bool> GetAgentsAsync(Uri cityUri)
        {
            LogHelper.Info(cityUri.ToString());
            var pageSource = await HttpClient.GetStringAsync(cityUri);
            while (!pageSource.Contains("查企业"))
            {
                if (pageSource.StartsWith("<script>window.location"))
                {
                    VertifyCode(new Uri(pageSource.Split("'")[1]));
                    pageSource = await HttpClient.GetStringAsync(cityUri);
                }
                else if (pageSource.Contains("小查还没找到数据"))
                {
                    return false;
                }
            }
            var block = JumonyParser.Parse(pageSource).Find(".m_srchList tbody tr td:nth-child(3)");
            foreach (var item in block)
            {
                await VertifyAsync(item.InnerHtml());
            }
            if (block.Count() < PageSize)
            {
                return false;
            }
            return true;
        }

        private void VertifyCode(Uri uri)
        {
            try
            {
                SeleniumVertifyCode(uri);
            }
            catch (WebDriverException exception)
            {
                LogHelper.Error($"{exception.Message}");
            }
        }

        private void SeleniumVertifyCode(Uri uri)
        {
            var options = new OpenQA.Selenium.Chrome.ChromeOptions();
            options.AddArgument("-headless");
            options.AddArgument("log-level=3");
            using IWebDriver driver = new OpenQA.Selenium.Chrome.ChromeDriver(options);
            driver.Manage().Window.Maximize();
            driver.Navigate().GoToUrl(uri);
            driver.Manage().Cookies.AddCookie(new Cookie("QCCSESSID", Cookie, ".qcc.com", "/", null));
            driver.Navigate().GoToUrl(uri);
            driver.ExecuteJavaScript("Object.defineProperty(navigator, 'webdriver', {get: () => undefined})");
            var slide = driver.FindElement(By.CssSelector("#nc_1_n1z"));
            var verifyContainer = driver.FindElement(By.CssSelector(".nc-lang-cnt"));
            var width = verifyContainer.Size.Width;
            var action = new Actions(driver);
            action.ClickAndHold(slide).Perform();
            Random random = new Random();
            int offset = 0;
            const int minOffset = 10;
            const int maxOffset = 30;
            while (width > offset)
            {
                offset += random.Next(minOffset, maxOffset);
                action.MoveByOffset(offset, 0).Perform();
                var code = driver.FindElement(By.CssSelector(".nc-lang-cnt")).Text;
                if (code.Contains("验证通过"))
                {
                    break;
                }
                System.Threading.Thread.Sleep(offset * minOffset);
            }
            //截图测试
            //Screenshot screenShotFile = ((ITakesScreenshot)driver).GetScreenshot();
            //string img_url = Environment.CurrentDirectory + "\\test.png";
            //screenShotFile.SaveAsFile(img_url, ScreenshotImageFormat.Png);
            action.Click(driver.FindElement(By.CssSelector("#verify"))).Perform();
            driver.Quit();
        }

        private async Task VertifyAsync(string source)
        {
            var nameResult = Regex.Matches(source, "人物名称':'([\u4e00-\u9fa5]{2,4})");
            string name = string.Empty;
            if (nameResult.Any())
            {
                name = nameResult[0].Groups[1].Value;
            }
            var cellphone = Regex.Matches(source, @"电话：\s+?([\d-]{11,})");
            if (!cellphone.Any())
            {
                LogHelper.Info("企查查无电话");
                return;
            }
            var telphone = cellphone[0].Groups[1].Value;
            var result = Regex.Matches(source, @"gt;([\s\S]+?)/a>");
            if (!result.Any())
            {
                result = Regex.Matches(source, "企业名称':'([\u4e00-\u9fa5（）]{1,})");
            }
            var company = result[0].Groups[1].Value.Replace("em", "")
                .Replace("<", "").Replace(">", "").Replace("/", "").Trim();
            var city = company.Substring(0, 2);
            await SaveAsync(city, name, company, telphone);
        }

        private async Task SaveAsync(string city, string name, string company, string telphone)
        {
            LogHelper.Info($"企查查:{city},{name}, {company}, {telphone}");
            //await SaveToDb();
        }

        private static readonly HttpClient HttpClient = new HttpClient();
        private static readonly string Cookie = Config.GetSection("QccCookie").Value;

        static QccCrawler()
        {
            HttpClient.DefaultRequestHeaders.Add("Cookie", Cookie);
            HttpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 AppleWebKit/537.36 Safari/537.36");
        }

        public override async Task WriteAreaToFileAsync(string configPath, string directory)
        {
            const string BaseProvinceLink = "https://www.qcc.com/search?key=关键字#industrycode:K&";
            const string BaseCityLink = "https://www.qcc.com/search_getCityListHtml?province={0}";
            const string BaseCountyLink = "https://www.qcc.com/search_getCountyListHtml?city={0}";
            List<string> provinces = await GetCodeAsync(new Uri(BaseProvinceLink), ".sfilter-tag.clearfix.provinceChoose dd a");
            const string baseText = "province:{0}&city:{1}&county:{2}&";
            List<string> list = new List<string>();
            foreach (var province in provinces)
            {
                StringBuilder.Clear();
                Uri provinceUri = new Uri(StringBuilder.AppendFormat(BaseCityLink, province).ToString());
                var cities = await GetCodeAsync(provinceUri, "dd a");
                foreach (var city in cities)
                {
                    StringBuilder.Clear();
                    Uri cityUri = new Uri(StringBuilder.AppendFormat(BaseCountyLink, city).ToString());
                    var counties = await GetCodeAsync(cityUri, "dd a");
                    foreach (var county in counties)
                    {
                        StringBuilder.Clear();
                        StringBuilder.Append(BaseProvinceLink);
                        string area = StringBuilder.AppendFormat(baseText, province, city, county).
                            Replace("search", "search_index").Replace("中介#", "中介&ajaxflag=1&")
                            .Replace(":industrycode", "=industrycode").ToString();
                        list.Add(area);
                    }
                }
            }
            await File.WriteAllLinesAsync("企查查.txt", list);
        }

        private readonly static StringBuilder StringBuilder = new StringBuilder(128);

        private async Task<List<string>> GetCodeAsync(Uri uri, string filter)
        {
            var source = await HttpClient.GetStringAsync(uri);
            var html = JumonyParser.Parse(source).Find(filter);
            List<string> codeList = new List<string>();
            foreach (var item in html)
            {
                var code = item.Attribute("data-value").AttributeValue;
                var name = item.Attribute("data-append").AttributeValue;
                Console.WriteLine(name);
                codeList.Add(code);
            }
            return codeList;
        }
    }
}