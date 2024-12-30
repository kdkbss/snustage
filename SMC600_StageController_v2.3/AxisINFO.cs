using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace SMC600_StageController_v2._0
{
    public class AxisINFO
    {
        public double x_position;
        public double y_position;
        public double z_position;
        public string pictureFilePath;
        public string Status; // "Save" or "Not Saved"
        public string ImagePath; // 이미지 경로


        public static List<AxisINFO> coordinateValueList_WhereStageMoveto = new List<AxisINFO>();

        //현재의 업데이트되는 XYZ 위치 값을 저장
        public AxisINFO(double x, double y, double z)
        {
            x_position = x;
            y_position = y;
            z_position = z;

            Status = "Not Saved";
            ImagePath = "";
        }


    }
}
