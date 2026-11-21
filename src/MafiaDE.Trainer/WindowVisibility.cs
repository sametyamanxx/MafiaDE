namespace MafiaDE.Trainer;

internal static class WindowVisibility
{
    // A visible window may be covered by a full-screen game. In that case Insert
    // must show/activate it, not treat WS_VISIBLE alone as a request to hide.
    public static bool ShouldHide(bool visible, bool minimized, bool foreground) =>
        visible && !minimized && foreground;
}
