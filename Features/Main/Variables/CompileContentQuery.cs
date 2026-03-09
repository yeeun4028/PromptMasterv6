using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace PromptMasterv6.Features.Main.Variables
{
    public record CompileContentQuery(string? Content, Dictionary<string, string> Variables, string? AdditionalInput = null) : IRequest<string>;

    public class CompileContentHandler : IRequestHandler<CompileContentQuery, string>
    {
        public Task<string> Handle(CompileContentQuery request, CancellationToken cancellationToken)
        {
            string finalContent = request.Content ?? "";
            
            if (request.Variables != null)
            {
                foreach (var kvp in request.Variables)
                {
                    finalContent = finalContent.Replace("{{" + kvp.Key + "}}", kvp.Value ?? "");
                }
            }

            if (!string.IsNullOrEmpty(request.AdditionalInput))
            {
                finalContent = finalContent.Replace("{{input}}", request.AdditionalInput);
            }

            return Task.FromResult(finalContent);
        }
    }
}
