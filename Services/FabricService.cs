using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PromptMasterv5.Core.Models;
using PromptMasterv5.Core.Interfaces;

namespace PromptMasterv5.Services
{
    public class FabricService
    {
        private readonly string _patternsPath;
        private List<string> _cachedPatternNames = new();

        public FabricService()
        {
            // 设定 patterns 文件夹在程序运行目录下
            _patternsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "patterns");
            RefreshPatterns();
        }

        public void RefreshPatterns()
        {
            if (Directory.Exists(_patternsPath))
            {
                // 获取所有子文件夹名称作为模式名
                _cachedPatternNames = Directory.GetDirectories(_patternsPath)
                                               .Select(Path.GetFileName)
                                               .Where(n => !string.IsNullOrEmpty(n))
                                               .ToList()!;
            }
        }

        /// <summary>
        /// 核心方法：根据用户输入，找出最匹配的模式，并读取其 system.md 内容
        /// </summary>
        public async Task<string> FindBestPatternAndContentAsync(string userInput, IAiService aiService, AppConfig config)
        {
            if (_cachedPatternNames.Count == 0)
            {
                return $"[错误] 未在 {_patternsPath} 下找到任何模式文件夹。请确认已下载 Fabric patterns。";
            }

            // 1. 构造路由提示词 (让 DeepSeek 从列表中选一个)
            string patternList = string.Join(", ", _cachedPatternNames);

            // 限制列表长度，防止 token 溢出 (DeepSeek V3通常没问题，但为了保险)
            if (patternList.Length > 10000) patternList = patternList.Substring(0, 10000) + "...";

            string routerSystemPrompt = $@"You are a semantic router. 
Your task is to select the BEST matching pattern name from the following list based on the user's input.
PATTERN LIST: [{patternList}]

Rules:
1. Return ONLY the pattern name. Do not add any explanation or punctuation.
2. If no pattern matches well, return 'default'.";

            // 2. 调用 AI 进行决策
            string selectedPatternName = await aiService.ChatAsync(userInput, config, routerSystemPrompt);

            // 清理返回结果 (防止 AI 这里加句号或空格)
            selectedPatternName = selectedPatternName.Trim().TrimEnd('.').TrimEnd('。');

            if (string.Equals(selectedPatternName, "default", StringComparison.OrdinalIgnoreCase) ||
                !_cachedPatternNames.Contains(selectedPatternName))
            {
                // 如果没匹配到，或者 AI 乱造了一个名字，返回空，表示不用 Fabric 模式
                return "";
            }

            // 3. 读取该模式的 system.md
            string systemMdPath = Path.Combine(_patternsPath, selectedPatternName, "system.md");
            if (File.Exists(systemMdPath))
            {
                string content = await File.ReadAllTextAsync(systemMdPath);

                // ★★★ 关键：Fabric 原生是英文，我们需要强行注入“请输出中文”的指令 ★★★
                // 在 System Prompt 末尾追加指令，确保最终执行时输出中文
                return content + "\n\n# OUTPUT INSTRUCTIONS\nIMPORTANT: Please output the final response in Simplified Chinese unless the user explicitly asks for another language.";
            }

            return "";
        }
    }
}