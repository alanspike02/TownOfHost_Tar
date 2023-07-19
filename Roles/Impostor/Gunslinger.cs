using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using AmongUs.GameOptions;
using UnityEngine;
using Hazel;
using TownOfHost.Roles.Core;
using TownOfHost.Roles.Core.Interfaces;
using static TownOfHost.Roles.Impostor.Witch;
using Epic.OnlineServices.Presence;
using static UnityEngine.GraphicsBuffer;
using MS.Internal.Xml.XPath;

namespace TownOfHost.Roles.Impostor;
public sealed class Gunslinger : RoleBase, IImpostor
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(Gunslinger),
            player => new Gunslinger(player),
            CustomRoles.Gunslinger,
            () => RoleTypes.Shapeshifter,
            CustomRoleTypes.Impostor,
            27000,
            SetupCustomOption,
            "gs"
        );
    public Gunslinger(PlayerControl player)
    : base(
        RoleInfo,
        player
    )
    {
        MultiKillCount = OptionCanUseMultiKillCount.GetInt();
        MKDef = OptionCanUseMultiKillCount.GetInt();
        MultiKillCool = OptionMultiKillCool.GetFloat();
        MultiKillTime = OptionMultiKillTime.GetFloat();
        GsAddKC = OptionAddKillCool.GetFloat();
        DefaultKillCool = OptionDefaultKillCool.GetFloat();
    }

    private static OptionItem OptionMultiKillCool;
    private static OptionItem OptionCanUseMultiKillCount;
    private static OptionItem OptionMultiKillTime;
    private static OptionItem OptionDefaultKillCool;
    private static OptionItem OptionAddKillCool;
    enum OptionName
    {
        GunslingerMultiKillCount,
        GunslingerMultiKillCooldown,
        GunslingerMultiKillTime,
        GunslingerDefaultKillCool,
        GunslingerAddKillCool,
    }
    private static int MultiKillCount;
    private static int MKDef;
    private static float DefaultKillCool;
    private static float MultiKillCool;
    private static float MultiKillTime;
    private static float GsAddKC;
    private static bool IsKilledOnce;
    public override void Add()
    {
        var playerId = Player.PlayerId;
        Player.AddDoubleTrigger();
        MultiKillCount = OptionCanUseMultiKillCount.GetInt();
        IsKilledOnce = false;
    }
    public static void SetupCustomOption()
    {
        OptionDefaultKillCool = FloatOptionItem.Create(RoleInfo, 10, OptionName.GunslingerDefaultKillCool, new(2.5f, 180f, 2.5f), 30f, false)
            .SetValueFormat(OptionFormat.Seconds);
        OptionMultiKillCool = FloatOptionItem.Create(RoleInfo, 11, OptionName.GunslingerMultiKillCooldown, new(1f, 10f, 0.25f), 2f, false)
            .SetValueFormat(OptionFormat.Multiplier);
        OptionCanUseMultiKillCount = IntegerOptionItem.Create(RoleInfo, 12, OptionName.GunslingerMultiKillCount, new(1, 15, 1), 1, false);
        OptionMultiKillTime = FloatOptionItem.Create(RoleInfo, 13, OptionName.GunslingerMultiKillTime, new(1f, 180f, 1f), 3f, false)
            .SetValueFormat(OptionFormat.Seconds);
        OptionAddKillCool = FloatOptionItem.Create(RoleInfo, 14, OptionName.GunslingerAddKillCool, new(0f, 180f, 2.5f), 5f, false)
            .SetValueFormat(OptionFormat.Seconds);
    }

    public float CalculateKillCooldown() => DefaultKillCool + (GsAddKC* (MKDef - MultiKillCount));
   public override void ApplyGameOptions(IGameOptions opt) => AURoleOptions.ShapeshifterCooldown = IsKilledOnce ? MultiKillTime : 255;
    public void OnCheckMurderAsKiller(MurderInfo info)
    {
        var (killer, target) = info.AttemptTuple;
        killer.SyncSettings();
        if (!IsKilledOnce && MultiKillCount > 0)
        {
            IsKilledOnce = true;
            Main.AllPlayerKillCooldown[killer.PlayerId] = 0.0001f;
            killer.SyncSettings();
            killer.RpcResetAbilityCooldown();
            new LateTask(() =>
            {
                if (IsKilledOnce)
                {
                    IsKilledOnce = false;
                    Main.AllPlayerKillCooldown[killer.PlayerId] = DefaultKillCool + (GsAddKC * (MKDef - MultiKillCount));
                    killer.SyncSettings();
                    killer.ResetKillCooldown();
                    killer.SetKillCooldown();
                    killer.RpcResetAbilityCooldown();
                    killer.MarkDirtySettings();
                    RPC.PlaySoundRPC(killer.PlayerId, Sounds.KillSound);
                }
            }, MultiKillTime, "GS MultiKill");
        }
        else if (IsKilledOnce && MultiKillCount > 0)
        {
            IsKilledOnce = false;
            Main.AllPlayerKillCooldown[killer.PlayerId] = DefaultKillCool * MultiKillCool;
            killer.SyncSettings();
            killer.RpcResetAbilityCooldown();
            killer.SyncSettings();
            MultiKillCount--;
        }
    }
    public override string GetProgressText(bool comms = false) => Utils.ColorString(MultiKillCount > 0 ? Color.red.ShadeColor(0.5f) : Color.gray, $"({MultiKillCount})");
}