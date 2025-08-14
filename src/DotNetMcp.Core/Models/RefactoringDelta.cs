namespace DotNetMcp.Core.Models;

public record RefactoringDelta(
    string FilePath,
    List<TextChange> Changes,
    string? NewMethodSignature = null,
    string[]? AffectedVariables = null);

public record TextChange(
    int StartLine,
    int EndLine,
    string OriginalText,
    string NewText,
    ChangeType Type);

public enum ChangeType
{
    Replace,
    Insert,
    Delete
}

public record CompactRefactoringResult(
    bool Success,
    List<RefactoringDelta> Deltas,
    string? ErrorMessage = null,
    RefactoringSummary? Summary = null);

public record RefactoringSummary(
    string MethodName,
    string ReturnType,
    string[] Parameters,
    int TokensSaved);