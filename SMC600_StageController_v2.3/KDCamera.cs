using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Drawing;
using System.Drawing.Imaging;
using System.Diagnostics;
using OpenCvSharp;
using OpenCvSharp.Extensions;
using System.IO;
using System.Windows.Forms;
using System.Threading;
using static GitHub.secile.Video.DirectShow;
using ZedGraph;

namespace SMC600_StageController_v2._0
{
    public class KDCamera
    {
        public KDBasicSettingValues mybasicsetvalue;

        public KDCamera(KDBasicSettingValues mbasicsetvalues)
        {
            mybasicsetvalue = mbasicsetvalues;
        }



        //현재 화면에 비춰지는 이미지의 사진을 저장하는 기능(파라미터: 현재 화면 이미지 / 수동 촬영[0] or 자동촬영[1] )
        public static string savePhoto(Image mycaturenow, int check_ManualShooting_or_AutomaticShooting, string filepath)
        {
            string folderpath = Path.GetDirectoryName(filepath);

            
            //시료 전체를 저장하는 폴더 생성
            if (!Directory.Exists(folderpath))
            {
                Directory.CreateDirectory(folderpath);
                Console.WriteLine($"Directory created: {folderpath}");
            }

            else
            {
                //Console.WriteLine($"Directory already exists: {folderpath}");
            }
            
            using (Stream BitmapStream = File.Open(filepath, System.IO.FileMode.OpenOrCreate))
            {
                mycaturenow.Save(BitmapStream, ImageFormat.Png);

                string msgstr = filepath + "\r\n파일이 저장되어있습니다.";
                Debug.WriteLine(msgstr);
            }

            return filepath;
        }

        // 현재 이미지의 초점값을 double형으로 반환하는 함수 (초점값이 증가할수록 정확한 초점)
        public static double getSharpenessValue(Image orgimg)
        {
            double sharpnessValue;
            Mat sharpeningIMGtemp;
            Mat median = new Mat();
            Mat laplacian = new Mat();

            try
            {
                Bitmap temBitmap = new Bitmap(orgimg);
                sharpeningIMGtemp = temBitmap.ToMat();


                Cv2.MeanStdDev(sharpeningIMGtemp, out var mean, out var stddev);
                sharpnessValue = stddev.Val0 * stddev.Val0;
            }

            finally
            {
                median.Dispose();
                laplacian.Dispose();
            }


            return sharpnessValue;              //sharpnesvalue 도출된 초점값 반환
        }

