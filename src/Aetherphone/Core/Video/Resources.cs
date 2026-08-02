using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using Newtonsoft.Json;
using Dalamud.Utility;
using SharpCompress.Archives;
using SharpCompress.Common;
using Newtonsoft.Json.Linq;

namespace Aetherphone.Core.Video;

internal sealed class Resources : IDisposable
{
	private readonly HttpClient _httpClient;
	private readonly string _configDir;

	internal string[] MpvCheckResult { get; private set; } = [string.Empty, string.Empty];
	internal string[] YtdlpCheckResult { get; private set; } = [string.Empty, string.Empty];
	private long _ntpTimeOffset;
	private long _sysTimeOffset;

	internal long CurrentTimeNTPNormalizedMilliseconds => _ntpTimeOffset > 0 ? _ntpTimeOffset + (DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - _sysTimeOffset) : DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

	internal string RomsDirectory => Path.Combine(_configDir, "roms");


	internal Resources()
	{
		_httpClient = new HttpClient();
		_httpClient.DefaultRequestHeaders.Add("User-Agent", "AetherphoneAetherStreamUpdater/1.0");
		_configDir = Plugin.PluginInterface.ConfigDirectory.FullName;

		Initialize();
	}

	public void Dispose()
	{
		_httpClient.Dispose();
		GC.SuppressFinalize(this);
	}

	private void Initialize()
	{
		if(!Directory.Exists(Path.Combine(_configDir, "roms")))
		{
			Directory.CreateDirectory(Path.Combine(_configDir, "roms"));
		}
		_=GetNtpUtcAsync().ContinueWith(task =>
		{
			//Set NTP time
			if (task.IsCompletedSuccessfully)
			{
				_ntpTimeOffset = task.GetResultSafely();
				AepLog.Debug("Received NTP Time Offset: " + (_ntpTimeOffset - DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()) + " ms.");
			}
			_sysTimeOffset = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
		}).ContinueWith(_ =>
		{
			//Check for MPV Updates, then auto-download in the background if one was found - a
			//tester never has to visit Settings at all for mpv to become ready. The Settings
			//page's own button (see AetherStreamApp.Settings.cs) stays as a manual fallback for
			//when this attempt hits a network hiccup at plugin load.
			CheckMPVAsync().ContinueWith(task =>
			{
				if (!task.IsCompletedSuccessfully)
				{
					AepLog.Error("Failed to check for MPV updates: " + task.Exception?.ToString());
					return;
				}

				if (MpvCheckResult[0].Length > 0)
				{
					_ = DownloadMPVAsync();
				}
			});
		}).ContinueWith(_=>
		{
			//Check for YTDLP Updates - same auto-download reasoning as the MPV check above.
			CheckYTDLPAsync().ContinueWith(task =>
			{
				if (!task.IsCompletedSuccessfully)
				{
					AepLog.Error("Failed to check for YTDLP updates: " + task.Exception?.ToString());
					return;
				}

				if (YtdlpCheckResult[0].Length > 0)
				{
					_ = DownloadYTDLPAsync();
				}
			});
		});
	}

	internal string? GetLocationMPV()
	{
		string filenameStart = "mpv-dev-lgpl-x86_64-";
		string? dir = Directory.GetDirectories(_configDir, $"{filenameStart}*").FirstOrDefault();
		if (dir != null)
		{
			return dir + "/libmpv-2.dll";
		}
		else
		{
			return null;
		}
	}

	internal string? GetLocationYTDLP()
	{
		string filenameStart = "yt-dlp";
		string? dir = Directory.GetDirectories(_configDir, $"{filenameStart}*").FirstOrDefault();
		if (dir != null)
		{
			return dir + "/yt-dlp.exe";
		}
		else
		{
			return null;
		}
	}

	internal string? GetLocationSNES9X()
	{
		string directoryName = "snes9x";
		string? dir = Directory.GetDirectories(_configDir, $"{directoryName}*").FirstOrDefault();
		if (dir != null)
		{
			string file = Path.Combine(_configDir, directoryName, "snes9x_libretro.dll");
			if(File.Exists(file))
			{
				return file;
			}
		}
		else
		{
			Directory.CreateDirectory(Path.Combine(_configDir, "snes9x"));
		}
		
		return null;
	}

