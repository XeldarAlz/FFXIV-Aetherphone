using System.Buffers;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Newtonsoft.Json;
using SharpDX;
using SharpDX.D3DCompiler;
using SharpDX.Direct3D;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using SharpDX.Mathematics.Interop;
using SharpDX.Multimedia;
using SharpDX.XAudio2;

namespace Aetherphone.Core.Video
{
internal sealed class Snes9xRenderer : IDisposable
	{
		private const uint RETRO_DEVICE_JOYPAD = 1;

		private static Snes9xRenderer? _instance;

		private static readonly RetroEnvironmentT _envCb = EnvironmentCb;
		private static readonly RetroVideoRefreshT _videoCb = VideoRefreshCb;
		private static readonly RetroAudioSampleT _audioCb = AudioSampleCb;
		private static readonly RetroAudioSampleBatchT _audioBatchCb = AudioBatchCb;
		private static readonly RetroInputPollT _inputPollCb = InputPollCb;
		private static readonly RetroInputStateT _inputStateCb = InputStateCb;

		private static IntPtr _sysDirPtr;
		private static IntPtr _romPathPtr;
		private static string _srmPath = string.Empty;

		private readonly string? _assemblyLocationSnes;
		private readonly string _romsDirectory;
		private readonly short[,] _input = new short[2, 16];
		private readonly Lock _lock = new();

		private Texture2D? _targetTexture;
		private CrtLottesScaler? _scaler;
		private Snes9xAudio? _audio;
		private Thread? _runThread;
		private CancellationTokenSource? _cancel;
		private volatile bool _running;
		private bool _coreInited;
		private double _fps = 60.0;

		internal Snes9xRenderer(string? assemblyLocationSnes, string romsDirectory)
		{
			_assemblyLocationSnes = assemblyLocationSnes;
			_romsDirectory = romsDirectory;
		}

		#region native loading (manual, so the DLL can be unloaded for a clean re-init)
		//Global c++ state, not safe to init twice in one process, un/load dll during runtime with native lib to avoid dangling
		private static IntPtr _lib;

		private static RetroApiVersionFn _apiVersion = null!;
		private static RetroSetEnvironmentFn _setEnvironment = null!;
		private static RetroSetVideoRefreshFn _setVideoRefresh = null!;
		private static RetroSetAudioSampleFn _setAudioSample = null!;
		private static RetroSetAudioSampleBatchFn _setAudioSampleBatch = null!;
		private static RetroSetInputPollFn _setInputPoll = null!;
		private static RetroSetInputStateFn _setInputState = null!;
		private static RetroInitFn _init = null!;
		private static RetroDeinitFn _deinit = null!;
		private static RetroGetSystemInfoFn _getSystemInfo = null!;
		private static RetroGetSystemAvInfoFn _getSystemAvInfo = null!;
		private static RetroLoadGameFn _loadGame = null!;
		private static RetroUnloadGameFn _unloadGame = null!;
		private static RetroRunFn _run = null!;
		private static RetroSetControllerPortDeviceFn _setControllerPortDevice = null!;
		private static RetroSerializeSizeFn _serializeSize = null!;
		private static RetroSerializeFn _serialize = null!;
		private static RetroUnserializeFn _unserialize = null!;
		private static RetroGetMemoryDataFn _getMemoryData = null!;
		private static RetroGetMemorySizeFn _getMemorySize = null!;

		private static T Get<T>(string name) where T : Delegate =>
			Marshal.GetDelegateForFunctionPointer<T>(NativeLibrary.GetExport(_lib, name));

