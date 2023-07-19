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
using Rewired.UI.ControlMapper;
using UnityEngine.Profiling;
using HarmonyLib;
using Newtonsoft.Json.Bson;

namespace TownOfHost.Roles.Impostor;
public sealed class MeetingManager : RoleBase, IImpostor
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(MeetingManager),
            player => new MeetingManager(player),
            CustomRoles.MeetingManager,
            () => RoleTypes.Impostor,
            CustomRoleTypes.Impostor,
            28250,
            SetupCustomOption,
            "mm"
        );
    public MeetingManager(PlayerControl player)
    : base(
        RoleInfo,
        player
    )
    {
        canblocksabo = OptionCanBlockRepairSabotage.GetBool();
        blockrepocount = OptionBlockRepoCount.GetInt();
    }

    private static OptionItem OptionBlockRepoCount;
    private static OptionItem OptionBlockCooldown;
    private static OptionItem OptionCanBlockRepairSabotage;
    enum OptionName
    {
        MeetingManagerBlockRepoCount,
        MeetingManagerBlockCooldown,
        MeetingManagerCanBlockRepairSabotage,
    }
    private bool canblocksabo;
    private int blockrepocount;
    private static Dictionary<byte, MeetingManager> Block = new(15);
    public List<byte> BlockedPlayer = new();
    public override void OnDestroy()
    {
        Block.Clear();
    }
    private void SendRPC(byte targetId, byte typeId)
    {
        using var sender = CreateSender(CustomRPC.SyncMBlock);

        sender.Writer.Write(typeId);
        sender.Writer.Write(targetId);
    }
    public override void ReceiveRPC(MessageReader reader, CustomRPC rpcType)
    {
        if (rpcType != CustomRPC.SyncMBlock) return;

        var typeId = reader.ReadByte();
        var targetId = reader.ReadByte();

        switch (typeId)
        {
            case 0: //Dictionaryのクリア
                Block.Clear();
                break;
            case 1: //Dictionaryに追加
                Block[targetId] = this;
                break;
            case 2: //DictionaryのKey削除
                Block.Remove(targetId);
                break;
        }
    }
    public override void Add()
    {
        var playerId = Player.PlayerId;
        Player.AddDoubleTrigger();
    }
    public static void SetupCustomOption()
    {
        OptionBlockCooldown = FloatOptionItem.Create(RoleInfo, 10, OptionName.MeetingManagerBlockCooldown, new(2.5f, 180f, 2.5f), 15f, false)
            .SetValueFormat(OptionFormat.Seconds);
        OptionBlockRepoCount = IntegerOptionItem.Create(RoleInfo, 11, OptionName.MeetingManagerBlockRepoCount, new(1, 15, 1), 1, false)
            .SetValueFormat(OptionFormat.Seconds);
        OptionCanBlockRepairSabotage = BooleanOptionItem.Create(RoleInfo, 12, OptionName.MeetingManagerCanBlockRepairSabotage, true, false);
    }
    public void BlockMeeting(PlayerControl killer, PlayerControl target)
    {
        Block[target.PlayerId] = this;
        BlockedPlayer.Add(target.PlayerId);
        SendRPC(target.PlayerId, 1);
        Utils.NotifyRoles(SpecifySeer: killer);
        Main.AllPlayerKillCooldown[killer.PlayerId] = OptionBlockCooldown.GetFloat();
        killer.SyncSettings();
        killer.SetKillCooldown();
        Main.AllPlayerKillCooldown[killer.PlayerId] = Options.DefaultKillCooldown;
        blockrepocount--;
        killer.SyncSettings();
    }

    public override bool OnSabotage(PlayerControl player, SystemTypes systemType, byte amount)
    {
        if (BlockedPlayer.Contains(player.PlayerId) && canblocksabo)
        {
            return false;
        }
        return true;
    }

    public void OnCheckMurderAsKiller(MurderInfo info)
    {
        var (killer, target) = info.AttemptTuple;
        if (!BlockedPlayer.Contains(target.PlayerId) && blockrepocount > 0)
        {
            info.DoKill = killer.CheckDoubleTrigger(target, () => { BlockMeeting(killer, target); });
        }
        info.DoKill &= info.CanKill;
    }

    public override bool OnReportDeadBodyBeforeMeeting(PlayerControl reporter, GameData.PlayerInfo target)
    {
        if (!(BlockedPlayer.Contains(reporter.PlayerId) && target == null)) return true;
        return false;
    }

    public override string GetMark(PlayerControl seer, PlayerControl seen, bool _ = false)
    {
        //seenが省略の場合seer
        seen ??= seer;

        if (!(Block.ContainsValue(this) &&
            Block.ContainsKey(seen.PlayerId))) return "";

        return Utils.ColorString(RoleInfo.RoleColor, " Θ");
    }

    public override string GetProgressText(bool comms = false) => Utils.ColorString(blockrepocount > 0 ? Color.red : Color.gray, $"({blockrepocount})");

}