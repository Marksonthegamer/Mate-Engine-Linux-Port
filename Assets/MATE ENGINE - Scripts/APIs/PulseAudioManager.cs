using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using AOT;
using Unity.Collections;
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
        public double volume { get; set; }
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

        private List<AudioProgram> _audioPrograms = new();
        private IntPtr _mainloop;
        private IntPtr _context;
        private bool _initialized;
        private bool _contextReady;
        [HideInInspector]
        public bool allSet;
        [HideInInspector]
        public bool callbackRunning;
        
        private readonly object _programsLock = new();
        
        private Thread _mainloopThread;
        private volatile bool _running;

        private void OnEnable()
        {
            Instance = this;
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

            _context = pa_context_new(api, "PulseAudio Monitor");
            if (_context == IntPtr.Zero)
            {
                pa_mainloop_free(_mainloop);
                throw new Exception("Failed to create PulseAudio context");
            }

            // No state callback needed

            if (pa_context_connect(_context, null, 0, IntPtr.Zero) < 0)
            {
                Cleanup();
                throw new Exception("Failed to connect PulseAudio context");
            }

            Log("PulseAudio mainloop initialized. Connecting...");
            _running = true;
            _mainloopThread = new Thread(MainloopThread);
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
            Log("PulseAudio context ready.");
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
            var cb = new PaSinkInputInfoCbT(OnSinkInputInfo);
            IntPtr op = pa_context_get_sink_input_info_list(_context, cb, IntPtr.Zero);

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
            if (eol != 0)
            {
                return;
            }
            
            if (info == IntPtr.Zero)
                return;
            
            try
            {
                var sinkInput = Marshal.PtrToStructure<PaSinkInputInfo>(info);
                var audioProgram = ParseSinkInputProperties(sinkInput);
                if (!audioProgram.IsMuted && !audioProgram.IsCorked && IsAudioRelatedNode(audioProgram.MediaClass) && audioProgram.ProcessName != Path.GetFileName(Process.GetCurrentProcess().ProcessName))
                {
                    lock (_programsLock)
                    {
                        _audioPrograms.Add(audioProgram);
                    }
                    //Log($"Found playback process: {audioProgram.ProcessName}");
                }
            }
            catch (Exception ex)
            {
                ShowError($"Error in sink input callback: {ex.Message}");
            }
        }

        private AudioProgram ParseSinkInputProperties(PaSinkInputInfo sinkInput)
        {
            var volumes = new List<double>();
            for (int i = 0; i < sinkInput.volume.channels; i++)
            {
                volumes.Add(sinkInput.volume.values[i] / 65535f);
            }
            var audioProgram = new AudioProgram
            {
                NodeId = sinkInput.index,
                IsMuted = sinkInput.mute != 0,
                IsCorked = sinkInput.corked != 0,
                Name = $"SinkInput {sinkInput.index}",
                ProcessName = "",
                ProcessId = 0,
                MediaClass = "Stream/SinkInput",
                volume = volumes.Average(),
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

            // Check for audio playback streams (adjust based on common values like "Audio/Playback" in PipeWire)
            return mediaClass.Contains("Audio") || mediaClass.Contains("Stream/SinkInput");
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