using CommunityToolkit.Mvvm.ComponentModel;
using System;
namespace RSPhotoConverter
{
    public partial class Device : ObservableObject
    {
        [ObservableProperty]
        string _maker, model;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(CropFactor))]
        [NotifyPropertyChangedFor(nameof(FocalLengthIn35mmFilm))]
        double _f, _fstop, _sensorWidth, _sensorHeight;
        public double CropFactor
        {
            get
            {
                double sensor35Diagonal = Math.Sqrt(36 * 36 + 24 * 24);
                double sensorDiagonal = Math.Sqrt(SensorWidth * SensorWidth + SensorHeight * SensorHeight);
                return sensor35Diagonal / sensorDiagonal;
            }
        }
        public double FocalLengthIn35mmFilm => CropFactor * F;
        public override string ToString() => Model;
    }
}