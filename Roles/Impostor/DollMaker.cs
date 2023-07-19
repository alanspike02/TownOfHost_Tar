using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using AmongUs.GameOptions;

using TownOfHost.Roles.Core;
using TownOfHost.Roles.Core.Interfaces;
using static TownOfHost.Translator;
using MS.Internal.Xml.XPath;

namespace TownOfHost.Roles.Impostor
{
    public sealed class DollMaker : RoleBase, IImpostor
    {
        public static readonly SimpleRoleInfo RoleInfo =
            SimpleRoleInfo.Create(
                typeof(DollMaker),
                player => new DollMaker(player),
                CustomRoles.DollMaker,
                () => RoleTypes.Impostor,
                CustomRoleTypes.Impostor,
                25700,
                SetupOptionItem,
                "dm"
            );
        public DollMaker(PlayerControl player)
        : base(
            RoleInfo,
            player
        )
        {
            KillDelay = OptionKillDelay.GetFloat();

            DollPlayers.Clear();
        }

        static OptionItem OptionKillDelay;
        enum OptionName
        {
            DollMakerKillDelay
        }

        static float KillDelay;

        public bool CanBeLastImpostor { get; } = false;
        Dictionary<byte, float> DollPlayers = new(14);

        private static void SetupOptionItem()
        {
            OptionKillDelay = FloatOptionItem.Create(RoleInfo, 10, OptionName.DollMakerKillDelay, new(1f, 1000f, 1f), 10f, false)
                .SetValueFormat(OptionFormat.Seconds);
        }

        private void KillDoll(PlayerControl target, bool isButton = false)
        {
            var tmpSpeed = Main.AllPlayerSpeed[Player.PlayerId];
            var dollmaker = Player;
            if (target.IsAlive())
            {
                Main.AllPlayerSpeed[target.PlayerId] = tmpSpeed;
                ReportDeadBodyPatch.CanReport[target.PlayerId] = true;
                target.MarkDirtySettings();
                RPC.PlaySoundRPC(target.PlayerId, Sounds.TaskComplete);
                PlayerState.GetByPlayerId(target.PlayerId).DeathReason = CustomDeathReason.Break;
                target.SetRealKiller(dollmaker);
                CustomRoleManager.OnCheckMurder(
                    dollmaker, target,
                    target, target
                );

                Logger.Info($"Vampireに噛まれている{target.name}を自爆させました。", "Vampire.KillBitten");
                if (!isButton && dollmaker.IsAlive())
                {
                    RPC.PlaySoundRPC(dollmaker.PlayerId, Sounds.KillSound);
                    dollmaker.KillFlash();
                }
            }
            else
            {
                Logger.Info($"Vampireに噛まれている{target.name}はすでに死んでいました。", "Vampire.KillBitten");
            }
        }
            public void OnCheckMurderAsKiller(MurderInfo info)
        {
            if (!info.CanKill) return; //キル出来ない相手には無効
            var (killer, target) = info.AttemptTuple;

            if (target.Is(CustomRoles.Bait)) return;
            if (info.IsFakeSuicide) return;

            //誰かに噛まれていなければ登録
            if (!DollPlayers.ContainsKey(target.PlayerId))
            {
                var tmpSpeed = Main.AllPlayerSpeed[target.PlayerId];
                killer.SetKillCooldown();
                DollPlayers.Add(target.PlayerId, 0f);
                Main.AllPlayerSpeed[target.PlayerId] = Main.MinSpeed;    //tmpSpeedで後ほど値を戻すので代入しています。
                ReportDeadBodyPatch.CanReport[target.PlayerId] = false;
                target.MarkDirtySettings();
            }
            info.DoKill = false;
        }
        public override void OnFixedUpdate(PlayerControl _)
        {
            if (!AmongUsClient.Instance.AmHost || !GameStates.IsInTask) return;
            foreach (var (targetId, timer) in DollPlayers.ToArray())
            {
                var doll = Utils.GetPlayerById(targetId);
                {
                    if (timer >= KillDelay)
                    {
                        KillDoll(doll);
                        DollPlayers.Remove(targetId);
                    }
                    else
                    {
                        DollPlayers[targetId] += Time.fixedDeltaTime;
                    }
                }
                if (!doll.IsAlive())
                {
                    DollPlayers.Remove(targetId);
                }
                else
                {
                    var dollPos = doll.transform.position;//puppetの位置
                    Dictionary<PlayerControl, float> targetDistance = new();
                    foreach (var pc in Main.AllAlivePlayerControls.ToArray())
                    {
                        if (pc.PlayerId != doll.PlayerId && pc.PlayerId != Player.PlayerId)
                        {
                            var dis = Vector2.Distance(dollPos, pc.transform.position);
                            targetDistance.Add(pc, dis);
                        }
                    }
                    if (targetDistance.Keys.Count <= 0) return;

                    var min = targetDistance.OrderBy(c => c.Value).FirstOrDefault();//一番値が小さい
                    var target = min.Key;
                    var KillRange = 1;
                    if (min.Value <= KillRange && doll.CanMove && target.CanMove)
                    {
                        KillDoll(doll);
                        DollPlayers.Remove(targetId);
                    }
                }
            }
        }
        public override void OnReportDeadBody(PlayerControl _, GameData.PlayerInfo __)
        {
            foreach (var targetId in DollPlayers.Keys)
            {
                var target = Utils.GetPlayerById(targetId);
                KillDoll(target, true);
            }
            DollPlayers.Clear();

            return;
        }
        public bool OverrideKillButtonText(out string text)
        {
            text = GetString("DollMakerDollButtonText");
            return true;
        }
    }
}
