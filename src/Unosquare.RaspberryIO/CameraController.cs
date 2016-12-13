﻿namespace Unosquare.RaspberryIO
{
    using System;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// The Raspberry Pi's camera controller wrapping raspistill and raspivid programs.
    /// This class is a singleton
    /// </summary>
    public class CameraController
    {
        #region Private Declarations

        private static CameraController m_Instance = null;
        private static readonly ManualResetEventSlim OperationDone = new ManualResetEventSlim(true);
        private static readonly object SyncLock = new object();
        private static readonly CancellationTokenSource VideoCts = new CancellationTokenSource();

        #endregion

        #region Properties

        /// <summary>
        /// Gets the instance of the Pi's camera controller.
        /// </summary>
        internal static CameraController Instance
        {
            get
            {
                lock (SyncLock)
                {
                    return m_Instance ?? (m_Instance = new CameraController());
                }
            }
        }

        /// <summary>
        /// Gets a value indicating whether the camera module is busy.
        /// </summary>
        /// <value>
        ///   <c>true</c> if this instance is busy; otherwise, <c>false</c>.
        /// </value>
        public bool IsBusy => OperationDone.IsSet == false;

        #endregion
        
        #region Image Capture Methods

        /// <summary>
        /// Captures an image asynchronously.
        /// </summary>
        /// <param name="settings">The settings.</param>
        /// <param name="ct">The ct.</param>
        /// <returns></returns>
        /// <exception cref="System.InvalidOperationException">Cannot use camera module because it is currently busy.</exception>
        public async Task<byte[]> CaptureImageAsync(CameraStillSettings settings, CancellationToken ct)
        {
            if (Instance.IsBusy)
                throw new InvalidOperationException("Cannot use camera module because it is currently busy.");

            if (settings.CaptureTimeoutMilliseconds <= 0)
                throw new ArgumentException($"{nameof(settings.CaptureTimeoutMilliseconds)} needs to be greater than 0");

            try
            {
                OperationDone.Reset();

                var ms = new MemoryStream();
                var exitCode =
                    await ProcessHelper.RunProcessAsync(settings.CommandName, settings.CreateProcessArguments(),
                        (data, proc) =>
                        {
                            ms.Write(data, 0, data.Length);
                        }, null, true, ct);

                return exitCode != 0 ? new byte[] {} : ms.ToArray();
            }
            catch (Exception ex)
            {
                throw ex;
            }
            finally
            {
                OperationDone.Set();
            }
        }

        /// <summary>
        /// Captures an image asynchronously.
        /// </summary>
        /// <param name="settings">The settings.</param>
        /// <returns></returns>
        public async Task<byte[]> CaptureImageAsync(CameraStillSettings settings)
        {
            var cts = new CancellationTokenSource();
            return await CaptureImageAsync(settings, cts.Token);
        }

        /// <summary>
        /// Captures an image.
        /// </summary>
        /// <param name="settings">The settings.</param>
        /// <returns></returns>
        public byte[] CaptureImage(CameraStillSettings settings)
        {
            return CaptureImageAsync(settings).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Captures a JPEG encoded image asynchronously at 90% quality.
        /// </summary>
        /// <param name="width">The width.</param>
        /// <param name="height">The height.</param>
        /// <param name="ct">The ct.</param>
        /// <returns></returns>
        public async Task<byte[]> CaptureImageJpegAsync(int width, int height, CancellationToken ct)
        {
            var settings = new CameraStillSettings()
            {
                CaptureWidth = width,
                CaptureHeight = height,
                CaptureJpegQuality = 90,
                CaptureTimeoutMilliseconds = 300,
            };

            return await CaptureImageAsync(settings, ct);
        }

        /// <summary>
        /// Captures a JPEG encoded image at 90% quality.
        /// </summary>
        /// <param name="width">The width.</param>
        /// <param name="height">The height.</param>
        /// <returns></returns>
        public byte[] CaptureImageJpeg(int width, int height)
        {
            var cts = new CancellationTokenSource();
            return CaptureImageJpegAsync(width, height, cts.Token).GetAwaiter().GetResult();
        }

        #endregion

        #region Video Capture Methods

        /// <summary>
        /// Performs a continuous read of the standard output and fires the corresponding events.
        /// </summary>
        /// <param name="settings">The settings.</param>
        /// <param name="onDataCallback">The on data callback.</param>
        /// <param name="onExitCallback">The on exit callback.</param>
        private static async Task VideoWorkerDoWork(CameraVideoSettings settings, Action<byte[]> onDataCallback,
            Action onExitCallback)
        {
            try
            {
                await ProcessHelper.RunProcessAsync(settings.CommandName, settings.CreateProcessArguments(),
                    (data, proc) =>
                    {
                        onDataCallback?.Invoke(data);
                    }, null, true, VideoCts.Token);

                onExitCallback?.Invoke();
            }
            catch
            {
                // swallow
            }
            finally
            {
                Instance.CloseVideoStream();
                OperationDone.Set();
            }
        }

        /// <summary>
        /// Opens the video stream with a timeout of 0 (running indefinitely) at 1080p resolution, variable bitrate and 25 FPS.
        /// No preview is shown
        /// </summary>
        /// <param name="onDataCallback">The on data callback.</param>
        public void OpenVideoStream(Action<byte[]> onDataCallback)
        {
            OpenVideoStream(onDataCallback, null);
        }

        /// <summary>
        /// Opens the video stream with a timeout of 0 (running indefinitely) at 1080p resolution, variable bitrate and 25 FPS.
        /// No preview is shown
        /// </summary>
        /// <param name="onDataCallback">The on data callback.</param>
        /// <param name="onExitCallback">The on exit callback.</param>
        public void OpenVideoStream(Action<byte[]> onDataCallback, Action onExitCallback)
        {
            var settings = new CameraVideoSettings()
            {
                CaptureTimeoutMilliseconds = 0,
                CaptureDisplayPreview = false,
                CaptureWidth = 1920,
                CaptureHeight = 1080
            };

            OpenVideoStream(settings, onDataCallback, onExitCallback);
        }

        /// <summary>
        /// Opens the video stream with the supplied settings. Capture Timeout Milliseconds has to be 0 or greater
        /// </summary>
        /// <param name="settings">The settings.</param>
        /// <param name="onDataCallback">The on data callback.</param>
        /// <param name="onExitCallback">The on exit callback.</param>
        /// <exception cref="System.InvalidOperationException">Cannot use camera module because it is currently busy.</exception>
        /// <exception cref="System.ArgumentException">CaptureTimeoutMilliseconds</exception>
        public void OpenVideoStream(CameraVideoSettings settings, Action<byte[]> onDataCallback, Action onExitCallback)
        {
            if (Instance.IsBusy)
                throw new InvalidOperationException("Cannot use camera module because it is currently busy.");

            if (settings.CaptureTimeoutMilliseconds < 0)
                throw new ArgumentException($"{nameof(settings.CaptureTimeoutMilliseconds)} needs to be greater than or equal to 0");

            try
            {
                OperationDone.Reset();
                Task.Factory.StartNew(() => VideoWorkerDoWork(settings, onDataCallback, onExitCallback));
            }
            catch (Exception ex)
            {
                OperationDone.Set();
                throw ex;
            }
        }

        /// <summary>
        /// Closes the video stream of a video stream is open.
        /// </summary>
        public void CloseVideoStream()
        {
            lock (SyncLock)
            {
                if (IsBusy == false)
                    return;

                if (VideoCts.IsCancellationRequested == false)
                    VideoCts.Cancel();
            }

        }

        #endregion
    }
}