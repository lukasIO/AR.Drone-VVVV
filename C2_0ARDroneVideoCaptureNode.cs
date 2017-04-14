#region usings
using System;
using System.ComponentModel.Composition;
using System.Runtime.InteropServices;

using System.Drawing.Imaging;


using PixelFormat = System.Drawing.Imaging.PixelFormat;
using VideoPixelFormat = AR.Drone.Video.PixelFormat;

using System.Drawing;
using AR.Drone.Video;


using VVVV.Core.Logging;
using VVVV.PluginInterfaces.V1;
using VVVV.PluginInterfaces.V2;
using VVVV.PluginInterfaces.V2.EX9;
using VVVV.Utils.VColor;
using VVVV.Utils.VMath;
using SlimDX;
using VVVV.CV.Core;

#endregion usings


namespace VVVV.Nodes
{
    #region PluginInfo
    [PluginInfo(Name = "VideoCapture", Category = "ARDrone", Version = "2.0", Help = "Basic template which creates a texture", Tags = "")]
    #endregion PluginInfo
    public class C2_0ARDroneVideoCaptureNode : IPluginEvaluate, IPartImportsSatisfiedNotification
    {
        //little helper class used to store information for each
        //texture resource
        public class Info
        {
            public int Slice;
            public int Width;
            public int Height;
            public double WaveCount;
        }

        private int width = 256;
        private int height = 256;
        private Bitmap _frameBitmap;
        private uint _frameNumber;
        private VideoFrame _frame;
        private CVImageLink _videoImage;

        [Input("VideoFrame")]
        public ISpread<VideoFrame> frame;

        [Input("FilterColours")]
        public ISpread<RGBAColor> FColoursIn;

        [Input("Epsilon RGB")]
        public ISpread<Vector3D> FEpsilonRGB;

        [Output("Image Out")]
        public ISpread<CVImageLink> FImageOut;

        [Output("Frame Number")]
        public ISpread<uint> FFrameNumberOut;

        [Output("Bitmaps")]
        public ISpread<Bitmap> FBitmapOut;

        [Import()]
        public ILogger FLogger;

        public void OnImportsSatisfied()
        {
            //spreads have a length of one by default, change it
            //to zero so ResizeAndDispose works properly.
            //FImageOut.SliceCount = 0;
        }

        //called when data for any output pin is requested
        public void Evaluate(int spreadMax)
        {
            _frame = frame[0];

            for (int i = 0; i < spreadMax; i++)
            {
                              
                //recreate textures if resolution was changed

                //update textures if their wave count changed
                if (_frame != null)
                {
                    width = _frame.Width;
                    height = _frame.Height;
                   

                    if (_frame.Number != _frameNumber)
                    {
                        if (_frameBitmap == null || FImageOut[0].ImageAttributes.Width != width)
                        {
                            _frameBitmap = VideoHelper.CreateBitmap(ref _frame);
                            
                            _videoImage = new CVImageLink();
                            _videoImage.Initialise(new CVImageAttributes(TColorFormat.RGB8, width, height));
                            
                            _videoImage.Send(_frameBitmap);
                        }
                        else {
                            VideoHelper.UpdateBitmap(ref _frameBitmap, ref _frame);

                            

                            _videoImage.Send(_frameBitmap);
                        }
                  
                    }
                   
                    _frameNumber = _frame.Number;
                    FFrameNumberOut[0] = _frame.Number;
                    

                }
               
                FImageOut[0] = _videoImage;
                FBitmapOut[0] = _frameBitmap;

            }
        }

    }

    public static class VideoHelper
    {
        public static PixelFormat ConvertPixelFormat(VideoPixelFormat pixelFormat)
        {
            switch (pixelFormat)
            {
                case VideoPixelFormat.Gray8:
                    return PixelFormat.Format8bppIndexed;
                case VideoPixelFormat.BGR24:
                    return PixelFormat.Format24bppRgb;
                case VideoPixelFormat.RGB24:
                    throw new NotSupportedException();
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        public static Bitmap CreateBitmap(ref VideoFrame frame)
        {
            PixelFormat pixelFormat = ConvertPixelFormat(frame.PixelFormat);
            var bitmap = new Bitmap(frame.Width, frame.Height, pixelFormat);
            if (pixelFormat == PixelFormat.Format8bppIndexed)
            {
                ColorPalette palette = bitmap.Palette;
                for (int i = 0; i < palette.Entries.Length; i++)
                {
                    palette.Entries[i] = Color.FromArgb(i, i, i);
                }
                bitmap.Palette = palette;
            }
            UpdateBitmap(ref bitmap, ref frame);
            return bitmap;
        }

        public static void UpdateBitmap(ref Bitmap bitmap, ref VideoFrame frame)
        {
            var rect = new Rectangle(0, 0, bitmap.Width, bitmap.Height);
            BitmapData data = bitmap.LockBits(rect, ImageLockMode.WriteOnly, bitmap.PixelFormat);
            Marshal.Copy(frame.Data, 0, data.Scan0, frame.Data.Length);
            
            bitmap.UnlockBits(data);

        }

        public static Bitmap FilterColour(ref Bitmap bitmap, ISpread<RGBAColor> filtercolours, Vector3D epsilons)
        {
            Bitmap result = new Bitmap(bitmap.Width, bitmap.Height, bitmap.PixelFormat);
            for (int x = 0; x < result.Width; x++)
            {
                for (int y = 0; y < result.Height; y++)
                {
                    foreach (Color colour in filtercolours)
                    {
                        Color pixel = bitmap.GetPixel(x, y);
                        if (Math.Abs(pixel.R - colour.R) < epsilons[0] && Math.Abs(pixel.G - colour.G) < epsilons[1] && Math.Abs(pixel.B - colour.B) < epsilons[2])
                        {
                            pixel = Color.Black;
                        }

                        result.SetPixel(x, y, pixel);
                    }
                    
                }
            }


            return result;
        }
    }
}
