﻿using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using WatsonTcp;

namespace TCPSharpFileSync
{
    public class Server : TCPFileWorker
    {
        /// <summary>
        /// WatsonTcpServer class for wrap TCP works.
        /// </summary>
        WatsonTcpServer servH;
        /// <summary>
        /// List of all clients.
        /// </summary>
        List<string> clients;
        /// <summary>
        /// Variable that represents a Local path that's the current file is downloading to.
        /// </summary>
        string DownloadFileTo;

        /// <summary>
        /// Constructor that initializes Server object based on given TCPSettings.
        /// </summary>
        /// <param name="c">TCPSettings for server work.</param>
        public Server(TCPSettings c)
        {
            ts = c;
            msBeforeTimeOut = ts.msTimeout;
            servH = new WatsonTcpServer(ts.ip, ts.port);
            servH.Events.ClientConnected += ClientConnected;
            servH.Events.ClientDisconnected += ClientDisconnected;
            servH.Events.StreamReceived += StreamReceived;
            servH.Callbacks.SyncRequestReceived += SyncSolver;

            servH.Keepalive.EnableTcpKeepAlives = true;
            servH.Keepalive.TcpKeepAliveInterval = 10;
            servH.Keepalive.TcpKeepAliveTime = msBeforeTimeOut;
            servH.Keepalive.TcpKeepAliveRetryCount = 10;

            FileScan(ts.directoryPath);
            servH.Start();
            LogHandler.WriteLog($"Server started!", Color.Green);
        }

        /// <summary>
        /// Event handler for SyncRequest being received by server.
        /// </summary>
        /// <param name="arg">The SyncRequest that recieved.</param>
        /// <returns></returns>
        //======Client requests======
        //!qq = Exit
        //!getHash *relative path to file* = get file hash to client
        //!getFile *relative path to file* = upload file to client
        //!catchFile *relative path to file* = get file from client
        //!exists *relative path to file* = check if file exists on servers side and then answer for client
        //!getFileList = get all relative pathes and send it to client
        //!sessiondone = updates servers Filer and Hasher
        //!rm *relative path to file* = remove file on server side
        //!getFileInfo *relative path to file* = get file info from server

        //======Server Respones======
        //!dd = ready for next operation
        //!Yes = answer for file existance if it does exist
        //!No = answer for file existance if it does not exist
        private SyncResponse SyncSolver(SyncRequest arg)
        {
            servH.Events.StreamReceived -= StreamReceived;
            string cmd = GetStringFromBytes(arg.Data);
            SyncResponse sr = new SyncResponse(arg, GetBytesFromString("NotRecognized"));
            if (cmd.Contains("!qq"))
            {
                servH.DisconnectClients();
                servH.Stop();
                servH.Dispose();
            }
            else if (cmd.Contains("!getHashes "))
            {
                cmd = cmd.Replace("!getHashes ", "");
                string hashes = GetAllAskedHashesToSeparatedString(cmd);
                sr = new SyncResponse(arg, GetBytesFromString(hashes));
            }
            else if (cmd.Contains("!getFile "))
            {
                cmd = cmd.Replace("!getFile ", "");
                UploadFile(arg.IpPort, cmd);
                sr = new SyncResponse(arg, GetBytesFromString("!dd"));
            }
            else if (cmd.Contains("!catchFile "))
            {
                while (gettingFile)
                {

                }

                DownloadFileTo = Filed.RootPath + cmd.Replace("!catchFile ", "");
                sr = new SyncResponse(arg, GetBytesFromString("!dd"));
                servH.Events.StreamReceived += StreamReceived;
            }
            else if (cmd.Contains("!exists "))
            {
                cmd = cmd.Replace("!exists ", "");
                if (Filed.CheckFileExistanceFromRelative(cmd))
                    sr = new SyncResponse(arg, GetBytesFromString("!Yes"));
                else
                    sr = new SyncResponse(arg, GetBytesFromString("!No"));
            }
            else if (cmd.Contains("!getFileList"))
            {
                cmd = cmd.Replace("!getFileList", "");
                sr = new SyncResponse(arg, GetBytesFromString(GetFileList()));
            }
            else if (cmd.Contains("!sessiondone"))
            {
                cmd = cmd.Replace("!sessiondone", "");
                Filed = new Filer(Filed.RootPath);
                Hashed.UpdateHasherBasedOnUpdatedFiler(Filed);
                sr = new SyncResponse(arg, GetBytesFromString("!dd"));
                LogHandler.WriteLog("Session done!", Color.Green);
            }
            else if (cmd.Contains("!rm "))
            {
                cmd = cmd.Replace("!rm ", "");
                File.Delete(Filed.GetLocalFromRelative(cmd));
                sr = new SyncResponse(arg, GetBytesFromString("!dd"));
            }
            else if (cmd.Contains("!getFileInfo "))
            {
                cmd = cmd.Replace("!getFileInfo ", "");
                FileInfo fi = Filed.GetLocalFileInfoFromRelative(cmd);
                sr = new SyncResponse(arg, GetBytesFromString($"{fi.Length}\n{fi.LastAccessTime.ToString()}"));
            }
            return sr;
        }

