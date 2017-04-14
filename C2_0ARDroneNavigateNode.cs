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
    [PluginInfo(Name = "Navigate", Category = "ARDrone", Version = "2.0", Help = "Navigate ar drone", Tags = "")]
    #endregion PluginInfo
    public class C2_0ARDroneNavigateNode : IPluginEvaluate,
IPartImportsSatisfiedNotification, IDisposable
    {

        

        private DroneClient _droneClient;
       
        private Settings _settings;
        private VideoFrame _frame;
        
        private NavigationData _navigationData;
        private NavigationPacket _navigationPacket;

        private PacketRecorder _packetRecorderWorker;
        
        private Autopilot _autopilot;
        private bool isStarted = false;

        private SlimDX.Vector3 _velocity;
        private bool isInitialized = false;


        #region fields & pins
        [Input("DroneLink")]
        public ISpread<DroneClient> FInput;

        [Input("Hover", IsBang = true)]
        public ISpread<bool> FHoverIn;

        [Input("Absolute Control", DefaultBoolean = false)]
        public ISpread<bool> FAbsoluteControlIn;

        [Input("Calibrate Magnetometer", DefaultBoolean = false)]
        public ISpread<bool> FCalibrateIn;

        [Input("Velocity")]
        public IDiffSpread<Vector3D> FVelocityIn;

        [Output("Altitude")]
        public ISpread<float> FAltitudeOut;

        [Output("Pressure RAW")]
        public ISpread<float> FPressureOut;

        [Import()]
        public ILogger FLogger;
 
        #endregion fields & pins

        public void OnImportsSatisfied()
        {        


        }
        public void Dispose()
        {
            _droneClient.NavigationPacketAcquired -= OnNavigationPacketAcquired;

            _droneClient.NavigationDataAcquired -= data => _navigationData = data;


        }
        //called when data for any output pin is requested
        public void Evaluate(int SpreadMax)
        {
            FAltitudeOut.SliceCount = 1;

            _droneClient = FInput[0];

            if (_droneClient != null && _droneClient.IsConnected && _droneClient.IsActive)
            {
                if (!isInitialized)
                {
                    _droneClient.NavigationPacketAcquired += OnNavigationPacketAcquired;

                    _droneClient.NavigationDataAcquired += data => _navigationData = data;
                    isInitialized = true;
                }

                FAltitudeOut[0] = _navigationData.Altitude;
                NavdataBag navdataBag;
                if (_navigationPacket.Data != null && NavdataBagParser.TryParse(ref _navigationPacket, out navdataBag))
                {
                    FPressureOut[0] = navdataBag.kalman_pressure.cov_alt;
                   
                }

                if (FHoverIn[0])
                    _droneClient.Hover();

                if (FCalibrateIn[0])
                {
                    _droneClient.Send(CalibrateCommand.Magnetometer);
                }

                if (FVelocityIn.IsChanged)
                {
                    if (Math.Abs(FVelocityIn[0].Length) < 0.000001f)
                    {
                        _droneClient.Hover();

                    }
                    else
                    {
                        if (FAbsoluteControlIn[0])
                        {
                            _droneClient.ProgressWithMagneto(FlightMode.Progressive, roll: (float)FVelocityIn[0].x, gaz: (float)FVelocityIn[0].y, pitch: (float)FVelocityIn[0].z);
                        }
                        else
                        {
                            _droneClient.Progress(FlightMode.Progressive, roll: (float)FVelocityIn[0].x, gaz: (float)FVelocityIn[0].y, pitch: (float)FVelocityIn[0].z);
                            FLogger.Log(LogType.Debug, FVelocityIn[0].x + " : " + FVelocityIn[0].y + " : " + FVelocityIn[0].z);
                        }

                    }

                }

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
          
        }


        private void UnhandledException(object sender, Exception exception)
        {
            MessageBox.Show(exception.ToString(), "Unhandled Exception (Ctrl+C)", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }



}
