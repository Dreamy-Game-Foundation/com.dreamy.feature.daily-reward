using System;
using Dreamy.DataConfig;
using Dreamy.Datasave;
using Dreamy.Economy;
using Dreamy.DailyReward;
using Dreamy.UI;

namespace Dreamy.Feature.DailyReward.Integration
{
    public static class DailyRewardFeatureInstaller
    {
        public static void RegisterConfig(IDataConfigService config) => DailyRewardInstaller.RegisterConfig(config);

        public static IDailyRewardService Install(PanelPresenterFactory factory, DailyRewardScheduleConfig config, IDatasaveService save, IResourceWallet wallet, IRewardClock clock, string saveKey = DailyRewardInstaller.DefaultSaveKey, IDailyRewardFeedback feedback = null)
        {
            if (factory == null) throw new ArgumentNullException(nameof(factory));
            return Install(factory, DailyRewardInstaller.Install(config, save, wallet, clock, saveKey), feedback);
        }

        public static IDailyRewardService Install(PanelPresenterFactory factory, IDailyRewardService service, IDailyRewardFeedback feedback = null)
        {
            if (factory == null) throw new ArgumentNullException(nameof(factory));
            if (service == null) throw new ArgumentNullException(nameof(service));
            factory.Register<DailyRewardPanel>(view => new DailyRewardPresenter(service, view, feedback));
            return service;
        }
    }
}
