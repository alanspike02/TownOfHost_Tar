using AmongUs.GameOptions;

using TownOfHost.Modules;
using TownOfHost.Roles.Core;
using TownOfHost.Roles.Core.Interfaces;

namespace TownOfHost.Roles.Madmate;
public sealed class MadDictator : RoleBase, IKillFlashSeeable, IDeathReasonSeeable
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(MadDictator),
            player => new MadDictator(player),
            CustomRoles.MadDictator,
            () => OptionCanVent.GetBool() ? RoleTypes.Engineer : RoleTypes.Crewmate,
            CustomRoleTypes.Madmate,
            26500,
            SetupOptionItem,
            "md",
            introSound: () => GetIntroSound(RoleTypes.Impostor)
        );
    public MadDictator(PlayerControl player)
    : base(
        RoleInfo,
        player,
     () => HasTask.ForRecompute)
    {
        canSeeKillFlash = Options.MadmateCanSeeKillFlash.GetBool();
        canSeeDeathReason = Options.MadmateCanSeeDeathReason.GetBool();
    }
    private static OptionItem OptionCanVent;
    private static Options.OverrideTasksData Tasks;
    enum OptionName
    {
        CanVent,
    }
    private static bool canSeeKillFlash;
    private static bool canSeeDeathReason;

    public static void SetupOptionItem()
    {
        OptionCanVent = BooleanOptionItem.Create(RoleInfo, 10, OptionName.CanVent, false, false);
        Tasks = Options.OverrideTasksData.Create(RoleInfo, 20);
    }

    public bool CheckKillFlash(MurderInfo info) => canSeeKillFlash;
    public bool CheckSeeDeathReason(PlayerControl seen) => canSeeDeathReason;
    public override (byte? votedForId, int? numVotes, bool doVote) OnVote(byte voterId, byte sourceVotedForId)
    {
        var (votedForId, numVotes, doVote) = base.OnVote(voterId, sourceVotedForId);
        var baseVote = (votedForId, numVotes, doVote);
        if (voterId != Player.PlayerId || sourceVotedForId == Player.PlayerId || sourceVotedForId >= 253 || !Player.IsAlive() || !IsTaskFinished)
        {
            return baseVote;
        }
        MeetingHudPatch.TryAddAfterMeetingDeathPlayers(CustomDeathReason.Suicide, Player.PlayerId);
        Utils.GetPlayerById(sourceVotedForId).SetRealKiller(Player);
        MeetingVoteManager.Instance.ClearAndExile(Player.PlayerId, sourceVotedForId);
        return (votedForId, numVotes, false);
    }
}
