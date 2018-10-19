﻿// Machina.FFXIV ~ FFXIVNetworkMonitor.cs
// 
// Copyright © 2017 Ravahn - All Rights Reserved
// 
//This program is free software: you can redistribute it and/or modify
//it under the terms of the GNU General Public License as published by
//the Free Software Foundation, either version 3 of the License, or
//(at your option) any later version.

//This program is distributed in the hope that it will be useful,
//but WITHOUT ANY WARRANTY; without even the implied warranty of
//MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.See the
//GNU General Public License for more details.

//You should have received a copy of the GNU General Public License
//along with this program.If not, see<http://www.gnu.org/licenses/>.

using System;
using System.Collections.Generic;

namespace Machina.FFXIV
{
    /// <summary>
    /// FFXIVNetworkMonitor is configured through the following properties after it is constructed:
    ///   MonitorType: Specifies whether it should use a winsock raw socket, or use WinPCap (requires separate kernel driver installation).  Default is a raw socket.
    ///   ProcessID: (optional) Specifies the process ID to record traffic from
    ///     
    /// This class uses the Machina.TCPNetworkMonitor class to find and monitor the communication from Final Fantasy XIV.  It decodes the data thaat adheres to the
    ///   FFXIV network packet format and calls the message delegate when data is received.
    /// </summary>
    public class FFXIVNetworkMonitor
    {
        public enum ConnectionType
        {
            Game,
            Lobby
        }

        /// <summary>
        /// Specifies the type of monitor to use - Raw socket or WinPCap
        /// </summary>
        public TCPNetworkMonitor.NetworkMonitorType MonitorType
        { get; set; } = TCPNetworkMonitor.NetworkMonitorType.RawSocket;

        /// <summary>
        /// Specifies the Process ID that is generating or receiving the traffic.  Either ProcessID or WindowName must be specified.
        /// </summary>
        public uint ProcessID
        { get; set; } = 0;

        /// <summary>
        /// Specifies the local IP address to override the detected IP
        /// </summary>
        public string LocalIP
        { get; set; } = "";

        #region Message Delegates section
        public delegate void MessageReceivedDelegate(long epoch, byte[] message, int set, ConnectionType connectionType);

        /// <summary>
        /// Specifies the delegate that is called when data is received and successfully decoded/
        /// </summary>
        public MessageReceivedDelegate MessageReceived = null;

        public void OnMessageReceived(long epoch, byte[] message, int set, ConnectionType connectionType)
        {
            MessageReceived?.Invoke(epoch, message, set, connectionType);
        }
        
        public delegate void MessageSentDelegate(long epoch, byte[] message, int set, ConnectionType connectionType);

        public MessageSentDelegate MessageSent = null;

        public void OnMessageSent(long epoch, byte[] message, int set, ConnectionType connectionType)
        {
            MessageSent?.Invoke(epoch, message, set, connectionType);
        }

        #endregion

        private TCPNetworkMonitor _monitor = null;
        private Dictionary<string, FFXIVBundleDecoder> _sentDecoders = new Dictionary<string, FFXIVBundleDecoder>();
        private Dictionary<string, FFXIVBundleDecoder> _receivedDecoders = new Dictionary<string, FFXIVBundleDecoder>();

        /// <summary>
        /// Validates the parameters and starts the monitor.
        /// </summary>
        public void Start()
        {
            if (_monitor != null)
            {
                _monitor.Stop();
                _monitor = null;
            }

            if (MessageReceived == null)
                throw new ArgumentException("MessageReceived delegate must be specified.");

            _monitor = new TCPNetworkMonitor();
            _monitor.ProcessID = ProcessID;
            if (_monitor.ProcessID == 0)
                _monitor.WindowName = "FINAL FANTASY XIV";
            _monitor.MonitorType = MonitorType;
            _monitor.LocalIP = LocalIP;

            _monitor.DataSent = ProcessSentMessage;
            _monitor.DataReceived = ProcessReceivedMessage;

            _monitor.Start();
        }

        /// <summary>
        /// Stops the monitor if it is active.
        /// </summary>
        public void Stop()
        {
            _monitor?.Stop();
            _monitor = null;

            _sentDecoders.Clear();
            _receivedDecoders.Clear();
        }

        public void ProcessSentMessage(string connection, ushort remotePort, byte[] data)
        {
            try
            {
                Tuple<long, byte[], int> message;
                if (!_sentDecoders.ContainsKey(connection))
                    _sentDecoders.Add(connection, new FFXIVBundleDecoder());

                _sentDecoders[connection].StoreData(data);

                ConnectionType connType = ConnectionType.Game;
                if (remotePort == 54994)
                    connType = ConnectionType.Lobby;
                    
                while ((message = _sentDecoders[connection].GetNextFFXIVMessage()) != null)
                {
                    OnMessageSent(message.Item1, message.Item2, message.Item3, connType);
                }
            }
            catch (KeyNotFoundException)
            {
            }
        }

        public void ProcessReceivedMessage(string connection, ushort remotePort, byte[] data)
        {
            try
            {
                Tuple<long, byte[], int> message;
                if (!_receivedDecoders.ContainsKey(connection))
                    _receivedDecoders.Add(connection, new FFXIVBundleDecoder());

                _receivedDecoders[connection].StoreData(data);
                
                ConnectionType connType = ConnectionType.Game;
                if (remotePort == 54994)
                    connType = ConnectionType.Lobby;
                
                while ((message = _receivedDecoders[connection].GetNextFFXIVMessage()) != null)
                {
                    OnMessageReceived(message.Item1, message.Item2, message.Item3, connType);
                }
            }
            catch (KeyNotFoundException)
            {
            }
        }
    }
}
