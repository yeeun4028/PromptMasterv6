using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using PromptMasterv6.Core.Interfaces;
using PromptMasterv6.Core.Models;
using PromptMasterv6.Infrastructure.Services;
using PromptMasterv6.Features.ExternalTools.Messages;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace PromptMasterv6.Features.Settings.ApiProviders;

public partial class ApiProvidersViewModel : ObservableObject
{
    private readonly ISettingsService _settingsService;
    private readonly IBaiduService _baiduService;
    private readonly ITencentService _tencentService;
    private readonly IGoogleService _googleService;

    public AppConfig Config => _settingsService.Config;

    #region Baidu Credentials
    [ObservableProperty] private string? baiduOcrApiKey;
    [ObservableProperty] private string? baiduOcrSecretKey;
    [ObservableProperty] private string? baiduTranslateAppId;
    [ObservableProperty] private string? baiduTranslateSecretKey;
    #endregion

    #region Tencent Credentials
    [ObservableProperty] private string? tencentOcrSecretId;
    [ObservableProperty] private string? tencentOcrSecretKey;
    [ObservableProperty] private string? tencentTranslateSecretId;
    [ObservableProperty] private string? tencentTranslateSecretKey;
    #endregion

    #region Youdao Credentials
    [ObservableProperty] private string? youdaoOcrAppKey;
    [ObservableProperty] private string? youdaoOcrAppSecret;
    [ObservableProperty] private string? youdaoTranslateAppKey;
    [ObservableProperty] private string? youdaoTranslateAppSecret;
    #endregion

    #region Google Credentials
    [ObservableProperty] private string? googleBaseUrl;
    [ObservableProperty] private string? googleApiKey;
    #endregion

    #region Test Status
    [ObservableProperty] private string? baiduOcrTestStatus;
    [ObservableProperty] private System.Windows.Media.Brush baiduOcrTestStatusColor = System.Windows.Media.Brushes.Gray;
    [ObservableProperty] private string? baiduTranslateTestStatus;
    [ObservableProperty] private System.Windows.Media.Brush baiduTranslateTestStatusColor = System.Windows.Media.Brushes.Gray;
    [ObservableProperty] private string? tencentOcrTestStatus;
    [ObservableProperty] private System.Windows.Media.Brush tencentOcrTestStatusColor = System.Windows.Media.Brushes.Gray;
    [ObservableProperty] private string? tencentTranslateTestStatus;
    [ObservableProperty] private System.Windows.Media.Brush tencentTranslateTestStatusColor = System.Windows.Media.Brushes.Gray;
    [ObservableProperty] private string? youdaoTestStatus;
    [ObservableProperty] private System.Windows.Media.Brush youdaoTestStatusColor = System.Windows.Media.Brushes.Gray;
    [ObservableProperty] private string? googleTestStatus;
    [ObservableProperty] private System.Windows.Media.Brush googleTestStatusColor = System.Windows.Media.Brushes.Gray;
    #endregion

    public ApiProvidersViewModel(
        ISettingsService settingsService,
        IBaiduService baiduService,
        ITencentService tencentService,
        IGoogleService googleService)
    {
        _settingsService = settingsService;
        _baiduService = baiduService;
        _tencentService = tencentService;
        _googleService = googleService;

        LoadAllCredentials();
    }

    private void LoadAllCredentials()
    {
        LoadBaiduCredentials();
        LoadTencentCredentials();
        LoadYoudaoCredentials();
        LoadGoogleCredentials();
    }

    #region Baidu API Testing
    [RelayCommand]
    private async Task TestBaiduOcr()
    {
        SaveBaiduCredentials();

        var profile = Config.ApiProfiles.FirstOrDefault(p =>
            p.Provider == ApiProvider.Baidu && p.ServiceType == ServiceType.OCR);

        if (profile == null || string.IsNullOrWhiteSpace(profile.Key1) || string.IsNullOrWhiteSpace(profile.Key2))
        {
            BaiduOcrTestStatus = "请先填写 API Key 和 Secret Key";
            BaiduOcrTestStatusColor = System.Windows.Media.Brushes.Red;
            return;
        }

        BaiduOcrTestStatus = "测试中...";
        BaiduOcrTestStatusColor = System.Windows.Media.Brushes.Gray;

        try
        {
            byte[] testImage = CreateTestImage();
            var result = await _baiduService.OcrAsync(testImage, profile);

            if (result.StartsWith("错误") || result.Contains("错误"))
            {
                BaiduOcrTestStatus = $"连接失败：{result}";
                BaiduOcrTestStatusColor = System.Windows.Media.Brushes.Red;
            }
            else
            {
                BaiduOcrTestStatus = "连接成功！";
                BaiduOcrTestStatusColor = System.Windows.Media.Brushes.Green;
            }
        }
        catch (Exception ex)
        {
            BaiduOcrTestStatus = $"测试出错: {ex.Message}";
            BaiduOcrTestStatusColor = System.Windows.Media.Brushes.Red;
            LoggerService.Instance.LogException(ex, "Failed to test Baidu OCR", "ApiProvidersViewModel.TestBaiduOcr");
        }
    }

