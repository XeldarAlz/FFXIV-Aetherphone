using System.Runtime.InteropServices;
using SharpDX.Direct3D11;

namespace Aetherphone.Core.Video
{
	internal class MpvRenderer : IDisposable
	{
		private const string DLL = "libmpv-2";
		private static Resources? _resources;
		public static void Setup(Resources resources)
		{
			_resources = resources;
		}
		[DllImport(DLL, CallingConvention = CallingConvention.Cdecl)] private static extern IntPtr mpv_create();
		[DllImport(DLL, CallingConvention = CallingConvention.Cdecl)] private static extern int mpv_initialize(IntPtr ctx);
		[DllImport(DLL, CallingConvention = CallingConvention.Cdecl)] private static extern int mpv_set_option_string(IntPtr ctx, string name, string data);
		[DllImport(DLL, CallingConvention = CallingConvention.Cdecl)] private static extern int mpv_command(IntPtr ctx, string[] args);
		[DllImport(DLL, CallingConvention = CallingConvention.Cdecl)] private static extern int mpv_render_context_create(ref IntPtr res, IntPtr ctx, IntPtr parms);
		[DllImport(DLL, CallingConvention = CallingConvention.Cdecl)] private static extern int mpv_render_context_render(IntPtr ctx, IntPtr parms);
		[DllImport(DLL, CallingConvention = CallingConvention.Cdecl)] private static extern void mpv_render_context_free(IntPtr ctx);
		[DllImport(DLL, CallingConvention = CallingConvention.Cdecl)] private static extern void mpv_render_context_set_update_callback(IntPtr ctx, MpvRenderUpdateFn callback, IntPtr callback_ctx);
		[DllImport(DLL, CallingConvention = CallingConvention.Cdecl)] private static extern ulong mpv_render_context_update(IntPtr ctx);
		[DllImport(DLL, CallingConvention = CallingConvention.Cdecl)] private static extern IntPtr mpv_wait_event(IntPtr ctx, double timeout);
		[DllImport(DLL, CallingConvention = CallingConvention.Cdecl)] private static extern int mpv_request_log_messages(IntPtr ctx, string min_level);
		[DllImport(DLL, CallingConvention = CallingConvention.Cdecl)] private static extern void mpv_terminate_destroy(IntPtr ctx);
		[DllImport(DLL, CallingConvention = CallingConvention.Cdecl)] private static extern int mpv_get_property(IntPtr ctx, string name, int format, out double data);
		[DllImport(DLL, CallingConvention = CallingConvention.Cdecl)] private static extern int mpv_get_property(IntPtr ctx, string name, int format, IntPtr data);
		[DllImport(DLL, CallingConvention = CallingConvention.Cdecl)] private static extern IntPtr mpv_get_property_string(IntPtr ctx, string name);
		[DllImport(DLL, CallingConvention = CallingConvention.Cdecl)] private static extern void mpv_free(IntPtr data);

		[StructLayout(LayoutKind.Sequential)]
		private struct MpvRenderParam { public int Type; public IntPtr Data; }

		public delegate void MpvRenderUpdateFn(IntPtr callback_ctx);

		private const string RenderKey = "mpv";

		private IntPtr _mpvCtx;
		private IntPtr _mpvRenderCtx;
		private IntPtr _bufferPtr;
		private IntPtr _snapA, _snapB;
		private bool _useSnapA = true;
		private int _frameBytes;
		private int _width, _height;
		private CancellationTokenSource? _cancelToken;
		private IntPtr _renderParamsPtr;
		private IntPtr _sizePtr, _stridePtr, _formatPtr;
		private Texture2D? _targetTexture;
		private ManualResetEventSlim _frameReady = new ManualResetEventSlim(false);
		private MpvRenderUpdateFn? _updateCallback;
		private GCHandle _updateCallbackHandle;
		private bool _closed = true;
		private Thread? _eventThread;
		private readonly Lock _snapshotLock = new();
		private IntPtr _latestSnapshot;

