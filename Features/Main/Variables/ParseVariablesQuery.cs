using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace PromptMasterv6.Features.Main.Variables
{
    public record ParseVariablesQuery(string? Content) : IRequest<List<string>>;

    public class ParseVariablesHandler : IRequestHandler<ParseVariablesQuery, List<string>>
    {
        private static readonly Regex VariableRegex = new(@"\{\{(.*?)\}\}", RegexOptions.Compiled);

        public Task<List<string>> Handle(ParseVariablesQuery request, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(request.Content))
            {
                return Task.FromResult(new List<string>());
            }

            var matches = VariableRegex.Matches(request.Content);
            var varNames = matches.Cast<Match>()
                .Select(m => m.Groups[1].Value.Trim())
                .Where(s => !string.IsNullOrEmpty(s))
                .Distinct()
                .ToList();

            return Task.FromResult(varNames);
        }
    }
}
