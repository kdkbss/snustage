using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Threading;
using System.Diagnostics;
using System.IO;
using SMC600_StageController;
using Newtonsoft.Json.Linq;



namespace SMC600_StageController_v2._0
{
    //  지정 좌표로  움직여서 촬영및 저장 
    public partial class KDucAuto : UserControl
    {
        Form1 myform1;
        KDControlDevice mykdcontroller;
        public AxisINFO mfindpos = new AxisINFO(0, 0, 0);
        KDBasicSettingValues mybasicsetvalue;
        KDCamera mycamera;

        public List<AxisINFO> coordinateValueList_WhereStageMoveto;

        //Thread Control
        private ManualResetEventSlim mlcompleteEvent = new ManualResetEventSlim(false); //to control actions completed by the stage_self
        CancellationTokenSource cts = new CancellationTokenSource();
        private ManualResetEventSlim pauseEvent = new ManualResetEventSlim(true); //for users to control stop and restart the stage

        public double count_progressbar = 0;

        public double maxsharpen = 0;

        //박편 감지 UI
        public List<Rectangle> findObjlist = new List<Rectangle>();

        public double myXstandard_cm = 16.549;
        public double myYstandard_cm = 45.318;
        public double myZstandard_cm = 31.7;
        public int myXstandard_coordinate = 938;
        public int myYstandard_coordinate = 115;
        public int number_focus_attempts = 50;
        public int myXtest = 789;
        public int myYtest = 254;

        public double X_finaltest;
        public double Y_finaltest;

        public double seven_Magnification = 36.329;
        public double hundredeleveen_Magnification = 40.093;

        public string entire_sample_folder = "";
        public string sub_sample_folder = "";

        public string stage_current_task_inprogress;

        //public List<AxisINFO> coordinateValueList_WhereStageMove;
        private List<AxisINFO> items; // 아이템 리스트
        private readonly object updateLock = new object(); // 동기화를 위한 락 객체

        int check_number_of_snapshot = 1;


        public KDucAuto(Form1 mform1)
        {
            InitializeComponent();
            myform1 = mform1;
            mykdcontroller = mform1.myKDcontroller;
            mybasicsetvalue = mform1.myBasicSetvalue;
            mycamera = mform1.mycamera;

        }

        private void KDucAuto_Load(object sender, EventArgs e)
        {
            listView1.VirtualMode = true; // VirtualMode 활성화 
            listView1.RetrieveVirtualItem += ListView_RetrieveVirtualItem;
        }

        private void ListView_RetrieveVirtualItem(object sender, RetrieveVirtualItemEventArgs e)
        {

            if (items != null && items.Count > 0 && e.ItemIndex >= 0 && e.ItemIndex < items.Count)
            {
                var item = items[e.ItemIndex];
                e.Item = new ListViewItem(new string[] { e.ItemIndex.ToString(), item.x_position.ToString(), item.y_position.ToString(), item.z_position.ToString(), item.Status.ToString(), item.ImagePath });
            }
            else
            {
                e.Item = new ListViewItem(); // 기본 빈 항목 반환
            }
        }

        public void UpdateItem(int index, string newpath)
        {
            lock (updateLock) // 데이터 접근 보호
            {
                if (index >= 0 && index < items.Count)
                {
                    var item = items[index];
                    item.Status = "Saved";
                    item.ImagePath = newpath;
                    listView1.Invoke(new Action(() => listView1.RedrawItems(index, index, true)));
                }
            }
        }

        public void UpdateAllItems(List<AxisINFO> updatedItems)
        {
            lock (updateLock) // 데이터 접근 보호
            {
                // 전체 아이템 리스트를 업데이트
                items = updatedItems;
                listView1.Invoke(new Action(() =>
                {
                    listView1.VirtualListSize = items.Count;
                    listView1.Refresh(); // 리스트뷰 전체 갱신
                }));

            }
        }


        public enum magnificationMode
        {
            low,
            high
        }

