﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

using Emgu.CV;
using Emgu.CV.Structure;

using IronOcr;

namespace EmguCV_TextDetection
{
    public partial class Form1 : Form
    {
        private PictureBox leftPicture, rightPicture;
        private OpenFileDialog file;

        IronTesseract Ocr;
        public Form1()
        {
            InitializeComponent();
            // set true, otherwise key press is swallowed by the control that has focus
            this.KeyPreview = true;
            // add KeyEvent to the form
            this.KeyUp += new System.Windows.Forms.KeyEventHandler(KeyEvent);

            leftPicture = new PictureBox();
            rightPicture = new PictureBox();

            leftPicture.SizeMode = PictureBoxSizeMode.AutoSize;
            rightPicture.SizeMode = PictureBoxSizeMode.AutoSize;

            flowLayoutPanel1.AutoScroll = true;
            flowLayoutPanel2.AutoScroll = true;

            flowLayoutPanel1.Controls.Add(leftPicture);
            flowLayoutPanel2.Controls.Add(rightPicture);

            // initialize for IronOCR
            Ocr = new IronTesseract();

            // improve speed
            Ocr.Language = OcrLanguage.ChineseSimplifiedFast;
            // Latest Engine 
            Ocr.Configuration.TesseractVersion = TesseractVersion.Tesseract5;
            //AI OCR only without font analysis
            Ocr.Configuration.EngineMode = TesseractEngineMode.LstmOnly;
            //Turn off unneeded options
            Ocr.Configuration.ReadBarCodes = false;
            Ocr.Configuration.RenderSearchablePdfsAndHocr = false;
            // Assume text is laid out neatly in an orthagonal document
            Ocr.Configuration.PageSegmentationMode = TesseractPageSegmentationMode.SparseTextOsd;
            // TesseractPageSegmentationMode.SparseTextOsd;
        }

        private void openImageToolStripMenuItem_Click(object sender, EventArgs e)
        {
            file = new OpenFileDialog
            {
                Title = "Open Image"
            };
            if (file.ShowDialog() == DialogResult.OK)
            {
                leftPicture.Image = null; // delete the old image
                rightPicture.Image = null; // delete the old image
                System.GC.Collect();
                leftPicture.Image = new Bitmap(file.FileName); // set to the new image

                // enable the options in the MenuStrip
                detectTextToolStripMenuItem.Enabled = true;
                translateTextToolStripMenuItem.Enabled = true;
            }
        }

        private async void detectTextToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Bitmap bm = (Bitmap)(leftPicture.Image);
            await DrawBoundingRectangles_Async(bm.ToImage<Bgr, byte>());
        }

        /**
         * Method to handle various methods from pressing down the key
         */
        private async void KeyEvent(object sender, KeyEventArgs e) //Keyup Event 
        {
            if (e.KeyCode == Keys.F8)
            {
                // detect the text when press F8
                if (detectTextToolStripMenuItem.Enabled)
                {
                    Bitmap bm = (Bitmap)(leftPicture.Image);
                    await DrawBoundingRectangles_Async(bm.ToImage<Bgr, byte>());
                }
            }
            if (e.KeyCode == Keys.F9)
            {
                // translate the text when press F9
                if (translateTextToolStripMenuItem.Enabled)
                {
                    Bitmap bitmap = (Bitmap)(leftPicture.Image);
                    await TranslateText(bitmap.ToImage<Bgr, byte>());
                    //await TranslateText_IronOCR(bitmap);
                }
            }

        }


        /**
         * Algorithm taken from https://www.youtube.com/watch?v=KHes5M7zpGg
         * Detect text in the image and get Bounding Rectangles around it.
         * Return: a list of rectangles (can get (X, Y), width, and height)
         */
        private List<Rectangle> GetBoudingRectangles(Image<Bgr, byte> img)
        {
            /*
             1. Edge detection (sobel)
             2. Dilation (10,1)
             3. FindContours
             4. Geometrical Constrints
             */
            //sobel
            Image<Gray, byte> sobel = img.Convert<Gray, byte>().Sobel(1, 0, 3).AbsDiff(new Gray(0.0)).Convert<Gray, byte>().ThresholdBinary(new Gray(50), new Gray(255));
            Mat SE = CvInvoke.GetStructuringElement(Emgu.CV.CvEnum.ElementShape.Rectangle, new Size(10, 2), new Point(-1, -1));
            sobel = sobel.MorphologyEx(Emgu.CV.CvEnum.MorphOp.Dilate, SE, new Point(-1, -1), 1, Emgu.CV.CvEnum.BorderType.Reflect, new MCvScalar(255));
            Emgu.CV.Util.VectorOfVectorOfPoint contours = new Emgu.CV.Util.VectorOfVectorOfPoint();
            Mat m = new Mat();

            CvInvoke.FindContours(sobel, contours, m, Emgu.CV.CvEnum.RetrType.External, Emgu.CV.CvEnum.ChainApproxMethod.ChainApproxSimple);

            List<Rectangle> list = new List<Rectangle>();

            for (int i = 0; i < contours.Size; i++)
            {
                Rectangle brect = CvInvoke.BoundingRectangle(contours[i]);

                // add more height and width to the rectangle
                var value = 0; // adjustable in the future?!
                brect.X -= value;
                brect.Y -= value;
                brect.Width += value;
                brect.Height += value;

                double ar = brect.Width / brect.Height;
                if (ar > 0.8 && brect.Width > 10 && brect.Height > 10 && brect.Height < 60)
                {
                    list.Add(brect);
                }

            }

            return list; // return the list of Rectangles
        }

