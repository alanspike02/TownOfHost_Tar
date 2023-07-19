using AmongUs.GameOptions;

using TownOfHost.Roles.Core;

namespace TownOfHost.Roles.Crewmate;
public sealed class Gambler : RoleBase
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(Gambler),
            player => new Gambler(player),
            CustomRoles.Gambler,
            () => RoleTypes.Crewmate,
            CustomRoleTypes.Crewmate,
            23700,
            SetupOptionItem,
            "gam",
            "#ffd70f",
            introSound: () => GetIntroSound(RoleTypes.Crewmate)
        );
    public Gambler(PlayerControl player)
    : base(
        RoleInfo,
        player
    )
    {
        MaxSaveVote = OptionMaxSaveVote.GetInt();
        NumOfSaveVote = 0;
        SaveVoteOnVote = OptionSaveVoteOnVote.GetInt();
    }

    private static OptionItem OptionMaxSaveVote;
    private static OptionItem OptionSaveVoteOnVote;
    enum OptionName
    {
        GamblerMaxSaveVote,
        GamblerSaveVoteOnVote,
    }
    public static int MaxSaveVote;
    public static int NumOfSaveVote;
    public static int SaveVoteOnVote;
    private static void SetupOptionItem()
    {
        OptionMaxSaveVote = IntegerOptionItem.Create(RoleInfo, 10, OptionName.GamblerMaxSaveVote, new(1, 99, 1), 5, false)
            .SetValueFormat(OptionFormat.Votes);
        OptionSaveVoteOnVote = IntegerOptionItem.Create(RoleInfo, 11, OptionName.GamblerSaveVoteOnVote, new(1, 99, 1), 1, false)
            .SetValueFormat(OptionFormat.Votes);
    }
    public override (byte? votedForId, int? numVotes, bool doVote) OnVote(byte voterId, byte sourceVotedForId)
    {
        // 既定値
        var (votedForId, numVotes, doVote) = base.OnVote(voterId, sourceVotedForId);
        var baseVote = (votedForId, numVotes, doVote);
        if (voterId != Player.PlayerId || sourceVotedForId >= 253 || !Player.IsAlive())
        {
            return baseVote;
        }
        else if (sourceVotedForId == Player.PlayerId) 
        {
            if (NumOfSaveVote < MaxSaveVote)
            {
                numVotes = 0;
                NumOfSaveVote += SaveVoteOnVote;
            }
        }
        else if (voterId == Player.PlayerId)
        {
            numVotes = NumOfSaveVote;
            NumOfSaveVote = 0;
        }
        return (votedForId, numVotes, doVote);
    }
    public override string GetProgressText(bool comms = false)
    {
        return Utils.ColorString(Utils.GetRoleColor(CustomRoles.Gambler), $" + {NumOfSaveVote}");
    }
}
