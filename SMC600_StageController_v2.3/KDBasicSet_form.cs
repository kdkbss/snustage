using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Diagnostics;
using System.IO;
using System.Text.Json;

namespace SMC600_StageController_v2._0
{
    public partial class KDBasicSet_form : Form
    {
        Form1 myform1;
        KDBasicSettingValues mybasicset;

        public double getnumofYpic, getnumofXpic, getnumofZpic;
        public int Realdistance_microscopeCamera_overlaid_Xaxis, Realdistance_microscopeCamera_overlaid_Yaxis;
        public string selectFilePath;

        private void button_savefolderselc_Click(object sender, EventArgs e)
        {
            FolderBrowserDialog fbd = new FolderBrowserDialog();
            if (fbd.ShowDialog() == DialogResult.OK)
            {
                selectFilePath = fbd.SelectedPath;
                label_savefilename.Text = selectFilePath;
            }

        }

        private void groupBox2_Enter(object sender, EventArgs e)
        {

        }

        public KDBasicSet_form(Form1 mform1, KDBasicSettingValues mbasicset)
        {
            InitializeComponent();
            this.FormBorderStyle = FormBorderStyle.FixedDialog;

            myform1 = mform1;
            mybasicset = mbasicset;

            //KDBasicSettingValues 객체에 저장된 값을 해당 폼을 열 때, UI로 업데이트
            groupBox2.Text = $"자동 촬영 설정 ({"microscpCamName"} :: {mybasicset.mycameraWidth}X{mybasicset.mycameraHeight})";
            tBox_uPixcelSet.Text = mybasicset.usbCAMset_pixcelValue.ToString();
            textBox_userWriteWidth.Text = mybasicset.bWidth.ToString();
            textBox_userWriteHeight.Text = mybasicset.bHeight.ToString();
            tbox_overlay.Text = mybasicset.myoverlay.ToString();

            textBox_cropHeight.Text = mybasicset.myHeightcropimgSize.ToString();
            textBox_cropWidth.Text = mybasicset.myWidthcropimgSize.ToString();
            textBox_cropX.Text = mybasicset.myXcropimgSize.ToString();
            textBox_cropY.Text = mybasicset.myYcropimgSize.ToString();

            textBox_ZaxisCaptureRange.Text = mybasicset.myZaxisCaptureRange.ToString();
            textBox_ZaxisCaptureInterval.Text = mybasicset.myZaxisCaptureInterval.ToString();
            textBox_CamCenterXPos_mm.Text = mybasicset.myCamCenterXPos_mm.ToString();
            textBox_CamCenterYPos_mm.Text = mybasicset.myCamCenterYPos_mm.ToString();
            textBox_CamFocusZPos_mm.Text = mybasicset.myCamFocusZPos_mm.ToString();

            textBox_microCampixcel_mm.Text = mybasicset.myMicrocam_magni_mm.ToString();
            textBox_monitorResolution.Text = mybasicset.myMicrocamResolution.ToString();
            textBox_delaytime.Text = mybasicset.mydelaytime.ToString();
            textBox_user_define_obj_size.Text = (mybasicset.myobjRectangle_size * 100).ToString();


            //textBox_plantSpecimenName.Text = mybasicset.myPlantSpecimenName;
            Realdistance_microscopeCamera_overlaid_Xaxis = (int)get_howmuch_toOverlay(0.72);
            Realdistance_microscopeCamera_overlaid_Yaxis = (int)get_howmuch_toOverlay(0.416);
            getnumofXpic = double.Parse(textBox_userWriteWidth.Text) / Realdistance_microscopeCamera_overlaid_Xaxis;
            getnumofYpic = double.Parse(textBox_userWriteHeight.Text) / Realdistance_microscopeCamera_overlaid_Yaxis;
            getnumofZpic = double.Parse(textBox_ZaxisCaptureRange.Text) / double.Parse(textBox_ZaxisCaptureInterval.Text);

            textBox_number_of_slices.Text = "1";

            label_savefilename.Text = mybasicset.myfolderpath.ToString();
            label_microCamRealdistance_mm.Text = (mybasicset.microscopeCAMset_pixcelValue * 1920).ToString("0.00") + "mm";
        }

        private void button_magni_distance_Click(object sender, EventArgs e)
        {
            double getmicrocam_magnification = double.Parse(textBox_microCampixcel_mm.Text);

            mybasicset.microscopeCAMset_pixcelValue = GetFov(getmicrocam_magnification) / 1920;  //실제 화면상의 거리를 해상도로 나눠서 픽셀당 거리를 계산
            label_microCamRealdistance_mm.Text = (mybasicset.microscopeCAMset_pixcelValue * 1920).ToString("0.00") + "mm";
        }

