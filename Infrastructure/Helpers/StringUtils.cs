using System.Linq;

namespace PromptMasterv5.Infrastructure.Helpers
{
    /// <summary>
    /// 字符串工具类
    /// 提供常用的字符串处理方法
    /// </summary>
    public static class StringUtils
    {
        /// <summary>
        /// 标准化符号 - 将中文标点和特殊符号转换为英文标点
        /// 用于搜索匹配时的符号统一
        /// </summary>
        public static string NormalizeSymbols(string s)
        {
            return new string((s ?? "").Select(NormalizeSymbol).ToArray());
        }

        /// <summary>
        /// 单个字符标准化
        /// </summary>
        private static char NormalizeSymbol(char c)
        {
            return c switch
            {
                '\uFF1B' => ';',  // 全角分号
                '\uFF07' => '\'', // 全角单引号
                '\u2018' => '\'', // 左单引号
                '\u2019' => '\'', // 右单引号
                '`' => '\'',
                '\u00B4' => '\'', // 锐音符
                _ => c
            };
        }
    }
}