	internal async Task CheckMPVAsync()
	{
		string filenameStart = "mpv-dev-lgpl-x86_64-";
		string filenameEnd = ".7z";
		string url = "https://api.github.com/repos/zhongfly/mpv-winbuild/releases/latest";
		MpvCheckResult = await CheckForUpdateAsync(_configDir, filenameStart, filenameEnd, url);
	}
	internal async Task CheckYTDLPAsync()
	{
		string filenameStart = "yt-dlp.exe";
		string filenameEnd = ".exe";
		string url = "https://api.github.com/repos/yt-dlp/yt-dlp/releases/latest";
		YtdlpCheckResult = await CheckForUpdateAsync(_configDir, filenameStart, filenameEnd, url);
	}
	// downloadURL is empty either because CheckForUpdateAsync already found the local folder up
	// to date, or because the check itself failed (rate limit, no network yet at plugin load) and
	// fell back to its empty-result default - either way there is nothing to fetch, and calling
	// HttpClient.GetAsync with an empty URI throws. Callers (AetherStreamApp.Settings) already
	// re-run the check first when this is empty, but this guard stays as the actual line that
	// can never hand HttpClient an invalid request.
	internal async Task<bool> DownloadMPVAsync()
	{
		string filenameStart = "mpv-dev-lgpl-x86_64-";
		string filenameEnd = ".7z";
		string downloadURL = MpvCheckResult[0];
		string folderName = MpvCheckResult[1];
		if (downloadURL.Length == 0)
		{
			return false;
		}

		return await UpdateAsync(_configDir, filenameStart, filenameEnd, downloadURL, folderName);
	}
	internal async Task<bool> DownloadYTDLPAsync()
	{
		string filenameStart = "yt-dlp";
		string filenameEnd = ".exe";
		string downloadURL = YtdlpCheckResult[0];
		string folderName = YtdlpCheckResult[1];
		if (downloadURL.Length == 0)
		{
			return false;
		}

		return await UpdateAsync(_configDir, filenameStart, filenameEnd, downloadURL, folderName);
	}
	private async Task<string[]> CheckForUpdateAsync(string configDir, string nameStartsWith, string nameEndsWith, string checkURL)
	{
		try{
			string json = await _httpClient.GetStringAsync(checkURL);
			var doc = JObject.Parse(json);
			long remoteId = doc["id"]!.Value<long>();
			var asset = doc["assets"]!
				.First(a => a["name"]!.Value<string>()!
					.StartsWith(nameStartsWith, StringComparison.Ordinal) &&
					a["name"]!.Value<string>()!.EndsWith(nameEndsWith, StringComparison.Ordinal));

			string assetName = asset["name"]!.Value<string>()!;
			string folderName = assetName.Replace(nameEndsWith, "") + "_" + remoteId;

			string localFolder = Path.Combine(configDir, folderName);

			if (Directory.Exists(localFolder))
			{
				return [string.Empty, folderName]; //Already up to date
			}

			string downloadURL = asset["browser_download_url"]!.Value<string>()!;
			AepLog.Warning("Found Update: " + downloadURL);
			return [downloadURL, folderName];
		}
		catch (Exception exception)
		{
			AepLog.Warning("Failed to check for update (" + checkURL + "): " + exception);
			return [string.Empty, string.Empty];
		}
	}

	private async Task<bool> UpdateAsync(string configDir, string nameStartsWith, string nameEndsWith, string downloadURL, string folderName)
	{
		try
		{
			AepLog.Debug("Downloading Update: " + downloadURL);
			string tempFile = Path.GetTempFileName() + nameEndsWith;
			var response = await _httpClient.GetAsync(downloadURL, HttpCompletionOption.ResponseHeadersRead);
			await using (var fs = File.OpenWrite(tempFile))
			{
				await response.Content.CopyToAsync(fs);
			}
			AepLog.Debug("Finished Downloading " + downloadURL);
			if (nameEndsWith == ".7z")
			{
				string localFolder = Path.Combine(configDir, Path.GetRandomFileName());
				Directory.CreateDirectory(localFolder);
				using (var archive = ArchiveFactory.OpenArchive(tempFile))
				{
					foreach (var entry in archive.Entries.Where(e => !e.IsDirectory))
					{
						entry.WriteToDirectory(localFolder, new ExtractionOptions
						{
							ExtractFullPath = true,
							Overwrite = true
						});
					}
				}

				File.Delete(tempFile);

				foreach (string dir in Directory.GetDirectories(configDir, $"{nameStartsWith}*"))
				{
					Directory.Delete(dir, recursive: true);
				}

				if (Directory.Exists(Path.Combine(configDir, folderName))) //Super weird but lets just do this to be safe
				{
					foreach (string file in Directory.GetFiles(localFolder, "*", SearchOption.AllDirectories))
					{
						string relative = Path.GetRelativePath(localFolder, file);
						string target = Path.Combine(Path.Combine(configDir, folderName), relative);
						Directory.CreateDirectory(Path.GetDirectoryName(target)!);
						File.Copy(file, target, overwrite: true);
					}
				}
				else
				{
					Directory.Move(localFolder, Path.Combine(configDir, folderName));
				}
			}
			else
			{
				foreach (string dir in Directory.GetDirectories(configDir, $"{nameStartsWith}*"))
				{
					Directory.Delete(dir, recursive: true);
				}

				string localFolder = Path.Combine(configDir, folderName);
				Directory.CreateDirectory(localFolder);

				string targetPath = Path.Combine(localFolder, nameStartsWith.EndsWith(nameEndsWith, StringComparison.Ordinal) ? nameStartsWith : nameStartsWith + nameEndsWith);
				File.Copy(tempFile, targetPath, overwrite: true);
				File.Delete(tempFile);
			}
			return true;
		}
		catch (Exception e)
		{
			AepLog.Error($"Error updating {nameStartsWith}: {e.Message} {e.StackTrace}");
			return false;
		}
	}

