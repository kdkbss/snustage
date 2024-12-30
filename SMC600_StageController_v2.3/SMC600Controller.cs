using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading;
using System.Diagnostics;

namespace SMC600_StageController
{

    public class SMC600Controller
    {
        private SerialPort mSerialPort;

        private Thread mControlThread;
        private bool ThRun;

        private byte[] mBuffer;

        public MotorStatus mXsatus;
        public MotorStatus mYsatus;
        public MotorStatus mZsatus;

        public double mXpos;
        public double mYpos;
        public double mZpos;

        //모더의 현재 상태
        public enum MotorStatus : int
        {
            MS_STOP = 0,
            MS_MOVEING = 1,  //모터 움직이고 있음
            MS_OFF_SLEEP = 2, // 모터 멈춰있고 슬립모드 
            MS_ERROR = 90,   // 모터 에러

        }


        //PC와 컨트롤러를 연결용 세팅값
        public SMC600Controller(int portNum)
        {
            mSerialPort = new SerialPort();
            mSerialPort.PortName = "COM" + portNum;
            mSerialPort.BaudRate = 115200;
            mSerialPort.Parity = Parity.None;
            mSerialPort.DataBits = 8;
            mSerialPort.StopBits = StopBits.One;
            mSerialPort.Handshake = Handshake.None;

            mSerialPort.Encoding = Encoding.ASCII;
            mSerialPort.DataReceived += new SerialDataReceivedEventHandler(SerialPort_DataReceived);

            mSerialPort.ReadTimeout = 2000;
            mSerialPort.WriteTimeout = 1000;

            mBuffer = new byte[0x1000];
        }


        //PC와 컨트롤러를 연결하는 함수
        public bool Connet()
        {
            mSerialPort.Open();
            if (mSerialPort.IsOpen)
            {
                if (mControlThread != null)
                {
                    ThRun = false;
                    Thread.Sleep(200);
                    mControlThread = null;
                }

                mControlThread = new Thread(ThreadProc);
                ThRun = true;
                mControlThread.Start();

                return true;
            }


            return false;

        }

        //PC와 컨트롤러의 연결을 해제하는 함수
        public void Disconnect()
        {
            ThRun = false;
            Thread.Sleep(100);
            mSerialPort.Close();
        }


        private void ThreadProc()
        {
            while (ThRun)
            {
                RequestStatus();
                Thread.Sleep(100);
            }
        }

        //시리얼포트로부터 데이터를 송신받는 이벤트 함수
        private void SerialPort_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            try
            {
                if (mSerialPort.IsOpen == true)             //연결돼있을 경우
                {

                    int readCount = mSerialPort.Read(mBuffer, 0, mBuffer.Length);
                    if (readCount >= 62 && mBuffer[0] == '{' && mBuffer[61] == '}')                 //지정된 버퍼 이상 수신되며 시작과 끝 버퍼의 형식이 적합한 경우
                    {
                        byte[] buffer = new byte[60];
                        for (int i = 0; i < buffer.Length; i++)
                        {
                            buffer[i] = mBuffer[i + 1];
                        }
                        char[] decodedCharArray = System.Text.Encoding.UTF8.GetString(buffer).ToCharArray();
                        byte[] decodedBuffer = Convert.FromBase64CharArray(decodedCharArray, 0, decodedCharArray.Length);
                        MSGPacketPC revMsg = new MSGPacketPC(decodedBuffer);

                        //Debug.WriteLine(" rad ptype : " + revMsg.ptype);

                        if (revMsg.ptype == (int)MSGPacketPC.UC_TYPE.UC_ACK_STATUS)  
                        {
                            UpdateStatus(revMsg);
                        }
                    }
                }
           }
           
            catch (System.IO.IOException e44)
            {

            }
            catch (Exception e1)
            {

            }
        }

        //컨트롤러를 통해 스테이지에 명령을 전달하는 함수
        private int writeBytes(byte[] buffer)
        {
            if (mSerialPort.IsOpen == true)
            {
                try
                {
                    mSerialPort.Write(buffer, 0, buffer.Length);
                    return buffer.Length;
                }
                catch (TimeoutException e)
                {

                }
                catch (System.IO.IOException e)
                {
                   
                }
                catch (Exception e)
                {

                }

            }
            return -1;
        }