        /**
         * Algorithm taken from https://www.youtube.com/watch?v=KHes5M7zpGg
         * Detect text in the image and draw Bounding Rectangles around it.
         */
        private async Task DrawBoundingRectangles_Async(Image<Bgr, byte> img)
        {
            List<Rectangle> currentRectlist = await Task.Run(() => GetBoudingRectangles(img));
            // draw the rectangles
            foreach (var r in currentRectlist)
            {
                CvInvoke.Rectangle(img, r, new MCvScalar(0, 0, 255), 2);
            }

            rightPicture.Image = null; // delete the old image
            System.GC.Collect();
            rightPicture.Image = img.ToBitmap();
        }

        /**
         * 
         */
        private async void translateTextToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Bitmap bitmap = (Bitmap)(leftPicture.Image);
            await TranslateText(bitmap.ToImage<Bgr, byte>());
        }

        /**
         * Detect text in the image and draw Bounding Rectangles around it.
         */
        private async Task TranslateText(Image<Bgr, byte> img)
        {
            List<Rectangle> currentRectList = await Task.Run(() => GetBoudingRectangles(img));
            
            foreach (var brect in currentRectList)
            {
                // check if the area contains text in the rectangle
                using (var Input = new OcrInput())
                {
                    // Input.DeNoise();
                    Input.AddImage(leftPicture.Image, brect);
                    // use the default value (https://ironsoftware.com/csharp/ocr/troubleshooting/x-and-y-coordinates-change/)
                    Input.MinimumDPI = null;
                    // Input.ToGrayScale();

                    var Result = await Task.Run(() => Ocr.Read(Input));
                    String LineText = Encoding.UTF8.GetString(Encoding.Default.GetBytes(Result.Text));
                    Boolean containsText = !string.IsNullOrEmpty(LineText);

                    StringFormat strFormat = new StringFormat();
                    strFormat.Alignment = StringAlignment.Center;
                    strFormat.LineAlignment = StringAlignment.Center;
                    strFormat.FormatFlags = StringFormatFlags.NoFontFallback;

                    if (containsText)
                    {
                        // draw the rectangles
                        CvInvoke.Rectangle(img, brect, new MCvScalar(220, 220, 220), -1);
                        // draw the text
                        // CvInvoke.PutText(img, "Hello World", new Point(Result.Lines[0].X, Result.Lines[0].Y), Emgu.CV.CvEnum.FontFace.HersheySimplex, 1, new Bgr(Color.Black).MCvScalar);
                        using (Graphics g = Graphics.FromImage(img.AsBitmap()))
                        {
                            
                            g.DrawString(LineText, new Font("Times New Roman", 11), Brushes.Black, new RectangleF(brect.X, brect.Y, brect.Width, brect.Height), strFormat);
                        }
                    }
                }
            }
            rightPicture.Image = null; // delete the old image
            System.GC.Collect();
            rightPicture.Image = img.ToBitmap();
        }

        private async Task TranslateText_IronOCR(Bitmap bitmap)
        {
            // Image<Bgr, byte> img = bitmap.ToImage<Bgr, byte>();
            
            using (var Input = new OcrInput(bitmap))
            {
                Input.TargetDPI = 300;

                var Result = await Task.Run(() => Ocr.Read(Input));
                Image<Bgr, byte> img = Result.Pages[0].ContentAreaToBitmap(Input).ToImage<Bgr, byte>();

                foreach (var Line in Result.Lines)
                {
                    // only draw if the confidence is higher than 25%
                    if (Line.Confidence > 0 && !string.IsNullOrEmpty(Line.Text))
                    {
                        String LineText = Encoding.UTF8.GetString(Encoding.Default.GetBytes(Line.Text));
                        int LineX_location = Line.X;
                        int LineY_location = Line.Y;
                        int LineWidth = Line.Width;
                        int LineHeight = Line.Height;
                        double LineOcrAccuracy = Line.Confidence;

                        Console.WriteLine("LineText: {0}\nX: {1}, Y: {2}\nWidth: {3}, Height: {4}, Confidence: {5}"
                            , LineText, LineX_location, LineY_location, LineWidth, LineHeight, LineOcrAccuracy);

                        Rectangle rect = new Rectangle(LineX_location, LineY_location, LineWidth, LineHeight);

                        CvInvoke.Rectangle(img, rect, new MCvScalar(220, 220, 220), -1);
                    }
                    
                }

                rightPicture.Image = null; // delete the old image
                System.GC.Collect();
                Bitmap resized = new Bitmap(img.ToBitmap(), leftPicture.Size);
                rightPicture.Image = resized;
                // CvInvoke.Rectangle(img, brect, new MCvScalar(50, 50, 50), -1);
            }
        }
    }
}
