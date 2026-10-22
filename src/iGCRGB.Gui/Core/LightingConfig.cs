using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace iGCRGB.Core
{
    public sealed class EffectData
    {
        public int Bri = 127;
        public int Speed = 2;
        public int Dir = 0;
        public int Sensitivity = 30;
        public int R = 255;
        public int G = 255;
        public int B = 255;

        public EffectData Clone()
        {
            return (EffectData)MemberwiseClone();
        }

        public void Normalize()
        {
            Bri = Clamp(Bri, 0, 255);
            Speed = Clamp(Speed, 1, 10);
            Dir = Clamp(Dir, 0, 3);
            Sensitivity = Clamp(Sensitivity, 0, 100);
            R = Clamp(R, 0, 255);
            G = Clamp(G, 0, 255);
            B = Clamp(B, 0, 255);
        }

        private static int Clamp(int v, int min, int max)
        {
            return v < min ? min : (v > max ? max : v);
        }
    }

    /// <summary>灯光设置模型 —— 与 iGC 的 LiteRGBEffectConfig 一一对应，另加本程序的界面偏好。</summary>
    public sealed class LightingConfig
    {
        public int Version = 2;
        public bool SystemOn = true;
        public string Effect = "Tune";
        public int Fps = 30;

        public int StateLow = 50;
        public int StateHigh = 70;
        public string StateBase = "CPU";
        public int StateTemp = 40;

        public bool AutoApply = true;
        public bool AutoStart = false;
        public bool CloseToTray = true;

        /// <summary>iGC 灯光配置文件路径；留空时自动在常见安装位置查找。</summary>
        public string IgcConfigPath = "";

        public Dictionary<string, EffectData> Effects = new Dictionary<string, EffectData>(StringComparer.OrdinalIgnoreCase);

        public static LightingConfig CreateDefault()
        {
            LightingConfig cfg = new LightingConfig();
            foreach (EffectMeta meta in EffectCatalog.All)
            {
                EffectData data = new EffectData();
                if (meta.Key == "StatusDisplay" || meta.Key == "ImmersiveGameplay")
                    data.Bri = 255;
                cfg.Effects[meta.Key] = data;
            }
            // 默认使用与界面主题一致的紫色，用户首次启动后可以自由调整
            EffectData accent = new EffectData { Bri = 127, Speed = 2, R = 138, G = 107, B = 255 };
            cfg.Effects["Tune"] = accent.Clone();
            cfg.Effects["Static"] = accent.Clone();
            cfg.Effects["Breath"] = accent.Clone();
            return cfg;
        }

        public void CopyFrom(LightingConfig other)
        {
            if (other == null) return;
            Version = other.Version;
            SystemOn = other.SystemOn;
            Effect = other.Effect;
            Fps = other.Fps;
            StateLow = other.StateLow;
            StateHigh = other.StateHigh;
            StateBase = other.StateBase;
            StateTemp = other.StateTemp;
            AutoApply = other.AutoApply;
            AutoStart = other.AutoStart;
            CloseToTray = other.CloseToTray;
            IgcConfigPath = other.IgcConfigPath;
            Effects = new Dictionary<string, EffectData>(StringComparer.OrdinalIgnoreCase);
            foreach (KeyValuePair<string, EffectData> pair in other.Effects)
                Effects[pair.Key] = pair.Value.Clone();
            Normalize();
        }

        public EffectData GetEffect(string key)
        {
            EffectData data;
            if (!Effects.TryGetValue(key, out data))
            {
                data = new EffectData();
                Effects[key] = data;
            }
            return data;
        }

        public EffectMeta CurrentMeta
        {
            get { return EffectCatalog.Find(Effect); }
        }

        public EffectData CurrentData
        {
            get { return GetEffect(Effect); }
        }

        // ---------------- 读写 ----------------

        /// <summary>iGC 灯光配置文件的候选位置。</summary>
        private static readonly string[] IgcConfigCandidates =
        {
            @"E:\iGC.Lite\CCData\Configs\LiteRGBEffectConfig.json",
            @"D:\iGC.Lite\CCData\Configs\LiteRGBEffectConfig.json",
            @"C:\iGC.Lite\CCData\Configs\LiteRGBEffectConfig.json",
            @"C:\Program Files\iGC.Lite\CCData\Configs\LiteRGBEffectConfig.json",
            @"C:\Program Files (x86)\iGC.Lite\CCData\Configs\LiteRGBEffectConfig.json"
        };

        /// <summary>解析实际可用的 iGC 配置文件：优先使用配置中的路径，否则在常见位置查找。</summary>
        public static string ResolveIgcConfigPath(string configured)
        {
            if (!string.IsNullOrEmpty(configured) && File.Exists(configured))
                return configured;
            foreach (string candidate in IgcConfigCandidates)
            {
                if (File.Exists(candidate)) return candidate;
            }
            return null;
        }

        public static LightingConfig LoadOrCreate(string path, out bool created)
        {
            created = false;
            try
            {
                if (File.Exists(path))
                {
                    string text = File.ReadAllText(path, Encoding.UTF8);
                    object root = Json.Parse(text);
                    LightingConfig cfg = FromJson(root);
                    cfg.Normalize();
                    return cfg;
                }
            }
            catch
            {
                // 文件损坏时退回默认值，并由调用方决定是否覆盖
            }

            created = true;
            LightingConfig fresh = CreateDefault();
            fresh.AutoStart = StartupManager.IsEnabled();
            fresh.TryImportIgc(fresh.IgcConfigPath, out _);
            return fresh;
        }

        private static LightingConfig FromJson(object root)
        {
            LightingConfig cfg = CreateDefault();
            if (root == null) return cfg;
            cfg.Version = Json.GetInt(root, "version", 2);
            cfg.SystemOn = Json.GetBool(root, "systemOn", true);
            cfg.Effect = Json.GetString(root, "effect", cfg.Effect);
            cfg.Fps = Json.GetInt(root, "fps", cfg.Fps);
            cfg.StateLow = Json.GetInt(root, "stateLow", cfg.StateLow);
            cfg.StateHigh = Json.GetInt(root, "stateHigh", cfg.StateHigh);
            cfg.StateBase = Json.GetString(root, "stateBase", cfg.StateBase);
            cfg.StateTemp = Json.GetInt(root, "stateTemp", cfg.StateTemp);
            cfg.AutoApply = Json.GetBool(root, "autoApply", cfg.AutoApply);
            cfg.AutoStart = Json.GetBool(root, "autoStart", cfg.AutoStart);
            cfg.CloseToTray = Json.GetBool(root, "closeToTray", cfg.CloseToTray);
            cfg.IgcConfigPath = Json.GetString(root, "igcConfigPath", cfg.IgcConfigPath);

            Dictionary<string, object> effects = Json.AsObject(Json.Get(root, "effects"));
            if (effects != null)
            {
                foreach (KeyValuePair<string, object> pair in effects)
                {
                    Dictionary<string, object> e = Json.AsObject(pair.Value);
                    if (e == null) continue;
                    EffectData data = cfg.GetEffect(pair.Key);
                    data.Bri = Json.GetInt(pair.Value, "bri", data.Bri);
                    data.Speed = Json.GetInt(pair.Value, "speed", data.Speed);
                    data.Dir = Json.GetInt(pair.Value, "dir", data.Dir);
                    data.Sensitivity = Json.GetInt(pair.Value, "sensitivity", data.Sensitivity);
                    data.R = Json.GetInt(pair.Value, "r", data.R);
                    data.G = Json.GetInt(pair.Value, "g", data.G);
                    data.B = Json.GetInt(pair.Value, "b", data.B);
                }
            }
            return cfg;
        }

        public void Normalize()
        {
            if (!Effects.ContainsKey(EffectCatalog.Find(Effect).Key))
                Effect = "Tune";
            Fps = Fps < 1 ? 1 : (Fps > 60 ? 60 : Fps);
            StateLow = Clamp(StateLow, 0, 120);
            StateHigh = Clamp(StateHigh, 0, 120);
            StateTemp = Clamp(StateTemp, 0, 120);
            foreach (KeyValuePair<string, EffectData> pair in Effects)
                pair.Value.Normalize();
        }

        private static int Clamp(int v, int min, int max)
        {
            return v < min ? min : (v > max ? max : v);
        }

        public void Save(string path)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("{");
            sb.AppendLine("  \"version\": 2,");
            sb.AppendLine("  \"systemOn\": " + (SystemOn ? "true" : "false") + ",");
            sb.AppendLine("  \"effect\": \"" + Json.Escape(Effect) + "\",");
            sb.AppendLine("  \"fps\": " + Fps + ",");
            sb.AppendLine("  \"stateLow\": " + StateLow + ",");
            sb.AppendLine("  \"stateHigh\": " + StateHigh + ",");
            sb.AppendLine("  \"stateBase\": \"" + Json.Escape(StateBase) + "\",");
            sb.AppendLine("  \"stateTemp\": " + StateTemp + ",");
            sb.AppendLine("  \"autoApply\": " + (AutoApply ? "true" : "false") + ",");
            sb.AppendLine("  \"autoStart\": " + (AutoStart ? "true" : "false") + ",");
            sb.AppendLine("  \"closeToTray\": " + (CloseToTray ? "true" : "false") + ",");
            sb.AppendLine("  \"igcConfigPath\": \"" + Json.Escape(IgcConfigPath) + "\",");
            sb.AppendLine("  \"effects\": {");
            int index = 0;
            foreach (EffectMeta meta in EffectCatalog.All)
            {
                EffectData d = GetEffect(meta.Key);
                d.Normalize();
                sb.Append("    \"" + meta.Key + "\": { \"bri\": " + d.Bri + ", \"speed\": " + d.Speed
                          + ", \"dir\": " + d.Dir + ", \"sensitivity\": " + d.Sensitivity
                          + ", \"r\": " + d.R + ", \"g\": " + d.G + ", \"b\": " + d.B + " }");
                sb.AppendLine(++index < EffectCatalog.All.Count ? "," : "");
            }
            sb.AppendLine("  }");
            sb.AppendLine("}");
            string dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));
        }

        // ---------------- 从 iGC 导入 ----------------

        /// <summary>把 iGC 的 LiteRGBEffectConfig.json 完整读进本配置（所有灯效参数、总开关、温度区间）。</summary>
        public bool TryImportIgc(string path, out string message)
        {
            message = null;
            string resolved = ResolveIgcConfigPath(path);
            if (resolved == null)
            {
                message = "未找到 iGC 灯光配置文件，请先在 iGC 中保存一次灯光预设。";
                return false;
            }
            path = resolved;
            try
            {
                object root = Json.Parse(File.ReadAllText(path, Encoding.UTF8));
                SystemOn = Json.GetBool(root, "RGBSystemSwitch", true);

                int effectIndex = Json.GetInt(root, "CheckedEffect", 5);
                string effectName = EffectNameFromIndex(effectIndex);
                if (effectName != null) Effect = effectName;

                object effectParam = Json.Get(root, "EffectParam");
                foreach (EffectMeta meta in EffectCatalog.All)
                {
                    object p = Json.Get(effectParam, meta.Key);
                    if (p == null) continue;
                    EffectData data = GetEffect(meta.Key);
                    data.Bri = Json.GetInt(p, "Bri", data.Bri);
                    data.Speed = Json.GetInt(p, "Speed", data.Speed);
                    data.Dir = Json.GetInt(p, "Dir", data.Dir);
                    data.Sensitivity = Json.GetInt(p, "Sensitivity", data.Sensitivity);
                    object c = Json.Get(p, "Color");
                    if (c != null)
                    {
                        data.R = Json.GetInt(c, "R", data.R);
                        data.G = Json.GetInt(c, "G", data.G);
                        data.B = Json.GetInt(c, "B", data.B);
                    }
                }

                StateLow = Json.GetInt(root, "Effect_State_Low", StateLow);
                StateHigh = Json.GetInt(root, "Effect_State_High", StateHigh);
                int stateBase = Json.GetInt(root, "Effect_State_Base", 0);
                StateBase = stateBase == 1 ? "GPU" : "CPU";
                Normalize();

                message = string.Format("已导入 iGC 预设：{0} 个灯效，当前灯效 {1}",
                    EffectCatalog.All.Count, EffectCatalog.Find(Effect).Title);
                return true;
            }
            catch (Exception ex)
            {
                message = "读取 iGC 配置失败：" + ex.Message;
                return false;
            }
        }

        private static string EffectNameFromIndex(int index)
        {
            string[] names =
            {
                "Sleep", "Static", "Breath", "ColorCycle", "Rainbow", "Tune", "Flicker", "Lightning",
                "StarryNight", "ImmersiveGameplay", "Music", "MusicA", "RazerConnect", "StatusDisplay",
                "MusicC", "MusicD", "NewStart", "Pulse"
            };
            if (index < 0 || index >= names.Length) return null;
            return names[index];
        }
    }
}
