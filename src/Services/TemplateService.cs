using System.Text.Json;
using FileLockApp.Models;
using System.IO;

namespace FileLockApp.Services
{
    public static class TemplateService
    {
        public static void Export(LockGroup group, string filePath)
        {
            var template = new GroupTemplate
            {
                Name = group.Name,
                IncludeSubfolders = group.IncludeSubfolders,
                RequirePasswordEachDelete = group.RequirePasswordEachDelete,
                PasswordGraceMinutes = group.PasswordGraceMinutes
            };
            var json = JsonSerializer.Serialize(template, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(filePath, json);
        }

        public static GroupTemplate? Import(string filePath)
        {
            var json = File.ReadAllText(filePath);
            return JsonSerializer.Deserialize<GroupTemplate>(json);
        }
    }
}
