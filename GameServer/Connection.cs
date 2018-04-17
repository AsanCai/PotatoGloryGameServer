using System;
using System.Net;
using System.Net.Sockets;
using System.Collections;
using System.Collections.Generic;

namespace GameServer {

    public class Connection {
        public const int BUFFER_SIZE = 1024;
        public Socket socket;

        public bool isUse = false;
        public byte[] readBuff = new byte[BUFFER_SIZE];

        public int bufferCount = 0;

        public Connection() {
            readBuff = new byte[BUFFER_SIZE];
        }

        //初始化方法，启用一个连接时会调用该方法
        public void Init(Socket socket) {
            this.socket = socket;

            isUse = true;

            bufferCount = 0;
        }

        //缓冲区剩余字节数
        public int BufferRemain() {
            return BUFFER_SIZE - bufferCount;
        }

        //获取客户端地址
        public string GetAddress() {
            if (!isUse) {
                return "无法获取地址";
            } else {
                return socket.RemoteEndPoint.ToString();
            }
        }

        //关闭该连接
        public void Close() {
            if (!isUse) {
                return;
            }

            Console.WriteLine("【断开连接】" + GetAddress());

            socket.Close();
            isUse = false;
        }
    }

}