        //리스트의 순서에 따라 스테이지가 이동할 때 콜백 불리는 함수(초점 체크용)
        public void maincallbackFocus(AxisINFO mpos, int mindex)
        {
            try
            {
                check_to_Pause_or_Cancel(pauseEvent, cts.Token);
                //pauseEvent.Wait(cts.Token);

                if (mpos == null)                           //리스트의 이동 좌표가 끝난 경우
                {
                    Debug.WriteLine("초점: " + mfindpos.x_position + " Y: " + mfindpos.y_position + " Z: " + mfindpos.z_position);

                    mykdcontroller.MoveAbsolute(mfindpos.x_position, mfindpos.y_position, mfindpos.z_position);
                    string imgpath = Path.Combine(entire_sample_folder, sub_sample_folder) + @"\" + "초점" + ".png";
                    imgpath = KDCamera.savePhoto(myform1.myUVCInterface.capturedBitmap, 1, imgpath);


                    mlcompleteEvent.Set();
                }
                else                                            //다음 이동 좌표가 남아있는 경우
                {
                    double sharpenessValue = KDCamera.getSharpenessValue(myform1.myUVCInterface.capturedBitmap);
                    Debug.WriteLine("sharpenessValue :" + sharpenessValue);

                    count_progressbar++;
                    progressBarUI_Update(count_progressbar * 2);

                    if (maxsharpen < sharpenessValue)         //이전 값보다 현재의 초점 값이 높은 경우 MAX의 값을 갱신
                    {
                        maxsharpen = sharpenessValue;
                        Debug.WriteLine("Max sharpenessValue: " + maxsharpen);
                        mfindpos = mpos;
                    }

                    if(cts.Token.IsCancellationRequested)
                    {
                        mlcompleteEvent.Set();
                    }
                }
            }

            catch (OperationCanceledException)
            {
                // 여기로 들어오면 cts.Cancel()로 인해 취소된 것.
                // 취소 시점에 대한 UI 정리나 자원해제 로직을 작성
                Debug.WriteLine("작업 취소됨: fullautoThread");
                progressStatusUI_update("작업이 취소되었습니다.");
                //mlcompleteEvent.Set();
                // progressBarUI_Update(0) 같은 초기화도 여기서 가능
            }
            catch (Exception ex)
            {
                // 다른 일반 예외 처리
                Debug.WriteLine($"fullautoThread 예외 발생: {ex.Message}");
                MessageBox.Show("자동 촬영 중 예외가 발생하였습니다: " + ex.Message);
                //mlcompleteEvent.Set();
            }
            finally
            {
                // (선택) 스레드 종료 후 마무리 로직
                Debug.WriteLine("fullautoThread 종료");
            }


        }

        //탐지된 박편의 위치를 기반으로 해당 박편의 정확한 중앙 XYZ 좌표를 구하는 함수
        public double[] getXYZcoorArrayByRect(System.Drawing.Rectangle mRect)
        {
            double[] xy = new double[2];

            double centerXpos = (mRect.X + mRect.Width / 2);
            double centerYpos = (mRect.Y + mRect.Height / 2);

            double centerXrealmm = centerXpos * mybasicsetvalue.usbCAMset_pixcelValue;
            double centerYrealmm = centerYpos * mybasicsetvalue.usbCAMset_pixcelValue;

            //화면의 x -y 카메라 축바뀜
            X_finaltest = centerXrealmm - mybasicsetvalue.myCamCenterXPos_mm;
            Y_finaltest = centerYrealmm + mybasicsetvalue.myCamCenterYPos_mm;

            xy[0] = X_finaltest;
            xy[1] = Y_finaltest;

            return xy;
        }