		public void Initialize(int width, int height, Texture2D? targetTexture, CancellationTokenSource cancelToken,
			bool hardwareDecoding = false, int maxQualityHeight = 1080, bool allowInsecureDirectUrls = false,
			int initialVolume = 60)
		{
			_width = width;
			_height = height;
			_cancelToken = cancelToken;
			_targetTexture = targetTexture;

			_frameBytes = width * height * 4;
			_bufferPtr = Marshal.AllocHGlobal(_frameBytes);
			_snapA = Marshal.AllocHGlobal(_frameBytes);
			_snapB = Marshal.AllocHGlobal(_frameBytes);

			_mpvCtx = mpv_create();
			_ = mpv_set_option_string(_mpvCtx, "vo", "libmpv");
			// Not measured on this project's Wine/RADV setup - mpv has no GPU render path here
			// either way, only decode could benefit. Off is the safe default; read fresh here so a
			// settings change takes effect on the next video, not the current one.
			_ = mpv_set_option_string(_mpvCtx, "hwdec", hardwareDecoding ? "auto-safe" : "no");
			_ = mpv_set_option_string(_mpvCtx, "profile", "sw-fast");
			_ = mpv_set_option_string(_mpvCtx, "ytdl", "yes");
			_ = mpv_set_option_string(_mpvCtx, "script-opts", $"ytdl_hook-ytdl_path={_resources?.GetLocationYTDLP()}");
			_ = mpv_set_option_string(_mpvCtx, "ytdl-format", $"bestvideo[height<={maxQualityHeight}][ext=mp4]+bestaudio/best[height<={maxQualityHeight}]");
			_ = mpv_set_option_string(_mpvCtx, "terminal", "yes");
			_ = mpv_set_option_string(_mpvCtx, "volume", initialVolume.ToString(System.Globalization.CultureInfo.InvariantCulture));
			_ = mpv_set_option_string(_mpvCtx, "msg-level", "all=warn,ffmpeg=error");
			_ = mpv_set_option_string(_mpvCtx, "ytdl-raw-options", "force-ipv4=,hls-use-mpegts=");
			_ = mpv_set_option_string(_mpvCtx, "idle", "yes");
			_ = mpv_set_option_string(_mpvCtx, "keep-open", "yes");
			// Wine's own certificate store is essentially empty by default - only disabling
			// verification worked around it on this project's Wine setup. Never applies on real
			// Windows, and only when the user has explicitly opted in.
			if (WineEnvironment.IsWine && allowInsecureDirectUrls)
			{
				_ = mpv_set_option_string(_mpvCtx, "tls-verify", "no");
			}
			_ = mpv_request_log_messages(_mpvCtx, "warn");
			_ = mpv_initialize(_mpvCtx);

			nint apiTypePtr = Marshal.StringToHGlobalAnsi("sw");

			IntPtr paramsPtr = Marshal.AllocHGlobal(Marshal.SizeOf<MpvRenderParam>() * 2);
			Marshal.StructureToPtr(new MpvRenderParam { Type = 1, Data = apiTypePtr }, paramsPtr, false);
			Marshal.StructureToPtr(new MpvRenderParam { Type = 0, Data = IntPtr.Zero }, paramsPtr + 16, false);

			int rc = mpv_render_context_create(ref _mpvRenderCtx, _mpvCtx, paramsPtr);

			Marshal.FreeHGlobal(apiTypePtr);
			Marshal.FreeHGlobal(paramsPtr);

			_sizePtr = Marshal.AllocHGlobal(8);
			Marshal.WriteInt32(_sizePtr, _width);
			Marshal.WriteInt32(_sizePtr + 4, _height);

			_stridePtr = Marshal.AllocHGlobal(IntPtr.Size);
			Marshal.WriteIntPtr(_stridePtr, new IntPtr(_width * 4));

			_formatPtr = Marshal.StringToHGlobalAnsi("bgra");

			_renderParamsPtr = Marshal.AllocHGlobal(Marshal.SizeOf<MpvRenderParam>() * 5);
			Marshal.StructureToPtr(new MpvRenderParam { Type = 17, Data = _sizePtr }, _renderParamsPtr, false);
			Marshal.StructureToPtr(new MpvRenderParam { Type = 18, Data = _formatPtr }, _renderParamsPtr + 16, false);
			Marshal.StructureToPtr(new MpvRenderParam { Type = 19, Data = _stridePtr }, _renderParamsPtr + 32, false);
			Marshal.StructureToPtr(new MpvRenderParam { Type = 20, Data = _bufferPtr }, _renderParamsPtr + 48, false);
			Marshal.StructureToPtr(new MpvRenderParam { Type = 0, Data = IntPtr.Zero }, _renderParamsPtr + 64, false);

			_updateCallback = (ctx) => _frameReady.Set();
			_updateCallbackHandle = GCHandle.Alloc(_updateCallback);
			mpv_render_context_set_update_callback(_mpvRenderCtx, _updateCallback, IntPtr.Zero);

			_eventThread = new Thread(EventLoop)
			{
				IsBackground = true,
				Name = "mpv-events"
			};

			_eventThread.Start();

			_closed = false;

			AepLog.Debug("[MPV] Video Player started");
		}

