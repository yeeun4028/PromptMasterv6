using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MediatR;
using PromptMasterv6.Infrastructure.Services;
using PromptMasterv6.Features.Shared.Models;
using System.Linq;
using System.Threading.Tasks;

namespace PromptMasterv6.Features.Settings.ApiCredentials
{
    public partial class ApiCredentialsViewModel : ObservableObject
    {
        private readonly SettingsService _settingsService;
        private readonly IMediator _mediator;

        public AppConfig Config => _settingsService.Config;

        #region Tab Navigation

        [ObservableProperty] private int selectedProviderTab = 0;

        [RelayCommand]
        private void SelectProviderTab(string tabIndexStr)
        {
            if (int.TryParse(tabIndexStr, out int tabIndex))
            {
                SelectedProviderTab = tabIndex;
            }
        }

        #endregion

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

        public ApiCredentialsViewModel(
            SettingsService settingsService,
            IMediator mediator)
        {
            _settingsService = settingsService;
            _mediator = mediator;

            LoadAllCredentials();
        }

        public void LoadAllCredentials()
        {
            LoadBaiduCredentials();
            LoadTencentCredentials();
            LoadYoudaoCredentials();
            LoadGoogleCredentials();
        }

        #region Baidu Commands

        [RelayCommand]
        private async Task TestBaiduOcr()
        {
            SaveBaiduCredentials();

            BaiduOcrTestStatus = "测试中...";
            BaiduOcrTestStatusColor = System.Windows.Media.Brushes.Gray;

            var result = await _mediator.Send(new TestBaiduOcrFeature.Command(BaiduOcrApiKey ?? "", BaiduOcrSecretKey ?? ""));

            BaiduOcrTestStatus = result.Success ? $"✅ {result.Message}" : $"❌ {result.Message}";
            BaiduOcrTestStatusColor = result.Success ? System.Windows.Media.Brushes.Green : System.Windows.Media.Brushes.Red;
        }

        [RelayCommand]
        private async Task TestBaiduTranslate()
        {
            SaveBaiduCredentials();

            BaiduTranslateTestStatus = "测试中...";
            BaiduTranslateTestStatusColor = System.Windows.Media.Brushes.Gray;

            var result = await _mediator.Send(new TestBaiduTranslateFeature.Command(BaiduTranslateAppId ?? "", BaiduTranslateSecretKey ?? ""));

            BaiduTranslateTestStatus = result.Success ? $"✅ {result.Message}" : $"❌ {result.Message}";
            BaiduTranslateTestStatusColor = result.Success ? System.Windows.Media.Brushes.Green : System.Windows.Media.Brushes.Red;
        }

        #endregion

        #region Tencent Commands

        [RelayCommand]
        private async Task TestTencentOcr()
        {
            SaveTencentCredentials();

            TencentOcrTestStatus = "测试中...";
            TencentOcrTestStatusColor = System.Windows.Media.Brushes.Gray;

            var result = await _mediator.Send(new TestTencentOcrFeature.Command(TencentOcrSecretId ?? "", TencentOcrSecretKey ?? ""));

            TencentOcrTestStatus = result.Success ? $"✅ {result.Message}" : $"❌ {result.Message}";
            TencentOcrTestStatusColor = result.Success ? System.Windows.Media.Brushes.Green : System.Windows.Media.Brushes.Red;
        }

        [RelayCommand]
        private async Task TestTencentTranslate()
        {
            SaveTencentCredentials();

            TencentTranslateTestStatus = "测试中...";
            TencentTranslateTestStatusColor = System.Windows.Media.Brushes.Gray;

            var result = await _mediator.Send(new TestTencentTranslateFeature.Command(TencentTranslateSecretId ?? "", TencentTranslateSecretKey ?? ""));

            TencentTranslateTestStatus = result.Success ? $"✅ {result.Message}" : $"❌ {result.Message}";
            TencentTranslateTestStatusColor = result.Success ? System.Windows.Media.Brushes.Green : System.Windows.Media.Brushes.Red;
        }

        #endregion

        #region Youdao Commands

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

        #region Google Commands

        [RelayCommand]
        private async Task TestGoogle()
        {
            SaveGoogleCredentials();

            GoogleTestStatus = "测试中...";
            GoogleTestStatusColor = System.Windows.Media.Brushes.Gray;

            var result = await _mediator.Send(new TestGoogleFeature.Command(GoogleBaseUrl ?? "", GoogleApiKey ?? ""));

            GoogleTestStatus = result.Success ? $"✅ {result.Message}" : $"❌ {result.Message}";
            GoogleTestStatusColor = result.Success ? System.Windows.Media.Brushes.Green : System.Windows.Media.Brushes.Red;
        }

        #endregion

        #region Credentials Load/Save

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
            _mediator.Send(new SaveApiCredentialsFeature.Command(
                ApiProvider.Baidu, ServiceType.OCR, "百度 OCR",
                BaiduOcrApiKey ?? "", BaiduOcrSecretKey ?? "")).Wait();

            _mediator.Send(new SaveApiCredentialsFeature.Command(
                ApiProvider.Baidu, ServiceType.Translation, "百度翻译",
                BaiduTranslateAppId ?? "", BaiduTranslateSecretKey ?? "")).Wait();
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
            _mediator.Send(new SaveApiCredentialsFeature.Command(
                ApiProvider.Tencent, ServiceType.OCR, "腾讯云 OCR",
                TencentOcrSecretId ?? "", TencentOcrSecretKey ?? "")).Wait();

            _mediator.Send(new SaveApiCredentialsFeature.Command(
                ApiProvider.Tencent, ServiceType.Translation, "腾讯云翻译",
                TencentTranslateSecretId ?? "", TencentTranslateSecretKey ?? "")).Wait();
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
            _mediator.Send(new SaveApiCredentialsFeature.Command(
                ApiProvider.Google, ServiceType.Translation, "Google 翻译",
                GoogleApiKey ?? "", "", GoogleBaseUrl ?? "")).Wait();
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
            _mediator.Send(new SaveApiCredentialsFeature.Command(
                ApiProvider.Youdao, ServiceType.OCR, "有道 OCR",
                YoudaoOcrAppKey ?? "", YoudaoOcrAppSecret ?? "")).Wait();

            _mediator.Send(new SaveApiCredentialsFeature.Command(
                ApiProvider.Youdao, ServiceType.Translation, "有道翻译",
                YoudaoTranslateAppKey ?? "", YoudaoTranslateAppSecret ?? "")).Wait();
        }

        #endregion
    }
}
