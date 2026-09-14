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

// Make names from System.Collections available here without writing their full prefix each time. This does
// not run that library's code.
using System.Collections;

// Place this file's definitions in the BitExplorer.Core naming group, which prevents clashes with names in
// other groups.
namespace BitExplorer.Core;

/// <summary>Navigation intentions supplied by the UI; the core does not need to know about keyboard event types.</summary>
public enum SelectionNavigation { Left, Right, Up, Down, Home, End, PageUp, PageDown }

/// <summary>
/// Selected physical file bits and navigation anchors, independent of their interpreted value order.
/// Changes exceeding the inspection limit leave membership and anchors unchanged.
/// </summary>
/// <remarks>
/// Membership answers "which source bits are highlighted?" and is stored in file
/// order. A NamedField separately answers "in what order do those bits form a value?"
/// Physical address 8, for example, is the leftmost bit of the second byte.
/// No byte values are read or changed by this class.
/// </remarks>
public sealed class BitSelection
{
    /// <summary>The inspection-selection limit: 65,536 bits, equivalent to 8 KiB of complete bytes.</summary>
    public const int MaximumSelectedBits = 65_536;
    // SortedSet removes duplicates and provides consistent ascending enumeration.
    // The wrapper prevents callers from modifying the set without validation/events.
    private readonly SortedSet<long> _bits = [];
    // Reserve _view to hold a collection that this reference permits us to read, but not change; setup can
    // supply its value, otherwise the type's default is used.
    private readonly IReadOnlyCollection<long> _view;
    // Reserve _bitLength to hold a whole number with room for larger values; setup can supply its value,
    // otherwise the type's default is used.
    private long _bitLength;

    /// <summary>Creates an empty selection; SetDocumentLength establishes the valid address range.</summary>
    public BitSelection() => _view = new ReadOnlyBits(_bits);

    /// <summary>A read-only, live view of selected physical addresses in ascending order.</summary>
    public IReadOnlyCollection<long> Bits => _view;
    /// <summary>The starting point retained while Shift extends a range; -1 means there is no anchor.</summary>
    public long Anchor { get; private set; } = -1;
    /// <summary>The current cursor address, which can remain on a bit that Ctrl just deselected.</summary>
    public long ActiveBit { get; private set; } = -1;
    /// <summary>The active bit's zero-based byte offset, or -1 when there is no active bit.</summary>
    // Integer division discards the within-byte remainder: addresses 8 through 15
    // all belong to byte 1. checked prevents a too-large long from silently wrapping.
    public int ActiveByteOffset => ActiveBit >= 0 ? checked((int)(ActiveBit / 8)) : -1;
    /// <summary>Notifies observers after a successful selection or navigation update.</summary>
    public event EventHandler? Changed;
    /// <summary>Notifies the UI that an attempted selection exceeded the limit; current state remains intact.</summary>
    public event EventHandler? SelectionLimitReached;

    /// <summary>Tests membership of one physical address without exposing the underlying set.</summary>
    public bool Contains(long bit) => _bits.Contains(bit);

    /// <summary>Returns true if any of a byte's eight physical bits is selected.</summary>
    public bool ByteHasSelection(int offset)
    {
        // Remember offset, converted to long multiplied by 8 as first.
        long first = (long)offset * 8;
        // Repeat with int bit = 0 as the starting state, while bit is less than 8; after each pass,
        // increase bit by one. On each pass, if _bits.Contains(first + bit) is true, return true (yes) to
        // the caller and leave this method.
        for (int bit = 0; bit < 8; bit++) if (_bits.Contains(first + bit)) return true;
        // Return false (no) to the caller and leave this method.
        return false;
    }

