﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace TCPSharpFileSync
{
    public struct TCPSettings 
    {
        public string pathToSyncDir;
        public string ip;
        public int port;
        public bool doDownload;
        public bool doUpload;
        public int timeouter;
        public bool rmIfnDefOnClient;
        public bool rmIfnDefOnServer;

        public TCPSettings(string ptd, string i, int p, int msToTimeout) 
        {
            pathToSyncDir = ptd;
            ip = i;
            port = p;
            doDownload = false;
            doUpload = false;
            timeouter = msToTimeout;
            rmIfnDefOnClient = false;
            rmIfnDefOnServer = false;
        }
        public TCPSettings(string ptd, string i, int p, bool dd, bool du, bool rmifndefc, bool rmifndefs, int msToTimeout)
        {
            pathToSyncDir = ptd;
            ip = i;
            port = p;
            doDownload = dd;
            doUpload = du;
            timeouter = msToTimeout;
            rmIfnDefOnClient = rmifndefc;
            rmIfnDefOnServer = rmifndefs;
        }
    }

    public abstract class TCPFileWorker
    {
        protected Filer filer { get; set; }
        protected Hasher hasher { get; set; }

        protected TCPSettings ts;

        protected bool gettingFile = false;

        public long bufferSize = 65556;
        public static int msBeforeTimeOut = 5000;

        protected void FileScan(string pathToDir)
        {
            filer = new Filer(pathToDir);
            hasher = new Hasher(filer.GetLocalFiles());
        }

        protected string GetStringFromBytes(byte[] b) 
        {
            return System.Text.Encoding.Unicode.GetString(b);
        }

        public static string GetLocalIPAddress()
        {
            var host = Dns.GetHostEntry(Dns.GetHostName());
            foreach (var ip in host.AddressList)
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork)
                {
                    return ip.ToString();
                }
            }
            throw new Exception("No network adapters with an IPv4 address in the system!");
        }
    }
}