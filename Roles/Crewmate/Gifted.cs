using System;
using System.Collections.Generic;
using UnityEngine;

using TownOfHost.Attributes;
using AmongUs.GameOptions;
using TownOfHost.Roles.Core;
using static TownOfHost.Options;
using TownOfHost.Roles.Core.Interfaces;
using System.Linq;
using TownOfHost.Roles.Vanilla;

namespace TownOfHost.Roles.Crewmate;
public sealed class Gifted : RoleBase, IKillFlashSeeable, IDeathReasonSeeable
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(Gifted),
            player => new Gifted(player),
            CustomRoles.Gifted,
            () => RoleTypes.Crewmate,
            CustomRoleTypes.Crewmate,
            28000,
            SetupOptionItem,
            "gt",
            "#8FEAB2"
        );
    public Gifted(PlayerControl player)
    : base(
        RoleInfo,
        player
    )
    {
    }
    private static OptionItem OptionSeeVoteTask;
    private static OptionItem OptionSeeDeathInfoTask;
    private static OptionItem OptionSeeKillFlashTask;
    private static OptionItem OptionIncreaseVisionTask;
    private static OptionItem OptionRevealSelfTask;
    enum OptionName
    {
        GiftedSeeVoteTask,
        GiftedDeathInfoTask,
        GiftedKillFlashTask,
        GiftedIncreaseVisionTask,
        GiftedRevealSelfTask,
    }

    public override void Add()
    {
        var playerId = Player.PlayerId;
    }
    private static void SetupOptionItem()
    {
        OptionSeeVoteTask = IntegerOptionItem.Create(RoleInfo, 10, OptionName.GiftedSeeVoteTask, new(1, 100, 1), 1, false)
            .SetValueFormat(OptionFormat.Pieces);
        OptionSeeDeathInfoTask = IntegerOptionItem.Create(RoleInfo, 11, OptionName.GiftedDeathInfoTask, new(1, 100, 1), 2, false)
            .SetValueFormat(OptionFormat.Pieces);
        OptionSeeKillFlashTask = IntegerOptionItem.Create(RoleInfo, 12, OptionName.GiftedKillFlashTask, new(1, 100, 1), 3, false)
            .SetValueFormat(OptionFormat.Pieces);
        OptionIncreaseVisionTask = IntegerOptionItem.Create(RoleInfo, 13, OptionName.GiftedIncreaseVisionTask, new(1, 100, 1), 4, false)
            .SetValueFormat(OptionFormat.Pieces);
        OptionRevealSelfTask = IntegerOptionItem.Create(RoleInfo, 14, OptionName.GiftedRevealSelfTask, new(1, 100, 1), 6, false)
            .SetValueFormat(OptionFormat.Pieces);
    }

    //opt.SetBool(BoolOptionNames.AnonymousVotes, false);
    public override void ApplyGameOptions(IGameOptions opt)
    {
        if (MyTaskState.CompletedTasksCount >= OptionSeeVoteTask.GetInt())
        {
            opt.SetBool(BoolOptionNames.AnonymousVotes, false);
        }
        if (MyTaskState.CompletedTasksCount >= OptionIncreaseVisionTask.GetInt())
        {
            var crewLightMod = FloatOptionNames.CrewLightMod;
            opt.SetFloat(crewLightMod, 3);
        }
    }

    public bool CheckSeeDeathReason(PlayerControl seen) => MyTaskState.CompletedTasksCount >= OptionSeeDeathInfoTask.GetInt();

    public bool CheckKillFlash(MurderInfo info) => MyTaskState.CompletedTasksCount >= OptionSeeKillFlashTask.GetInt();
    public override bool OnCompleteTask()
    {
        {
            if(MyTaskState.CompletedTasksCount >= OptionRevealSelfTask.GetInt())
            {
                foreach (var players in Main.AllAlivePlayerControls)
                {
                    NameColorManager.Add(players.PlayerId, Player.PlayerId, Player.GetRoleColorCode());
                }
            }
            Player.MarkDirtySettings();
        }
        return true;
    }
}