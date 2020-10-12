using CliWrap;
using Clypto.Shared;
using Serilog;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Clypto.Server.Services
{
	public enum NormalizerResult
	{
		Skipped,
		Error,
		Updated
	}

	public class AudioLoudnessNormalizer
	{
		private static readonly SemaphoreSlim NormalizerSemaphore = new SemaphoreSlim(1, 1);

		public async Task<NormalizerResult> NormalizeFileLoudness(string inputPath, IProgress<double> progress = null, CancellationToken cancellationToken = default(CancellationToken))
		{
			await NormalizerSemaphore.WaitAsync(cancellationToken);
			try
			{
				var ffmpegPath = PathUtilities.GetFullPath("ffmpeg.exe");
				if (!File.Exists(ffmpegPath))
				{
					ffmpegPath = PathUtilities.GetFullPath("ffmpeg");
				}
				if (!File.Exists(ffmpegPath))
				{
					throw new ArgumentNullException("ffmpeg not found");
				}

				// Copy the original elsewhere
				// If we already have an .original, it's already been normalized. Just skip it
				if (File.Exists(inputPath))
				{
					File.Delete($"{inputPath}.original");
					File.Copy(inputPath, $"{inputPath}.original");
				}
				else
				{
					return NormalizerResult.Error;
				}

				var args = new List<string>()
				{
					"-y",
					$"-i {inputPath}.original",
					"-filter:a loudnorm",
					$"{inputPath}"
				};

				var progressRouter = new FfmpegProgressRouter(progress);

				Log.Information("{clip} Beginning loudness normalization", inputPath);

				var cli = await Cli.Wrap(ffmpegPath)
					.WithWorkingDirectory(Directory.GetCurrentDirectory())
					.WithArguments(string.Join(" ", args))
					.WithStandardErrorPipe(PipeTarget.ToDelegate((line) => progressRouter.ProcessLine(line))) // handle stderr to parse and route progress
					.WithValidation(CommandResultValidation.None) // disable stderr validation because ffmpeg writes progress there
					.ExecuteAsync(cancellationToken);

				File.Delete($"{inputPath}.original");

				Log.Information("{clip} Completed loudness normalization", inputPath);
				return NormalizerResult.Updated;
			}
			catch (Exception ex)
			{
				Log.Information("{clip} loudness normalization ERROR", inputPath);
				Log.Information("Loudness Normalizer Error Message: {0}", ex.Message);
				// Replace the base file with the .original version
				File.Delete(inputPath);
				File.Copy($"{inputPath}.original", inputPath);
				return NormalizerResult.Error;
			}
			finally
			{
				NormalizerSemaphore.Release();
			}
		}

		private class FfmpegProgressRouter
		{
			private readonly IProgress<double> _output;

			private TimeSpan _totalDuration = TimeSpan.Zero;

			public FfmpegProgressRouter(IProgress<double> output)
			{
				_output = output;
			}

			public void ProcessLine(string line)
			{
				// Parse total duration if it's not known yet
				if (_totalDuration == TimeSpan.Zero)
				{
					var totalDurationRaw = Regex.Match(line, @"Duration:\s(\d\d:\d\d:\d\d.\d\d)").Groups[1].Value;
					if (totalDurationRaw.IsNotBlank())
						_totalDuration = TimeSpan.ParseExact(totalDurationRaw, "c", CultureInfo.InvariantCulture);
				}
				// Parse current duration and report progress if total duration is known
				else
				{
					var currentDurationRaw = Regex.Match(line, @"time=(\d\d:\d\d:\d\d.\d\d)").Groups[1].Value;
					if (currentDurationRaw.IsNotBlank())
					{
						var currentDuration =
							TimeSpan.ParseExact(currentDurationRaw, "c", CultureInfo.InvariantCulture);

						// Report progress
						_output?.Report(currentDuration.TotalMilliseconds / _totalDuration.TotalMilliseconds);
					}
				}
			}


		}
	}

	public static class StringExtensions
	{
		public static bool IsNotBlank(this string str)
		{
			return !string.IsNullOrWhiteSpace(str);
		}

		public static bool IsBlank(this string str)
		{
			return string.IsNullOrWhiteSpace(str);
		}
	}
}
