namespace Jellyfin.Plugin.Lazyfin.Enhancements.RtlFix;

/// <summary>
/// The text subtitle formats <see cref="RtlLineRule.Apply"/> reads. Each one says where the lines are and which markup they carry.
/// </summary>
/// <remarks>
/// This is the rule's own list, not a Jellyfin type or a file extension. New formats are added at the end.
/// </remarks>
public enum SubtitleTextFormat
{
    /// <summary>
    /// Jellyfin's JSON subtitle, an object whose <c>TrackEvents</c> array holds one cue per item, with its lines in <c>Text</c>.
    /// </summary>
    Json,

    /// <summary>
    /// SubRip, or SRT: numbered cues whose lines follow a timing line, up to a blank line.
    /// </summary>
    SubRip,

    /// <summary>
    /// Advanced SubStation Alpha. The rule leaves it as authored.
    /// </summary>
    Ass,

    /// <summary>
    /// SubStation Alpha. The rule leaves it as authored.
    /// </summary>
    Ssa,
}
