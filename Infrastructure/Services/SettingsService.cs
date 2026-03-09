using PromptMasterv6.Core.Interfaces;
using PromptMasterv6.Infrastructure.Services;
using System.IO.Compression;

namespace PromptMasterv6.Infrastructure.Services
{
    /// <summary>
    /// 配置管理服务实现
    /// 作为配置的单一真实来源，所有 ViewModel 通过此服务访问配置
    /// </summary>
    public class SettingsService : ISettingsService
    {
        // Icon path constants (extracted to avoid inline string issues)
        private static readonly string PerplexityIconData =
            "M905.728 304.512h-98.432V118.528a32.832 32.832 0 0 0-18.816-29.76 34.112 34.112 0 0 0-35.008 4.352L544.704 266.88V75.84A33.088 33.088 0 0 0 512 43.008a33.152 33.152 0 0 0-32.896 32.832v191.488l-208.64-174.208a32 32 0 0 0-35.008-4.352 32.832 32.832 0 0 0-18.816 29.76v186.048h-98.56a33.024 33.024 0 0 0-32.768 32.768v350.08c0 17.92 14.912 32.832 32.832 32.832h98.432v185.984a32.96 32.96 0 0 0 32.896 32.768 34.112 34.112 0 0 0 20.928-7.424l208.768-173.696v191.424c0 17.92 14.848 32.896 32.832 32.896 17.92 0 32.832-14.976 32.832-32.896v-191.424l208.704 173.696a34.112 34.112 0 0 0 20.992 7.488 32.832 32.832 0 0 0 32.832-32.832v-186.048h98.496c17.92 0 32.768-14.848 32.768-32.768V337.28a33.088 33.088 0 0 0-32.832-32.832z" +
            " m-164.032-115.968v115.968H602.496l139.2-115.968z" +
            " m-459.52 0l139.136 115.968H282.24V188.544z" +
            " m-131.328 466.112V370.176h281.856L226.176 576.64a32.832 32.832 0 0 0-9.6 23.168v54.72H150.784z" +
            " m131.328 32.832V613.504l196.928-196.928v255.552l-196.928 164.096v-148.736z" +
            " m459.52 148.736L544.64 672.128V416.576l196.992 196.928v222.72z" +
            " m131.2-181.632h-65.6v-54.656a32.64 32.64 0 0 0-9.6-23.168L591.104 370.24h281.792v284.416z";

