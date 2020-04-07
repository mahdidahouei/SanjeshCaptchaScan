using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Net;
using System.IO;
using Tesseract;

namespace SanjeshCaptchaScan
{

    public partial class Form1 : Form
    {
        private Point MouseLocation;
        private Point MouseLocation1;
        private Point MouseLocation2;
        private string siteUrl = "http://92.242.195.165/ci_sherkat/index.php/karname_nah/m13";
        private string imageURL;

        public Form1()
        {
            InitializeComponent();
        }

        private void Form1_Load(object sender, EventArgs e) { webBrowser1.Navigate(siteUrl); } //open the site

        private async void WebBrowser1_DocumentCompleted(object sender, WebBrowserDocumentCompletedEventArgs e)
        {
            Bitmap image;
            try
            {
                this.imageURL = webBrowser1.Document.GetElementsByTagName("img")[0].GetAttribute("src");
                Task<Bitmap> task = new Task<Bitmap>(DownloadImage);
                task.Start();
                image = await task; //download image in other thread
                if (image == null) throw new Exception("image is null"); //if there was a problem downloading the image, throw an exception
            }
            catch
            {
                MessageBox.Show("Connection error");
                return; 
            }

            Bitmap middleImage = new Bitmap(image, image.Width, image.Height); //make a new bitmap 

            int R = 105, G = 126, B = 177; // this color is a little bit darker than the background of our image.. so we can work with the pixels that are in background or not!

            int left = middleImage.Width, top = middleImage.Height, right = 0, bottom = 0; // we're ganna calculate the height and width of the text in the picture so we need to find top, bottom, left, and right pixel of the text

            /*
             * loop through all of the pixels in bitmap
             */
            for (int i = 0; i < image.Width; i++)
            {
                for(int j = 0; j < image.Height; j++)
                {
                    if(Convert.ToInt64(image.GetPixel(i, j).ToArgb()) < Convert.ToInt64(Color.FromArgb(R, G, B).ToArgb())) // if this condition is true the pixel is in text
                    { // findind left, top, right, and bottom of the text
                        if (i < left) left = i - 1;
                        if (j <= top) top = j - 1;
                        if (i >= right) right = i + 1;
                        if (j > bottom) bottom = j + 1;
                    }
                }
            }

            Bitmap finalImage = new Bitmap(middleImage.Width, middleImage.Height); //make a new bitmap for pictureBox2

            int textWidth = right - left, textHeight = bottom - top; // now we calculate the width and height of the text

            int finalLeft = (middleImage.Width - textWidth) / 2, finalTop = (middleImage.Height - textHeight) / 2; // if the distance of the text from the top and left becomes like finalTop and finalLeft so the text will be in the center of the image

            int leftDifference = finalLeft - left, topDifference = finalTop - top; // we need to add topDifference and LeftDifference to current top and left, so the text will be in the center of image

            int maxLeftSquare = 0, bottomLeftX = 0, bottomLeftY = 0, maxRightSquare = 0, bottomRightX = 0, bottomRightY = 0; // we're ganna find two pixels! one is the most left and bottommost pixel of the text, the other is the most right and bottommost pixel of the text

            for (int i = 0; i < image.Width; i++)
            {
                for (int j = 0; j < image.Height; j++)
                {
                    if (Convert.ToInt64(image.GetPixel(i, j).ToArgb()) < Convert.ToInt64(Color.FromArgb(R, G, B).ToArgb()))
                    {
                        int x = i + leftDifference;
                        int y = j + topDifference;

                        finalImage.SetPixel(x, y, middleImage.GetPixel(i, j));// draw the text in the center of finalImage

                        int leftArea = i * j, rightArea = (finalImage.Width - i) * j; 

                        if (leftArea > maxLeftSquare)
                        { // finding bottomLeft pixel
                            maxLeftSquare = leftArea;
                            bottomLeftX = x;
                            bottomLeftY = y;
                        }
                        if (rightArea > maxRightSquare)
                        { // finding bottomRight pixel
                            maxRightSquare = rightArea;
                            bottomRightX = x;
                            bottomRightY = y;
                        }
                    }
                }
            }

            //finalImage.SetPixel(bottomLeftX, bottomLeftY, Color.Red); //bottom left pixel of captcha
            //finalImage.SetPixel(bottomRightX, bottomRightY, Color.Red); //bottom right pixel of captcha


            double gradient = (double) (bottomLeftY - bottomRightY) / (bottomRightX - bottomLeftX); // gradient of the text
            double radian = Math.Atan(gradient); // this in the angle that we need to ratate the text, so it'll become horizontal
            short degree = Convert.ToInt16(radian * (180.0 / Math.PI)); // convert the angle to degree

            finalImage = rotateImage(finalImage, degree);

            // scanning original image
            TesseractEngine engineImage = new TesseractEngine("./tessdata", "eng", EngineMode.Default);
            Page imagePage = engineImage.Process(image, PageSegMode.Auto);

            // scanning finalImage
            TesseractEngine engine = new TesseractEngine("./tessdata", "eng", EngineMode.Default);
            Page page = engine.Process(finalImage, PageSegMode.Auto);
            string imageNumber = page.GetText();

            if (correctScanedNumber(imageNumber).Equals("false")) webBrowser1.Navigate(siteUrl);
            else // show information to user
            {
                pictureBox1.Image = image;
                pictureBox2.Image = finalImage;
                textBox1.Text = correctScanedNumber(imageNumber); // check some special characters
                textBox2.Text = imagePage.GetText();
            }
        }