    /// <summary>Sets the valid address range and removes any selection left beyond its end.</summary>
    /// <param name="bitLength">A count of bits, not bytes; valid addresses are zero through this count minus one.</param>
    /// <remarks>When the active address no longer exists, it falls back to the greatest retained bit or -1.</remarks>
    public void SetDocumentLength(long bitLength)
    {
        // Reject bitLength if it is below zero.
        ArgumentOutOfRangeException.ThrowIfNegative(bitLength);
        // Set _bitLength to bitLength.
        _bitLength = bitLength;
        // Remember whether the number of items removed because they pass the condition after => is greater
        // than 0 as changed.
        bool changed = _bits.RemoveWhere(bit => bit >= bitLength) > 0;
        // Work out repaired navigation state before publishing one coherent change.
        long active = ActiveBit >= bitLength ? (_bits.Count > 0 ? _bits.Max : -1) : ActiveBit;
        // Remember active when Anchor is at least bitLength; otherwise Anchor as anchor.
        long anchor = Anchor >= bitLength ? active : Anchor;
        // If changed is true, or active differs from ActiveBit, or anchor differs from Anchor, run the
        // following grouped instructions.
        if (changed || active != ActiveBit || anchor != Anchor)
        {
            // Set ActiveBit to active.
            ActiveBit = active;
            // Set Anchor to anchor.
            Anchor = anchor;
            // If Changed is present, perform .Invoke(this, EventArgs.Empty); ?. skips this call when there
            // is no recipient.
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>Clears membership and both navigation positions, then notifies observers.</summary>
    public void Clear() => Commit([], -1, -1);

    /// <summary>Replaces membership, ignoring invalid addresses and duplicates, if the result fits the limit.</summary>
    /// <returns>False on a limit violation; true after committing the valid, possibly empty result.</returns>
    public bool SetSelection(IEnumerable<long> bits)
    {
        // Reject bits immediately if it is null (no object was supplied).
        ArgumentNullException.ThrowIfNull(bits);
        // Build separately: a rejected request must not leave half a selection behind.
        var replacement = ReadBounded(bits);
        // If replacement matches null, return the result returned by Reject() to the caller and leave this
        // method.
        if (replacement is null) return Reject();
        // Call Commit: Installs already-validated state and sends one notification after all three parts
        // agree.
        Commit(replacement, replacement.Count > 0 ? replacement.Min : -1, replacement.Count > 0 ? replacement.Max : -1);
        // Return true (yes) to the caller and leave this method.
        return true;
    }

    /// <summary>Selects an inclusive range and optionally retains a previous selection for Ctrl-drag.</summary>
    /// <param name="start">The drag anchor. Its direction is preserved even when start is greater than end.</param>
    /// <param name="end">The active endpoint; both endpoints are clipped to the existing document.</param>
    /// <param name="wholeBytes">Expands the range to byte boundaries, without going past the document.</param>
    /// <param name="preserve">Optional old membership to combine with the new range.</param>
    /// <returns>False for an empty document or a range exceeding the selection limit.</returns>
    public bool SelectRange(long start, long end, bool wholeBytes, IEnumerable<long>? preserve = null)
    {
        // If _bitLength equals 0, return false (no) to the caller and leave this method.
        if (_bitLength == 0) return false;
        // Set start to a value limited to the minimum and maximum supplied to Math.Clamp.
        start = Math.Clamp(start, 0, _bitLength - 1);
        // Set end to a value limited to the minimum and maximum supplied to Math.Clamp.
        end = Math.Clamp(end, 0, _bitLength - 1);
        // Membership is ascending, but the original start/end still describe the
        // user's drag direction and become Anchor/ActiveBit at commit time.
        long first = Math.Min(start, end);
        // Remember the larger of the two supplied numbers as last.
        long last = Math.Max(start, end);
        // If wholeBytes is true, run the following grouped instructions.
        if (wholeBytes)
        {
            // Divide then multiply to round down to a byte's first bit. Adding
            // seven reaches its last bit; Min also supports a partial final byte.
            first = first / 8 * 8;
            // Set last to the smaller of the two supplied numbers.
            last = Math.Min(last / 8 * 8 + 7, _bitLength - 1);
        }
        // The +1 counts both endpoints. Reject a large range before allocating
        // its members, then also check the union with any preserved selections.
        if (last - first + 1 > MaximumSelectedBits) return Reject();
        // Remember an empty collection when preserve matches null; otherwise the result returned by
        // ReadBounded(...) as replacement.
        var replacement = preserve is null ? [] : ReadBounded(preserve);
        // If replacement matches null, return the result returned by Reject() to the caller and leave this
        // method.
        if (replacement is null) return Reject();
        // Repeat with long bit = first as the starting state, while bit is at most last; after each pass,
        // increase bit by one. The braces contain one pass.
        for (long bit = first; bit <= last; bit++)
        {
            // Add bit to replacement.
            replacement.Add(bit);
            // If replacement.Count is greater than MaximumSelectedBits, return the result returned by
            // Reject() to the caller and leave this method.
            if (replacement.Count > MaximumSelectedBits) return Reject();
        }
        // Call Commit: Installs already-validated state and sends one notification after all three parts
        // agree.
        Commit(replacement, start, end);
        // Return true (yes) to the caller and leave this method.
        return true;
    }

    /// <summary>Applies pointer or navigation selection without exposing mutable anchor state.</summary>
    /// <param name="bit">The clicked or navigated-to physical address.</param>
    /// <param name="wholeByte">Whether the action selects an entire byte instead of one bit.</param>
    /// <param name="extend">Shift behavior: retain the old anchor and select through this address.</param>
    /// <param name="toggle">Ctrl behavior: add/remove a group, or preserve old groups while extending.</param>
    public bool SelectAt(long bit, bool wholeByte, bool extend = false, bool toggle = false)
    {
        // If bit is less than 0, or bit is at least _bitLength, return false (no) to the caller and leave
        // this method.
        if (bit < 0 || bit >= _bitLength) return false;
        // Remember Anchor when both extend is true and Anchor is at least 0; otherwise bit as anchor.
        long anchor = extend && Anchor >= 0 ? Anchor : bit;
        // If extend is true, return the result returned by SelectRange(...) to the caller and leave this
        // method.
        if (extend) return SelectRange(anchor, bit, wholeByte, toggle ? _bits : null);
        // If it is not the case that toggle is true, return the result returned by SelectRange(...) to the
        // caller and leave this method.
        if (!toggle) return SelectRange(bit, bit, wholeByte);

        // Remember bit divided by 8 multiplied by 8 when wholeByte is true; otherwise bit as first.
        long first = wholeByte ? bit / 8 * 8 : bit;
        // Remember the smaller of the two supplied numbers when wholeByte is true; otherwise bit as last.
        long last = wholeByte ? Math.Min(first + 7, _bitLength - 1) : bit;
        // Remember a new SortedSet<long> object using the inputs in parentheses as replacement.
        var replacement = new SortedSet<long>(_bits);
        // Ctrl-click removes a complete selected group. For a partly selected
        // byte, it first fills in all eight bits instead of inverting each one.
        bool allSelected = true;
        // Repeat with long address = first as the starting state, while address is at most last; after each
        // pass, increase address by one. On each pass, combine allSelected with _bits.Contains(address)
        // using AND and keep the result in allSelected. For true/false values, both must be true.
        for (long address = first; address <= last; address++) allSelected &= _bits.Contains(address);
        // Repeat with long address = first as the starting state, while address is at most last; after each
        // pass, increase address by one. On each pass, if allSelected is true, call
        // replacement.Remove(...); the values in parentheses are the inputs; otherwise, add address to
        // replacement.
        for (long address = first; address <= last; address++)
            // If allSelected is true, call replacement.Remove(...); the values in parentheses are the
            // inputs. Otherwise, add address to replacement.
            if (allSelected) replacement.Remove(address); else replacement.Add(address);
        // If replacement.Count is greater than MaximumSelectedBits, return the result returned by Reject()
        // to the caller and leave this method.
        if (replacement.Count > MaximumSelectedBits) return Reject();
        // Call Commit: Installs already-validated state and sends one notification after all three parts
        // agree.
        Commit(replacement, anchor, bit);
        // Return true (yes) to the caller and leave this method.
        return true;
    }

    /// <summary>Moves the cursor without changing membership or the range anchor.</summary>
    /// <remarks>Invalid addresses are ignored. This is useful when editing a byte already under the pointer.</remarks>
    public void SetActiveBit(long bit)
    {
        // If bit is less than 0, or bit is at least _bitLength, or bit equals ActiveBit, leave this method
        // immediately.
        if (bit < 0 || bit >= _bitLength || bit == ActiveBit) return;
        // Set ActiveBit to bit.
        ActiveBit = bit;
        // If Changed is present, perform .Invoke(this, EventArgs.Empty); ?. skips this call when there is
        // no recipient.
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Turns arrow, Home/End, or page movement into the same selection operations used by the mouse.</summary>
    /// <param name="direction">The requested movement, independent of a particular UI key type.</param>
    /// <param name="individualBits">True moves/selects one bit; false moves/selects complete bytes.</param>
    /// <param name="bytesPerRow">The current grouping, used by vertical and row-boundary movement.</param>
    /// <param name="visibleRows">The number of rows to move for PageUp/PageDown, with a minimum of one.</param>
    /// <param name="extend">Keeps the original anchor, as Shift does.</param>
    /// <param name="documentBoundary">Makes Home/End target the whole file rather than the current row.</param>
    public bool Navigate(SelectionNavigation direction, bool individualBits, int bytesPerRow, int visibleRows, bool extend = false, bool documentBoundary = false)
    {
        // If _bitLength equals 0, return false (no) to the caller and leave this method.
        if (_bitLength == 0) return false;
        // Reject bytesPerRow unless it is greater than zero.
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(bytesPerRow);
        // Remember the larger of the two supplied numbers as current.
        long current = Math.Max(0, ActiveBit);
        // Remember 1 when individualBits is true; otherwise 8 as step.
        long step = individualBits ? 1 : 8;
        // Convert row size to bits once, since every navigation address uses that
        // unit. The L makes the multiplication use long arithmetic from the start.
        long rowBits = bytesPerRow * 8L;
        // Remember the result selected by matching direction to one of the cases below as next.
        long next = direction switch
        {
            // For SelectionNavigation.Left, use current minus step.
            SelectionNavigation.Left => current - step,
            // For SelectionNavigation.Right, use current + step (addition, or joining text).
            SelectionNavigation.Right => current + step,
            // For SelectionNavigation.Up, use current minus rowBits.
            SelectionNavigation.Up => current - rowBits,
            // For SelectionNavigation.Down, use current + rowBits (addition, or joining text).
            SelectionNavigation.Down => current + rowBits,
            // For SelectionNavigation.Home, use 0 when documentBoundary is true; otherwise current divided
            // by rowBits multiplied by rowBits.
            SelectionNavigation.Home => documentBoundary ? 0 : current / rowBits * rowBits,
            // For SelectionNavigation.End, use _bitLength minus 1 when documentBoundary is true; otherwise
            // current / rowBits * rowBits + rowBits (addition, or joining text) minus step.
            SelectionNavigation.End => documentBoundary ? _bitLength - 1 : current / rowBits * rowBits + rowBits - step,
            // For SelectionNavigation.PageUp, use current minus the larger of the two supplied numbers
            // multiplied by rowBits.
            SelectionNavigation.PageUp => current - Math.Max(1, visibleRows) * rowBits,
            // For SelectionNavigation.PageDown, use current + the larger of the two supplied numbers
            // multiplied by rowBits (addition, or joining text).
            SelectionNavigation.PageDown => current + Math.Max(1, visibleRows) * rowBits,
            // For any remaining case, report the error shown below.
            _ => throw new ArgumentOutOfRangeException(nameof(direction))
        };
        // Set next to a value limited to the minimum and maximum supplied to Math.Clamp.
        next = Math.Clamp(next, 0, _bitLength - 1);
        // Byte navigation lands on a byte's first physical bit; SelectAt then
        // expands that position into a whole-byte selection when appropriate.
        if (!individualBits) next = next / 8 * 8;
        // Return the result returned by SelectAt(...) to the caller and leave this method.
        return SelectAt(next, !individualBits, extend);
    }

    /// <summary>Collects valid distinct addresses and returns null as soon as their count exceeds the cap.</summary>
    /// <remarks>The input is expected to be a finite sequence; duplicates do not consume selection capacity.</remarks>
    private SortedSet<long>? ReadBounded(IEnumerable<long> bits)
    {
        // Remember a new SortedSet<long> object as result.
        var result = new SortedSet<long>();
        // Take each item from bits in turn, call the current item bit, and run the following grouped
        // instructions.
        foreach (long bit in bits)
        {
            // If both bit is at least 0 and bit is less than _bitLength, add bit to result.
            if (bit >= 0 && bit < _bitLength) result.Add(bit);
            // If result.Count is greater than MaximumSelectedBits, return null (no value) to the caller and
            // leave this method.
            if (result.Count > MaximumSelectedBits) return null;
        }
        // Return result to the caller and leave this method.
        return result;
    }

    /// <summary>Installs already-validated state and sends one notification after all three parts agree.</summary>
    // Keep the same set instance so previously obtained read-only views remain live.
    private void Commit(SortedSet<long> bits, long anchor, long active)
    {
        // Remove all current items from _bits.
        _bits.Clear();
        // Add every item from bits to _bits, keeping only one copy of each distinct item.
        _bits.UnionWith(bits);
        // Set Anchor to anchor.
        Anchor = anchor;
        // Set ActiveBit to active.
        ActiveBit = active;
        // If Changed is present, perform .Invoke(this, EventArgs.Empty); ?. skips this call when there is
        // no recipient.
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Reports a rejected request without clearing membership or moving the cursor.</summary>
    private bool Reject()
    {
        // If SelectionLimitReached is present, perform .Invoke(this, EventArgs.Empty); ?. skips this call
        // when there is no recipient.
        SelectionLimitReached?.Invoke(this, EventArgs.Empty);
        // Return false (no) to the caller and leave this method.
        return false;
    }

    /// <summary>Exposes enumeration and Count while hiding SortedSet's mutation methods.</summary>
    // Returning the set as IReadOnlyCollection alone would not be enough: callers
    // could cast it back to a mutable collection. This separate wrapper prevents that.
    private sealed class ReadOnlyBits(SortedSet<long> bits) : IReadOnlyCollection<long>
    {
        // Expose Count as a whole number. Reading it computes bits.Count.
        public int Count => bits.Count;
        // Define GetEnumerator, an operation called using parentheses. Its declared result is an object or
        // value of type IEnumerator<long>. The => form supplies its body in one expression.
        public IEnumerator<long> GetEnumerator() => bits.GetEnumerator();
        // Define GetEnumerator, an operation called using parentheses. Its declared result is an object or
        // value of type IEnumerator. The => form supplies its body in one expression.
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