		public bool RenderFrame()
		{
			try
			{
				_frameReady.Wait();
				_frameReady.Reset();
			}
			catch
			{
				AepLog.Debug("[MPV] Video Player stopped");
				return false;
			}
			if (_closed || _cancelToken!.Token.IsCancellationRequested)
			{ AepLog.Debug("[MPV] Video Player stopped"); return false; }
			ulong flags = mpv_render_context_update(_mpvRenderCtx);
			if ((flags & 1) == 0)
			{
				return true;
			}

			try
			{
				int rc = mpv_render_context_render(_mpvRenderCtx, _renderParamsPtr);

				if (_closed || _cancelToken!.Token.IsCancellationRequested)
				{
					return false;
				}

				if (rc == 0 && _targetTexture != null)
				{
					IntPtr snapshot = _useSnapA ? _snapA : _snapB;
					_useSnapA = !_useSnapA;

					unsafe
					{
						System.Buffer.MemoryCopy((void*)_bufferPtr, (void*)snapshot, _frameBytes, _frameBytes);
					}

					lock (_snapshotLock)
					{
						_latestSnapshot = snapshot;
					}

					Texture2D texture = _targetTexture;
					int width = _width;
					DxHandler.RunOnRenderThread(RenderKey, () =>
					{
						DxHandler.Device?.ImmediateContext.UpdateSubresource(texture, 0, null, snapshot, width * 4, 0);
					});
					return true;
				}
				else
				{
					AepLog.Warning($"[MPV] Error rendering frame: RC: {rc} Texture: {_targetTexture}");
				}
			}
			catch (Exception e)
			{
				AepLog.Warning($"[MPV] Error rendering frame: {e.Message} {e.StackTrace}");
			}
			return false;
		}
		private readonly Lock _mpvLock = new();
		public void StopRender()
		{
			_closed = true;
			_cancelToken!.Cancel();
			DxHandler.CancelRenderThreadWork(RenderKey);
			lock (_snapshotLock)
			{
				_latestSnapshot = IntPtr.Zero;
			}

			Task.Run(() =>
			{
				lock (_mpvLock)
				{
					if (_mpvRenderCtx != IntPtr.Zero)
					{
						mpv_render_context_free(_mpvRenderCtx);
						_mpvRenderCtx = IntPtr.Zero;
					}
					if (_updateCallbackHandle.IsAllocated)
					{
						_updateCallbackHandle.Free();
					}

					if (_mpvCtx != IntPtr.Zero)
					{
						mpv_terminate_destroy(_mpvCtx);
						_mpvCtx = IntPtr.Zero;
					}

					if (_bufferPtr != IntPtr.Zero)
					{
						Marshal.FreeHGlobal(_bufferPtr);
						_bufferPtr = IntPtr.Zero;
					}
					if (_snapA != IntPtr.Zero)
					{
						Marshal.FreeHGlobal(_snapA);
						_snapA = IntPtr.Zero;
					}
					if (_snapB != IntPtr.Zero)
					{
						Marshal.FreeHGlobal(_snapB);
						_snapB = IntPtr.Zero;
					}

					Marshal.FreeHGlobal(_sizePtr);
					Marshal.FreeHGlobal(_stridePtr);
					Marshal.FreeHGlobal(_formatPtr);
					Marshal.FreeHGlobal(_renderParamsPtr);

					_targetTexture = null;
				}
			});

			_eventThread?.Join(2000);
		}

		public void Play(string url, double playbackPosition, bool isPlaying)
		{
			if (!_closed)
			{
				AepLog.Debug("Playing New Video at " + playbackPosition + " | " + isPlaying);
				lock (_mpvLock)
				{
					if(url == string.Empty)
					{
						Stop();
					}
					else
					{
						string startStr = ((int)playbackPosition).ToString(System.Globalization.CultureInfo.InvariantCulture);
						string pauseStr = !isPlaying ? ",pause=yes" : string.Empty;
						_ = mpv_command(_mpvCtx, ["loadfile", url, "replace", "0", $"start={startStr}{pauseStr}", null!]);	
					}
				}
			}
		}