        private Bitmap DownloadImage()
        {
            try
            {
                WebRequest request = WebRequest.Create(imageURL);
                WebResponse response = request.GetResponse();
                Stream responseStream = response.GetResponseStream();
                return new Bitmap(responseStream);
            }
            catch
            {
                return null;
            }
        }

        private Bitmap rotateImage(Bitmap bitmap, float angle)
        {
            Bitmap rotatedImage = new Bitmap(bitmap.Width, bitmap.Height);
            Graphics graphics = Graphics.FromImage(rotatedImage);
            Rectangle ImageSize = new Rectangle(0, 0, bitmap.Width, bitmap.Height);
            graphics.FillRectangle(Brushes.White, ImageSize); // brush background to white
            graphics.TranslateTransform((float)bitmap.Width / 2, (float)bitmap.Height / 2);
            graphics.RotateTransform(angle);
            graphics.TranslateTransform(-(float)bitmap.Width / 2, -(float)bitmap.Height / 2);
            graphics.DrawImage(bitmap, new Point(0, 0)); // draw rotated image
            return rotatedImage;
        }

        private string correctScanedNumber(string imageNumber)
        {
            imageNumber = imageNumber.ToLower();
            string number = "";
            for(int i = 0; i < imageNumber.Length; i++)
            {
                if (isLegal(imageNumber[i]))
                {
                    if (imageNumber[i] == 'o' || imageNumber[i] == 'd' || imageNumber[i] == 'b' || imageNumber[i] == 'a' || imageNumber[i] == '9') number += "0";
                    else if (imageNumber[i] == '?') number += "2";
                    else if (imageNumber[i] == '$' || imageNumber[i] == '%') number += "35";
                    else if (imageNumber[i] == '8') number += "3";
                    else if (imageNumber[i] == '6' || (imageNumber[i] == 'f' && imageNumber[i + 1] == ')')) number += "5";
                    else if (imageNumber[i] == '\"') number += "41";
                    else if (imageNumber[i] == ':' && imageNumber[i + 1] == 'i')
                    {
                        number += "3";
                        i++;
                    }
                    else if ((imageNumber[i] == '|' && imageNumber[i + 1] == ']') || (imageNumber[i] == 'i' && imageNumber[i + 1] == ']') ||
                             (imageNumber[i] == '!' && imageNumber[i + 1] == ']') || (imageNumber[i] == '|' && imageNumber[i + 1] == '}') ||
                             (imageNumber[i] == 'i' && imageNumber[i + 1] == '}') || (imageNumber[i] == '!' && imageNumber[i + 1] == '}'))
                    {
                        number += "0";
                        i++;
                    }
                    else if (imageNumber[i] == 'i' || imageNumber[i] == '!' || imageNumber[i] == 'f' || imageNumber[i] == '7') number += "1";
                    else number += imageNumber[i].ToString();
                }
            }
            if (number.Length == 5) return number;
            else return "false";
        }

        private bool isLegal(char number)
        {
            switch (number)
            {
                case ')':
                case '%':
                case '}':
                case ']':
                case '|':
                case '\"':
                case 'a':
                case 'f':
                case 'b':
                case 'd':
                case 'i':
                case '?':
                case '!':
                case '$':
                case '0':
                case '9':
                case '8':
                case '7':
                case '6':
                case '5':
                case '4':
                case '3':
                case '2':
                case '1':
                    return true;
                default: return false;
            }
        }

        private void ExitButton_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void MinimizeButton_Click(object sender, EventArgs e)
        {
            this.WindowState = FormWindowState.Minimized;
        }

        private void RefreshButton_Click(object sender, EventArgs e)
        {
            webBrowser1.Navigate(siteUrl);
        }

        private void Form1_MouseDown(object sender, MouseEventArgs e)
        {
            MouseLocation = new Point(-e.X, -e.Y);
        }

        private void Form1_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                Point mousePosition = Control.MousePosition;
                mousePosition.Offset(MouseLocation.X, MouseLocation.Y);
                Location = mousePosition;
            }
        }

        private void PictureBox1_MouseDown(object sender, MouseEventArgs e)
        {
            MouseLocation1 = new Point(-e.X, -e.Y);
        }

        private void PictureBox1_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                Point mousePosition = Control.MousePosition;
                mousePosition.Offset(MouseLocation1.X - pictureBox1.Left, MouseLocation1.Y - pictureBox1.Top);
                Location = mousePosition;
            }
        }

        private void PictureBox2_MouseDown(object sender, MouseEventArgs e)
        {
            MouseLocation2 = new Point(-e.X, -e.Y);
        }

        private void PictureBox2_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                Point mousePosition = Control.MousePosition;
                mousePosition.Offset(MouseLocation2.X - pictureBox2.Left, MouseLocation2.Y - pictureBox2.Top);
                Location = mousePosition;
            }
        }
    }
}