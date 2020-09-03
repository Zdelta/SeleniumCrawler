using log4net;
using log4net.Config;
using System.IO;

namespace SeleniumCrawler
{
    public static class LogHelper
    {
        private static readonly ILog Log = Configure();

        public static ILog Configure(string repositoryName = "NETCoreRepository", string configFile = @"Utils\log4net.config")
        {
            XmlConfigurator.Configure(LogManager.CreateRepository(repositoryName), new FileInfo(configFile));
            return LogManager.GetLogger(repositoryName, "");
        }

        public static void Info(string msg)
        {
            Log.Info(msg);
        }

        public static void Warn(string msg)
        {
            Log.Warn(msg);
        }

        public static void Error(string msg)
        {
            Log.Error(msg);
        }
    }
}