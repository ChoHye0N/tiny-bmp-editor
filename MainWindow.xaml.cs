using Microsoft.Win32;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace AD_High_GUI_;

public partial class MainWindow : Window
{
    public BMPViewModel vm = new BMPViewModel();
    private Point startPoint, currentPoint;
    private bool isDragging = false;

    public MainWindow()
    {
        InitializeComponent();
    }

    private void OnMenuItemInfoClick(object sender, RoutedEventArgs e)
    {
        string[] compressionMethods = {
            "None", "RLE 8-bit", "RLE 4-bit", "Bitfields", "Unknown"
        };

        MessageBox.Show($"BMP 파일 정보\n\n" +
            $"파일 크기: {vm.BMPHeaderInfo().BfSize} bytes\n" +
            $"사진 크기: {vm.BMPHeaderInfo().BiWidth} X {vm.BMPHeaderInfo().BiHeight} pixels\n" +
            $"픽셀 당 비트 수: {vm.BMPHeaderInfo().BiBitCount} bits per pixel\n" +
            $"압축 방법: {compressionMethods[vm.BMPHeaderInfo().BiCompression]}", "파일 정보");
    }

    private void OnMenuItemOpenClick(object sender, RoutedEventArgs e)
    {
        // 메뉴 초기화
        reverse.IsChecked = false;


        OpenFileDialog openFileDialog = new OpenFileDialog();
        openFileDialog.Filter = "BMP 파일|*.bmp|모든 파일|*.*";

        if (openFileDialog.ShowDialog() == true)
        {
            string selectedImagePath = openFileDialog.FileName;

            try
            {
                vm.ReadBMP(selectedImagePath);
                imgDisplay.Source = vm.UpdateBMPImage();

                // 이미지가 로드된 후에 윈도우 크기 설정
                imgDisplay.Dispatcher.Invoke(() =>
                {
                    this.Width = imgDisplay.ActualWidth + 100;
                    this.Height = imgDisplay.ActualHeight + 150;
                }, System.Windows.Threading.DispatcherPriority.Background);

                save_as.IsEnabled = true;
                info.IsEnabled = true;
                draw.IsEnabled = true;
                reverse.IsEnabled = true;

            }
            catch (Exception ex)
            {
                MessageBox.Show($"이미지를 열 수 없습니다.\n오류: {ex.Message}"
                    , "파일 열기", MessageBoxButton.OK, MessageBoxImage.Error);
                if(vm.BMPResource != "")
                {
                    imgDisplay.Source = vm.UpdateBMPImage();
                }
            }
        }
    }

    private void OnMenuItemExitClick(object sender, RoutedEventArgs e)
    {
        // 애플리케이션 종료
        Application.Current.Shutdown();
    }

    private void OnCanvasMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
        {
            startPoint = e.GetPosition(canvas);
            isDragging = true;
            dragRectangle.Visibility = Visibility.Visible;
            Canvas.SetLeft(dragRectangle, startPoint.X);
            Canvas.SetTop(dragRectangle, startPoint.Y);
            dragRectangle.Width = 0;
            dragRectangle.Height = 0;
        }
    }

    private void OnCanvasMouseMove(object sender, MouseEventArgs e)
    {
        if (isDragging)
        {
            currentPoint = e.GetPosition(canvas);

            double width = Math.Abs(currentPoint.X - startPoint.X);
            double height = Math.Abs(currentPoint.Y - startPoint.Y);

            dragRectangle.Width = width;
            dragRectangle.Height = height;

            Canvas.SetLeft(dragRectangle, Math.Min(currentPoint.X, startPoint.X));
            Canvas.SetTop(dragRectangle, Math.Min(currentPoint.Y, startPoint.Y));
        }
    }

    private void OnCanvasMouseUp(object sender, MouseButtonEventArgs e)
    {
        isDragging = false;
    }

    private void OnMenuItemDrawClick(object sender, RoutedEventArgs e)
    {
        vm.DrawRectangle((int)startPoint.X, (int)startPoint.Y,
            (int)currentPoint.X, (int)currentPoint.Y);

        imgDisplay.Source = vm.UpdateBMPImage();
    }

    private void OnMenuItemReverseClick(object sender, RoutedEventArgs e)
    {
        reverse.IsChecked = !reverse.IsChecked;

        vm.InvertBrightness();

        imgDisplay.Source = vm.UpdateBMPImage();

    }

    private void OnMenuItemSaveClick(object sender, RoutedEventArgs e)
    {

        // SaveFileDialog 생성
        SaveFileDialog saveFileDialog = new SaveFileDialog();

        // 초기 파일 이름 및 필터 설정
        saveFileDialog.FileName = "output.bmp";
        saveFileDialog.Filter = "BMP 파일 (*.bmp)|*.bmp|모든 파일 (*.*)|*.*";

        // 다이얼로그를 통해 새로운 파일 이름을 입력받음
        if (saveFileDialog.ShowDialog() == true)
        {
            // 입력받은 파일 이름으로 저장
            try
            {
                vm.WriteBMP(saveFileDialog.FileName);

                MessageBox.Show($"{saveFileDialog.FileName} 경로에 파일이 저장되었습니다",
                    "파일 저장");
            }
            
            catch(Exception ex)
            {
                MessageBox.Show($"이미지를 저장할 수 없습니다.\n오류: {ex.Message}"
                    , "파일 저장", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