        // 탐색한 박편의 XYZ 값을 찾는 함수
        public void maincallbackFindOBJ(AxisINFO mpos, int mindex)
        {
            try
            {

                check_to_Pause_or_Cancel(pauseEvent, cts.Token);
                //pauseEvent.Wait(cts.Token);

                if (mpos == null)
                {
                    Debug.WriteLine("초점: " + mfindpos.x_position + " Y: " + mfindpos.y_position + " Z: " + mfindpos.z_position);
                    mlcompleteEvent.Set();
                    Debug.WriteLine("Find object Start.................");

                }
                else
                {
                    mfindpos = mpos;
                    Debug.WriteLine("Stage is Moving....................");
                }
            }

            catch (OperationCanceledException)
            {
                progressStatusUI_update("작업이 취소되었습니다.");
            }
            catch (Exception ex)
            {
                MessageBox.Show("자동 촬영 중 예외가 발생하였습니다: " + ex.Message);
            }
            finally
            {
                Debug.WriteLine("fullautoThread 종료");
            }
        }


        private void check_to_Pause_or_Cancel(ManualResetEventSlim pauseEvent, CancellationToken token)
        {
            pauseEvent.Wait(token);

        }

        //초점을 맞추는 기능이 들어간 스레드
        public void fullautoThread(CancellationToken token)
        {
            try
            {
                Debug.WriteLine("Focus Thread Start");
                mlcompleteEvent.Reset();  //스레드 이벤트 초기화

                //begin to start check the pause state
                check_to_Pause_or_Cancel(pauseEvent, token);
                //pauseEvent.Wait(token);

                //원점이동 체크일 경우
                if (checkBox_home.Checked == true)
                {
                    progressBarUI_Update(0);    //진행바 UI 초기화
                    progressStatusUI_update("원점 이동 중...");

                    mykdcontroller.MoveAbsolute(-1, -1, 0);

                    while (mykdcontroller.zPositionValue > 1)
                    {
                        if (mykdcontroller.zPositionValue == 1)
                        {
                            break;
                        }
                        check_to_Pause_or_Cancel(pauseEvent, token);
                        //pauseEvent.Wait(token);

                    }
                    mykdcontroller.MoveHome();


                    while (true)
                    {
                        Debug.WriteLine("Checking Stage Move Start,,,,,,,,,,,,,");
                        Thread.Sleep(300);

                        check_to_Pause_or_Cancel(pauseEvent, token);
                        //pauseEvent.Wait(token);


                        if (mykdcontroller.isMove() == false)                       //스테이지의 이동 유무를 파악
                        {
                            progressStatusUI_update("원점 이동 완료...");
                            break;
                        }
                    }
                }

                int largest_value_amongXY;

                //USB 카메라를 통해 시료의 위치 찾기
                if (checkBox_detectobj.Checked == true)
                {
                    progressBarUI_Update(0);    //진행바 UI 초기화
                    progressStatusUI_update("박편 탐지 중...");

                    check_to_Pause_or_Cancel(pauseEvent, token);
                    //pauseEvent.Wait(token);

                    findObjlist.Clear();

                    AxisINFO getobjList = new AxisINFO(100, 100, 0);
                    List<AxisINFO> searchingObjList = new List<AxisINFO>();
                    searchingObjList.Add(getobjList);

                    check_to_Pause_or_Cancel(pauseEvent, token);
                    //pauseEvent.Wait(token);

                    largest_value_amongXY = (int)mykdcontroller.get_value_on_specific_axis(0) > (int)mykdcontroller.get_value_on_specific_axis(1) ?
                        (int)mykdcontroller.get_value_on_specific_axis(0) : (int)mykdcontroller.get_value_on_specific_axis(1);


                    mykdcontroller.MoveXYZ(searchingObjList, new KDControlDevice.MoveDoneCallback(maincallbackFindOBJ));        //좌표 위치로 스테이지를 이동

                    Debug.WriteLine("Stage Move wait........");

                    mlcompleteEvent.Wait(token);

                    Debug.WriteLine("wait end........");

                    Thread.Sleep(3000); //카메라 변환 시간 wait

                    check_to_Pause_or_Cancel(pauseEvent, token);

                    findObjlist = mycamera.changethepictogray(myform1.myUVCInterface.capturedBitmap, mybasicsetvalue.getCropRect());

                    Bitmap detectedOBJbitmap = new Bitmap(myform1.myUVCInterface.capturedBitmap);

                    using (Graphics g = Graphics.FromImage(detectedOBJbitmap))
                    {
                        foreach (Rectangle rect in findObjlist)
                        {
                            g.DrawRectangle(Pens.GreenYellow, rect);
                        }
                    }

                    string imgpath = entire_sample_folder + @"\" + "박편 탐지" + ".png";
                    imgpath = KDCamera.savePhoto(detectedOBJbitmap, 1, imgpath);

                    progressStatusUI_update("박편 탐지 완료...");

                    Thread.Sleep(6000);

                    check_to_Pause_or_Cancel(pauseEvent, token);
                }

                if (findObjlist.Count <= 0)
                {
                    return;
                }


                for (int i = 0; i < findObjlist.Count; i++)
                {
                    check_to_Pause_or_Cancel(pauseEvent, token);

                    Rectangle mObjectrect = findObjlist[i];

                    //탐색한 박편의 위치를 바탕으로 1차 / 2차 초점 맞추기 진행 
                    if (checkBox_focus.Checked == true)
                    {
                        progressBarUI_Update(0);    //진행바 UI 초기화
                        progressStatusUI_update(i + 1 + "번 박편 초점 조절 중...");

                        check_to_Pause_or_Cancel(pauseEvent, token);

                        char chrindex = (char)('A' + i);
                        sub_sample_folder = textBox_objName.Text + "_" + chrindex;

                        //1~2차 초점 찾기 함수
                        search_obj_focus(mObjectrect, token);
                        progressStatusUI_update(i + 1 + "번 박편 초점 조정 완료...");

                        check_to_Pause_or_Cancel(pauseEvent, token);

                    }


                    if (checkBox_capture.Checked == true)
                    {
                        //check_to_Pause_or_Cancel(pauseEvent, token);
                        progressBarUI_Update(0);    //진행바 UI 초기화
                        count_progressbar = 0;
                        progressStatusUI_update(i + 1 + "번 박편 사진 촬영 중...");

                        double zpos = mybasicsetvalue.myCamFocusZPos_mm;

                        if (checkBox_focus.Checked == true)
                        {
                            zpos = mfindpos.z_position;
                        }

                        char chrindex = (char)('A' + i);
                        sub_sample_folder = textBox_objName.Text + "_" + chrindex;

                        string imgpath = Path.Combine(entire_sample_folder, sub_sample_folder);

                        
                        coordinateValueList_WhereStageMoveto = mybasicsetvalue.getXYZcoorList(mObjectrect, zpos, imgpath);                 //0은 나중에 1로 바꿔야함

                        UpdateAllItems(coordinateValueList_WhereStageMoveto);

                        Debug.WriteLine("subfoldername: " + sub_sample_folder);
                        check_to_Pause_or_Cancel(pauseEvent, token);
                        mlcompleteEvent.Reset();
                        mykdcontroller.MoveXYZ(coordinateValueList_WhereStageMoveto, new KDControlDevice.MoveDoneCallback(maincallbackCapture));
                        mlcompleteEvent.Wait(token);
                        check_to_Pause_or_Cancel(pauseEvent, token);
                        progressStatusUI_update(i + 1 + "번 박편 사진 촬영 종료...");

                        check_to_Pause_or_Cancel(pauseEvent, token);
                    }
                }

                progressStatusUI_update("자동 시작 종료...");

                progressBarUI_Update(0);    //진행바 UI 초기화
                count_progressbar = 0;
                MessageBox.Show("자동 촬영이 종료됐습니다.", "자동 촬영");

            }

            catch (OperationCanceledException)
            {
                // 여기로 들어오면 cts.Cancel()로 인해 취소된 것.
                // 취소 시점에 대한 UI 정리나 자원해제 로직을 작성
                Debug.WriteLine("작업 취소됨: fullautoThread");
                progressStatusUI_update("작업이 취소되었습니다.");
                // progressBarUI_Update(0) 같은 초기화도 여기서 가능
            }
            catch (Exception ex)
            {
                // 다른 일반 예외 처리
                Debug.WriteLine($"fullautoThread 예외 발생: {ex.Message}");
                MessageBox.Show("자동 촬영 중 예외가 발생하였습니다: " + ex.Message);
            }
            finally
            {
                // (선택) 스레드 종료 후 마무리 로직
                Debug.WriteLine("fullautoThread 종료");
            }
        }


