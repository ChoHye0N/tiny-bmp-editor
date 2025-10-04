using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace AD_High_GUI_;

public struct BMPHeader
{
    // 비트맵 파일 헤더
    public char[] Signature;    // BMP 파일 식별자 ('BM')
    public uint BfSize;         // 파일 크기 (전체 파일 크기)
    public ushort BfReserved1;  // 예약 영역 1 (사용하지 않음, 0)
    public ushort BfReserved2;  // 예약 영역 2 (사용하지 않음, 0)
    public uint BfOffset;       // 비트맵 데이터까지의 오프셋

    // 비트맵 정보 헤더
    public uint BiHeaderSize;   // 헤더 크기 (이 헤더의 크기, 40)
    public int BiWidth;         // 이미지 가로 크기
    public int BiHeight;        // 이미지 세로 크기
    public ushort BiPlanes;     // 색상 판의 수 (1)
    public ushort BiBitCount;   // 비트맵당 비트 수
    public uint BiCompression;  // 압축 방식
    public uint BiDataSize;     // 비트맵 데이터 크기
    public int BiHResolution;   // 수평 해상도 (픽셀/미터)
    public int BiVResolution;   // 수직 해상도 (픽셀/미터)
    public uint BiColors;       // 사용되는 색상 인덱스 수 (8 비트 그레이 스케일의 경우 256)
    public uint BiImportantColors;  // 중요한 색상 인덱스 수 (0, 모든 색상 중요함)
}

// 색상 테이블
public struct ColorPalette
{
    public byte Blue;
    public byte Green;
    public byte Red;
    public byte Reserved;
}

public class BMPModel
{
    private string BMPFileName = "";
    private BMPHeader _bmpHeader = new BMPHeader();  // BMP 파일 헤더
    private List<ColorPalette> _colorTable = new List<ColorPalette>();

    private byte[] _decodedData = new byte[0];
    private byte[] _pixelData = new byte[0];
    private List<byte> _encodedData = new List<byte>();
    

    public BMPHeader BMP { get => _bmpHeader; }
    public string GetBMPFileName { get => BMPFileName; }

    public void InvertBrightness()
    {
        for (int i = 0; i < _pixelData.Length; ++i)
        {
            _pixelData[i] = (byte)(255 - _pixelData[i]);
        }
    }

    public void DrawRectangle(int x1, int y1, int x2, int y2)
    {
        int paddingWidth = (_bmpHeader.BiWidth + 3) / 4 * 4;

        for (int j = y1; j < y2; j++)
        {
            for (int i = x1; i < x2; i++)
            {
                if (i >= 0 && i < _bmpHeader.BiWidth && j >= 0 && j < _bmpHeader.BiHeight)
                {
                    // 좌표를 사용하여 인덱스 계산
                    int index = i + (_bmpHeader.BiHeight - 1 - j) * paddingWidth;

                    // 그린 부분을 흰색으로 설정
                    _pixelData[index] = 255;
                }
            }
        }
    }

