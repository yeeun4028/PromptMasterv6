using MediatR;
using PromptMasterv6.Infrastructure.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace PromptMasterv6.Features.Launcher.Queries
{
    public record GetLauncherItemsQuery(IEnumerable<string> Paths, bool ForceRefresh = false) : IRequest<List<LauncherItem>>;

    public class GetLauncherItemsHandler : IRequestHandler<GetLauncherItemsQuery, List<LauncherItem>>
    {
        private readonly LoggerService _logger;
        
        private List<LauncherItem> _cache = new();
        private readonly object _cacheLock = new();
        private int _cacheHash;

        public GetLauncherItemsHandler(LoggerService logger)
        {
            _logger = logger;
        }

        public async Task<List<LauncherItem>> Handle(GetLauncherItemsQuery request, CancellationToken cancellationToken)
        {
            var pathsList = request.Paths?.ToList() ?? new List<string>();
            var currentHash = ComputePathsHash(pathsList);

            if (!request.ForceRefresh)
            {
                lock (_cacheLock)
                {
                    if (_cache.Count > 0 && _cacheHash == currentHash)
                    {
                        return _cache.ToList();
                    }
                }
            }

            var items = new List<LauncherItem>();

            await Task.Run(() =>
            {
                foreach (var path in pathsList)
                {
                    if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path)) continue;

                    try
                    {
                        var files = Directory.GetFiles(path, "*.*", SearchOption.TopDirectoryOnly)
                            .Where(IsLaunchable);

                        foreach (var file in files)
                        {
                            var item = new LauncherItem
                            {
                                FilePath = file,
                                Title = Path.GetFileNameWithoutExtension(file),
                                Category = GetCategoryFromExtension(file)
                            };

                            items.Add(item);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogException(ex, $"Failed to Scan Directory during launcher discovery: {path}", "GetLauncherItemsHandler.Handle");
                    }
                }
            }, cancellationToken);

            lock (_cacheLock)
            {
                _cache = items.ToList();
                _cacheHash = currentHash;
            }

            return items;
        }

        private int ComputePathsHash(List<string> paths)
        {
            if (paths == null || paths.Count == 0) return 0;
            
            unchecked
            {
                int hash = 17;
                foreach (var path in paths.OrderBy(p => p))
                {
                    hash = hash * 31 + (path?.GetHashCode() ?? 0);
                }
                return hash;
            }
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
