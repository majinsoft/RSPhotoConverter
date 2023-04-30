using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
namespace RSPhotoConverter
{
    public partial class Device : ObservableObject
    {
        [ObservableProperty]
        string _maker, model;
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(CropFactor))]
        double _f, _fstop, _sensorWidth, _sensorHeight;
        public override string ToString() => Model;
        public double CropFactor
        {
            get
            {
                double sensor35Diagonal = Math.Sqrt(36 * 36 + 24 * 24);
                double sensorDiagonal = Math.Sqrt(SensorWidth * SensorWidth + SensorHeight * SensorHeight);
                return sensor35Diagonal / sensorDiagonal;
            }
        }
    }
    public partial class MainWindowVM : ObservableObject
    {
        [ObservableProperty]
        Device _device;

        [ObservableProperty]
        List<Device> _presets;

        [ObservableProperty]
        bool _isGeolocalized;

        public MainWindowVM()
        {
            _isGeolocalized = true;
            _presets = new List<Device>() {
                new Device()
                {
                    // OV2740
                    Maker = "Intel",
                    Model = "RealSense D415",
                    F=1.88,
                    Fstop=2.0,
                    SensorWidth=2.7288,
                    SensorHeight=1.5498
                },
                new Device()
                {
                    // OV9282,OV9782
                    Maker = "Intel",
                    Model = "RealSense D435/D405/D455/D457",
                    F=1.93,
                    Fstop=2.0,
                    SensorWidth=3.896,
                    SensorHeight=2.453
                },
                new Device()
                {
                    Maker = "RS Photo Converter",
                    Model = "Custom",
                    F=1.0,
                    Fstop=1.0,
                    SensorWidth=3.0,
                    SensorHeight=2.0
                }
            };
            _device = _presets[0];
        }
    }
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
            // Metadata query from https://learn.microsoft.com/en-us/windows/win32/wic/system-photo

            var bmpMetadata = new BitmapMetadata("tiff");

            double FocalLengthIn35mmFilm = _vm.Device.CropFactor * _vm.Device.F;

            // Geolocalization
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
                    // 44.4829078, 9.7689463, 486.199981689453

                    // TIFF Exif metadata
                    bmpMetadata.SetQuery("/ifd/gps/{ushort=0}", GPSVersionID);
                    bmpMetadata.SetQuery("/ifd/gps/{ushort=1}", latitudeRef);
                    bmpMetadata.SetQuery("/ifd/gps/{ushort=2}", decimalToDegree(latitude));
                    bmpMetadata.SetQuery("/ifd/gps/{ushort=3}", longitudeRef);
                    bmpMetadata.SetQuery("/ifd/gps/{ushort=4}", decimalToDegree(longitude));
                    bmpMetadata.SetQuery("/ifd/gps/{ushort=6}", rational(altitude));

                    // TIFF XMP metadata
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
            bmpMetadata.SetQuery("/ifd/exif/{ushort=41989}", FocalLengthIn35mmFilm.ToString());
            bmpMetadata.SetQuery("/ifd/exif/{ushort=33437}", _vm.Device.Fstop);
            bmpMetadata.SetQuery("/ifd/{ushort=271}", _vm.Device.Maker);
            bmpMetadata.SetQuery("/ifd/{ushort=272}", _vm.Device.Model);

            // TIFF XMP metadata
            bmpMetadata.SetQuery("/ifd/xmp/exif:FocalLength", _vm.Device.F.ToString());
            bmpMetadata.SetQuery("/ifd/xmp/exif:FocalLengthIn35mmFilm", FocalLengthIn35mmFilm.ToString());
            bmpMetadata.SetQuery("/ifd/xmp/exif:FNumber", _vm.Device.Fstop.ToString());
            bmpMetadata.SetQuery("/ifd/xmp/tiff:Make", _vm.Device.Maker);
            bmpMetadata.SetQuery("/ifd/xmp/tiff:Model", _vm.Device.Model);

            return bmpMetadata;
        }

        ulong[] decimalToDegree(double decimalAngle)
        {
            var result = new ulong[3];
            // degree
            result[0] = rational(Math.Floor(decimalAngle));
            // minutes
            result[1] = rational(Math.Floor(((decimalAngle - Math.Floor(decimalAngle)) * 60.0)));
            // seconds
            result[2] = rational((((decimalAngle - Math.Floor(decimalAngle)) * 60.0) - Math.Floor(((decimalAngle - Math.Floor(decimalAngle)) * 60.0))) * 60);
            return result;
        }

        ulong rational(double dbl)
        {
            uint denom = 1000;
            uint num = (uint)(dbl * denom);
            ulong tmp;
            tmp = (ulong)denom << 32;
            tmp |= (ulong)num;
            return tmp;
        }

        void ExportDepth(string fileInput, string fileOutput)
        {
            // 16 Bit
            var bytes = File.ReadAllBytes(fileInput);
            var image = BitmapSource.Create(IMAGE_WIDTH, IMAGE_HEIGHT, 96, 96, PixelFormats.Gray16, null, bytes, IMAGE_WIDTH * 2);
            using (var stream = new FileStream(fileOutput, FileMode.Create))
            {
                var encoder = new TiffBitmapEncoder();
                encoder.Compression = TiffCompressOption.None;
                var bmpFrame = BitmapFrame.Create(image, null, GetMetadata(fileInput), null);
                encoder.Frames.Add(bmpFrame);
                encoder.Save(stream);
            }
        }
        void ExportColor(string fileInput, string fileOutput)
        {
            // RGB8
            var bytes = File.ReadAllBytes(fileInput);
            var image = BitmapSource.Create(IMAGE_WIDTH, IMAGE_HEIGHT, 96, 96, PixelFormats.Rgb24, null, bytes, IMAGE_WIDTH * 3);
            using (var stream = new FileStream(fileOutput, FileMode.Create))
            {
                var encoder = new TiffBitmapEncoder();
                encoder.Compression = TiffCompressOption.None;
                var bmpFrame = BitmapFrame.Create(image, null, GetMetadata(fileInput), null);
                encoder.Frames.Add(bmpFrame);
                encoder.Save(stream);
            }
        }
        void ExportIR(string fileInput, string fileOutput)
        {
            // Y8
            var bytes = File.ReadAllBytes(fileInput);
            var image = BitmapSource.Create(IMAGE_WIDTH, IMAGE_HEIGHT, 96, 96, PixelFormats.Gray8, null, bytes, IMAGE_WIDTH);
            using (var stream = new FileStream(fileOutput, FileMode.Create))
            {
                var encoder = new TiffBitmapEncoder();
                encoder.Compression = TiffCompressOption.None;
                var bmpFrame = BitmapFrame.Create(image, null, GetMetadata(fileInput), null);
                encoder.Frames.Add(bmpFrame);
                encoder.Save(stream);
            }
        }
    }
}