using System.Windows;
using System.Windows.Media;
using Aranet4Monitor.Presentation;

namespace Aranet4Monitor.Presentation.ViewModels;

public enum ListenerStatusKind
{
    Idle,
    Listening,
    Error,
    BluetoothUnavailable
}

public sealed class StatusBarViewModel : ObservableObject
{
    private ListenerStatusKind listenerStatus = ListenerStatusKind.Idle;
    private Visibility noticeVisibility = Visibility.Collapsed;
    private Visibility noticeActionVisibility = Visibility.Visible;
    private Visibility bluetoothSettingsVisibility = Visibility.Collapsed;
    private string noticeText = "Live readings are off.";
    private string noticeActionText = "Start listening";
    private string? noticeToolTip;
    private string noticeBackgroundKey = "SurfaceSecondary";
    private string noticeBorderKey = "Border";
    private Brush noticeBackground = ThemeService.GetBrush("SurfaceSecondary");
    private Brush noticeBorder = ThemeService.GetBrush("Border");
    private string footerStatusText = "Searching";
    private string footerStatusBrushKey = "Muted";
    private Brush footerStatusBrush = ThemeService.GetBrush("Muted");
    private string syncStatusText = string.Empty;
    private string? syncStatusToolTip;
    private Visibility syncProgressVisibility = Visibility.Collapsed;
    private string syncToastText = string.Empty;
    private Visibility syncToastVisibility = Visibility.Collapsed;
    private string syncProblemText = string.Empty;
    private string? syncProblemToolTip;
    private Visibility syncProblemVisibility = Visibility.Collapsed;

    public ListenerStatusKind ListenerStatus => listenerStatus;

    public Visibility NoticeVisibility
    {
        get => noticeVisibility;
        private set => SetProperty(ref noticeVisibility, value);
    }

    public Visibility NoticeActionVisibility
    {
        get => noticeActionVisibility;
        private set => SetProperty(ref noticeActionVisibility, value);
    }

    public Visibility BluetoothSettingsVisibility
    {
        get => bluetoothSettingsVisibility;
        private set => SetProperty(ref bluetoothSettingsVisibility, value);
    }

    public string NoticeText
    {
        get => noticeText;
        private set => SetProperty(ref noticeText, value);
    }

    public string NoticeActionText
    {
        get => noticeActionText;
        private set => SetProperty(ref noticeActionText, value);
    }

    public string? NoticeToolTip
    {
        get => noticeToolTip;
        set => SetProperty(ref noticeToolTip, value);
    }

    public Brush NoticeBackground
    {
        get => noticeBackground;
        private set => SetProperty(ref noticeBackground, value);
    }

    public Brush NoticeBorder
    {
        get => noticeBorder;
        private set => SetProperty(ref noticeBorder, value);
    }

    public string FooterStatusText
    {
        get => footerStatusText;
        private set => SetProperty(ref footerStatusText, value);
    }

    public Brush FooterStatusBrush
    {
        get => footerStatusBrush;
        private set => SetProperty(ref footerStatusBrush, value);
    }

    public string SyncStatusText
    {
        get => syncStatusText;
        private set => SetProperty(ref syncStatusText, value);
    }

    public string? SyncStatusToolTip
    {
        get => syncStatusToolTip;
        private set => SetProperty(ref syncStatusToolTip, value);
    }

    public Visibility SyncProgressVisibility
    {
        get => syncProgressVisibility;
        private set => SetProperty(ref syncProgressVisibility, value);
    }

    public string SyncToastText
    {
        get => syncToastText;
        private set => SetProperty(ref syncToastText, value);
    }

    public Visibility SyncToastVisibility
    {
        get => syncToastVisibility;
        private set => SetProperty(ref syncToastVisibility, value);
    }

    public string SyncProblemText
    {
        get => syncProblemText;
        private set => SetProperty(ref syncProblemText, value);
    }

    public string? SyncProblemToolTip
    {
        get => syncProblemToolTip;
        private set => SetProperty(ref syncProblemToolTip, value);
    }

    public Visibility SyncProblemVisibility
    {
        get => syncProblemVisibility;
        private set => SetProperty(ref syncProblemVisibility, value);
    }

    public void SetListenerStatus(string message, ListenerStatusKind kind, string? details, bool listenerHasBeenStarted)
    {
        SetProperty(ref listenerStatus, kind, nameof(ListenerStatus));
        NoticeVisibility = kind == ListenerStatusKind.Listening || !listenerHasBeenStarted
            ? Visibility.Collapsed
            : Visibility.Visible;

        switch (kind)
        {
            case ListenerStatusKind.Idle:
                NoticeText = "Live readings are off.";
                NoticeActionText = "Start listening";
                BluetoothSettingsVisibility = Visibility.Collapsed;
                SetNoticeColors("SurfaceSecondary", "Border");
                NoticeToolTip = null;
                break;
            case ListenerStatusKind.Error:
                NoticeText = "Live readings stopped. Check Bluetooth settings or permissions, then try again.";
                NoticeActionText = "Try again";
                BluetoothSettingsVisibility = Visibility.Collapsed;
                SetNoticeColors("DangerBackground", "Danger");
                NoticeToolTip = details ?? message;
                break;
            case ListenerStatusKind.BluetoothUnavailable:
                NoticeText = "Bluetooth is off. Turn it on in Windows settings to see live readings.";
                BluetoothSettingsVisibility = Visibility.Visible;
                SetNoticeColors("WarningBackground", "Warning");
                NoticeToolTip = details ?? message;
                break;
            default:
                BluetoothSettingsVisibility = Visibility.Collapsed;
                SetNoticeColors("PositiveBackground", "Positive");
                NoticeToolTip = details;
                break;
        }

        NoticeActionVisibility = kind == ListenerStatusKind.BluetoothUnavailable
            ? Visibility.Collapsed
            : Visibility.Visible;
    }

    public void SetFooterStatus(string text, string brushKey)
    {
        FooterStatusText = text;
        footerStatusBrushKey = brushKey;
        FooterStatusBrush = ThemeService.GetBrush(footerStatusBrushKey);
    }

    public void SetSyncStatus(string text, string? details)
    {
        SyncStatusText = text;
        SyncStatusToolTip = details ?? text;
        SyncProgressVisibility = string.IsNullOrWhiteSpace(text) ? Visibility.Collapsed : Visibility.Visible;
    }

    public void ShowSyncToast(string text)
    {
        SyncToastText = text;
        SyncToastVisibility = Visibility.Visible;
    }

    public void HideSyncToast() => SyncToastVisibility = Visibility.Collapsed;

    public void ShowSyncProblem(string text, string details)
    {
        SyncProblemText = text;
        SyncProblemToolTip = details;
        SyncProblemVisibility = Visibility.Visible;
    }

    public void HideSyncProblem() => SyncProblemVisibility = Visibility.Collapsed;

    public void RefreshTheme()
    {
        NoticeBackground = ThemeService.GetBrush(noticeBackgroundKey);
        NoticeBorder = ThemeService.GetBrush(noticeBorderKey);
        FooterStatusBrush = ThemeService.GetBrush(footerStatusBrushKey);
    }

    private void SetNoticeColors(string backgroundKey, string borderKey)
    {
        noticeBackgroundKey = backgroundKey;
        noticeBorderKey = borderKey;
        NoticeBackground = ThemeService.GetBrush(backgroundKey);
        NoticeBorder = ThemeService.GetBrush(borderKey);
    }
}
