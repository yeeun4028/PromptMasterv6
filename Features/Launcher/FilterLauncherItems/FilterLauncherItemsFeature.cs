using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace PromptMasterv6.Features.Launcher.FilterLauncherItems;

public static class FilterLauncherItemsFeature
{
    // 1. 定义输入
    public record Command(
        IEnumerable<LauncherItem> Items,
        Dictionary<string, int> ItemOrders,
        string CurrentCategory,
        bool IsSinglePageDisplay) : IRequest<Result>;

    // 2. 定义输出
    public record Result(
        bool Success,
        List<LauncherItem>? Bookmarks,
        List<LauncherItem>? Applications,
        List<LauncherItem>? Tools,
        List<LauncherItem>? FilteredItems,
        string? ErrorMessage);

    // 3. 执行逻辑
    public class Handler : IRequestHandler<Command, Result>
    {
        public Task<Result> Handle(Command request, CancellationToken cancellationToken)
        {
            if (request.Items == null)
            {
                return Task.FromResult(new Result(false, null, null, null, null, "项目列表为空"));
            }

            var items = request.Items.ToList();

            // 应用排序
            foreach (var item in items)
            {
                var key = $"{item.Category}_{item.Title}";
                if (request.ItemOrders != null && request.ItemOrders.TryGetValue(key, out var order))
                {
                    item.DisplayOrder = order;
                }
                else
                {
                    item.DisplayOrder = int.MaxValue;
                }
            }

            try
            {
                if (request.IsSinglePageDisplay)
                {
                    // 单页模式：返回所有分类
                    var bookmarks = items
                        .Where(i => i.Category == LauncherCategory.Bookmark)
                        .OrderBy(i => i.DisplayOrder)
                        .ToList();

                    var applications = items
                        .Where(i => i.Category == LauncherCategory.Application)
                        .OrderBy(i => i.DisplayOrder)
                        .ToList();

                    var tools = items
                        .Where(i => i.Category == LauncherCategory.Tool)
                        .OrderBy(i => i.DisplayOrder)
                        .ToList();

                    return Task.FromResult(new Result(true, bookmarks, applications, tools, null, null));
                }
                else
                {
                    // 分类模式：返回当前分类
                    var enumCategory = request.CurrentCategory switch
                    {
                        "Bookmark" => LauncherCategory.Bookmark,
                        "Application" => LauncherCategory.Application,
                        "Tool" => LauncherCategory.Tool,
                        _ => LauncherCategory.Bookmark
                    };

                    var filtered = items
                        .Where(i => i.Category == enumCategory)
                        .OrderBy(i => i.DisplayOrder)
                        .ToList();

                    return Task.FromResult(new Result(true, null, null, null, filtered, null));
                }
            }
            catch (Exception ex)
            {
                return Task.FromResult(new Result(false, null, null, null, null, ex.Message));
            }
        }
    }
}
