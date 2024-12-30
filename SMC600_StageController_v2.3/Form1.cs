using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Diagnostics;
using GitHub.secile.Video;
using System.IO;
using System.Text.Json;
using System.Runtime.InteropServices;
using SMC600_StageController;
using OpenCvSharp;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.Button;
using System.Security.Cryptography;
using Newtonsoft.Json.Linq;


namespace SMC600_StageController_v2._0
{
    public partial class Form1 : Form
    {
        //스테이지 제어 관련
        public KDControlDevice myKDcontroller;

        //프로그램 UI(수동제어 / 자동제어 / 카메라) 관련
        public KDucManaul myUCmanual;
        public KDucAuto myUCauto;
        public KDUVCInterface myUVCInterface;

        //촬영용 basic 설정 관련
        public KDBasicSettingValues myBasicSetvalue;
        public KDCamera mycamera;

        private Rectangle rectRoi;
        private bool drawing = false;


        public Form1()
        {
            InitializeComponent();
            myBasicSetvalue = KDBasicSettingValues.Loadbyjson();
            myKDcontroller = new KDControlDevice(this);
            mycamera = new KDCamera(myBasicSetvalue);

            myUCauto = new KDucAuto(this);
            panel_UCmanualcontroller.Controls.Add(new KDucManaul(myKDcontroller));
            panel_UCautocontroller.Controls.Add(myUCauto);

            initcamera();
            radioButton_camFirst.Checked = true;
            groupBox1.Enabled = false;

        }

        //picture box에 현재 촬영 중인 카메라 화면을 업데이트
        private void UpdatePictureBox(Bitmap frame)
        {
            if (pictureBox_cameraScreen.InvokeRequired)                     //ui 스레드가 실행되지 않는 경우
            {
                pictureBox_cameraScreen.Invoke(new Action(() => pictureBox_cameraScreen.Image = frame));
            }
            else
            {
                pictureBox_cameraScreen.Image = frame;
            }
        }

        //카메라 연결
        private void initcamera()
        {
            myUVCInterface = new KDUVCInterface(UpdatePictureBox);

        }

        //PC와 컨트롤러 시리얼포트 연결
        private void btn_connection_Click(object sender, EventArgs e)
        {
            myKDcontroller.Connect();
        }


        //카메라 스크린 더블 클릭 시 발생 이벤트(클릭 좌표를 기점으로 사용자가 설정한 크기의 박스 형성)
        private void pictureBox_cameraScreen_MouseDoubleClick_1(object sender, MouseEventArgs e)
        {
            int pw = myBasicSetvalue.getRealDistance_Width_pixel();
            int ph = myBasicSetvalue.getRealDistance_Height_pixel();
            myUCauto.findObjlist.Clear();


            Rectangle mrc = new Rectangle(e.Location.X - pw / 2, e.Location.Y - ph / 2, (int)myBasicSetvalue.getRealDistance_Width_pixel(), (int)myBasicSetvalue.getRealDistance_Height_pixel());
            Rectangle newRc = KDCamera.imageRectToscreen(mrc, 1920, 1080, (double)pictureBox_cameraScreen.Width, (double)pictureBox_cameraScreen.Height);

            myUCauto.findObjlist.Add(newRc);

        }

        //라디오 버튼 클릭 시, 현미경 카메라 화면으로 전환(USB to MicroSc)
        private void radioButton_camFirst_CheckedChanged_1(object sender, EventArgs e)
        {
            if (radioButton_camFirst.Checked)                   //현미경카메라 라디오 버튼이 클릭된 경우
            {
                myUVCInterface?.SwitchCamera(true);
            }
        }

        //라디오 버튼 클릭 시, USB 카메라 화면으로 전환(USB to MicroSc)
        private void radioButton_camSecond_CheckedChanged_1(object sender, EventArgs e)
        {
            if (radioButton_camSecond.Checked)              //usb카메라 라디오 버튼이 클릭된 경우
            {
                myUVCInterface?.SwitchCamera(false);
            }
        }