		public void Stop()
		{
			if (!_closed)
			{
				lock (_mpvLock)
				{
					_ = mpv_command(_mpvCtx, ["stop", null!]);
					_closed = true;
					_frameReady?.Set();
				}
			}
		}

		public bool GetPaused()
		{
			if (_closed)
			{
				return true;
			}

			lock (_mpvLock)
			{
				if (_mpvCtx == IntPtr.Zero)
				{
					return true;
				}

				IntPtr ptr = Marshal.AllocHGlobal(4);
				try
				{
					_ = mpv_get_property(_mpvCtx, "pause", 3, ptr);
					return Marshal.ReadInt32(ptr) == 1;
				}
				finally
				{
					Marshal.FreeHGlobal(ptr);
				}
			}
		}
		// A managed copy of the latest decoded frame - for CPU-side consumers (the debug window
		// and the plain screen-window fallback) alongside the GPU texture upload RenderFrame
		// already does. Not on the hot path: only copied when actually asked for.
		public byte[]? TryGetFrame(out int width, out int height)
		{
			width = _width;
			height = _height;
			lock (_snapshotLock)
			{
				if (_latestSnapshot == IntPtr.Zero || _frameBytes == 0)
				{
					return null;
				}

				var frame = new byte[_frameBytes];
				Marshal.Copy(_latestSnapshot, frame, 0, _frameBytes);
				return frame;
			}
		}

		public double[] GetProperties()
		{
			if (_closed)
			{
				return [0, 0, 100];
			}

			lock (_mpvLock)
			{
				if (_mpvCtx == IntPtr.Zero)
				{
					return [0, 0, 100];
				}

				_ = mpv_get_property(_mpvCtx, "time-pos", 5, out double position);
				_ = mpv_get_property(_mpvCtx, "duration", 5, out double duration);
				_ = mpv_get_property(_mpvCtx, "volume", 5, out double volume);
				return [position, duration, volume];
			}
		}

		public void Pause(bool pause)
		{
			if (!_closed)
			{
				lock (_mpvLock)
				{
					_ = mpv_command(_mpvCtx, ["set", "pause", pause ? "yes" : "no", null!]);
				}
			}
		}
		
		public void SetVolume(int volume)
		{
			if (!_closed)
			{
				lock (_mpvLock)
				{
					_ = mpv_command(_mpvCtx, ["set", "volume", volume.ToString(System.Globalization.CultureInfo.InvariantCulture), null!]);
				}
			}
		}

		public void Seek(int seconds)
		{
			if (_closed)
			{
				AepLog.Debug($"[MPV] Seek to {seconds}s ignored: player closed");
				return;
			}

			lock (_mpvLock)
			{
				if (_mpvCtx == IntPtr.Zero)
				{
					AepLog.Debug($"[MPV] Seek to {seconds}s ignored: no mpv context");
					return;
				}

				int rc = mpv_command(_mpvCtx, ["seek", seconds.ToString(System.Globalization.CultureInfo.InvariantCulture), "absolute", null!]);
				if (rc < 0)
				{
					AepLog.Warning($"[MPV] Seek to {seconds}s failed: rc={rc}");
				}
			}
		}

		public string? GetMediaTitle()
		{
			if (_closed)
			{
				return null;
			}

			lock (_mpvLock)
			{
				if (_mpvCtx == IntPtr.Zero)
				{
					return null;
				}

				IntPtr ptr = mpv_get_property_string(_mpvCtx, "media-title");
				if (ptr != IntPtr.Zero)
				{
					try
					{
						return Marshal.PtrToStringUTF8(ptr);
					}
					finally
					{
						mpv_free(ptr);
					}
				}
				return null;
			}
		}

		public string? GetCurrentUrl()
		{
			if (_closed)
			{
				return null;
			}

			lock (_mpvLock)
			{
				if (_mpvCtx == IntPtr.Zero)
				{
					return null;
				}

				IntPtr ptr = mpv_get_property_string(_mpvCtx, "path");
				if (ptr == IntPtr.Zero)
				{
					return null;
				}

				try
				{
					return Marshal.PtrToStringAnsi(ptr);
				}
				finally
				{
					mpv_free(ptr);
				}
			}
		}

