using System.Runtime.InteropServices;

namespace HerculesAudioControl.Services;

public static class MediaKeyService
{
    private const byte VK_MEDIA_NEXT_TRACK = 0xB0;
    private const byte VK_MEDIA_PREV_TRACK = 0xB1;
    private const byte VK_MEDIA_PLAY_PAUSE = 0xB3;
    private const uint KEYEVENTF_KEYUP = 0x0002;

    public static void PlayPause() => Press(VK_MEDIA_PLAY_PAUSE);
    public static void Previous() => Press(VK_MEDIA_PREV_TRACK);
    public static void Next() => Press(VK_MEDIA_NEXT_TRACK);

    private static void Press(byte key)
    {
        keybd_event(key, 0, 0, UIntPtr.Zero);
        keybd_event(key, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
    }

    [DllImport("user32.dll")]
    private static extern void keybd_event(
        byte bVk,
        byte bScan,
        uint dwFlags,
        UIntPtr dwExtraInfo);
}