        //폼 종료 이벤트로 카메라 및 컨트롤러와의 연결을 해제
        private void Closing(object sender, FormClosingEventArgs e)
        {
            myKDcontroller.ThreadStop();
            myUVCInterface?.StopCamera();
            myKDcontroller.Disconnect();
        }

        //현재 카메라 화면을 촬영
        private void btn_capture_Click_1(object sender, EventArgs e)
        {
            SaveFileDialog saveFileDialog = new SaveFileDialog();
            saveFileDialog.Title = "PNG 이미지 저장";
            saveFileDialog.Filter = "PNG 이미지 (*.png)|*.png";
            saveFileDialog.DefaultExt = "png";
            saveFileDialog.AddExtension = true;

            // 대화상자 열기
            if (saveFileDialog.ShowDialog() == DialogResult.OK)
            {
                KDCamera.savePhoto(myUVCInterface.capturedBitmap, 0, saveFileDialog.FileName);
            }
        }
        public Rectangle mrc;
        //검출된 박편의 모서리를 화면 송출 picturbox에 표시하는 이벤트 함수 
        private void pictureBox_cameraScreen_Paint_1(object sender, PaintEventArgs e)
        {
            PictureBox pb = (PictureBox)sender;
            Graphics g = e.Graphics;

            if (radioButton_camSecond.Checked == true)                      //usb카메라가 활성화 된 경우
            {
                foreach (Rectangle rc in myUCauto.findObjlist)
                {
                    mrc = KDCamera.imageRectToscreen(rc, (double)pb.Width, (double)pb.Height, KDUVCInterface.UVCFrameWidth, KDUVCInterface.UVCFrameHeight);
                    g.DrawRectangle(Pens.GreenYellow, mrc);
                }
            }

            using (Pen pen = new Pen(Color.Red, 2))
            {
                e.Graphics.DrawRectangle(pen, rectRoi);
                g.DrawString("x축: " + (xMouseLocation * 1.3333).ToString("0") + "        y축: " + (yMouseLocation * 1.3333).ToString("0") + "\r\n높이: " + (rectRoi.Height * 1.3333).ToString("0") + "        너비:" + (rectRoi.Width * 1.3333).ToString("0"),
                    DefaultFont, Brushes.Red, xMouseLocation, yMouseLocation);
            }

        }

        //임시 박편 찾기 버튼 클릭 이벤트
        private void button_findPlantTarget_Click_1(object sender, EventArgs e)
        {
            myUCauto.findObjlist = mycamera.changethepictogray(myUVCInterface.capturedBitmap, myBasicSetvalue.getCropRect());

        }


        //선택된 카메라의 라디오 버튼으로 ui를 변경하는 함수
        public void cameraSet(bool ismircoCam)
        {
            this.BeginInvoke(new Action(delegate ()
            {
                if (ismircoCam == true)                         //현미경 카메라가 실행되고 있는 경우
                {
                    radioButton_camFirst.Checked = true;
                }
                else
                {
                    radioButton_camSecond.Checked = true;
                }
            }));
        }


        //촬영 기본 설정 ui를 생성하는 함수
        private const int WM_SYSCOMMAND = 0x112;
        private const int MF_STRING = 0x0;
        private const int MF_SEPARATOR = 0x800;

