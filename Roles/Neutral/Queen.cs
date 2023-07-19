using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using AmongUs.GameOptions;

using TownOfHost.Roles.Core;
using TownOfHost.Roles.Core.Interfaces;
using static TownOfHost.Translator;
using TownOfHost.Modules;
using Hazel;
using TownOfHost.Roles.Impostor;
using TownOfHost.Roles.Vanilla;
using static RoleEffectAnimation;

namespace TownOfHost.Roles.Neutral
{
    public sealed class Queen : RoleBase, IKiller
    {
        public static readonly SimpleRoleInfo RoleInfo =
            SimpleRoleInfo.Create(
                typeof(Queen),
                player => new Queen(player),
                CustomRoles.Queen,
                () => RoleTypes.Impostor,
                CustomRoleTypes.Neutral,
                60000,
                SetupOptionItem,
                "qu",
                "#FFFFEF",
                introSound: () => GetIntroSound(RoleTypes.Shapeshifter),
                countType: CountTypes.Queen
            );
        public Queen(PlayerControl player)
        : base(
            RoleInfo,
            player,
            () => HasTask.False
        )
        {
            KillCooldown = OptionKillCooldown.GetFloat();
            ServantTaskReduce = OptionReduceKCTask.GetFloat();
            ServantKillReduce = OptionReduceKCKill.GetFloat();
            CanVent = OptionCanVent.GetBool();
            HasImpostorVision = OptionHasImpostorVision.GetBool();
            ServantSuicide = OptionKillTypeServantCanSuicide.GetBool();

            CustomRoleManager.MarkOthers.Add(GetMarkOthers);
        }

        private static OptionItem OptionKillCooldown;
        static OptionItem OptionServantCount;
        static OptionItem OptionReduceKCTask;
        static OptionItem OptionReduceKCKill;
        public static OptionItem OptionCanVent;
        private static OptionItem OptionHasImpostorVision;
        private static OptionItem OptionKillTypeServantCanSuicide;
        enum OptionName
        {
            QueenServantCount,
            ServantReduceKCTask,
            ServantReduceKCKill,
            KillerServantCanSuicide,
        }

        private static float KillCooldown;
        private static float ServantTaskReduce;
        private static float ServantKillReduce;
        static int ServCount;
        private static bool HasImpostorVision;
        public static bool CanVent;
        public static bool ServantSuicide;

        private static Dictionary<byte, Queen> Servant = new(15);
        public List<byte> ServantPlayer = new();
        public PlayerControl PCServantPlayer;

        private void SendRPC(byte targetId, byte typeId)
        {
            using var sender = CreateSender(CustomRPC.SyncServant);

            sender.Writer.Write(typeId);
            sender.Writer.Write(targetId);
        }
        public override void ReceiveRPC(MessageReader reader, CustomRPC rpcType)
        {
            if (rpcType != CustomRPC.SyncServant) return;

            var typeId = reader.ReadByte();
            var targetId = reader.ReadByte();

            switch (typeId)
            {
                case 0: //Dictionaryのクリア
                    Servant.Clear();
                    break;
                case 1: //Dictionaryに追加
                    Servant[targetId] = this;
                    break;
                case 2: //DictionaryのKey削除
                    Servant.Remove(targetId);
                    break;
            }
        }

