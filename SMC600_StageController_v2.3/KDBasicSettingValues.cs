using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Diagnostics;
using System.Text.Json;
using OpenCvSharp;


namespace SMC600_StageController_v2._0
{
    public class KDBasicSettingValues
    {
        public double usbCAMset_pixcelValue { get; set; }                                   //USB 카메라의 픽셀당 거리값
        public double microscopeCAMset_pixcelValue { get; set; }                        //현미경 카메라의 픽셀당 거리값
        public double bWidth { get; set; }                                                          //사용자 임의 측정 너비 값 
        public double bHeight { get; set; }                                                        //사용자 임의 측정 높이 값 
        public double myoverlay { get; set; }                                                     //사용자 지정 오버레이값
        public double myZaxisCaptureRange { get; set; }                                    //자동 촬영 시 Z축 이동 거리 값
        public double myZaxisCaptureInterval { get; set; }                                   //자동 촬영 시 Z축 촬영 횟수 값 
        public double myCamCenterXPos_mm { get; set; }                                 //현미경 카메라의 X축 중심값 
        public double myCamCenterYPos_mm { get; set; }                                   //현미경 카메라의 Y축 중심값 
        public double myCamFocusZPos_mm { get; set; }                                   //현미경 카메라의 Z축 중심값 
        public string myPlantSpecimenName { get; set; }                                   //저장할 파일의 이름(현재 박편의 이름) 

        public string myfolderpath { get; set; }

        public double myXcropimgSize { get; set; }
        public double myYcropimgSize { get; set; }
        public double myWidthcropimgSize { get; set; }
        public double myHeightcropimgSize { get; set; }

        public double mycameraWidth = 0;
        public double mycameraHeight = 0;
        public double myMicrocamResolution { get; set; }
        public double myMicrocam_magni_mm { get; set; }

        public double myobjRectangle_size { get; set; }

        public double myRectangleScale { get; set; }

        public int mydelaytime { get; set; }

        public double gainf;
        public double offsetf;

        //기본 설정 값을 JSON으로 수신
        public static KDBasicSettingValues Loadbyjson()
        {
            string jsonstringformy = Properties.Settings.Default.basicinfo;

            if (jsonstringformy.Length > 10)                //받아오는 JSON의 값이 있는 경우
            {
                Debug.WriteLine(jsonstringformy);
                return System.Text.Json.JsonSerializer.Deserialize<KDBasicSettingValues>(jsonstringformy);      //해당 값을 현재의 객체에 전달

            }
            else
            {
                return new KDBasicSettingValues();

            }
        }
        public KDBasicSettingValues()
        {
            computegainoffset(7, 112, 0.0018229, 0.0001666);
        }

        public void computegainoffset(double x_min, double x_max, double y_min, double y_max)
        {

            double yb = y_max - y_min;
            double xb = x_max - x_min;
            if (xb != 0 && yb != 0)
            {
                gainf = yb / xb;
                offsetf = y_min - x_min * gainf;

                Debug.WriteLine("기울기: " + gainf + "              ///         " + offsetf);
            }
        }