        public void search_obj_focus(Rectangle obj_rect, CancellationToken token)
        {
            try
            {
                check_to_Pause_or_Cancel(pauseEvent, token);

                myform1.cameraSet(true);

                double[] realxy_pos = getXYZcoorArrayByRect(obj_rect);

                maxsharpen = 0;
                List<AxisINFO> ml = KDControlDevice.searchAccurateFocus_Stage(realxy_pos[0], realxy_pos[1], 50, mybasicsetvalue.myCamFocusZPos_mm, 0.04);       //1차 초점 맞추기
                mlcompleteEvent.Reset();
                mykdcontroller.MoveXYZ(ml, new KDControlDevice.MoveDoneCallback(maincallbackFocus));
                mlcompleteEvent.Wait(token);

                check_to_Pause_or_Cancel(pauseEvent, token);


                // 2차 초점 맞추기
                maxsharpen = 0;
                count_progressbar = 0;
                List<AxisINFO> ml2 = KDControlDevice.searchAccurateFocus_Stage(mfindpos.x_position, mfindpos.y_position, number_focus_attempts, mfindpos.z_position - number_focus_attempts / 2 * 0.001, 0.001);
                mlcompleteEvent.Reset();
                mykdcontroller.MoveXYZ(ml2, new KDControlDevice.MoveDoneCallback(maincallbackFocus));
                mlcompleteEvent.Wait(token);

                check_to_Pause_or_Cancel(pauseEvent, token);
            }



            catch (OperationCanceledException)
            {
                progressStatusUI_update("작업이 취소되었습니다.");
            }
            catch (Exception ex)
            {
                MessageBox.Show("자동 촬영 중 예외가 발생하였습니다: " + ex.Message);
            }
            finally
            {
                Debug.WriteLine("fullautoThread 종료");
            }
        }