        private void SendPacket(MSGPacketPC mMsg)
        {
            byte[] mmsg = mMsg.getEncodePacket();
            this.writeBytes(mmsg);
        }


        //현재 스테이지의 상태를 요청하는 메시지 전송
        private void RequestStatus()
        {
            MSGPacketPC mp = new MSGPacketPC();
            mp.ptype = (int)MSGPacketPC.UC_TYPE.UC_REQ_STATUS;
            SendPacket(mp);
        }

        //현재 스테이지의 위치 좌표를 업데이트
        private void UpdateStatus(MSGPacketPC mMsg)
        {
            this.mXsatus = (MotorStatus)mMsg.xcmd_status;
            this.mXpos = mMsg.xpos;

            this.mYsatus = (MotorStatus)mMsg.ycmd_status;
            this.mYpos = mMsg.ypos;


            this.mZsatus = (MotorStatus)mMsg.zcmd_status;
            this.mZpos = mMsg.zpos;

        }

        //원점으로 이동 명령 버퍼를 생성 및 스테이지에 전달하는 함수
        public bool GotoHome(bool mX, bool mY, bool mZ)
        {
            MSGPacketPC mp = new MSGPacketPC();
            mp.ptype = (int)MSGPacketPC.UC_TYPE.UC_REQ_CONTROL;
            if (mX)
            {
                mp.xcmd_status = (int)MSGPacketPC.UC_TYPE.UC_MOTOR_SET_HOME;

            }
            if (mY)
            {
                mp.ycmd_status = (int)MSGPacketPC.UC_TYPE.UC_MOTOR_SET_HOME;
            }
            if (mZ)
            {
                mp.zcmd_status = (int)MSGPacketPC.UC_TYPE.UC_MOTOR_SET_HOME;
            }
            SendPacket(mp);
            return true;
        }

        //이동 중지를 명령 버퍼를 생성 및 스테이지에 전달하는 함수
        public bool MotorStop(bool mX, bool mY, bool mZ)
        {
            MSGPacketPC mp = new MSGPacketPC();
            mp.ptype = (int)MSGPacketPC.UC_TYPE.UC_REQ_CONTROL;
            if (mX)
            {
                mp.xcmd_status = (int)MSGPacketPC.UC_TYPE.UC_MOTOR_SET_STOP;
            }
            if (mY)
            {
                mp.ycmd_status = (int)MSGPacketPC.UC_TYPE.UC_MOTOR_SET_STOP;
            }
            if (mZ)
            {
                mp.zcmd_status = (int)MSGPacketPC.UC_TYPE.UC_MOTOR_SET_STOP;
            }
            SendPacket(mp);
            return true;
        }

        //XYZ축으로의 이동 명령 버퍼를 생성 및 스테이지에 전달하는 함수
        public bool MotorABSMove(int mXpos, int mYpos, int mZpos)
        {
            MSGPacketPC mp = new MSGPacketPC();
            mp.ptype = (int)MSGPacketPC.UC_TYPE.UC_REQ_CONTROL;

            mp.xpos = -1;
            mp.ypos = -1;
            mp.zpos = -1;

            if (mXpos >= 0)
            {
                mp.xcmd_status = (int)MSGPacketPC.UC_TYPE.UC_MOTOR_SET_ABS_POS;
                mp.xpos = mXpos;
                mXsatus = MotorStatus.MS_MOVEING;

            }
            if (mYpos >= 0)
            {
                mp.ycmd_status = (int)MSGPacketPC.UC_TYPE.UC_MOTOR_SET_ABS_POS;
                mp.ypos = mYpos;
                mYsatus = MotorStatus.MS_MOVEING;
            }
            if (mZpos >= 0)
            {
                mp.zcmd_status = (int)MSGPacketPC.UC_TYPE.UC_MOTOR_SET_ABS_POS;
                mp.zpos = mZpos;
                mZsatus = MotorStatus.MS_MOVEING;
            }


            SendPacket(mp);
            return true;
        }