		private static void LoadNative(string dllPath)
		{
			_lib = NativeLibrary.Load(dllPath);
			_apiVersion = Get<RetroApiVersionFn>("retro_api_version");
			_setEnvironment = Get<RetroSetEnvironmentFn>("retro_set_environment");
			_setVideoRefresh = Get<RetroSetVideoRefreshFn>("retro_set_video_refresh");
			_setAudioSample = Get<RetroSetAudioSampleFn>("retro_set_audio_sample");
			_setAudioSampleBatch = Get<RetroSetAudioSampleBatchFn>("retro_set_audio_sample_batch");
			_setInputPoll = Get<RetroSetInputPollFn>("retro_set_input_poll");
			_setInputState = Get<RetroSetInputStateFn>("retro_set_input_state");
			_init = Get<RetroInitFn>("retro_init");
			_deinit = Get<RetroDeinitFn>("retro_deinit");
			_getSystemInfo = Get<RetroGetSystemInfoFn>("retro_get_system_info");
			_getSystemAvInfo = Get<RetroGetSystemAvInfoFn>("retro_get_system_av_info");
			_loadGame = Get<RetroLoadGameFn>("retro_load_game");
			_unloadGame = Get<RetroUnloadGameFn>("retro_unload_game");
			_run = Get<RetroRunFn>("retro_run");
			_setControllerPortDevice = Get<RetroSetControllerPortDeviceFn>("retro_set_controller_port_device");
			_serializeSize = Get<RetroSerializeSizeFn>("retro_serialize_size");
			_serialize = Get<RetroSerializeFn>("retro_serialize");
			_unserialize = Get<RetroUnserializeFn>("retro_unserialize");
			_getMemoryData = Get<RetroGetMemoryDataFn>("retro_get_memory_data");
			_getMemorySize = Get<RetroGetMemorySizeFn>("retro_get_memory_size");
		}

		private static void FreeNative()
		{
			_apiVersion = null!; _setEnvironment = null!; _setVideoRefresh = null!;
			_setAudioSample = null!; _setAudioSampleBatch = null!; _setInputPoll = null!;
			_setInputState = null!; _init = null!; _deinit = null!; _getSystemInfo = null!;
			_getSystemAvInfo = null!; _loadGame = null!; _unloadGame = null!; _run = null!;
			_setControllerPortDevice = null!; _serializeSize = null!; _serialize = null!;
			_unserialize = null!;

			if (_lib != IntPtr.Zero)
			{
				AepLog.Debug($"[SNES9X] diag: freeing native dll, _lib={_lib}");
				NativeLibrary.Free(_lib);
				_lib = IntPtr.Zero;
			}
		}
		#endregion