        private static readonly string DeepSeekIconData =
            "M929.678222 254.122667c-9.045333-4.323556-12.913778 3.982222-18.204444 8.192" +
            "-1.763556 1.365333-3.299556 3.185778-4.835556 4.778666" +
            "-13.255111 13.937778-28.672 23.096889-48.810666 21.959111" +
            "-29.468444-1.592889-54.613333 7.509333-76.913778 29.809778" +
            "-4.721778-27.420444-20.48-43.804444-44.373334-54.385778" +
            "-12.515556-5.461333-25.144889-10.922667-33.905777-22.755555" +
            "-6.144-8.476444-7.793778-17.92-10.865778-27.192889" +
            "-1.934222-5.518222-3.868444-11.320889-10.410667-12.288" +
            "-7.111111-1.080889-9.898667 4.778667-12.686222 9.671111" +
            "-11.093333 20.081778-15.36 42.268444-15.018667 64.625778 " +
            "0.967111 50.346667 22.528 90.510222 65.365334 119.011555 " +
            "4.835556 3.299556 6.144 6.599111 4.551111 11.377778" +
            "-2.844444 9.784889-6.371556 19.342222-9.443556 29.240889" +
            "-1.877333 6.257778-4.835556 7.623111-11.662222 4.892445" +
            "a196.608 196.608 0 0 1-61.724444-41.415112" +
            "c-30.435556-29.013333-58.026667-61.098667-92.330667-86.243555" +
            "a403.285333 403.285333 0 0 0-24.462222-16.497778" +
            "c-35.043556-33.564444 4.551111-61.212444 13.710222-64.455111 " +
            "9.671111-3.413333 3.356444-15.189333-27.648-15.018667" +
            "-31.004444 0.113778-59.392 10.353778-95.573333 24.007111" +
            "a109.681778 109.681778 0 0 1-16.497778 4.778667 " +
            "345.656889 345.656889 0 0 0-102.513778-3.584" +
            "c-67.015111 7.395556-120.547556 38.684444-159.914667 92.046222" +
            "-47.274667 64.170667-58.424889 137.102222-44.828444 213.105778 " +
            "14.392889 80.156444 55.808 146.488889 119.466667 198.428445 " +
            "66.104889 53.76 142.222222 80.099556 228.920888 75.093333 " +
            "52.736-3.015111 111.388444-10.012444 177.607112-65.308445 " +
            "16.725333 8.192 34.190222 11.491556 63.317333 13.937778 " +
            "22.357333 2.048 43.918222-1.080889 60.586667-4.494222 " +
            "26.168889-5.461333 24.291556-29.354667 14.904888-33.678222" +
            "-76.686222-35.271111-59.790222-20.935111-75.093333-32.540445 " +
            "38.912-45.511111 97.564444-92.728889 120.547556-245.76 " +
            "1.763556-12.174222 0.284444-19.797333 0-29.582222" +
            "-0.113778-6.030222 1.251556-8.362667 8.192-9.102222 " +
            "19.228444-1.934222 37.888-7.566222 54.954666-16.611556 " +
            "49.607111-26.737778 69.688889-70.712889 74.410667-123.448889 " +
            "0.682667-7.964444-0.170667-16.384-8.817778-20.593777z" +
            " m-432.64 474.282666c-74.24-57.571556-110.250667-76.572444-125.155555-75.719111" +
            "-13.880889 0.796444-11.377778 16.497778-8.305778 26.737778 " +
            "3.242667 10.126222 7.395556 17.066667 13.255111 25.941333 " +
            "3.982222 5.859556 6.826667 14.620444-4.039111 21.162667" +
            "-23.893333 14.620444-65.536-4.949333-67.470222-5.859556" +
            "-48.355556-28.16-88.860444-65.308444-117.361778-116.053333" +
            "a350.947556 350.947556 0 0 1-46.193778-157.297778" +
            "c-0.682667-13.539556 3.356444-18.318222 17.009778-20.707555 " +
            "17.92-3.299556 36.408889-3.982222 54.328889-1.365334 " +
            "75.776 10.922667 140.344889 44.373333 194.389333 97.28 " +
            "30.890667 30.151111 54.272 66.218667 78.279111 101.489778 " +
            "25.6 37.376 53.191111 73.045333 88.177778 102.229334 " +
            "12.401778 10.24 22.243556 18.033778 31.744 23.779555" +
            "-28.501333 3.128889-76.060444 3.811556-108.657778-21.617778z" +
            " m35.669334-225.905777a10.808889 10.808889 0 0 1 14.677333-10.126223 " +
            "9.728 9.728 0 0 1 4.096 2.56 10.808889 10.808889 0 0 1-7.964445 18.318223 " +
            "10.695111 10.695111 0 0 1-10.808888-10.752z" +
            " m110.535111 55.978666a65.024 65.024 0 0 1-21.048889 5.518222 " +
            "44.657778 44.657778 0 0 1-28.330667-8.817777" +
            "c-9.671111-8.078222-16.668444-12.515556-19.626667-26.624" +
            "a59.278222 59.278222 0 0 1 0.568889-20.593778" +
            "c2.503111-11.491556-0.284444-18.887111-8.533333-25.6" +
            "-6.599111-5.404444-15.132444-6.940444-24.462222-6.940445" +
            "a20.081778 20.081778 0 0 1-8.988445-2.730666" +
            "c-3.868444-1.877333-7.111111-6.656-3.982222-12.515556" +
            "a40.049778 40.049778 0 0 1 6.826667-7.395555" +
            "c12.572444-7.054222 27.192889-4.778667 40.675555 0.568889 " +
            "12.515556 5.006222 21.959111 14.336 35.612445 27.420444 " +
            "13.937778 15.815111 16.384 20.252444 24.348444 32.085333 " +
            "6.257778 9.329778 11.946667 18.887111 15.872 29.809778 " +
            "2.275556 6.826667-0.739556 12.344889-8.931555 15.758222v0.056889z";