    [RelayCommand]
    private async Task TestBaiduTranslate()
    {
        SaveBaiduCredentials();

        var profile = Config.ApiProfiles.FirstOrDefault(p =>
            p.Provider == ApiProvider.Baidu && p.ServiceType == ServiceType.Translation);

        if (profile == null || string.IsNullOrWhiteSpace(profile.Key1) || string.IsNullOrWhiteSpace(profile.Key2))
        {
            BaiduTranslateTestStatus = "请先填写 App ID 和 Secret Key";
            BaiduTranslateTestStatusColor = System.Windows.Media.Brushes.Red;
            return;
        }

        BaiduTranslateTestStatus = "测试中...";
        BaiduTranslateTestStatusColor = System.Windows.Media.Brushes.Gray;

        try
        {
            var result = await _baiduService.TranslateAsync("Hello", profile, "en", "zh");

            if (result.StartsWith("错误") || result.Contains("错误") || result.Contains("异常"))
            {
                BaiduTranslateTestStatus = $"连接失败：{result}";
                BaiduTranslateTestStatusColor = System.Windows.Media.Brushes.Red;
            }
            else
            {
                BaiduTranslateTestStatus = $"连接成功！翻译结果：{result}";
                BaiduTranslateTestStatusColor = System.Windows.Media.Brushes.Green;
            }
        }
        catch (Exception ex)
        {
            BaiduTranslateTestStatus = $"测试出错: {ex.Message}";
            BaiduTranslateTestStatusColor = System.Windows.Media.Brushes.Red;
            LoggerService.Instance.LogException(ex, "Failed to test Baidu Translate", "ApiProvidersViewModel.TestBaiduTranslate");
        }
    }
    #endregion

    #region Tencent Cloud API Testing
    [RelayCommand]
    private async Task TestTencentOcr()
    {
        SaveTencentCredentials();

        var profile = Config.ApiProfiles.FirstOrDefault(p =>
            p.Provider == ApiProvider.Tencent && p.ServiceType == ServiceType.OCR);

        if (profile == null || string.IsNullOrWhiteSpace(profile.Key1) || string.IsNullOrWhiteSpace(profile.Key2))
        {
            TencentOcrTestStatus = "请先填写 Secret ID 和 Secret Key";
            TencentOcrTestStatusColor = System.Windows.Media.Brushes.Red;
            return;
        }

        TencentOcrTestStatus = "测试中...";
        TencentOcrTestStatusColor = System.Windows.Media.Brushes.Gray;

        try
        {
            var testImage = CreateTestImage();
            var result = await _tencentService.OcrAsync(testImage, profile);

            if (result.StartsWith("Error") || result.StartsWith("Tencent Error"))
            {
                TencentOcrTestStatus = $"连接失败：{result}";
                TencentOcrTestStatusColor = System.Windows.Media.Brushes.Red;
            }
            else
            {
                TencentOcrTestStatus = "连接成功！";
                TencentOcrTestStatusColor = System.Windows.Media.Brushes.Green;
            }
        }
        catch (Exception ex)
        {
            TencentOcrTestStatus = $"测试出错: {ex.Message}";
            TencentOcrTestStatusColor = System.Windows.Media.Brushes.Red;
            LoggerService.Instance.LogException(ex, "Failed to test Tencent OCR", "ApiProvidersViewModel.TestTencentOcr");
        }
    }