		public bool IsIdle()
		{
			if (_closed)
			{
				return true;
			}

			lock (_mpvLock)
			{
				if (_mpvCtx == IntPtr.Zero)
				{
					return true;
				}

				IntPtr ptr = Marshal.AllocHGlobal(4);
				try
				{
					int rc = mpv_get_property(_mpvCtx, "idle-active", 3, ptr);
					if (rc < 0)
					{
						return true;
					}

					return Marshal.ReadInt32(ptr) == 1;
				}
				finally
				{
					Marshal.FreeHGlobal(ptr);
				}
			}
		}

		public bool IsEofReached()
		{
			if (_closed)
			{
				return true;
			}

			lock (_mpvLock)
			{
				if (_mpvCtx == IntPtr.Zero)
				{
					return true;
				}

				IntPtr ptr = Marshal.AllocHGlobal(4);
				try
				{
					int rc = mpv_get_property(_mpvCtx, "eof-reached", 3, ptr);
					if (rc < 0)
					{
						return false;
					}

					return Marshal.ReadInt32(ptr) == 1;
				}
				finally
				{
					Marshal.FreeHGlobal(ptr);
				}
			}
		}
		
		public void Dispose()
		{
			StopRender();
			_frameReady.Dispose();
			GC.SuppressFinalize(this);
		}

		private void EventLoop()
		{
			
            AepLog.Verbose("[MPV] event loop started");
            try
            {
                while (!_closed)
                {
                    IntPtr ev = mpv_wait_event(_mpvCtx, 1);
                    if (ev == IntPtr.Zero) {continue;}

                    int eventId = Marshal.ReadInt32(ev);

                    
                    switch (eventId)
                    {
                        
                        case 0: // MPV_EVENT_NONE (Timeout)
                            continue;

                        case 1: // MPV_EVENT_SHUTDOWN
                            AepLog.Verbose("[MPV] SHUTDOWN");
                            return;

                        case 2: // MPV_EVENT_LOG_MESSAGE
                            {
                                IntPtr dataPtr2 = Marshal.ReadIntPtr(ev + 16);
                                if (dataPtr2 != IntPtr.Zero && dataPtr2.ToInt64() > 65536)
                                {
									string? prefix = Marshal.PtrToStringAnsi(Marshal.ReadIntPtr(dataPtr2));
									string? level  = Marshal.PtrToStringAnsi(Marshal.ReadIntPtr(dataPtr2 + 8));
									string? text   = Marshal.PtrToStringAnsi(Marshal.ReadIntPtr(dataPtr2 + 16));
									if(prefix != null && prefix.Contains("ytdl") && level == "error" && text != null && text.Contains("Unsupported URL"))
									{
										AepLog.Warning($"[MPV/{prefix}/{level}] {text?.Trim()}");
										Stop();
									}
                                    AepLog.Verbose($"[MPV/{prefix}/{level}] {text?.Trim()}");
                                }
                                break;
                            }

                        case 3:  AepLog.Verbose("[MPV] GET_PROPERTY_REPLY"); break;
                        case 4:  AepLog.Verbose("[MPV] SET_PROPERTY_REPLY"); break;
                        case 5:  AepLog.Verbose("[MPV] COMMAND_REPLY");      break;
                        case 6:  AepLog.Verbose("[MPV] START_FILE");         break;
                        
                        case 7: // MPV_EVENT_END_FILE
								break;
                        
                        case 8:  AepLog.Verbose("[MPV] FILE_LOADED");      break;
                        case 14: AepLog.Verbose("[MPV] CLIENT_MESSAGE");   break;
                        case 15: AepLog.Verbose("[MPV] VIDEO_RECONFIG");   break;
                        case 16: AepLog.Verbose("[MPV] AUDIO_RECONFIG");   break;
                        case 17: AepLog.Verbose("[MPV] SEEK");             break;
                        case 18: AepLog.Verbose("[MPV] PLAYBACK_RESTART"); break;
                        case 19: AepLog.Verbose("[MPV] PROPERTY_CHANGE");  break;
                        case 22: AepLog.Verbose("[MPV] HOOK");             break;

                        default:
                            break;
                    }
                }
            }
            catch (Exception e)
            {
                AepLog.Verbose($"[MPV] event loop crashed: {e.Message}\n{e.StackTrace}");
            }
            finally
            {
                AepLog.Verbose("[MPV] event loop ended");
            }
            
		}
	}
}