        private static readonly string DoubaoIconData =
            "M932.683294 471.883294c-145.046588-96.858353-297.803294-111.314824-297.803294-111.314823" +
            "s7.710118 221.184-10.119529 288.165647" +
            "c-0.481882 9.155765-11.083294 70.836706-40.478118 148.419764" +
            "a510.192941 510.192941 0 0 1-37.104941 76.619294" +
            "c-38.550588 69.872941-84.329412 119.988706-84.329412 119.988706" +
            " 112.760471-45.778824 193.234824-87.220706 291.538824-165.285647" +
            " 115.651765-91.075765 215.883294-239.495529 178.29647-356.592941z" +
            " M340.449882 430.441412c148.901647-94.930824 290.575059-74.209882 293.948236-70.354824" +
            " 0.481882 0.481882 0.963765 0.481882 0.963764 0.481883" +
            " 0-13.492706-6.264471-57.344-12.528941-100.23153" +
            "-11.083294-57.344-21.202824-117.579294-21.684706-125.771294" +
            "-160.466824-131.072-335.872-73.728-421.647059 24.094118" +
            "C121.675294 224.677647 82.160941 318.644706 83.124706 369.724235" +
            "v406.226824c0.481882-12.047059 1.445647-19.275294 1.927529-19.275294" +
            " 16.865882-185.524706 255.397647-326.234353 255.397647-326.234353z" +
            " M775.589647 314.307765c-39.514353-40.96-82.401882-83.365647-115.169882-118.061177" +
            "-31.322353-32.768-55.898353-57.344-59.27153-61.680941" +
            " 0.481882 8.192 10.601412 68.427294 21.684706 125.771294" +
            " 6.264471 42.887529 12.047059 87.220706 12.528941 100.23153" +
            " 0 0 152.756706 14.456471 297.803294 111.314823" +
            "-0.481882 0-78.546824-75.655529-157.575529-157.575529z" +
            " m-227.930353 559.465411c-2.409412 0.963765-186.970353 100.713412-275.154823-28.912941" +
            "-19.757176-28.912941-29.876706-64.090353-31.322353-100.231529" +
            "-2.891294-70.354824 4.818824-214.437647 99.267764-314.187294" +
            " 0 0-238.531765 140.709647-254.915764 326.234353" +
            "a65.174588 65.174588 0 0 0-1.92753 19.275294" +
            "c-0.963765 30.840471 4.336941 92.521412 40.478118 146.010353" +
            " 49.152 73.728 181.669647 130.590118 308.886588 80.474353" +
            "l31.322353-9.155765c-0.963765 0.481882 44.815059-49.152 83.365647-119.506824z";

