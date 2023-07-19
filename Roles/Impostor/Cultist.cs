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
using TownOfHost.Roles.Crewmate;
using static TownOfHost.Translator;
using MS.Internal.Xml.XPath;
using Rewired;
using Unity.Services.Authentication.Internal;
using TownOfHost.Roles.Neutral;
using TownOfHost.Roles.Vanilla;

namespace TownOfHost.Roles.Impostor;
public sealed class Cultist : RoleBase, IImpostor
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(Cultist),
            player => new Cultist(player),
            CustomRoles.Cultist,
            () => RoleTypes.Impostor,
            CustomRoleTypes.Impostor,
            27500,
            SetupCustomOption,
            "cl"
        );
    public Cultist(PlayerControl player)
    : base(
        RoleInfo,
        player
    )
    {
        FollowerCount = OptionFollowerCount.GetInt();
        NeutralConvert = OptionCanConvertNonCrew.GetBool();

        CustomRoleManager.MarkOthers.Add(GetMarkOthers);
    }

    private static OptionItem OptionFollowerCount;
    private static OptionItem OptionCanConvertNonCrew;
    enum OptionName
    {
        CultistFollowerCount,
        CultistCanConvertNonCrew,
    }
    private static int FollowerCount;
    private bool NeutralConvert;
    private static Dictionary<byte, Cultist> Follower = new(15);
    public List<byte> FollowedPlayer = new();
    public PlayerControl FollowerPlayer;
    private static HashSet<byte> FollowArrow = new();
    public override void OnDestroy()
    {
        Follower.Clear();
        FollowArrow.Clear();
    }
    private void SendRPC(byte targetId, byte typeId)
    {
        using var sender = CreateSender(CustomRPC.SyncFollower);

        sender.Writer.Write(typeId);
        sender.Writer.Write(targetId);
    }
    public override void ReceiveRPC(MessageReader reader, CustomRPC rpcType)
    {
        if (rpcType != CustomRPC.SyncFollower) return;

        var typeId = reader.ReadByte();
        var targetId = reader.ReadByte();

        FollowerPlayer = Utils.GetPlayerById(targetId);
        TargetArrow.Add(FollowerPlayer.PlayerId, Player.PlayerId);

        switch (typeId)
        {
            case 0: //Dictionaryのクリア
                Follower.Clear();
                break;
            case 1: //Dictionaryに追加
                Follower[targetId] = this;
                break;
            case 2: //DictionaryのKey削除
                Follower.Remove(targetId);
                break;
        }
    }
    public override void Add()
    {
        var playerId = Player.PlayerId;
        Player.AddDoubleTrigger();
        FollowerCount = OptionFollowerCount.GetInt();
        FollowArrow.Add(Player.PlayerId);
    }
    public static void SetupCustomOption()
    {
        OptionFollowerCount = IntegerOptionItem.Create(RoleInfo, 10, OptionName.CultistFollowerCount, new(1, 15, 1), 1, false);
        OptionCanConvertNonCrew = BooleanOptionItem.Create(RoleInfo, 11, OptionName.CultistCanConvertNonCrew, true, false);
    }
    public void SetFollower(PlayerControl killer, PlayerControl target)
    {
        Follower[target.PlayerId] = this;
        FollowedPlayer.Add(target.PlayerId);
        FollowerCount--;
        SendRPC(target.PlayerId, 1);
        Utils.NotifyRoles(SpecifySeer: killer);
        killer.SetKillCooldown();
    }

    public void OnCheckMurderAsKiller(MurderInfo info)
    {
        var (killer, target) = info.AttemptTuple;
        if (FollowerCount > 0)
        {
            info.DoKill = killer.CheckDoubleTrigger(target, () => { SetFollower(killer, target); });
        }
        info.DoKill &= info.CanKill;
    }

    public override void AfterMeetingTasks()
    {
        foreach (var followers in Main.AllAlivePlayerControls)
        {
            if (FollowedPlayer.Contains(followers.PlayerId) && !followers.Is(CustomRoles.Follower) && !(!NeutralConvert && !followers.Is(CustomRoleTypes.Crewmate)))
            {
                var follower = CustomRoles.Follower;
                followers.RpcSetCustomRole(follower);
                Utils.MarkEveryoneDirtySettings();
                foreach (var impostors in Main.AllPlayerControls.Where(player => player.Is(CustomRoleTypes.Impostor)).ToArray())
                {
                    NameColorManager.Add(impostors.PlayerId, followers.PlayerId, impostors.GetRoleColorCode());
                    NameColorManager.Add(followers.PlayerId, impostors.PlayerId, impostors.GetRoleColorCode());
                    TargetArrow.Add(followers.PlayerId, impostors.PlayerId);
                    Utils.NotifyRoles(SpecifySeer: impostors);
                }
                Utils.NotifyRoles(followers);
            }
        }
    }


    public override string GetMark(PlayerControl seer, PlayerControl seen, bool _ = false)
    {
        //seenが省略の場合seer
        seen ??= seer;

        if ((FollowedPlayer.Contains(seen.PlayerId) && seen.Is(CustomRoles.Follower) &&
            seer.Is(CustomRoleTypes.Impostor))) return Utils.ColorString(RoleInfo.RoleColor, " (F)");
        else if ((FollowedPlayer.Contains(seen.PlayerId) && !seen.Is(CustomRoles.Follower) &&
            seer.Is(CustomRoles.Cultist))) return Utils.ColorString(Color.gray, " (F)");

        return Utils.ColorString(RoleInfo.RoleColor, "");
    }

    public static string GetMarkOthers(PlayerControl seer, PlayerControl seen, bool isForMeeting = false)
    {
        //seenが省略の場合seer
        seen ??= seer;
        foreach (var arrowId in Main.AllPlayerControls.Where(player => player.Is(CustomRoles.Cultist)).ToArray())
        {
            string text = Utils.ColorString(Palette.ImpostorRed, TargetArrow.GetArrows(seer, arrowId.PlayerId));
            if((seer.Is(CustomRoles.Follower) && seen.Is(CustomRoles.Follower) && !isForMeeting))
            {
                return text;
            }
        }
        return "";
    }

    public override string GetProgressText(bool comms = false) => Utils.ColorString(FollowerCount > 0 ? Color.red : Color.gray, $"◇");
}