using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;

namespace SaveSync
{
    internal sealed class GameProfile
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public string Exe { get; set; }
        public string Saves { get; set; }
        public string Drive { get; set; }

        public void Normalize()
        {
            Name = (Name ?? "").Trim();
            if (Id == Guid.Empty || Name.Length == 0)
                throw new ArgumentException("Укажите название игры.");

            Exe = FullPath(Exe);
            if (!Path.GetExtension(Exe).Equals(".exe", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Выберите EXE игры.");

            Saves = FullPath(Saves);
            Drive = FullPath(Drive);
            if (Overlaps(Saves, Drive))
                throw new ArgumentException("Папки сохранений и Google Диска должны быть отдельными и не вложенными друг в друга.");
        }

        private static string FullPath(string value)
        {
            string path = Environment.ExpandEnvironmentVariables((value ?? "").Trim().Trim('"'));
            if (path.Length < 3 || path[1] != ':' || path[2] != '\\')
                throw new ArgumentException("Укажите полный путь на локальном диске, например C:\\Игры\\Сохранения.");

            path = Path.GetFullPath(path);
            if (path == Path.GetPathRoot(path))
                throw new ArgumentException("Выберите папку внутри диска.");
            return path.TrimEnd('\\', '/');
        }

        public static bool Overlaps(string left, string right)
        {
            return (left + "\\").StartsWith(right + "\\", StringComparison.OrdinalIgnoreCase)
                || (right + "\\").StartsWith(left + "\\", StringComparison.OrdinalIgnoreCase);
        }
    }

    internal sealed class ProfileStore
    {
        private readonly JavaScriptSerializer json = new JavaScriptSerializer();
        private readonly string file;

        public ProfileStore(string directory)
        {
            DirectoryPath = directory;
            file = Path.Combine(directory, "profiles.json");
        }

        public string DirectoryPath { get; private set; }

        public List<GameProfile> Load()
        {
            if (!File.Exists(file))
                return new List<GameProfile>();

            var profiles = json.Deserialize<List<GameProfile>>(File.ReadAllText(file, Encoding.UTF8));
            if (profiles == null)
                throw new InvalidDataException("Файл профилей пуст.");

            var ids = new HashSet<Guid>();
            foreach (var profile in profiles)
            {
                if (profile == null || !ids.Add(profile.Id))
                    throw new InvalidDataException("Файл профилей повреждён.");
                Validate(profile);
            }
            return profiles;
        }

        public void Validate(GameProfile profile)
        {
            profile.Normalize();
            if (GameProfile.Overlaps(profile.Saves, DirectoryPath) || GameProfile.Overlaps(profile.Drive, DirectoryPath))
                throw new ArgumentException("Выберите папки за пределами каталога настроек SaveSync.");
        }

        public void Save(List<GameProfile> profiles)
        {
            Directory.CreateDirectory(DirectoryPath);
            var temporary = file + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.WriteAllText(temporary, json.Serialize(profiles), Encoding.UTF8);
                if (File.Exists(file))
                    File.Replace(temporary, file, null);
                else
                    File.Move(temporary, file);
            }
            finally
            {
                File.Delete(temporary);
            }
        }
    }
}
