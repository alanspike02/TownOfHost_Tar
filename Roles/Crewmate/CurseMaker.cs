using AmongUs.GameOptions;

using TownOfHost.Roles.Core;
using TownOfHost.Roles.Core.Interfaces;
using static TownOfHost.Options;

namespace TownOfHost.Roles.Crewmate
{
    public sealed class CurseMaker : RoleBase
    {
        public static readonly SimpleRoleInfo RoleInfo =
            SimpleRoleInfo.Create(
                typeof(CurseMaker),
                player => new CurseMaker(player),
                CustomRoles.CurseMaker,
                () => RoleTypes.Crewmate,
                CustomRoleTypes.Crewmate,
                24500,
                SetupOptionItem,
                "cm",
                "#6652cc"
            );
        public CurseMaker(PlayerControl player)
        : base(
            RoleInfo,
            player
        )
        {
            IncreaseKillCooldown = OptionIncreaseKillCooldown.GetInt();
            cmdeath = false;
        }
        private static OptionItem OptionIncreaseKillCooldown;
        enum OptionName
        {
            CurseMakerIncreaseMKillCooldown,
        }
        public static float IncreaseKillCooldown = 0f;
        public static int ReportCount = 0;

        public bool cmdeath = false;

        private static void SetupOptionItem()
        {
            OptionIncreaseKillCooldown = IntegerOptionItem.Create(RoleInfo, 10, OptionName.CurseMakerIncreaseMKillCooldown, new(0, 180, 5), 15, false)
                .SetValueFormat(OptionFormat.Seconds);
        }

        public float CalculateKillCooldown()
        {
            var ksec = IncreaseKillCooldown * ReportCount;
            return ksec;
        }
        // MyState.GetKillCount(true));
        public override void OnReportDeadBody(PlayerControl reporter, GameData.PlayerInfo target)
        {
            if (reporter.Is(CustomRoles.CurseMaker) && target != null) //noボタン
                ReportCount++;
            Options.DefaultKillCooldown += CalculateKillCooldown();
            return;
        }
        public override void OnFixedUpdate(PlayerControl player)
        {
            if (player.Is(CustomRoles.CurseMaker) && player.Data.IsDead && cmdeath == false && ReportCount >= 0)
                foreach (var allkiller in Main.AllAlivePlayerControls)
                {
                    Options.DefaultKillCooldown -= CalculateKillCooldown();
                    allkiller.SetKillCooldown();
                }
            cmdeath = true;
            return;
        }
        public override string GetProgressText(bool comms = false)
        {
            var time = CalculateKillCooldown();
            return time > 0 ? Utils.ColorString(Utils.GetRoleColor(CustomRoles.CurseMaker).ShadeColor(0.5f), $" +{time}s") : "";
        }
    }
}