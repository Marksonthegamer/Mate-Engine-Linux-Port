#pragma warning disable CS0168

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using AOT;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace PulseAudio
{
    public class AudioProgram
    {
        public string Name { get; set; }
        public uint NodeId { get; set; }
        public string ProcessName { get; set; }
        public int ProcessId { get; set; }
        public bool IsMuted { get; set; }
        public bool IsCorked { get; set; }
        public string MediaClass { get; set; }
        public double Volume { get; set; }
    }

    public class PulseAudioManager : MonoBehaviour
    {
        public static PulseAudioManager Instance;

        #region API

        private const string LibraryName = "libpulse.so.0";

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr pa_mainloop_new();

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        private static extern void pa_mainloop_free(IntPtr mainloop);
        
        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        private static extern void pa_mainloop_wakeup(IntPtr mainloop);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr pa_mainloop_get_api(IntPtr mainloop);
        
        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        private static extern int pa_mainloop_iterate(IntPtr mainloop, int blocking, out int retval);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr pa_context_new(IntPtr mainloopAPI, string name);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        private static extern void pa_context_unref(IntPtr context);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        private static extern int pa_context_connect(IntPtr context, string server, int flags, IntPtr api);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        private static extern void pa_context_disconnect(IntPtr context);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        private static extern uint pa_context_get_state(IntPtr context);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr pa_context_get_sink_input_info_list(IntPtr context, PaSinkInputInfoCbT cb, IntPtr userdata);
        
        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr pa_proplist_gets(IntPtr proplist, [MarshalAs(UnmanagedType.LPStr)] string key);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        private static extern void pa_operation_unref(IntPtr operation);
        
        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        private static extern int pa_operation_get_state(IntPtr operation);
        
        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr pa_stream_new(IntPtr context, string name, ref PaSampleSpec ss, IntPtr map);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        private static extern int pa_stream_connect_record(IntPtr s, string dev, IntPtr attr, int flags);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        private static extern void pa_stream_set_read_callback(IntPtr s, PaStreamRequestCbT cb, IntPtr userdata);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        private static extern int pa_stream_peek(IntPtr s, out IntPtr data, out UIntPtr nbytes);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        private static extern int pa_stream_drop(IntPtr s);
        
        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        private static extern void pa_stream_disconnect(IntPtr s);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        private static extern void pa_stream_unref(IntPtr s);
        
        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr pa_context_subscribe(IntPtr context, PaSubscriptionMask mask, PaContextSuccessCbT cb, IntPtr userdata);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        private static extern void pa_context_set_subscribe_callback(IntPtr context, PaSubscribeCbT cb, IntPtr userdata);
        
        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        private static extern int pa_stream_set_monitor_stream(IntPtr s, uint idx);

        private enum PaSubscriptionMask { SinkInput = 0x0004 }
        
        private enum PaSubscriptionEventType 
        { 
            TypeMask = 0x0030,
            FacilityMask = 0x000F,
            New = 0x0000,
            Change = 0x0010,
            Remove = 0x0020
        }
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void PaContextSuccessCbT(IntPtr c, int success, IntPtr userdata);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void PaSubscribeCbT(IntPtr c, PaSubscriptionEventType t, uint index, IntPtr userdata);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void PaStreamRequestCbT(IntPtr s, UIntPtr nbytes, IntPtr userdata);
        
        private const int PA_STREAM_DONT_MOVE = 0x0200;
        private const int PA_STREAM_PEAK_DETECT = 0x0800;
        private const int PA_STREAM_ADJUST_LATENCY = 0x2000;
        
        private enum PaOperationState
        {
            Running = 0,
            Done = 1,
            Cancelled = 2
        }

        private enum PaContextState
        {
            Unconnected = 0,
            Connecting = 1,
            Authorizing = 2,
            SettingName = 3,
            Ready = 4,
            Failed = 5,
            Terminated = 6
        }

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void PaSinkInputInfoCbT(IntPtr context, IntPtr info, int eol, IntPtr userdata);
        
        private const int PaChannelsMax = 32;

        [StructLayout(LayoutKind.Sequential)]
        private struct PaSampleSpec
        {
            public int format; // format_t (enum, treat as int)
            public uint rate;
            public byte channels;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct PaChannelMap
        {
            public byte channels;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = PaChannelsMax)]
            public int[] map; // pa_channel_position_t (enum, treat as int)
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct PaCVolume
        {
            public byte channels;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = PaChannelsMax)]
            public uint[] values; // pa_volume_t (uint32_t)
        }
        
        [StructLayout(LayoutKind.Sequential)]
        private struct PaSinkInputInfo
        {
            public uint index;
            public IntPtr name;
            public uint owner_module;
            public uint client;
            public uint sink;
            public PaSampleSpec sample_spec;
            public PaChannelMap channel_map;
            public PaCVolume volume;
            public ulong buffer_usec;
            public ulong sink_usec;
            public IntPtr resample_method;
            public IntPtr driver;
            public int mute;
            public IntPtr proplist;
            public int corked;
            public int has_volume;
            public int volume_writable;
            public PaFormatInfo format;
            // Fields after proplist can be omitted if unused
        }
        
        [StructLayout(LayoutKind.Sequential)]
        private struct PaFormatInfo
        {
            public PaEncoding encoding;
            public IntPtr plist;
        }
        
        private enum PaEncoding
        {
            PaEncodingAny,
            PaEncodingPCM,
            PaEncodingAc3Iec61937,
            PaEncodingEAC3Iec61937,
            PaEncodingMpegIec61937,
            PaEncodingDtsIec61937,
            PaEncodingMpeg2AacIec61937,
            PaEncodingMax,
            PaEncodingInvalid = -1
        }
        
        #endregion

        private readonly List<AudioProgram> _audioPrograms = new();
        public Dictionary<uint, float> ProgramPeaks = new ();
        private IntPtr _mainloop;
        private IntPtr _context;
        private bool _initialized;
        private bool _contextReady;
        [HideInInspector]
        public bool allSet;
        [HideInInspector]
        public bool callbackRunning;
        
        private readonly object _programsLock = new();
        
        private PaSinkInputInfoCbT _cachedSinkInputCb;
        private PaSubscribeCbT _cachedSubscriptionCb;
        private PaContextSuccessCbT _successDelegate;
        private PaStreamRequestCbT _cachedReadCb;
        
        private Thread _mainloopThread;
        private volatile bool _running;

        private void OnEnable()
        {
            Instance = this;
            _cachedSinkInputCb = OnSinkInputInfo;
            _cachedSubscriptionCb = OnSubscriptionEvent;
            _cachedReadCb = OnStreamRead;
            _successDelegate = SubscribeSuccessCb;
            Init();
        }

        public void Init()
        {
            if (_initialized) return;

            _mainloop = pa_mainloop_new();
            if (_mainloop == IntPtr.Zero)
            {
                throw new Exception("Failed to create PulseAudio mainloop");
            }

            IntPtr api = pa_mainloop_get_api(_mainloop);

            _context = pa_context_new(api, "MateEngine PulseAudio Wrapper");
            if (_context == IntPtr.Zero)
            {
                pa_mainloop_free(_mainloop);
                throw new Exception("Failed to create PulseAudio context");
            }
            
            if (pa_context_connect(_context, null, 0, IntPtr.Zero) < 0)
            {
                Cleanup();
                throw new Exception("Failed to connect PulseAudio context");
            }

            Log("PulseAudio mainloop initialized. Connecting...");
            _running = true;
            _mainloopThread = new Thread(MainloopThread)
            {
                Name = "PaManThread",
            };
            _mainloopThread.Start();
            StartCoroutine(CheckReady());
            _initialized = true;
        }

        private IEnumerator CheckReady()
        {
            while (true)
            {
                PaContextState state = (PaContextState)pa_context_get_state(_context);
                if (state == PaContextState.Ready)
                {
                    _contextReady = true;
                    pa_context_set_subscribe_callback(_context, _cachedSubscriptionCb, IntPtr.Zero);
                    var op = pa_context_subscribe(_context, PaSubscriptionMask.SinkInput, _successDelegate, IntPtr.Zero);
                    StartCoroutine(WaitForOperation(op, () => {}));
                    break;
                }
                if (state == PaContextState.Failed || state == PaContextState.Terminated)
                {
                    Cleanup();
                    throw new Exception("Failed to connect to PulseAudio");
                }

                yield return null;
            }

            allSet = true;
            Log("PulseAudio context ready. Event subscribed.");
            // Test with: GetPlayingAudioPrograms(programs => { Log($"Found {programs.Count} programs"); });
        }
        
        public void GetPlayingAudioPrograms(Action<List<AudioProgram>> onComplete)
        {
            if (!_initialized || !_contextReady)
            {
                ShowError("PulseAudio not initialized or context not ready");
                onComplete?.Invoke(new List<AudioProgram>());
                return;
            }

            lock (_programsLock)
            {
                _audioPrograms.Clear();
            }

            callbackRunning = true;
            IntPtr op = pa_context_get_sink_input_info_list(_context, _cachedSinkInputCb, IntPtr.Zero);

            if (op == IntPtr.Zero)
            {
                onComplete?.Invoke(new List<AudioProgram>());
                return;
            }

            StartCoroutine(WaitForOperation(op, () =>
            {
                List<AudioProgram> result;
                lock (_programsLock)
                {
                    result = new List<AudioProgram>(_audioPrograms);
                }
                onComplete?.Invoke(result);
                callbackRunning = false;
            }));
        }

        private IEnumerator WaitForOperation(IntPtr op, Action onComplete)
        {
            while (true)
            {
                PaOperationState opState = (PaOperationState)pa_operation_get_state(op);
                if (opState != PaOperationState.Running)
                {
                    break;
                }

                yield return null;
            }

            pa_operation_unref(op);
            onComplete?.Invoke();
        }

        [MonoPInvokeCallback(typeof(PaSinkInputInfoCbT))]
        private void OnSinkInputInfo(IntPtr context, IntPtr info, int eol, IntPtr userdata)
        {
            if (eol != 0 || info == IntPtr.Zero)
            {
                return;
            }
            
            try
            {
                var sinkInput = Marshal.PtrToStructure<PaSinkInputInfo>(info);
                var audioProgram = ParseSinkInputProperties(sinkInput);
                if (!audioProgram.IsMuted && !audioProgram.IsCorked && IsAudioRelatedNode(audioProgram.MediaClass) && audioProgram.ProcessName != Path.GetFileName(Process.GetCurrentProcess().ProcessName))
                {
                    lock (_programsLock)
                    {
                        _audioPrograms.Add(audioProgram);
                        if (SaveLoadHandler.Instance.data.allowedApps.Contains(audioProgram.Name) |
                            SaveLoadHandler.Instance.data.allowedApps.Contains(audioProgram.ProcessName))
                        {
                            StartMonitoringStream(audioProgram.NodeId);
                        }
                    }
                    //Log($"Found playback process: {audioProgram.ProcessName}");
                }
            }
            catch (Exception ex)
            {
                ShowError($"Error in sink input callback: {ex.Message}");
            }
        }
        
        private readonly Dictionary<uint, IntPtr> _activeMonitors = new();
        
        public void StartMonitoringStream(uint sinkInputIndex)
        {
            if (_activeMonitors.TryGetValue(sinkInputIndex, out _)) return;
            PaSampleSpec ss = new PaSampleSpec { format = 5, rate = 25, channels = 1 };
            var stream = pa_stream_new(_context, "MateEnginePeakDetection", ref ss, IntPtr.Zero);
            if (stream == IntPtr.Zero)
            {
                ShowError("Failed to create a stream for monitoring.");
                return;
            }
            
            pa_stream_set_monitor_stream(stream, sinkInputIndex);
            pa_stream_set_read_callback(stream, _cachedReadCb, (IntPtr)sinkInputIndex);
            int connectRet = pa_stream_connect_record(stream, null, IntPtr.Zero, PA_STREAM_DONT_MOVE | PA_STREAM_PEAK_DETECT | PA_STREAM_ADJUST_LATENCY);
            if (connectRet < 0)
            {
                ShowError($"Failed to connect monitor stream for node {sinkInputIndex}");
                pa_stream_unref(stream);
                return;
            }
            _activeMonitors.Add(sinkInputIndex, stream);
            lock (_programsLock)
                ProgramPeaks.TryAdd(sinkInputIndex, -1);
        }
        
        private void StopMonitoringStream(uint index)
        {
            if (!_activeMonitors.TryGetValue(index, out var stream)) return;
            pa_stream_disconnect(stream);
            pa_stream_unref(stream);
            _activeMonitors.Remove(index);
        
            lock (_programsLock)
            {
                _audioPrograms.RemoveAll(p => p.NodeId == index);
                ProgramPeaks.Remove(index);
            }
        }
        
        [MonoPInvokeCallback(typeof(PaStreamRequestCbT))]
        private void OnStreamRead(IntPtr s, UIntPtr nbytes, IntPtr userdata)
        {
            if (pa_stream_peek(s, out IntPtr data, out UIntPtr length) < 0) return;

            if (data != IntPtr.Zero && (long)length > 0)
            {
                int sampleCount = (int)length / sizeof(float); // 4 bytes per float
                float[] samples = new float[sampleCount];
                Marshal.Copy(data, samples, 0, sampleCount);

                float maxPeak = 0f;
                for (int i = 0; i < sampleCount; i++)
                {
                    if (samples[i] > maxPeak) maxPeak = samples[i];
                }
        
                UpdateProgramPeak(userdata, maxPeak);
            }
            pa_stream_drop(s);
        }
        
        [MonoPInvokeCallback(typeof(PaSubscribeCbT))]
        private void OnSubscriptionEvent(IntPtr c, PaSubscriptionEventType t, uint index, IntPtr userdata)
        {
            var type = t & PaSubscriptionEventType.TypeMask;

            if (type == PaSubscriptionEventType.New)
            {
                lock (_audioPrograms)
                {
                    var audioProgram = _audioPrograms.FirstOrDefault(p => p.NodeId == index);
                    if (audioProgram != null && SaveLoadHandler.Instance.data.allowedApps.Contains(audioProgram.Name) |
                        SaveLoadHandler.Instance.data.allowedApps.Contains(audioProgram.ProcessName))
                    {
                        StartMonitoringStream(audioProgram.NodeId);
                    }
                }
            }
            if (type == PaSubscriptionEventType.Remove)
            {
                StopMonitoringStream(index);
            }
        }
        
        private void SubscribeSuccessCb(IntPtr context, int success, IntPtr userdata)
        {
            if (success == 0) ShowError("Failed to subscribe to events");
        }

        private static void UpdateProgramPeak(IntPtr idPtr, float peak)
        {
            uint nodeId = (uint)idPtr.ToInt64();
            
            lock (Instance._programsLock) 
            {
                var program = Instance._audioPrograms.FirstOrDefault(p => p.NodeId == nodeId);
                if (program != null)
                {
                    Instance.ProgramPeaks[nodeId] = peak;
                }
            }
        }

        private AudioProgram ParseSinkInputProperties(PaSinkInputInfo sinkInput)
        {
            var volumes = new List<double>();
            for (int i = 0; i < sinkInput.volume.channels; i++)
            {
                volumes.Add(sinkInput.volume.values[i] / 65536.0);
            }
            var audioProgram = new AudioProgram
            {
                NodeId = sinkInput.index,
                IsMuted = sinkInput.mute != 0,
                IsCorked = sinkInput.corked != 0,
                Name = $"SinkInput {sinkInput.index}",
                ProcessName = "",
                ProcessId = 0,
                MediaClass = "Audio/Stream/Output",
                Volume = volumes.Average(),
            };

            if (sinkInput.proplist == IntPtr.Zero)
                return audioProgram;

            try
            {
                var props = ParsePaProplist(sinkInput.proplist);
                if (props.ContainsKey("media.class"))
                    audioProgram.MediaClass = props["media.class"];
                else
                    audioProgram.MediaClass = "Stream/SinkInput";  // Fallback
                if (props.ContainsKey("application.name"))
                    audioProgram.Name = props["application.name"];
                else if (sinkInput.name != IntPtr.Zero)
                    audioProgram.Name = Marshal.PtrToStringAnsi(sinkInput.name);
                else if (props.ContainsKey("media.name"))
                    audioProgram.Name = props["media.name"];
                if (props.ContainsKey("application.process.binary"))
                    audioProgram.ProcessName = props["application.process.binary"];

                if (props.ContainsKey("application.process.id") &&
                    int.TryParse(props["application.process.id"], out int processId))
                {
                    audioProgram.ProcessId = processId;
                }
                return audioProgram;
            }
            catch (Exception ex)
            {
                ShowError($"Error parsing sink input properties: {ex.Message}");
                return audioProgram;
            }
        }

        private bool IsAudioRelatedNode(string mediaClass)
        {
            if (string.IsNullOrEmpty(mediaClass))
                return false;
            
            return mediaClass.Contains("Audio") || 
                   mediaClass.Contains("Stream") || 
                   mediaClass.Contains("Output");
        }
        
        private Dictionary<string, string> ParsePaProplist(IntPtr proplist)
        {
            var result = new Dictionary<string, string>();
            if (proplist == IntPtr.Zero)
                return result;

            try
            {
                // Common PulseAudio property keys
                string[] keys = {
                    "application.name",
                    "application.process.binary",
                    "application.process.id",
                    "media.name",
                    "media.title",
                    "media.artist",
                    "media.class"  // Added for better stream classification
                };
                foreach (string key in keys)
                {
                    IntPtr valuePtr = pa_proplist_gets(proplist, key);
                    if (valuePtr != IntPtr.Zero)
                    {
                        string value = Marshal.PtrToStringAnsi(valuePtr);
                        if (!string.IsNullOrEmpty(value))
                        {
                            result[key] = value;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                ShowError($"Error parsing proplist: {ex.Message}");
            }

            return result;
        }

        private void MainloopThread()
        {
            while (_running)
            {
                int ret = pa_mainloop_iterate(_mainloop, 1, out int retval);  // blocking=1
                if (ret < 0)
                {
                    // Log errors from main thread if needed, but avoid Unity API calls here
                }
            }
        }

        private void OnApplicationQuit()
        {
            StopAllCoroutines();
            Cleanup();
        }

        public void Cleanup()
        {
            foreach (var key in _activeMonitors.Keys.ToList())
            {
                StopMonitoringStream(key);
            }
            if (_context != IntPtr.Zero)
            {
                pa_context_disconnect(_context);
                pa_context_unref(_context);
                _context = IntPtr.Zero;
            }

            if (_mainloop != IntPtr.Zero)
            {
                if (_mainloopThread != null && _mainloopThread.IsAlive)
                {
                    _running = false;
                    pa_mainloop_wakeup(_mainloop);
                    _mainloopThread.Join();
                }
                pa_mainloop_free(_mainloop);
                _mainloop = IntPtr.Zero;
            }

            _initialized = false;
            _contextReady = false;
            _cachedSinkInputCb = null;
            _cachedSubscriptionCb = null;
            _cachedReadCb = null;
            lock (_programsLock)
            {
                _audioPrograms.Clear();
            }
        }

        void Log(string message)
        {
            Debug.Log($"{typeof(PulseAudioManager)}: {message}");
        }

        void ShowError(string error)
        {
            Debug.LogError($"{typeof(PulseAudioManager)}: {error}");
        }
    }
}
