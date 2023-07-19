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

namespace TownOfHost.Roles.Impostor;
public sealed class HexMaster : RoleBase, IImpostor
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(HexMaster),
            player => new HexMaster(player),
            CustomRoles.HexMaster,
            () => RoleTypes.Impostor,
            CustomRoleTypes.Impostor,
            26000,
            SetupCustomOption,
            "hm"
        );
    public HexMaster(PlayerControl player)
    : base(
        RoleInfo,
        player
    )
    {
        HexCount = OptionCanUseHexCount.GetInt();

    }

    private static OptionItem OptionHexCooldown;
    private static OptionItem OptionCanUseHexCount;
    private static OptionItem OptionBlockMoveTime;
    enum OptionName
    {
        HexMasterCanHexCount,
        HexMasterHexCooldown,
        HexMasterBlockMoveTime,
    }
    private static int HexCount;
    private static Dictionary<byte, HexMaster> Hex = new(15);
    public List<byte> HexedPlayer = new();
    public override void OnDestroy()
    {
        Hex.Clear();
    }
    private void SendRPC(byte targetId, byte typeId)
    {
        using var sender = CreateSender(CustomRPC.SyncHex);

        sender.Writer.Write(typeId);
        sender.Writer.Write(targetId);
    }
    public override void ReceiveRPC(MessageReader reader, CustomRPC rpcType)
    {
        if (rpcType != CustomRPC.SyncHex) return;

        var typeId = reader.ReadByte();
        var targetId = reader.ReadByte();

        switch (typeId)
        {
            case 0: //Dictionaryのクリア
                Hex.Clear();
                break;
            case 1: //Dictionaryに追加
                Hex[targetId] = this;
                break;
            case 2: //DictionaryのKey削除
                Hex.Remove(targetId);
                break;
        }
    }
    public override void Add()
    {
        var playerId = Player.PlayerId;
        Player.AddDoubleTrigger();
        HexCount = OptionCanUseHexCount.GetInt();
    }
    public static void SetupCustomOption()
    {
        OptionHexCooldown = FloatOptionItem.Create(RoleInfo, 10, OptionName.HexMasterHexCooldown, new(2.5f, 180f, 2.5f), 15f, false)
            .SetValueFormat(OptionFormat.Seconds);
        OptionCanUseHexCount = IntegerOptionItem.Create(RoleInfo, 11, OptionName.HexMasterCanHexCount, new(1, 15, 1), 3, false);
        OptionBlockMoveTime = FloatOptionItem.Create(RoleInfo, 12, OptionName.HexMasterBlockMoveTime, new(2.5f, 180f, 2.5f), 15f, false)
            .SetValueFormat(OptionFormat.Seconds);
    }
    public void SetHexed(PlayerControl killer, PlayerControl target)
    {
        Hex[target.PlayerId] = this;
        HexedPlayer.Add(target.PlayerId);
        HexCount--;
        SendRPC(target.PlayerId, 1);
        Utils.NotifyRoles(SpecifySeer: killer);
        Main.AllPlayerKillCooldown[killer.PlayerId] = OptionHexCooldown.GetFloat();
        killer.SyncSettings();
        killer.SetKillCooldown();
        Main.AllPlayerKillCooldown[killer.PlayerId] = Options.DefaultKillCooldown;
        killer.SyncSettings();
    }

    public void OnCheckMurderAsKiller(MurderInfo info)
    {
        var (killer, target) = info.AttemptTuple;
        if (HexCount > 0)
        {
            info.DoKill = killer.CheckDoubleTrigger(target, () => { SetHexed(killer, target); });
        }
        else
        {
            var tmpSpeed = Main.AllPlayerSpeed[killer.PlayerId];
            foreach (var hexed in Main.AllAlivePlayerControls)
            {
                if (HexedPlayer.Contains(hexed.PlayerId))
                {
                    hexed.SetRealKiller(killer);
                    hexed.RpcMurderPlayerV2(hexed);
                    killer.KillFlash();
                    killer.SetKillCooldown();
                    SendRPC(target.PlayerId, 2);
                    Main.AllPlayerSpeed[killer.PlayerId] = Main.MinSpeed;    //tmpSpeedで後ほど値を戻すので代入しています。
                    ReportDeadBodyPatch.CanReport[killer.PlayerId] = false;
                    killer.MarkDirtySettings();
                    new LateTask(() =>
                    {
                        Main.AllPlayerSpeed[killer.PlayerId] = tmpSpeed;
                        ReportDeadBodyPatch.CanReport[killer.PlayerId] = true;
                        killer.MarkDirtySettings();
                        RPC.PlaySoundRPC(killer.PlayerId, Sounds.KillSound);
                    }, OptionBlockMoveTime.GetFloat(), "HM BlockMove");

                    info.DoKill = false;
                }
            }
        }
        info.DoKill &= info.CanKill;
    }

    public override string GetMark(PlayerControl seer, PlayerControl seen, bool _ = false)
    {
        //seenが省略の場合seer
        seen ??= seer;

        if (!(Hex.ContainsValue(this) &&
            Hex.ContainsKey(seen.PlayerId))) return "";

        return Utils.ColorString(RoleInfo.RoleColor, "❖");
    }

    public override string GetProgressText(bool comms = false) => Utils.ColorString(HexCount > 0 ? Color.red : Color.red.ShadeColor(0.5f), $"({HexCount})");
}