        private static readonly string QwenIconData =
            "M532.877 143.756a16008.983 16008.983 0 0 1 40.556 71.681 6.218 6.218 0 0 0 5.424 3.144H770.65" +
            "c6.01 0 11.123 3.8 15.407 11.296l50.228 88.78c6.564 11.642 8.29 16.513 0.83 28.915" +
            "-8.983 14.854-17.722 29.847-26.255 44.908l-12.678 22.73c-3.662 6.772-7.704 9.673-1.382 17.688" +
            "l91.613 160.185c5.942 10.398 3.835 17.065-1.485 26.6a4552.76 4552.76 0 0 1-46.118 80.835" +
            "c-5.492 9.396-12.16 12.955-23.49 12.782-26.842-0.553-53.614-0.346-80.386 0.553" +
            "a3.42 3.42 0 0 0-2.799 1.727 19866.717 19866.717 0 0 1-93.444 163.743" +
            "c-5.838 10.122-13.127 12.54-25.045 12.575-34.441 0.105-69.16 0.138-104.222 0.069" +
            "a18.55 18.55 0 0 1-16.064-9.362l-46.117-80.248a3.11 3.11 0 0 0-2.868-1.693h-176.8" +
            "c-9.846 1.036-19.104-0.034-27.81-3.178l-55.375-95.69a18.758 18.758 0 0 1-0.07-18.654" +
            "l41.696-73.235a6.84 6.84 0 0 0 0-6.806c-21.72-37.603-43.31-75.28-64.771-113.031" +
            "l-27.291-48.19c-5.527-10.71-5.976-17.135 3.282-33.336a26965.952 26965.952 0 0 0 47.914-84.152" +
            "c4.56-8.084 10.501-11.538 20.174-11.573 29.812-0.125 59.625-0.137 89.437-0.034" +
            "a4.284 4.284 0 0 0 3.696-2.176l96.934-169.098a16.858 16.858 0 0 1 14.578-8.498" +
            "c18.101-0.035 36.376 0 54.684-0.208l35.133-0.8c11.78-0.098 25.01 1.111 31.09 11.751z" +
            "M414.32 157.678c-0.741 0-1.426 0.394-1.797 1.036l-99.006 173.243a5.424 5.424 0 0 1-4.663 2.695h-99.006" +
            "c-1.935 0-2.418 0.864-1.417 2.556l200.707 350.84c0.864 1.45 0.449 2.141-1.175 2.176" +
            "l-96.553 0.518a7.53 7.53 0 0 0-6.909 4.007l-45.6 79.8c-1.52 2.694-0.725 4.075 2.35 4.075" +
            "l197.459 0.277c1.59 0 2.764 0.69 3.593 2.107l48.466 84.773c1.59 2.799 3.179 2.833 4.802 0" +
            "l172.932-302.614 27.05-47.741a1.9 1.9 0 0 1 3.316 0l49.192 87.399a4.214 4.214 0 0 0 3.696 2.142" +
            "l95.448-0.691c0.498 0.004 0.96-0.26 1.209-0.691 0.24-0.43 0.24-0.953 0-1.382l-100.18-175.696" +
            "a3.73 3.73 0 0 1 0-3.903l10.121-17.515 38.69-68.295c0.83-1.417 0.415-2.142-1.209-2.142" +
            "H415.286c-2.038 0-2.522-0.898-1.485-2.66l49.537-86.535a3.696 3.696 0 0 0 0-3.938" +
            "l-47.188-82.77a2.085 2.085 0 0 0-1.831-1.071z m217.288 277.05c1.589 0 2.003 0.692 1.174 2.074" +
            "L604.04 487.41l-90.266 158.389a1.935 1.935 0 0 1-1.727 1.002 2.004 2.004 0 0 1-1.728-1.002" +
            "L391.035 437.423c-0.69-1.174-0.345-1.796 0.968-1.865l7.461-0.415 232.212-0.414h-0.07z";

        public AppConfig Config { get; private set; }
        public LocalSettings LocalConfig { get; private set; }

        public SettingsService()
        {
            // 启动时加载配置
            Config = ConfigService.Load();
            LocalConfig = LocalConfigService.Load();

            InitializeDefaultWebTargets();

            LoggerService.Instance.LogInfo("Settings loaded successfully", "SettingsService.ctor");
        }