    public WriteableBitmap UpdateBMPImage()
    {
        try
        {
            int paddingWidth = (_bmpHeader.BiWidth + 3) / 4 * 4;

            // _colorTable을 사용하여 BitmapPalette 생성
            BitmapPalette myPalette = new BitmapPalette(
                _colorTable.Select(cp => Color.FromRgb(cp.Red, cp.Green, cp.Blue)).ToList()
            );

            // PixelFormats.Gray8로 설정된 WriteableBitmap 생성 및 사용자 정의 팔레트 적용
            WriteableBitmap bitmap;

            if (_bmpHeader.BiBitCount == 8)
            {
                bitmap = new WriteableBitmap(_bmpHeader.BiWidth, _bmpHeader.BiHeight,
                   96, 96, PixelFormats.Indexed8, myPalette);
            }
            else if (_bmpHeader.BiBitCount == 4)
            {
                bitmap = new WriteableBitmap(_bmpHeader.BiWidth, _bmpHeader.BiHeight,
                  96, 96, PixelFormats.Indexed4, myPalette);
            }
            else
            {
                throw new Exception("1Bit에서 8Bit BMP 파일만 지원합니다");
            }

            // WriteableBitmap을 잠금
            bitmap.Lock();

            // Int32Rect를 사용하여 픽셀 데이터에 직접 접근
            Int32Rect rect = new Int32Rect(0, 0, _bmpHeader.BiWidth, _bmpHeader.BiHeight);

            // 각 행의 크기 계산
            double bytesPerPixel = _bmpHeader.BiBitCount / 8; // 비트당 바이트 수 계산
            int rowSize = (int)(paddingWidth * bytesPerPixel); // 패딩을 고려한 행의 크기 계산

            // 행 단위로 자른 후 역순으로 배열
            List<byte[]> reversedRows = new List<byte[]>();
            for (int i = 0; i < _bmpHeader.BiHeight; i++)
            {
                byte[] row = new byte[rowSize];
                Array.Copy(_pixelData, i * rowSize, row, 0, rowSize);
                reversedRows.Add(row);
            }

            reversedRows.Reverse();

            // 역순으로 변경된 픽셀 데이터 생성
            byte[] reversedPixelData = reversedRows.SelectMany(row => row).ToArray();

            // WriteableBitmap에 픽셀 데이터 복사
            bitmap.WritePixels(rect, reversedPixelData, paddingWidth, 0);

            // WriteableBitmap 잠금 해제
            bitmap.Unlock();

            return bitmap;
        }
        catch(Exception ex)
        {
            throw new Exception("비트맵을 불러오는데 문제가 발생했습니다");
        }

    }

    // RLE 인코딩 함수
    public void RLEEncoder(byte[] decodedData, ref List<byte> encodedData)
    {
        encodedData.Clear();
        int paddedWidth = (_bmpHeader.BiWidth + 3) / 4 * 4; // 패딩된 너비 계산
        
        List<byte> notRunLengthList = new List<byte>();

        // RLE 압축 수행
        for (int y = 0; y < _bmpHeader.BiHeight; ++y)
        {
            int x = 0;

            while (x < _bmpHeader.BiWidth)
            {
                byte currentPixel = decodedData[y * paddedWidth + x];
                byte runLength = 1;

                // 연속값일 때 runLength++
                while (runLength < 255
                    && (x + runLength) < paddedWidth
                    && decodedData[y * paddedWidth + x + runLength] == currentPixel)
                {
                    runLength++;
                }

                // runLength 판별
                if (runLength > 2)
                {
                    // Absolute mode
                    if (notRunLengthList.Count > 0)
                    {
                        if (notRunLengthList.Count < 3)
                        {
                            foreach (byte value in notRunLengthList)
                            {
                                encodedData.Add(1);
                                encodedData.Add(value);
                            }
                        }
                        else
                        {
                            encodedData.Add(0);
                            encodedData.Add((byte)notRunLengthList.Count);
                            foreach (byte value in notRunLengthList)
                            {
                                encodedData.Add(value);
                            }
                            if (notRunLengthList.Count % 2 != 0)
                            {
                                encodedData.Add(0);
                            }
                        }
                        notRunLengthList.Clear();
                    }

                    // Encoded mode
                    encodedData.Add(runLength);
                    encodedData.Add(currentPixel);
                    x += runLength;
                }
                // 불연속 값일 때 x++
                else
                {
                    notRunLengthList.Add(currentPixel);
                    x++;
                }
            }

            // 행이 끝나고 나서 Absolute mode에 사용된 배열이 남아있을때 추가 작업
            if (notRunLengthList.Count > 0)
            {
                if (notRunLengthList.Count < 3)
                {
                    foreach (byte value in notRunLengthList)
                    {
                        encodedData.Add(1);
                        encodedData.Add(value);
                    }
                }
                else
                {
                    encodedData.Add(0);
                    encodedData.Add((byte)notRunLengthList.Count);
                    foreach (byte value in notRunLengthList)
                    {
                        encodedData.Add(value);
                    }
                    if (notRunLengthList.Count % 2 != 0)
                    {
                        encodedData.Add(0);
                    }
                }
                notRunLengthList.Clear();
            }

            // End Line
            if (y != _bmpHeader.BiHeight - 1)
            {
                encodedData.Add(0);
                encodedData.Add(0);
            }
        }

        // End Bitmap
        encodedData.Add(0);
        encodedData.Add(1);
    }

