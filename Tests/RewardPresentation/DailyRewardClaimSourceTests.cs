using System;
using System.IO;
using System.Threading;
using Dreamy.Datasave;
using Dreamy.Economy;
using Dreamy.Economy.Rewards;
using Dreamy.Economy.UI;
using Dreamy.DailyReward.Presentation;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Dreamy.DailyReward.Tests
{
    public sealed class DailyRewardClaimSourceTests
    {
        private sealed class Clock : IRewardClock { public DateTime UtcNow { get; set; } = new(2026,10,20,12,0,0,DateTimeKind.Utc); }
        private string path;
        private DatasaveResourceWallet wallet;
        private DailyRewardClaimSource source;
        private Clock clock;
        [SetUp] public void Setup()
        {
            path=Path.Combine(Path.GetTempPath(),"DreamyDailyPresentation-"+Guid.NewGuid().ToString("N"));clock=new Clock();
            var save=new DatasaveService(new DatasaveOptions{DirectoryName=path,CreateFileOnFirstLoad=false});wallet=new DatasaveResourceWallet(save);
            var config=JsonConvert.DeserializeObject<DailyRewardScheduleConfig>("{\"scheduleId\":\"daily\",\"cycleMode\":\"Repeat\",\"rewards\":[{\"id\":\"one\",\"day\":1,\"resourceId\":\"currency.coin\",\"amount\":100}]}");config.Initialize("daily");
            var model=new DailyRewardModel(config,save,new RewardDeliveryGateway(new IRewardDestination[]{wallet}),clock,"daily");source=new DailyRewardClaimSource("profile",model);
        }
        [TearDown] public void Cleanup(){if(Directory.Exists(path))Directory.Delete(path,true);}
        [Test] public void PreviewThenClaimThenRepeatCreditsOnce()
        {
            var id=source.CurrentReference();var preview=source.QueryAsync(id,CancellationToken.None).GetAwaiter().GetResult();
            Assert.AreEqual(0,wallet.GetBalance(new ResourceId("currency.coin")));
            var command=new RewardClaimCommand(id,preview.Fingerprint);
            Assert.AreEqual(RewardClaimStatus.Claimed,source.ClaimAsync(command).GetAwaiter().GetResult().Status);
            Assert.AreEqual(RewardClaimStatus.AlreadyClaimed,source.ClaimAsync(command).GetAwaiter().GetResult().Status);
            Assert.AreEqual(100,wallet.GetBalance(new ResourceId("currency.coin")));
        }
        [Test] public void OpenAcrossMidnightCannotGrantAnotherDay()
        {
            var id=source.CurrentReference();var preview=source.QueryAsync(id,CancellationToken.None).GetAwaiter().GetResult();clock.UtcNow=clock.UtcNow.AddDays(1);
            Assert.AreEqual(RewardClaimStatus.Expired,source.QueryAsync(id,CancellationToken.None).GetAwaiter().GetResult().Status);
            Assert.IsFalse(source.ClaimAsync(new RewardClaimCommand(id,preview.Fingerprint)).GetAwaiter().GetResult().IsCompleted);
            Assert.AreEqual(0,wallet.GetBalance(new ResourceId("currency.coin")));
        }
    }
}