        private void InitializeDefaultWebTargets()
        {
            if (Config.WebDirectTargets == null) Config.WebDirectTargets = new();

            bool added = false;

            void AddIfMissing(string name, string url, string icon)
            {
                if (!Config.WebDirectTargets.Any(t => t.Name.Equals(name, System.StringComparison.OrdinalIgnoreCase)))
                {
                    Config.WebDirectTargets.Add(new WebTarget { Name = name, UrlTemplate = url, IconData = icon });
                    added = true;
                }
            }

            // 1. ChatGPT
            AddIfMissing("ChatGPT", "https://chat.openai.com/?q={0}", 
                "M12,2L20.66,7V17L12,22L3.34,17V7L12,2Z");

            // 2. Claude
            AddIfMissing("Claude", "https://claude.ai/new?q={0}", 
                "M12,2A10,10 0 1,0 22,12A10,10 0 0,0 12,2M17,15.5L15.5,17A8,8 0 1,1 15.5,7L17,8.5A6,6 0 1,0 17,15.5Z");

            // 3. Gemini (Google)
            AddIfMissing("Gemini", "https://gemini.google.com/app?q={0}", 
                "M12,2L14.5,9.5L22,12L14.5,14.5L12,22L9.5,14.5L2,12L9.5,9.5Z");

            // 4. Perplexity
            AddIfMissing("Perplexity", "https://www.perplexity.ai/?q={0}", PerplexityIconData);

            // 5. DeepSeek (深度求索)
            AddIfMissing("DeepSeek", "https://chat.deepseek.com?q={0}", DeepSeekIconData);

            // 6. GLM (智谱清言)
            AddIfMissing("GLM", "https://chatglm.cn/main/all?q={0}",
                "M20,2H4C2.9,2 2,2.9 2,4V22L6,18H20C21.1,18 22,17.1 22,16V4C22,2.9 21.1,2 20,2M20,16H5.17L4,17.17V4H20V16Z");

            // 7. Qwen (通义千问)
            AddIfMissing("Qwen", "https://tongyi.aliyun.com/qianwen?q={0}", QwenIconData);

            // 8. Doubao (豆包)
            AddIfMissing("Doubao", "https://www.doubao.com/chat/?q={0}", DoubaoIconData);

            // 9. AI Studio (Google)
            AddIfMissing("AI Studio", "https://aistudio.google.com/prompts/new_chat?q={0}",
                "M12,2L14.5,9.5L22,12L14.5,14.5L12,22L9.5,14.5L2,12L9.5,9.5Z M12,8L13,10.5L15.5,11.5L13,12.5L12,15L11,12.5L8.5,11.5L11,10.5Z");

            // Migration: Ensure all targets have ?q={0} (for Userscript support)
            bool needsSave = false;
            foreach (var target in Config.WebDirectTargets)
            {
                // Update Perplexity Icon (Migration)
                if (target.Name.Equals("Perplexity", System.StringComparison.OrdinalIgnoreCase))
                {
                    if (!target.IconData.Equals(PerplexityIconData))
                    {
                        target.IconData = PerplexityIconData;
                        needsSave = true;
                    }
                }

                // Update DeepSeek Icon (Migration)
                if (target.Name.Equals("DeepSeek", System.StringComparison.OrdinalIgnoreCase))
                {
                    if (!target.IconData.Equals(DeepSeekIconData))
                    {
                        target.IconData = DeepSeekIconData;
                        needsSave = true;
                    }
                }

                // Update Qwen Icon (Migration)
                if (target.Name.Equals("Qwen", System.StringComparison.OrdinalIgnoreCase))
                {
                    if (!target.IconData.Equals(QwenIconData))
                    {
                        target.IconData = QwenIconData;
                        needsSave = true;
                    }
                }

                // Update Doubao Icon (Migration)
                if (target.Name.Equals("Doubao", System.StringComparison.OrdinalIgnoreCase))
                {
                    if (!target.IconData.Equals(DoubaoIconData))
                    {
                        target.IconData = DoubaoIconData;
                        needsSave = true;
                    }
                }

                // List of targets that should have query params now
                var scriptTargets = new[] { "Gemini", "DeepSeek", "GLM", "Qwen", "Doubao", "AI Studio" };
                
                if (scriptTargets.Contains(target.Name, StringComparer.OrdinalIgnoreCase) && !target.UrlTemplate.Contains("{0}"))
                {
                    if (target.Name == "Gemini") target.UrlTemplate = "https://gemini.google.com/app?q={0}";
                    if (target.Name == "DeepSeek") target.UrlTemplate = "https://chat.deepseek.com?q={0}";
                    if (target.Name == "GLM") target.UrlTemplate = "https://chatglm.cn/main/all?q={0}";
                    if (target.Name == "Qwen") target.UrlTemplate = "https://tongyi.aliyun.com/qianwen?q={0}";
                    if (target.Name == "Doubao") target.UrlTemplate = "https://www.doubao.com/chat/?q={0}";
                    if (target.Name == "AI Studio") target.UrlTemplate = "https://aistudio.google.com/prompts/new_chat?q={0}";
                    needsSave = true;
                }
            }

            // 仅在有实际变化时才写盘（避免启动时无意义的重复保存）
            if (added || needsSave) SaveConfig();
        }

