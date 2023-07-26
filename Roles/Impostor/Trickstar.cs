using AmongUs.GameOptions;

using TownOfHost.Roles.Core;
using TownOfHost.Roles.Core.Interfaces;
using TownOfHost.Roles.Crewmate;
using UnityEngine;
using static TownOfHost.Options;

namespace TownOfHost.Roles.Impostor
{
    public sealed class Trickster : RoleBase, IImpostor
    {
        public static readonly SimpleRoleInfo RoleInfo =
            SimpleRoleInfo.Create(
                typeof(Trickster),
                player => new Trickster(player),
                CustomRoles.Trickster,
                () => RoleTypes.Shapeshifter,
                CustomRoleTypes.Impostor,
                29000,
                SetupCustomOption,
                "tr"
            );
        public Trickster(PlayerControl player)
        : base(
            RoleInfo,
            player
        )
        {
            IsLightOff = false;
        }

        private static OptionItem OptionLightOffCooldown;
        private static OptionItem OptionLightOffTime;
        private static OptionItem OptionLightOffVision;
        enum OptionName
        {
            LightOffCooldown,
            LightOffTime,
            LightOffVision,
        }
        private static bool IsLightOff;
        private float lighttime;
        public bool CanBeLastImpostor { get; } = false;

        public static void SetupCustomOption()
        {
            OptionLightOffCooldown = FloatOptionItem.Create(RoleInfo, 12, OptionName.LightOffCooldown, new(2.5f, 180f, 2.5f), 30f, false).SetValueFormat(OptionFormat.Seconds);
            OptionLightOffTime = FloatOptionItem.Create(RoleInfo, 13, OptionName.LightOffTime, new(2.5f, 30f, 2.5f), 7.5f, false).SetValueFormat(OptionFormat.Seconds);
            OptionLightOffVision = FloatOptionItem.Create(RoleInfo, 14, OptionName.LightOffVision, new(0f, 5f, 0.25f), 2f, false);
        }

        public override void ApplyGameOptions(IGameOptions opt)
        {
            AURoleOptions.ShapeshifterCooldown = IsLightOff ? OptionLightOffTime.GetFloat() : OptionLightOffCooldown.GetFloat();
            AURoleOptions.ShapeshifterDuration = 1f;
            foreach (var player in Main.AllPlayerControls)
            {
                if (IsLightOff)
                {
                    opt.SetFloat(FloatOptionNames.CrewLightMod, 0); ;
                }
                else if (!IsLightOff)
                {
                    opt.SetFloat(FloatOptionNames.CrewLightMod, Main.DefaultCrewmateVision);
                    //Main.DefaultCrewmateVision
                }
            }
        }
        public override void OnShapeshift(PlayerControl target)
        {
            var shapeshifting = Player.PlayerId != target.PlayerId;

            if (shapeshifting && !IsLightOff)
            { //オーバーロード中で加速済みでない場合
                IsLightOff = true;
                new LateTask(() =>
                {
                    {
                        Player.RpcRevertShapeshift(true);
                        Player.SyncSettings();
                        Player.RpcResetAbilityCooldown();
                        foreach (var player in Main.AllPlayerControls)
                        {
                            player.MarkDirtySettings();
                        }
                    }
                }, 0.2f, "Trickster LOFF");
                new LateTask(() =>
                {
                    {
                        IsLightOff = false;
                        Player.SyncSettings();
                        Player.RpcResetAbilityCooldown();
                        foreach (var player in Main.AllPlayerControls)
                        {
                            player.MarkDirtySettings();
                        }
                    }
                }, OptionLightOffTime.GetFloat(), "Trickster LON");
            }
        }
        public override void OnReportDeadBody(PlayerControl reporter, GameData.PlayerInfo target)
        {
            foreach (var player in Main.AllPlayerControls)
            {
                if (player.Is(CustomRoles.Overloader) && IsLightOff)
                {
                    IsLightOff = false;
                    AURoleOptions.ShapeshifterCooldown = OptionLightOffCooldown.GetFloat();
                    player.RpcResetAbilityCooldown();
                    player.SyncSettings();
                }
            }
            return;
        }
        public override string GetProgressText(bool comms = false)
        {
            return IsLightOff ? Utils.ColorString(Palette.ImpostorRed, $"×") : "";
        }
    }

}