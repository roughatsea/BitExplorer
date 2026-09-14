// READING THIS FILE
// A program is a set of instructions. This file defines some of those instructions;
// defining a method does not run it. A call such as Refresh() asks it to run.
// Comments explain the next instruction or the whole block introduced below them.
// Within a running block, instructions normally run from top to bottom. Braces { }
// group a body; a closing brace ends that group. Blank lines only separate ideas.
// A semicolon ends an instruction. A long instruction can continue on several lines;
// its commas, closing parentheses and braces belong to the explanation at its start.
// Names identify values or operations: x = y stores y in x; x == y compares them.
// A dot selects something belonging to an object, and (...) supplies inputs to a call.
// See docs/ReadingTheCode.md for types, symbols, examples, and the application map.

// Make names from BitExplorer.Core available here without writing their full prefix each time. This does
// not run that library's code.
using BitExplorer.Core;

// This second part of Program reuses the small test runner and assertions from
// Program.cs. These tests need no document bytes or WPF controls: selection is
// about physical addresses, range boundaries, and navigation state only.
internal static partial class Program
{
    /// <summary>Registers independent selection scenarios alongside the existing core regression tests.</summary>
    private static void RunSelectionTests()
    {
        // Run SelectionRangeBoundaries as a test; the quoted text names the behavior reported in the
        // results.
        Test("bit selection keeps drag direction and clips whole-byte ranges at the file boundary", SelectionRangeBoundaries);
        // Run SelectionToggleAndExtend as a test; the quoted text names the behavior reported in the
        // results.
        Test("Ctrl toggles and Ctrl+Shift ranges retain independent selected groups", SelectionToggleAndExtend);
        // Run SelectionLimitIsAtomic as a test; the quoted text names the behavior reported in the results.
        Test("oversized selections reject atomically without changing membership or navigation", SelectionLimitIsAtomic);
        // Run SelectionViewAndPruning as a test; the quoted text names the behavior reported in the
        // results.
        Test("selection exposes a read-only live view and prunes it when the document shrinks", SelectionViewAndPruning);
        // Run SelectionNavigation as a test; the quoted text names the behavior reported in the results.
        Test("active-byte navigation and clear preserve the documented selection invariants", SelectionNavigation);
        // Run SelectionKeyboardNavigation as a test; the quoted text names the behavior reported in the
        // results.
        Test("keyboard navigation honors byte groups, row ends, and Shift anchors", SelectionKeyboardNavigation);
    }

    /// <summary>Checks reversed drags, whole-byte expansion, and clipping at a partial final byte.</summary>
    /// <remarks>A synthetic 19-bit document tests boundary handling independently of ordinary eight-bit file bytes.</remarks>
    private static void SelectionRangeBoundaries()
    {
        // Remember a new BitSelection object as selection.
        var selection = new BitSelection();
        // Call selection.SetDocumentLength: Sets the valid address range and removes any selection left
        // beyond its end.
        selection.SetDocumentLength(19);
        // Check that selection.SelectRange(17, 9, wholeBytes: false) equals the expected true; a mismatch
        // fails this test.
        Equal(true, selection.SelectRange(17, 9, wholeBytes: false));
        // Membership is sorted even though the drag runs backward. Anchor and
        // ActiveBit must retain the original direction for subsequent Shift moves.
        Sequence(Enumerable.Range(9, 9).Select(i => (long)i), selection.Bits);
        // Check that selection.Anchor equals the expected 17L; a mismatch fails this test.
        Equal(17L, selection.Anchor);
        // Check that selection.ActiveBit equals the expected 9L; a mismatch fails this test.
        Equal(9L, selection.ActiveBit);
        // Check that selection.ActiveByteOffset equals the expected 1; a mismatch fails this test.
        Equal(1, selection.ActiveByteOffset);

        // The range reaches bytes 1 and 2. The last byte is partial, so expansion
        // must stop at bit 18 rather than manufacturing bits 19 through 23.
        Equal(true, selection.SelectRange(17, 8, wholeBytes: true));
        // Check that selection.Bits has the same items in the same order as Enumerable.Range(8,
        // 11).Select(i => (long)i).
        Sequence(Enumerable.Range(8, 11).Select(i => (long)i), selection.Bits);
        // Check that selection.Anchor equals the expected 17L; a mismatch fails this test.
        Equal(17L, selection.Anchor);
        // Check that selection.ActiveBit equals the expected 8L; a mismatch fails this test.
        Equal(8L, selection.ActiveBit);
        // Check that selection.SelectRange(-10, 500, wholeBytes: true) equals the expected true; a mismatch
        // fails this test.
        Equal(true, selection.SelectRange(-10, 500, wholeBytes: true));
        // Check that selection.Bits has the same items in the same order as Enumerable.Range(0,
        // 19).Select(i => (long)i).
        Sequence(Enumerable.Range(0, 19).Select(i => (long)i), selection.Bits);
        // Check that selection.Anchor equals the expected 0L; a mismatch fails this test.
        Equal(0L, selection.Anchor);
        // Check that selection.ActiveBit equals the expected 18L; a mismatch fails this test.
        Equal(18L, selection.ActiveBit);
        // Check that selection.SelectAt(19, wholeByte: false) equals the expected false; a mismatch fails
        // this test.
        Equal(false, selection.SelectAt(19, wholeByte: false));
        // Check that selection.SelectAt(-1, wholeByte: false) equals the expected false; a mismatch fails
        // this test.
        Equal(false, selection.SelectAt(-1, wholeByte: false));
        // Check that selection.ActiveBit equals the expected 18L; a mismatch fails this test.
        Equal(18L, selection.ActiveBit);
    }

