using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;
using UnityEngine;
using Hazel;
using TownOfHost.Roles.Core;
using TownOfHost.Roles.Core.Interfaces;
using Random = UnityEngine.Random;

namespace TownOfHost.Roles.Impostor;
public sealed class Bomber : RoleBase, IImpostor
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(Bomber),
            player => new Bomber(player),
            CustomRoles.Bomber,
            () => RoleTypes.Impostor,
            CustomRoleTypes.Impostor,
            25500,
            SetupCustomOption,
            "bom"
        );
    public Bomber(PlayerControl player)
    : base(
        RoleInfo,
        player
    )
    {
        BombCount = OptionCanUseBombCount.GetInt();
        BombDelay = Random.RandomRange(OptionBombDelayMin.GetInt(), OptionBombDelayMax.GetInt());
    }

    private static OptionItem OptionBombCooldown;
    private static OptionItem OptionCanUseBombCount;
    private static OptionItem OptionBombDelayMin;
    private static OptionItem OptionBombDelayMax;
    private static OptionItem OptionBomberRadius;
    enum OptionName
    {
        BomberCanBombCount,
        BomberBombCooldown,
        BomberBombDelayMin,
        BomberBombDelayMax,
        BomberRadius,
    }
    static float BombDelay;
    private static int BombCount;
    private static float BomberRadius;
    private static Dictionary<byte, Bomber> Bomb = new(15);
    Dictionary<byte, float> BombedPlayers = new(16);
    List<Vector3> BomberPosition = new();
    public override void OnDestroy()
    {
        Bomb.Clear();
    }
    private void SendRPC(byte targetId, byte typeId)
    {
        using var sender = CreateSender(CustomRPC.SyncBomb);

        sender.Writer.Write(typeId);
        sender.Writer.Write(targetId);
    }
    public override void ReceiveRPC(MessageReader reader, CustomRPC rpcType)
    {
        if (rpcType != CustomRPC.SyncBomb) return;

        var typeId = reader.ReadByte();
        var targetId = reader.ReadByte();

        switch (typeId)
        {
            case 0: //Dictionaryのクリア
                Bomb.Clear();
                break;
            case 1: //Dictionaryに追加
                Bomb[targetId] = this;
                break;
            case 2: //DictionaryのKey削除
                Bomb.Remove(targetId);
                break;
        }
    }
    public override void Add()
    {
        var playerId = Player.PlayerId;
        Player.AddDoubleTrigger();
        BombCount = OptionCanUseBombCount.GetInt();
        BomberRadius = OptionBomberRadius.GetInt();
    }
    public static void SetupCustomOption()
    {
        OptionBombCooldown = FloatOptionItem.Create(RoleInfo, 10, OptionName.BomberBombCooldown, new(2.5f, 180f, 2.5f), 15f, false)
            .SetValueFormat(OptionFormat.Seconds);
        OptionCanUseBombCount = IntegerOptionItem.Create(RoleInfo, 11, OptionName.BomberCanBombCount, new(1, 15, 1), 1, false);
        OptionBombDelayMin = FloatOptionItem.Create(RoleInfo, 12, OptionName.BomberBombDelayMin, new(2.5f, 180f, 2.5f), 15f, false)
            .SetValueFormat(OptionFormat.Seconds);
        OptionBombDelayMax = FloatOptionItem.Create(RoleInfo, 13, OptionName.BomberBombDelayMax, new(2.5f, 180f, 2.5f), 50, false)
            .SetValueFormat(OptionFormat.Seconds);
        OptionBomberRadius = FloatOptionItem.Create(RoleInfo, 14, OptionName.BomberRadius, new(0.5f, 3f, 0.5f), 1f, false).SetValueFormat(OptionFormat.Multiplier);
    }
    public void SetBombed(PlayerControl killer, PlayerControl target)
    {
        Bomb[target.PlayerId] = this;
        BombedPlayers.Add(target.PlayerId, 0f);
        BombCount--;
        SendRPC(target.PlayerId, 1);
        Utils.NotifyRoles(SpecifySeer: killer);
        Main.AllPlayerKillCooldown[killer.PlayerId] = OptionBombCooldown.GetFloat();
        killer.SyncSettings();
        Player.SetKillCooldown();
        Main.AllPlayerKillCooldown[killer.PlayerId] = Options.DefaultKillCooldown;
        killer.SyncSettings();
    }



    public override void OnFixedUpdate(PlayerControl _)
    {
        if (!AmongUsClient.Instance.AmHost) return;

        foreach (var (targetId, timer) in BombedPlayers.ToArray())
        {
            if (timer >= BombDelay)
            {
                var target = Utils.GetPlayerById(targetId);
                KillBomb(target);
                BombedPlayers.Remove(targetId);
                Bomb.Remove(targetId);
            }
            else
            {
                BombedPlayers[targetId] += Time.fixedDeltaTime;
            }
        }
    }

    private void KillBomb(PlayerControl target, bool isButton = false)
    {
        var bomber = Player;
        if (target.IsAlive())
        {
            PlayerState.GetByPlayerId(target.PlayerId).DeathReason = CustomDeathReason.Bombed;
            target.SetRealKiller(bomber);
            BomberPosition.Add(target.transform.position);
            if (AmongUsClient.Instance.AmHost)
            {
                //爆破処理はホストのみ
                foreach (var fireTarget in Main.AllAlivePlayerControls)
                {
                    foreach (var pos in BomberPosition)
                    {
                        var dis = Vector2.Distance(pos, fireTarget.transform.position);
                        if (dis > BomberRadius) continue;
                        if (fireTarget.Is(CustomRoleTypes.Impostor)) continue;
                        {
                            fireTarget.SetRealKiller(Player);
                            fireTarget.RpcMurderPlayerV2(fireTarget);
                            PlayerState.GetByPlayerId(fireTarget.PlayerId).DeathReason = CustomDeathReason.Bombed;
                        }
                    }
                }
                if (!isButton && bomber.IsAlive())
                {
                    RPC.PlaySoundRPC(bomber.PlayerId, Sounds.KillSound);
                    Utils.KillFlash(bomber);
                }
            }
            BombedPlayers.Clear();
        }
    }


    public void OnCheckMurderAsKiller(MurderInfo info)
    {
        var (killer, target) = info.AttemptTuple;
        if (BombCount > 0)
        {
            info.DoKill = killer.CheckDoubleTrigger(target, () => { SetBombed(killer, target); });
        }
        info.DoKill &= info.CanKill;
    }

    public override string GetMark(PlayerControl seer, PlayerControl seen, bool _ = false)
    {
        //seenが省略の場合seer
        seen ??= seer;

        if (!(Bomb.ContainsValue(this) &&
            Bomb.ContainsKey(seen.PlayerId))) return "";

        return Utils.ColorString(RoleInfo.RoleColor, "※");
    }

    public override string GetProgressText(bool comms = false) => Utils.ColorString(BombCount > 0 ? Color.red : Color.gray, $"({BombCount})");
}