		#region native types
		[UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate uint RetroApiVersionFn();
		[UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void RetroSetEnvironmentFn(RetroEnvironmentT cb);
		[UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void RetroSetVideoRefreshFn(RetroVideoRefreshT cb);
		[UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void RetroSetAudioSampleFn(RetroAudioSampleT cb);
		[UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void RetroSetAudioSampleBatchFn(RetroAudioSampleBatchT cb);
		[UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void RetroSetInputPollFn(RetroInputPollT cb);
		[UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void RetroSetInputStateFn(RetroInputStateT cb);
		[UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void RetroInitFn();
		[UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void RetroDeinitFn();
		[UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void RetroGetSystemInfoFn(out RetroSystemInfo info);
		[UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void RetroGetSystemAvInfoFn(out RetroSystemAvInfo info);
		[UnmanagedFunctionPointer(CallingConvention.Cdecl)] [return: MarshalAs(UnmanagedType.U1)] private delegate bool RetroLoadGameFn(ref RetroGameInfo game);
		[UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void RetroUnloadGameFn();
		[UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void RetroRunFn();
		[UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void RetroSetControllerPortDeviceFn(uint port, uint device);
		[UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate nuint RetroSerializeSizeFn();
		[UnmanagedFunctionPointer(CallingConvention.Cdecl)] [return: MarshalAs(UnmanagedType.U1)] private delegate bool RetroSerializeFn(IntPtr data, nuint size);
		[UnmanagedFunctionPointer(CallingConvention.Cdecl)] [return: MarshalAs(UnmanagedType.U1)] private delegate bool RetroUnserializeFn(IntPtr data, nuint size);

		[UnmanagedFunctionPointer(CallingConvention.Cdecl)] [return: MarshalAs(UnmanagedType.U1)] private delegate bool RetroEnvironmentT(uint cmd, IntPtr data);
		[UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void RetroVideoRefreshT(IntPtr data, uint width, uint height, nuint pitch);
		[UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void RetroAudioSampleT(short left, short right);
		[UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate nuint RetroAudioSampleBatchT(IntPtr data, nuint frames);
		[UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void RetroInputPollT();
		[UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate short RetroInputStateT(uint port, uint device, uint index, uint id);
		[UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate IntPtr RetroGetMemoryDataFn(uint id);
		[UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate nuint RetroGetMemorySizeFn(uint id);

		[StructLayout(LayoutKind.Sequential)]
		private struct RetroSystemInfo
		{
			internal IntPtr Library_name;
			internal IntPtr Library_version;
			internal IntPtr Valid_extensions;
			[MarshalAs(UnmanagedType.U1)] internal bool Need_fullpath;
			[MarshalAs(UnmanagedType.U1)] internal bool Block_extract;
		}

		[StructLayout(LayoutKind.Sequential)]
		private struct RetroGameGeometry
		{
			internal uint Base_width, Base_height, Max_width, Max_height;
			internal float Aspect_ratio;
		}

		[StructLayout(LayoutKind.Sequential)]
		private struct RetroSystemTiming { internal double Fps, Sample_rate; }

		[StructLayout(LayoutKind.Sequential)]
		private struct RetroSystemAvInfo { internal RetroGameGeometry Geometry; internal RetroSystemTiming Timing; }

		[StructLayout(LayoutKind.Sequential)]
		private struct RetroGameInfo
		{
			internal IntPtr Path;
			internal IntPtr Data;
			internal nuint Size;
			internal IntPtr Meta;
		}

		private const uint ENV_GET_CAN_DUPE = 3;
		private const uint ENV_GET_SYSTEM_DIRECTORY = 9;
		private const uint ENV_SET_PIXEL_FORMAT = 10;
		private const uint ENV_GET_VARIABLE = 15;
		private const uint ENV_GET_VARIABLE_UPDATE = 17;
		private const uint ENV_GET_SAVE_DIRECTORY = 31;
		private const int PIXFMT_RGB565 = 2; //0=0RGB1555 1=XRGB8888 2=RGB565
		#endregion

		internal bool Load(Texture2D? targetTexture, string romPath)
		{
			if (_running)
			{
				Unload();
			}
			AepLog.Debug("Starting: " + romPath);
			lock (_lock)
			{
				if (_assemblyLocationSnes == null)
				{
					AepLog.Error("[SNES9X] core dll path not set");
					return false;
				}

				_instance = this;
				_cancel = new CancellationTokenSource();

				_targetTexture = targetTexture;
				AepLog.Debug($"[SNES9X] diag: using target texture, null={_targetTexture == null}");

				if (_targetTexture != null && DxHandler.Device != null)
				{
					AepLog.Debug("[SNES9X] diag: constructing CrtLottesScaler");
					_scaler = new CrtLottesScaler(DxHandler.Device, _targetTexture);
					AepLog.Debug("[SNES9X] diag: CrtLottesScaler constructed");
				}

				AepLog.Debug($"[SNES9X] diag: loading native dll, prior _lib={_lib}");
				LoadNative(_assemblyLocationSnes);
				AepLog.Debug($"[SNES9X] diag: native dll loaded, _lib={_lib}");

				_setEnvironment(_envCb);
				_setVideoRefresh(_videoCb);
				_setAudioSample(_audioCb);
				_setAudioSampleBatch(_audioBatchCb);
				_setInputPoll(_inputPollCb);
				_setInputState(_inputStateCb);
				AepLog.Debug("[SNES9X] diag: callbacks set, calling retro_init");

				_init();
				AepLog.Debug("[SNES9X] diag: retro_init returned");
				_coreInited = true;

				_getSystemInfo(out RetroSystemInfo si);
				AepLog.Debug($"[SNES9X] core {Marshal.PtrToStringAnsi(si.Library_name)} {Marshal.PtrToStringAnsi(si.Library_version)}");

				AepLog.Debug($"[SNES9X] need_fullpath={si.Need_fullpath}, romPath={romPath}");
				var info = new RetroGameInfo();
				IntPtr dataPtr = IntPtr.Zero;
				try
				{
					if (_romPathPtr != IntPtr.Zero) 
					{
						Marshal.FreeHGlobal(_romPathPtr); 
					}
					_romPathPtr = Marshal.StringToHGlobalAnsi(romPath);

					info.Path = _romPathPtr;

					if (!si.Need_fullpath)
					{
						byte[] rom = File.ReadAllBytes(romPath);
						dataPtr = Marshal.AllocHGlobal(rom.Length);
						Marshal.Copy(rom, 0, dataPtr, rom.Length);
						info.Data = dataPtr;
						info.Size = (nuint)rom.Length;
					}

					if (!_loadGame(ref info))
					{
						AepLog.Error("[SNES9X] retro_load_game failed");
						TeardownLocked();
						return false;
					}
				}
				finally
				{
					if (dataPtr != IntPtr.Zero)
					{
						Marshal.FreeHGlobal(dataPtr);
					}
				}

				_srmPath = Path.ChangeExtension(romPath, ".srm");
				LoadSram();

				_getSystemAvInfo(out RetroSystemAvInfo av);
				_fps = av.Timing.Fps > 1 ? av.Timing.Fps : 60.0;
				_audio = new Snes9xAudio((int)av.Timing.Sample_rate);
				_setControllerPortDevice(0, RETRO_DEVICE_JOYPAD);
				
				SetVolume(25);

				AepLog.Debug($"[SNES9X] loaded {Path.GetFileName(romPath)} @ {_fps:0.##}fps, {av.Timing.Sample_rate:0}Hz");
			}

			_running = true;
			_runThread = new Thread(RunLoop) { IsBackground = true, Name = "snes9x-run" };
			_runThread.Start();
			return true;
		}

		internal void Unload()
		{
			_running = false;
			_cancel?.Cancel();
			_runThread?.Join();
			_runThread = null;

			lock (_lock)
			{
				TeardownLocked();
			}
		}

		public void Dispose()
		{
			Unload();
			GC.SuppressFinalize(this);
		}

		private void TeardownLocked()
		{
			if (_coreInited)
			{
				SaveSramIfChanged();
				AepLog.Debug("[SNES9X] calling unload_game");
				try { 
					_unloadGame();
				}
				catch (Exception e) { AepLog.Error($"[SNES9X] unload_game threw: {e}"); }

				try { _deinit(); }
				catch (Exception e) { AepLog.Error($"[SNES9X] deinit threw: {e}"); }
				_coreInited = false;
				AepLog.Debug("[SNES9X] diag: retro_deinit returned");
			}

			_audio?.Dispose(); _audio = null;
			_scaler?.Dispose(); _scaler = null;
			_targetTexture = null;
			AepLog.Debug("[SNES9X] diag: scaler disposed");

			FreeNative();

			_cancel?.Dispose(); _cancel = null;
			if (_instance == this)
			{
				_instance = null;
			}
		}

		private IntPtr GetDir()
		{
			if (_sysDirPtr == IntPtr.Zero)
			{
				_sysDirPtr = Marshal.StringToHGlobalAnsi(_romsDirectory);
			}
			return _sysDirPtr;
		}

		private DateTime _lastSramCheck = DateTime.UtcNow;
		internal void OnFrameworkUpdate()
		{
			if ((DateTime.UtcNow - _lastSramCheck).TotalSeconds >= 3)
			{
				_lastSramCheck = DateTime.UtcNow;
				SaveSramIfChanged();
			}
		}

		private byte[]? _lastSram;
		private const uint RETRO_MEMORY_SAVE_RAM = 0;
		private void SaveSramIfChanged()
		{
			if (!_coreInited) { return; }
			nuint size = _getMemorySize(RETRO_MEMORY_SAVE_RAM);
			IntPtr ptr = _getMemoryData(RETRO_MEMORY_SAVE_RAM);
			if (size == 0 || ptr == IntPtr.Zero) { return; }

			byte[] sram = new byte[(int)size];
			Marshal.Copy(ptr, sram, 0, (int)size);

			if (_lastSram != null && sram.AsSpan().SequenceEqual(_lastSram)) { return; }
			_lastSram = sram;
			File.WriteAllBytes(_srmPath, sram);
		}

		private void LoadSram()
		{
			if (!File.Exists(_srmPath)) { return; }
			nuint size = _getMemorySize(RETRO_MEMORY_SAVE_RAM);
			IntPtr ptr = _getMemoryData(RETRO_MEMORY_SAVE_RAM);
			if (size == 0 || ptr == IntPtr.Zero) { return; }

			byte[] sram = File.ReadAllBytes(_srmPath);
			int n = Math.Min(sram.Length, (int)size);
			Marshal.Copy(sram, 0, ptr, n);
			_lastSram = sram;
		}

		internal void SetButton(int port, int id, bool pressed)
		{
			if (port is < 0 or > 1 || id is < 0 or > 15)
			{
				return;
			}
			_input[port, id] = (short)(pressed ? 1 : 0);
		}

		private void RunLoop()
		{
			double frameMs = 1000.0 / _fps;
			var sw = Stopwatch.StartNew();
			double next = 0;
			while (_running)
			{
				if (_cancel?.IsCancellationRequested == true)
				{
					break;
				}
				lock (_lock)
				{
					if (!_running || _run == null)
					{
						break;
					}
					_run();
				}
				next += frameMs;
				double wait = next - sw.Elapsed.TotalMilliseconds;
				if (wait > 1)
				{
					Thread.Sleep((int)wait);
				}
				else if (wait < -250)
				{
					next = sw.Elapsed.TotalMilliseconds; //resync
				}
			}
		}
		private static void AudioSampleCb(short left, short right) { } //snes9x uses batch, assign but leave it empty
		private static void InputPollCb() { }
		private static bool EnvironmentCb(uint cmd, IntPtr data)
		{
			var self = _instance;
			if (self == null || data == IntPtr.Zero) { return false; }
			switch (cmd)
			{
				case ENV_SET_PIXEL_FORMAT:
					int fmt = Marshal.ReadInt32(data);
					if (fmt == PIXFMT_RGB565)
					{
						return true;
					}
					AepLog.Warning($"[SNES9X] core requested unsupported pixel format {fmt}");
					return false;
				case ENV_GET_CAN_DUPE:
					Marshal.WriteByte(data, 1);
					return true;
				case ENV_GET_SYSTEM_DIRECTORY:
					Marshal.WriteIntPtr(data, self.GetDir());
					return true;
				case ENV_GET_SAVE_DIRECTORY:
					return false;
				case ENV_GET_VARIABLE_UPDATE:
					Marshal.WriteByte(data, 0);
					return true;
				case ENV_GET_VARIABLE:
				default:
					return false;
			}
		}

		private static void VideoRefreshCb(IntPtr data, uint width, uint height, nuint pitch)
		{
			try
			{
				Snes9xRenderer? self = _instance;
				if (self == null || data == IntPtr.Zero) { return; }
				self._scaler?.Submit(data, (int)width, (int)height, (int)pitch);
			}
			catch { }
		}

		

		private static nuint AudioBatchCb(IntPtr data, nuint frames)
		{
			try { _instance?._audio?.Submit(data, (int)frames); } catch { }
			return frames;
		}

		private static short InputStateCb(uint port, uint device, uint index, uint id)
		{
			try
			{
				var self = _instance;
				if (self == null || device != RETRO_DEVICE_JOYPAD) { return 0; }
				if (port < 2 && id < 16) { return self._input[port, id]; }
			}
			catch { }
			return 0;
		}

		internal void SetVolume(int volume) => _audio?.SetVolume(volume);
	}
	
	internal sealed class Snes9xAudio : IDisposable
	{
		private readonly XAudio2 _xaudio;
		private readonly MasteringVoice _master;
		private readonly SourceVoice _source;
		private readonly System.Collections.Concurrent.ConcurrentDictionary<IntPtr, DataStream> _pending = new();
		private int _ctr;

		internal Snes9xAudio(int sampleRate)
		{
			_xaudio = new XAudio2();
			_master = new MasteringVoice(_xaudio);
			var fmt = new WaveFormat(sampleRate, 16, 2);
			_source = new SourceVoice(_xaudio, fmt, true);
			_source.BufferEnd += OnBufferEnd;
			_source.Start();
		}
		internal void SetVolume(int volume)
		{
			float vol = volume / 100.0f;
			_source.SetVolume(Math.Clamp(vol, 0f, 2f));
		}
		internal void Submit(IntPtr data, int frames)
		{
			int bytes = frames * 4;
			if (bytes <= 0) 
			{
				return;
			}
			//don't let the queue run away if rendering outpaces playback
			if (_source.State.BuffersQueued > 32) 
			{
				return;
			}

			byte[] tmp = ArrayPool<byte>.Shared.Rent(bytes);
			try
			{
				Marshal.Copy(data, tmp, 0, bytes);
				var ds = new DataStream(bytes, true, true);
				ds.Write(tmp, 0, bytes);
				ds.Position = 0;

				IntPtr key = (IntPtr)Interlocked.Increment(ref _ctr);
				_pending[key] = ds;
				var ab = new AudioBuffer { Stream = ds, AudioBytes = bytes, Flags = BufferFlags.None, Context = key };
				_source.SubmitSourceBuffer(ab, null);
			}
			finally
			{
				ArrayPool<byte>.Shared.Return(tmp);
			}
		}

		private void OnBufferEnd(IntPtr context)
		{
			if (_pending.TryRemove(context, out var ds)) 
			{
				ds.Dispose();
			}
		}

		public void Dispose()
		{
			try { _source.Stop(); _source.FlushSourceBuffers(); } catch { }
			_source.BufferEnd -= OnBufferEnd;
			foreach (var kv in _pending) 
			{
				kv.Value.Dispose();
			}
			_pending.Clear();
			_source.DestroyVoice();
			_source.Dispose();
			_master.Dispose();
			_xaudio.Dispose();
		}
	}

	public enum Snes9xInput
	{
		B = 0, Y = 1, SELECT = 2, START = 3,
		UP = 4, DOWN = 5, LEFT = 6, RIGHT = 7,
		A = 8, X = 9, L = 10, R = 11
	}

	//probably not gonna go for crt royale
	internal sealed class CrtLottesScaler : IDisposable
	{
		private const int SrcMaxW = 512, SrcMaxH = 480;
		private const int SrcMaxBytes = SrcMaxW * SrcMaxH * 2; //RGB565, worst case pitch*height

		private const string RenderKey = "snes";

		private readonly Texture2D _src;
		private readonly ShaderResourceView _srv;
		private readonly RenderTargetView _rtv;
		private readonly VertexShader _vs;
		private readonly PixelShader _ps;
		private readonly SamplerState _sampler;
		private readonly SharpDX.Direct3D11.Buffer _cbuf;
		private readonly int _dstW, _dstH;
		private readonly Texture2D _privateRt;
		private readonly Texture2D _shared;

		private readonly IntPtr _snapA = Marshal.AllocHGlobal(SrcMaxBytes);
		private readonly IntPtr _snapB = Marshal.AllocHGlobal(SrcMaxBytes);
		private bool _useSnapA = true;

		internal float MaskStrength = 0.30f; //intensity
		internal float ScanBeam = 2.5f;

		[StructLayout(LayoutKind.Sequential)]
		private struct CrtParams
		{
			internal RawVector2 UvScale;
			internal RawVector2 SrcSize;
			internal RawVector2 OutSize;
			internal float MaskStrength;
			internal float ScanBeam;
		}

		internal CrtLottesScaler(SharpDX.Direct3D11.Device dev, Texture2D displayTarget)
		{
			_dstW = displayTarget.Description.Width;
			_dstH = displayTarget.Description.Height;

			_src = new Texture2D(dev, new Texture2DDescription
			{
				Width = SrcMaxW,
				Height = SrcMaxH,
				MipLevels = 1,
				ArraySize = 1,
				Format = Format.B5G6R5_UNorm,
				SampleDescription = new SampleDescription(1, 0),
				Usage = ResourceUsage.Default,
				BindFlags = BindFlags.ShaderResource,
				CpuAccessFlags = CpuAccessFlags.None,
				OptionFlags = ResourceOptionFlags.None
			});
			_srv = new ShaderResourceView(dev, _src);
			_shared = displayTarget;
			_privateRt = new Texture2D(dev, new Texture2DDescription
			{
				Width = _dstW,
				Height = _dstH,
				MipLevels = 1,
				ArraySize = 1,
				Format = displayTarget.Description.Format,
				SampleDescription = new SampleDescription(1, 0),
				Usage = ResourceUsage.Default,
				BindFlags = BindFlags.RenderTarget | BindFlags.ShaderResource,
				CpuAccessFlags = CpuAccessFlags.None,
				OptionFlags = ResourceOptionFlags.None   // NICHT shared
			});
			_rtv = new RenderTargetView(dev, _privateRt);

			const string hlsl = @"
								cbuffer C : register(b0) {
									float2 uvScale;     // filled fraction of the source texture
									float2 srcSize;     // actual SNES frame size in px
									float2 outSize;     // display target size in px
									float  maskStrength;
									float  scanBeam;
								};
								struct VOut { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };
								VOut VS(uint id : SV_VertexID) {
									VOut o;
									float2 t = float2((id << 1) & 2, id & 2);
									o.pos = float4(t * float2(2,-2) + float2(-1,1), 0, 1);
									o.uv  = t * uvScale;
									return o;
								}
								Texture2D tex : register(t0);
								SamplerState smp : register(s0);

								float4 PS(VOut i) : SV_TARGET {
									// vertical position in source scanlines
									float py = i.uv.y / uvScale.y * srcSize.y;
									float r  = floor(py - 0.5);
									float f  = (py - 0.5) - r;

									// sample the two nearest scanline centers
									float y0 = (r + 0.5) / srcSize.y * uvScale.y;
									float y1 = (r + 1.5) / srcSize.y * uvScale.y;
									float3 c0 = tex.Sample(smp, float2(i.uv.x, y0)).rgb;
									float3 c1 = tex.Sample(smp, float2(i.uv.x, y1)).rgb;

									// un-normalized beam -> brightness dips between scanlines
									float w0 = exp(-scanBeam * f * f);
									float w1 = exp(-scanBeam * (1.0 - f) * (1.0 - f));
									float3 col = c0 * w0 + c1 * w1;

									// aperture-grille mask
									float3 mask = float3(1.0 - maskStrength, 1.0 - maskStrength, 1.0 - maskStrength);
									float mx = fmod(i.pos.x, 6.0);
									if (mx < 1.0)      mask.r = 1.0;
									else if (mx < 2.0) mask.g = 1.0;
									else               mask.b = 1.0;
									col *= mask;

									col *= 1.0 + maskStrength * 0.6; // compensate mask dimming
									return float4(saturate(col), 1.0);
								}";

			using (var vsb = ShaderBytecode.Compile(hlsl, "VS", "vs_4_0"))
			using (var psb = ShaderBytecode.Compile(hlsl, "PS", "ps_4_0"))
			{
				_vs = new VertexShader(dev, vsb);
				_ps = new PixelShader(dev, psb);
			}

			_sampler = new SamplerState(dev, new SamplerStateDescription
			{
				Filter = Filter.MinMagMipLinear,
				AddressU = TextureAddressMode.Clamp,
				AddressV = TextureAddressMode.Clamp,
				AddressW = TextureAddressMode.Clamp,
				ComparisonFunction = Comparison.Never,
				MinimumLod = 0,
				MaximumLod = float.MaxValue
			});

			_cbuf = new SharpDX.Direct3D11.Buffer(dev, 32, ResourceUsage.Default,
				BindFlags.ConstantBuffer, CpuAccessFlags.None, ResourceOptionFlags.None, 0);
		}

		//Called from the emulation thread. 'data' is only valid for the duration of this call (owned by the
		//libretro core), so we snapshot it here and defer the actual D3D work to the game's own render thread.
		internal unsafe void Submit(IntPtr data, int w, int h, int pitch)
		{
			int bytes = pitch * h;
			if (bytes <= 0 || bytes > SrcMaxBytes)
			{
				return;
			}

			IntPtr snapshot = _useSnapA ? _snapA : _snapB;
			_useSnapA = !_useSnapA;
			System.Buffer.MemoryCopy((void*)data, (void*)snapshot, SrcMaxBytes, bytes);

			DxHandler.RunOnRenderThread(RenderKey, () =>
			{
				if (DxHandler.Device != null)
				{
					Blit(DxHandler.Device.ImmediateContext, snapshot, w, h, pitch);
				}
			});
		}

		//Runs on the game's own render thread/context (via DxHandler.RunOnRenderThread) - save and restore
		//whatever pipeline state we touch so the game's own next draw calls aren't affected.
		private void Blit(DeviceContext ctx, IntPtr data, int w, int h, int pitch)
		{
			//Not saving/restoring the viewport here: querying it via SharpDX's generic GetViewports<T>() crashes
			//with an IndexOutOfRangeException on this SharpDX build. We always set our own viewport explicitly
			//before drawing, and the game sets its own again before its next real draw call regardless.
			RenderTargetView[] prevRtvs = ctx.OutputMerger.GetRenderTargets(1, out DepthStencilView? prevDsv);
			RasterizerState? prevRs = ctx.Rasterizer.State;
			BlendState? prevBlend = ctx.OutputMerger.BlendState;
			DepthStencilState? prevDss = ctx.OutputMerger.DepthStencilState;
			VertexShader? prevVs = ctx.VertexShader.Get();
			PixelShader? prevPs = ctx.PixelShader.Get();
			InputLayout? prevIl = ctx.InputAssembler.InputLayout;
			PrimitiveTopology prevTopo = ctx.InputAssembler.PrimitiveTopology;

			try
			{
				var region = new ResourceRegion(0, 0, 0, w, h, 1);
				ctx.UpdateSubresource(_src, 0, region, data, pitch, 0);

				var p = new CrtParams
				{
					UvScale = new RawVector2((float)w / SrcMaxW, (float)h / SrcMaxH),
					SrcSize = new RawVector2(w, h),
					OutSize = new RawVector2(_dstW, _dstH),
					MaskStrength = MaskStrength,
					ScanBeam = ScanBeam
				};
				ctx.UpdateSubresource(ref p, _cbuf);

				ctx.OutputMerger.SetRenderTargets(_rtv);
				ctx.ClearRenderTargetView(_rtv, new RawColor4(0, 0, 0, 1));
				float arW = VideoEngine.ScreenHeight * 4f / 3f;
				float x = (_dstW - arW) / 2f;
				ctx.Rasterizer.SetViewport(x, 0, arW, _dstH);
				ctx.Rasterizer.State = null;
				ctx.OutputMerger.BlendState = null;
				ctx.OutputMerger.DepthStencilState = null;
				ctx.InputAssembler.InputLayout = null;
				ctx.InputAssembler.PrimitiveTopology = PrimitiveTopology.TriangleList;
				ctx.VertexShader.Set(_vs);
				ctx.VertexShader.SetConstantBuffer(0, _cbuf);
				ctx.PixelShader.Set(_ps);
				ctx.PixelShader.SetConstantBuffer(0, _cbuf);
				ctx.PixelShader.SetShaderResource(0, _srv);
				ctx.PixelShader.SetSampler(0, _sampler);
				ctx.Draw(3, 0);

				ctx.OutputMerger.ResetTargets();
				ctx.CopyResource(_privateRt, _shared);
			}
			finally
			{
				ctx.OutputMerger.SetRenderTargets(prevDsv, prevRtvs);
				foreach (RenderTargetView? rtv in prevRtvs)
				{
					rtv?.Dispose();
				}
				prevDsv?.Dispose();

				ctx.Rasterizer.State = prevRs; prevRs?.Dispose();
				ctx.OutputMerger.BlendState = prevBlend; prevBlend?.Dispose();
				ctx.OutputMerger.DepthStencilState = prevDss; prevDss?.Dispose();

				ctx.VertexShader.Set(prevVs); prevVs?.Dispose();
				ctx.PixelShader.Set(prevPs); prevPs?.Dispose();
				ctx.InputAssembler.InputLayout = prevIl; prevIl?.Dispose();
				ctx.InputAssembler.PrimitiveTopology = prevTopo;
			}
		}

		public void Dispose()
		{
			DxHandler.CancelRenderThreadWork(RenderKey);

			Marshal.FreeHGlobal(_snapA);
			Marshal.FreeHGlobal(_snapB);

			_cbuf.Dispose();
			_sampler.Dispose();
			_ps.Dispose();
			_vs.Dispose();
			_rtv.Dispose();
			_srv.Dispose();
			_src.Dispose();
			_privateRt.Dispose();
		}
	}
}