        // P/Invoke declarations
        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetSystemMenu(IntPtr hWnd, bool bRevert);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern bool AppendMenu(IntPtr hMenu, int uFlags, int uIDNewItem, string lpNewItem);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern bool InsertMenu(IntPtr hMenu, int uPosition, int uFlags, int uIDNewItem, string lpNewItem);
        private int SYSMENU_ABOUT_ID = 0x1;

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            IntPtr hSysMenu = GetSystemMenu(this.Handle, false);
            AppendMenu(hSysMenu, MF_SEPARATOR, 0, string.Empty);
            AppendMenu(hSysMenu, MF_STRING, SYSMENU_ABOUT_ID, "촬영 기본 설정");
        }
        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);

            if ((m.Msg == WM_SYSCOMMAND) && ((int)m.WParam == SYSMENU_ABOUT_ID))
            {
                KDBasicSet_form modal = new KDBasicSet_form(this, myBasicSetvalue);
                modal.ShowDialog();
            }
        }

        //<<<<<<<<<<<<<<UI업데이트>>>>>>>>>>>>>>>
        public delegate void MySensorDelegate(int a);
        private void MainDelegate(int a) //센서 상태 변경

        {
            if (a == 0)
            {

                string cs = "";
                cs += "X: " + myKDcontroller.xPositionValue.ToString("0.000") + " mm\r\n";
                cs += "Y: " + myKDcontroller.yPositionValue.ToString("0.000") + " mm\r\n";
                cs += "Z: " + myKDcontroller.zPositionValue.ToString("0.000") + " mm\r\n";

                //Debug.WriteLine("Z값: " + myKDcontroller.zPositionValue);

                richTextBox_state.Text = cs;
            }
        }

        public void callAsyncMainMessageHandler(int a)
        {
            try
            {
                this.BeginInvoke(new MySensorDelegate(MainDelegate), new object[] { a });
            }

            catch (Exception e)
            {
                Debug.WriteLine("callAsyncMainMessageHandler eror: " + e.ToString());
            }
        }

        public int xMouseLocation, yMouseLocation = 0;

        private void pictureBox_cameraScreen_Click_1(object sender, EventArgs e)
        {
            drawing = true;
        }

        private void pictureBox_cameraScreen_MouseDown_1(object sender, MouseEventArgs e)
        {

            xMouseLocation = e.Location.X;
            yMouseLocation = e.Location.Y;

            rectRoi = new Rectangle(e.X, e.Y, 0, 0);
            this.Refresh();
        }

        private void button1_Click_1(object sender, EventArgs e)
        {

            double[] realxy_pos = myUCauto.getXYZcoorArrayByRect(myUCauto.findObjlist[0]);

            myKDcontroller.MoveAbsolute(realxy_pos[0], realxy_pos[1], -1);

        }

        private bool isconnected = false;

        private void btn_connection_Click_1(object sender, EventArgs e)
        {
            if (!isconnected)
            {
                myKDcontroller.Connect();
                groupBox1.Enabled = true;

                isconnected = true;
                btn_connection.Text = "연결해제";
            }

            else
            {
                myKDcontroller.Disconnect();
                groupBox1.Enabled = false;

                isconnected = false;
                btn_connection.Text = "연결";

            }


        }

        private void button2_Click(object sender, EventArgs e)
        {
            myKDcontroller.MoveAbsolute(100, 100, 0);
        }



        private void button_find_focus_Click(object sender, EventArgs e)
        {
            double now_stage_xpos = myKDcontroller.xPositionValue;
            double now_stage_ypos = myKDcontroller.yPositionValue;
            double now_stage_zpos = myKDcontroller.zPositionValue;

            cameraSet(true);

            myUCauto.maxsharpen = 0;
            List<AxisINFO> ml = KDControlDevice.searchAccurateFocus_Stage(now_stage_xpos, now_stage_ypos, 50, myBasicSetvalue.myCamFocusZPos_mm, 0.04);       //1차 초점 맞추기
            //myUCauto.mlcompleteEvent.Reset();
            myKDcontroller.MoveXYZ(ml, new KDControlDevice.MoveDoneCallback(myUCauto.maincallbackFocus));
            //myUCauto.mlcompleteEvent.Wait(token);

        }

        private void pictureBox_cameraScreen_MouseMove_1(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                rectRoi = new Rectangle(rectRoi.Left, rectRoi.Top, Math.Min(e.X - rectRoi.Left, pictureBox_cameraScreen.ClientRectangle.Width - rectRoi.Left), Math.Min(e.Y - rectRoi.Top, pictureBox_cameraScreen.ClientRectangle.Height - rectRoi.Top));

                this.Refresh();
            }
        }
    }
}