    /// <summary>Models Ctrl-click and Ctrl+Shift: toggling one group must preserve other groups.</summary>
    private static void SelectionToggleAndExtend()
    {
        // Remember a new BitSelection object as selection.
        var selection = new BitSelection();
        // Call selection.SetDocumentLength: Sets the valid address range and removes any selection left
        // beyond its end.
        selection.SetDocumentLength(19);
        // Call selection.SelectAt: Applies pointer or navigation selection without exposing mutable anchor
        // state.
        selection.SelectAt(1, wholeByte: false);
        // Call selection.SelectAt: Applies pointer or navigation selection without exposing mutable anchor
        // state.
        selection.SelectAt(9, wholeByte: false, toggle: true);
        // Check that selection.Bits has the same items in the same order as new long[] { 1, 9 }.
        Sequence(new long[] { 1, 9 }, selection.Bits);
        // Call selection.SelectAt: Applies pointer or navigation selection without exposing mutable anchor
        // state.
        selection.SelectAt(9, wholeByte: false, toggle: true);
        // Check that selection.Bits has the same items in the same order as new long[] { 1 }.
        Sequence(new long[] { 1 }, selection.Bits);
        // Ctrl removed bit 9, but the cursor/anchor stayed there. Ctrl+Shift now
        // extends from that anchor while retaining the separately selected bit 1.
        selection.SelectAt(12, wholeByte: false, extend: true, toggle: true);
        // Check that selection.Bits has the same items in the same order as new long[] { 1, 9, 10, 11, 12
        // }.
        Sequence(new long[] { 1, 9, 10, 11, 12 }, selection.Bits);
        // Check that selection.Anchor equals the expected 9L; a mismatch fails this test.
        Equal(9L, selection.Anchor);
        // Check that selection.ActiveBit equals the expected 12L; a mismatch fails this test.
        Equal(12L, selection.ActiveBit);

        // The first click fills in a partly selected byte; the next click removes
        // that whole byte. Bits in another selected group must be left alone.
        selection.SelectAt(3, wholeByte: true, toggle: true);
        // Check that selection.Bits has the same items in the same order as Enumerable.Range(0,
        // 8).Concat(Enumerable.Range(9, 4)).Select(i => (long)i).
        Sequence(Enumerable.Range(0, 8).Concat(Enumerable.Range(9, 4)).Select(i => (long)i), selection.Bits);
        // Call selection.SelectAt: Applies pointer or navigation selection without exposing mutable anchor
        // state.
        selection.SelectAt(3, wholeByte: true, toggle: true);
        // Check that selection.Bits has the same items in the same order as new long[] { 9, 10, 11, 12 }.
        Sequence(new long[] { 9, 10, 11, 12 }, selection.Bits);
        // Call selection.SelectAt: Applies pointer or navigation selection without exposing mutable anchor
        // state.
        selection.SelectAt(18, wholeByte: true, toggle: true);
        // Check that selection.Bits has the same items in the same order as new long[] { 9, 10, 11, 12, 16,
        // 17, 18 }.
        Sequence(new long[] { 9, 10, 11, 12, 16, 17, 18 }, selection.Bits);
        // Call selection.SelectAt: Applies pointer or navigation selection without exposing mutable anchor
        // state.
        selection.SelectAt(18, wholeByte: true, toggle: true);
        // Check that selection.Bits has the same items in the same order as new long[] { 9, 10, 11, 12 }.
        Sequence(new long[] { 9, 10, 11, 12 }, selection.Bits);
    }

