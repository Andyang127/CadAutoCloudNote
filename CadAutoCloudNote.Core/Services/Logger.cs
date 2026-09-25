using System;
using System.IO;
using System.Text;

namespace CadAutoCloudNote.Core.Services
{
    /// <summary>
    /// 日志记录服务：输出至 %AppData%\InkVerse\CadAutoCloudNote\Logs\
    /// </summary>
    public static class Logger
    {
        private static readonly object _lock = new object();
        private static string _logDirectory;

        static Logger()
        {
            try
            {
                string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                _logDirectory = Path.Combine(appData, "InkVerse\\CadAutoCloudNote\\Logs");
                if (!Directory.Exists(_logDirectory))
                {
                    Directory.CreateDirectory(_logDirectory);
                }
            }
            catch
            {
                _logDirectory = null;
            }
        }

        public static void Info(string message)
        {
            Write("INFO", message, null);
        }

        public static void Warn(string message)
        {
            Write("WARN", message, null);
        }

        public static void Error(string message, Exception ex = null)
        {
            Write("ERROR", message, ex);
        }

        private static void Write(string level, string message, Exception ex)
        {
            if (string.IsNullOrEmpty(_logDirectory)) return;

            try
            {
                string fileName = string.Format("log_{0:yyyyMMdd}.txt", DateTime.Now);
                string filePath = Path.Combine(_logDirectory, fileName);
                StringBuilder sb = new StringBuilder();
                sb.AppendFormat("[{0:yyyy-MM-dd HH:mm:ss.fff}] [{1}] {2}", DateTime.Now, level, message);
                sb.AppendLine();
                if (ex != null)
                {
                    sb.AppendLine("Exception: " + ex.Message);
                    sb.AppendLine("StackTrace: " + ex.StackTrace);
                }

                lock (_lock)
                {
                    File.AppendAllText(filePath, sb.ToString(), Encoding.UTF8);
                }
            }
            catch
            {
                // 静默保证业务绝不因日志失败
            }
        }
    }
}