        // 박편의 위치를 파악하기 위해 흑백화, 반전 등 사진의 효과를 넣는 함수
        public List<Rectangle> changethepictogray(Image mogimage, Rect cropRect)
        {
            Debug.WriteLine("이미지 초점 맞추기 시작,,,,,,,,,,,,,,,,,,,,,");
            Mat testImg = new Mat();
            Mat sbuimg = new Mat();

            //  Rect cropRect = new Rect(int.Parse(mybasicsetvalue.myXcropimgSize.ToString()), int.Parse(mybasicsetvalue.myYcropimgSize.ToString()), int.Parse(mybasicsetvalue.myWidthcropimgSize.ToString()), int.Parse(mybasicsetvalue.myHeightcropimgSize.ToString()));

            Bitmap temBitmap = new Bitmap(mogimage);
            Mat mogimage1 = temBitmap.ToMat();

            Mat hsvImage = new Mat();
            Cv2.CvtColor(mogimage1, hsvImage, ColorConversionCodes.BGR2HSV);

            // 붉은색 범위 정의 (HSV에서 붉은색은 두 개의 범위로 나뉨)
            Scalar lowerRed1 = new Scalar(0, 65, 65);
            Scalar upperRed1 = new Scalar(10, 255, 255);
            Scalar lowerRed2 = new Scalar(160, 65, 65);
            Scalar upperRed2 = new Scalar(180, 255, 255);

            // 붉은색 마스크 생성
            Mat mask1 = new Mat();
            Cv2.InRange(hsvImage, lowerRed1, upperRed1, mask1);
            Mat mask2 = new Mat();
            Cv2.InRange(hsvImage, lowerRed2, upperRed2, mask2);

            // 두 마스크를 합침
            Mat redMask = new Mat();
            Cv2.BitwiseOr(mask1, mask2, redMask);

            // 노이즈 제거를 위한 형태학적 연산 적용 (열기 및 닫기)
            Mat kernel = Cv2.GetStructuringElement(MorphShapes.Rect, new OpenCvSharp.Size(5, 5));
            Cv2.MorphologyEx(redMask, redMask, MorphTypes.Close, kernel);
            Cv2.MorphologyEx(redMask, redMask, MorphTypes.Open, kernel);

            // 히스토그램 계산을 위한 설정
            int hBins = 180; // Hue 채널의 히스토그램 빈
            int sBins = 256; // Saturation 채널의 히스토그램 빈
            int[] histSize = { hBins, sBins };
            Rangef[] ranges = { new Rangef(0, 180), new Rangef(0, 256) };
            int[] channels = { 0, 1 }; // Hue와 Saturation 채널 사용

            // 붉은색 영역의 히스토그램 계산
            Mat hist = new Mat();
            Cv2.CalcHist(
                new Mat[] { hsvImage },
                channels,
                redMask,
                hist,
                2,
                histSize,
                ranges,
                accumulate: false
            );

            // 히스토그램 정규화
            Cv2.Normalize(hist, hist, 0, 255, NormTypes.MinMax);

            // 히스토그램 역투영 수행
            Mat backProj = new Mat();
            Cv2.CalcBackProject(
                new Mat[] { hsvImage },
                channels,
                hist,
                backProj,
                ranges
                //scale: 1
            );

            // 역투영 결과를 부드럽게 하기 위해 필터 적용
            Mat discKernel = Cv2.GetStructuringElement(MorphShapes.Ellipse, new OpenCvSharp.Size(5, 5));
            Cv2.Filter2D(backProj, backProj, -1, discKernel);



            /*


            // HSV 채널 분리
            Mat[] hsvChannels = Cv2.Split(hsvImage);

            // 채도 채널(Saturation)을 조정 (예: 1.5배 증가)
            double scale = 10;
            hsvChannels[1] = hsvChannels[1] * scale;

            // 채널 병합
            Cv2.Merge(hsvChannels, hsvImage);

            // HSV를 다시 BGR로 변환
            Cv2.CvtColor(hsvImage, testImg, ColorConversionCodes.HSV2BGR);

            */

            //Cv2.CvtColor(testImg, testImg, ColorConversionCodes.BGR2GRAY);                        //흑백화

            testImg = backProj.SubMat(cropRect);

            //Cv2.GaussianBlur(testImg, testImg, new OpenCvSharp.Size(9, 9), 50);                          //블러 넣기
            Cv2.Threshold(testImg, testImg, 0, 255, ThresholdTypes.Binary | ThresholdTypes.Otsu);                               // 이진화
            //Cv2.BitwiseNot(testImg, testImg);

            OpenCvSharp.Point[][] contours;
            HierarchyIndex[] hierarchy;

            Cv2.FindContours(testImg, out contours, out hierarchy, RetrievalModes.External, ContourApproximationModes.ApproxSimple);                //1차 모서리 찾기

            Mat testimg2 = new Mat();

            testImg.CopyTo(testimg2);

            Cv2.FindContours(testImg, out contours, out hierarchy, RetrievalModes.External, ContourApproximationModes.ApproxSimple);                //2차 모서리 찾기

            List<Rectangle> myrectg = new List<Rectangle>();

            for (int i = 0; i < contours.Length; i++)                                                                                               //찾은 모서리를 기반으로 사각형 도출
            {
                Rect rect = Cv2.BoundingRect(contours[i]);

                //Rectangle rcg = new Rectangle(rect.X + cropRect.X, rect.Y + cropRect.Y, rect.Width, rect.Height);

                int newWidth = (int)(rect.Width * mybasicsetvalue.myobjRectangle_size);
                int newHeight = (int)(rect.Height * mybasicsetvalue.myobjRectangle_size);

                int centerX = rect.X + rect.Width / 2;
                int centerY = rect.Y + rect.Height / 2;

                int newX = centerX - (newWidth / 2);
                int newY = centerY - (newHeight / 2);

                newX += cropRect.X;
                newY += cropRect.Y;

                Rectangle rcg = new Rectangle(newX, newY, newWidth, newHeight);

                if (rect.Width * rect.Height >= 500 && rect.Width >= 40 && rect.Height >= 40 && rect.Width <= 220 && rect.Height <= 220 &&
                                        rect.X > 0 && rect.Y > 0 &&  // 가장자리 제외
                    rect.X + rect.Width < testImg.Width &&
                    rect.Y + rect.Height < testImg.Height)
                {
                    myrectg.Add(rcg);
                }
            }

            return myrectg;                 //해당 사각형 값 반환
        }
        /*
        public Rectangle rectangle_scale_to_custom()
        {
            Rect rect = Cv2.BoundingRect(contours[i]);

            int newWidth = (int)(rect.Width * scale);
            int newHeight = (int)(rect.Height * scale);

            int centerX = rect.X + rect.Width / 2;
            int centerY = rect.Y + rect.Height / 2;

            int newX = centerX - (newWidth / 2);
            int newY = centerY - (newHeight / 2);

            newX += cropRect.X;
            newY += cropRect.Y;

            Rectangle rcg = new Rectangle(newX, newY, newWidth, newHeight);

            return new Rectangle()
        }

        */

        // 탐지된 박편의 위치 정보를 사각형 객체로 반환하는 함수
        public static Rectangle imageRectToscreen(Rectangle mrc, double screenwidth, double screenheight, double imgwidth, double imgheight)
        {
            double Xweight = screenwidth / imgwidth;
            double Yweight = screenheight / imgheight;

            return new Rectangle((int)(mrc.X * Xweight), (int)(mrc.Y * Yweight), (int)(mrc.Width * Xweight), (int)(mrc.Height * Yweight));            //박편의 위치 정보를 사각형 객체로 반환
        }
    }
}
