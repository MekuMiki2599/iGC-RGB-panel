using System;
using System.Text;
using System.Threading;
using iGameAPI.Contracts.LED;
using iGameAPI.Contracts.LED.iGameLEDDevices;

namespace iGCRGB.Core
{
    public sealed class LightingEngine : IDisposable
    {
        private readonly object _lock = new object();
        private ILEDBase _board;
        private bool _initialized;

        public bool IsInitialized
        {
            get { lock (_lock) return _initialized; }
        }

        public string DeviceSummary { get; private set; }

        /// <summary>初始化灯光链路，返回设备摘要。失败时抛异常。</summary>
        public string Initialize()
        {
            lock (_lock)
            {
                ShutdownInternal();
                try { iGameSpaceCalcAPI.iGameSpaceCalc_Stop(); } catch { }
                try { iGameEasyCalcAPI.iGameEasyCalc_Stop(); } catch { }
                Thread.Sleep(100);

                iGameSpaceCalcAPI.iGameSpaceCalc_Init();
                iGameEasyCalcAPI.iGameEasyCalc_Init();
                Thread.Sleep(100);

                _board = LEDFactory.CreateLEDDevice(iGameLEDDeviceType.MBoard);
                _board.Init();
                int count = _board.GetDeviceCount();
                if (count <= 0)
                    throw new InvalidOperationException("未检测到主板灯光设备（请确认没有同时运行 iGC）。");

                StringBuilder summary = new StringBuilder();
                iGameLEDDevice[] devices = _board.GetDeviceList();
                for (int i = 0; i < devices.Length; i++)
                {
                    if (i > 0) summary.Append(" / ");
                    summary.Append(devices[i].DeviceID);
                    if (devices[i].ChannelCount > 0)
                    {
                        summary.Append("（");
                        for (int c = 0; c < devices[i].ChannelCount; c++)
                        {
                            if (c > 0) summary.Append("+");
                            summary.Append(devices[i].Channel[c].Length);
                        }
                        summary.Append(" 颗灯珠）");
                    }
                }
                DeviceSummary = summary.ToString();

                _board.Set_EasyCalc_Effects();
                _initialized = true;
                AppLog.Info("灯光链路初始化成功：" + DeviceSummary);
                return DeviceSummary;
            }
        }

        /// <summary>按配置下发灯效。</summary>
        public void Apply(LightingConfig config)
        {
            lock (_lock)
            {
                if (!_initialized || _board == null)
                    throw new InvalidOperationException("灯光设备尚未初始化。");

                EffectMeta meta = config.CurrentMeta;
                EffectData data = config.CurrentData;
                iGameEasyCalc_LEDParameter param = new iGameEasyCalc_LEDParameter(0);
                param.LEDType = config.SystemOn ? ParseEffect(meta.Key) : iGameEasyCalc_LEDType.Sleep;
                param.Color.R = (byte)data.R;
                param.Color.G = (byte)data.G;
                param.Color.B = (byte)data.B;
                param.Brightness = data.Bri / 255f;
                // iGC 的同步灯效里灵敏度固定 10；音乐类灯效放开给用户调
                param.Sensitivity = meta.IsMusic ? data.Sensitivity : 10;
                param.Speed = data.Speed;
                param.Direction = data.Dir;
                param.LEDCount = 100;
                param.FPS = config.Fps;

                iGameEasyCalcAPI.iGameEasyCalc_Calc_Effects(param);

                if (param.LEDType == iGameEasyCalc_LEDType.StatusDisplay)
                {
                    iGameEasyCalc_StateRealityData state = new iGameEasyCalc_StateRealityData(true);
                    state.temperature_Reality = config.StateTemp;
                    state.temperature_Min = config.StateLow;
                    state.temperature_Max = config.StateHigh;
                    iGameEasyCalcAPI.iGameEasyCalc_Set_StateReality(state);
                }

                AppLog.Info(string.Format("已应用灯效：{0}（亮度 {1}，颜色 {2},{3},{4}，速度 {5}）",
                    param.LEDType, data.Bri, data.R, data.G, data.B, data.Speed));
            }
        }

        private static iGameEasyCalc_LEDType ParseEffect(string key)
        {
            try
            {
                object value = Enum.Parse(typeof(iGameEasyCalc_LEDType), key, true);
                return (iGameEasyCalc_LEDType)value;
            }
            catch
            {
                return iGameEasyCalc_LEDType.Tune;
            }
        }

        public void Shutdown()
        {
            lock (_lock)
            {
                ShutdownInternal();
            }
        }

        private void ShutdownInternal()
        {
            _initialized = false;
            _board = null;
            try { iGameEasyCalcAPI.iGameEasyCalc_Stop(); } catch { }
            try { iGameSpaceCalcAPI.iGameSpaceCalc_Stop(); } catch { }
        }

        public void Dispose()
        {
            Shutdown();
        }
    }
}
