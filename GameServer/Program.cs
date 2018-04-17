﻿using System;
using System.Net;
using System.Net.Sockets;

namespace GameServer{
    class Program{
        static void Main(string[] args){
            Console.WriteLine("Hello World!");
            ServerManager server = new ServerManager();

            server.Start("0.0.0.0", 8080);

            while (true) {
                string str = Console.ReadLine();

                switch (str) {
                    case "quit":
                        return;
                }
            }
        }
    }
}