    [RelayCommand]
    private async Task TestTencentCloud()
    {
        SaveTencentCredentials();

        var profile = Config.ApiProfiles.FirstOrDefault(p =>
            p.Provider == ApiProvider.Tencent && p.ServiceType == ServiceType.Translation);

        if (profile == null || string.IsNullOrWhiteSpace(profile.Key1) || string.IsNullOrWhiteSpace(profile.Key2))
        {
            TencentTranslateTestStatus = "请先填写 Secret ID 和 Secret Key";
            TencentTranslateTestStatusColor = System.Windows.Media.Brushes.Red;
            return;
        }

        TencentTranslateTestStatus = "测试中...";
        TencentTranslateTestStatusColor = System.Windows.Media.Brushes.Gray;

        try
        {
            var result = await _tencentService.TranslateAsync("Hello", profile, "auto", "zh");

            if (result.StartsWith("Error") || result.StartsWith("Tencent Error"))
            {
                TencentTranslateTestStatus = $"连接失败：{result}";
                TencentTranslateTestStatusColor = System.Windows.Media.Brushes.Red;
            }
            else
            {
                TencentTranslateTestStatus = $"连接成功！翻译结果：{result}";
                TencentTranslateTestStatusColor = System.Windows.Media.Brushes.Green;
            }
        }
        catch (Exception ex)
        {
            TencentTranslateTestStatus = $"测试出错: {ex.Message}";
            TencentTranslateTestStatusColor = System.Windows.Media.Brushes.Red;
            LoggerService.Instance.LogException(ex, "Failed to test Tencent Cloud", "ApiProvidersViewModel.TestTencentCloud");
        }
    }
    #endregion

    #region Youdao API Testing
    [RelayCommand]
    private void TestYoudao()
    {
        SaveYoudaoCredentials();
        
        var profile = Config.ApiProfiles.FirstOrDefault(p =>
            p.Provider == ApiProvider.Youdao && p.ServiceType == ServiceType.Translation);

        if (profile == null || string.IsNullOrWhiteSpace(profile.Key1) || string.IsNullOrWhiteSpace(profile.Key2))
        {
            YoudaoTestStatus = "请先填写 App Key 和 App Secret";
            YoudaoTestStatusColor = System.Windows.Media.Brushes.Red;
            return;
        }

        YoudaoTestStatus = "有道连接测试功能将在未来版本中实现";
        YoudaoTestStatusColor = System.Windows.Media.Brushes.Orange;
    }
    #endregion

    #region Google API Testing
    [RelayCommand]
    private async Task TestGoogle()
    {
        SaveGoogleCredentials();

        var profile = Config.ApiProfiles.FirstOrDefault(p =>
            p.Provider == ApiProvider.Google && p.ServiceType == ServiceType.Translation);

        if (profile == null || string.IsNullOrWhiteSpace(profile.Key1))
        {
            GoogleTestStatus = "请先填写 API Key";
            GoogleTestStatusColor = System.Windows.Media.Brushes.Red;
            return;
        }

        GoogleTestStatus = "测试中...";
        GoogleTestStatusColor = System.Windows.Media.Brushes.Gray;

        try
        {
            var result = await _googleService.TranslateAsync("Hello World", profile);

            if (!string.IsNullOrWhiteSpace(result) && !result.StartsWith("Google") && !result.StartsWith("错误") && !result.StartsWith("Google API 错误"))
            {
                GoogleTestStatus = $"连接成功！翻译结果：{result}";
                GoogleTestStatusColor = System.Windows.Media.Brushes.Green;
            }
            else
            {
                GoogleTestStatus = $"连接失败：{result}";
                GoogleTestStatusColor = System.Windows.Media.Brushes.Red;
            }
        }
        catch (Exception ex)
        {
            GoogleTestStatus = $"测试出错: {ex.Message}";
            GoogleTestStatusColor = System.Windows.Media.Brushes.Red;
            LoggerService.Instance.LogException(ex, "Failed to test Google", "ApiProvidersViewModel.TestGoogle");
        }
    }
    #endregion

    #region Credentials Management
    private void LoadBaiduCredentials()
    {
        var baiduOcrProfile = Config.ApiProfiles.FirstOrDefault(p =>
            p.Provider == ApiProvider.Baidu && p.ServiceType == ServiceType.OCR);
        var baiduTransProfile = Config.ApiProfiles.FirstOrDefault(p =>
            p.Provider == ApiProvider.Baidu && p.ServiceType == ServiceType.Translation);

        if (baiduOcrProfile != null)
        {
            BaiduOcrApiKey = baiduOcrProfile.Key1;
            BaiduOcrSecretKey = baiduOcrProfile.Key2;
        }

        if (baiduTransProfile != null)
        {
            BaiduTranslateAppId = baiduTransProfile.Key1;
            BaiduTranslateSecretKey = baiduTransProfile.Key2;
        }
    }