	internal async Task<bool> DownloadSNES9XAsync()
	{
		try
		{
			string directoryName = "snes9x";
			string temp = Path.GetTempFileName() + ".zip";
			var response = await _httpClient.GetAsync("https://buildbot.libretro.com/nightly/windows/x86_64/latest/snes9x_libretro.dll.zip", HttpCompletionOption.ResponseHeadersRead);
			await using (var fs = File.OpenWrite(temp))
			{
				await response.Content.CopyToAsync(fs);
			}

			string localFolder = Path.Combine(_configDir, directoryName);
			Directory.CreateDirectory(localFolder);
			using (var archive = ArchiveFactory.OpenArchive(temp))
			{
				foreach (var entry in archive.Entries.Where(e => !e.IsDirectory))
				{
					entry.WriteToDirectory(localFolder, new ExtractionOptions
					{
						ExtractFullPath = true,
						Overwrite = true
					});
				}
			}

			File.Delete(temp);
			return true;
		}
		catch
		{
			return false;
		}
	}

	private async Task<long> GetNtpUtcAsync(string server = "pool.ntp.org")
	{
		try
		{
			byte[] ntpData = new byte[48];
			ntpData[0] = 0x1B;

			var addresses = await Dns.GetHostAddressesAsync(server);
			var ep = new IPEndPoint(addresses[0], 123);

			using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
			socket.ReceiveTimeout = 3000;
			await socket.ConnectAsync(ep);
			await socket.SendAsync(ntpData);
			await socket.ReceiveAsync(ntpData);

			ulong intPart = ((ulong)ntpData[40] << 24) | ((ulong)ntpData[41] << 16) | ((ulong)ntpData[42] << 8) | ntpData[43];
			ulong fracPart = ((ulong)ntpData[44] << 24) | ((ulong)ntpData[45] << 16) | ((ulong)ntpData[46] << 8) | ntpData[47];
			ulong ms = intPart * 1000 + fracPart * 1000 / 0x100000000L;
			var dto = new DateTimeOffset(1900, 1, 1, 0, 0, 0, TimeSpan.Zero).AddMilliseconds((long)ms);
        	return dto.ToUnixTimeMilliseconds();
		}
		catch
		{
			return 0;
		}
	}

	internal static class NativeLoader
	{
		private static Resources? _resources;
		private static bool _registered;

		internal static void Register(Resources resources)
		{
			_resources = resources;
			if (_registered)
			{
				return;
			}

			_registered = true;
			NativeLibrary.SetDllImportResolver(typeof(NativeLoader).Assembly, Resolve);
		}

		private static IntPtr Resolve(string name, System.Reflection.Assembly assembly, DllImportSearchPath? path)
		{
			switch (name)
			{
				case "libmpv-2":
					// Queried fresh rather than cached at startup - mpv-winbuild may still be
					// downloading (see CheckMPVAsync/DownloadMPVAsync) the first time this
					// resolves.
					return TryLoad(_resources?.GetLocationMPV(), "MPV");
				default:
					return IntPtr.Zero;
			}
		}

		private static IntPtr TryLoad(string? location, string tag)
		{
			if (location != null && NativeLibrary.TryLoad(location, out nint handle))
			{
				return handle;
			}
			AepLog.Error($"[{tag}] Failed to load native lib from: {location}");
			return IntPtr.Zero;
		}
	}
}