        private void button1_Click(object sender, EventArgs e)
        {
            double myzCaptureRange = double.Parse(textBox_ZaxisCaptureRange.Text);

            double mynumber_of_slices = double.Parse(textBox_number_of_slices.Text);
            double zposCaptureInterval = myzCaptureRange / mynumber_of_slices;

            textBox_ZaxisCaptureInterval.Text = zposCaptureInterval.ToString("0.00");
        }

        private void button2_Click(object sender, EventArgs e)
        {
            double myzCaptureRange = double.Parse(textBox_ZaxisCaptureRange.Text);

            double myzCaptureInterval = double.Parse(textBox_ZaxisCaptureInterval.Text);
            double number_of_slices = myzCaptureRange / myzCaptureInterval;

            textBox_number_of_slices.Text = number_of_slices.ToString("0");
        }

        public double GetFov(double magnification)
        {
            if (magnification <= 0)
                throw new ArgumentException("배율(M)은 양의 값이어야 합니다.");

            // ln(M)
            double lnM = Math.Log(magnification);

            // ln(FOV(M)) = A - B*ln(M) - C*(ln(M))^2
            double lnFov = 2.308 - 0.41 * lnM - 0.068 * (lnM * lnM);

            // 최종 FOV = e^(lnFov)
            return Math.Exp(lnFov);
        }

        //저장된 오버레이 퍼센트를 실제 촬영 좌표에 반영하는 함수
        public double get_howmuch_toOverlay(double realfilmingWidthorHeigth)
        {
            return realfilmingWidthorHeigth - (realfilmingWidthorHeigth * mybasicset.myoverlay) / 100.0;
        }

        //현재 폼에 기입된 설정 값을 기본 값으로 저장하는 버튼
        private void btn_apply_Click(object sender, EventArgs e)
        {
            mybasicset.usbCAMset_pixcelValue = double.Parse(tBox_uPixcelSet.Text);
            mybasicset.bHeight = double.Parse(textBox_userWriteHeight.Text);
            mybasicset.bWidth = double.Parse(textBox_userWriteWidth.Text);
            //mybasicset.microscopeCAMset_pixcelValue = mybasicset.myMicrocamResolution * mybasicset.myMicrocam_magni_mm;
            mybasicset.microscopeCAMset_pixcelValue = GetFov(double.Parse(textBox_microCampixcel_mm.Text)) / 1920;
            mybasicset.myHeightcropimgSize = double.Parse(textBox_cropHeight.Text);
            mybasicset.myWidthcropimgSize = double.Parse(textBox_cropWidth.Text);
            mybasicset.myXcropimgSize = double.Parse(textBox_cropX.Text);
            mybasicset.myYcropimgSize = double.Parse(textBox_cropY.Text);

            Debug.WriteLine(mybasicset.microscopeCAMset_pixcelValue);

            mybasicset.myoverlay = double.Parse(tbox_overlay.Text);
            mybasicset.myZaxisCaptureRange = double.Parse(textBox_ZaxisCaptureRange.Text);
            mybasicset.myZaxisCaptureInterval = double.Parse(textBox_ZaxisCaptureInterval.Text);
            mybasicset.myCamCenterXPos_mm = double.Parse(textBox_CamCenterXPos_mm.Text);
            mybasicset.myCamCenterYPos_mm = double.Parse(textBox_CamCenterYPos_mm.Text);

            mybasicset.myMicrocam_magni_mm = double.Parse(textBox_microCampixcel_mm.Text);
            mybasicset.myMicrocamResolution = double.Parse(textBox_monitorResolution.Text);

            mybasicset.myCamFocusZPos_mm = double.Parse(textBox_CamFocusZPos_mm.Text);
            mybasicset.mydelaytime = int.Parse(textBox_delaytime.Text);

            mybasicset.myfolderpath = label_savefilename.Text;

            mybasicset.myobjRectangle_size = double.Parse(textBox_user_define_obj_size.Text) / 100;

            //mybasicset.microscopeCAMset_pixcelValue = GetFov(getmicrocam_magnification);
            mybasicset.save();

            Debug.WriteLine("USB카메라 픽셀값: " + mybasicset.usbCAMset_pixcelValue + "       현미경 카메라 픽셀값: " + mybasicset.microscopeCAMset_pixcelValue
                + "\r\n디텍팅 상자 너비: " + mybasicset.bHeight + "       ");



        }
    }
}
