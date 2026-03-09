using PromptMasterv6.Core.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace PromptMasterv6.Features.Launcher
{
    public class LauncherService : ILauncherService
    {
        private List<LauncherItem> _cache = new();

        public async Task<List<LauncherItem>> GetItemsAsync(IEnumerable<string> paths)
        {
            if (_cache != null && _cache.Any())
            {
                return _cache.ToList();
            }

            return await Task.Run(() =>
            {
                var items = new List<LauncherItem>();
                if (paths == null) return items;

                foreach (var path in paths)
                {
                    if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path)) continue;

                    try
                    {
                        var files = Directory.GetFiles(path, "*.*", SearchOption.TopDirectoryOnly)
                            .Where(f => IsLaunchable(f));

                        foreach (var file in files)
                        {
                            var baseName = Path.GetFileNameWithoutExtension(file);
                            var dirPath = Path.GetDirectoryName(file);
                            var customPngPath = Path.Combine(dirPath!, $"{baseName}.png");
                            
                            var item = new LauncherItem
                            {
                                Title = baseName,
                                FilePath = file,
                                IconPath = file,
                                Category = GetCategoryFromExtension(file)
                            };
                            
                            if (File.Exists(customPngPath))
                            {
                                item.CustomImagePath = customPngPath;
                            }
                            
                            items.Add(item);
                        }
                    }
                    catch (Exception ex)
                    {
                        Infrastructure.Services.LoggerService.Instance.LogException(ex, $"Failed to scan directory during launcher discovery: {path}", "LauncherService.GetItemsAsync");
                    }
                }

                _cache = items.ToList();
                return items;
            });
        }

        public void ClearCache()
        {
            _cache.Clear();
        }

        private bool IsLaunchable(string filePath)
        {
            var ext = Path.GetExtension(filePath).ToLower();
            return ext == ".exe" || ext == ".lnk" || ext == ".bat" || ext == ".cmd" || ext == ".ps1" || ext == ".url" || ext == ".vbs" || ext == ".ahk" || ext == ".website";
        }
        
        private LauncherCategory GetCategoryFromExtension(string filePath)
        {
            var ext = Path.GetExtension(filePath).ToLower();
            if (ext == ".url" || ext == ".website") 
                return LauncherCategory.Bookmark;
            
            if (ext == ".bat" || ext == ".cmd" || ext == ".ps1" || ext == ".vbs" || ext == ".ahk")
                return LauncherCategory.Tool;
                
            return LauncherCategory.Application;
        }
    }
}