        //이동의 유무를 체크하는 명령 버퍼를 생성 및 스테이지에 전달하는 함수
        public bool isBusy()
        {
            if (mXsatus == MotorStatus.MS_MOVEING || mYsatus == MotorStatus.MS_MOVEING || mZsatus == MotorStatus.MS_MOVEING)
            {
             //   Debug.WriteLine(" is moveing...");
                return true;
            }

          //  Debug.WriteLine(" is stop..");
            return false;

        }

    }



    public class MSGPacketPC
    {
        public int ptype;
        public int opid;
        public int xcmd_status;
        public int xpos;
        public int ycmd_status;
        public int ypos;
        public int zcmd_status;
        public int zpos;
        public int rv1;
        public int rv2;
        public int rv3;
        public byte rv4;

        public enum UC_TYPE : int
        {
            UC_NONE = 0,
            UC_REQ_STATUS = 10,//상태정보요청
            UC_ACK_STATUS = 13,//상태정보 응답
            UC_REQ_CONTROL = 14,//모터 제어 명령어 
            UC_ACK_CONTROL = 15,//모터 제어 명령어  응답

            UC_MOTOR_SET_HOME = 20,//모터 홈위치로
            UC_MOTOR_SET_STOP = 21, // 모터 바로 정지
            UC_MOTOR_SET_ABS_POS = 22,//모터 절대위치로 이동
            UC_MOTOR_SET_RLE_POS = 23,//모터 상대위치로 이동

        }

        public MSGPacketPC()
        {

        }
        public MSGPacketPC(byte[] mbuffers)
        {
            ptype = BitConverter.ToInt32(mbuffers, 0);
            opid = BitConverter.ToInt32(mbuffers, 4);
            xcmd_status = BitConverter.ToInt32(mbuffers, 8);
            xpos = BitConverter.ToInt32(mbuffers, 12);
            ycmd_status = BitConverter.ToInt32(mbuffers, 16);
            ypos = BitConverter.ToInt32(mbuffers, 20);
            zcmd_status = BitConverter.ToInt32(mbuffers, 24);
            zpos = BitConverter.ToInt32(mbuffers, 28);
            rv1 = BitConverter.ToInt32(mbuffers, 32);
            rv2 = BitConverter.ToInt32(mbuffers, 36);
            rv3 = BitConverter.ToInt32(mbuffers, 40);

        }

        public byte[] serializeData()
        {

            List<byte> buffer = new List<byte>();


            buffer.AddRange(BitConverter.GetBytes(ptype));
            buffer.AddRange(BitConverter.GetBytes(opid));
            buffer.AddRange(BitConverter.GetBytes(xcmd_status));
            buffer.AddRange(BitConverter.GetBytes(xpos));
            buffer.AddRange(BitConverter.GetBytes(ycmd_status));
            buffer.AddRange(BitConverter.GetBytes(ypos));
            buffer.AddRange(BitConverter.GetBytes(zcmd_status));
            buffer.AddRange(BitConverter.GetBytes(zpos));
            buffer.AddRange(BitConverter.GetBytes(rv1));
            buffer.AddRange(BitConverter.GetBytes(rv2));
            buffer.AddRange(BitConverter.GetBytes(rv3));
            buffer.Add(rv4);
            return buffer.ToArray();

        }

        public byte[] getEncodePacket()
        {
            List<byte> encodeuffer = new List<byte>();
            char[] encodedBody = new char[60];
            byte[] bufferBody = serializeData();
            Convert.ToBase64CharArray(bufferBody, 0, bufferBody.Length, encodedBody, 0);
            byte[] encodedBodyBytes = Encoding.UTF8.GetBytes(encodedBody);
            //Console.WriteLine("encode b64: " + encodedBody);
            encodeuffer.Add((byte)'{');
            encodeuffer.AddRange(encodedBodyBytes);
            encodeuffer.Add((byte)'}');


            return encodeuffer.ToArray();

        }

    }


}