        /// <summary>
        /// Event handler for receiving stream from client. Currently made for downloading files.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        private void StreamReceived(object sender, StreamReceivedFromClientEventArgs args)
        {
            gettingFile = true;
            long bytesRemaining = args.ContentLength;
            int bytesRead = 0;
            byte[] buffer = new byte[bufferSize];

            Directory.CreateDirectory(Path.GetDirectoryName(DownloadFileTo));

            using (FileStream fs = new FileStream(DownloadFileTo, FileMode.CreateNew))
            {
                while (bytesRemaining > 0)
                {
                    bytesRead = args.DataStream.Read(buffer, 0, buffer.Length);
                    if (bytesRead > 0)
                    {
                        fs.Write(buffer, 0, bytesRead);
                        bytesRemaining -= bytesRead;
                    }
                }
            }

            LogHandler.WriteLog($"Downloaded {DownloadFileTo.Replace(Filed.RootPath, "")}", Color.Green);

            DownloadFileTo = "";
            servH.Events.StreamReceived -= StreamReceived;
            gettingFile = false;
        }

        /// <summary>
        /// Function made for collecting all requested by client hashed and made them into a string with delimiter.
        /// </summary>
        /// <param name="requested">String that contains Relative pathes to get hashes of.</param>
        /// <returns>String with delimited full of hashes that were asked.</returns>
        private string GetAllAskedHashesToSeparatedString(string requested)
        {
            string response = "";
            List<string> toBeProcessed = requested.Split(new string[] { "?" }, StringSplitOptions.RemoveEmptyEntries).ToList();
            List<string> recievedHashes = new List<string>();

            foreach (var item in toBeProcessed)
            {
                if (File.Exists(Filed.RootPath + item))
                {
                    recievedHashes.Add(Hashed.GetHashMD5FromLocal(Filed.RootPath + item));
                }
                else
                    recievedHashes.Add("-");
            }

            response = string.Join("?", recievedHashes.ToArray());

            return response;
        }

        /// <summary>
        /// Function that uploads file to a client based on Relative path and IP:Port that it has to upload to.
        /// </summary>
        /// <param name="IpPost">IP:Port that it has to upload to.</param>
        /// <param name="rel"> Relative path of uploading file.</param>
        public void UploadFile(string IpPost, string rel)
        {
            string loc = Filed.GetLocalFromRelative(rel);

            using (FileStream fs = new FileStream(loc, FileMode.Open))
            {
                servH.Send(IpPost, fs.Length, fs);
            }
            LogHandler.WriteLog($"Uploaded {rel}", Color.Green);
        }
        private void ClientDisconnected(object sender, ClientDisconnectedEventArgs e)
        {
            clients = servH.ListClients().ToList();
            LogHandler.WriteLog(e.IpPort + $" disconnected! (Reason: {e.Reason})", Color.Red);
        }

        private void ClientConnected(object sender, ClientConnectedEventArgs e)
        {
            clients = servH.ListClients().ToList();
            LogHandler.WriteLog($"{e.IpPort} connected!", Color.Green);
        }

        /// <summary>
        /// Function that makes a string full of Relative pathes of existing files on this device. 
        /// </summary>
        /// <returns>String full of Relative pathes of existing files on this device.</returns>
        private string GetFileList()
        {
            var fileList = Filed.RelativeFilePathes;
            string sendString = "";

            for (int i = 0; i < fileList.Count; i++)
            {
                sendString += fileList[i];

                if (i != fileList.Count - 1)
                    sendString += "\n";
            }

            return sendString;
        }
    }
}