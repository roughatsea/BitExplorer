using BitExplorer.Core;

internal static partial class Program
{
    private static void RunSelectionTests()
    {
        Test("bit selection keeps drag direction and clips whole-byte ranges at the file boundary", SelectionRangeBoundaries);
        Test("Ctrl toggles and Ctrl+Shift ranges retain independent selected groups", SelectionToggleAndExtend);
        Test("oversized selections reject atomically without changing membership or navigation", SelectionLimitIsAtomic);
        Test("selection exposes a read-only live view and prunes it when the document shrinks", SelectionViewAndPruning);
        Test("active-byte navigation and clear preserve the documented selection invariants", SelectionNavigation);
        Test("keyboard navigation honors byte groups, row ends, and Shift anchors", SelectionKeyboardNavigation);
    }

    private static void SelectionRangeBoundaries()
    {
        var selection = new BitSelection();
        selection.SetDocumentLength(19);
        Equal(true, selection.SelectRange(17, 9, wholeBytes: false));
        Sequence(Enumerable.Range(9, 9).Select(i => (long)i), selection.Bits);
        Equal(17L, selection.Anchor);
        Equal(9L, selection.ActiveBit);
        Equal(1, selection.ActiveByteOffset);

        Equal(true, selection.SelectRange(17, 8, wholeBytes: true));
        Sequence(Enumerable.Range(8, 11).Select(i => (long)i), selection.Bits);
        Equal(17L, selection.Anchor);
        Equal(8L, selection.ActiveBit);
        Equal(true, selection.SelectRange(-10, 500, wholeBytes: true));
        Sequence(Enumerable.Range(0, 19).Select(i => (long)i), selection.Bits);
        Equal(0L, selection.Anchor);
        Equal(18L, selection.ActiveBit);
        Equal(false, selection.SelectAt(19, wholeByte: false));
        Equal(false, selection.SelectAt(-1, wholeByte: false));
        Equal(18L, selection.ActiveBit);
    }

    private static void SelectionToggleAndExtend()
    {
        var selection = new BitSelection();
        selection.SetDocumentLength(19);
        selection.SelectAt(1, wholeByte: false);
        selection.SelectAt(9, wholeByte: false, toggle: true);
        Sequence(new long[] { 1, 9 }, selection.Bits);
        selection.SelectAt(9, wholeByte: false, toggle: true);
        Sequence(new long[] { 1 }, selection.Bits);
        selection.SelectAt(12, wholeByte: false, extend: true, toggle: true);
        Sequence(new long[] { 1, 9, 10, 11, 12 }, selection.Bits);
        Equal(9L, selection.Anchor);
        Equal(12L, selection.ActiveBit);

        selection.SelectAt(3, wholeByte: true, toggle: true);
        Sequence(Enumerable.Range(0, 8).Concat(Enumerable.Range(9, 4)).Select(i => (long)i), selection.Bits);
        selection.SelectAt(3, wholeByte: true, toggle: true);
        Sequence(new long[] { 9, 10, 11, 12 }, selection.Bits);
        selection.SelectAt(18, wholeByte: true, toggle: true);
        Sequence(new long[] { 9, 10, 11, 12, 16, 17, 18 }, selection.Bits);
        selection.SelectAt(18, wholeByte: true, toggle: true);
        Sequence(new long[] { 9, 10, 11, 12 }, selection.Bits);
    }

