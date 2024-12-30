using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SMC600_StageController_v2._0
{
    public partial class UserClick_ShowPicFiles : Form
    {
        public string getimgPath;

        //폼 실행과 동시에 저장된 경로의 이미지를 보여주는
        public UserClick_ShowPicFiles(string idx)
        {
            InitializeComponent();

            getimgPath = idx;
            this.Text = getimgPath;
            pictureBox_savedSnapshotImgShow.ImageLocation = getimgPath;
        }
    }
}
