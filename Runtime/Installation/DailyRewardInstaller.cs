using System;
using Dreamy.Core;
using Dreamy.DataConfig;
using Dreamy.Datasave;
using Dreamy.Economy;

namespace Dreamy.DailyReward
{
    public static class DailyRewardInstaller
    {
        public const string DefaultSaveKey = "daily-reward";

        public static void RegisterConfig(IDataConfigService dataConfigService)
        {
            if (dataConfigService == null)
            {
                throw new ArgumentNullException(nameof(dataConfigService));
            }

            dataConfigService.Register<DailyRewardScheduleConfig>("dailyRewardSchedule");
        }

        public static IDailyRewardService Install(string saveKey = DefaultSaveKey)
        {
            IDataConfigService configService = ServiceLocator.Get<IDataConfigService>();
            IDatasaveService datasaveService = ServiceLocator.Get<IDatasaveService>();
            IResourceWallet resourceWallet = ServiceLocator.Get<IResourceWallet>();
            IRewardClock clock = ServiceLocator.TryGet<IRewardClock>(out IRewardClock registeredClock)
                ? registeredClock
                : new SystemRewardClock();

            return Install(
                configService.GetTable<DailyRewardScheduleConfig>(),
                datasaveService,
                resourceWallet,
                clock,
                saveKey);
        }

        public static IDailyRewardService Install(
            DailyRewardScheduleConfig schedule,
            IDatasaveService datasaveService,
            IResourceWallet resourceWallet,
            IRewardClock clock,
            string saveKey = DefaultSaveKey)
        {
            DailyRewardModel service = new(schedule, datasaveService, resourceWallet, clock, saveKey);
            ServiceLocator.Register<IDailyRewardService>(service);
            return service;
        }
    }
}