    /// <summary>Attempts several oversized selections and proves that rejection changes neither membership nor anchors.</summary>
    private static void SelectionLimitIsAtomic()
    {
        // Remember a new BitSelection object as selection.
        var selection = new BitSelection();
        // Call selection.SetDocumentLength: Sets the valid address range and removes any selection left
        // beyond its end.
        selection.SetDocumentLength(100_000);
        // Request selection of the supplied physical bit addresses; the selection code checks its bounds
        // and limit.
        selection.SetSelection([5, 6]);
        // Events are part of the contract too: a rejected request should notify
        // the limit warning, not tell observers that selection changed.
        int changes = 0, rejections = 0;
        // Register the operation after => as a listener for selection.Changed; the listener runs when that
        // event is raised.
        selection.Changed += (_, _) => changes++;
        // Register the operation after => as a listener for selection.SelectionLimitReached; the listener
        // runs when that event is raised.
        selection.SelectionLimitReached += (_, _) => rejections++;

        // Check that selection.SelectRange(0, BitSelection.MaximumSelectedBits, wholeBytes: false) equals
        // the expected false; a mismatch fails this test.
        Equal(false, selection.SelectRange(0, BitSelection.MaximumSelectedBits, wholeBytes: false));
        // Check that selection.SetSelection(Enumerable.Range(0, BitSelection.MaximumSelectedBits +
        // 1).Select(i => (long)i)) equals the expected false; a mismatch fails this test.
        Equal(false, selection.SetSelection(Enumerable.Range(0, BitSelection.MaximumSelectedBits + 1).Select(i => (long)i)));
        // This range alone fits exactly, but its union with the extra preserved
        // bit does not. Capacity must be checked after combining both sources.
        Equal(false, selection.SelectRange(0, BitSelection.MaximumSelectedBits - 1, wholeBytes: false, preserve: [70_000]));
        // Check that selection.Bits has the same items in the same order as new long[] { 5, 6 }.
        Sequence(new long[] { 5, 6 }, selection.Bits);
        // Check that selection.Anchor equals the expected 5L; a mismatch fails this test.
        Equal(5L, selection.Anchor);
        // Check that selection.ActiveBit equals the expected 6L; a mismatch fails this test.
        Equal(6L, selection.ActiveBit);
        // Check that changes equals the expected 0; a mismatch fails this test.
        Equal(0, changes);
        // Check that rejections equals the expected 3; a mismatch fails this test.
        Equal(3, rejections);

        // At the exact limit, selection must succeed; one additional whole byte
        // must then fail without replacing this valid full-capacity selection.
        Equal(true, selection.SelectRange(0, BitSelection.MaximumSelectedBits - 1, wholeBytes: false));
        // Check that selection.Bits.Count equals the expected BitSelection.MaximumSelectedBits; a mismatch
        // fails this test.
        Equal(BitSelection.MaximumSelectedBits, selection.Bits.Count);
        // Check that selection.SelectAt(70_000, wholeByte: true, toggle: true) equals the expected false; a
        // mismatch fails this test.
        Equal(false, selection.SelectAt(70_000, wholeByte: true, toggle: true));
        // Check that selection.Bits.Count equals the expected BitSelection.MaximumSelectedBits; a mismatch
        // fails this test.
        Equal(BitSelection.MaximumSelectedBits, selection.Bits.Count);
        // Check that selection.Anchor equals the expected 0L; a mismatch fails this test.
        Equal(0L, selection.Anchor);
        // Check that selection.ActiveBit equals the expected (long)BitSelection.MaximumSelectedBits - 1; a
        // mismatch fails this test.
        Equal((long)BitSelection.MaximumSelectedBits - 1, selection.ActiveBit);
        // Check that changes equals the expected 1; a mismatch fails this test.
        Equal(1, changes);
        // Check that rejections equals the expected 4; a mismatch fails this test.
        Equal(4, rejections);
    }

