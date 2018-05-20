using System;
using System.Net;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using System.Collections;
using System.Collections.Generic;

using MySql.Data;
using MySql.Data.MySqlClient;
using System.Data;

namespace GameServer {
    public class ServerManager {
        public Socket listenfd;
        public Connection[] conns;

        public int maxConn = 50;

        MySqlConnection sqlConn;
        private string fail = "FAIL";
        private string success = "SUCCESS";
        private string repeat = "REPEAT";

        //创建主计时器，每秒执行一次
        private System.Timers.Timer timer = new System.Timers.Timer(1000);
        //心跳检查时间间隔
        public long heartBeatTime = 20;


        public int NewIndex() {
            if(conns == null) {
                return -1;
            }

            for(int i = 0; i < conns.Length; i++) {
                if(conns[i] == null) {
                    conns[i] = new Connection();
                    return i;
                } else {
                    if(conns[i].isUse == false) {
                        return i;
                    }
                }
            }

            //没有可用的连接
            return -1;
        }

        //启用连接
        public void Start(string host, int port) {
            string connStr = "Database=potatoglory;Data Source=127.0.0.1;";
            connStr += "User Id=root;Password=software@#2018;port=3306";

            sqlConn = new MySqlConnection(connStr);

            try {
                sqlConn.Open();
            }catch(Exception e) {
                Console.WriteLine("【数据库】连接失败" + e.Message);
                return;
            }

            conns = new Connection[maxConn];

            for(int i = 0; i < maxConn; i++) {
                conns[i] = new Connection();
            }

            listenfd = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

            IPAddress ipAdr = IPAddress.Parse(host);

            IPEndPoint ipEp = new IPEndPoint(ipAdr, port);

            //开启监听端口
            listenfd.Bind(ipEp);
            listenfd.Listen(maxConn);

            //开启接收回调函数
            listenfd.BeginAccept(Acceptcb, null);


            //启动心跳机制
            timer.Elapsed += new System.Timers.ElapsedEventHandler(HandleMainTimer);
            timer.AutoReset = false;
            timer.Enabled = true;

            Console.WriteLine("【服务器】启动成功");
        }

        public void HandleMainTimer(object sender, System.Timers.ElapsedEventArgs e) {
            HeartBeat();
            timer.Start();
        }

        public void HeartBeat() {
            long timeNow = Sys.GetTimeStamp();

            for(int i = 0; i < conns.Length; i++) {
                Connection conn = conns[i];

                if(conn == null) {
                    continue;
                }

                if (!conn.isUse) {
                    continue;
                }

                if(conn.lastTickTime < timeNow - heartBeatTime) {
                    Console.WriteLine("【心跳引起断开连接】" + conn.GetAddress());

                    lock (conn) {
                        conn.Close();
                    }
                }
            }
        }

        private void Acceptcb(IAsyncResult ar) {
            try {
                Socket socket = listenfd.EndAccept(ar);

                int index = NewIndex();

                if(index < 0) {
                    socket.Close();

                    Console.WriteLine("【警告】连接已满");
                } else {
                    Connection conn = conns[index];

                    conn.Init(socket);

                    string adr = conn.GetAddress();

                    Console.WriteLine("客户端连接 [" + adr + "] conn池ID: " + index);

                    //为什么最后一个参数可以是Connection类型的？
                    conn.socket.BeginReceive(
                        conn.readBuff, 
                        conn.bufferCount, 
                        conn.BufferRemain(),
                        SocketFlags.None,
                        Receivecb,
                        conn);
                }
            } catch(Exception e) {
                Console.WriteLine("Acceptcb失败: " + e.Message);
            }

            //确保能不断循环接收信息
            listenfd.BeginAccept(Acceptcb, null);
        }

        private void Receivecb(IAsyncResult ar) {
            Connection conn = (Connection)ar.AsyncState;

            try {
                int count = conn.socket.EndReceive(ar);

                if (count <= 0) {
                    Console.WriteLine("收到 [" + conn.GetAddress() + "] 断开连接");

                    conn.Close();
                } else {
                    string str = System.Text.Encoding.UTF8.GetString(conn.readBuff, 0, count);

                    Console.WriteLine("收到 [" + conn.GetAddress() + "] 数据: " + str);

                    //处理接收到的数据
                    HandleMsg(conn, str);

                    //继续开始新的接收
                    conn.socket.BeginReceive(
                        conn.readBuff,
                        conn.bufferCount,
                        conn.BufferRemain(),
                        SocketFlags.None,
                        Receivecb,
                        conn
                        );
                }

            } catch (Exception e) {
                Console.WriteLine("收到 [" + conn.GetAddress() + "] 断开连接");
                conn.Close();
            }
        }


        public void HandleMsg(Connection conn, string str) {
            //如果是心跳协议，那么更新心跳时间戳
            if(str == "HeartBeat") {
                conn.lastTickTime = Sys.GetTimeStamp();
                return;
            }


            string[] args = str.Split(' ');
            string resultStr = "";

            if(args[0] == "LOGIN") {
                bool result = CheckPassword(args[1], args[2]);

                if (result) {
                    resultStr = args[0] + " " + success;
                } else {
                    resultStr = args[0] + " " + fail;
                }
            } else if(args[0] == "REGISTER") {
                if (CanRegister(args[1])) {
                    bool result = Register(args[1], args[2]);

                    if (result) {
                        resultStr = args[0] + " " + success;
                    } else {
                        resultStr = args[0] + " " + fail;
                    }
                } else {
                    resultStr = args[0] + " " + repeat;
                }
            } else {
                Console.WriteLine("【服务器】未知协议");
                return;
            }

            conn.socket.Send(System.Text.Encoding.Default.GetBytes(resultStr));
        }


        private bool CanRegister(string username) {
            string cmdStr = string.Format("select * from user where username='{0}';", username);
            MySqlCommand cmd = new MySqlCommand(cmdStr, sqlConn);

            try {
                MySqlDataReader dataReader = cmd.ExecuteReader();

                bool hasRows = dataReader.HasRows;

                dataReader.Close();

                Console.WriteLine("【服务器】" + username + "已存在");
                return !hasRows;
            } catch(Exception e) {
                Console.WriteLine("【服务器】CanRegister fail " + e.Message);
                return false;
            }
        }

        private bool Register(string username, string password) {
            string cmdStr = string.Format(
                "insert into user set username='{0}', password='{1}';",
                username, 
                password
                );

            MySqlCommand cmd = new MySqlCommand(cmdStr, sqlConn);

            try {
                cmd.ExecuteNonQuery();
                return true;
            } catch(Exception e) {
                Console.WriteLine("【服务器】Register " + e.Message);
                return false;
            }
        }

        private bool CheckPassword(string username, string password) {
            string cmdStr = string.Format(
                "select * from user where username='{0}' and password='{1}';",
                username,
                password);

            MySqlCommand cmd = new MySqlCommand(cmdStr, sqlConn);
            try {
                MySqlDataReader dataReader = cmd.ExecuteReader();

                bool hasRows = dataReader.HasRows;
                dataReader.Close();
                return hasRows;
            } catch(Exception e) {
                Console.WriteLine("【服务器】CheckPassword " + e.Message);
                return false;
            }
        }
    }
}
