using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using SMC600_StageController;

namespace SMC600_StageController_v2._0
{
    public partial class KDucManaul : UserControl
    {
        KDControlDevice myKDcontroller;

        double Adjusting_movement_whenPressArrowKeys = 0.0;

        public KDucManaul(KDControlDevice mKDcontroller)
        {
            InitializeComponent();

            myKDcontroller = mKDcontroller;

            radioButton_Velocity2.Checked = true;
        }

        //X축을 +방향으로 사용자가 설정한 속도로 이동
        private void button_moveLeft_Click(object sender, EventArgs e)
        {
            myKDcontroller.MoveAbsolute(myKDcontroller.xPositionValue + Adjusting_movement_whenPressArrowKeys, -1, -1);
        }

        //X축을 -방향으로 사용자가 설정한 속도로 이동
        private void button_moveRight_Click(object sender, EventArgs e)
        {
            myKDcontroller.MoveAbsolute(myKDcontroller.xPositionValue - Adjusting_movement_whenPressArrowKeys, -1, -1);
        }

        //Y축을 +방향으로 사용자가 설정한 속도로 이동
        private void button_moveUp_Click(object sender, EventArgs e)
        {
            myKDcontroller.MoveAbsolute(-1, myKDcontroller.yPositionValue + Adjusting_movement_whenPressArrowKeys, -1);

        }

        //Y축을 -방향으로 사용자가 설정한 속도로 이동
        private void button_moveDown_Click(object sender, EventArgs e)
        {
            myKDcontroller.MoveAbsolute(-1, myKDcontroller.yPositionValue - Adjusting_movement_whenPressArrowKeys, -1);
        }

        //Z축을 +방향으로 사용자가 설정한 속도로 이동
        private void button_stage_height_UP_Click(object sender, EventArgs e)
        {
            myKDcontroller.MoveAbsolute(-1, -1, myKDcontroller.zPositionValue + Adjusting_movement_whenPressArrowKeys);
        }

        //Z축을 -방향으로 사용자가 설정한 속도로 이동
        private void button_stage_height_Down_Click(object sender, EventArgs e)
        {
            myKDcontroller.MoveAbsolute(-1, -1, myKDcontroller.zPositionValue - Adjusting_movement_whenPressArrowKeys);
        }

        //스테이지 속도를 0.01MM로 지정
        private void radioButton_Velocity1_CheckedChanged(object sender, EventArgs e)
        {
            Adjusting_movement_whenPressArrowKeys = 0.01;

        }

        //스테이지 속도를 0.1MM로 지정
        private void radioButton_Velocity2_CheckedChanged(object sender, EventArgs e)
        {
            Adjusting_movement_whenPressArrowKeys = 0.1;

        }

        //스테이지 속도를 10MM로 지정
        private void radioButton_Velocity3_CheckedChanged(object sender, EventArgs e)
        {
            Adjusting_movement_whenPressArrowKeys = 10.0;

        }

        //사용자가 직접 입력한 좌표의 값으로 스테이지를 이동시키는 버튼
        private void button_moveUserPosition_Click(object sender, EventArgs e)
        {
            double X_userDirectlyEntersCoor = double.Parse(textBox_XaxisABS.Text);
            double Y_userDirectlyEntersCoor = double.Parse(textBox_YaxisABS.Text);
            double Z_userDirectlyEntersCoor = double.Parse(textBox_ZaxisABS.Text);


            myKDcontroller.MoveAbsolute(X_userDirectlyEntersCoor, Y_userDirectlyEntersCoor, Z_userDirectlyEntersCoor);
        }

        //스테이지를 원점으로 이동하게 하는 버튼
        private void button_getHomePosition_Click(object sender, EventArgs e)
        {
            myKDcontroller.mySMC600.GotoHome(true, true, true);
        }

        //스테이지의 실시간 현재 위치를 값을 UI로 업데이트 하는 함수
        public void setXYZpos(AxisINFO x)
        {
            this.BeginInvoke(new Action(delegate ()
            {

                textBox_XaxisABS.Text = x.x_position.ToString("0.000");
                textBox_YaxisABS.Text = x.y_position.ToString("0.000");
                textBox_ZaxisABS.Text = x.z_position.ToString("0.000");

            }));

        }

        //스테이지의 모든 이동을 중지시키는 버튼
        private void button_MoveStop_Click(object sender, EventArgs e)
        {
            myKDcontroller.mySMC600.MotorStop(true, true, true);
        }
    }
}
