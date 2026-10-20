using Slinky.Services;
using Xunit;

namespace Slinky.Tests
{
    [Trait("Category", "Unit")]
    public class EditorTapRoutingTests
    {
        [Fact]
        public void EmptyEditorTapPreservesNativeCaretHandling()
        {
            // The editor contains its first logical line even when Text is empty. A click
            // below that line must not be interpreted as a request for a nonexistent line.
            Assert.True(EditorTapRouting.ShouldPreserveNativeHandling(
                tappedInsideEditor: true,
                tappedInsideAuxiliaryControl: false));
        }

        [Fact]
        public void TapOnAnExistingNewlineCreatedLinePreservesNativeCaretHandling()
        {
            // Once Enter has created another line, the native TextBox must still receive the
            // click so it can place the caret in that actual line instead of being moved to the
            // end by the page-level handler.
            Assert.True(EditorTapRouting.ShouldPreserveNativeHandling(
                tappedInsideEditor: true,
                tappedInsideAuxiliaryControl: false));
        }

        [Fact]
        public void TapOutsideEditorUsesThePageFallback()
        {
            Assert.False(EditorTapRouting.ShouldPreserveNativeHandling(
                tappedInsideEditor: false,
                tappedInsideAuxiliaryControl: false));
        }

        [Fact]
        public void TapInsideFindBarDoesNotMoveTheEditorCaret()
        {
            Assert.True(EditorTapRouting.ShouldPreserveNativeHandling(
                tappedInsideEditor: false,
                tappedInsideAuxiliaryControl: true));
        }

        [Theory]
        [InlineData(12, 12, 400, 200, true)]
        [InlineData(12, 190, 400, 200, true)]
        [InlineData(12, 201, 400, 200, false)]
        [InlineData(-1, 12, 400, 200, false)]
        public void EditorSurfaceBoundsIncludeVisualSpaceWithoutCreatingALine(
            double x,
            double y,
            double width,
            double height,
            bool expected)
        {
            Assert.Equal(expected, EditorTapRouting.IsWithinEditorBounds(x, y, width, height));
        }
    }
}
