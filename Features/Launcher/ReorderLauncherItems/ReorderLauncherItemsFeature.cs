using MediatR;
using PromptMasterv6.Features.Launcher.Orders;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace PromptMasterv6.Features.Launcher.ReorderLauncherItems;

public static class ReorderLauncherItemsFeature
{
    // 1. 定义输入
    public record Command(
        IList<LauncherItem> Items,
        LauncherItem Source,
        LauncherItem Target) : IRequest<Result>;

    // 2. 定义输出
    public record Result(bool Success, Dictionary<string, int>? UpdatedOrders, string? ErrorMessage);

    // 3. 执行逻辑
    public class Handler : IRequestHandler<Command, Result>
    {
        private readonly IMediator _mediator;

        public Handler(IMediator mediator)
        {
            _mediator = mediator;
        }

        public async Task<Result> Handle(Command request, CancellationToken cancellationToken)
        {
            if (request.Items == null || request.Source == null || request.Target == null)
            {
                return new Result(false, null, "参数无效");
            }

            if (request.Source == request.Target)
            {
                return new Result(false, null, "源和目标相同");
            }

            var oldIndex = request.Items.IndexOf(request.Source);
            var newIndex = request.Items.IndexOf(request.Target);

            if (oldIndex < 0 || newIndex < 0)
            {
                return new Result(false, null, "找不到源或目标项目");
            }

            // 执行移动
            if (request.Items is System.Collections.ObjectModel.ObservableCollection<LauncherItem> observableCollection)
            {
                observableCollection.Move(oldIndex, newIndex);
            }
            else
            {
                // 对于非 ObservableCollection，手动移动
                var item = request.Items[oldIndex];
                request.Items.RemoveAt(oldIndex);
                if (newIndex > oldIndex) newIndex--;
                request.Items.Insert(newIndex, item);
            }

            // 更新所有项目的 DisplayOrder
            var itemOrders = new Dictionary<string, int>();
            for (int i = 0; i < request.Items.Count; i++)
            {
                var item = request.Items[i];
                item.DisplayOrder = i;

                var key = $"{item.Category}_{item.Title}";
                itemOrders[key] = i;
            }

            // 保存订单
            await _mediator.Send(new SaveLauncherOrdersCommand(itemOrders));

            return new Result(true, itemOrders, null);
        }
    }
}
