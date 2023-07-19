using AmongUs.GameOptions;
using TownOfHost.Roles.Core;
using Random = UnityEngine.Random;

namespace TownOfHost.Roles.Crewmate;
public sealed class Bait : RoleBase
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(Bait),
            player => new Bait(player),
            CustomRoles.Bait,
            () => RoleTypes.Crewmate,
            CustomRoleTypes.Crewmate,
            20000,
            SetupOptionItem,
            "ba",
            "#00f7ff"
        );
    public Bait(PlayerControl player)
    : base(
        RoleInfo,
        player
    )
    {
        baittask = OptionTaskTrigger.GetInt();
    }
    private static OptionItem OptionTaskTrigger;
    private static OptionItem OptionreportDelayMin;
    private static OptionItem OptionreportDelayMax;
    enum OptionName
    {
        BaitTaskTrigger,
        BaitreportDelayMin,
        BaitreportDelayMax,
    }
    private int baittask;
    private static void SetupOptionItem()
    {
        OptionTaskTrigger = IntegerOptionItem.Create(RoleInfo, 10, OptionName.BaitTaskTrigger, new(0, 99, 1), 5, false)
            .SetValueFormat(OptionFormat.Pieces);
        OptionreportDelayMin = IntegerOptionItem.Create(RoleInfo, 11, OptionName.BaitreportDelayMin, new(0, 180, 1), 2, false)
                .SetValueFormat(OptionFormat.Seconds);
        OptionreportDelayMax = IntegerOptionItem.Create(RoleInfo, 12, OptionName.BaitreportDelayMax, new(0, 180, 1), 5, false)
                .SetValueFormat(OptionFormat.Seconds);
    }
    public override void OnMurderPlayerAsTarget(MurderInfo info)
    {
        var (killer, target) = info.AttemptTuple;
        if (target.Is(CustomRoles.Bait) && !info.IsSuicide)
        {
            if (MyTaskState.CompletedTasksCount >= baittask || IsTaskFinished)
            {
                Reports(info, killer, target);
            }
        }
    }
    private void Reports(MurderInfo info, PlayerControl killer, PlayerControl target)
    {
        {
            new LateTask(() => killer.CmdReportDeadBody(target.Data), Random.Range(OptionreportDelayMin.GetInt(), OptionreportDelayMax.GetInt()), "Bait Self Report");
        }
    }

    public override bool OnCompleteTask()
    {
        if (MyTaskState.CompletedTasksCount >= baittask)
        {
            Utils.NotifyRoles();
        }
        return true;
    }

    public override string GetProgressText(bool comms = false)
    {
        var task = MyTaskState.CompletedTasksCount;
        return task >= baittask ? Utils.ColorString(Utils.GetRoleColor(CustomRoles.Bait), $"★") : "";
    }
}