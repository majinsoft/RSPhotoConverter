using System;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
namespace RSPhotoConverter
{
    public partial class MainWindow : Window
    {
        const int IMAGE_WIDTH = 1280;
        const int IMAGE_HEIGHT = 720;
        MainWindowVM _vm = new MainWindowVM();
        public MainWindow()
        {
            Thread.CurrentThread.CurrentCulture = new CultureInfo("en-US");
            DataContext = _vm;
            InitializeComponent();
        }
        void btnConvert_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                using (var dialog = new System.Windows.Forms.FolderBrowserDialog())
                {
                    if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                    {
                        var folder = dialog.SelectedPath;

                        var depthFolder = $"{folder}/Depth";
                        var colorFolder = $"{folder}/Color";
                        var irFolder = $"{folder}/IR";
                        var rearFolder = $"{folder}/Rear";

                        Directory.CreateDirectory(depthFolder);
                        Directory.CreateDirectory(colorFolder);
                        Directory.CreateDirectory(irFolder);
                        Directory.CreateDirectory(rearFolder);

                        var files = Directory.GetFiles(folder, "*");
                        foreach (var file in files)
                        {
                            if (file.EndsWith("_depth.dat"))
                            {
                                ExportDepth(file, $"{depthFolder}/{Path.GetFileNameWithoutExtension(file)}.tif");
                            }
                            else if (file.EndsWith("_color.dat"))
                            {
                                ExportColor(file, $"{colorFolder}/{Path.GetFileNameWithoutExtension(file)}.tif");
                            }
                            else if (file.EndsWith("_ir.dat"))
                            {
                                ExportIR(file, $"{irFolder}/{Path.GetFileNameWithoutExtension(file)}.tif");
                            }
                            else if (file.EndsWith("_rear.jpg"))
                            {
                                File.Copy(file, $"{rearFolder}/{Path.GetFileName(file)}", true);
                            }
                        }
                        MessageBox.Show("Done");
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error: {ex.ToString()}");
            }
        }
        BitmapMetadata GetMetadata(string fileInput)
        {
            // Metadata query documentation: https://learn.microsoft.com/en-us/windows/win32/wic/system-photo
            var bmpMetadata = new BitmapMetadata("tiff");
            if (_vm.IsGeolocalized)
            {
                string fileNumber = Path.GetFileName(fileInput).Split("_")[0];
                string fileMetadata = $"{Path.GetDirectoryName(fileInput)}\\{fileNumber}_meta.txt";
                if (File.Exists(fileMetadata))
                {
                    string[] lines = File.ReadAllLines(fileMetadata);
                    string location = lines[0];
                    string[] coordinates = location.Split(",", StringSplitOptions.RemoveEmptyEntries);
                    double latitude = double.Parse(coordinates[0].Trim());
                    double longitude = double.Parse(coordinates[1].Trim());
                    double altitude = double.Parse(coordinates[2].Trim());
                    string latitudeRef = latitude >= 0 ? "N" : "S";
                    string longitudeRef = longitude >= 0 ? "E" : "W";
                    const string GPSVersionID = "2.2.0.0";
                    // TIFF GPS metadata
                    bmpMetadata.SetQuery("/ifd/gps/{ushort=0}", GPSVersionID);
                    bmpMetadata.SetQuery("/ifd/gps/{ushort=1}", latitudeRef);
                    bmpMetadata.SetQuery("/ifd/gps/{ushort=2}", DecimalToDegree(latitude));
                    bmpMetadata.SetQuery("/ifd/gps/{ushort=3}", longitudeRef);
                    bmpMetadata.SetQuery("/ifd/gps/{ushort=4}", DecimalToDegree(longitude));
                    bmpMetadata.SetQuery("/ifd/gps/{ushort=6}", Rational(altitude));
                    // TIFF XMP GPS metadata
                    bmpMetadata.SetQuery("/ifd/xmp/exif:GPSVersionID", GPSVersionID);
                    bmpMetadata.SetQuery("/ifd/xmp/exif:GPSLatitudeRef", latitudeRef);
                    bmpMetadata.SetQuery("/ifd/xmp/exif:GPSLatitude", latitude.ToString());
                    bmpMetadata.SetQuery("/ifd/xmp/exif:GPSLongitudeRef", longitudeRef);
                    bmpMetadata.SetQuery("/ifd/xmp/exif:GPSLongitude", longitude.ToString());
                    bmpMetadata.SetQuery("/ifd/xmp/exif:GPSAltitude", altitude.ToString());
                }
            }
            // TIFF Exif metadata
            bmpMetadata.SetQuery("/ifd/exif/{ushort=37386}", _vm.Device.F);
            bmpMetadata.SetQuery("/ifd/exif/{ushort=41989}", _vm.Device.FocalLengthIn35mmFilm.ToString());
            bmpMetadata.SetQuery("/ifd/exif/{ushort=33437}", _vm.Device.Fstop);
            bmpMetadata.SetQuery("/ifd/{ushort=271}", _vm.Device.Maker);
            bmpMetadata.SetQuery("/ifd/{ushort=272}", _vm.Device.Model);
            // TIFF XMP metadata
            bmpMetadata.SetQuery("/ifd/xmp/exif:FocalLength", _vm.Device.F.ToString());
            bmpMetadata.SetQuery("/ifd/xmp/exif:FocalLengthIn35mmFilm", _vm.Device.FocalLengthIn35mmFilm.ToString());
            bmpMetadata.SetQuery("/ifd/xmp/exif:FNumber", _vm.Device.Fstop.ToString());
            bmpMetadata.SetQuery("/ifd/xmp/tiff:Make", _vm.Device.Maker);
            bmpMetadata.SetQuery("/ifd/xmp/tiff:Model", _vm.Device.Model);
            return bmpMetadata;
        }
        ulong[] DecimalToDegree(double decimalAngle)
        {
            var result = new ulong[3];
            // degree
            result[0] = Rational(Math.Floor(decimalAngle));
            // minutes
            result[1] = Rational(Math.Floor(((decimalAngle - Math.Floor(decimalAngle)) * 60.0)));
            // seconds
            result[2] = Rational((((decimalAngle - Math.Floor(decimalAngle)) * 60.0) - Math.Floor(((decimalAngle - Math.Floor(decimalAngle)) * 60.0))) * 60);
            return result;
        }
        ulong Rational(double dbl)
        {
            const uint denom = 1000;
            uint num = (uint)(dbl * denom);            
            return ((ulong)denom << 32 | (ulong)num);
        }
        void ExportIR(string fileInput, string fileOutput)
        {
            var bytes = File.ReadAllBytes(fileInput);
            var image = BitmapSource.Create(IMAGE_WIDTH, IMAGE_HEIGHT, 96, 96, PixelFormats.Gray8, null, bytes, IMAGE_WIDTH);
            using (var stream = new FileStream(fileOutput, FileMode.Create))
            {
                var encoder = new TiffBitmapEncoder() { Compression = TiffCompressOption.None };
                var bmpFrame = BitmapFrame.Create(image, null, GetMetadata(fileInput), null);
                encoder.Frames.Add(bmpFrame);
                encoder.Save(stream);
            }
        }
        void ExportDepth(string fileInput, string fileOutput)
        {
            var bytes = File.ReadAllBytes(fileInput);
            var image = BitmapSource.Create(IMAGE_WIDTH, IMAGE_HEIGHT, 96, 96, PixelFormats.Gray16, null, bytes, IMAGE_WIDTH * 2);
            using (var stream = new FileStream(fileOutput, FileMode.Create))
            {
                var encoder = new TiffBitmapEncoder() { Compression = TiffCompressOption.None };
                var bmpFrame = BitmapFrame.Create(image, null, GetMetadata(fileInput), null);
                encoder.Frames.Add(bmpFrame);
                encoder.Save(stream);
            }
        }
        void ExportColor(string fileInput, string fileOutput)
        {
            var bytes = File.ReadAllBytes(fileInput);
            var image = BitmapSource.Create(IMAGE_WIDTH, IMAGE_HEIGHT, 96, 96, PixelFormats.Rgb24, null, bytes, IMAGE_WIDTH * 3);
            using (var stream = new FileStream(fileOutput, FileMode.Create))
            {
                var encoder = new TiffBitmapEncoder() { Compression = TiffCompressOption.None };
                var bmpFrame = BitmapFrame.Create(image, null, GetMetadata(fileInput), null);
                encoder.Frames.Add(bmpFrame);
                encoder.Save(stream);
            }
        }
    }
}