    private static void SelectionLimitIsAtomic()
    {
        var selection = new BitSelection();
        selection.SetDocumentLength(100_000);
        selection.SetSelection([5, 6]);
        int changes = 0, rejections = 0;
        selection.Changed += (_, _) => changes++;
        selection.SelectionLimitReached += (_, _) => rejections++;

        Equal(false, selection.SelectRange(0, BitSelection.MaximumSelectedBits, wholeBytes: false));
        Equal(false, selection.SetSelection(Enumerable.Range(0, BitSelection.MaximumSelectedBits + 1).Select(i => (long)i)));
        Equal(false, selection.SelectRange(0, BitSelection.MaximumSelectedBits - 1, wholeBytes: false, preserve: [70_000]));
        Sequence(new long[] { 5, 6 }, selection.Bits);
        Equal(5L, selection.Anchor);
        Equal(6L, selection.ActiveBit);
        Equal(0, changes);
        Equal(3, rejections);

        Equal(true, selection.SelectRange(0, BitSelection.MaximumSelectedBits - 1, wholeBytes: false));
        Equal(BitSelection.MaximumSelectedBits, selection.Bits.Count);
        Equal(false, selection.SelectAt(70_000, wholeByte: true, toggle: true));
        Equal(BitSelection.MaximumSelectedBits, selection.Bits.Count);
        Equal(0L, selection.Anchor);
        Equal((long)BitSelection.MaximumSelectedBits - 1, selection.ActiveBit);
        Equal(1, changes);
        Equal(4, rejections);
    }

    private static void SelectionViewAndPruning()
    {
        var selection = new BitSelection();
        selection.SetDocumentLength(16);
        IReadOnlyCollection<long> view = selection.Bits;
        Equal(false, view is ICollection<long>);
        Equal(true, selection.SetSelection([-1, 15, 1, 9, 1, 16]));
        Sequence(new long[] { 1, 9, 15 }, view);
        selection.SetDocumentLength(8);
        Sequence(new long[] { 1 }, view);
        Equal(1L, selection.Anchor);
        Equal(1L, selection.ActiveBit);
        selection.SetDocumentLength(0);
        Equal(0, view.Count);
        Equal(-1L, selection.Anchor);
        Equal(-1L, selection.ActiveBit);
        Equal(-1, selection.ActiveByteOffset);
        Equal(false, selection.SelectRange(0, 7, wholeBytes: true));
        Throws<ArgumentOutOfRangeException>(() => selection.SetDocumentLength(-1));
    }

    private static void SelectionNavigation()
    {
        var selection = new BitSelection();
        selection.SetDocumentLength(32);
        selection.SetSelection([0, 14, 20]);
        Equal(true, selection.Contains(14));
        Equal(false, selection.Contains(15));
        Equal(true, selection.ByteHasSelection(0));
        Equal(true, selection.ByteHasSelection(1));
        Equal(true, selection.ByteHasSelection(2));
        Equal(false, selection.ByteHasSelection(3));
        selection.SetActiveBit(31);
        Sequence(new long[] { 0, 14, 20 }, selection.Bits);
        Equal(0L, selection.Anchor);
        Equal(31L, selection.ActiveBit);
        Equal(3, selection.ActiveByteOffset);
        selection.SetActiveBit(32);
        Equal(31L, selection.ActiveBit);
        selection.Clear();
        Equal(0, selection.Bits.Count);
        Equal(-1L, selection.Anchor);
        Equal(-1L, selection.ActiveBit);
    }

    private static void SelectionKeyboardNavigation()
    {
        var selection = new BitSelection();
        selection.SetDocumentLength(80);
        selection.SelectAt(10, wholeByte: false);
        selection.Navigate(BitExplorer.Core.SelectionNavigation.End, individualBits: true, bytesPerRow: 2, visibleRows: 2, extend: true);
        Sequence(Enumerable.Range(10, 6).Select(i => (long)i), selection.Bits);
        Equal(10L, selection.Anchor);
        Equal(15L, selection.ActiveBit);
        selection.Navigate(BitExplorer.Core.SelectionNavigation.Down, individualBits: false, bytesPerRow: 2, visibleRows: 2);
        Sequence(Enumerable.Range(24, 8).Select(i => (long)i), selection.Bits);
        Equal(24L, selection.ActiveBit);
        selection.Navigate(BitExplorer.Core.SelectionNavigation.End, individualBits: false, bytesPerRow: 2, visibleRows: 2, documentBoundary: true);
        Sequence(Enumerable.Range(72, 8).Select(i => (long)i), selection.Bits);
        selection.Navigate(BitExplorer.Core.SelectionNavigation.Home, individualBits: true, bytesPerRow: 2, visibleRows: 2, documentBoundary: true);
        Sequence(new long[] { 0 }, selection.Bits);
    }
}
