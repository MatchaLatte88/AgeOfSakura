using System;
using System.IO;
using System.Text;
using AgeOfSakura.Core;
using UnityEngine;

namespace AgeOfSakura.Game
{
    /// <summary>
    /// Save file under Application.persistentDataPath (works on Android and iOS; no platform-specific paths).
    /// Writes go to a temp file first so a crash mid-write cannot destroy the previous save.
    /// </summary>
    public sealed class FileSaveStorage : ISaveStorage
    {
        private readonly string path;

        public string FilePath => path;

        public FileSaveStorage(string fileName = "save.json")
        {
            path = Path.Combine(Application.persistentDataPath, fileName);
        }

        public bool Exists() => File.Exists(path);

        public string Read() => File.ReadAllText(path, Encoding.UTF8);

        public void Write(string contents)
        {
            string temp = path + ".tmp";
            File.WriteAllText(temp, contents, Encoding.UTF8);
            File.Copy(temp, path, true);
            File.Delete(temp);
        }

        public void Delete()
        {
            if (File.Exists(path)) File.Delete(path);
            if (File.Exists(path + ".tmp")) File.Delete(path + ".tmp");
        }

        public void PreserveCorrupt(string contents)
        {
            string target = path + ".corrupt-" + DateTime.UtcNow.ToString("yyyyMMddTHHmmss");
            File.WriteAllText(target, contents ?? string.Empty, Encoding.UTF8);
            GameLog.Warn(LogCategory.Save, "Unreadable save preserved at " + target);
        }
    }
}