        public void progressStatusUI_update(string now_stageStatus)
        {
            this.BeginInvoke(new Action(delegate ()
            {
                label_progressstatus.Text = now_stageStatus;

            }));
        }

        //프로그레스바의 UI를 업데이트 하는 함수
        public void progressBarUI_Update(double progressvalue)
        {
            this.BeginInvoke(new Action(delegate ()
            {
                if (progressvalue >= progressBar_forfocus.Maximum)
                {
                    progressvalue = progressBar_forfocus.Maximum;
                }
                progressBar_forfocus.Value = (int)progressvalue;

            }));
        }

        // 사진 촬영 콜백 함수
        public void maincallbackCapture(AxisINFO mpos, int mindex)
        {

            try
            {
                int delaytime = mybasicsetvalue.mydelaytime;

                check_to_Pause_or_Cancel(pauseEvent, cts.Token);
                //pauseEvent.Wait(cts.Token);

                Task.Delay(delaytime, cts.Token).Wait();


                if (mpos == null)
                {
                    mlcompleteEvent.Set();
                    check_number_of_snapshot = 1;
                }

                else
                {
                    int numchec = coordinateValueList_WhereStageMoveto.Count;
                    double chec = 100.0 / numchec;

                    count_progressbar += 100.0 / coordinateValueList_WhereStageMoveto.Count;

                    //Debug.WriteLine("리스트 수: " + numchec + "        퍼센트값: " + chec + "       누적:" + count_progressbar);

                    progressBarUI_Update(count_progressbar);

                    string imgpath = Path.Combine(entire_sample_folder, sub_sample_folder) + @"\" + sub_sample_folder + "_" + check_number_of_snapshot + "번 사진.png";
                    imgpath = KDCamera.savePhoto(myform1.myUVCInterface.capturedBitmap, 1, imgpath);
                    if (imgpath != null)
                    {
                        UpdateItem(mindex, imgpath);
                        check_number_of_snapshot++;
                    }
                    else
                    {
                        Debug.WriteLine("파일이 존재하지 않습니다.");
                        check_number_of_snapshot++;
                    }
                }
            }
            
            
            catch (OperationCanceledException)
            {
                // 여기로 들어오면 cts.Cancel()로 인해 취소된 것.
                // 취소 시점에 대한 UI 정리나 자원해제 로직을 작성
                Debug.WriteLine("작업 취소됨: fullautoThread");
                progressStatusUI_update("작업이 취소되었습니다.");
                //mlcompleteEvent.Set();
                // progressBarUI_Update(0) 같은 초기화도 여기서 가능
            }
            catch (Exception ex)
            {
                // 다른 일반 예외 처리
                Debug.WriteLine($"fullautoThread 예외 발생: {ex.Message}");
                MessageBox.Show("자동 촬영 중 예외가 발생하였습니다: " + ex.Message);
                //mlcompleteEvent.Set();
            }
            finally
            {
                // (선택) 스레드 종료 후 마무리 로직
                Debug.WriteLine("fullautoThread 종료");
            }
        }