    public void SaveBaiduCredentials()
    {
        var baiduOcrProfile = Config.ApiProfiles.FirstOrDefault(p =>
            p.Provider == ApiProvider.Baidu && p.ServiceType == ServiceType.OCR);

        if (baiduOcrProfile == null)
        {
            baiduOcrProfile = new ApiProfile
            {
                Name = "百度 OCR",
                Provider = ApiProvider.Baidu,
                ServiceType = ServiceType.OCR
            };
            Config.ApiProfiles.Add(baiduOcrProfile);
        }

        baiduOcrProfile.Key1 = BaiduOcrApiKey ?? "";
        baiduOcrProfile.Key2 = BaiduOcrSecretKey ?? "";

        var baiduTransProfile = Config.ApiProfiles.FirstOrDefault(p =>
            p.Provider == ApiProvider.Baidu && p.ServiceType == ServiceType.Translation);

        if (baiduTransProfile == null)
        {
            baiduTransProfile = new ApiProfile
            {
                Name = "百度翻译",
                Provider = ApiProvider.Baidu,
                ServiceType = ServiceType.Translation
            };
            Config.ApiProfiles.Add(baiduTransProfile);
        }

        baiduTransProfile.Key1 = BaiduTranslateAppId ?? "";
        baiduTransProfile.Key2 = BaiduTranslateSecretKey ?? "";

        if (string.IsNullOrEmpty(Config.OcrProfileId))
        {
            Config.OcrProfileId = baiduOcrProfile.Id;
        }
        if (string.IsNullOrEmpty(Config.TranslateProfileId))
        {
            Config.TranslateProfileId = baiduTransProfile.Id;
        }

        _settingsService.SaveConfig();
        WeakReferenceMessenger.Default.Send(new RefreshExternalToolsMessage());
    }

    private void LoadTencentCredentials()
    {
        var tencentOcrProfile = Config.ApiProfiles.FirstOrDefault(p =>
            p.Provider == ApiProvider.Tencent && p.ServiceType == ServiceType.OCR);
        var tencentTransProfile = Config.ApiProfiles.FirstOrDefault(p =>
            p.Provider == ApiProvider.Tencent && p.ServiceType == ServiceType.Translation);

        if (tencentOcrProfile != null)
        {
            TencentOcrSecretId = tencentOcrProfile.Key1;
            TencentOcrSecretKey = tencentOcrProfile.Key2;
        }

        if (tencentTransProfile != null)
        {
            TencentTranslateSecretId = tencentTransProfile.Key1;
            TencentTranslateSecretKey = tencentTransProfile.Key2;
        }
    }

    public void SaveTencentCredentials()
    {
        var tencentOcrProfile = Config.ApiProfiles.FirstOrDefault(p =>
            p.Provider == ApiProvider.Tencent && p.ServiceType == ServiceType.OCR);

        if (tencentOcrProfile == null)
        {
            tencentOcrProfile = new ApiProfile
            {
                Name = "腾讯云 OCR",
                Provider = ApiProvider.Tencent,
                ServiceType = ServiceType.OCR
            };
            Config.ApiProfiles.Add(tencentOcrProfile);
        }

        tencentOcrProfile.Key1 = TencentOcrSecretId ?? "";
        tencentOcrProfile.Key2 = TencentOcrSecretKey ?? "";

        var tencentTransProfile = Config.ApiProfiles.FirstOrDefault(p =>
            p.Provider == ApiProvider.Tencent && p.ServiceType == ServiceType.Translation);

        if (tencentTransProfile == null)
        {
            tencentTransProfile = new ApiProfile
            {
                Name = "腾讯云翻译",
                Provider = ApiProvider.Tencent,
                ServiceType = ServiceType.Translation
            };
            Config.ApiProfiles.Add(tencentTransProfile);
        }

        tencentTransProfile.Key1 = TencentTranslateSecretId ?? "";
        tencentTransProfile.Key2 = TencentTranslateSecretKey ?? "";

        if (string.IsNullOrEmpty(Config.OcrProfileId))
        {
            Config.OcrProfileId = tencentOcrProfile.Id;
        }
        if (string.IsNullOrEmpty(Config.TranslateProfileId))
        {
            Config.TranslateProfileId = tencentTransProfile.Id;
        }

        _settingsService.SaveConfig();
        WeakReferenceMessenger.Default.Send(new RefreshExternalToolsMessage());
    }

