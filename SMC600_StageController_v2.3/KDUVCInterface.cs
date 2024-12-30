using System;
using System.Drawing;
using System.Diagnostics;
using System.Windows.Forms;
using OpenCvSharp;
using OpenCvSharp.Extensions;

namespace SMC600_StageController_v2._0
{
    public class KDUVCInterface
    {
        public const int previewIntervalmsec = 30;
        public const int caputureIntervalmsec = 100;
        public const int UVCFrameWidth = 1920;
        public const int UVCFrameHeight = 1080;
        private VideoCapture _cameraActive;
        private Timer _timer;
        private int capturecount = 0;
        public Image capturedBitmap;

        private Action<Bitmap> _onFrameCaptured;

        public KDUVCInterface(Action<Bitmap> onFrameCaptured)
        {
            _onFrameCaptured = onFrameCaptured;
        }

        //카메라를 연결하는 함수
        private void InitializeCamera(int camindex)
        {
            try
            {
                _timer?.Stop();
                _cameraActive?.Dispose();
                _cameraActive = new VideoCapture(camindex, VideoCaptureAPIs.ANY);
                _cameraActive.Set(VideoCaptureProperties.FrameWidth, UVCFrameWidth);
                _cameraActive.Set(VideoCaptureProperties.FrameHeight, UVCFrameHeight);
                _timer = new Timer { Interval = previewIntervalmsec };
                _timer.Tick += Timer_Tick;
                _timer.Start();
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
                MessageBox.Show("카메라를 초기화할 수 없습니다: " + ex.Message);
            }
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            if (_cameraActive != null && _cameraActive.IsOpened())
            {
                // 화면업데이트는 빠르게 촬영은 0.1초 마다 좀더 느리게 
                using (Mat frame = new Mat()) // _cameraActive.QueryFrame())
                {
                    _cameraActive.Read(frame);
                    if (frame != null)
                    {
                        Bitmap bmp = frame.ToBitmap();
                        if (capturecount++ >= (caputureIntervalmsec / previewIntervalmsec))
                        {
                            capturedBitmap = bmp.Clone() as Image;// new Bitmap(bmp);
                            capturecount = 0;
                            // Debug.WriteLine("captured...");
                            GC.Collect();
                        }
                        _onFrameCaptured?.Invoke(bmp);
                    }
                }
            }
        }

        //카메라 화면 업데이트를 중지하는 함수
        public void StopCamera()
        {
            _timer?.Stop();
            _cameraActive?.Dispose();

        }

        //USB 혹은 현미경 카메라로 변경
        public void SwitchCamera(bool isusb)
        {
            InitializeCamera(isusb ? 0 : 1);
        }
    }
}