        //생성된 좌표 리스트를 더블 클릭 시, 해당 촬영 사진 노출
        private void listView1_MouseDoubleClick_1(object sender, MouseEventArgs e)
        {
            if (listView1.SelectedIndices.Count > 0)
            {
                string idx = listView1.Items[listView1.FocusedItem.Index].SubItems[5].Text.ToString();

                Debug.WriteLine("저장위치: " + idx);

                UserClick_ShowPicFiles savedSnapshotImg = new UserClick_ShowPicFiles(idx);

                savedSnapshotImg.ShowDialog();
            }
        }

        private void btn_autocaptureStop_Click(object sender, EventArgs e)
        {
            mykdcontroller.ThreadStop();
            findObjlist.Clear();
        }


        private void button_step_by_step_start_Click_1(object sender, EventArgs e)
        {
            if (cts != null) //기존의 cts 취소(오류 예방 차원)
            {
                cts.Dispose();
            }

            cts = new CancellationTokenSource();
            pauseEvent.Set();

            entire_sample_folder = mybasicsetvalue.myfolderpath + @"\" + textBox_objName.Text;
            myform1.cameraSet(false);
            Task.Run(() => fullautoThread(cts.Token));
            //버튼 ui
            button_all_stop.Enabled = true;
            button_step_by_step_start.Enabled = false;

        }

        private bool isPaused = false;

        private void button1_Click(object sender, EventArgs e)
        {
            if (!isPaused)
            {
                pauseEvent.Reset();
                isPaused = true;
                button1.Text = "재개";
                progressStatusUI_update("자동 프로세스 일시정지...");
            }

            else
            {
                pauseEvent.Set();
                isPaused = false;
                button1.Text = "일시정지";
                progressStatusUI_update("자동 프로세스 재개...");
            }

        }

        private void button_all_stop_Click(object sender, EventArgs e)
        {
            cts.Cancel();
            mykdcontroller.ThreadStop_to_userstop();
            mykdcontroller.MoveAbsolute(-1, -1, -1);


            //ui초기화
            progressBarUI_Update(0);    //진행바 UI 초기화
            count_progressbar = 0;
            progressStatusUI_update("자동 촬영 대기 중...");


            listView1.Invoke(new Action(() =>
            {
                listView1.Refresh(); // 리스트뷰 전체 갱신
            }));

            button_step_by_step_start.Enabled = true;
            button_all_stop.Enabled = false;
        }


    }
}
