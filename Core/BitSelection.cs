using System.Collections;

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
    private readonly IReadOnlyCollection<long> _view;
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
        long first = (long)offset * 8;
        for (int bit = 0; bit < 8; bit++) if (_bits.Contains(first + bit)) return true;
        return false;
    }

    /// <summary>Sets the valid address range and removes any selection left beyond its end.</summary>
    /// <param name="bitLength">A count of bits, not bytes; valid addresses are zero through this count minus one.</param>
    /// <remarks>When the active address no longer exists, it falls back to the greatest retained bit or -1.</remarks>
    public void SetDocumentLength(long bitLength)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(bitLength);
        _bitLength = bitLength;
        bool changed = _bits.RemoveWhere(bit => bit >= bitLength) > 0;
        // Work out repaired navigation state before publishing one coherent change.
        long active = ActiveBit >= bitLength ? (_bits.Count > 0 ? _bits.Max : -1) : ActiveBit;
        long anchor = Anchor >= bitLength ? active : Anchor;
        if (changed || active != ActiveBit || anchor != Anchor)
        {
            ActiveBit = active;
            Anchor = anchor;
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>Clears membership and both navigation positions, then notifies observers.</summary>
    public void Clear() => Commit([], -1, -1);

    /// <summary>Replaces membership, ignoring invalid addresses and duplicates, if the result fits the limit.</summary>
    /// <returns>False on a limit violation; true after committing the valid, possibly empty result.</returns>
    public bool SetSelection(IEnumerable<long> bits)
    {
        ArgumentNullException.ThrowIfNull(bits);
        // Build separately: a rejected request must not leave half a selection behind.
        var replacement = ReadBounded(bits);
        if (replacement is null) return Reject();
        Commit(replacement, replacement.Count > 0 ? replacement.Min : -1, replacement.Count > 0 ? replacement.Max : -1);
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
        if (_bitLength == 0) return false;
        start = Math.Clamp(start, 0, _bitLength - 1);
        end = Math.Clamp(end, 0, _bitLength - 1);
        // Membership is ascending, but the original start/end still describe the
        // user's drag direction and become Anchor/ActiveBit at commit time.
        long first = Math.Min(start, end);
        long last = Math.Max(start, end);
        if (wholeBytes)
        {
            // Divide then multiply to round down to a byte's first bit. Adding
            // seven reaches its last bit; Min also supports a partial final byte.
            first = first / 8 * 8;
            last = Math.Min(last / 8 * 8 + 7, _bitLength - 1);
        }
        // The +1 counts both endpoints. Reject a large range before allocating
        // its members, then also check the union with any preserved selections.
        if (last - first + 1 > MaximumSelectedBits) return Reject();
        var replacement = preserve is null ? [] : ReadBounded(preserve);
        if (replacement is null) return Reject();
        for (long bit = first; bit <= last; bit++)
        {
            replacement.Add(bit);
            if (replacement.Count > MaximumSelectedBits) return Reject();
        }
        Commit(replacement, start, end);
        return true;
    }

    /// <summary>Applies pointer or navigation selection without exposing mutable anchor state.</summary>
    /// <param name="bit">The clicked or navigated-to physical address.</param>
    /// <param name="wholeByte">Whether the action selects an entire byte instead of one bit.</param>
    /// <param name="extend">Shift behavior: retain the old anchor and select through this address.</param>
    /// <param name="toggle">Ctrl behavior: add/remove a group, or preserve old groups while extending.</param>
    public bool SelectAt(long bit, bool wholeByte, bool extend = false, bool toggle = false)
    {
        if (bit < 0 || bit >= _bitLength) return false;
        long anchor = extend && Anchor >= 0 ? Anchor : bit;
        if (extend) return SelectRange(anchor, bit, wholeByte, toggle ? _bits : null);
        if (!toggle) return SelectRange(bit, bit, wholeByte);

        long first = wholeByte ? bit / 8 * 8 : bit;
        long last = wholeByte ? Math.Min(first + 7, _bitLength - 1) : bit;
        var replacement = new SortedSet<long>(_bits);
        // Ctrl-click removes a complete selected group. For a partly selected
        // byte, it first fills in all eight bits instead of inverting each one.
        bool allSelected = true;
        for (long address = first; address <= last; address++) allSelected &= _bits.Contains(address);
        for (long address = first; address <= last; address++)
            if (allSelected) replacement.Remove(address); else replacement.Add(address);
        if (replacement.Count > MaximumSelectedBits) return Reject();
        Commit(replacement, anchor, bit);
        return true;
    }

    /// <summary>Moves the cursor without changing membership or the range anchor.</summary>
    /// <remarks>Invalid addresses are ignored. This is useful when editing a byte already under the pointer.</remarks>
    public void SetActiveBit(long bit)
    {
        if (bit < 0 || bit >= _bitLength || bit == ActiveBit) return;
        ActiveBit = bit;
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
        if (_bitLength == 0) return false;
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(bytesPerRow);
        long current = Math.Max(0, ActiveBit);
        long step = individualBits ? 1 : 8;
        // Convert row size to bits once, since every navigation address uses that
        // unit. The L makes the multiplication use long arithmetic from the start.
        long rowBits = bytesPerRow * 8L;
        long next = direction switch
        {
            SelectionNavigation.Left => current - step,
            SelectionNavigation.Right => current + step,
            SelectionNavigation.Up => current - rowBits,
            SelectionNavigation.Down => current + rowBits,
            SelectionNavigation.Home => documentBoundary ? 0 : current / rowBits * rowBits,
            SelectionNavigation.End => documentBoundary ? _bitLength - 1 : current / rowBits * rowBits + rowBits - step,
            SelectionNavigation.PageUp => current - Math.Max(1, visibleRows) * rowBits,
            SelectionNavigation.PageDown => current + Math.Max(1, visibleRows) * rowBits,
            _ => throw new ArgumentOutOfRangeException(nameof(direction))
        };
        next = Math.Clamp(next, 0, _bitLength - 1);
        // Byte navigation lands on a byte's first physical bit; SelectAt then
        // expands that position into a whole-byte selection when appropriate.
        if (!individualBits) next = next / 8 * 8;
        return SelectAt(next, !individualBits, extend);
    }

    /// <summary>Collects valid distinct addresses and returns null as soon as their count exceeds the cap.</summary>
    /// <remarks>The input is expected to be a finite sequence; duplicates do not consume selection capacity.</remarks>
    private SortedSet<long>? ReadBounded(IEnumerable<long> bits)
    {
        var result = new SortedSet<long>();
        foreach (long bit in bits)
        {
            if (bit >= 0 && bit < _bitLength) result.Add(bit);
            if (result.Count > MaximumSelectedBits) return null;
        }
        return result;
    }

    /// <summary>Installs already-validated state and sends one notification after all three parts agree.</summary>
    // Keep the same set instance so previously obtained read-only views remain live.
    private void Commit(SortedSet<long> bits, long anchor, long active)
    {
        _bits.Clear();
        _bits.UnionWith(bits);
        Anchor = anchor;
        ActiveBit = active;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Reports a rejected request without clearing membership or moving the cursor.</summary>
    private bool Reject()
    {
        SelectionLimitReached?.Invoke(this, EventArgs.Empty);
        return false;
    }

    /// <summary>Exposes enumeration and Count while hiding SortedSet's mutation methods.</summary>
    // Returning the set as IReadOnlyCollection alone would not be enough: callers
    // could cast it back to a mutable collection. This separate wrapper prevents that.
    private sealed class ReadOnlyBits(SortedSet<long> bits) : IReadOnlyCollection<long>
    {
        public int Count => bits.Count;
        public IEnumerator<long> GetEnumerator() => bits.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
