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
using TownOfHost.Roles.Madmate;

namespace TownOfHost.Roles.Impostor;
public sealed class Necromaster : RoleBase, IImpostor
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(Necromaster),
            player => new Necromaster(player),
            CustomRoles.Necromaster,
            () => RoleTypes.Impostor,
            CustomRoleTypes.Impostor,
            26000,
            SetupCustomOption,
            "nm"
        );
    public Necromaster(PlayerControl player)
    : base(
        RoleInfo,
        player
    )
    {
        HexCount = OptionCanUseHexCount.GetInt();

        CustomRoleManager.MarkOthers.Add(GetMarkOthers);
    }

    private static OptionItem OptionHexCooldown;
    private static OptionItem OptionCanUseHexCount;
    enum OptionName
    {
        NecromasterCanHexCount,
        NecromasterHexCooldown,
    }
    private static int HexCount;
    private static Dictionary<byte, Necromaster> Hex = new(15);
    public List<byte> HexedPlayer = new();
    public override void OnDestroy()
    {
        Hex.Clear();

        CustomRoleManager.MarkOthers.Remove(GetMarkOthers);
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
        OptionHexCooldown = FloatOptionItem.Create(RoleInfo, 10, OptionName.NecromasterHexCooldown, new(2.5f, 180f, 2.5f), 15f, false)
            .SetValueFormat(OptionFormat.Seconds);
        OptionCanUseHexCount = IntegerOptionItem.Create(RoleInfo, 11, OptionName.NecromasterCanHexCount, new(2, 15, 1), 3, false);
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
        else if (HexedPlayer.Contains(target.PlayerId))
        {
            foreach (var hexed in Main.AllAlivePlayerControls)
            {
                if (HexedPlayer.Contains(hexed.PlayerId) && target != hexed)
                {
                    hexed.SetRealKiller(killer);
                    hexed.RpcMurderPlayerV2(hexed);
                    SendRPC(target.PlayerId, 2);
                    killer.MarkDirtySettings();
                }
            }
        }
        info.DoKill &= info.CanKill;
    }

    public override string GetMark(PlayerControl seer, PlayerControl seen, bool _ = false)
    {
        //seenが省略の場合seer
        seen ??= seer;

        if (!(Hex.ContainsKey(seen.PlayerId) && seer.Is(CustomRoles.Necromaster))) return "";

        return Utils.ColorString(Color.red, " ☯");
    }

    public override string GetProgressText(bool comms = false) => HexCount > 0 ? Utils.ColorString(Color.red, $"({HexCount})") : Utils.ColorString(Color.red.ShadeColor(0.75f), $" ☯");
}