    /// <summary>Checks deduplication, read-only exposure, live updates, and removal of addresses after a document shrinks.</summary>
    private static void SelectionViewAndPruning()
    {
        // Remember a new BitSelection object as selection.
        var selection = new BitSelection();
        // Call selection.SetDocumentLength: Sets the valid address range and removes any selection left
        // beyond its end.
        selection.SetDocumentLength(16);
        // Remember selection.Bits as view.
        IReadOnlyCollection<long> view = selection.Bits;
        // Merely declaring a return type read-only would still allow a cast back
        // to a mutable set. The actual object must not expose ICollection mutation.
        Equal(false, view is ICollection<long>);
        // Check that selection.SetSelection([-1, 15, 1, 9, 1, 16]) equals the expected true; a mismatch
        // fails this test.
        Equal(true, selection.SetSelection([-1, 15, 1, 9, 1, 16]));
        // Check that view has the same items in the same order as new long[] { 1, 9, 15 }.
        Sequence(new long[] { 1, 9, 15 }, view);
        // Keep the same previously obtained view while shrinking the document;
        // it should reflect the new membership rather than being a stale snapshot.
        selection.SetDocumentLength(8);
        // Check that view has the same items in the same order as new long[] { 1 }.
        Sequence(new long[] { 1 }, view);
        // Check that selection.Anchor equals the expected 1L; a mismatch fails this test.
        Equal(1L, selection.Anchor);
        // Check that selection.ActiveBit equals the expected 1L; a mismatch fails this test.
        Equal(1L, selection.ActiveBit);
        // Call selection.SetDocumentLength: Sets the valid address range and removes any selection left
        // beyond its end.
        selection.SetDocumentLength(0);
        // Check that view.Count equals the expected 0; a mismatch fails this test.
        Equal(0, view.Count);
        // Check that selection.Anchor equals the expected -1L; a mismatch fails this test.
        Equal(-1L, selection.Anchor);
        // Check that selection.ActiveBit equals the expected -1L; a mismatch fails this test.
        Equal(-1L, selection.ActiveBit);
        // Check that selection.ActiveByteOffset equals the expected -1; a mismatch fails this test.
        Equal(-1, selection.ActiveByteOffset);
        // Check that selection.SelectRange(0, 7, wholeBytes: true) equals the expected false; a mismatch
        // fails this test.
        Equal(false, selection.SelectRange(0, 7, wholeBytes: true));
        // Check that the supplied operation reports the expected exception type; accepting this invalid
        // case would fail the test.
        Throws<ArgumentOutOfRangeException>(() => selection.SetDocumentLength(-1));
    }

    /// <summary>Moving only the active cursor must not change selected bits; Clear must reset all selection state.</summary>
    private static void SelectionNavigation()
    {
        // Remember a new BitSelection object as selection.
        var selection = new BitSelection();
        // Call selection.SetDocumentLength: Sets the valid address range and removes any selection left
        // beyond its end.
        selection.SetDocumentLength(32);
        // Request selection of the supplied physical bit addresses; the selection code checks its bounds
        // and limit.
        selection.SetSelection([0, 14, 20]);
        // Check that selection.Contains(14) equals the expected true; a mismatch fails this test.
        Equal(true, selection.Contains(14));
        // Check that selection.Contains(15) equals the expected false; a mismatch fails this test.
        Equal(false, selection.Contains(15));
        // Check that selection.ByteHasSelection(0) equals the expected true; a mismatch fails this test.
        Equal(true, selection.ByteHasSelection(0));
        // Check that selection.ByteHasSelection(1) equals the expected true; a mismatch fails this test.
        Equal(true, selection.ByteHasSelection(1));
        // Check that selection.ByteHasSelection(2) equals the expected true; a mismatch fails this test.
        Equal(true, selection.ByteHasSelection(2));
        // Check that selection.ByteHasSelection(3) equals the expected false; a mismatch fails this test.
        Equal(false, selection.ByteHasSelection(3));
        // The active cursor may point to an unselected bit, such as a byte opened
        // for editing. Membership and the Shift anchor are intentionally separate.
        selection.SetActiveBit(31);
        // Check that selection.Bits has the same items in the same order as new long[] { 0, 14, 20 }.
        Sequence(new long[] { 0, 14, 20 }, selection.Bits);
        // Check that selection.Anchor equals the expected 0L; a mismatch fails this test.
        Equal(0L, selection.Anchor);
        // Check that selection.ActiveBit equals the expected 31L; a mismatch fails this test.
        Equal(31L, selection.ActiveBit);
        // Check that selection.ActiveByteOffset equals the expected 3; a mismatch fails this test.
        Equal(3, selection.ActiveByteOffset);
        // Call selection.SetActiveBit: Moves the cursor without changing membership or the range anchor.
        selection.SetActiveBit(32);
        // Check that selection.ActiveBit equals the expected 31L; a mismatch fails this test.
        Equal(31L, selection.ActiveBit);
        // Remove all current items from selection.
        selection.Clear();
        // Check that selection.Bits.Count equals the expected 0; a mismatch fails this test.
        Equal(0, selection.Bits.Count);
        // Check that selection.Anchor equals the expected -1L; a mismatch fails this test.
        Equal(-1L, selection.Anchor);
        // Check that selection.ActiveBit equals the expected -1L; a mismatch fails this test.
        Equal(-1L, selection.ActiveBit);
    }

