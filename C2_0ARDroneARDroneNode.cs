#region usings
using System;
using System.ComponentModel.Composition;

using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using AR.Drone.Client;
using AR.Drone.Client.Command;
using AR.Drone.Client.Configuration;
using AR.Drone.Data;
using AR.Drone.Data.Navigation;
using AR.Drone.Data.Navigation.Native;
using AR.Drone.Media;
using AR.Drone.Video;
using AR.Drone.Avionics;
using AR.Drone.Avionics.Objectives;
using AR.Drone.Avionics.Objectives.IntentObtainers;

using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using PixelFormat = System.Drawing.Imaging.PixelFormat;
using VideoPixelFormat = AR.Drone.Video.PixelFormat;

using VVVV.PluginInterfaces.V1;
using VVVV.PluginInterfaces.V2;
using VVVV.Utils.VColor;
using VVVV.Utils.VMath;

using VVVV.Core.Logging;
#endregion usings

namespace VVVV.Nodes
{
	#region PluginInfo
	[PluginInfo(Name = "ARDrone", Category = "ARDRone", Version = "2.0", Help = "", Tags = "")]
	#endregion PluginInfo
	public class C2_0ARDroneARDroneNode : IPluginEvaluate, 
IPartImportsSatisfiedNotification, IDisposable
	{
		
		private const string ARDroneTrackFileExt = ".ardrone";
        private const string ARDroneTrackFilesFilter = "AR.Drone track files (*.ardrone)|*.ardrone";

        private DroneClient _droneClient;
        private VideoPacketDecoderWorker _videoPacketDecoderWorker;
        private Settings _settings;
        private VideoFrame _frame;
        private Bitmap _frameBitmap;
        private uint _frameNumber;
        private NavigationData _navigationData;
        private NavigationPacket _navigationPacket;
        private PacketRecorder _packetRecorderWorker;
        private FileStream _recorderStream;
        private Autopilot _autopilot;
		private bool isStarted = false;
		
		
		#region fields & pins
		[Input("Input")]
		public ISpread<bool> FInput;

        [Input("Camera")]
        public IDiffSpread<int> FCamIdIn;

        [Input("Take Off",IsBang =true)]
        public ISpread<bool> FTakeoffIn;
        [Input("Land", IsBang = true)]
        public ISpread<bool> FLandIn;
        [Input("Flattrim", IsBang = true)]
        public ISpread<bool> FFlattrimIn;
        [Input("Reset", IsBang = true)]
        public ISpread<bool> FResetIn;

        [Output("VideoFrame")]
		public ISpread<VideoFrame> FOutput;

        [Output("DroneLink")]
        public ISpread<DroneClient> FDroneOut;

        [Output("IsConnected")]
        public ISpread<bool> FIsConnectedOut;

        [Output("IsActive")]
        public ISpread<bool> FIsActiveOut;

        [Import()]
		public ILogger FLogger;
		#endregion fields & pins
		
		public void OnImportsSatisfied()
		{
			_videoPacketDecoderWorker = new VideoPacketDecoderWorker(VideoPixelFormat.BGR24, true, OnVideoPacketDecoded);

            _videoPacketDecoderWorker.Start();

            _droneClient = new DroneClient("192.168.1.1");
            _droneClient.NavigationPacketAcquired += OnNavigationPacketAcquired;
            _droneClient.VideoPacketAcquired += OnVideoPacketAcquired;
            _droneClient.NavigationDataAcquired += data => _navigationData = data;

            _videoPacketDecoderWorker.UnhandledException += UnhandledException;
		}
		public void Dispose()
		{
			_droneClient.Stop();
			
			if (_autopilot != null)
            {
                _autopilot.UnbindFromClient();
                _autopilot.Stop();
            }

            //StopRecording();

            _droneClient.Dispose();
            _videoPacketDecoderWorker.Dispose();
			
			isStarted = false;
			
		}
		//called when data for any output pin is requested
		public void Evaluate(int SpreadMax)
		{
			
			if(FInput[0] == true && !isStarted)
			{
				_droneClient.Start();
				isStarted = true;
                var config = new Settings();
                config.Video.Channel = (VideoChannelType)(FCamIdIn[0]);
                _droneClient.Send(config);
            }
			else if(FInput[0] == false && isStarted)
			{
				_droneClient.Stop();
				isStarted = false;
			}

            if (FCamIdIn.IsChanged)
            {
                var configuration = new Settings();
                configuration.Video.Channel = (VideoChannelType)(FCamIdIn[0]);
                _droneClient.Send(configuration);
            }
            

            FOutput.SliceCount = SpreadMax;

            if(isStarted)
            {
                if(FFlattrimIn[0])
                {
                    _droneClient.FlatTrim();
                }

                if (FTakeoffIn[0])
                {
                    _droneClient.Takeoff();
                }

                if(FLandIn[0])
                {
                    _droneClient.Land();
                }

                if (FResetIn[0])
                {
                    _droneClient.ResetEmergency();
                }


            }


            FIsConnectedOut[0] = _droneClient.IsConnected;
            FIsActiveOut[0] = _droneClient.IsActive;
            if (_droneClient.IsConnected)
            {
                FDroneOut[0] = _droneClient;
                FOutput[0] = _frame;
            }


        }
		
		
		
		private void OnNavigationPacketAcquired(NavigationPacket packet)
        {
            if (_packetRecorderWorker != null && _packetRecorderWorker.IsAlive)
                _packetRecorderWorker.EnqueuePacket(packet);

            _navigationPacket = packet;
        }

        private void OnVideoPacketAcquired(VideoPacket packet)
        {
            if (_packetRecorderWorker != null && _packetRecorderWorker.IsAlive)
                _packetRecorderWorker.EnqueuePacket(packet);
            if (_videoPacketDecoderWorker.IsAlive)
                _videoPacketDecoderWorker.EnqueuePacket(packet);
        }

        private void OnVideoPacketDecoded(VideoFrame frame)
        {
            _frame = frame;
        }
		
		 private void UnhandledException(object sender, Exception exception)
        {
            MessageBox.Show(exception.ToString(), "Unhandled Exception (Ctrl+C)", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
	}
	
	
	 
}