        //기본설정에 기입된 값을 기반으로 스테이지가 탐색해야 하는 좌표 리스트를 생성하는 함수
        public List<AxisINFO> getXYZcoorList(System.Drawing.Rectangle objREC, double zpos, string mypath)
        {
            double realWidth = (objREC.Width * usbCAMset_pixcelValue);                //나중에 0은 i로 바꿔서 해야함
            double realHeight = (objREC.Height * usbCAMset_pixcelValue);

            Debug.WriteLine("디텍팅 너비:" + realWidth + "      높이:" + realHeight + "     픽셀값: " + microscopeCAMset_pixcelValue);

            //double Xoverlayrate = 0.32 - (0.32 * myoverlay) / 100.0;
            //double Yoverlayrate = 0.18 - (0.18 * myoverlay) / 100.0;

            //double widthmmperonepicture = 1920 * usbCAMset_pixcelValue;

            //현미경 카메라로 본 실제 가로 mm 길이, 가로/세로 비율값을 적용해 실제 세로 mm 길이
            double width_mmDistance_throughmicroscopeCam = microscopeCAMset_pixcelValue * 1920;
            double height_mmDistance_throughmicroscopeCam = microscopeCAMset_pixcelValue * 1080;

            width_mmDistance_throughmicroscopeCam = width_mmDistance_throughmicroscopeCam - (width_mmDistance_throughmicroscopeCam * (myoverlay / 100.0));
            height_mmDistance_throughmicroscopeCam = height_mmDistance_throughmicroscopeCam - (height_mmDistance_throughmicroscopeCam * (myoverlay / 100.0));


            /* 기존값
            double Xoverlayrate = 0.72 - (0.72 * mybasicsetvalue.myoverlay) / 100.0;
            double Yoverlayrate = 0.416 - (0.416 * mybasicsetvalue.myoverlay) / 100.0;
            */

            //double ZRangeLength = myZaxisCaptureRange / myZaxisCaptureInterval;

            double firstXpos = (objREC.X * usbCAMset_pixcelValue) - myCamCenterXPos_mm;
            double firstYpos = (objREC.Y * usbCAMset_pixcelValue) + myCamCenterYPos_mm;

            Debug.WriteLine("X축 " + firstXpos + "      Y축  " + firstYpos);


            double xx;
            double yy;
            double zz;

            List<AxisINFO> stglist = new List<AxisINFO>();


            for (double start_xpos = 0; start_xpos < realWidth; start_xpos += width_mmDistance_throughmicroscopeCam)
            {
                xx = firstXpos + start_xpos;
                for (double start_ypos = 0; start_ypos < realHeight; start_ypos += height_mmDistance_throughmicroscopeCam)
                {
                    yy = firstYpos + start_ypos;
                    for (double start_zpos = 0; start_zpos < myZaxisCaptureRange; start_zpos += myZaxisCaptureInterval)
                    {
                        zz = zpos - (myZaxisCaptureRange / 2) + start_zpos;

                        stglist.Add(new AxisINFO(xx, yy, zz));

                    }
                }
            }



            //리스트에 저장된 좌표값을 json으로 추출
            saveListtoJson(stglist, mypath);



            /*
            for (double start_xpos = 0; start_xpos < realWidth; start_xpos += width_mmDistance_throughmicroscopeCam)
            {
                xx = firstXpos + start_xpos;
                for (double start_ypos = 0; start_ypos < realHeight; start_ypos += height_mmDistance_throughmicroscopeCam)
                {
                    yy = firstYpos + start_ypos;
                    for (double start_zpos = 0; start_zpos < myZaxisCaptureRange; start_zpos += myZaxisCaptureInterval)
                    {
                        zz = zpos - (myZaxisCaptureRange / 2) + start_zpos;

                        stglist.Add(new AxisINFO(xx, yy, zz));
                    }
                }
            }
            */
            return stglist;
        }


        public void saveListtoJson(List<AxisINFO> mylist, string mypath)
        {
            var onlyXYZ = mylist.Select((item, idx) => new
            {
                num = idx + 1,
                x = item.x_position,
                y = item.y_position,
                z = item.z_position
            }).ToList();

            string jason_file_saving_path = mypath;

            Directory.CreateDirectory(jason_file_saving_path);

            jason_file_saving_path = jason_file_saving_path + @"\" + "좌표.json";

            string jsonString = JsonConvert.SerializeObject(onlyXYZ, Formatting.Indented);
            File.WriteAllText(jason_file_saving_path, jsonString);

            Debug.WriteLine("파일 저장 완료!,,,,,");
        }

        public Rect getCropRect()
        {
            Rect cropRect = new Rect(int.Parse(myXcropimgSize.ToString()), int.Parse(myYcropimgSize.ToString()), int.Parse(myWidthcropimgSize.ToString()), int.Parse(myHeightcropimgSize.ToString()));
            return cropRect;
        }

        //픽셀로 너비의 실거리 값을 구하는 함수
        public int getRealDistance_Width_pixel()
        {
            return (int)(bWidth / usbCAMset_pixcelValue);
        }

        //픽셀로 높이의 실거리 값을 구하는 함수
        public int getRealDistance_Height_pixel()
        {
            return (int)(bHeight / usbCAMset_pixcelValue);
        }

        //너비의 실거리 값을 구하는 함수
        public double getRealDistance_Width()
        {
            return bWidth * usbCAMset_pixcelValue;
        }
        //높이의 실거리 값을 구하는 함수
        public double getRealDistance_Height()
        {
            return bHeight * usbCAMset_pixcelValue;
        }

        //객체가 가지고 있는 기본 설정값을 저장하는 함수
        public void save()
        {
            string jsonString = System.Text.Json.JsonSerializer.Serialize(this);
            Debug.WriteLine(jsonString);

            Properties.Settings.Default.basicinfo = jsonString;
            Properties.Settings.Default.Save();
        }

    }
}