        public override void Add()
        {
            var playerId = Player.PlayerId;
            Player.AddDoubleTrigger();
            ServCount = OptionServantCount.GetInt();

            if (!Main.ResetCamPlayerList.Contains(playerId))
                Main.ResetCamPlayerList.Add(playerId);
        }
        private static void SetupOptionItem()
        {
            OptionKillCooldown = FloatOptionItem.Create(RoleInfo, 10, GeneralOption.KillCooldown, new(2.5f, 180f, 2.5f), 30f, false)
                .SetValueFormat(OptionFormat.Seconds);
            OptionServantCount = IntegerOptionItem.Create(RoleInfo, 11, OptionName.QueenServantCount, new(1, 1000, 1), 1, false)
                .SetValueFormat(OptionFormat.Pieces);
            OptionReduceKCTask = FloatOptionItem.Create(RoleInfo, 12, OptionName.ServantReduceKCTask, new(2.5f, 180f, 2.5f), 2.5f, false)
                .SetValueFormat(OptionFormat.Seconds);
            OptionReduceKCKill = FloatOptionItem.Create(RoleInfo, 13, OptionName.ServantReduceKCKill, new(2.5f, 180f, 2.5f), 5f, false)
                .SetValueFormat(OptionFormat.Seconds);
            OptionCanVent = BooleanOptionItem.Create(RoleInfo, 14, GeneralOption.CanVent, true, false);
            OptionHasImpostorVision = BooleanOptionItem.Create(RoleInfo, 15, GeneralOption.ImpostorVision, true, false);
            OptionKillTypeServantCanSuicide = BooleanOptionItem.Create(RoleInfo, 16, OptionName.KillerServantCanSuicide, true, false);
        }

        public override void ApplyGameOptions(IGameOptions opt) => opt.SetVision(HasImpostorVision);

        public override bool OnInvokeSabotage(SystemTypes systemType) => false;
        public float CalculateKillCooldown()
        {
            foreach (var seva in Main.AllPlayerControls.Where(player => player.Is(CustomRoles.Servant)).ToArray())
            {
                var sevatask = seva.GetPlayerTaskState();
                var sevakill = PlayerState.GetByPlayerId(seva.PlayerId);
                return seva.GetRoleClass() is IKiller killer ? KillCooldown - (sevakill.GetKillCount(true) * ServantKillReduce) : KillCooldown - (sevatask.CompletedTasksCount * ServantTaskReduce);
            }
            return KillCooldown;
        }
        public void SetServant(PlayerControl killer, PlayerControl target)
        {
            var serv = CustomRoles.Servant;
            Servant[target.PlayerId] = this;
            ServantPlayer.Add(target.PlayerId);
            PlayerState.GetByPlayerId(target.PlayerId).SetSubRole(serv);
            NameColorManager.Add(killer.PlayerId, target.PlayerId, killer.GetRoleColorCode());
            NameColorManager.Add(target.PlayerId, killer.PlayerId, killer.GetRoleColorCode());
            ServCount--;
            SendRPC(target.PlayerId, 1);
            Utils.NotifyRoles(target);
            Utils.NotifyRoles(SpecifySeer: killer);
            killer.SetKillCooldown();
        }
        public void OnCheckMurderAsKiller(MurderInfo info)
        {
            var (killer, target) = info.AttemptTuple;
            if (ServCount > 0)
            {
                info.DoKill = killer.CheckDoubleTrigger(target, () => { SetServant(killer, target); });
            }
            info.DoKill &= info.CanKill;
        }

        public override string GetProgressText(bool comms = false) => ServCount > 0 ? Utils.ColorString(Utils.GetRoleColor(CustomRoles.Queen).ShadeColor(0.25f), $"({ServCount})") : "";
        public override string GetMark(PlayerControl seer, PlayerControl seen, bool _ = false)
        {
            //seenが省略の場合seer
            seen ??= seer;

            if ((seer.Is(CustomRoles.Queen) &&
                seen.Is(CustomRoles.Servant))) return Utils.ColorString(RoleInfo.RoleColor, " Ⓠ"); ;
            return "";
        }
        public static string GetMarkOthers(PlayerControl seer, PlayerControl seen, bool isForMeeting = false)
        {
            //seenが省略の場合seer
            seen ??= seer;
            foreach (var arrowId in Main.AllPlayerControls.Where(player => player.Is(CustomRoles.Queen)).ToArray())
            {
                string text = Utils.ColorString(RoleInfo.RoleColor, " Ⓠ"); ;
                if ((seer.Is(CustomRoles.Servant) && seen.Is(CustomRoles.Queen)))
                {
                    return text;
                }
            }
            return "";
        }
    }
}