        public void SaveConfig()
        {
            try
            {
                ConfigService.Save(Config);
                LoggerService.Instance.LogInfo("AppConfig saved", "SettingsService.SaveConfig");
            }
            catch (System.Exception ex)
            {
                LoggerService.Instance.LogException(ex, "Failed to save AppConfig", "SettingsService.SaveConfig");
                throw;
            }
        }

        public void SaveLocalConfig()
        {
            try
            {
                LocalConfigService.Save(LocalConfig);
                LoggerService.Instance.LogInfo("LocalSettings saved", "SettingsService.SaveLocalConfig");
            }
            catch (System.Exception ex)
            {
                LoggerService.Instance.LogException(ex, "Failed to save LocalSettings", "SettingsService.SaveLocalConfig");
                throw;
            }
        }

        public void ReloadConfigs()
        {
            try
            {
                Config = ConfigService.Load();
                LocalConfig = LocalConfigService.Load();
                LoggerService.Instance.LogInfo("Settings reloaded", "SettingsService.ReloadConfigs");
            }
            catch (System.Exception ex)
            {
                LoggerService.Instance.LogException(ex, "Failed to reload settings", "SettingsService.ReloadConfigs");
                throw;
            }
        }

        public void ExportSettings(string zipPath)
        {
            try
            {
                // 先保存当前内存中的配置到磁盘，确保导出的是最新状态
                SaveConfig();
                SaveLocalConfig();

                if (System.IO.File.Exists(zipPath))
                {
                    System.IO.File.Delete(zipPath);
                }

                // 创建临时目录来存放要打包的文件
                // 虽然可以直接从ConfigPath打包，但为了扩展性和安全性，显式指定要打包的文件更稳妥
                // 这里我们直接利用 System.IO.Compression.ZipFile 的 CreateFromDirectory 或者逐个添加
                // 由于只打包两个特定文件，手动创建 zip 更灵活

                using (var archive = System.IO.Compression.ZipFile.Open(zipPath, System.IO.Compression.ZipArchiveMode.Create))
                {
                    string configPath = System.IO.Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "config.json");
                    string localConfigPath = System.IO.Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "local_settings.json");

                    if (System.IO.File.Exists(configPath))
                    {
                        archive.CreateEntryFromFile(configPath, "config.json");
                    }

                    if (System.IO.File.Exists(localConfigPath))
                    {
                        archive.CreateEntryFromFile(localConfigPath, "local_settings.json");
                    }
                }

                LoggerService.Instance.LogInfo($"Settings exported to {zipPath}", "SettingsService.ExportSettings");
            }
            catch (System.Exception ex)
            {
                LoggerService.Instance.LogException(ex, "Failed to export settings", "SettingsService.ExportSettings");
                throw;
            }
        }

        public void ImportSettings(string zipPath)
        {
            try
            {
                if (!System.IO.File.Exists(zipPath))
                {
                    throw new System.IO.FileNotFoundException("Import file not found", zipPath);
                }

                using (var archive = System.IO.Compression.ZipFile.OpenRead(zipPath))
                {
                    var configEntry = archive.GetEntry("config.json");
                    var localConfigEntry = archive.GetEntry("local_settings.json");

                    if (configEntry == null && localConfigEntry == null)
                    {
                        throw new System.Exception("The selected file does not contain valid configuration files (config.json or local_settings.json).");
                    }

                    string baseDir = System.AppDomain.CurrentDomain.BaseDirectory;

                    if (configEntry != null)
                    {
                        string targetPath = System.IO.Path.Combine(baseDir, "config.json");
                        configEntry.ExtractToFile(targetPath, overwrite: true);
                    }

                    if (localConfigEntry != null)
                    {
                        string targetPath = System.IO.Path.Combine(baseDir, "local_settings.json");
                        localConfigEntry.ExtractToFile(targetPath, overwrite: true);
                    }
                }

                // 导入后重新加载内存中的配置
                ReloadConfigs();

                LoggerService.Instance.LogInfo($"Settings imported from {zipPath}", "SettingsService.ImportSettings");
            }
            catch (System.Exception ex)
            {
                LoggerService.Instance.LogException(ex, "Failed to import settings", "SettingsService.ImportSettings");
                throw;
            }
        }
    }
}
