using AmongUs.GameOptions;

using TownOfHost.Roles.Core;
using TownOfHost.Roles.Core.Interfaces;
using TownOfHost.Roles.Crewmate;
using UnityEngine;
using static TownOfHost.Options;

namespace TownOfHost.Roles.Impostor
{
    public sealed class Overloader : RoleBase, IImpostor
    {
        public static readonly SimpleRoleInfo RoleInfo =
            SimpleRoleInfo.Create(
                typeof(Overloader),
                player => new Overloader(player),
                CustomRoles.Overloader,
                () => RoleTypes.Shapeshifter,
                CustomRoleTypes.Impostor,
                23500,
                SetupCustomOption,
                "ov"
            );
        public Overloader(PlayerControl player)
        : base(
            RoleInfo,
            player
        )
        {

            idAccelerated = false;
            ReleaseOverload = false;

        }

         private static OptionItem OptionKillCooldownInOverload;
        private static OptionItem OptionSpeedInOverload;
        private static OptionItem OptionOverloadCooldown;
        private static OptionItem OptionOverloadLocktime;
        private static OptionItem OptionReleaseOverload;
            enum OptionName
            {
            OverloaderKillCooldownInOverload,
            OverloaderAddSpeedInOverload,
            OverloadCooldown,
            OverloadLocktime,
            Releaseoverload

        }
        private static bool idAccelerated = false;//加速済みかフラグ
        private static bool ReleaseOverload;
        public bool CanBeLastImpostor { get; } = false;

        public static void SetupCustomOption()
        {
            OptionKillCooldownInOverload = FloatOptionItem.Create(RoleInfo, 10, OptionName.OverloaderKillCooldownInOverload, new(2.5f, 180f, 2.5f), 7.5f, false).SetValueFormat(OptionFormat.Seconds);
            OptionSpeedInOverload = FloatOptionItem.Create(RoleInfo, 11, OptionName.OverloaderAddSpeedInOverload, new(0.1f, 1.0f, 0.1f), 0.3f, false);
            OptionOverloadCooldown = FloatOptionItem.Create(RoleInfo, 12, OptionName.OverloadCooldown, new(2.5f, 180f, 2.5f), 30f, false).SetValueFormat(OptionFormat.Seconds);
            OptionOverloadLocktime = FloatOptionItem.Create(RoleInfo, 13, OptionName.OverloadLocktime, new(2.5f, 180f, 2.5f), 7.5f, false).SetValueFormat(OptionFormat.Seconds);
            OptionReleaseOverload = BooleanOptionItem.Create(RoleInfo, 14, OptionName.Releaseoverload, true, false);

        }
        public float CalculateKillCooldown() => idAccelerated ? OptionKillCooldownInOverload.GetFloat() : DefaultKillCooldown;

        public override void ApplyGameOptions(IGameOptions opt)
        {
            AURoleOptions.ShapeshifterCooldown = OptionOverloadCooldown.GetFloat();
            AURoleOptions.ShapeshifterDuration = 1f;
        }
        public override void OnShapeshift(PlayerControl target)
        {
            var shapeshifting = Player.PlayerId != target.PlayerId;

            if (shapeshifting && !idAccelerated)
            { //オーバーロード中で加速済みでない場合
                idAccelerated = true;
                Main.AllPlayerSpeed[Player.PlayerId] += OptionSpeedInOverload.GetFloat();//Overloaderの速度を加算
                Main.AllPlayerKillCooldown[Player.PlayerId] = OptionKillCooldownInOverload.GetFloat();
                Player.SyncSettings();
                Player.SetKillCooldown();
                new LateTask(() =>
                {
                    {
                        Player.RpcRevertShapeshift(true);
                        AURoleOptions.ShapeshifterCooldown = OptionOverloadLocktime.GetFloat();
                        Player.RpcResetAbilityCooldown();
                        Player.SyncSettings();
                    }
                }, 0.2f, "Overloader SS");
            }
            else if (shapeshifting && idAccelerated)
            { //オーバーロード中ではなく加速済みになっている場合
                idAccelerated = false;
                Main.AllPlayerSpeed[Player.PlayerId] -= OptionSpeedInOverload.GetFloat();//Overloaderの速度を減算
                Main.AllPlayerKillCooldown[Player.PlayerId] = DefaultKillCooldown;
                if (ReleaseOverload)
                {
                    Player.SyncSettings();
                    Player.SetKillCooldown();
                }
                new LateTask(() =>
                {
                    {
                        Player.RpcRevertShapeshift(true);
                        AURoleOptions.ShapeshifterCooldown = OptionOverloadCooldown.GetFloat();
                        Player.SyncSettings();
                        Player.RpcResetAbilityCooldown();
                    }
                }, 0.2f, "Overloader SS");
            }
        }
        public override void OnReportDeadBody(PlayerControl reporter, GameData.PlayerInfo target)
        {
            foreach (var player in Main.AllPlayerControls)
            {
                if (player.Is(CustomRoles.Overloader) && idAccelerated)
                {
                    idAccelerated = false;
                    Main.AllPlayerSpeed[player.PlayerId] -= OptionSpeedInOverload.GetFloat();//Overloaderの速度を減算
                    Main.AllPlayerKillCooldown[player.PlayerId] = DefaultKillCooldown;
                    AURoleOptions.ShapeshifterCooldown = OptionOverloadCooldown.GetFloat();
                    player.RpcResetAbilityCooldown();
                    player.SyncSettings();
                }
            }
            return;
        }
        public override string GetProgressText(bool comms = false)
        {
            return idAccelerated ? Utils.ColorString(Palette.ImpostorRed, $"★") : "";
        }
        public static bool KnowTargetRoleColor(PlayerControl target, bool isMeeting)
            => !isMeeting && idAccelerated && target.Is(CustomRoles.Overloader);
    }

}