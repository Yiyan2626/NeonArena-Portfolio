using System;
using UnityEngine;

namespace NeonArena
{
    public enum AmmoUpgrade { Piercing, Ricochet, Laser, Vitality, DashCapacity, DashRecovery, Jump }

    [Serializable]
    public sealed class WeaponLoadout
    {
        [SerializeField] int piercing, ricochet, laser, vitality, dashCapacity, dashRecovery, jump;
        [SerializeField] bool frontier;
        [SerializeField] int selectedAmmo = -1;
        public bool FrontierRules => frontier;
        public int SelectedAmmo => selectedAmmo;
        public void UseFrontierRules() { frontier=true; Reset(); }
        public int Jump => jump;
        public int Vitality => vitality;
        public int DashCapacity => dashCapacity;
        public int DashRecovery => dashRecovery;
        public int Piercing => piercing;
        public int Ricochet => ricochet;
        public int Laser => laser;
        public int TotalRanks => piercing + ricochet + laser;
        public int ExtraTargets => piercing == 0 ? 0 : frontier ? 31 : Mathf.Min(12, piercing + 1);
        public int Bounces => ricochet == 0 ? 0 : frontier ? 3 : Mathf.Min(8, ricochet + 1);
        public float DamageMultiplier => frontier ? 1 : 1 + 0.12f * TotalRanks;
        public float BeamDps => laser == 0 ? 0 : (38 + 18 * (laser - 1)) * DamageMultiplier;
        public float BeamWidth => 0.09f + Mathf.Min(0.2f, laser * 0.025f);
        public int Level(AmmoUpgrade type)
        {
            switch (type) { case AmmoUpgrade.Piercing: return piercing; case AmmoUpgrade.Ricochet: return ricochet;
                case AmmoUpgrade.Laser: return laser; case AmmoUpgrade.Vitality: return vitality;
                case AmmoUpgrade.DashCapacity: return dashCapacity; case AmmoUpgrade.Jump: return jump; default: return dashRecovery; }
        }
        public bool CanUpgrade(AmmoUpgrade type) => frontier ?
            ((int)type>=0 && (int)type<=2 || type==AmmoUpgrade.Jump && jump<2 || type==AmmoUpgrade.DashCapacity && dashCapacity<2) : (int)type >= 0 && (int)type <= 6 &&
            (type != AmmoUpgrade.Jump || jump < 2) && (type != AmmoUpgrade.DashCapacity || dashCapacity < 3) && (type != AmmoUpgrade.DashRecovery || dashRecovery < 8);
        public void Upgrade(AmmoUpgrade type)
        {
            if (!CanUpgrade(type)) return;
            if(frontier && (int)type<=2) { selectedAmmo=(int)type; piercing=type==AmmoUpgrade.Piercing?1:0;ricochet=type==AmmoUpgrade.Ricochet?1:0;laser=type==AmmoUpgrade.Laser?1:0;return; }
            if (type == AmmoUpgrade.Piercing) piercing++;
            else if (type == AmmoUpgrade.Ricochet) ricochet++;
            else if (type == AmmoUpgrade.Laser) laser++;
            else if (type == AmmoUpgrade.Vitality) vitality++;
            else if (type == AmmoUpgrade.DashCapacity) dashCapacity++;
            else if (type == AmmoUpgrade.DashRecovery) dashRecovery++;
            else if (type == AmmoUpgrade.Jump) jump++;
        }
        public void Reset() { piercing = ricochet = laser = vitality = dashCapacity = dashRecovery = 0; jump=frontier?1:0;selectedAmmo=-1; }
        public ShotProfile Profile => CreateProfile(true, true);
        public ShotProfile CreateProfile(bool pierceActive, bool ricochetActive) => new ShotProfile(
            pierceActive ? ExtraTargets : 0, ricochetActive ? Bounces : 0,
            ricochetActive && ricochet > 0 ? UpgradeColor(AmmoUpgrade.Ricochet) : UpgradeColor(AmmoUpgrade.Piercing),
            1 + Mathf.Min(0.8f, TotalRanks * 0.07f), frontier && piercing>0);
        public static Color UpgradeColor(AmmoUpgrade type)
        {
            switch (type) { case AmmoUpgrade.Piercing: return new Color(0.15f, 1, 0.86f);
                case AmmoUpgrade.Ricochet: return new Color(0.72f, 0.35f, 1);
                case AmmoUpgrade.Laser: return new Color(1, 0.68f, 0.12f);
                case AmmoUpgrade.Vitality: return new Color(1, 0.35f, 0.45f);
                case AmmoUpgrade.DashCapacity: return new Color(0.3f, 0.7f, 1);
                case AmmoUpgrade.Jump: return new Color(.2f,.8f,1);
                default: return new Color(0.65f, 1, 0.35f); }
        }
        public static string Title(AmmoUpgrade type)
        {
            switch (type) { case AmmoUpgrade.Piercing: return "穿透弹 / PIERCING";
                case AmmoUpgrade.Ricochet: return "反射弹 / RICOCHET";
                case AmmoUpgrade.Laser: return "激光束 / LASER";
                case AmmoUpgrade.Vitality: return "生命强化 / VITALITY";
                case AmmoUpgrade.DashCapacity: return "冲刺储备 / CHARGES";
                case AmmoUpgrade.Jump: return "跳跃模块 / JETPACK";
                default: return "冲刺恢复 / RECOVERY"; }
        }
        public string Preview(AmmoUpgrade type)
        {
            if(frontier) {
                if(type==AmmoUpgrade.Piercing)return "34 伤害 / 0.30 秒一发\n穿透墙体与多个敌人；适合纵队与掩体后目标。";
                if(type==AmmoUpgrade.Ricochet)return "18 伤害 / 0.12 秒一发\n墙面与地面最多反弹 3 次；适合拐角房间。";
                if(type==AmmoUpgrade.Laser)return "82 伤害 / 0.60 秒一发\n远距直线单发；瞬破能量盾，不能穿透。";
                if(type==AmmoUpgrade.Jump)return "跳跃 Lv.1 → Lv.2：解锁喷气背包\n空中按住空格爬升，能量耗尽落下。";
                return "冲刺次数 +1，最多 3 次\n新增次数立即可用；逐次充能。";
            }
            int next = Level(type) + 1;
            if(type==AmmoUpgrade.Jump)return jump==0?"强化跳跃：跳高约 1.6 → 2.6 米。\n下一级解锁喷气背包。":"解锁喷气背包：空中按住空格升空。\n燃料 2.6 秒，落地后恢复。";
            if (type == AmmoUpgrade.Vitality) return "生命上限 +20，并恢复 20 生命。\n保留已有武器与冲刺升级。";
            if (type == AmmoUpgrade.DashCapacity) return "冲刺储备 " + (1 + dashCapacity) + " → " + Mathf.Min(4, 2 + dashCapacity) + " 次。\n增加一个立即可用的充能。\n用完后逐次恢复，上限 4 次。";
            if (type == AmmoUpgrade.DashRecovery) return "每次充能 " + (2 - 0.15f * dashRecovery).ToString("0.00") + " → " + Mathf.Max(0.8f, 2 - 0.15f * next).ToString("0.00") + " 秒。\n恢复时间下限 0.80 秒。\n不延长无敌时间。";
            string detail = type == AmmoUpgrade.Piercing ? "最多命中 " + (1 + Mathf.Min(12, next + 1)) + " 个敌人；3 倍破盾伤害。\n持续 1.6 秒 / 冷却 1.3 秒。" :
                type == AmmoUpgrade.Ricochet ? "碰墙或完整盾牌最多反弹 " + Mathf.Min(8, next + 1) + " 次。\n持续 2.0 秒 / 冷却 1.6 秒。" :
                "持续光束；无法伤害或穿过完整盾牌。\n持续 1.25 秒 / 冷却 2.2 秒。";
            return detail + "\n武器伤害倍率 " + DamageMultiplier.ToString("0.00") + " → " + (DamageMultiplier + 0.12f).ToString("0.00") + "\n已有能力保留，按各自冷却叠加。";
        }

    }

    public readonly struct ShotProfile
    {
        public readonly int ExtraTargets, Bounces;
        public readonly Color Color;
        public readonly float Size;
        public readonly bool ThroughWalls;
        public ShotProfile(int targets, int bounces, Color color, float size = 1, bool throughWalls=false)
        { ExtraTargets = targets; Bounces = bounces; Color = color; Size = size; ThroughWalls=throughWalls; }
        public static ShotProfile Standard => new ShotProfile(0, 0, new Color(0.15f, 0.95f, 1));
    }
}