    // RLE 디코딩 함수
    private void RLEDecoder(byte[] encodedData, ref byte[] pixelData)
    {
        int colorSize = 255;
        int paddedWidth = (_bmpHeader.BiWidth + 3) / 4 * 4;

        int currentPos = 0;
        int rleIndex = 0;

        byte count, value;

        while (currentPos < pixelData.Length && rleIndex < encodedData.Length)
        {
            count = encodedData[rleIndex++];

            if (count == 0)
            {
                // Escape 코드 처리
                count = encodedData[rleIndex++];

                if (count == 0)
                {
                    // 라인 종료 코드 (다음 라인으로 이동)
                    int remaining = paddedWidth - (currentPos % paddedWidth);
                    if (remaining != paddedWidth)
                    {
                        currentPos += remaining;
                    }
                }

                else if (count == 1)
                {
                    // 종료 코드 (비트맵 종료)
                    break;
                }

                else if (count == 2)
                {
                    // Delta 패킷
                    byte dx = encodedData[rleIndex++];
                    byte dy = encodedData[rleIndex++];
                    currentPos += dx + dy * paddedWidth;
                }

                else if (count >= 3 && count <= colorSize)
                {
                    // Absolute mode 패킷 처리
                    if (count % 2 != 0)
                    {
                        for (int i = 0; i < count + 1; i++)
                        {
                            if (currentPos < paddedWidth * _bmpHeader.BiHeight)
                            {
                                value = encodedData[rleIndex++];
                                if (i == count) { continue; }
                                pixelData[currentPos++] = value;
                            }
                        }
                    }
                    else
                    {
                        for (int i = 0; i < count; i++)
                        {
                            if (currentPos < paddedWidth * _bmpHeader.BiHeight)
                            {
                                value = encodedData[rleIndex++];
                                pixelData[currentPos++] = value;
                            }
                        }
                    }
                }
            }

            else if (count >= 1 && count <= colorSize)
            {
                // Encoded mode 패킷 처리
                value = encodedData[rleIndex++];

                for (int i = 0; i < count; i++)
                {
                    if (currentPos < paddedWidth * _bmpHeader.BiHeight)
                    {
                        pixelData[currentPos++] = value;
                    }
                }
            }
        }
    }

