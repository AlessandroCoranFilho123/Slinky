namespace Slinky.Services
{
    /// <summary>
    /// Decides whether a bubbled page tap may apply a fallback focus action.
    /// The native TextBox must own every tap inside the editor so it can preserve
    /// its real caret and line hit-testing behavior.
    /// </summary>
    public static class EditorTapRouting
    {
        public static bool ShouldPreserveNativeHandling(bool tappedInsideEditor, bool tappedInsideAuxiliaryControl)
            => tappedInsideEditor || tappedInsideAuxiliaryControl;

        /// <summary>
        /// Treats the whole rendered TextBox surface as editor space, including visual space
        /// below the last logical line. That space must not be converted into a new document
        /// line or into a forced caret position.
        /// </summary>
        public static bool IsWithinEditorBounds(double x, double y, double width, double height)
            => width > 0 && height > 0 && x >= 0 && y >= 0 && x <= width && y <= height;
    }
}
