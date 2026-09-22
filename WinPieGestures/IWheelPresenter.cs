using System.Windows;
using System.Windows.Threading;

namespace WinPieGestures;

internal interface IWheelPresenter
{
    Dispatcher Dispatcher { get; }
    Point ActualPhysicalCenter { get; }
    long PresentationVersion { get; }
    void Present(Point center, WheelProfile profile, long configurationRevision, long presentationVersion);
    void Dismiss(long expectedPresentationVersion);
    void HighlightSector(int mainIndex, int subIndex, bool showSubTier);
    void SetOuterEscapeState(bool escaped);
    void SetVolumePreview(int percent, bool isActive);
    void SwitchToLayer(int layerIndex);
    void CloseFast();
}