    // BMP 파일 읽기 함수
    public bool ReadBMP(string fileName)
    {
        BMPFileName = fileName;

        using (BinaryReader reader = new BinaryReader(File.OpenRead(fileName)))
        {
            try
            {
                // 파일 헤더 읽기
                _bmpHeader.Signature = reader.ReadChars(2);

                // BMP 파일이 아닐 경우 예외 던지기
                if (_bmpHeader.Signature[0] != 'B' || _bmpHeader.Signature[1] != 'M')
                {
                    throw new Exception("BMP 파일이 아닌 다른 파일은 열 수 없습니다");
                }

                _bmpHeader.BfSize = reader.ReadUInt32();
                _bmpHeader.BfReserved1 = reader.ReadUInt16();
                _bmpHeader.BfReserved2 = reader.ReadUInt16();
                _bmpHeader.BfOffset = reader.ReadUInt32();

                // 비트맵 정보 헤더 읽기
                _bmpHeader.BiHeaderSize = reader.ReadUInt32();
                _bmpHeader.BiWidth = reader.ReadInt32();
                _bmpHeader.BiHeight = reader.ReadInt32();
                _bmpHeader.BiPlanes = reader.ReadUInt16();
                _bmpHeader.BiBitCount = reader.ReadUInt16();
                _bmpHeader.BiCompression = reader.ReadUInt32();
                _bmpHeader.BiDataSize = reader.ReadUInt32();
                _bmpHeader.BiHResolution = reader.ReadInt32();
                _bmpHeader.BiVResolution = reader.ReadInt32();
                _bmpHeader.BiColors = reader.ReadUInt32();
                _bmpHeader.BiImportantColors = reader.ReadUInt32();

                //팔레트 정보 읽기
                for (int i = 0; i < (int)Math.Pow(2, _bmpHeader.BiBitCount); i++)
                {
                    ColorPalette color = new ColorPalette();
                    color.Blue = reader.ReadByte();
                    color.Green = reader.ReadByte();
                    color.Red = reader.ReadByte();
                    color.Reserved = reader.ReadByte();
                    _colorTable.Add(color);
                }

                // 픽셀 데이터 읽기
                _decodedData = reader.ReadBytes((int)_bmpHeader.BiDataSize);

                double bytesPerPixel = (double)_bmpHeader.BiBitCount / 8;
                int paddedWidth = (_bmpHeader.BiWidth + 3) / 4 * 4;

                Array.Resize(ref _pixelData, (int)(paddedWidth * (double)_bmpHeader.BiHeight * bytesPerPixel));

                if(_bmpHeader.BiCompression == 0)
                {
                    _pixelData = _decodedData;
                    
                }
                else if(_bmpHeader.BiCompression == 1)
                {
                    RLEDecoder(_decodedData, ref _pixelData);
                    //_bmpHeader.BiCompression = 0;
                }

                return true;
            }

            catch
            {
                return false;
            }
        }
    }

    // BMP 파일 쓰기 함수
    public bool WriteBMP(string fileName)
    {
        if (File.Exists(fileName))
        {
            File.Delete(fileName);
        }
        using (BinaryWriter writer = new BinaryWriter(File.OpenWrite(fileName)))
        {
            try
            {
                // RLE8 압축
                RLEEncoder(_pixelData, ref _encodedData);

                // 파일 헤더 쓰기
                writer.Write(_bmpHeader.Signature);
                writer.Write(_bmpHeader.BfSize);
                writer.Write(_bmpHeader.BfReserved1);
                writer.Write(_bmpHeader.BfReserved2);
                writer.Write(_bmpHeader.BfOffset);

                // 비트맵 정보 헤더 쓰기
                writer.Write(_bmpHeader.BiHeaderSize);
                writer.Write(_bmpHeader.BiWidth);
                writer.Write(_bmpHeader.BiHeight);
                writer.Write(_bmpHeader.BiPlanes);
                writer.Write(_bmpHeader.BiBitCount);
                writer.Write(_bmpHeader.BiCompression);
                writer.Write(_bmpHeader.BiDataSize);
                writer.Write(_bmpHeader.BiHResolution);
                writer.Write(_bmpHeader.BiVResolution);
                writer.Write(_bmpHeader.BiColors);
                writer.Write(_bmpHeader.BiImportantColors);

                // 색상 테이블 쓰기
                foreach (ColorPalette color in _colorTable)
                {
                    writer.Write(color.Blue);
                    writer.Write(color.Green);
                    writer.Write(color.Red);
                    writer.Write(color.Reserved);
                }

                //writer.Write(_pixelData);
                writer.Write(_encodedData.ToArray());

                // BfSize 업데이트
                writer.Seek(2, SeekOrigin.Begin);
                writer.Write((int)writer.BaseStream.Length);

                // BiDataSize 업데이트
                writer.Seek(34, SeekOrigin.Begin);
                writer.Write((int)_decodedData.Length);

                double compressionRate = (double)_encodedData.ToArray().Length / 
                    _pixelData.Length * 100;
                MessageBox.Show($"원본 파일의 {compressionRate:F2}%의 압축률을 보임", "압축률");

                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