    private void LoadGoogleCredentials()
    {
        var googleProfile = Config.ApiProfiles.FirstOrDefault(p =>
            p.Provider == ApiProvider.Google && p.ServiceType == ServiceType.Translation);

        if (googleProfile != null)
        {
            GoogleBaseUrl = googleProfile.BaseUrl;
            GoogleApiKey = googleProfile.Key1;
        }
    }

    public void SaveGoogleCredentials()
    {
        var googleProfile = Config.ApiProfiles.FirstOrDefault(p =>
            p.Provider == ApiProvider.Google && p.ServiceType == ServiceType.Translation);

        if (googleProfile == null)
        {
            googleProfile = new ApiProfile
            {
                Name = "Google 翻译",
                Provider = ApiProvider.Google,
                ServiceType = ServiceType.Translation
            };
            Config.ApiProfiles.Add(googleProfile);
        }

        googleProfile.BaseUrl = GoogleBaseUrl ?? "";
        googleProfile.Key1 = GoogleApiKey ?? "";

        _settingsService.SaveConfig();
        WeakReferenceMessenger.Default.Send(new RefreshExternalToolsMessage());
    }

    private void LoadYoudaoCredentials()
    {
        var youdaoOcrProfile = Config.ApiProfiles.FirstOrDefault(p =>
            p.Provider == ApiProvider.Youdao && p.ServiceType == ServiceType.OCR);
        var youdaoTransProfile = Config.ApiProfiles.FirstOrDefault(p =>
            p.Provider == ApiProvider.Youdao && p.ServiceType == ServiceType.Translation);

        if (youdaoOcrProfile != null)
        {
            YoudaoOcrAppKey = youdaoOcrProfile.Key1;
            YoudaoOcrAppSecret = youdaoOcrProfile.Key2;
        }

        if (youdaoTransProfile != null)
        {
            YoudaoTranslateAppKey = youdaoTransProfile.Key1;
            YoudaoTranslateAppSecret = youdaoTransProfile.Key2;
        }
    }

    public void SaveYoudaoCredentials()
    {
        var youdaoOcrProfile = Config.ApiProfiles.FirstOrDefault(p =>
            p.Provider == ApiProvider.Youdao && p.ServiceType == ServiceType.OCR);

        if (youdaoOcrProfile == null)
        {
            youdaoOcrProfile = new ApiProfile
            {
                Name = "有道 OCR",
                Provider = ApiProvider.Youdao,
                ServiceType = ServiceType.OCR
            };
            Config.ApiProfiles.Add(youdaoOcrProfile);
        }

        youdaoOcrProfile.Key1 = YoudaoOcrAppKey ?? "";
        youdaoOcrProfile.Key2 = YoudaoOcrAppSecret ?? "";

        var youdaoTransProfile = Config.ApiProfiles.FirstOrDefault(p =>
            p.Provider == ApiProvider.Youdao && p.ServiceType == ServiceType.Translation);

        if (youdaoTransProfile == null)
        {
            youdaoTransProfile = new ApiProfile
            {
                Name = "有道翻译",
                Provider = ApiProvider.Youdao,
                ServiceType = ServiceType.Translation
            };
            Config.ApiProfiles.Add(youdaoTransProfile);
        }

        youdaoTransProfile.Key1 = YoudaoTranslateAppKey ?? "";
        youdaoTransProfile.Key2 = YoudaoTranslateAppSecret ?? "";

        _settingsService.SaveConfig();
        WeakReferenceMessenger.Default.Send(new RefreshExternalToolsMessage());
    }
    #endregion

    private static byte[] CreateTestImage()
    {
        var width = 200;
        var height = 60;
        var renderBitmap = new System.Windows.Media.Imaging.RenderTargetBitmap(width, height, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
        var visual = new System.Windows.Media.DrawingVisual();

        using (var context = visual.RenderOpen())
        {
            context.DrawRectangle(System.Windows.Media.Brushes.White, null, new System.Windows.Rect(0, 0, width, height));

            var formattedText = new System.Windows.Media.FormattedText(
                "OCR TEST",
                System.Globalization.CultureInfo.InvariantCulture,
                System.Windows.FlowDirection.LeftToRight,
                new System.Windows.Media.Typeface("Arial"),
                24,
                System.Windows.Media.Brushes.Black,
                1.0);

            context.DrawText(formattedText, new System.Windows.Point(40, 15));
        }

        renderBitmap.Render(visual);

        var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
        encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(renderBitmap));

        using (var stream = new System.IO.MemoryStream())
        {
            encoder.Save(stream);
            return stream.ToArray();
        }
    }
}
