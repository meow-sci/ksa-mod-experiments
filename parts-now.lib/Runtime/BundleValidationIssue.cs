// THREADING RULE (repeated in every parts-now file):
// Everything runs on the game thread except RuntimeModLoader's loader step, which runs on a
// Task.Run worker. The worker touches only ILoader.Load(). Completion is polled from Update(dt).
// Do NOT use MeowSci.KsaAbstractions.GameThread — its queue is only drained when
// unladen-swallow.lib is present, and parts-now must work standalone.

namespace MeowSci.PartsNowLib;

/// <summary>How badly a validation finding affects a load.</summary>
public enum IssueSeverity
{
    /// <summary>Blocks the load. Nothing is written, registered or bound.</summary>
    Error,

    /// <summary>The load may proceed, but the result is degraded in a way the user should see.</summary>
    Warning,
}

/// <summary>
/// One validation finding, tagged with the rule that produced it so the UI can group and explain it.
/// </summary>
/// <param name="Severity">Whether this blocks the load.</param>
/// <param name="Rule">The rule number, <c>"V1"</c> through <c>"V15"</c>.</param>
/// <param name="Message">An actionable, human-readable description of the problem.</param>
/// <param name="ElementId">
/// The offending element's id (or a best-effort label such as a file path or line reference) so the
/// user can find it in their XML. Empty when the finding is about the document as a whole.
/// </param>
/// <param name="SourceName">The submitted document the finding came from.</param>
public sealed record ValidationIssue(
    IssueSeverity Severity,
    string Rule,
    string Message,
    string ElementId,
    string SourceName);
