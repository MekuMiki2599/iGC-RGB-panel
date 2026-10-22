using System;

namespace iGCRGB.Core
{
    /// <summary>
    /// 灯光运行时封装。
    /// 把对厂商组件（iGameAPI.*）的依赖集中在这里，避免主程序在缺少组件时
    /// 因为 CLR 提前解析类型而直接崩溃 —— 这样启动检查才能先给出友好提示。
    /// </summary>
    public static class LightingRuntime
    {
        private static readonly object SyncRoot = new object();
        private static LightingEngine _engine;

        private static LightingEngine Engine
        {
            get
            {
                if (_engine == null) _engine = new LightingEngine();
                return _engine;
            }
        }

        public static bool IsInitialized
        {
            get
            {
                lock (SyncRoot)
                {
                    return _engine != null && _engine.IsInitialized;
                }
            }
        }

        public static string DeviceSummary
        {
            get
            {
                lock (SyncRoot)
                {
                    return _engine == null ? null : _engine.DeviceSummary;
                }
            }
        }

        public static string EnsureInitialized()
        {
            lock (SyncRoot)
            {
                if (!Engine.IsInitialized)
                {
                    return Engine.Initialize();
                }
                return Engine.DeviceSummary;
            }
        }

        public static void Apply(LightingConfig config)
        {
            lock (SyncRoot)
            {
                if (!Engine.IsInitialized)
                {
                    Engine.Initialize();
                }
                Engine.Apply(config);
            }
        }

        public static void Shutdown()
        {
            lock (SyncRoot)
            {
                if (_engine != null)
                {
                    _engine.Shutdown();
                }
            }
        }
    }
}
