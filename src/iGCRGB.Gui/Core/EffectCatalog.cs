using System;
using System.Collections.Generic;

namespace iGCRGB.Core
{
    public sealed class EffectMeta
    {
        public string Key;              // iGameEasyCalc_LEDType 枚举名
        public string Title;            // 中文名
        public string Desc;             // 一句话说明
        public string Glyph;            // 列表里的小图标字符
        public string Badge;            // 预览角标
        public bool ShowColor = true;
        public bool ShowSpeed = true;
        public bool ShowDirection;
        public bool ShowSensitivity;
        public bool ShowTemperature;
        public bool IsMusic;
    }

    /// <summary>灯效目录：顺序、名称、说明以及每种灯效可调的参数。</summary>
    public static class EffectCatalog
    {
        public static readonly List<EffectMeta> All = new List<EffectMeta>
        {
            new EffectMeta { Key = "Sleep", Title = "关灯", Desc = "关闭主板 ARGB 灯光", Glyph = "⏻", Badge = "关闭",
                ShowColor = false, ShowSpeed = false },
            new EffectMeta { Key = "Static", Title = "常亮", Desc = "单色常亮，稳定不闪", Glyph = "●", Badge = "基础",
                ShowSpeed = false },
            new EffectMeta { Key = "Breath", Title = "呼吸", Desc = "明暗渐变，像呼吸一样起伏", Glyph = "◐", Badge = "基础" },
            new EffectMeta { Key = "ColorCycle", Title = "色彩循环", Desc = "颜色平滑循环过渡", Glyph = "◔", Badge = "流动",
                ShowColor = false },
            new EffectMeta { Key = "Rainbow", Title = "彩虹", Desc = "七彩流光，循环流动", Glyph = "≋", Badge = "流动",
                ShowColor = false },
            new EffectMeta { Key = "Tune", Title = "流光", Desc = "光带沿灯珠缓缓流动", Glyph = "➤", Badge = "流动",
                ShowDirection = true },
            new EffectMeta { Key = "Flicker", Title = "闪烁", Desc = "随机频率的闪烁效果", Glyph = "✦", Badge = "动态" },
            new EffectMeta { Key = "Lightning", Title = "闪电", Desc = "闪电般的瞬间爆闪", Glyph = "⚡", Badge = "动态" },
            new EffectMeta { Key = "StarryNight", Title = "星空", Desc = "星星点点，夜空中闪烁", Glyph = "✧", Badge = "动态" },
            new EffectMeta { Key = "ImmersiveGameplay", Title = "沉浸式游戏", Desc = "跟随游戏画面变化的氛围灯", Glyph = "▣", Badge = "扩展",
                ShowColor = false, ShowSpeed = false },
            new EffectMeta { Key = "Music", Title = "音乐", Desc = "随音乐节奏律动", Glyph = "♪", Badge = "音乐",
                ShowSpeed = false, ShowSensitivity = true, IsMusic = true },
            new EffectMeta { Key = "MusicA", Title = "音乐 A", Desc = "音乐律动 · 风格 A", Glyph = "♫", Badge = "音乐",
                ShowSpeed = false, ShowSensitivity = true, IsMusic = true },
            new EffectMeta { Key = "MusicC", Title = "音乐 C", Desc = "音乐律动 · 风格 C", Glyph = "♬", Badge = "音乐",
                ShowSpeed = false, ShowSensitivity = true, IsMusic = true },
            new EffectMeta { Key = "MusicD", Title = "音乐 D", Desc = "音乐律动 · 风格 D", Glyph = "♩", Badge = "音乐",
                ShowSpeed = false, ShowSensitivity = true, IsMusic = true },
            new EffectMeta { Key = "RazerConnect", Title = "雷蛇联动", Desc = "与雷蛇设备灯光同步", Glyph = "◈", Badge = "联动",
                ShowColor = false, ShowSpeed = false },
            new EffectMeta { Key = "StatusDisplay", Title = "状态显示", Desc = "用颜色表示 CPU / GPU 温度", Glyph = "℃", Badge = "状态",
                ShowColor = false, ShowSpeed = false, ShowTemperature = true },
            new EffectMeta { Key = "NewStart", Title = "开机", Desc = "开机时的启动动画", Glyph = "↻", Badge = "动态" },
            new EffectMeta { Key = "Pulse", Title = "脉冲", Desc = "一波一波的脉冲光", Glyph = "◉", Badge = "动态" },
        };

        public static EffectMeta Find(string key)
        {
            if (string.IsNullOrEmpty(key)) return All[5];
            foreach (EffectMeta meta in All)
            {
                if (string.Equals(meta.Key, key, StringComparison.OrdinalIgnoreCase)) return meta;
            }
            return All[5];
        }
    }
}
