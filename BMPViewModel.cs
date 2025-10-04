using AD_High_GUI_;
using System;
using System.ComponentModel;
using System.Windows.Media.Imaging;


public class BMPViewModel : INotifyPropertyChanged
{
    private BMPModel BMP = new BMPModel();

    private string _bmpResource;

    public BMPViewModel()
    {
        _bmpResource = BMP.GetBMPFileName;
    }

    public BMPHeader BMPHeaderInfo()
    {
        return BMP.BMP;
    }

    public void DrawRectangle(int startX, int startY, int endX, int endY)
    {
        int correctedStartX = Math.Min(startX, endX);
        int correctedStartY = Math.Min(startY, endY);
        int correctedEndX = Math.Max(startX, endX);
        int correctedEndY = Math.Max(startY, endY);

        BMP.DrawRectangle(correctedStartX, correctedStartY, correctedEndX, correctedEndY);
    }

    public void InvertBrightness()
    {
        BMP.InvertBrightness();
    }

    public void ReadBMP(string fileName)
    {
        BMP = new BMPModel();

        if (!BMP.ReadBMP(fileName))
        {
            BMP.ReadBMP(_bmpResource);
            throw new Exception("BMP 파일을 읽는 중에 오류가 발생했습니다.");
        }

        _bmpResource = BMP.GetBMPFileName;
        OnPropertyChanged(nameof(BMPResource));
    }

    public void WriteBMP(string fileName)
    {
        if (!BMP.WriteBMP(fileName))
        {
            throw new Exception("BMP 파일을 쓰는 중에 오류가 발생했습니다.");
        }
    }

    public WriteableBitmap UpdateBMPImage()
    {
        return BMP.UpdateBMPImage();
    }

    public string BMPResource
    {
        get { return _bmpResource; }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged(string propName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
    }
}