    /// <summary>Checks Shift-to-row-end, whole-byte vertical movement, and whole-document Home/End boundaries.</summary>
    private static void SelectionKeyboardNavigation()
    {
        // Remember a new BitSelection object as selection.
        var selection = new BitSelection();
        // Call selection.SetDocumentLength: Sets the valid address range and removes any selection left
        // beyond its end.
        selection.SetDocumentLength(80);
        // Call selection.SelectAt: Applies pointer or navigation selection without exposing mutable anchor
        // state.
        selection.SelectAt(10, wholeByte: false);
        // A two-byte row contains 16 bits. Shift+End from address 10 therefore
        // selects 10 through 15, retaining 10 as the anchor. The fully qualified
        // enum name avoids confusing it with the similarly named test method above.
        selection.Navigate(BitExplorer.Core.SelectionNavigation.End, individualBits: true, bytesPerRow: 2, visibleRows: 2, extend: true);
        // Check that selection.Bits has the same items in the same order as Enumerable.Range(10,
        // 6).Select(i => (long)i).
        Sequence(Enumerable.Range(10, 6).Select(i => (long)i), selection.Bits);
        // Check that selection.Anchor equals the expected 10L; a mismatch fails this test.
        Equal(10L, selection.Anchor);
        // Check that selection.ActiveBit equals the expected 15L; a mismatch fails this test.
        Equal(15L, selection.ActiveBit);
        // Call selection.Navigate: Turns arrow, Home/End, or page movement into the same selection
        // operations used by the mouse.
        selection.Navigate(BitExplorer.Core.SelectionNavigation.Down, individualBits: false, bytesPerRow: 2, visibleRows: 2);
        // Check that selection.Bits has the same items in the same order as Enumerable.Range(24,
        // 8).Select(i => (long)i).
        Sequence(Enumerable.Range(24, 8).Select(i => (long)i), selection.Bits);
        // Check that selection.ActiveBit equals the expected 24L; a mismatch fails this test.
        Equal(24L, selection.ActiveBit);
        // Call selection.Navigate: Turns arrow, Home/End, or page movement into the same selection
        // operations used by the mouse.
        selection.Navigate(BitExplorer.Core.SelectionNavigation.End, individualBits: false, bytesPerRow: 2, visibleRows: 2, documentBoundary: true);
        // Check that selection.Bits has the same items in the same order as Enumerable.Range(72,
        // 8).Select(i => (long)i).
        Sequence(Enumerable.Range(72, 8).Select(i => (long)i), selection.Bits);
        // Call selection.Navigate: Turns arrow, Home/End, or page movement into the same selection
        // operations used by the mouse.
        selection.Navigate(BitExplorer.Core.SelectionNavigation.Home, individualBits: true, bytesPerRow: 2, visibleRows: 2, documentBoundary: true);
        // Check that selection.Bits has the same items in the same order as new long[] { 0 }.
        Sequence(new long[] { 0 }, selection.Bits);
    }
}
