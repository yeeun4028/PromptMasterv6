using PromptMasterv6.Core.Interfaces;
using PromptMasterv6.Core.Models;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.RegularExpressions;

namespace PromptMasterv6.Infrastructure.Services;

public class VariableService : IVariableService
{
    private static readonly Regex VariableRegex = new(@"\{\{(.*?)\}\}", RegexOptions.Compiled);

    public void ParseVariables(string content, ObservableCollection<VariableItem> variables)
    {
        if (string.IsNullOrEmpty(content))
        {
            variables.Clear();
            return;
        }

        var matches = VariableRegex.Matches(content);
        var newVarNames = matches.Cast<Match>()
            .Select(m => m.Groups[1].Value.Trim())
            .Where(s => !string.IsNullOrEmpty(s))
            .Distinct()
            .ToList();

        for (int i = variables.Count - 1; i >= 0; i--)
        {
            if (!newVarNames.Contains(variables[i].Name)) variables.RemoveAt(i);
        }

        foreach (var name in newVarNames)
        {
            if (!variables.Any(v => v.Name == name)) variables.Add(new VariableItem { Name = name });
        }
    }

    public string CompileContent(string? content, ObservableCollection<VariableItem> variables, string? additionalInput = null)
    {
        string finalContent = content ?? "";
        foreach (var variable in variables)
        {
            finalContent = finalContent.Replace("{{" + variable.Name + "}}", variable.Value ?? "");
        }

        if (!string.IsNullOrEmpty(additionalInput))
        {
            finalContent = finalContent.Replace("{{input}}", additionalInput);
        }

        return finalContent;
    }

    public bool HasVariables(ObservableCollection<VariableItem> variables)
    {
        return variables.Count > 0;
    }
}
