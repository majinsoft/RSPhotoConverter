using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.Generic;
namespace RSPhotoConverter
{
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
}