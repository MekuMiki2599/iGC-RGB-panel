using System;
using System.IO;
using System.Text;

namespace iGCRGB.Core
{
    /// <summary>极简日志：写在程序目录（不可写时退回到 %LOCALAPPDATA%\iGCRGB）。</summary>
    public static class AppLog
    {
        private static readonly object Lock = new object();
        private static string _path;
        private static long _written;

        public static string LogPath
        {
            get { return _path; }
        }

        public static void Init(string preferredPath)
        {
            lock (Lock)
            {
                _path = null;
                if (TrySet(preferredPath)) return;
                try
                {
                    string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                    string alt = Path.Combine(Path.Combine(local, "iGCRGB"), "iGCRGB.log");
                    TrySet(alt);
                }
                catch
                {
                }
            }
        }

        private static bool TrySet(string path)
        {
            try
            {
                string dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
                File.AppendAllText(path, "", new UTF8Encoding(false));
                _path = path;
                if (File.Exists(path)) _written = new FileInfo(path).Length;
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static void Info(string message)
        {
            Write("INFO", message);
        }

        public static void Error(string message)
        {
            Write("ERROR", message);
        }

        public static void Error(string message, Exception ex)
        {
            Write("ERROR", message + " :: " + ex);
        }

        private static void Write(string level, string message)
        {
            lock (Lock)
            {
                if (_path == null) return;
                try
                {
                    if (_written > 512 * 1024)
                    {
                        string old = _path + ".old";
                        if (File.Exists(old)) File.Delete(old);
                        File.Move(_path, old);
                        _written = 0;
                    }
                    string line = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " [" + level + "] " + message
                                  + Environment.NewLine;
                    File.AppendAllText(_path, line, new UTF8Encoding(false));
                    _written += Encoding.UTF8.GetByteCount(line);
                }
                catch
                {
                }
            }
        }
    }
}
